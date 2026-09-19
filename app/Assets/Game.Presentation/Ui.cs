// Programmatic uGUI construction (no prefabs at this stage). Colour tokens from UI/UX 1.3 §05.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThatDamnFly.Presentation
{
    public static class Palette
    {
        public static readonly Color Paper = Hex("#F7F0DE"), Ink = Hex("#202622"), Action = Hex("#F4CD3C"), Fury = Hex("#AF3D2F"), Info = Hex("#2755A5"), Surface = Hex("#CBD3B9"), Muted = Hex("#555E55"), White = Color.white;
        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    }

    public static class Ui
    {
        public static TMP_FontAsset Font, FontBold, FontDisplay;
        public static void LoadFonts()
        {
            Font = Resources.Load<TMP_FontAsset>("fonts/Archivo-Regular SDF") ?? TMP_Settings.defaultFontAsset;
            FontBold = Resources.Load<TMP_FontAsset>("fonts/Archivo-SemiBold SDF") ?? Font;
            FontDisplay = Resources.Load<TMP_FontAsset>("fonts/Archivo-Black SDF") ?? FontBold;
        }

        public static RectTransform Panel(Transform parent, string name, Color color, bool raycast = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = raycast;
            return go.GetComponent<RectTransform>();
        }

        public static RectTransform Empty(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go.GetComponent<RectTransform>();
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style; t.raycastTarget = false;
            bool bold = (style & FontStyles.Bold) != 0; var f = bold ? (size >= 26 ? FontDisplay : FontBold) : Font;
            if (f != null) { t.font = f; if (bold && f != Font) t.fontStyle = style & ~FontStyles.Bold; }
            t.enableWordWrapping = true;
            return t;
        }

        public static Button Button(Transform parent, string name, string label, Color bg, Color fg, float fontSize, System.Action onClick)
        {
            var rt = Panel(parent, name, bg); var go = rt.gameObject;
            var outline = go.AddComponent<Outline>(); outline.effectColor = Palette.Ink; outline.effectDistance = new Vector2(2, -2);
            var btn = go.AddComponent<Button>(); btn.targetGraphic = go.GetComponent<Image>();
            var colors = btn.colors; colors.highlightedColor = bg * 0.95f; colors.pressedColor = bg * 0.85f; colors.disabledColor = new Color(bg.r, bg.g, bg.b, 0.45f); btn.colors = colors;
            var t = Text(go.transform, "Label", label, fontSize, fg, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(t.rectTransform, 8, 4, 8, 4);
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static void Stretch(RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        public static Sprite CircleSprite(int px = 128)
        {
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false); var c = new Color32[px * px]; float r = px * 0.5f - 1f, cx = px * 0.5f - 0.5f;
            for (int y = 0; y < px; y++) for (int x = 0; x < px; x++) { float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cx) * (y - cx)); float a = Mathf.Clamp01(r - d + 0.5f); c[y * px + x] = new Color32(255, 255, 255, (byte)(a * 255)); }
            tex.SetPixels32(c); tex.Apply(); tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), px);
        }

        static Sprite _rounded, _vignette;
        /// <summary>Radial gradient (transparent at the center, opaque at the corners) for screen vignettes.</summary>
        public static Sprite VignetteSprite()
        {
            if (_vignette != null) return _vignette;
            const int n = 96; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false); var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f; float d = Mathf.Sqrt(dx * dx + dy * dy) / 0.7071f;
                float a = Mathf.Clamp01((d - 0.45f) / 0.55f); a = a * a; px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply(); tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            return _vignette = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        /// <summary>9-sliced rounded rectangle (radius ≈ 18 u at 1×): cards, buttons and thumbnails with soft corners.</summary>
        public static Sprite RoundedSprite()
        {
            if (_rounded != null) return _rounded;
            const int px = 64, r = 18; var tex = new Texture2D(px, px, TextureFormat.RGBA32, false); var c = new Color32[px * px];
            for (int y = 0; y < px; y++) for (int x = 0; x < px; x++)
            {
                float dx = Mathf.Max(0, Mathf.Max(r - x - 0.5f, x + 0.5f - (px - r))), dy = Mathf.Max(0, Mathf.Max(r - y - 0.5f, y + 0.5f - (px - r)));
                float d = Mathf.Sqrt(dx * dx + dy * dy); float a = Mathf.Clamp01(r - d + 0.5f);
                c[y * px + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(c); tex.Apply(); tex.filterMode = FilterMode.Bilinear;
            _rounded = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), px, 0, SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
            return _rounded;
        }

        /// <summary>Rounded card: background + ink outline (image behind, slightly larger) + hard cartoon-style shadow.</summary>
        public static RectTransform Card(Transform parent, string name, Color fill, float outline = 3f, Vector2? shadow = null, bool raycast = true)
        {
            var host = Empty(parent, name);
            if (shadow.HasValue) { var sh = Panel(host, "Shadow", Palette.Ink, false); Stretch(sh); sh.anchoredPosition = shadow.Value; var si = sh.GetComponent<Image>(); si.sprite = RoundedSprite(); si.type = Image.Type.Sliced; }
            var ol = Panel(host, "Outline", Palette.Ink, false); Stretch(ol); var oi = ol.GetComponent<Image>(); oi.sprite = RoundedSprite(); oi.type = Image.Type.Sliced;
            var fillRt = Panel(host, "Fill", fill, raycast); Stretch(fillRt, outline, outline, outline, outline); var fi = fillRt.GetComponent<Image>(); fi.sprite = RoundedSprite(); fi.type = Image.Type.Sliced;
            return host;
        }

        /// <summary>Rounded button with a hard shadow; returns the Button (on the fill) and the host for positioning.</summary>
        public static Button RoundButton(Transform parent, string name, string label, Color bg, Color fg, float fontSize, System.Action onClick, out RectTransform host, Sprite icon = null, float iconSize = 28f)
        {
            host = Card(parent, name, bg, 3f, new Vector2(4, -4));
            var fill = host.Find("Fill").GetComponent<Image>(); var btn = fill.gameObject.AddComponent<Button>(); btn.targetGraphic = fill;
            var colors = btn.colors; colors.highlightedColor = bg * 0.95f; colors.pressedColor = bg * 0.85f; colors.disabledColor = new Color(bg.r, bg.g, bg.b, 0.45f); btn.colors = colors;
            if (icon != null)
            {
                var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image)); ig.transform.SetParent(fill.transform, false); var img = ig.GetComponent<Image>(); img.sprite = icon; img.preserveAspect = true; img.raycastTarget = false;
                Place(ig.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(iconSize, iconSize));
                var t = Text(fill.transform, "Label", label, fontSize, fg, TextAlignmentOptions.Left, FontStyles.Bold); Stretch(t.rectTransform, 14 + iconSize + 10, 4, 10, 4);
            }
            else { var t = Text(fill.transform, "Label", label, fontSize, fg, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(t.rectTransform, 8, 4, 8, 4); }
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static Sprite SquareSprite()
        {
            var tex = Texture2D.whiteTexture; return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        }
    }
}
