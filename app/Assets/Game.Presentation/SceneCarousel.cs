// Scene selector (ADR-011, visual ADR-012): full-width carousel with a rounded hero card, smaller faded neighbours,
// page dots, arrows, a fly landed on the chosen card (beating wings). Drag with inertia and snapping; tapping a card chooses it.
using System;
using System.Collections.Generic;
using ThatDamnFly.Domain;
using ThatDamnFly.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ThatDamnFly.Presentation
{
    public sealed class SceneCarousel : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        public const float CardW = 210f, CardH = 280f, Gap = 22f;
        ScrollRect _scroll; RectTransform _content, _viewport;
        sealed class Card { public RectTransform Host; public Image Fill, Outline; public TextMeshProUGUI Name, Best; public RectTransform Chip; public SceneDef Scene; public Image[] Stars; public RectTransform Lock; public TextMeshProUGUI LockText; }
        readonly List<Card> _cards = new List<Card>();
        readonly List<Image> _dots = new List<Image>();
        int _selected; bool _dragging; float _target; bool _snapping;
        Action<SceneDef> _onSelect; Func<SceneDef, int> _bestOf, _starMaskOf; Func<SceneDef, bool> _unlockedOf; Sprite _starOn, _starOff;
        Image _fly; Sprite[] _flyFrames; float _wingT; int _wingFrame; float _bob; Vector2 _anchor;
        public SceneDef Selected => _cards[_selected].Scene;

        public static SceneCarousel Build(RectTransform parent, Vector2 pos, float width, SceneDef initial, Action<SceneDef> onSelect, Func<SceneDef, int> bestOf, Action tap, Vector2? anchorOpt = null, Func<SceneDef, int> starMaskOf = null, Func<SceneDef, bool> unlockedOf = null)
        {
            var anchor = anchorOpt ?? new Vector2(0.5f, 0.5f);
            var go = new GameObject("SceneCarousel", typeof(RectTransform), typeof(Image), typeof(ScrollRect)); go.transform.SetParent(parent, false);
            var c = go.AddComponent<SceneCarousel>(); c._onSelect = onSelect; c._bestOf = bestOf; c._starMaskOf = starMaskOf; c._unlockedOf = unlockedOf;
            c._starOn = Resources.Load<Sprite>("art/icon_star"); c._starOff = Resources.Load<Sprite>("art/icon_star_empty");
            c._viewport = go.GetComponent<RectTransform>(); Ui.Place(c._viewport, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(width, CardH + 24)); c._anchor = anchor;
            go.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);   // catches the drag; no mask (the neighbours peek in, faded)
            c._content = Ui.Empty(go.transform, "Content"); c._content.anchorMin = new Vector2(0, 0.5f); c._content.anchorMax = new Vector2(0, 0.5f); c._content.pivot = new Vector2(0, 0.5f);
            c._scroll = go.GetComponent<ScrollRect>(); c._scroll.content = c._content; c._scroll.viewport = c._viewport; c._scroll.horizontal = true; c._scroll.vertical = false; c._scroll.movementType = ScrollRect.MovementType.Elastic; c._scroll.inertia = true; c._scroll.decelerationRate = 0.05f; c._scroll.scrollSensitivity = 20f;
            int n = Scenes.All.Length; float pad = (width - CardW) * 0.5f;
            c._content.sizeDelta = new Vector2(pad * 2 + n * CardW + (n - 1) * Gap, CardH);
            for (int i = 0; i < n; i++)
            {
                var sc = Scenes.All[i]; int idx = i;
                var host = Ui.Card(c._content, "Card_" + sc.Id, Palette.Paper, 3f, new Vector2(5, -5));
                host.anchorMin = new Vector2(0, 0.5f); host.anchorMax = new Vector2(0, 0.5f); host.pivot = new Vector2(0.5f, 0.5f); host.anchoredPosition = new Vector2(pad + i * (CardW + Gap) + CardW * 0.5f, 0); host.sizeDelta = new Vector2(CardW, CardH);
                var fill = host.Find("Fill").GetComponent<Image>(); var btn = fill.gameObject.AddComponent<Button>(); btn.targetGraphic = fill; btn.onClick.AddListener(() => { tap?.Invoke(); c.Select(idx, true); });
                var colors = btn.colors; colors.highlightedColor = Color.white; colors.pressedColor = new Color(0.9f, 0.9f, 0.9f); btn.colors = colors;
                // thumbnail with rounded corners (mask) at the top of the card
                var mgo = new GameObject("ThumbMask", typeof(RectTransform), typeof(Image), typeof(Mask)); mgo.transform.SetParent(fill.transform, false);
                var mi = mgo.GetComponent<Image>(); mi.sprite = Ui.RoundedSprite(); mi.type = Image.Type.Sliced; mi.color = Color.white; mi.raycastTarget = false; mgo.GetComponent<Mask>().showMaskGraphic = false;
                var mrt = mgo.GetComponent<RectTransform>(); Ui.Place(mrt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(CardW - 26, 190));
                var tgo = new GameObject("Thumb", typeof(RectTransform), typeof(Image)); tgo.transform.SetParent(mgo.transform, false); var img = tgo.GetComponent<Image>(); img.sprite = Resources.Load<Sprite>("art/" + sc.Id + "_bg"); img.preserveAspect = false; img.raycastTarget = false;
                var trt = tgo.GetComponent<RectTransform>(); Ui.Stretch(trt); trt.offsetMin = new Vector2(0, -20); trt.offsetMax = new Vector2(0, 0);   // 3:4 → soft crop top/bottom
                var name = Ui.Text(fill.transform, "Name", Loc.T("scene." + sc.Id), 20, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Place(name.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(CardW - 20, 30));
                // indoor/outdoor chip + record
                var chip = Ui.Card(fill.transform, "Chip", sc.Outdoor ? Palette.Action : Palette.Surface, 2f, null, false); Ui.Place(chip, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(CardW - 40, 24));
                var kind = Ui.Text(chip, "Kind", Loc.T(sc.Outdoor ? "scene.outdoor" : "scene.indoor"), 11, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Stretch(kind.rectTransform);
                // record in a dark pill at the top of the thumbnail (it used to sit over the scene name)
                var bpill = Ui.Panel(fill.transform, "BestPill", new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.6f), false); var bpi = bpill.GetComponent<Image>(); bpi.sprite = Ui.RoundedSprite(); bpi.type = Image.Type.Sliced; Ui.Place(bpill, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(112, 22));
                var best = Ui.Text(bpill, "Best", "", 11, Palette.Paper, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Stretch(best.rectTransform);
                // ADR-014: stars (fulfilled requests) in a dark pill at the bottom of the thumbnail; lock with the condition to open
                var spill = Ui.Panel(fill.transform, "Stars", new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.55f), false); var spi = spill.GetComponent<Image>(); spi.sprite = Ui.RoundedSprite(); spi.type = Image.Type.Sliced; Ui.Place(spill, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(96, 28));
                var stars = new Image[3];
                for (int s = 0; s < 3; s++) { var sgo = new GameObject("S" + s, typeof(RectTransform), typeof(Image)); sgo.transform.SetParent(spill, false); stars[s] = sgo.GetComponent<Image>(); stars[s].preserveAspect = true; stars[s].raycastTarget = false; Ui.Place(sgo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((s - 1) * 29, 0), new Vector2(23, 23)); }
                var lockRt = Ui.Panel(fill.transform, "Lock", new Color(Palette.Paper.r, Palette.Paper.g, Palette.Paper.b, 0.8f), false); Ui.Stretch(lockRt); var li = lockRt.GetComponent<Image>(); li.sprite = Ui.RoundedSprite(); li.type = Image.Type.Sliced;
                var lgo = new GameObject("Icon", typeof(RectTransform), typeof(Image)); lgo.transform.SetParent(lockRt, false); var lim = lgo.GetComponent<Image>(); lim.sprite = Resources.Load<Sprite>("art/icon_lock"); lim.preserveAspect = true; lim.raycastTarget = false; Ui.Place(lgo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 36), new Vector2(60, 60));
                var ltxt = Ui.Text(lockRt, "Text", "", 13, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Place(ltxt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -16), new Vector2(CardW - 44, 48));
                if (sc.Boss) { var bchip = Ui.Card(fill.transform, "BossChip", Palette.Fury, 2f, null, false); Ui.Place(bchip, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(84, 22)); var bt = Ui.Text(bchip, "T", Loc.T("scene.final"), 11, Palette.Paper, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Stretch(bt.rectTransform); }
                c._cards.Add(new Card { Host = host, Fill = fill, Outline = host.Find("Outline").GetComponent<Image>(), Name = name, Best = best, Chip = chip, Scene = sc, Stars = stars, Lock = lockRt, LockText = ltxt });
            }
            // fly landed on the corner of the chosen card (above everything)
            c._flyFrames = new[] { Resources.Load<Sprite>("art/fly_top_wings0"), Resources.Load<Sprite>("art/fly_top_wings1"), Resources.Load<Sprite>("art/fly_top_wings2") };
            var fgo = new GameObject("Fly", typeof(RectTransform), typeof(Image)); fgo.transform.SetParent(parent, false); c._fly = fgo.GetComponent<Image>(); c._fly.sprite = c._flyFrames[0]; c._fly.preserveAspect = true; c._fly.raycastTarget = false;
            Ui.Place(fgo.GetComponent<RectTransform>(), anchor, new Vector2(0.5f, 0.5f), new Vector2(pos.x + CardW * 0.5f - 6, pos.y + CardH * 0.5f - 8), new Vector2(64, 64)); fgo.transform.localRotation = Quaternion.Euler(0, 0, -35f);
            // page dots
            var dots = Ui.Empty(parent, "Dots"); Ui.Place(dots, anchor, new Vector2(0.5f, 0.5f), new Vector2(0, pos.y - CardH * 0.5f - 22), new Vector2(n * 18, 10));
            for (int i = 0; i < n; i++) { var d = Ui.Panel(dots, "Dot" + i, Palette.Ink, false); var di = d.GetComponent<Image>(); di.sprite = Ui.CircleSprite(32); Ui.Place(d, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(i * 18 + 9, 0), new Vector2(8, 8)); c._dots.Add(di); }
            // round arrows (mouse/keyboard)
            var left = Ui.RoundButton(parent, "CarouselLeft", "‹", Palette.Paper, Palette.Ink, 28, () => { tap?.Invoke(); c.Select(Mathf.Max(0, c._selected - 1), true); }, out var lrt); Ui.Place(lrt, anchor, new Vector2(0.5f, 0.5f), new Vector2(pos.x - width * 0.5f + 26, pos.y), new Vector2(44, 44));
            var right = Ui.RoundButton(parent, "CarouselRight", "›", Palette.Paper, Palette.Ink, 28, () => { tap?.Invoke(); c.Select(Mathf.Min(n - 1, c._selected + 1), true); }, out var rrt); Ui.Place(rrt, anchor, new Vector2(0.5f, 0.5f), new Vector2(pos.x + width * 0.5f - 26, pos.y), new Vector2(44, 44));
            int init = Mathf.Max(0, Array.IndexOf(Scenes.All, initial)); c.Refresh(); c.Select(init, false); c._content.anchoredPosition = new Vector2(c.TargetX(init), 0); c.LayoutCards();
            return c;
        }

        float TargetX(int idx) => -idx * (CardW + Gap);
        public void Refresh()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                var cd = _cards[i]; int b = _bestOf != null ? _bestOf(cd.Scene) : 0; cd.Best.text = b > 0 ? Loc.F("scene.best", b) : ""; cd.Best.transform.parent.gameObject.SetActive(b > 0);
                int mask = _starMaskOf != null ? _starMaskOf(cd.Scene) : 0;
                for (int s = 0; s < 3; s++) cd.Stars[s].sprite = ((mask >> s) & 1) != 0 ? _starOn : _starOff;
                bool open = _unlockedOf == null || _unlockedOf(cd.Scene); cd.Lock.gameObject.SetActive(!open);
                if (!open && i > 0) cd.LockText.text = Loc.F("scene.unlock", Loc.T("scene." + Scenes.All[i - 1].Id));
            }
        }
        public bool IsUnlocked(SceneDef sc) => _unlockedOf == null || _unlockedOf(sc);

        public void Select(int idx, bool animate)
        {
            _selected = Mathf.Clamp(idx, 0, _cards.Count - 1);
            for (int i = 0; i < _cards.Count; i++) { _cards[i].Fill.color = i == _selected ? Palette.Action : Palette.Paper; _cards[i].Fill.raycastTarget = true; }
            for (int i = 0; i < _dots.Count; i++) { _dots[i].color = i == _selected ? Palette.Fury : new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.35f); _dots[i].rectTransform.sizeDelta = i == _selected ? new Vector2(14, 8) : new Vector2(8, 8); }
            _target = TargetX(_selected); _snapping = true; if (!animate) _content.anchoredPosition = new Vector2(_target, 0);
            _onSelect?.Invoke(_cards[_selected].Scene);
        }

        public void OnBeginDrag(PointerEventData e) { _dragging = true; _snapping = false; }
        public void OnEndDrag(PointerEventData e)
        {
            _dragging = false;
            float x = _content.anchoredPosition.x + _scroll.velocity.x * 0.15f;   // anticipates the inertia
            int idx = Mathf.RoundToInt(-x / (CardW + Gap)); _scroll.velocity = Vector2.zero; Select(idx, true);
        }

        /// <summary>Scale and opacity by distance to the center: the chosen one at 1, neighbours at 0.82 and faded.</summary>
        void LayoutCards()
        {
            float cx = -_content.anchoredPosition.x;
            for (int i = 0; i < _cards.Count; i++)
            {
                float d = Mathf.Abs(i * (CardW + Gap) - cx) / (CardW + Gap); float u = Mathf.Clamp01(d);
                float s = Mathf.Lerp(1f, 0.82f, u); _cards[i].Host.localScale = Vector3.one * s;
                float a = Mathf.Lerp(1f, 0.55f, u);
                foreach (var g in _cards[i].Host.GetComponentsInChildren<Graphic>()) { var col = g.color; string nm = g.gameObject.name; col.a = a * (nm == "Shadow" ? 0.9f : (nm == "Lock" ? 0.8f : (nm == "Stars" ? 0.55f : (nm == "BestPill" ? 0.6f : 1f)))); g.color = col; }
            }
            // the fly follows the chosen card
            var sel = _cards[_selected].Host; var frt = _fly.rectTransform; var pos = _viewport.anchoredPosition;
            float x = pos.x + (sel.anchoredPosition.x + _content.anchoredPosition.x - _viewport.sizeDelta.x * 0.5f) + CardW * 0.5f - 6;
            frt.anchoredPosition = new Vector2(x, pos.y + CardH * 0.5f - 8 + Mathf.Sin(_bob) * 2f);
        }

        void Update()
        {
            _bob += Time.unscaledDeltaTime * 2.2f; _wingT += Time.unscaledDeltaTime; if (_wingT > 0.06f) { _wingT = 0f; _wingFrame = (_wingFrame + 1) % 3; if (_flyFrames[_wingFrame] != null) _fly.sprite = _flyFrames[_wingFrame]; }
            if (!_dragging && _snapping)
            {
                var p = _content.anchoredPosition; float nx = Mathf.Lerp(p.x, _target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
                if (Mathf.Abs(nx - _target) < 0.5f) { nx = _target; _snapping = false; }
                _content.anchoredPosition = new Vector2(nx, p.y);
            }
            LayoutCards();
        }
    }
}
