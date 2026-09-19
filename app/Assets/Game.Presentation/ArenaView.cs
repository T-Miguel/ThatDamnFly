// 3:4 arena rendered by an orthographic camera whose viewport follows the arena's RectTransform in the UI.
// World coordinates = normalized arena coordinates (W = 1, H = 4/3). The art does not decide collisions.
// ADR-003: several flies (one per slot) with art per kind, burst balloon on the catch, trail, landing/take-off.
// ADR-005: the tools are objects of the scene - they sit in their place in the background and, when they become available, fly out to the bar, revealing what was behind them.
using System.Collections.Generic;
using ThatDamnFly.Domain;
using ThatDamnFly.Simulation;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ThatDamnFly.Presentation
{
    public sealed class ArenaView
    {
        public readonly Camera Cam;
        public readonly RectTransform Rect;
        readonly Transform _root;
        readonly SpriteRenderer _aim, _targetMark;
        readonly Dictionary<FlyKind, FlySprites> _flyArt = new Dictionary<FlyKind, FlySprites>();
        readonly List<FlyVisual> _flies = new List<FlyVisual>();
        readonly Dictionary<string, Sprite> _toolSprites = new Dictionary<string, Sprite>();
        readonly Dictionary<int, ToolVisual> _tools = new Dictionary<int, ToolVisual>();
        readonly List<Fx> _fx = new List<Fx>();
        readonly List<SpriteRenderer> _decals = new List<SpriteRenderer>();
        readonly List<Balloon> _balloons = new List<Balloon>();
        readonly Sprite _ring, _stars, _square, _trail, _balloonBurst, _lockSprite; Sprite[] _decalSprites;
        readonly Canvas _canvas;
        Rect _lastPixelRect; float _shake, _shakeAmp, _punch; readonly Vector3 _camHome;
        public bool ReducedEffects;
        public System.Action<Attack> ImpactCallback;
        readonly System.Random _decalRng = new System.Random(1);
        public SceneDef Scene { get; private set; }
        readonly List<HomeObject> _homes = new List<HomeObject>();
        /// <summary>Fired when a scene object reaches the tool bar.</summary>
        /// <summary>Slot of the bar where the tool is (rotating hand and mirrored bar); −1 if it is not in the hand.</summary>
        readonly SpriteRenderer _floor;

        sealed class FlySprites { public Sprite[] Frames; public Sprite Flat, Landed; }
        sealed class FlyVisual { public SpriteRenderer Sr, Shadow; public FlySprites Art; public FlyKind Kind; public float WingTimer, DeadTimer, TakeoffPulse, TrailTimer; public int WingFrame; public bool Dead; public float BaseScale; }
        sealed class ToolVisual { public SpriteRenderer Sr, Shadow; public Attack A; public bool ImpactShown; }
        SpriteRenderer _baitSr, _baitRing; Sprite _baitSprite;   // ADR-015
        sealed class Fx { public SpriteRenderer Sr; public float T, Life; public float Scale0, Scale1; public Color Color = Color.white; }
        /// <summary>ADR-017: scene object in its place - selectable by touch, thrown from there and back there; a lock while fury is not enough; cooldown ring.</summary>
        enum HomeState { Home, ToHand, InHand, InFlight, BackToHand, ToHome, Dragging }
        sealed class HomeObject { public SpriteRenderer Sr, Lock, Cd, Halo, Prog; public ToolDefinition Tool; public Vec2 Home; public float Width, Rot; public HomeState State; public float T, Pulse, Shake, Dur; public Vec2 From; }
        ToolDefinition _handTool;
        public System.Action<ToolDefinition> ToolArrived;
        // ADR-017 (3rd round): first-person hand in the bottom-right corner of the arena, holding the chosen object; attacks leave from here and the object returns here
        SpriteRenderer _handSr, _handHalo, _handObj; TMPro.TextMeshPro _handLabel; float _handPunch, _handLabelLeft, _handBounce; bool _handGlow;
        const float HandW = 0.23f, HandX = 0.72f, HandGripY = 0.075f;
        // ADR-019: the face bonus - eyes, mouth and blush over the background (the face), hand marks on misses, head shake and tickles
        bool _faceOn; SpriteRenderer _faceEyes, _faceMouth, _faceBlush, _faceOver;
        /// <summary>Character of the bonus (1…FaceCount), chosen by the Bootstrap before SetScene.</summary>
        public int FaceIndex = 1; public const int FaceCount = 5; Sprite _eyesOpen, _eyesSquint, _eyesAngry, _mouthNeutral, _mouthOuch, _mouthAngry, _faceMark;
        float _faceOuch, _faceShake, _faceBlink, _faceBlinkT, _faceTickle, _facePop; int _faceFury;
        public bool FaceMode => _faceOn;   // hand width (W), center x, height of the grip point (W)
        float _haloT;   // available objects glow (breathing yellow halo); no badges
        sealed class Balloon { public SpriteRenderer Bg; public TMPro.TextMeshPro Text; public float T, Life, Width, Height, Rot; }

        // fraction of the sprite width that corresponds to the contact area (the rest is handle/decoration)
        static float ContactFraction(string toolId)
        {
            switch (toolId)
            {
                case "kitchen_pan": return 400f / 512f;
                case "kitchen_cloth": case "picnic_napkin": case "living_cushion": case "beach_flipflop": case "cafe_menu": return 0.86f;
                case "picnic_frisbee": case "bath_paper": case "bath_plunger": case "living_newspaper": case "beach_ball": case "cafe_tray": case "yard_fan": case "yard_grate": return 472f / 512f;
                default: return 1f;
            }
        }
        static string ShortId(string toolId) => toolId.Substring(toolId.IndexOf('_') + 1);
        /// <summary>Home of each object in the scene background (arena coordinates), visual width, rotation and "at home" sprite.</summary>
        /// <summary>Homes of the second objects (ADR-013), by id.</summary>
        static (Vec2 home, float width, float rot, string sprite)? HomeOfAlt(string toolId)
        {
            switch (toolId)
            {
                case "kitchen_spatula": return (new Vec2(0.30f, 0.47f), 0.10f, 30f, "tool_spatula");
                case "kitchen_potlid": return (new Vec2(0.42f, 0.70f), 0.13f, 0f, "tool_potlid");
                case "kitchen_table": return (new Vec2(0.62f, 0.20f), 0.30f, 0f, "home_table");
                case "picnic_strawhat": return (new Vec2(0.16f, 0.80f), 0.13f, 0f, "tool_strawhat");
                case "picnic_plate": return (new Vec2(0.88f, 0.74f), 0.12f, 0f, "tool_plate");
                case "picnic_cooler": return (new Vec2(0.82f, 0.18f), 0.22f, 0f, "home_cooler");
                case "bath_sponge": return (new Vec2(0.50f, 0.93f), 0.09f, 10f, "tool_sponge");
                case "bath_brush": return (new Vec2(0.72f, 0.20f), 0.13f, 0f, "tool_brush");
                case "bath_bathmat": return (new Vec2(0.30f, 0.09f), 0.34f, 0f, "home_bathmat");
                case "living_remote": return (new Vec2(0.68f, 0.31f), 0.09f, -30f, "tool_remote");
                case "living_book": return (new Vec2(0.86f, 0.36f), 0.13f, 0f, "tool_book");
                case "living_rug": return (new Vec2(0.62f, 0.09f), 0.44f, 0f, "home_rug");
                case "beach_shovel": return (new Vec2(0.62f, 0.16f), 0.09f, 20f, "tool_shovel");
                case "beach_bucket": return (new Vec2(0.22f, 0.20f), 0.13f, 0f, "tool_bucket");
                case "beach_towel": return (new Vec2(0.15f, 0.62f), 0.24f, 0f, "home_towel");
                case "cafe_coaster": return (new Vec2(0.30f, 0.31f), 0.07f, 0f, "tool_coaster");
                case "cafe_plate": return (new Vec2(0.49f, 0.29f), 0.12f, 0f, "tool_plate");
                case "cafe_chair": return (new Vec2(0.91f, 0.60f), 0.13f, 0f, "home_chair");
                case "yard_spatula": return (new Vec2(0.44f, 0.44f), 0.10f, 40f, "tool_spatula");
                case "yard_pot": return (new Vec2(0.10f, 0.62f), 0.13f, 0f, "tool_pot");
                case "yard_table": return (new Vec2(0.16f, 0.18f), 0.28f, 0f, "home_table");
            }
            return null;
        }

        static (Vec2 home, float width, float rot, string sprite)? HomeOf(string sceneId, ToolRole role)
        {
            if (sceneId == "kitchen")
                switch (role)
                {
                    case ToolRole.Fast: return (new Vec2(0.154f, 0.429f), 0.11f, 0f, "tool_cloth");            // folded cloth on the edge of the counter
                    case ToolRole.Medium: return (new Vec2(0.642f, 0.825f), 0.16f, 135f, "tool_pan");         // pan hanging on the hook
                    case ToolRole.Giant: return (new Vec2(0.858f, 0.425f), 0.2333f, 0f, "home_fridge");       // fridge on the right
                }
            if (sceneId == "picnic")
                switch (role)
                {
                    case ToolRole.Fast: return (new Vec2(0.72f, 0.45f), 0.10f, 0f, "tool_napkin");            // napkin on the blanket
                    case ToolRole.Medium: return (new Vec2(0.16f, 0.62f), 0.14f, 0f, "tool_frisbee");         // frisbee on the grass
                    case ToolRole.Giant: return (new Vec2(0.317f, 0.358f), 0.267f, 0f, "home_basket");        // basket on the blanket
                }
            if (sceneId == "bath")
                switch (role)
                {
                    case ToolRole.Fast: return (new Vec2(0.16f, 0.66f), 0.10f, 0f, "tool_paper");             // roll on its holder
                    case ToolRole.Medium: return (new Vec2(0.60f, 0.30f), 0.16f, 0f, "tool_plunger");         // plunger next to the toilet
                    case ToolRole.Giant: return (new Vec2(0.80f, 0.33f), 0.24f, 0f, "home_lid");              // lid on the toilet
                }
            if (sceneId == "living")
                switch (role)
                {
                    case ToolRole.Fast: return (new Vec2(0.62f, 0.36f), 0.11f, -20f, "tool_newspaper");       // newspaper on the table
                    case ToolRole.Medium: return (new Vec2(0.30f, 0.34f), 0.16f, 8f, "tool_cushion");         // cushion on the sofa
                    case ToolRole.Giant: return (new Vec2(0.30f, 0.22f), 0.50f, 0f, "home_sofa");             // sofa
                }
            if (sceneId == "beach")
                switch (role)
                {
                    case ToolRole.Fast: return (new Vec2(0.30f, 0.30f), 0.10f, 25f, "tool_flipflop");         // flip-flop on the towel
                    case ToolRole.Medium: return (new Vec2(0.70f, 0.35f), 0.16f, 0f, "tool_ball");            // ball on the sand
                    case ToolRole.Giant: return (new Vec2(0.78f, 0.55f), 0.26f, 0f, "home_umbrella");         // parasol planted
                }
            if (sceneId == "cafe")
                switch (role)
                {
                    case ToolRole.Fast: return (new Vec2(0.36f, 0.34f), 0.10f, 10f, "tool_menu");             // menu on the table
                    case ToolRole.Medium: return (new Vec2(0.62f, 0.31f), 0.16f, 0f, "tool_tray");            // tray on the table
                    case ToolRole.Giant: return (new Vec2(0.80f, 0.55f), 0.26f, 0f, "home_parasol");          // sun umbrella on its pole
                }
            if (sceneId == "yard")
                switch (role)
                {
                    case ToolRole.Fast: return (new Vec2(0.30f, 0.50f), 0.11f, 0f, "tool_fan");               // fan on the table
                    case ToolRole.Medium: return (new Vec2(0.58f, 0.19f), 0.17f, 0f, "tool_grate");           // grill leaning
                    case ToolRole.Giant: return (new Vec2(0.80f, 0.36f), 0.30f, 0f, "home_grilllid");         // lid on the barbecue
                }
            return null;
        }

        public ArenaView(Canvas canvas, RectTransform rect, SceneDef scene)
        {
            _canvas = canvas; Rect = rect; Scene = scene ?? Scenes.Kitchen;
            _root = new GameObject("ArenaWorld").transform;
            var camGo = new GameObject("ArenaCamera"); camGo.transform.SetParent(_root, false);
            Cam = camGo.AddComponent<Camera>(); Cam.orthographic = true; Cam.orthographicSize = Arena.H * 0.5f; Cam.transform.position = new Vector3(Arena.W * 0.5f, Arena.H * 0.5f, -10f);
            Cam.clearFlags = CameraClearFlags.SolidColor; Cam.backgroundColor = Palette.Surface; Cam.depth = -1; Cam.nearClipPlane = 0.1f; Cam.farClipPlane = 50f; Cam.cullingMask &= ~(1 << ShareCard.Layer);
            var urp = Cam.GetUniversalAdditionalCameraData(); if (urp != null) urp.renderPostProcessing = false;
            _camHome = Cam.transform.position;
            _square = Ui.SquareSprite(); _ring = Resources.Load<Sprite>("art/fx_impact_ring"); _lockSprite = Resources.Load<Sprite>("art/icon_lock"); _stars = Resources.Load<Sprite>("art/fx_stars"); _trail = Resources.Load<Sprite>("art/fx_trail") ?? Ui.CircleSprite(32);
            _balloonBurst = Resources.Load<Sprite>("art/balloon_burst");
            // scene background
            _floor = MakeSprite("Scene", _square, Color.white, 0); _floor.transform.position = new Vector3(Arena.W * 0.5f, Arena.H * 0.5f, 5f);
            SetScene(Scene);
            // fly art per kind
            _flyArt[FlyKind.House] = LoadFly("fly_top_"); _flyArt[FlyKind.Bluebottle] = LoadFly("fly_bb_"); _flyArt[FlyKind.FruitFly] = LoadFly("fly_ff_"); _flyArt[FlyKind.Horsefly] = LoadFly("fly_hf_"); _flyArt[FlyKind.Boss] = LoadFly("fly_bs_"); _flyArt[FlyKind.Golden] = LoadFly("fly_gd_");
            foreach (var sc in Scenes.All) { foreach (var t in sc.Tools) _toolSprites[t.Id] = Resources.Load<Sprite>("art/tool_" + ShortId(t.Id)); foreach (var t in sc.Alternates) _toolSprites[t.Id] = Resources.Load<Sprite>("art/tool_" + ShortId(t.Id)); }   // primaries and alternates (ADR-013): without this the thrown object was a white square
            _aim = MakeSprite("Aim", _square, new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.22f), 8); _aim.enabled = false;
            _targetMark = MakeSprite("Target", _ring ?? Ui.CircleSprite(64), new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.6f), 7); _targetMark.enabled = false;
        }

        /// <summary>Swaps the background (and the decals) for another scene; called when a round starts.</summary>
        public void SetScene(SceneDef scene)
        {
            Scene = scene;
            if (scene.Id == "picnic") _decalSprites = new[] { Resources.Load<Sprite>("art/pdmg_grass"), Resources.Load<Sprite>("art/pdmg_juice"), Resources.Load<Sprite>("art/pdmg_melon"), Resources.Load<Sprite>("art/pdmg_dirt") };
            else if (scene.Id == "kitchen") _decalSprites = new[] { Resources.Load<Sprite>("art/dmg_tile_crack"), Resources.Load<Sprite>("art/dmg_splat"), Resources.Load<Sprite>("art/dmg_dent"), Resources.Load<Sprite>("art/dmg_cracks") };
            else _decalSprites = new[] { Resources.Load<Sprite>("art/" + scene.Id + "_dmg_1"), Resources.Load<Sprite>("art/" + scene.Id + "_dmg_2"), Resources.Load<Sprite>("art/" + scene.Id + "_dmg_3"), Resources.Load<Sprite>("art/" + scene.Id + "_dmg_4") };
            FaceEnable(scene.Id == "face");
            var bg = Resources.Load<Sprite>(scene.Id == "face" ? "art/face" + FaceIndex + "_bg" : "art/" + scene.Id + "_bg");   // ADR-019: every bonus shows another character
            if (bg != null) { _floor.sprite = bg; _floor.color = Color.white; _floor.transform.localScale = new Vector3(Arena.W / bg.bounds.size.x, Arena.H / bg.bounds.size.y, 1f); }
            else { _floor.sprite = _square; _floor.color = Palette.Paper; _floor.transform.localScale = new Vector3(Arena.W, Arena.H, 1f); }
            // scene objects in their places
            foreach (var h in _homes) { Object.Destroy(h.Sr.gameObject); Object.Destroy(h.Lock.gameObject); Object.Destroy(h.Cd.gameObject); Object.Destroy(h.Halo.gameObject); Object.Destroy(h.Prog.gameObject); } _homes.Clear();
            foreach (var t in Concat(scene.Tools, scene.Alternates))
            {
                var spec = System.Array.IndexOf(scene.Alternates, t) >= 0 ? HomeOfAlt(t.Id) : HomeOf(scene.Id, t.Role); if (spec == null) continue;
                var (home, width, rot, spriteName) = spec.Value; var sp = Resources.Load<Sprite>("art/" + spriteName); if (sp == null) continue;
                var sr = MakeSprite("Home_" + t.Id, sp, Color.white, 3);
                var lk = MakeSprite("Lock_" + t.Id, _lockSprite ?? _square, Color.white, 6); lk.enabled = false;
                var cd = MakeSprite("Cd_" + t.Id, _ring ?? Ui.CircleSprite(64), new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.55f), 6); cd.enabled = false;
                var halo = MakeSprite("Halo_" + t.Id, sp, new Color(Palette.Action.r, Palette.Action.g, Palette.Action.b, 0.6f), 2); halo.enabled = false;   // yellow copy behind: "this can be grabbed"
                var prog = MakeSprite("Prog_" + t.Id, _ring ?? Ui.CircleSprite(64), new Color(Palette.Fury.r, Palette.Fury.g, Palette.Fury.b, 0.9f), 5); prog.enabled = false;   // ring that grows with fury until it opens (replaces the fury bar)
                _homes.Add(new HomeObject { Sr = sr, Lock = lk, Cd = cd, Halo = halo, Prog = prog, Tool = t, Home = home, Width = width, Rot = rot });
            }
            ResetHomes();
        }

        static System.Collections.Generic.IEnumerable<ToolDefinition> Concat(ToolDefinition[] a, ToolDefinition[] b) { foreach (var t in a) yield return t; foreach (var t in b) yield return t; }

        void ResetHomes()
        {
            foreach (var h in _homes)
            {
                h.State = HomeState.Home; h.T = 0f; h.Pulse = 0f; h.Shake = 0f; h.Sr.enabled = true; h.Sr.sortingOrder = 3; h.Sr.color = Color.white;
                h.Sr.transform.position = new Vector3(h.Home.X, h.Home.Y, 4f); h.Sr.transform.rotation = Quaternion.Euler(0, 0, h.Rot);
                h.Sr.transform.localScale = Vector3.one * (h.Width / h.Sr.sprite.bounds.size.x);
                h.Lock.enabled = false; h.Cd.enabled = false; h.Halo.enabled = false; h.Prog.enabled = false;
            }
            _handTool = null;
        }

        float HomeScale(HomeObject h) => h.Width / h.Sr.sprite.bounds.size.x;
        HomeObject HomeOfTool(ToolDefinition t) { foreach (var h in _homes) if (h.Tool == t) return h; return null; }
        /// <summary>Position of the hand in arena coordinates (below the arena, where the chosen object's pill is).</summary>
        public Vec2 HandPos() => new Vec2(HandX * Arena.W, HandGripY * Arena.W);

        // ---------- ADR-019: the face ----------
        void FaceEnable(bool on)
        {
            _faceOn = on;
            if (on && _faceEyes == null)
            {
                _eyesOpen = Resources.Load<Sprite>("art/face_eyes_open"); _eyesSquint = Resources.Load<Sprite>("art/face_eyes_squint"); _eyesAngry = Resources.Load<Sprite>("art/face_eyes_angry");
                _mouthNeutral = Resources.Load<Sprite>("art/face_mouth_neutral"); _mouthOuch = Resources.Load<Sprite>("art/face_mouth_ouch"); _mouthAngry = Resources.Load<Sprite>("art/face_mouth_angry"); _faceMark = Resources.Load<Sprite>("art/face_mark");
                _faceBlush = MakeSprite("FaceBlush", Resources.Load<Sprite>("art/face_blush") ?? _square, new Color(1, 1, 1, 0f), 1);
                _faceEyes = MakeSprite("FaceEyes", _eyesOpen ?? _square, Color.white, 2); _faceMouth = MakeSprite("FaceMouth", _mouthNeutral ?? _square, Color.white, 2);
                _faceOver = MakeSprite("FaceOver", _square, Color.white, 3); _faceOver.enabled = false;
            }
            if (_faceEyes != null) { _faceEyes.enabled = on; _faceMouth.enabled = on; _faceBlush.enabled = on; var over = on ? Resources.Load<Sprite>("art/face" + FaceIndex + "_over") : null; _faceOver.sprite = over ?? _square; _faceOver.enabled = on && over != null; }
            _faceOuch = 0f; _faceShake = 0f; _faceTickle = 0f; _facePop = 0f; _faceFury = 0; _faceBlink = 2.5f;
            if (_floor != null) { _floor.transform.position = new Vector3(Arena.W * 0.5f, Arena.H * 0.5f, 5f); _floor.transform.rotation = Quaternion.identity; }
        }
        /// <summary>Miss: the hand lands on the face: "ouch", squeezed eyes, red hand mark, head shake.</summary>
        public void FaceSlap(Vec2 at)
        {
            if (!_faceOn) return; _faceOuch = 0.55f; _faceShake = 0.35f;
            var m = SpawnFx(_faceMark, at, 0.19f, 0.19f, 7f, Color.white, 3); if (m != null) m.Sr.transform.rotation = Quaternion.Euler(0, 0, Random.Range(-40f, 40f));
        }
        /// <summary>Burst (fury 100): red flash, steam and stars.</summary>
        public void FacePop()
        {
            if (!_faceOn) return; _facePop = 1.2f; _faceShake = 1.0f;
            var c = new Vec2(Arena.W * 0.5f, 0.62f * Arena.W);
            SpawnFx(_ring, c, 0.3f, 1.4f, 0.6f, new Color(Palette.Fury.r, Palette.Fury.g, Palette.Fury.b, 0.9f), 20);
            if (!ReducedEffects) for (int i = 0; i < 10; i++) { float a = i * Mathf.PI / 5f; SpawnFx(_stars, new Vec2(c.X + 0.25f * Mathf.Cos(a), c.Y + 0.3f * Mathf.Sin(a)), 0.05f, 0.16f, 0.9f, Color.white, 20); }
        }
        void RenderFace(WorldSimulation sim, float dt)
        {
            if (!_faceOn || _faceEyes == null) return;
            var r = sim.Round; _faceFury = r.Fury;
            if (_faceOuch > 0f) _faceOuch -= dt; if (_faceShake > 0f) _faceShake -= dt; if (_facePop > 0f) _facePop -= dt;
            _faceBlink -= dt; if (_faceBlink <= 0f) { _faceBlink = Random.Range(2.2f, 4.5f); _faceBlinkT = 0.13f; } if (_faceBlinkT > 0f) _faceBlinkT -= dt;
            // tickles: some fly landed on an ear
            bool tickle = false; foreach (var f in sim.Flies) if (f.Active && f.Body.Landed && f.Body.Position.Y > 0.42f * Arena.W && f.Body.Position.Y < 0.68f * Arena.W && (f.Body.Position.X < 0.24f * Arena.W || f.Body.Position.X > 0.76f * Arena.W)) { tickle = true; break; }
            _faceTickle = tickle ? Mathf.Min(1f, _faceTickle + dt * 3f) : Mathf.Max(0f, _faceTickle - dt * 3f);
            float shake = _faceShake > 0f ? 0.018f * Mathf.Sin(_faceShake * 50f) * Mathf.Clamp01(_faceShake / 0.35f) : 0f;
            float tilt = _faceTickle * 3f * Mathf.Sin(Time.unscaledTime * 9f); float dx = shake * Arena.W, dy = _faceTickle * 0.004f * Mathf.Sin(Time.unscaledTime * 14f);
            float pop = _facePop > 0f ? 1f + 0.06f * Mathf.Sin(Mathf.Clamp01(_facePop / 1.2f) * Mathf.PI * 3f) : 1f;
            _floor.transform.position = new Vector3(Arena.W * 0.5f + dx, Arena.H * 0.5f + dy, 5f); _floor.transform.rotation = Quaternion.Euler(0, 0, tilt);
            var bgs = _floor.sprite; float bs = bgs != null ? Arena.W / bgs.bounds.size.x : 1f; _floor.transform.localScale = new Vector3(bs * pop, (bgs != null ? Arena.H / bgs.bounds.size.y : 1f) * pop, 1f);
            // eyes and mouth follow the head
            bool ouch = _faceOuch > 0f, angry = _faceFury >= 60, cross = _faceFury >= 30;
            _faceEyes.sprite = ouch || _faceBlinkT > 0f || _faceTickle > 0.5f ? _eyesSquint : (angry ? _eyesAngry : _eyesOpen);
            _faceMouth.sprite = ouch ? _mouthOuch : (cross ? _mouthAngry : _mouthNeutral);
            var rot = Quaternion.Euler(0, 0, tilt);
            Vector3 P(float x, float y) => _floor.transform.position + rot * new Vector3((x - 0.5f) * Arena.W * pop, (y - 0.5f * Arena.H / Arena.W) * Arena.W * pop, 0f);
            _faceEyes.transform.position = P(0.5f, 0.721f) + new Vector3(0, 0, -1f); _faceEyes.transform.rotation = rot; _faceEyes.transform.localScale = Vector3.one * (0.50f * Arena.W / _faceEyes.sprite.bounds.size.x) * pop;
            _faceMouth.transform.position = P(0.5f, 0.344f) + new Vector3(0, 0, -1f); _faceMouth.transform.rotation = rot; _faceMouth.transform.localScale = Vector3.one * (0.29f * Arena.W / _faceMouth.sprite.bounds.size.x) * (ouch ? 1.2f : 1f) * pop;
            if (_faceOver.enabled) { _faceOver.transform.position = P(0.5f, 0.712f) + new Vector3(0, 0, -1.5f); _faceOver.transform.rotation = rot; _faceOver.transform.localScale = Vector3.one * (0.56f * Arena.W / _faceOver.sprite.bounds.size.x) * pop; }
            _faceBlush.transform.position = P(0.5f, 0.64f) + new Vector3(0, 0, -0.5f); _faceBlush.transform.rotation = rot; _faceBlush.transform.localScale = Vector3.one * (0.72f * Arena.W / _faceBlush.sprite.bounds.size.x) * pop;
            _faceBlush.color = new Color(1, 1, 1, Mathf.Clamp01(_faceFury / 100f) * 0.95f + (_facePop > 0f ? 0.3f : 0f));
        }
        /// <summary>Drop here (drag): bottom-right quadrant of the arena, around the hand.</summary>
        public bool IsOverHand(Vec2 p) => p.X > 0.46f * Arena.W && p.Y < 0.30f * Arena.W;
        public void DragHighlight(bool on) { _handGlow = on; }
        public void ShowHandLabel(string text) { EnsureHand(); _handLabel.text = text; _handLabelLeft = 1.8f; }
        void EnsureHand()
        {
            if (_handSr != null) return;
            var hs = Resources.Load<Sprite>("art/hand") ?? _square;
            _handHalo = MakeSprite("HandHalo", hs, new Color(Palette.Action.r, Palette.Action.g, Palette.Action.b, 0f), 39); _handHalo.enabled = false;
            _handSr = MakeSprite("Hand", hs, Color.white, 40);
            _handObj = MakeSprite("HandObj", _square, Color.white, 41); _handObj.enabled = false;
            var lgo = new GameObject("HandLabel"); lgo.transform.SetParent(_root, false); _handLabel = lgo.AddComponent<TMPro.TextMeshPro>();
            _handLabel.alignment = TMPro.TextAlignmentOptions.Center; _handLabel.sortingOrder = 42; _handLabel.textWrappingMode = TMPro.TextWrappingModes.NoWrap; _handLabel.overflowMode = TMPro.TextOverflowModes.Overflow;
            _handLabel.font = Ui.FontBold ?? Ui.FontDisplay; _handLabel.fontSize = 0.7f; _handLabel.color = Palette.Ink; _handLabel.rectTransform.sizeDelta = new Vector2(0.5f, 0.1f); _handLabel.text = "";
        }
        /// <summary>Hand: slow sway, punch when throwing, hop when the object arrives; the chosen object resting on the fingers; halo and label while dragging/choosing.</summary>
        void RenderHand(WorldSimulation sim, float dt)
        {
            EnsureHand();
            float t = Time.unscaledTime; float sway = ReducedEffects ? 0f : 1f;
            if (_handPunch > 0f) _handPunch -= dt; if (_handBounce > 0f) _handBounce -= dt;
            float punch = Mathf.Clamp01(_handPunch / 0.22f), bounce = Mathf.Clamp01(_handBounce / 0.3f);
            float hw = HandW * Arena.W; float hs = hw / _handSr.sprite.bounds.size.x; float hh = _handSr.sprite.bounds.size.y * hs;
            // the center of the palm (where the object rests) is 0.094 of the sprite height below the center; the wrist leaves through the bottom of the arena
            var grip = HandPos(); float cx = grip.X + sway * 0.004f * Mathf.Sin(t * 1.1f), cy = grip.Y + 0.094f * hh + sway * 0.003f * Mathf.Sin(t * 1.7f);
            float lift = 0.05f * Mathf.Sin(punch * Mathf.PI) * Arena.W - 0.012f * Mathf.Sin(bounce * Mathf.PI) * Arena.W;
            _handSr.transform.position = new Vector3(cx, cy + lift, -4f); _handSr.transform.localScale = Vector3.one * hs; _handSr.transform.rotation = Quaternion.Euler(0, 0, -6f * Mathf.Sin(punch * Mathf.PI) + sway * 1.5f * Mathf.Sin(t * 0.9f));
            _handHalo.enabled = _handGlow; if (_handGlow) { _handHalo.transform.position = new Vector3(cx, cy + lift, -3.9f); _handHalo.transform.localScale = Vector3.one * hs * 1.10f; _handHalo.color = new Color(Palette.Action.r, Palette.Action.g, Palette.Action.b, 0.55f + 0.35f * Mathf.Sin(t * 7f)); }
            // object in the hand
            var h = HomeOfTool(_handTool); bool show = h != null && h.State == HomeState.InHand;
            _handObj.enabled = show;
            if (show)
            {
                _handObj.sprite = h.Sr.sprite; float ow = Mathf.Min(0.17f * Arena.W, h.Width * 1.15f); float os = ow / h.Sr.sprite.bounds.size.x; float oh = h.Sr.sprite.bounds.size.y * os;
                _handObj.transform.position = new Vector3(cx, grip.Y + oh * 0.22f + lift, -4.5f); _handObj.transform.localScale = Vector3.one * os; _handObj.transform.rotation = Quaternion.Euler(0, 0, h.Rot * 0.3f - 8f * Mathf.Sin(punch * Mathf.PI));
                _handObj.color = Color.white;
            }
            // label (name/gesture or "Drop here") above the hand
            if (_handGlow) { _handLabel.text = _dropText ?? _handLabel.text; _handLabelLeft = Mathf.Max(_handLabelLeft, 0.2f); }
            if (_handLabelLeft > 0f) { _handLabelLeft -= dt; _handLabel.enabled = true; _handLabel.transform.position = new Vector3(cx - 0.30f * Arena.W, grip.Y + 0.04f * Arena.W, -5f); var c = _handLabel.color; c.a = Mathf.Clamp01(_handLabelLeft / 0.3f); _handLabel.color = c; }
            else _handLabel.enabled = false;
        }
        string _dropText; public void SetDropText(string t) { _dropText = t; }
        public void HandPunch() { _handPunch = 0.22f; }
        /// <summary>Where an attack with this tool starts: from the hand if it is in the hand, otherwise from its home.</summary>
        Vec2 LaunchPos(ToolDefinition t) { var h = HomeOfTool(t); return h == null ? HandPos() : (h.State == HomeState.Home ? h.Home : HandPos()); }
        /// <summary>ADR-017: touch on a scene object (inside its rectangle, with a minimum margin of 0.05 W) → returns it. Only those at home.</summary>
        public bool HitHome(Vec2 at, out ToolDefinition tool)
        {
            tool = null; float best = float.MaxValue;
            foreach (var h in _homes)
            {
                if (h.State != HomeState.Home) continue;
                float hw = Mathf.Max(0.05f, h.Width * 0.5f + 0.015f), hh = Mathf.Max(0.05f, h.Width * 0.5f * (h.Sr.sprite.bounds.size.y / h.Sr.sprite.bounds.size.x) + 0.015f);
                float dx = Mathf.Abs(at.X - h.Home.X), dy = Mathf.Abs(at.Y - h.Home.Y);
                if (dx <= hw && dy <= hh) { float d = dx / hw + dy / hh; if (d < best) { best = d; tool = h.Tool; } }
            }
            return tool != null;
        }
        public void PulseHome(ToolDefinition t) { var h = HomeOfTool(t); if (h == null || h.State != HomeState.Home) return; h.Pulse = 0.9f; if (!ReducedEffects) SpawnFx(_ring, h.Home, 0.06f, 0.2f, 0.35f, new Color(Palette.Action.r, Palette.Action.g, Palette.Action.b, 0.9f), 9); }
        public void ShakeHome(ToolDefinition t) { var h = HomeOfTool(t); if (h != null) h.Shake = 0.35f; }
        /// <summary>Choose: what was in the hand goes back home; the new one flies from home to the hand and stays there (it only returns to the scene when another is chosen).</summary>
        /// <summary>Drag (ADR-017): the object follows the finger, shrunk; on release it goes to the hand (chosen) or back home.</summary>
        public void BeginDrag(ToolDefinition t) { var h = HomeOfTool(t); if (h == null || h.State != HomeState.Home) return; h.State = HomeState.Dragging; h.From = h.Home; h.Sr.sortingOrder = 30; h.Lock.enabled = false; h.Cd.enabled = false; }
        public void DragTo(Vec2 p) { foreach (var h in _homes) if (h.State == HomeState.Dragging) { h.From = p; h.Sr.transform.position = new Vector3(p.X, p.Y + 0.02f, -3f); h.Sr.transform.localScale = Vector3.one * HomeScale(h) * 0.8f; h.Sr.transform.rotation = Quaternion.Euler(0, 0, h.Rot + 8f * Mathf.Sin(Time.unscaledTime * 12f)); } }
        public void EndDrag(bool toHand)
        {
            foreach (var h in _homes)
            {
                if (h.State != HomeState.Dragging) continue;
                if (toHand) { h.State = HomeState.Home; SelectHome(h.Tool, h.From); }
                else { h.State = HomeState.ToHome; h.T = 0.25f; h.Sr.sortingOrder = 14; }   // spring back home (shorter flight)
            }
        }
        public Vec2 ScreenToArenaUnclamped(Vector2 screen) { var w = Cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f)); return new Vec2(w.x, w.y); }

        public void SelectHome(ToolDefinition t, Vec2? from = null)
        {
            if (t == _handTool) return;
            var prev = HomeOfTool(_handTool); if (prev != null && (prev.State == HomeState.InHand || prev.State == HomeState.ToHand || prev.State == HomeState.BackToHand)) { prev.From = prev.State == HomeState.ToHand ? Vec2.Lerp(prev.Home, HandPos(), Mathf.Clamp01(prev.T)) : HandPos(); prev.State = HomeState.ToHome; prev.T = 0f; prev.Sr.enabled = true; prev.Sr.sortingOrder = 14; }
            _handTool = t; var h = HomeOfTool(t); if (h == null) return;
            if (h.State == HomeState.Home || h.State == HomeState.ToHome) { h.From = from ?? (h.State == HomeState.ToHome ? Vec2.Lerp(h.From, h.Home, Mathf.Clamp01(h.T)) : h.Home); h.State = HomeState.ToHand; h.T = 0f; h.Sr.enabled = true; h.Sr.sortingOrder = 14; h.Lock.enabled = false; h.Cd.enabled = false; }
        }

        /// <summary>Visual state of the scene objects: at home (lock/cooldown/pulse/shake), flying to the hand, in the hand (hidden - the pill shows it), in attack flight, returning to the hand or home.</summary>
        void RenderHomes(WorldSimulation sim, float dt)
        {
            var r = sim.Round; var hand = HandPos(); _haloT += dt;
            foreach (var h in _homes)
            {
                Attack flying = null; foreach (var a in r.Attacks) if (a.Tool == h.Tool && a.Phase != AttackPhase.Done) { flying = a; break; }
                if (flying != null) { h.State = HomeState.InFlight; h.From = flying.Target; h.Sr.enabled = false; h.Lock.enabled = false; h.Cd.enabled = false; h.Halo.enabled = false; h.Prog.enabled = false; continue; }
                if (h.State == HomeState.InFlight)
                {   // the attack ended: back to the hand (if still the chosen one) or home
                    h.State = h.Tool == _handTool ? HomeState.BackToHand : HomeState.ToHome; h.T = 0f; h.Sr.enabled = !ReducedEffects; h.Sr.sortingOrder = 14; h.Dur = Mathf.Max(0.22f, r.CooldownRemainingMs(h.Tool.Id) / 1000f + 0.02f);
                    if (ReducedEffects) { h.State = h.Tool == _handTool ? HomeState.InHand : HomeState.Home; h.Sr.enabled = h.State == HomeState.Home; }
                }
                float baseS = HomeScale(h);
                if (h.State == HomeState.ToHand || h.State == HomeState.BackToHand || h.State == HomeState.ToHome)
                {   // short arched flights: home → hand (shrinks), target → hand, hand → home (grows)
                    bool toHand = h.State != HomeState.ToHome; float dur = h.State == HomeState.BackToHand ? h.Dur : 0.42f;   // returns to the hand in exactly the cooldown time: when it arrives, it is ready
                    h.T += dt / dur; float u = Mathf.Clamp01(h.T); float e = u * u * (3f - 2f * u);
                    Vec2 a0 = h.State == HomeState.ToHome ? h.From : (h.State == HomeState.ToHand ? h.From : h.From), a1 = toHand ? hand : h.Home;
                    var p = Vec2.Lerp(a0, a1, e); float lift = (h.State == HomeState.BackToHand ? 0.04f : 0.10f) * Mathf.Sin(u * Mathf.PI);
                    h.Sr.transform.position = new Vector3(p.X, p.Y + lift, -2f);
                    float s0 = h.State == HomeState.ToHome ? 0.45f : (h.State == HomeState.BackToHand ? 1.0f : 1f), s1 = toHand ? 0.45f : 1f;
                    h.Sr.transform.localScale = Vector3.one * baseS * Mathf.Lerp(s0, s1, e); h.Sr.transform.rotation = Quaternion.Euler(0, 0, h.Rot + 20f * Mathf.Sin(u * Mathf.PI * 2f));
                    h.Sr.color = Color.white; h.Lock.enabled = false; h.Cd.enabled = false; h.Halo.enabled = false; h.Prog.enabled = false;
                    if (u >= 1f)
                    {
                        if (toHand) { h.State = HomeState.InHand; h.Sr.enabled = false; _handBounce = 0.3f; if (h.Tool == _handTool) ToolArrived?.Invoke(h.Tool); }
                        else { h.State = HomeState.Home; h.Sr.sortingOrder = 3; if (!ReducedEffects) SpawnFx(_ring, h.Home, 0.05f, 0.14f, 0.3f, new Color(1, 1, 1, 0.7f), 9); }
                    }
                    continue;
                }
                if (h.State == HomeState.InHand) { h.Sr.enabled = false; h.Lock.enabled = false; h.Cd.enabled = false; h.Halo.enabled = false; h.Prog.enabled = false; continue; }
                if (h.State == HomeState.Dragging) { h.Sr.enabled = true; h.Sr.color = Color.white; h.Halo.enabled = false; continue; }   // position given by DragTo
                // at home
                bool eligible = r.IsEligible(h.Tool); int cd = r.CooldownRemainingMs(h.Tool.Id);
                float pulse = 0f; if (h.Pulse > 0f) { h.Pulse -= dt; pulse = 0.18f * Mathf.Sin(Mathf.Clamp01(h.Pulse / 0.9f) * Mathf.PI); }
                float shake = 0f; if (h.Shake > 0f) { h.Shake -= dt; shake = 0.012f * Mathf.Sin(h.Shake * 60f) * Mathf.Clamp01(h.Shake / 0.35f); }
                h.Sr.enabled = true; h.Sr.transform.position = new Vector3(h.Home.X + shake, h.Home.Y, 4f); h.Sr.transform.localScale = Vector3.one * baseS * (1f + pulse); h.Sr.transform.rotation = Quaternion.Euler(0, 0, h.Rot); h.Sr.sortingOrder = 3;
                h.Sr.color = eligible ? Color.white : new Color(1, 1, 1, 0.62f);
                h.Lock.enabled = !eligible;
                h.Prog.enabled = !eligible;
                if (!eligible)
                {
                    float lw = 0.045f; var lp = new Vector3(h.Home.X + Mathf.Min(0.05f, h.Width * 0.35f), h.Home.Y + 0.02f, 3f);
                    h.Lock.transform.position = lp; h.Lock.transform.localScale = Vector3.one * (lw / h.Lock.sprite.bounds.size.x);
                    float prog01 = Mathf.Clamp01(r.Fury / (float)Mathf.Max(1, h.Tool.MinFury)); float rw = h.Prog.sprite.bounds.size.x;   // ring growing around the lock: when it closes, it opens
                    h.Prog.transform.position = new Vector3(lp.x, lp.y, 3.1f); h.Prog.transform.localScale = Vector3.one * (lw * (0.9f + 1.1f * prog01) / rw); h.Prog.color = new Color(Palette.Fury.r, Palette.Fury.g, Palette.Fury.b, 0.35f + 0.55f * prog01);
                }
                h.Cd.enabled = eligible && cd > 0;
                if (h.Cd.enabled) { float f = Mathf.Clamp01(cd / (float)Mathf.Max(1, h.Tool.CooldownMs)); float rw = h.Cd.sprite.bounds.size.x; h.Cd.transform.position = new Vector3(h.Home.X, h.Home.Y, 3.2f); h.Cd.transform.localScale = Vector3.one * (Mathf.Max(0.06f, h.Width * 0.8f) * (0.5f + 0.5f * f) / rw); }
                // "grabbable": breathing yellow halo behind the available objects; hand badge in the corner (while the player has not yet learned to switch)
                h.Halo.enabled = eligible && !ReducedEffects;
                if (h.Halo.enabled)
                {
                    float breathe = 0.5f + 0.5f * Mathf.Sin(_haloT * 2.6f + h.Home.X * 7f);
                    h.Halo.transform.position = new Vector3(h.Home.X + shake, h.Home.Y, 4.2f); h.Halo.transform.rotation = h.Sr.transform.rotation;
                    h.Halo.transform.localScale = Vector3.one * baseS * (1f + pulse) * (1.10f + 0.06f * breathe); h.Halo.color = new Color(Palette.Action.r, Palette.Action.g, Palette.Action.b, 0.45f + 0.35f * breathe);
                }
            }
        }


        static FlySprites LoadFly(string prefix)
        {
            var fs = new FlySprites { Frames = new[] { Resources.Load<Sprite>("art/" + prefix + "wings0"), Resources.Load<Sprite>("art/" + prefix + "wings1"), Resources.Load<Sprite>("art/" + prefix + "wings2") }, Flat = Resources.Load<Sprite>("art/" + prefix + "flat"), Landed = Resources.Load<Sprite>("art/" + prefix + "landed") };
            return fs;
        }

        SpriteRenderer MakeSprite(string name, Sprite s, Color c, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.color = c; sr.sortingOrder = order; return sr;
        }

        public void SyncViewport()
        {
            var corners = new Vector3[4]; Rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(_canvas.worldCamera, corners[0]), max = RectTransformUtility.WorldToScreenPoint(_canvas.worldCamera, corners[2]);
            var px = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
            if (px != _lastPixelRect) { _lastPixelRect = px; Cam.rect = new Rect(px.x / Screen.width, px.y / Screen.height, px.width / Screen.width, px.height / Screen.height); }
        }

        public bool ScreenToArena(Vector2 screen, out Vec2 arena)
        {
            arena = default;
            if (!_lastPixelRect.Contains(screen)) return false;
            var w = Cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f)); arena = new Vec2(w.x, w.y); return true;
        }

        public void ShowAim(bool on, Vec2 at, float w, float h)
        {
            _aim.enabled = on; if (!on) return;
            _aim.transform.position = new Vector3(at.X, at.Y, 1f); _aim.transform.localScale = new Vector3(w, h, 1f);
        }

        FlyVisual VisualFor(int slot)
        {
            while (_flies.Count <= slot)
            {
                var art = _flyArt[FlyKind.House];
                var v = new FlyVisual { Art = art, Kind = FlyKind.House };
                v.Shadow = MakeSprite("FlyShadow" + slot, Ui.CircleSprite(64), new Color(0, 0, 0, 0.18f), 9); v.Shadow.transform.localScale = Vector3.one * 0.05f;
                v.Sr = MakeSprite("Fly" + slot, art.Frames[0] ?? _square, Color.white, 10 + slot);
                ApplyKind(v, FlyKind.House);
                _flies.Add(v);
            }
            return _flies[slot];
        }

        void ApplyKind(FlyVisual v, FlyKind kind)
        {
            v.Kind = kind; v.Art = _flyArt[kind];
            // the body takes ~0.62 of the sprite width (256 px) → scale so that the visual body measures FlyVisualSize × the kind's scale
            float bodyFrac = 0.62f; float spriteW = v.Art.Frames[0] != null ? v.Art.Frames[0].bounds.size.x : 1f;
            v.BaseScale = (Arena.FlyVisualSize * FlyKinds.Of(kind).VisualScale) / (spriteW * bodyFrac);
            v.Sr.transform.localScale = Vector3.one * v.BaseScale; if (v.Art.Frames[0] != null) v.Sr.sprite = v.Art.Frames[0];
        }

        public void Render(WorldSimulation sim, float dt)
        {
            var round = sim.Round;
            for (int i = 0; i < sim.Flies.Count; i++)
            {
                var ag = sim.Flies[i]; var v = VisualFor(i); var b = ag.Body;
                if (v.Kind != ag.State.Kind.Kind) ApplyKind(v, ag.State.Kind.Kind);
                bool dead = !ag.Active;
                v.Sr.transform.position = new Vector3(b.Position.X, b.Position.Y, 0f);
                if (dead)
                {
                    if (!v.Dead) { v.Dead = true; v.DeadTimer = 0f; if (v.Art.Flat != null) v.Sr.sprite = v.Art.Flat; v.Sr.enabled = true; SpawnFx(_stars, b.Position, 0.06f, 0.16f, 0.6f); v.Sr.transform.localScale = Vector3.one * v.BaseScale; }
                    v.DeadTimer += dt; v.Shadow.enabled = false;
                    if (v.DeadTimer > 0.45f) v.Sr.enabled = false;   // the flattened fly disappears before the next one enters
                    continue;
                }
                if (v.Dead) { v.Dead = false; v.Sr.enabled = true; }
                v.Sr.transform.rotation = Quaternion.Euler(0, 0, b.VisualHeading * Mathf.Rad2Deg);
                float speed = b.Speed;
                if (b.Landed && v.Art.Landed != null) v.Sr.sprite = v.Art.Landed;
                else
                {
                    v.WingTimer += dt * (1f + speed * 2f);
                    if (v.WingTimer > 0.035f) { v.WingTimer = 0f; v.WingFrame = (v.WingFrame + 1) % 3; }
                    if (v.Art.Frames[v.WingFrame] != null) v.Sr.sprite = v.Art.Frames[v.WingFrame];
                }
                // shadow: glued to the body when landed, offset and larger in flight; take-off gives a scale pulse
                float lift = b.Landed ? 0f : 1f; float pulse = 1f;
                if (v.TakeoffPulse > 0f) { v.TakeoffPulse -= dt; pulse = 1f + 0.35f * Mathf.Sin(Mathf.Clamp01(v.TakeoffPulse / 0.22f) * Mathf.PI); }
                v.Sr.transform.localScale = Vector3.one * (v.BaseScale * pulse);
                v.Shadow.enabled = true; float sh = (0.035f + 0.025f * lift + 0.02f * Mathf.Clamp01(speed / 1.2f)) * FlyKinds.Of(v.Kind).VisualScale;
                v.Shadow.transform.localScale = new Vector3(sh, sh * 0.7f, 1f);
                v.Shadow.transform.position = new Vector3(b.Position.X + 0.004f + 0.008f * lift, b.Position.Y - 0.004f - 0.01f * lift, 0.5f);
                v.Shadow.color = new Color(0, 0, 0, b.Landed ? 0.3f : 0.18f);
                // trail while escaping/darting
                if (!ReducedEffects && speed > 0.75f) { v.TrailTimer += dt; if (v.TrailTimer > 0.025f) { v.TrailTimer = 0f; SpawnFx(_trail, b.Position, 0.014f * FlyKinds.Of(v.Kind).VisualScale, 0.004f, 0.22f, new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.35f), 9); } }
            }
            for (int i = sim.Flies.Count; i < _flies.Count; i++) { _flies[i].Sr.enabled = false; _flies[i].Shadow.enabled = false; }
            RenderHomes(sim, dt); RenderHand(sim, dt); RenderFace(sim, dt);
            if (_punch > 0f) { _punch -= dt; float u = Mathf.Clamp01(_punch / 0.25f); Cam.orthographicSize = Arena.H * 0.5f * (1f - 0.05f * Mathf.Sin(u * Mathf.PI)); if (_punch <= 0f) Cam.orthographicSize = Arena.H * 0.5f; }
            if (_shake > 0f) { _shake -= dt; float a = Mathf.Clamp01(_shake / 0.25f); Cam.transform.position = _camHome + new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * (_shakeAmp * a); if (_shake <= 0f) Cam.transform.position = _camHome; }
            // tools
            foreach (var a in round.Attacks)
            {
                bool visible = a.Phase != AttackPhase.Done;
                if (!_tools.TryGetValue(a.Id, out var tv))
                {
                    if (!visible) continue;
                    if (!_toolSprites.TryGetValue(a.Tool.Id, out var ts) || ts == null) { ts = Resources.Load<Sprite>("art/tool_" + ShortId(a.Tool.Id)); _toolSprites[a.Tool.Id] = ts; }   // lazy load by id: covers scenes outside the Day (e.g. the bonus slap) - without this it was a white square
                    var ts0 = ts != null ? ts : _square;
                    var sr = MakeSprite("Attack" + a.Id, ts0, Color.white, 12); if (a.Tool == _handTool) _handPunch = 0.22f;
                    var sh = MakeSprite("AttackShadow" + a.Id, ts0, new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0f), 11); sh.enabled = false;   // ADR-015: shadow that grows while the object comes down
                    tv = new ToolVisual { Sr = sr, Shadow = sh, A = a }; _tools[a.Id] = tv;
                }
                float sw = tv.Sr.sprite.bounds.size.x, shh = tv.Sr.sprite.bounds.size.y; float frac = ContactFraction(a.Tool.Id);
                Vector3 baseScale = a.Tool.Shape == ToolShape.Circle ? Vector3.one * ((a.ScaledRadius * 2f) / (sw * frac)) : new Vector3(a.ScaledRectW / sw, a.ScaledRectH / shh, 1f);
                baseScale.z = 1f;
                if (a.Phase == AttackPhase.Preparing)
                {
                    float t = Mathf.Clamp01((round.SimMs - a.ConfirmedAtSimMs) / (float)Mathf.Max(1, a.PrepEndSimMs - a.ConfirmedAtSimMs));
                    var from = LaunchPos(a.Tool); float e = 1f - (1f - t) * (1f - t);   // ADR-017: leaves from the hand (or from home), in an arc, and falls on the target
                    var pp = Vec2.Lerp(from, a.Target, e); float arc = 0.10f * Mathf.Sin(t * Mathf.PI);
                    tv.Sr.enabled = true; tv.Sr.transform.position = new Vector3(pp.X, pp.Y + arc, 1f);
                    float grow = 1.45f - 0.45f * t; tv.Sr.transform.localScale = new Vector3(baseScale.x * grow, baseScale.y * grow, 1f);
                    tv.Sr.transform.rotation = a.Tool.Role == ToolRole.Medium ? Quaternion.Euler(0, 0, -540f * t) : Quaternion.identity;   // pan/frisbee spin while falling
                    tv.Sr.color = new Color(1, 1, 1, 0.35f + 0.65f * t);
                    if (!ReducedEffects)
                    {   // shadow on the floor: starts small and faint, grows and darkens until impact; follows the object's rotation
                        tv.Shadow.enabled = true; tv.Shadow.transform.position = new Vector3(a.Target.X + 0.012f, a.Target.Y - 0.012f, 1.5f);
                        float sg = 0.55f + 0.45f * t; tv.Shadow.transform.localScale = new Vector3(baseScale.x * sg, baseScale.y * sg, 1f); tv.Shadow.transform.rotation = tv.Sr.transform.rotation;
                        tv.Shadow.color = new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.10f + 0.22f * t);
                    }
                    _targetMark.enabled = true; _targetMark.transform.position = new Vector3(a.Target.X, a.Target.Y, 2f);
                    float mr = a.Tool.Shape == ToolShape.Circle ? a.ScaledRadius * 2f : Mathf.Max(a.ScaledRectW, a.ScaledRectH) * 0.5f;
                    _targetMark.transform.localScale = Vector3.one * (mr / _targetMark.sprite.bounds.size.x);
                }
                else if (a.Phase == AttackPhase.Active)
                {
                    tv.Sr.enabled = true; tv.Sr.transform.position = new Vector3(a.Target.X, a.Target.Y, 1f); tv.Sr.transform.localScale = baseScale; tv.Sr.transform.rotation = Quaternion.identity; tv.Sr.color = Color.white; _targetMark.enabled = false; tv.Shadow.enabled = false;
                    if (!tv.ImpactShown) { tv.ImpactShown = true; OnImpact(a); }
                }
                else
                {
                    tv.Sr.color = new Color(1, 1, 1, Mathf.MoveTowards(tv.Sr.color.a, 0f, dt * 5f)); if (tv.Sr.color.a <= 0.01f) tv.Sr.enabled = false; tv.Shadow.enabled = false;
                }
            }
            if (round.ActiveAttack == null) _targetMark.enabled = false;
            // ADR-015: bait on the floor with a ring of the remaining time
            if (sim.BaitActive)
            {
                if (_baitSr == null) { _baitSprite = Resources.Load<Sprite>("art/bait"); _baitSr = MakeSprite("Bait", _baitSprite ?? _square, Color.white, 8); _baitRing = MakeSprite("BaitRing", _ring ?? Ui.CircleSprite(64), new Color(Palette.Action.r, Palette.Action.g, Palette.Action.b, 0.8f), 7); }
                _baitSr.enabled = true; _baitRing.enabled = true; var bp = sim.BaitPos; float bw = _baitSr.sprite.bounds.size.x;
                _baitSr.transform.position = new Vector3(bp.X, bp.Y + 0.01f, 3f); _baitSr.transform.localScale = Vector3.one * (0.075f / bw) * (1f + 0.03f * Mathf.Sin(Time.unscaledTime * 6f));
                float rw = _baitRing.sprite.bounds.size.x; _baitRing.transform.position = new Vector3(bp.X, bp.Y, 3.5f); _baitRing.transform.localScale = Vector3.one * (WorldSimulation.BaitRadius * 2f * (0.6f + 0.6f * sim.BaitFraction) / rw);
            }
            else if (_baitSr != null && _baitSr.enabled) { _baitSr.enabled = false; _baitRing.enabled = false; }
            for (int i = _fx.Count - 1; i >= 0; i--)
            {
                var f = _fx[i]; f.T += dt; float u = Mathf.Clamp01(f.T / f.Life);
                f.Sr.transform.localScale = Vector3.one * (Mathf.Lerp(f.Scale0, f.Scale1, u) / f.Sr.sprite.bounds.size.x); f.Sr.color = new Color(f.Color.r, f.Color.g, f.Color.b, f.Color.a * (1f - u));
                if (u >= 1f) { Object.Destroy(f.Sr.gameObject); _fx.RemoveAt(i); }
            }
            for (int i = _balloons.Count - 1; i >= 0; i--)
            {
                var bl = _balloons[i]; bl.T += dt;
                float pop = bl.T < 0.12f ? (bl.T / 0.12f) * 1.15f : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((bl.T - 0.12f) / 0.18f));
                float wobble = 1f + 0.04f * Mathf.Sin(bl.T * 22f);
                float alpha = Mathf.Clamp01((bl.Life - bl.T) / 0.25f);
                float s = pop * wobble * (bl.Width / bl.Bg.sprite.bounds.size.x);
                bl.Bg.transform.localScale = Vector3.one * s; bl.Bg.transform.rotation = Quaternion.Euler(0, 0, bl.Rot);
                bl.Bg.color = new Color(1, 1, 1, alpha); var tc = bl.Text.color; tc.a = alpha; bl.Text.color = tc;
                bl.Text.transform.localScale = Vector3.one * pop; bl.Text.transform.rotation = Quaternion.Euler(0, 0, bl.Rot);
                if (bl.T >= bl.Life) { Object.Destroy(bl.Bg.gameObject); Object.Destroy(bl.Text.gameObject); _balloons.RemoveAt(i); }
            }
        }

        /// <summary>ADR-015: the bait drops - yellow ring and sweet sparks.</summary>
        public void BaitFx(Vec2 at)
        {
            SpawnFx(_ring ?? Ui.CircleSprite(64), at, 0.03f, 0.16f, 0.35f, new Color(Palette.Action.r, Palette.Action.g, Palette.Action.b, 0.9f), 9);
            if (!ReducedEffects) for (int i = 0; i < 6; i++) { float ang = i * Mathf.PI / 3f; SpawnFx(_square, new Vec2(at.X + 0.03f * Mathf.Cos(ang), at.Y + 0.03f * Mathf.Sin(ang)), 0.012f, 0.002f, 0.4f, Palette.Action, 9); }
        }

        void OnImpact(Attack a)
        {
            bool circle = a.Tool.Shape == ToolShape.Circle;
            SpawnFx(_ring, a.Target, circle ? a.ScaledRadius * 1.6f : a.ScaledRectW * 0.6f, circle ? a.ScaledRadius * 3.2f : a.ScaledRectW * 1.1f, ReducedEffects ? 0.15f : 0.3f);
            if (!ReducedEffects && a.Tool.Role != ToolRole.Fast) SpawnFx(_stars, a.Target, 0.08f, 0.2f, 0.35f);
            if (_decals.Count < 10 && _decalSprites[0] != null)
            {
                var sp = _decalSprites[_decalRng.Next(_decalSprites.Length)];
                var d = MakeSprite("Decal", sp, new Color(1, 1, 1, 0.85f), 2); d.transform.position = new Vector3(a.Target.X, a.Target.Y, 3f);
                float size = a.Tool.Role == ToolRole.Giant ? 0.22f : (a.Tool.Role == ToolRole.Medium ? 0.16f : 0.09f); d.transform.localScale = Vector3.one * (size / sp.bounds.size.x);
                d.transform.rotation = Quaternion.Euler(0, 0, (float)_decalRng.NextDouble() * 360f); _decals.Add(d);
            }
            ImpactCallback?.Invoke(a);
        }

        public void Shake(float amp) { if (ReducedEffects) return; _shake = 0.25f; _shakeAmp = amp; }
        /// <summary>Small camera zoom on the catch.</summary>
        public void Punch() { if (ReducedEffects) return; _punch = 0.25f; }

        /// <summary>Short balloon at a point (ADR-019: the character's "OUCH!").</summary>
        public void Shout(Vec2 at, string text) { ShowBalloon(new Vec2(at.X, at.Y + 0.1f), text, 0.8f, Random.Range(-8f, 8f)); }

        /// <summary>Comic-book burst ("SPLAT!") at the point of the catch.</summary>
        public void Splat(Vec2 at, string text)
        {
            ShowBalloon(new Vec2(at.X, at.Y + 0.09f), text, 1.0f, Random.Range(-10f, 10f));
            SpawnFx(_stars, at, 0.08f, 0.22f, 0.5f);
        }

        static Vec2 ClampBalloon(Vec2 at, float width, float height) => new Vec2(Mathf.Clamp(at.X, width * 0.5f + 0.01f, Arena.W - width * 0.5f - 0.01f), Mathf.Clamp(at.Y, height * 0.5f + 0.01f, Arena.H - height * 0.5f - 0.01f));

        public void TakeoffFx(FlyAgent ag)
        {
            var v = VisualFor(ag.Slot); v.TakeoffPulse = 0.22f;
            if (!ReducedEffects) SpawnFx(_ring, ag.Body.Position, 0.03f, 0.09f, 0.25f, new Color(1, 1, 1, 0.7f), 9);
        }

        Balloon ShowBalloon(Vec2 at, string text, float life, float rot)
        {
            var sprite = _balloonBurst; if (sprite == null) return null;
            while (_balloons.Count >= 3) { var old = _balloons[0]; Object.Destroy(old.Bg.gameObject); Object.Destroy(old.Text.gameObject); _balloons.RemoveAt(0); }
            float width = 0.34f; float height = width * sprite.bounds.size.y / sprite.bounds.size.x;
            var c0 = ClampBalloon(at, width, height); float x = c0.X, y = c0.Y;
            var bg = MakeSprite("Balloon", sprite, Color.white, 30); bg.transform.position = new Vector3(x, y, -1f);
            var tgo = new GameObject("BalloonText"); tgo.transform.SetParent(_root, false); var tmp = tgo.AddComponent<TMPro.TextMeshPro>();
            tmp.text = text; tmp.alignment = TMPro.TextAlignmentOptions.Center; tmp.sortingOrder = 31;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap; tmp.overflowMode = TMPro.TextOverflowModes.Overflow;   // a word never breaks ("SPLA-T"): the auto-size shrinks until it fits
            tmp.enableAutoSizing = true;
            tmp.font = Ui.FontDisplay ?? Ui.FontBold; tmp.color = Palette.Fury; tmp.fontSizeMin = 0.45f; tmp.fontSizeMax = 1.5f; tmp.rectTransform.sizeDelta = new Vector2(width * 0.72f, height * 0.46f);
            tmp.transform.position = new Vector3(x, y, -1.5f);
            var bl = new Balloon { Bg = bg, Text = tmp, Life = life, Width = width, Height = height, Rot = rot }; _balloons.Add(bl); return bl;
        }

        void SpawnFx(Sprite s, Vec2 at, float s0, float s1, float life) => SpawnFx(s, at, s0, s1, life, Color.white, 14);
        Fx SpawnFx(Sprite s, Vec2 at, float s0, float s1, float life, Color color, int order)
        {
            if (s == null) return null;
            var sr = MakeSprite("Fx", s, color, order); sr.transform.position = new Vector3(at.X, at.Y, 0.5f); sr.transform.localScale = Vector3.one * (s0 / s.bounds.size.x);
            var fx = new Fx { Sr = sr, Scale0 = s0, Scale1 = s1, Life = life, Color = color }; _fx.Add(fx); return fx;
        }

        public void Clear()
        {
            foreach (var kv in _tools) { Object.Destroy(kv.Value.Sr.gameObject); if (kv.Value.Shadow != null) Object.Destroy(kv.Value.Shadow.gameObject); } _tools.Clear();
            if (_baitSr != null) { _baitSr.enabled = false; _baitRing.enabled = false; }
            foreach (var f in _fx) Object.Destroy(f.Sr.gameObject); _fx.Clear();
            foreach (var d in _decals) Object.Destroy(d.gameObject); _decals.Clear();
            foreach (var b in _balloons) { Object.Destroy(b.Bg.gameObject); Object.Destroy(b.Text.gameObject); } _balloons.Clear();
            foreach (var v in _flies) { Object.Destroy(v.Sr.gameObject); Object.Destroy(v.Shadow.gameObject); } _flies.Clear();
            _aim.enabled = false; _targetMark.enabled = false; _shake = 0f; _punch = 0f; Cam.orthographicSize = Arena.H * 0.5f; Cam.transform.position = _camHome; ResetHomes();
        }

        public void SetActive(bool on) { _root.gameObject.SetActive(on); }
        public void Destroy() { Object.Destroy(_root.gameObject); }
    }
}
