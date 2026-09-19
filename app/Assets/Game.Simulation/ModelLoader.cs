// Fetching and verifying the neural package. Expected hash pinned in the build (ModelConfig). Never activates a fly without an intact model.
using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

namespace ThatDamnFly.Simulation
{
    /// <summary>Versions and hash pinned in the build (values generated in ModelConfigValues). An incompatible model update requires a new build.</summary>
    public static class ModelConfig
    {
        public const string ModelId = ModelConfigValues.ModelId;
        public const string FileName = ModelConfigValues.FileName;
        public const long ExpectedBytes = ModelConfigValues.ExpectedBytes;
    }

    public enum ModelLoadState { Idle, Downloading, Verifying, Ready, Failed }

    public sealed class ModelLoader
    {
        public ModelLoadState State { get; private set; } = ModelLoadState.Idle;
        public float Progress { get; private set; }
        public string Error { get; private set; }
        public byte[] Bytes { get; private set; }
        public bool FromCache { get; private set; }
        public string ModelId { get; private set; }
        public uint NeuronCount { get; private set; }

        public static string BaseUrl
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                // same origin as the page: <page>/model/<id>/
                string url = Application.absoluteURL; int q = url.IndexOf('?'); if (q >= 0) url = url.Substring(0, q); int h = url.IndexOf('#'); if (h >= 0) url = url.Substring(0, h);
                if (!url.EndsWith("/")) url = url.Substring(0, url.LastIndexOf('/') + 1);
                return url + "model/" + ModelConfig.ModelId + "/";
#else
                return ModelConfigValues.RemoteBaseUrl + ModelConfig.ModelId + "/";
#endif
            }
        }

        static string CachePath => Path.Combine(Application.persistentDataPath, "model", ModelConfig.FileName);

        public IEnumerator Load()
        {
            State = ModelLoadState.Downloading; Progress = 0f; Error = null; Bytes = null; FromCache = false;
            // 1) local cache (apps). On the web, the browser cache is reused by the HTTP request.
#if !UNITY_WEBGL
            var cached = TryReadCache();
            if (cached != null) { Bytes = cached; FromCache = true; State = ModelLoadState.Ready; Progress = 1f; yield break; }
#endif
            // 2) download
            string url = BaseUrl + ModelConfig.FileName;
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 60;
                var op = req.SendWebRequest();
                while (!op.isDone) { Progress = req.downloadProgress; yield return null; }
                if (req.result != UnityWebRequest.Result.Success) { Fail("download: " + req.error + " (" + url + ")"); yield break; }
                var data = req.downloadHandler.data;
                State = ModelLoadState.Verifying; yield return null;
                if (!Verify(data, out var verr)) { Fail(verr); yield break; }
                Bytes = data;
            }
#if !UNITY_WEBGL
            try { Directory.CreateDirectory(Path.GetDirectoryName(CachePath)); string tmp = CachePath + ".tmp"; File.WriteAllBytes(tmp, Bytes); if (File.Exists(CachePath)) File.Delete(CachePath); File.Move(tmp, CachePath); }
            catch (Exception e) { Debug.LogWarning("Could not cache the model: " + e.Message); }
#endif
            State = ModelLoadState.Ready; Progress = 1f;
        }

        byte[] TryReadCache()
        {
            try
            {
                if (!File.Exists(CachePath)) return null;
                var cached = File.ReadAllBytes(CachePath);
                if (Verify(cached, out var err)) return cached;
                Debug.LogWarning("Cached model invalid: " + err); File.Delete(CachePath);
            }
            catch (Exception e) { Debug.LogWarning("Model cache unavailable: " + e.Message); }
            return null;
        }

        bool Verify(byte[] data, out string error)
        {
            error = null;
            if (data == null || data.LongLength != ModelConfig.ExpectedBytes) { error = $"unexpected size {(data == null ? 0 : data.LongLength)} ≠ {ModelConfig.ExpectedBytes}"; return false; }
            string hex;
            using (var sha = SHA256.Create()) { var h = sha.ComputeHash(data); var sb = new System.Text.StringBuilder(64); foreach (var b in h) sb.Append(b.ToString("x2")); hex = sb.ToString(); }
            if (!string.Equals(hex, ModelConfigValues.ExpectedFileSha256, StringComparison.OrdinalIgnoreCase)) { error = "file hash does not match"; return false; }
            if (!FlyCoreInstance.TryInspect(data, out var id, out var n, out var st)) { error = "package rejected by FlyCore: " + st; return false; }
            if (id != ModelConfig.ModelId) { error = "unexpected model_id: " + id; return false; }
            ModelId = id; NeuronCount = n; return true;
        }

        void Fail(string msg) { State = ModelLoadState.Failed; Error = msg; Debug.LogError("model_load_failed: " + msg); }
    }
}
