// Platform adapters: sharing (text + canonical URL), vibration, text copy. Never claim a confirmed send.
using System.Runtime.InteropServices;
using UnityEngine;

namespace ThatDamnFly.Simulation
{
    public enum ShareResult { SheetOpened, Copied, Failed }

    public static class Platform
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int TDF_Share(string text, string url);
        [DllImport("__Internal")] static extern int TDF_ShareImage(byte[] png, int len, string text, string url);
        [DllImport("__Internal")] static extern void TDF_Vibrate(int ms);
        [DllImport("__Internal")] static extern void TDF_DevFly(float x, float y, float w, float h, int s);
        [DllImport("__Internal")] static extern int TDF_CopyText(string text);
#elif UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void TDF_ShareIOS(string text, string url);
        [DllImport("__Internal")] static extern void TDF_ShareImageIOS(string path, string text, string url);
        [DllImport("__Internal")] static extern void TDF_VibrateIOS(int strong);
#endif

        public static bool VibrationSupported =>
#if UNITY_ANDROID || UNITY_IOS
            !Application.isEditor;
#elif UNITY_WEBGL && !UNITY_EDITOR
            true; // navigator.vibrate only exists on Android/Chrome; in other browsers it is harmless
#else
            false;
#endif

        public static ShareResult Share(string text, string url)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            int r = TDF_Share(text, url); return r == 2 ? ShareResult.SheetOpened : (r == 1 ? ShareResult.Copied : ShareResult.Failed);
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                    intent.Call<AndroidJavaObject>("setType", "text/plain");
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text + " " + url);
                    using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "That Damn Fly");
                        activity.Call("startActivity", chooser);
                    }
                }
                return ShareResult.SheetOpened;
            }
            catch (System.Exception e) { Debug.LogWarning("share: " + e.Message); return Copy(text + " " + url) ? ShareResult.Copied : ShareResult.Failed; }
#elif UNITY_IOS && !UNITY_EDITOR
            try { TDF_ShareIOS(text, url); return ShareResult.SheetOpened; } catch (System.Exception e) { Debug.LogWarning("share: " + e.Message); return Copy(text + " " + url) ? ShareResult.Copied : ShareResult.Failed; }
#else
            return Copy(text + " " + url) ? ShareResult.Copied : ShareResult.Failed;
#endif
        }

        /// <summary>Shares a PNG with text and link. Web: Web Share with a file, otherwise download + text copy. Android: saves to Pictures (MediaStore) and opens the chooser. iOS: share sheet with the image.</summary>
        public static ShareResult ShareImage(byte[] png, string text, string url)
        {
            if (png == null || png.Length == 0) return Share(text, url);
#if UNITY_WEBGL && !UNITY_EDITOR
            int r = TDF_ShareImage(png, png.Length, text, url); return r == 2 ? ShareResult.SheetOpened : (r == 1 ? ShareResult.Copied : Share(text, url));
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
                using (var values = new AndroidJavaObject("android.content.ContentValues"))
                using (var mediaClass = new AndroidJavaClass("android.provider.MediaStore$Images$Media"))
                {
                    values.Call("put", "_display_name", "thatdamnfly-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
                    values.Call("put", "mime_type", "image/png");
                    values.Call("put", "relative_path", "Pictures/That Damn Fly");
                    var uri = resolver.Call<AndroidJavaObject>("insert", mediaClass.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"), values);
                    using (var os = resolver.Call<AndroidJavaObject>("openOutputStream", uri)) { os.Call("write", (object)png); os.Call("flush"); os.Call("close"); }
                    using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                    using (var intent = new AndroidJavaObject("android.content.Intent"))
                    {
                        intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                        intent.Call<AndroidJavaObject>("setType", "image/png");
                        intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_STREAM"), uri);
                        intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text + " " + url);
                        intent.Call<AndroidJavaObject>("addFlags", intentClass.GetStatic<int>("FLAG_GRANT_READ_URI_PERMISSION"));
                        var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "That Damn Fly");
                        activity.Call("startActivity", chooser);
                    }
                }
                return ShareResult.SheetOpened;
            }
            catch (System.Exception e) { Debug.LogWarning("share image: " + e.Message); return Share(text, url); }
#elif UNITY_IOS && !UNITY_EDITOR
            try { string path = System.IO.Path.Combine(Application.temporaryCachePath, "thatdamnfly.png"); System.IO.File.WriteAllBytes(path, png); TDF_ShareImageIOS(path, text, url); return ShareResult.SheetOpened; }
            catch (System.Exception e) { Debug.LogWarning("share image: " + e.Message); return Share(text, url); }
#else
            try { string path = System.IO.Path.Combine(Application.persistentDataPath, "thatdamnfly-share.png"); System.IO.File.WriteAllBytes(path, png); Debug.Log("[TDF] card saved at " + path); } catch { }
            return Share(text, url);
#endif
        }

        public static bool Copy(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return TDF_CopyText(text) == 1;
#else
            try { GUIUtility.systemCopyBuffer = text; return true; } catch { return false; }
#endif
        }

        /// <summary>Development builds only (headless verification): publishes the first active fly's screen position to the page (window.tdfFly), or (-1,-1) when none.</summary>
        public static void DevFly(float x, float y, int state)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            TDF_DevFly(x, y, Screen.width, Screen.height, state);
#endif
        }

        public static void Vibrate(bool strong)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            TDF_Vibrate(strong ? 40 : 15);
#elif UNITY_IOS && !UNITY_EDITOR
            TDF_VibrateIOS(strong ? 1 : 0);
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vib = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vib != null && vib.Call<bool>("hasVibrator")) vib.Call("vibrate", (long)(strong ? 40 : 15));
                }
            }
            catch { }
#endif
        }
    }
}
