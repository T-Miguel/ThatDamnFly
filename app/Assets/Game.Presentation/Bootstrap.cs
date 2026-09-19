// Start-up: S01 verifiable loading → S02 start (Play, Settings, The fly's brain) → S06 round → S07 result.
// S13 settings (sound, vibration, reduced effects, assistance, language) + Privacy/Credits/Support; S14 science. No store, no login, no stub.
using System.Collections;
using ThatDamnFly.Domain;
using ThatDamnFly.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ThatDamnFly.Presentation
{
    public sealed class Bootstrap : MonoBehaviour
    {
        public const string AppVersion = "1.2.4";
        public const string CanonicalUrl = "https://thatdamnfly.com";
        public const string SupportEmail = "support@thatdamnfly.com";
        public const string Publisher = "Miguel Prego";   // start screen text
        Canvas _canvas; RectTransform _loading, _start, _gameScreen, _result, _settingsScreen, _infoScreen, _arenaRect;
        TextMeshProUGUI _loadStatus, _loadDetail, _resultTitle, _resultStats, _resultPhrase, _resultRecord, _startInfo, _shareFeedback, _infoTitle, _infoBody, _settingsNote; RectTransform _loadBar; Button _retry;
        readonly ModelLoader _loader = new ModelLoader(); byte[] _model;
        ArenaView _arena; HudView _hud; GameController _game; AudioManager _audio;
        SaveService _save; readonly GameSettings _settings = new GameSettings();
        Sprite _logo; ulong _nextSeed; RoundOutcome _lastOutcome; RectTransform _infoReturn;
        SceneDef _scene = Scenes.Kitchen; string _dailyKey;   // != null → "fly of the day" round
        TextMeshProUGUI _resultDaily; Button _replayBtn; ReplayData _lastReplay; SceneCarousel _carousel; RectTransform _startBgA, _startBgB; bool _bgOnA = true; float _bgFade = -1f;
        // ADR-014: Play with a lock, total stars of the day, request lines on the result
        TextMeshProUGUI _playLabel, _starTotal, _titleText, _loadTip, _resultBonus; Image _playFill, _fade, _resultBg; RectTransform _replayHost, _bonusScreen; bool _inBonus; int _faceIdx = 1; Image _bonusBg; TextMeshProUGUI _bonusWho; int _lastSeenPct = -1; FlyKind _lastKind = FlyKind.House;   // ADR-019
        RectTransform _playRt; bool _fading; int _lastTip = -1;   // ADR-016
        readonly RectTransform[] _objRows = new RectTransform[3]; readonly Image[] _objStars = new Image[3]; readonly TextMeshProUGUI[] _objTexts = new TextMeshProUGUI[3]; readonly RectTransform[] _objNew = new RectTransform[3];
        static bool DevBuild =>
#if TDF_DEVBUILD
            true;
#else
            false;
#endif

        void Awake()
        {
            Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            _save = new SaveService(); _save.Load(AppVersion, ModelConfig.ModelId);
            ApplyPrefs();
            Loc.Load(_save.Data.prefs.language);
            _scene = Scenes.ById(_save.Data.prefs.scene); if (!_save.IsUnlocked(_scene)) _scene = Scenes.Kitchen;   // ADR-014: never start on a locked scene
            Ui.LoadFonts();
            _logo = Resources.Load<Sprite>("brand/logo-stacked-color-v1");
            _nextSeed = (ulong)System.DateTime.UtcNow.Ticks;
            if (GetComponent<AudioListener>() == null) gameObject.AddComponent<AudioListener>();   // without an AudioListener there is no sound anywhere
            _audio = new AudioManager(gameObject); _audio.Enabled = _settings.Sound; _audio.MusicEnabled = _settings.Music;
            BuildCanvas(); BuildScreens();
            StartCoroutine(LoadFlow());
        }

        void ApplyPrefs() { var p = _save.Data.prefs; _settings.Sound = p.sound; _settings.Music = p.music; _settings.Vibration = p.vibration; _settings.ReducedEffects = p.reducedEffects; _settings.Assist = p.assist; _settings.ToolsRight = p.toolsRight; _settings.HighContrast = p.highContrast; if (_audio != null) { _audio.Enabled = p.sound; _audio.MusicEnabled = p.music; } }

        void BuildCanvas()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = cgo.GetComponent<Canvas>(); _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(390, 844); sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; sc.matchWidthOrHeight = 0.5f;
            var bgCam = new GameObject("BackgroundCamera").AddComponent<Camera>();
            bgCam.clearFlags = CameraClearFlags.SolidColor; bgCam.backgroundColor = Palette.Paper; bgCam.cullingMask = 0; bgCam.depth = -2; bgCam.orthographic = true; bgCam.nearClipPlane = 0.1f; bgCam.farClipPlane = 1f;
            var bgData = bgCam.GetUniversalAdditionalCameraData(); if (bgData != null) bgData.renderPostProcessing = false;
        }

        void BuildScreens()
        {
            if (_loading != null) foreach (var rt in new[] { _loading, _start, _gameScreen, _result, _settingsScreen, _infoScreen, _bonusScreen }) if (rt != null) Destroy(rt.gameObject);
            var root = _canvas.transform;
            // S01
            _loading = Ui.Empty(root, "S01_Loading"); Ui.Stretch(_loading);
            Logo(_loading, 160);
            _loadStatus = Ui.Text(_loading, "Status", Loc.T("loading.model"), 18, Palette.Ink); Ui.Place(_loadStatus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(340, 60));
            var barBg = Ui.Panel(_loading, "BarBg", Palette.Surface); Ui.Place(barBg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(300, 14)); barBg.gameObject.AddComponent<Outline>().effectColor = Palette.Ink;
            _loadBar = Ui.Panel(barBg, "Bar", Palette.Action, false); _loadBar.anchorMin = Vector2.zero; _loadBar.anchorMax = new Vector2(0, 1); _loadBar.offsetMin = Vector2.zero; _loadBar.offsetMax = Vector2.zero;
            _loadDetail = Ui.Text(_loading, "Detail", "", 13, Palette.Muted); Ui.Place(_loadDetail.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(340, 80));
            _loadTip = Ui.Text(_loading, "Tip", "", 14, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Italic); Ui.Place(_loadTip.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(330, 70));   // ADR-016: tips while loading
            _retry = Ui.Button(_loading, "Retry", Loc.T("loading.retry"), Palette.Action, Palette.Ink, 18, () => StartCoroutine(LoadFlow())); Ui.Place(_retry.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(260, 56)); _retry.gameObject.SetActive(false);
            // S02
            _start = Ui.Empty(root, "S02_Start"); Ui.Stretch(_start);
            // live background: the chosen scene at full width, faded by a paper veil; swaps with a cross-fade (ADR-012)
            _startBgA = Ui.Panel(_start, "BgA", Color.white, false); Ui.Stretch(_startBgA); _startBgA.SetAsFirstSibling();
            _startBgB = Ui.Panel(_start, "BgB", Color.white, false); Ui.Stretch(_startBgB); _startBgB.SetSiblingIndex(1); _startBgB.GetComponent<Image>().color = new Color(1, 1, 1, 0f);
            foreach (var bgRt in new[] { _startBgA, _startBgB }) { var fit = bgRt.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = 0.75f; }   // covers the screen without distortion (3:4)
            var veil = Ui.Panel(_start, "Veil", new Color(Palette.Paper.r, Palette.Paper.g, Palette.Paper.b, 0.78f), false); Ui.Stretch(veil); veil.SetSiblingIndex(2);
            var haze = Ui.Panel(_start, "Haze", new Color(Palette.Paper.r, Palette.Paper.g, Palette.Paper.b, 0.9f), false); haze.anchorMin = new Vector2(0, 1); haze.anchorMax = new Vector2(1, 1); haze.pivot = new Vector2(0.5f, 1); haze.anchoredPosition = Vector2.zero; haze.sizeDelta = new Vector2(0, 260); haze.SetSiblingIndex(3);
            SetStartBackground(_scene, false);
            Logo(_start, 262); var logoRt = (_start.Find("Logo") ?? _start.Find("LogoText")) as RectTransform; if (logoRt != null) Ui.Place(logoRt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), logoRt.sizeDelta);   // anchored to the top
            // full-width scene carousel anchored to the top (ADR-011/012); Play and shortcuts anchored at the bottom - fits short screens
            _carousel = SceneCarousel.Build(_start, new Vector2(0, -318), 390, _scene, sc => { if (sc != _scene) { _scene = sc; _save.Data.prefs.scene = sc.Id; Persist(); SetStartBackground(sc, true); } UpdatePlay(); }, sc => Mathf.Max(_save.RecordsFor(sc.Id, false).bestFlies, _save.RecordsFor(sc.Id, true).bestFlies), () => _audio.Tap(), new Vector2(0.5f, 1), sc => _save.StarMask(sc.Id), sc => _save.IsUnlocked(sc));
            var play = Ui.RoundButton(_start, "Play", Loc.T("play"), Palette.Action, Palette.Ink, 26, OnPlay, out var playRt, Resources.Load<Sprite>("art/icon_play_fly"), 34); Ui.Place(playRt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 176), new Vector2(250, 66));
            _playLabel = play.GetComponentInChildren<TextMeshProUGUI>(); _playLabel.alignment = TextAlignmentOptions.Center; _playFill = play.targetGraphic as Image; _playRt = playRt;
            // ADR-016: player title (top-left corner)
            var titlePill = Ui.Card(_start, "Title", Palette.Paper, 2f, new Vector2(2, -2), false); Ui.Place(titlePill, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -12), new Vector2(150, 30));
            _titleText = Ui.Text(titlePill, "Text", "", 12, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Stretch(_titleText.rectTransform, 8, 0, 8, 0); _titleText.enableAutoSizing = true; _titleText.fontSizeMin = 9; _titleText.fontSizeMax = 12;
            // total stars of the day (top-right corner)
            var starPill = Ui.Card(_start, "StarTotal", Palette.Paper, 2f, new Vector2(2, -2), false); Ui.Place(starPill, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -12), new Vector2(84, 30));
            var stgo = new GameObject("Icon", typeof(RectTransform), typeof(Image)); stgo.transform.SetParent(starPill, false); var sti = stgo.GetComponent<Image>(); sti.sprite = Resources.Load<Sprite>("art/icon_star"); sti.preserveAspect = true; sti.raycastTarget = false; Ui.Place(stgo.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(22, 22));
            _starTotal = Ui.Text(starPill, "Text", "", 13, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Stretch(_starTotal.rectTransform, 30, 0, 6, 0);
            UpdatePlay();
            // four shortcuts with icons: fly of the day, records, settings, science
            var shortcuts = new (string id, string label, string icon, Color bg, System.Action act)[] {
                ("Daily", Loc.T("daily"), "art/icon_daily", Palette.Paper, () => OnPlay(Daily.TodayKey())),
                ("Records", Loc.T("records"), "art/icon_records", Palette.Paper, () => { _audio.Tap(); ShowInfo("records"); }),
                ("Settings", Loc.T("settings"), "art/icon_settings", Palette.Paper, () => { _audio.Tap(); Go(_settingsScreen); }),
                ("Brain", Loc.T("brain.short"), "art/icon_brain_eyes", Palette.Paper, () => { _audio.Tap(); ShowInfo("science"); }),
            };
            for (int i = 0; i < shortcuts.Length; i++)
            {
                var sc = shortcuts[i];
                var host = Ui.Card(_start, sc.id, sc.bg, 3f, new Vector2(3, -3)); Ui.Place(host, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-135 + i * 90, 84), new Vector2(84, 76));
                var fill = host.Find("Fill").GetComponent<Image>(); var b = fill.gameObject.AddComponent<Button>(); b.targetGraphic = fill; var act = sc.act; b.onClick.AddListener(() => act());
                var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image)); ig.transform.SetParent(fill.transform, false); var img = ig.GetComponent<Image>(); img.sprite = Resources.Load<Sprite>(sc.icon); img.preserveAspect = true; img.raycastTarget = false; Ui.Place(ig.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(34, 34));
                var lbl = Ui.Text(fill.transform, "Label", sc.label, 11, sc.id == "Daily" ? Palette.Fury : Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Place(lbl.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(84, 26));
            }
            _startInfo = Ui.Text(_start, "Info", "", 10, Palette.Muted); Ui.Place(_startInfo.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(360, 36));
            _start.gameObject.SetActive(false);
            // S06
            _gameScreen = Ui.Empty(root, "S06_Game"); Ui.Stretch(_gameScreen);
            _arenaRect = Ui.Empty(_gameScreen, "Arena"); _arenaRect.anchorMin = new Vector2(0.5f, 0.5f); _arenaRect.anchorMax = new Vector2(0.5f, 0.5f); _arenaRect.pivot = new Vector2(0.5f, 0.5f);
            _hud = new HudView(_gameScreen, () => _game?.PauseFromUser(), () => _game?.ResumeRequested(), () => _game?.Quit(), t => _game?.SelectTool(t), DevBuild);
            _gameScreen.gameObject.SetActive(false);
            if (_fade == null) { var frt = Ui.Panel(root, "Fade", new Color(1, 1, 1, 0f), false); Ui.Stretch(frt); _fade = frt.GetComponent<Image>(); _fade.enabled = false; }   // ADR-016
            _fade.transform.SetAsLastSibling();
            if (_arena != null) { _arena.Destroy(); _arena = null; } // the arena is recreated with the new Rect on the next round
            // S07
            _result = Ui.Empty(root, "S07_Result"); Ui.Stretch(_result);
            // ADR-016: the round's scene in the background, faded by a paper veil (as on the start screen)
            var rbg = Ui.Panel(_result, "Bg", Color.white, false); Ui.Stretch(rbg); var rfit = rbg.gameObject.AddComponent<AspectRatioFitter>(); rfit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; rfit.aspectRatio = 0.75f; _resultBg = rbg.GetComponent<Image>();
            var rveil = Ui.Panel(_result, "Veil", new Color(Palette.Paper.r, Palette.Paper.g, Palette.Paper.b, 0.88f), false); Ui.Stretch(rveil);
            _resultTitle = Ui.Text(_result, "Title", "", 36, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Place(_resultTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 256), new Vector2(360, 72)); _resultTitle.enableAutoSizing = true; _resultTitle.fontSizeMin = 22; _resultTitle.fontSizeMax = 36;
            _resultStats = Ui.Text(_result, "Stats", "", 17, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Place(_resultStats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 208), new Vector2(340, 46));
            _resultPhrase = Ui.Text(_result, "Phrase", "", 15, Palette.Muted); Ui.Place(_resultPhrase.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 154), new Vector2(330, 58));
            _resultRecord = Ui.Text(_result, "Record", "", 15, Palette.Info, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Place(_resultRecord.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 114), new Vector2(330, 28));
            _resultDaily = Ui.Text(_result, "Daily", "", 14, Palette.Fury, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Place(_resultDaily.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 306), new Vector2(340, 24));
            // ADR-014: the scene's three requests (★ fulfilled / ☆ pending; "NEW" when the star was earned in this round)
            for (int i = 0; i < 3; i++)
            {
                var row = Ui.Empty(_result, "Obj" + i); Ui.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80 - i * 28), new Vector2(300, 26));
                var sgo = new GameObject("Star", typeof(RectTransform), typeof(Image)); sgo.transform.SetParent(row, false); var si = sgo.GetComponent<Image>(); si.preserveAspect = true; si.raycastTarget = false; Ui.Place(sgo.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(24, 24));
                var ot = Ui.Text(row, "Text", "", 14, Palette.Ink, TextAlignmentOptions.Left, FontStyles.Bold); Ui.Place(ot.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(32, 0), new Vector2(212, 26));
                var tag = Ui.Card(row, "New", Palette.Action, 2f, null, false); Ui.Place(tag, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(54, 22));
                var tt = Ui.Text(tag, "T", Loc.T("result.new"), 11, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Stretch(tt.rectTransform);
                _objRows[i] = row; _objStars[i] = si; _objTexts[i] = ot; _objNew[i] = tag; row.gameObject.SetActive(false);
            }
            var again = Ui.RoundButton(_result, "Again", Loc.T("result.again"), Palette.Action, Palette.Ink, 22, () => OnPlay(_dailyKey), out var againRt); Ui.Place(againRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -58), new Vector2(260, 58));
            var share = Ui.RoundButton(_result, "Share", Loc.T("result.share"), Palette.Paper, Palette.Ink, 17, OnShare, out var shareRt); Ui.Place(shareRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-68, -122), new Vector2(130, 48));
            _replayBtn = Ui.RoundButton(_result, "Replay", Loc.T("result.replay"), Palette.Paper, Palette.Ink, 15, OnReplay, out _replayHost); Ui.Place(_replayHost, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(68, -122), new Vector2(130, 48));
            _shareFeedback = Ui.Text(_result, "ShareFb", "", 13, Palette.Muted); Ui.Place(_shareFeedback.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -152), new Vector2(300, 24));
            var home = Ui.RoundButton(_result, "Home", Loc.T("result.home"), Palette.Paper, Palette.Ink, 15, () => { _audio.Tap(); Go(_start); }, out var homeRt); Ui.Place(homeRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(180, 46));
            _resultBonus = Ui.Text(_result, "Bonus", "", 14, Palette.Info, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Place(_resultBonus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(330, 22));
            _result.gameObject.SetActive(false);
            // ADR-019: interstitial of the face bonus
            _bonusScreen = Ui.Empty(root, "S08_Bonus"); Ui.Stretch(_bonusScreen);
            var bbg = Ui.Panel(_bonusScreen, "Bg", Color.white, false); Ui.Stretch(bbg); var bfit = bbg.gameObject.AddComponent<AspectRatioFitter>(); bfit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; bfit.aspectRatio = 0.75f; _bonusBg = bbg.GetComponent<Image>();
            var bveil = Ui.Panel(_bonusScreen, "Veil", new Color(Palette.Paper.r, Palette.Paper.g, Palette.Paper.b, 0.55f), false); Ui.Stretch(bveil);
            var bcard = Ui.Card(_bonusScreen, "Card", Palette.Paper, 3f, new Vector2(5, -5)); Ui.Place(bcard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(320, 320));
            var bcf = bcard.Find("Fill");
            var btitle = Ui.Text(bcf, "Title", Loc.T("bonus.title"), 30, Palette.Fury, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Place(btitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(300, 44)); btitle.enableAutoSizing = true; btitle.fontSizeMin = 20; btitle.fontSizeMax = 30;
            var bigo2 = new GameObject("Icon", typeof(RectTransform), typeof(Image)); bigo2.transform.SetParent(bcf, false); var bim = bigo2.GetComponent<Image>(); bim.sprite = Resources.Load<Sprite>("art/icon_slap"); bim.preserveAspect = true; bim.raycastTarget = false; Ui.Place(bigo2.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -66), new Vector2(64, 64));
            _bonusWho = Ui.Text(bcf, "Who", "", 15, Palette.Info, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Place(_bonusWho.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -132), new Vector2(284, 22));
            var bbody = Ui.Text(bcf, "Body", Loc.T("bonus.body"), 14, Palette.Ink); Ui.Place(bbody.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -156), new Vector2(284, 84));
            var bgo2 = Ui.RoundButton(bcf, "Go", Loc.T("bonus.go"), Palette.Action, Palette.Ink, 20, StartBonus, out var bgoRt); Ui.Place(bgoRt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(240, 54));
            _bonusScreen.gameObject.SetActive(false);
            // S13
            _settingsScreen = Ui.Empty(root, "S13_Settings"); Ui.Stretch(_settingsScreen); BuildSettings(_settingsScreen); _settingsScreen.gameObject.SetActive(false);
            // S14 / scrolling information
            _infoScreen = Ui.Empty(root, "S14_Info"); Ui.Stretch(_infoScreen);
            _infoTitle = Ui.Text(_infoScreen, "Title", "", 26, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Place(_infoTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(340, 40));
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask)); scrollGo.transform.SetParent(_infoScreen, false);
            var srt = scrollGo.GetComponent<RectTransform>(); srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 1); srt.offsetMin = new Vector2(20, 90); srt.offsetMax = new Vector2(-20, -90);
            scrollGo.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f); scrollGo.GetComponent<Mask>().showMaskGraphic = false;
            var content = Ui.Empty(scrollGo.transform, "Content"); content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            _infoBody = Ui.Text(content, "Body", "", 15, Palette.Ink, TextAlignmentOptions.TopLeft); var brt = _infoBody.rectTransform; brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(0.5f, 1); brt.anchoredPosition = Vector2.zero; brt.sizeDelta = new Vector2(0, 0);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; _infoBody.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = scrollGo.GetComponent<ScrollRect>(); sr.content = content; sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 30f;
            var back = Ui.Button(_infoScreen, "Back", Loc.T("settings.back"), Palette.Action, Palette.Ink, 18, () => { _audio.Tap(); Go(_infoReturn ?? _start); }); Ui.Place(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(240, 56));
            _infoScreen.gameObject.SetActive(false);
        }

        /// <summary>Start screen background: the chosen scene; with animate, a 0.35 s cross-fade between the two layers.</summary>
        void SetStartBackground(SceneDef sc, bool animate)
        {
            var sprite = Resources.Load<Sprite>("art/" + sc.Id + "_bg"); if (sprite == null) return;
            var front = _bgOnA ? _startBgA : _startBgB; var back = _bgOnA ? _startBgB : _startBgA;
            if (!animate) { front.GetComponent<Image>().sprite = sprite; front.GetComponent<Image>().color = Color.white; back.GetComponent<Image>().color = new Color(1, 1, 1, 0f); _bgFade = -1f; return; }
            back.GetComponent<Image>().sprite = sprite; back.SetSiblingIndex(1); front.SetSiblingIndex(0); _bgOnA = !_bgOnA; _bgFade = 0f;
        }

        string RecordsText()
        {
            var sb = new System.Text.StringBuilder();
            if (_save.Data.totals.rounds == 0) return Loc.T("records.none");   // ADR-016: empty state
            sb.Append("<b>").Append(Loc.T("title." + _save.TitleTier())).Append("</b> · ").Append(Loc.F("records.total", _save.FliesTotalAll())).Append("\n");
            // ADR-016: collection
            sb.Append(Loc.T("records.collection")).Append(": ");
            bool anyKind = false;
            for (int k = 0; k < FlyKinds.Count; k++) { var kd = FlyKinds.Of((FlyKind)k); int n = _save.KindCount(kd.Id); if (n == 0) continue; if (anyKind) sb.Append(" · "); anyKind = true; sb.Append(Loc.T("kind." + kd.Id)).Append(" ").Append(n); }
            if (!anyKind) sb.Append(Loc.T("collection.none"));
            sb.Append("\n");
            sb.Append(Loc.F("records.stars", _save.DayStars(), _save.DayStarsMax)).Append(_save.Data.daysCompleted > 0 ? " · " + Loc.F("records.days", _save.Data.daysCompleted) : "").Append("\n\n");
            foreach (var sc in Scenes.All)
            {
                var r = _save.RecordsFor(sc.Id, false); var ra = _save.RecordsFor(sc.Id, true); int stars = _save.StarsOf(sc.Id);
                sb.Append("<b>").Append(Loc.T("scene." + sc.Id)).Append("</b> · ").Append(Loc.F("records.scenestars", stars, SaveService.StarsPerScene)).Append("\n");
                sb.Append(Loc.F("records.line", r.bestFlies, r.bestCombo, r.bestTimeMs > 0 ? (r.bestTimeMs / 1000f).ToString("0.0") : "-", r.fliesTotal, r.wins)).Append("\n");
                if (ra.wins > 0) sb.Append(Loc.T("result.assisted")).Append(": ").Append(Loc.F("records.line", ra.bestFlies, ra.bestCombo, ra.bestTimeMs > 0 ? (ra.bestTimeMs / 1000f).ToString("0.0") : "-", ra.fliesTotal, ra.wins)).Append("\n");
                sb.Append("\n");
            }
            sb.Append("<b>").Append(Loc.T("daily")).Append("</b>\n");
            string today = Daily.TodayKey(); bool any = false;
            for (int i = _save.Data.daily.Count - 1; i >= 0 && i >= _save.Data.daily.Count - 14; i--)
            {
                var d = _save.Data.daily[i]; any = true;
                sb.Append(d.date == today ? Loc.T("daily.today") : Daily.Pretty(d.date)).Append(" · ").Append(Loc.T("scene." + d.scene)).Append(": ").Append(Loc.F("records.daily", d.bestFlies, d.bestCombo, d.plays)).Append("\n");
            }
            if (!any) sb.Append(Loc.T("records.daily.none")).Append("\n");
            return sb.ToString();
        }


        void ShowInfo(string which)
        {
            _infoReturn = _settingsScreen.gameObject.activeSelf ? _settingsScreen : _start;
            string title, body;
            switch (which)
            {
                case "science": title = Loc.T("science.title"); body = Loc.T("science.body") + "\n\n" + Loc.T("science.brain") + "\n\n" + Loc.F("science.model", ModelConfig.ModelId, _loader.NeuronCount) + "\n\n" + Loc.T("science.sources") + ":\nMaleCNS v1.0 - male-cns.janelia.org/download\nCC BY 4.0 - creativecommons.org/licenses/by/4.0"; break;
                case "credits": title = Loc.T("credits.title"); body = Loc.T("credits.body") + "\n\nMaleCNS: male-cns.janelia.org\nCC BY 4.0: creativecommons.org/licenses/by/4.0\nArchivo (OFL): github.com/google/fonts/tree/main/ofl/archivo\n" + ModelConfig.ModelId + "\nSHA-256 " + ModelConfigValues.ExpectedFileSha256; break;
                case "privacy": title = Loc.T("privacy.title"); body = Loc.F("privacy.body", SupportEmail); break;
                case "records": title = Loc.T("records"); body = RecordsText(); break;
                default: title = Loc.T("support.title"); body = Loc.F("support.body", SupportEmail); break;
            }
            _infoTitle.text = title; _infoBody.text = body; Go(_infoScreen);
        }

        void BuildSettings(RectTransform screen)
        {
            var title = Ui.Text(screen, "Title", Loc.T("settings"), 26, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(340, 40));
            float y = -100f;
            Row(screen, Loc.T("settings.sound"), _settings.Sound, v => { _settings.Sound = v; _save.Data.prefs.sound = v; _audio.Enabled = v; _audio.Refresh(); Persist(); }, ref y);
            Row(screen, Loc.T("settings.music"), _settings.Music, v => { _settings.Music = v; _save.Data.prefs.music = v; _audio.MusicEnabled = v; Persist(); }, ref y);
            if (Platform.VibrationSupported) Row(screen, Loc.T("settings.vibration"), _settings.Vibration, v => { _settings.Vibration = v; _save.Data.prefs.vibration = v; Persist(); if (v) Platform.Vibrate(false); }, ref y);
            Row(screen, Loc.T("settings.reduced"), _settings.ReducedEffects, v => { _settings.ReducedEffects = v; _save.Data.prefs.reducedEffects = v; Persist(); }, ref y);
            Row(screen, Loc.T("settings.assist"), _settings.Assist, v => { _settings.Assist = v; _save.Data.prefs.assist = v; Persist(); }, ref y);
            var help = Ui.Text(screen, "AssistHelp", Loc.T("settings.assist.help"), 12, Palette.Muted, TextAlignmentOptions.Left); Ui.Place(help.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y + 6), new Vector2(340, 34)); y -= 40f;
            var langLbl = Ui.Text(screen, "LangLbl", Loc.T("settings.language"), 16, Palette.Ink, TextAlignmentOptions.Left); Ui.Place(langLbl.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, y), new Vector2(150, 48));
            for (int i = 0; i < Loc.Supported.Length; i++)
            {
                string code = Loc.Supported[i]; bool cur = code == Loc.Lang;
                var b = Ui.Button(screen, "Lang_" + code, Loc.T("lang." + code), cur ? Palette.Action : Palette.Paper, Palette.Ink, 14, () => { _audio.Tap(); _save.Data.prefs.language = code; Persist(); Loc.Load(code); BuildScreens(); Show(_settingsScreen); });
                Ui.Place(b.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24 - (Loc.Supported.Length - 1 - i) * 112, y), new Vector2(104, 48));
            }
            y -= 66f;
            foreach (var pair in new[] { ("settings.credits", "credits"), ("settings.privacy", "privacy"), ("settings.support", "support") })
            {
                string w = pair.Item2; var b = Ui.Button(screen, "Info_" + w, Loc.T(pair.Item1), Palette.Paper, Palette.Ink, 16, () => { _audio.Tap(); ShowInfo(w); }); Ui.Place(b.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(340, 48)); y -= 56f;
            }
            var exp = Ui.Button(screen, "Export", Loc.T("settings.export"), Palette.Paper, Palette.Ink, 13, () => { _audio.Tap(); bool ok = Platform.Copy(_save.DiagnosticsJson(null)); _settingsNote.text = ok ? Loc.T("settings.export.done") : ""; }); Ui.Place(exp.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-88, y), new Vector2(164, 44));
            var reset = Ui.Button(screen, "Reset", Loc.T("settings.reset"), Palette.Paper, Palette.Fury, 13, () => { _audio.Tap(); if (_settingsNote.text == Loc.T("settings.reset.confirm")) { _save.ResetProgress(); _settingsNote.text = ""; } else _settingsNote.text = Loc.T("settings.reset.confirm"); }); Ui.Place(reset.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(88, y), new Vector2(164, 44)); y -= 50f;
            _settingsNote = Ui.Text(screen, "Note", _save.StorageAvailable ? "" : Loc.T("settings.nostorage"), 13, Palette.Info); Ui.Place(_settingsNote.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(340, 40));
            var back = Ui.Button(screen, "Back", Loc.T("settings.back"), Palette.Action, Palette.Ink, 18, () => { _audio.Tap(); Show(_start); }); Ui.Place(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(240, 56));
        }

        void Row(RectTransform screen, string label, bool value, System.Action<bool> onChange, ref float y)
        {
            var lbl = Ui.Text(screen, "Lbl_" + label, label, 16, Palette.Ink, TextAlignmentOptions.Left); Ui.Place(lbl.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, y), new Vector2(220, 48));
            var go = new GameObject("Toggle_" + label, typeof(RectTransform), typeof(Image), typeof(Toggle)); go.transform.SetParent(screen, false);
            Ui.Place(go.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, y), new Vector2(104, 48));
            var bg = go.GetComponent<Image>(); bg.color = value ? Palette.Action : Palette.Surface; go.AddComponent<Outline>().effectColor = Palette.Ink;
            var t = go.GetComponent<Toggle>(); t.isOn = value; t.targetGraphic = bg;
            var txt = Ui.Text(go.transform, "State", Loc.T(value ? "on" : "off"), 14, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Stretch(txt.rectTransform);
            t.onValueChanged.AddListener(v => { bg.color = v ? Palette.Action : Palette.Surface; txt.text = Loc.T(v ? "on" : "off"); _audio.Tap(); onChange(v); });
            y -= 56f;
        }

        void Persist() { if (!_save.Save() && _settingsNote != null) _settingsNote.text = Loc.T("settings.nostorage"); }

        void Logo(Transform parent, float y)
        {
            if (_logo == null) { var t = Ui.Text(parent, "LogoText", "THAT DAMN FLY", 36, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(340, 60)); return; }
            var go = new GameObject("Logo", typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.sprite = _logo; img.preserveAspect = true; img.raycastTarget = false;
            Ui.Place(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(300, 118));
        }

        /// <summary>ADR-016: transition with a paper fade (0.16 s closing, 0.22 s opening); blocks touches while it lasts.</summary>
        void Go(RectTransform screen, System.Action atMidpoint = null)
        {
            if (_fade == null || _settings.ReducedEffects) { atMidpoint?.Invoke(); Show(screen); return; }
            StartCoroutine(FadeTo(screen, atMidpoint));
        }
        IEnumerator FadeTo(RectTransform screen, System.Action atMidpoint)
        {
            if (_fading) { atMidpoint?.Invoke(); Show(screen); yield break; }
            _fading = true; _fade.enabled = true; _fade.raycastTarget = true; _fade.transform.SetAsLastSibling();
            for (float t = 0f; t < 0.16f; t += Time.unscaledDeltaTime) { _fade.color = new Color(Palette.Paper.r, Palette.Paper.g, Palette.Paper.b, t / 0.16f); yield return null; }
            _fade.color = Palette.Paper; atMidpoint?.Invoke(); Show(screen); yield return null;
            for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime) { _fade.color = new Color(Palette.Paper.r, Palette.Paper.g, Palette.Paper.b, 1f - t / 0.22f); yield return null; }
            _fade.color = new Color(1, 1, 1, 0f); _fade.enabled = false; _fade.raycastTarget = false; _fading = false;
        }

        const int TipCount = 6;
        void Show(RectTransform screen)
        {
            if (screen == _start && _carousel != null) { _carousel.Refresh(); UpdatePlay(); }
            foreach (var rt in new[] { _loading, _start, _gameScreen, _result, _settingsScreen, _infoScreen, _bonusScreen }) if (rt != null) rt.gameObject.SetActive(rt == screen);
            _arena?.SetActive(screen == _gameScreen);
        }

        IEnumerator LoadFlow()
        {
            Debug.Log("[TDF] boot: loading model from " + ModelLoader.BaseUrl + " · store=" + _save.StoreKind + " storage=" + _save.StorageAvailable + " lang=" + Loc.Lang);
            Show(_loading); _retry.gameObject.SetActive(false); _loadStatus.text = Loc.T("loading.model"); _loadDetail.text = Loc.F("loading.detail", ModelConfig.FileName, ModelConfig.ExpectedBytes / 1024);
            var co = _loader.Load();
            while (co.MoveNext()) { _loadBar.anchorMax = new Vector2(Mathf.Clamp01(_loader.Progress), 1f); if (_loader.State == ModelLoadState.Verifying) _loadStatus.text = Loc.T("loading.verify"); int tip = (int)(Time.unscaledTime / 3.5f) % TipCount; if (tip != _lastTip) { _lastTip = tip; _loadTip.text = Loc.T("tip." + (tip + 1)); } yield return co.Current; }
            if (_loader.State != ModelLoadState.Ready)
            {
                _save.Count("model_load_failed"); _save.Save();
                _loadStatus.text = Loc.T("loading.fail"); _loadDetail.text = _loader.Error; _retry.gameObject.SetActive(true); yield break;
            }
            _model = _loader.Bytes; _loadBar.anchorMax = new Vector2(1, 1);
            Debug.Log($"[TDF] model ready: {_loader.ModelId} neurons={_loader.NeuronCount} fromCache={_loader.FromCache} abi={FlyCoreNative.fc_abi_version()}");
            _startInfo.text = "V " + AppVersion + " · © 2026 " + Publisher + (_save.StorageAvailable ? "" : "\n" + Loc.T("settings.nostorage"));   // technical details: Settings → Copy diagnostics
            Show(_start);
        }

        /// <summary>ADR-014: Play turns grey ("Locked") on a scene still locked; total stars of the day.</summary>
        void UpdatePlay()
        {
            if (_playLabel == null) return; bool open = _save.IsUnlocked(_scene);
            _playLabel.text = open ? Loc.T("play") : Loc.T("play.locked"); _playFill.color = open ? Palette.Action : Palette.Surface;
            _starTotal.text = _save.DayStars() + "/" + _save.DayStarsMax; if (_titleText != null) _titleText.text = Loc.T("title." + _save.TitleTier());
        }
        RulesConfig RulesFor(SceneDef scene, int levelOffset)
        {
            var r = new RulesConfig { LevelOffset = levelOffset, BossAtIndex = scene.Boss ? RulesConfig.BossIndexDefault : -1 };
#if TDF_DEVBUILD
            if (_devBossIndex >= 0) r.BossAtIndex = _devBossIndex;
#endif
            return r;
        }

        void OnPlay() => OnPlay(null);
        void OnPlay(string dailyKey)
        {
            if (!_save.IsUnlocked(_scene) || _fading) { _audio.Tap(); return; }
            _audio.Unlock(); _audio.Tap();
            Go(_gameScreen, () => StartRound(dailyKey));
        }
        void StartRound(string dailyKey)
        {
            _dailyKey = dailyKey;
            if (_arena == null) _arena = new ArenaView(_canvas, _arenaRect, _scene);
            _arena.Clear(); _arena.SetScene(_scene); _arena.ReducedEffects = _settings.ReducedEffects;
            _game?.Sim.Dispose();
            LayoutArena();
            ulong seed = dailyKey != null ? Daily.Seed(dailyKey, _scene.Id) : _nextSeed++;   // fly of the day: the same fly for everyone on that day and scene
            bool firstRun = !_save.Data.prefs.tutorialSeen;
            var rules = RulesFor(_scene, dailyKey != null ? 0 : (firstRun ? -2 : AdaptiveOffset()));   // ADR-010: ramp adjusted to the player's record (never on the fly of the day); ADR-014: boss in the final scene; ADR-016: calmer first round
            var sim = new WorldSimulation(_model, seed, rules, assisted: _settings.Assist, food: _scene.Food, scene: _scene);
            Debug.Log($"[TDF] round start seed={sim.Seed} scene={_scene.Id} daily={dailyKey ?? "-"} assisted={_settings.Assist}");
            _save.Count(dailyKey != null ? "daily_started" : "round_started");
            if (firstRun) { _save.Data.prefs.tutorialSeen = true; _save.Save(); }
            GameController.FirstRoundsHint = _save.CountOf("tool_switched") < 3;
            _game = new GameController(sim, _arena, _hud, _audio, _settings, firstRun, _scene); _game.DailyLabel = dailyKey != null ? Loc.F("daily.hud", Daily.Pretty(dailyKey)) : null; _game.Recording.LevelOffset = rules.LevelOffset;
            _game.RoundEnded += OnRoundEnded;
            _game.ToolSwitched += () => _save.Count("tool_switched");
            _shareFeedback.text = "";
        }

        /// <summary>Beginner (record &lt; 3): −1 - the first flies are calmer; expert: +1 from 9, +2 from 12, +3 from 16.</summary>
        int AdaptiveOffset()
        {
            var r = _save.RecordsFor(_scene.Id, _settings.Assist); int best = r.bestFlies;
            if (r.wins == 0 || best < 3) return -1;
            return best >= 16 ? 3 : (best >= 12 ? 2 : (best >= 9 ? 1 : 0));
        }

        /// <summary>Watch the replay of the best catch of the last round (window from 4 s before to 1.2 s after), with the same taps and the same fly.</summary>
        void OnReplay()
        {
            var rep = _lastReplay; if (rep == null || !rep.HasCatch) return; _audio.Tap();
            _save.Count("replay_viewed");
            if (_arena == null) _arena = new ArenaView(_canvas, _arenaRect, _scene);
            _arena.Clear(); _arena.SetScene(_scene); _arena.ReducedEffects = _settings.ReducedEffects;
            _game?.Sim.Dispose(); LayoutArena();
            var sim = new WorldSimulation(_model, rep.Seed, RulesFor(_scene, rep.LevelOffset), assisted: rep.Assisted, food: _scene.Food, scene: _scene);
            _game = new GameController(sim, _arena, _hud, _audio, _settings, false, _scene, rep);
            _game.ReplayFinished += () => { _audio.Tap(); Go(_result); };
            Show(_gameScreen);
        }

        void LayoutArena()
        {
            var canvasRt = _canvas.GetComponent<RectTransform>(); float L = canvasRt.rect.width, A = canvasRt.rect.height;
            const float top = 56f, barMin = HudView.BottomHeight;   // top (pause + request) and status bar; the arena (3:4) sits on the bar (the hand seems to come out from behind it)
            float w = Mathf.Min(L - 24f, 0.75f * (A - top - barMin - 2f)); w = Mathf.Min(w, 480f); float h = w * 4f / 3f;
            float spare = Mathf.Max(0f, A - top - barMin - 2f - h); float bar = barMin + Mathf.Clamp(spare * 0.6f, 0f, 64f);   // tall screens: the bar grows (up to +64) and the rest of the slack goes on top
            _hud.SetBarHeight(bar);
            _arenaRect.sizeDelta = new Vector2(w, h); _arenaRect.anchoredPosition = new Vector2(0, -A * 0.5f + bar + 2f + h * 0.5f);
        }

        void OnRoundEnded(RoundOutcome o)
        {
            _lastOutcome = o; var sim = _game.Sim; _lastReplay = _game.Recording; _replayHost.gameObject.SetActive(_lastReplay != null && _lastReplay.HasCatch); _resultBg.sprite = Resources.Load<Sprite>("art/" + _scene.Id + "_bg");
            _lastSeenPct = o.Attacks > 0 ? 100 * sim.AttacksSeen / o.Attacks : -1; _lastKind = sim.LastCaught != null ? sim.LastCaught.State.Kind.Kind : FlyKind.House; _resultBonus.text = "";
            Debug.Log($"[TDF] round end {o.Terminal} t={o.ActiveMs} attacks={o.Attacks} fury={o.Fury} neuralAvgMs={(sim.TicksRun > 0 ? sim.NeuralMsTotal / sim.TicksRun : 0):0.000} neuralMaxMs={sim.NeuralMsMax:0.00} techPauses={sim.TechnicalPauses}");
            bool interrupted = o.Terminal == RoundState.Interrupted; int flies = o.FliesCaught + (o.Terminal == RoundState.Won ? 1 : 0);
            _save.Count(interrupted ? "round_interrupted" : "round_completed");
            var (fr, tr, cr) = _save.ApplyResult($"{sim.Seed}-{o.Terminal}-{o.ActiveMs}", flies, o.FastestCatchMs, interrupted, o.Attacks, o.Assisted, o.BestCombo, _scene.Id); _save.Save();
            _resultTitle.text = interrupted ? Loc.T("result.interrupted") : (flies == 0 ? Loc.T("result.timeout") : (flies == 1 ? Loc.T("result.one") : Loc.F("result.flies", flies)));
            _resultStats.text = Loc.F("result.stats", flies, o.Attacks, o.BestCombo) + (o.Assisted ? " · " + Loc.T("result.assisted") : "")
                + (o.Attacks > 0 ? "\n<size=13><color=#555E55>" + Loc.F("result.ficha", sim.AttacksSeen, o.Attacks, sim.ReflexMeanMs, sim.Escapes) + "</color></size>" : "");   // ADR-015: perception sheet
            _resultPhrase.text = Phrase(o, flies) + (o.FastestCatchMs > 0 ? "\n" + Loc.F("result.fastest", (o.FastestCatchMs / 1000f).ToString("0.0")) : "") + (o.TimeBonusMs > 0 ? " · " + Loc.F("result.extra", (o.TimeBonusMs / 1000f).ToString("0")) : "");
            _resultRecord.text = fr ? Loc.F("result.record.flies", flies) : (cr ? Loc.F("result.record.combo", o.BestCombo) : (tr ? Loc.F("result.record.fastest", (o.FastestCatchMs / 1000f).ToString("0.0")) : ""));
            // ADR-014: this round's requests → stars; boss fly caught → day complete
            int tierBefore = _save.TitleTier(); _save.ApplyKinds(o.CaughtByKind);   // ADR-016: collection
            int newMask = _save.ApplyStars(_scene.Id, o.ObjectivesDone, o.BossCaught); if (newMask != 0) _save.Count("star_earned"); if (o.BossCaught) _save.Count("day_completed");
            for (int i = 0; i < 3; i++)
            {
                bool has = i < _scene.Objectives.Length; _objRows[i].gameObject.SetActive(has); if (!has) continue;
                bool done = i < o.ObjectivesDone.Length && o.ObjectivesDone[i]; int prog = i < o.ObjectiveProgress.Length ? o.ObjectiveProgress[i] : 0;
                _objStars[i].sprite = Resources.Load<Sprite>(done ? "art/icon_star" : "art/icon_star_empty");
                _objTexts[i].text = HudView.ObjectiveText(_scene.Objectives[i], prog); _objTexts[i].color = done ? Palette.Ink : Palette.Muted;
                _objNew[i].gameObject.SetActive(((newMask >> i) & 1) != 0);
            }
            if (o.BossCaught) { _resultTitle.text = Loc.T("result.day"); _resultPhrase.text = Loc.T("phrase.day"); _resultRecord.text = Loc.F("result.daystars", _save.DayStars(), _save.DayStarsMax); }
            else if (newMask != 0 && _resultRecord.text.Length == 0) _resultRecord.text = Loc.F("result.stars", _save.StarsOf(_scene.Id), SaveService.StarsPerScene);
            if (_save.TitleTier() > tierBefore) _resultRecord.text = Loc.F("result.title", Loc.T("title." + _save.TitleTier()));   // ADR-016: new title
            if (_dailyKey != null)
            {
                bool dayRec = _save.ApplyDaily(_dailyKey, _scene.Id, flies, o.BestCombo, o.FastestCatchMs, interrupted); _save.Save();
                var d = _save.DailyFor(_dailyKey, _scene.Id, false);
                _resultDaily.text = Loc.F("daily.result", Daily.Pretty(_dailyKey), Loc.T("scene." + _scene.Id)) + (dayRec && !interrupted ? " · " + Loc.T("daily.record") : (d != null ? " · " + Loc.F("daily.best", d.bestFlies) : ""));
            }
            else _resultDaily.text = "";
            bool bonus = !interrupted && newMask != 0 && _dailyKey == null;   // ADR-019: new star → face bonus before the result
            if (bonus) { _save.Count("bonus_offered"); StartCoroutine(ShowBonusAfter(1.2f)); } else StartCoroutine(ShowResultAfter(interrupted ? 0f : 1.2f));
        }
        IEnumerator ShowBonusAfter(float s) { yield return new WaitForSecondsRealtime(s); PickFace(); Go(_bonusScreen); }
        /// <summary>ADR-019: the character rotates on every bonus (never the same twice in a row).</summary>
        void PickFace() { _faceIdx = 1 + (_save.CountOf("bonus_offered") + _save.CountOf("bonus_started")) % ArenaView.FaceCount; _bonusBg.sprite = Resources.Load<Sprite>("art/face" + _faceIdx + "_bg"); _bonusWho.text = Loc.F("bonus.who", Loc.T("face." + _faceIdx)); }
        IEnumerator GoAfter(RectTransform screen, float s) { yield return new WaitForSecondsRealtime(s); Go(screen); }

        /// <summary>ADR-019: 20 s bonus round keeping the flies off the character's face - only the hand; the flies count for the total and the collection.</summary>
        void StartBonus()
        {
            if (_fading) return; _audio.Tap(); _inBonus = true;
            Go(_gameScreen, () =>
            {
                var face = Scenes.Face;
                if (_arena == null) _arena = new ArenaView(_canvas, _arenaRect, face);
                _arena.FaceIndex = _faceIdx;
                _arena.Clear(); _arena.SetScene(face); _arena.ReducedEffects = _settings.ReducedEffects;
                _game?.Sim.Dispose(); LayoutArena();
                var sim = new WorldSimulation(_model, _nextSeed++, FaceTools.Rules(), assisted: _settings.Assist, food: face.Food, scene: face);
                _save.Count("bonus_started");
                GameController.FirstRoundsHint = false;
                _game = new GameController(sim, _arena, _hud, _audio, _settings, false, face); _game.RoundEnded += OnBonusEnded;
            });
        }
        void OnBonusEnded(RoundOutcome o)
        {
            _inBonus = false; int flies = o.FliesCaught; int slaps = Mathf.Max(0, o.Attacks - flies); bool popped = _game.Popped;
            _save.Count("bonus_completed"); _save.ApplyKinds(o.CaughtByKind);
            var r = _save.RecordsFor("face", false); r.fliesTotal += flies; bool rec = flies > r.bestFlies && flies > 0; if (rec) r.bestFlies = flies; if (flies > 0) r.wins++; _save.Save();
            _resultBonus.text = (popped ? Loc.T("fx.pop") + " · " : "") + Loc.F("bonus.result", flies, slaps) + (rec ? " · " + Loc.T("bonus.record") : "");
            Debug.Log($"[TDF] bonus end flies={flies} slaps={slaps} popped={popped}");
            if (_lastOutcome == null) { StartCoroutine(GoAfter(_start, 1.4f)); return; }   // bonus started without a round before it (development only)
            StartCoroutine(ShowResultAfter(1.4f));
        }

        string Phrase(RoundOutcome o, int flies)
        {
            if (o.Terminal == RoundState.Interrupted) return Loc.T("phrase.interrupted");
            if (flies >= 6) return Loc.F("phrase.flies.many", flies);
            if (flies >= 2) return Loc.F("phrase.flies.some", flies);
            if (flies == 1) return o.FastestCatchMs > 0 && o.FastestCatchMs < 5000 ? Loc.T("phrase.won.fast") : Loc.T("phrase.won." + (o.LastToolId ?? "kitchen_cloth"));   // phrases per tool (the picnic ones have their own keys)
            if (o.Attacks == 0) return Loc.T("phrase.timeout.zero");
            if (o.LastToolId != null && _scene.ById(o.LastToolId)?.Role == ToolRole.Giant) return Loc.T("phrase.timeout." + o.LastToolId);
            return o.Attacks >= 12 ? Loc.F("phrase.timeout.many", o.Attacks) : Loc.T("phrase.timeout.few");
        }

        IEnumerator ShowResultAfter(float s) { yield return new WaitForSecondsRealtime(s); Go(_result); }

        void OnShare()
        {
            var o = _lastOutcome; if (o == null) return; _audio.Tap();
            _save.Count("share_requested"); _save.Save();
            int flies = o.FliesCaught + (o.Terminal == RoundState.Won ? 1 : 0);
            string text = _dailyKey != null ? Loc.F("share.daily", Daily.Pretty(_dailyKey), Loc.T("scene." + _scene.Id), flies, o.BestCombo)
                        : (flies > 0 ? Loc.F("share.won", flies, o.BestCombo, (o.ActiveMs / 1000f).ToString("0")) : Loc.F("share.timeout", 0, o.Attacks));
            byte[] png = null;
            try
            {
                string unit = flies == 1 ? Loc.T("card.fly") : Loc.T("card.flies");
                string sub = (_dailyKey != null ? Loc.F("daily.result", Daily.Pretty(_dailyKey), Loc.T("scene." + _scene.Id)) : Loc.T("scene." + _scene.Id) + " · " + System.DateTime.Now.ToString("dd/MM/yyyy"));
                string detail = Loc.F("card.detail", o.BestCombo, o.FastestCatchMs > 0 ? (o.FastestCatchMs / 1000f).ToString("0.0") : "-", (o.ActiveMs / 1000f).ToString("0")) + (_lastSeenPct >= 0 ? " · " + Loc.F("card.seen", _lastSeenPct) : "");
                var kind = _lastKind;
                png = ShareCard.RenderPng(flies.ToString(), unit, sub, detail, _scene, kind); Debug.Log($"[TDF] share card {png.Length} B");
            }
            catch (System.Exception e) { Debug.LogWarning("share card: " + e.Message); }
            var r = png != null ? Platform.ShareImage(png, text, CanonicalUrl) : Platform.Share(text, CanonicalUrl);
            _shareFeedback.text = r == ShareResult.Copied ? Loc.T("share.saved") : (r == ShareResult.Failed ? Loc.T("share.failed") : "");
        }

        bool _audioUnlocked;
        void Update()
        {
            if (!_audioUnlocked && UnityEngine.InputSystem.Pointer.current != null && UnityEngine.InputSystem.Pointer.current.press.wasPressedThisFrame) { _audioUnlocked = true; _audio.Unlock(); _audio.SetMusicLevel(1f); }
            _game?.Frame(Time.unscaledDeltaTime); _audio.Frame(Time.unscaledDeltaTime);
            if (_playRt != null && _start.gameObject.activeSelf) _playRt.localScale = Vector3.one * (_save.Data.totals.rounds == 0 && !_settings.ReducedEffects ? 1f + 0.035f * Mathf.Sin(Time.unscaledTime * 4.5f) : 1f);   // ADR-016: invitation to the first round
            if (_bgFade >= 0f)
            {   // cross-fade of the start screen's background layers
                _bgFade += Time.unscaledDeltaTime / 0.35f; float u = Mathf.Clamp01(_bgFade);
                var front = _bgOnA ? _startBgA : _startBgB; var back = _bgOnA ? _startBgB : _startBgA;
                front.GetComponent<Image>().color = new Color(1, 1, 1, u); back.GetComponent<Image>().color = new Color(1, 1, 1, 1f - u);
                if (u >= 1f) _bgFade = -1f;
            }
        }
        void OnApplicationPause(bool paused) { if (paused) _game?.PauseFromSystem(Loc.T("pause.background")); }
        void OnApplicationFocus(bool focus) { if (!focus) _game?.PauseFromSystem(Loc.T("pause.background")); }
        public void OnVisibility(string state) { if (state == "hidden") _game?.PauseFromSystem(Loc.T("pause.background")); }
#if TDF_DEVBUILD
        /// <summary>Development builds only (headless verification): "unlock" opens the Day, "stars" grants the three per scene, "boss" starts the bathroom with the boss as the 1st fly.</summary>
        public void OnDevCommand(string cmd)
        {
            if (cmd == "unlock" || cmd == "stars") { _save.DevGrantStars(cmd == "stars" ? 7 : 1); _save.Save(); if (_start.gameObject.activeSelf) { _carousel.Refresh(); UpdatePlay(); } }
            else if (cmd == "boss") { _save.DevGrantStars(1); _save.Save(); _scene = Scenes.Bath; _devBossIndex = 0; OnPlay(null); _devBossIndex = -1; }
            else if (cmd.StartsWith("bonus")) { var n = cmd.Length > 5 && char.IsDigit(cmd[cmd.Length - 1]) ? cmd[cmd.Length - 1] - '0' : 0; if (n > 0) _faceIdx = n; else PickFace(); if (n > 0) { _bonusBg.sprite = Resources.Load<Sprite>("art/face" + _faceIdx + "_bg"); _bonusWho.text = Loc.F("bonus.who", Loc.T("face." + _faceIdx)); } if (cmd.StartsWith("bonusintro")) Go(_bonusScreen); else { if (!_start.gameObject.activeSelf) Show(_start); StartBonus(); } }   // bonus, bonus3, bonusintro, bonusintro2
            else if (cmd == "resume") _game?.ResumeRequested();   // headless: recover from a technical pause (lag > 100 ms while capturing)
            else if (cmd == "record") { _save.RecordsFor("kitchen", false).bestFlies = 13; _save.RecordsFor("cafe", false).bestFlies = 7; _save.Save(); _carousel.Refresh(); }   // see the record pill
            else if (cmd.StartsWith("lang:")) { var code = cmd.Substring(5); _save.Data.prefs.language = code; Persist(); Loc.Load(code); BuildScreens(); Show(_start); }   // screenshots in the right language
            Debug.Log("[TDF] dev command " + cmd);
        }
        int _devBossIndex = -1;
#endif
        void OnDestroy() { _game?.Sim.Dispose(); }
    }
}
