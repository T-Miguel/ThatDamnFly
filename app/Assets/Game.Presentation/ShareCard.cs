// Share card (ADR-010): 1080×1350 PNG image with the logo, the fly, the number, the scene and the QR - the same on every platform.
// Rendered by a temporary camera on its own layer (31) into a RenderTexture; nothing stays in the scene.
using ThatDamnFly.Domain;
using ThatDamnFly.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThatDamnFly.Presentation
{
    public static class ShareCard
    {
        public const int Layer = 31, W = 1080, H = 1350;

        public static byte[] RenderPng(string big, string unit, string sub, string detail, SceneDef scene, FlyKind kind)
        {
            var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32);
            var camGo = new GameObject("ShareCardCam"); camGo.layer = Layer; camGo.transform.position = new Vector3(5000f, 5000f, -10f);
            var cam = camGo.AddComponent<Camera>(); cam.orthographic = true; cam.orthographicSize = H / 200f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Palette.Paper; cam.cullingMask = 1 << Layer; cam.targetTexture = rt; cam.enabled = false;
            var urp = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() ?? camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); urp.renderPostProcessing = false;
            var cgo = new GameObject("ShareCardCanvas", typeof(Canvas), typeof(CanvasScaler)); cgo.layer = Layer;
            var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 5f;
            var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(W, H); sc.matchWidthOrHeight = 0.5f;
            var root = cgo.GetComponent<RectTransform>();
            try
            {
                // background: scene strip dimmed at the bottom + paper
                var bg = Resources.Load<Sprite>("art/" + scene.Id + "_bg");
                if (bg != null) { var strip = Img(root, bg, new Color(1, 1, 1, 0.28f)); Place(strip, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(W, 620)); strip.GetComponent<Image>().preserveAspect = false; }
                var band = Img(root, Ui.SquareSprite(), Palette.Paper); Place(band, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(W, 730));
                // logo
                var logo = Resources.Load<Sprite>("brand/logo-stacked-color-v1");
                if (logo != null) Place(Img(root, logo, Color.white), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(520, 205));
                // fly (kind of the last catch) on the left, number on the right
                string prefix = kind == FlyKind.Bluebottle ? "fly_bb_" : (kind == FlyKind.FruitFly ? "fly_ff_" : (kind == FlyKind.Horsefly ? "fly_hf_" : (kind == FlyKind.Boss ? "fly_bs_" : (kind == FlyKind.Golden ? "fly_gd_" : "fly_top_"))));
                var fly = Resources.Load<Sprite>("art/" + prefix + "wings0");
                if (fly != null) { var f = Img(root, fly, Color.white); Place(f, new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, -300), new Vector2(340, 340)); f.localRotation = Quaternion.Euler(0, 0, 25f); }
                var num = Txt(root, big, 230, Palette.Ink, Ui.FontDisplay ?? Ui.FontBold); Place(num.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -290), new Vector2(620, 260)); num.alignment = TextAlignmentOptions.Right;
                var un = Txt(root, unit, 84, Palette.Fury, Ui.FontDisplay ?? Ui.FontBold); Place(un.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -545), new Vector2(620, 100)); un.alignment = TextAlignmentOptions.Right;
                var s1 = Txt(root, sub, 46, Palette.Ink, Ui.FontBold); Place(s1.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -680), new Vector2(960, 70));
                var s2 = Txt(root, detail, 38, Palette.Muted, Ui.Font); Place(s2.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -750), new Vector2(960, 60));
                // footer: QR + site + rights
                var qr = Resources.Load<Sprite>("brand/qr");
                var foot = Img(root, Ui.SquareSprite(), new Color(Palette.Paper.r, Palette.Paper.g, Palette.Paper.b, 0.92f)); Place(foot, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(W, 250));
                if (qr != null) Place(Img(root, qr, Color.white), new Vector2(0, 0), new Vector2(0, 0), new Vector2(70, 35), new Vector2(180, 180));
                var site = Txt(root, "thatdamnfly.com", 64, Palette.Ink, Ui.FontDisplay ?? Ui.FontBold); Place(site.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(280, 120), new Vector2(760, 90)); site.alignment = TextAlignmentOptions.Left;
                var rights = Txt(root, Loc.T("card.rights") + " · © 2026 Miguel Prego", 30, Palette.Muted, Ui.Font); Place(rights.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(280, 55), new Vector2(760, 60)); rights.alignment = TextAlignmentOptions.Left;
                foreach (var t in cgo.GetComponentsInChildren<TextMeshProUGUI>()) t.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                var req = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, req)) UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, req); else cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev;
                var png = tex.EncodeToPNG(); Object.Destroy(tex); return png;
            }
            finally
            {
                cam.targetTexture = null; RenderTexture.ReleaseTemporary(rt); Object.Destroy(cgo); Object.Destroy(camGo);
            }
        }

        static RectTransform Img(Transform parent, Sprite s, Color c)
        {
            var go = new GameObject("Img", typeof(RectTransform), typeof(Image)); go.layer = Layer; go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.sprite = s; img.color = c; img.preserveAspect = true; img.raycastTarget = false; return go.GetComponent<RectTransform>();
        }
        static TextMeshProUGUI Txt(Transform parent, string text, float size, Color color, TMP_FontAsset font)
        {
            var go = new GameObject("Txt", typeof(RectTransform), typeof(TextMeshProUGUI)); go.layer = Layer; go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = color; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false; if (font != null) t.font = font;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow; t.enableAutoSizing = true; t.fontSizeMin = size * 0.5f; t.fontSizeMax = size; return t;
        }
        static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size) { rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size; }
    }
}
