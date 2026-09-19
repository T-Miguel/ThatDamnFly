// Localization in our own JSON (no Addressables). Stable keys; PT-PT and EN. System language by default, EN as fallback.
using System.Collections.Generic;
using UnityEngine;

namespace ThatDamnFly.Simulation
{
    public static class Loc
    {
        public static string Lang { get; private set; } = "en";
        static readonly Dictionary<string, string> _t = new Dictionary<string, string>();
        public static readonly string[] Supported = { "pt-PT", "en" };

        public static string SystemDefault()
        {
            var l = Application.systemLanguage;
            return l == SystemLanguage.Portuguese ? "pt-PT" : "en";
        }

        public static void Load(string lang)
        {
            if (System.Array.IndexOf(Supported, lang) < 0) lang = "en";
            var ta = Resources.Load<TextAsset>("content/strings." + lang);
            if (ta == null && lang != "en") { ta = Resources.Load<TextAsset>("content/strings.en"); lang = "en"; }
            _t.Clear(); Lang = lang;
            if (ta != null) ParseFlatJson(ta.text, _t);
        }

        public static string T(string key) => _t.TryGetValue(key, out var v) ? v : key;
        public static string F(string key, params object[] args) { var s = T(key); for (int i = 0; i < args.Length; i++) s = s.Replace("{" + i + "}", args[i]?.ToString() ?? ""); return s; }

        // Minimal parser for a flat JSON object {"k":"v",...} with escapes \n \" \\ \uXXXX (avoids dependencies).
        static void ParseFlatJson(string json, Dictionary<string, string> into)
        {
            int i = 0; string key = null;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '"')
                {
                    var sb = new System.Text.StringBuilder(); i++;
                    while (i < json.Length && json[i] != '"')
                    {
                        if (json[i] == '\\' && i + 1 < json.Length)
                        {
                            char e = json[++i];
                            if (e == 'n') sb.Append('\n'); else if (e == 't') sb.Append('\t'); else if (e == 'u' && i + 4 < json.Length) { sb.Append((char)System.Convert.ToInt32(json.Substring(i + 1, 4), 16)); i += 4; } else sb.Append(e);
                        }
                        else sb.Append(json[i]);
                        i++;
                    }
                    i++;
                    if (key == null) key = sb.ToString(); else { into[key] = sb.ToString(); key = null; }
                }
                else i++;
            }
        }
    }
}
