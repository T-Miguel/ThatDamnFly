// Controls a round: input (one active gesture), HUD, event hints, pause/resume, sound/vibration. Rules in Game.Domain; simulation in Game.Simulation.
// ADR-003: combos, extra time, cartoon balloons on the catch, take-off, fly kinds, guided first round. The fly does not talk: the escape is signalled only with sound and trail.
using System;
using ThatDamnFly.Domain;
using ThatDamnFly.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ThatDamnFly.Presentation
{
    public sealed class GameSettings { public bool Sound = true, Music = true, Vibration = true, ReducedEffects = false, Assist = false, ToolsRight = false, HighContrast = false; }

    public sealed class GameController
    {
        public const int SplatLines = 6;
        public readonly WorldSimulation Sim;
        readonly ArenaView _arena; readonly HudView _hud; readonly AudioManager _audio; readonly GameSettings _settings;
        readonly SceneDef _scene; ToolDefinition _selected;
        bool _aiming; bool _pointerDown; bool _overCancel;
        float _resumeLeft; int _lastCountdownShown;
        public event Action<RoundOutcome> RoundEnded;
        /// <summary>ADR-017: the player dragged an object to the hand (so the Bootstrap can count and remove the badges once learned).</summary>
        public event Action ToolSwitched;
        /// <summary>First rounds (the Bootstrap decides before building): the initial hint explains the drag instead of "tap to swat".</summary>
        public static bool FirstRoundsHint;
        bool _ended, _tutorial; float _hitStop; bool _sawShown;
        /// <summary>Text of the fly of the day in the HUD (null = normal round).</summary>
        public string DailyLabel { set { _hud.SetLabel(value); } }
        // hints: at most one at a time and three per round; they never block
        int _hintsShown; float _hintTimer; bool _hintFuryShown, _hintThresholdShown, _hintFridgeShown;
        int _lastFury, _lastFuryLevel; int _lastAttackCount;
        float _escapeCooldown, _takeoffCooldown; int _lastSplat = -1; bool _twoShown, _threeShown;
        readonly System.Random _rng;   // phrases/exclamations: seeded by the round's seed (varies per round)

        /// <summary>Recording of this round (for "watch the replay").</summary>
        public readonly ReplayData Recording;
        readonly ReplayData _replay; int _replayNext; float _replayAcc; public event System.Action ReplayFinished; bool _replayReady;
        public bool IsReplay => _replay != null;

        public GameController(WorldSimulation sim, ArenaView arena, HudView hud, AudioManager audio, GameSettings settings, bool firstRun, SceneDef scene, ReplayData replay = null)
        {
            _replay = replay;
            Recording = replay ?? new ReplayData { Seed = sim.Seed, SceneId = (scene ?? Scenes.Kitchen).Id, Assisted = sim.Round.Assisted };
            Sim = sim; _arena = arena; _hud = hud; _audio = audio; _settings = settings; _rng = new System.Random((int)(sim.Seed % 2147483647UL));
            _scene = scene ?? Scenes.Kitchen; _bonus = _scene.Id == "face";
            _selected = _scene.Tools[0]; _hud.SetScene(_scene); _hud.SetAccessibility(settings.ToolsRight, settings.HighContrast); _audio.SetScene(_scene.Id, _scene.Outdoor);   // ADR-017: every scene object is at hand; starts with the primary fast one
            _arena.ToolArrived = t => { _audio.Tap(); }; _arena.SetDropText(Loc.T("hud.drophere"));
            _arena.SelectHome(_selected); _hud.SetSelected(_selected);
            _arena.ReducedEffects = settings.ReducedEffects; _arena.ImpactCallback = OnImpact;
            Sim.FlyCaught += OnFlyCaught; Sim.FlySpawned += OnFlySpawned; Sim.FlyEscaped += OnFlyEscaped; Sim.FlyTookOff += OnFlyTookOff; Sim.TimeExtended += OnTimeExtended;
            Sim.Round.ObjectiveCompleted += OnObjectiveDone; Sim.Round.BossCaught += OnBossCaught;   // ADR-014
            if (_bonus) Sim.Round.AttackMissed += a => { _arena.FaceSlap(a.Target); _arena.Shout(a.Target, Loc.T("fx.ouch")); _audio.Ouch(Mathf.Lerp(1.15f, 0.8f, Sim.Round.Fury / 100f)); if (_settings.Vibration) Platform.Vibrate(false); };   // ADR-019: slap on the face
            Sim.BaitEarned += OnBaitEarned; Sim.BaitPlaced += at => { _arena.BaitFx(at); _audio.Bait(); };   // ADR-015
            _hud.BaitTap = ToggleBait;
            _audio.SetMusicLevel(0.55f); _audio.SetAmbience(true);
            if (replay != null) { _hud.SetLabel(Loc.T("replay.label")); _hud.ShowHint(Loc.T("replay.hint")); Sim.MarkReady(); }
            else if (firstRun) { _tutorial = true; _firstRun = true; _hintCap = 5; _hud.ShowTutorial(Loc.T("tutorial.body"), Loc.T("tutorial.go"), () => { _tutorial = false; _audio.Tap(); ShowHint(Loc.T("hint.tap"), 6f); Sim.MarkReady(); }); }
            else { ShowHint(Loc.T(_bonus ? "hint.bonus" : (FirstRoundsHint ? "hint.drag" : "hint.tap")), 6f); Sim.MarkReady(); }
        }

        int _hintCap = 3; float _beatTimer; bool _firstRun, _hintPickShown, _hintCooldownShown;
        readonly bool _bonus; public bool Popped { get; private set; }   // ADR-019: the face bonus
        ToolDefinition _dragTool; Vector2 _dragStart; Vec2 _pressAt; bool _dragging;   // ADR-017: drag a scene object to the hand
        void ShowHint(string text, float seconds) { if (_hintsShown >= _hintCap && text != null) return; _hud.ShowHint(text); _hintTimer = seconds; if (text != null) _hintsShown++; }

        public void Frame(float unscaledDelta)
        {
            _arena.SyncViewport();
            var st = Sim.Round.State;
            if (_ended) { _arena.Render(Sim, unscaledDelta); SilenceBuzz();
#if TDF_DEVBUILD
                Platform.DevFly(-1, -1, 3);
#endif
                return; }
            if (_replay != null) { ReplayFrame(unscaledDelta); return; }
            if (_tutorial) { _arena.Render(Sim, 0f); _hud.Render(Sim, _selected); return; }
            if (_resumeLeft > 0f)
            {
                _resumeLeft -= unscaledDelta; int n = Mathf.CeilToInt(Mathf.Max(0f, _resumeLeft));
                if (n != _lastCountdownShown) { _lastCountdownShown = n; _hud.ShowCountdown(n); if (n > 0) _audio.Tick(); }
                if (_resumeLeft <= 0f) { _hud.ShowCountdown(0); Sim.Resume(); _pointerDown = Pointer.current != null && Pointer.current.press.isPressed; }
                _arena.Render(Sim, 0f); _hud.Render(Sim, _selected); return;
            }
            if (st == RoundState.Paused) { _arena.Render(Sim, 0f); _hud.Render(Sim, _selected); SilenceBuzz();
#if TDF_DEVBUILD
                Platform.DevFly(-1, -1, 2);
#endif
                return; }
            if (_hitStop > 0f) { _hitStop -= unscaledDelta; _arena.Render(Sim, unscaledDelta * 0.15f); _hud.Render(Sim, _selected); return; }   // freezes the simulation 80 ms on the catch (impact)
            if (unscaledDelta > 0.5f) { PauseFromSystem(Loc.T("pause.background")); return; }
            HandleInput();
            int pausesBefore = Sim.TechnicalPauses;
            float simDelta = unscaledDelta;
            if (_slowLeft > 0f) { _slowLeft -= unscaledDelta; simDelta *= SlowFactor; }   // ADR-015: "she saw you" - half a second at 0.3×
            if (_slowCooldown > 0f) _slowCooldown -= unscaledDelta;
            _hud.SlowMo(_slowLeft > 0f ? Mathf.Clamp01(_slowLeft / SlowSeconds) : 0f);
            _audio.SetFury(Sim.Round.Fury);   // ADR-016: the percussion rises with fury
            // ADR-016: last 10 s - accelerating heartbeat and pulsing red vignette
            if (Sim.Round.State == RoundState.Playing && Sim.Round.RemainingMs <= 10000)
            {
                float left = Sim.Round.RemainingMs / 10000f; _beatTimer -= unscaledDelta;
                if (_beatTimer <= 0f) { _beatTimer = Mathf.Lerp(0.5f, 1.0f, left); _audio.Heartbeat(Mathf.Lerp(1.25f, 0.95f, left)); _hud.DangerPulse(); }
                _hud.Danger(1f - left);
            }
            else _hud.Danger(0f);
            Sim.Advance(simDelta);
            if (Sim.TechnicalPauses > pausesBefore)
            {
                if (Sim.TechnicalPauses >= 3) Sim.Interrupt("lag_recorrente");
                else { _hud.ShowPause(true, Loc.T("pause.technical")); _aiming = false; _arena.ShowAim(false, default, 0, 0); }
            }
            ObserveEvents();
            if (_hintTimer > 0f) { _hintTimer -= unscaledDelta; if (_hintTimer <= 0f) _hud.ShowHint(null); }
            if (_escapeCooldown > 0f) _escapeCooldown -= unscaledDelta; if (_takeoffCooldown > 0f) _takeoffCooldown -= unscaledDelta;
            // "she saw you": the escape circuit fired (E above threshold) in some active fly
            bool saw = false; foreach (var f in Sim.Flies) if (f.Active && f.Body.EscapeDrive > Sim.EscapeDriveThreshold) { saw = true; break; }
            _hud.Brain(saw);
            if (saw && !_sawShown) { _sawShown = true; _hud.ShowHint(Loc.T("hint.saw")); _hintTimer = 1.6f; }
            _arena.Render(Sim, simDelta); _hud.Render(Sim, _selected);
#if TDF_DEVBUILD
            { var sp = new Vector2(-1, -1); foreach (var f in Sim.Flies) if (f.Active) { var w = _arena.Cam.WorldToScreenPoint(new Vector3((float)f.Body.Position.X, (float)f.Body.Position.Y, 0f)); sp = new Vector2(w.x, w.y); break; } Platform.DevFly(sp.x, sp.y, Sim.Round.IsTerminal ? 3 : 1); }   // headless "hunt" mode
#endif
            for (int i = 0; i < AudioManager.BuzzSlots; i++)
            {
                var ag = i < Sim.Flies.Count ? Sim.Flies[i] : null;
                if (ag == null) _audio.SetBuzz(i, false, 0, 0.5f, 1f);
                else _audio.SetBuzz(i, ag.Active, ag.Body.Landed ? 0f : ag.Body.Speed, ag.Body.Position.X / Arena.W, ag.State.Kind.BuzzPitch);
            }
            if (Sim.Round.IsTerminal && !_ended)
            {
                _ended = true; _arena.ShowAim(false, default, 0, 0); _hud.ShowHint(null); _hud.ShowCancelZone(false); _audio.SetAmbience(false); _audio.SetMusicLevel(1f);
                var o = Sim.Round.Outcome;
                if (_bonus && o.Fury >= 100) { Popped = true; _arena.FacePop(); _audio.Pop(); _hud.Banner(Loc.T("fx.pop"), Palette.Fury, 1.6f, 1.5f); if (!_settings.ReducedEffects) { _arena.Shake(0.02f); _hud.Flash(); } }   // ADR-019: the character burst
                if (o.Terminal == RoundState.Won || (o.Terminal == RoundState.TimedOut && o.FliesCaught > 0)) { _audio.Win(); if (_settings.Vibration) Platform.Vibrate(true); } else if (o.Terminal == RoundState.TimedOut) _audio.Timeout();
                RoundEnded?.Invoke(o);
            }
        }

        /// <summary>Replay: advances the simulation in 10 ms steps injecting the recorded confirmations at the right ticks; skips silently to the window of the best catch.</summary>
        void ReplayFrame(float unscaledDelta)
        {
            var ptr = Pointer.current;
            if (ptr != null && ptr.press.wasPressedThisFrame) { Finish(); return; }
            if (!_replayReady)
            {   // fast forward with a time budget per frame (without freezing the web)
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (Sim.TicksRun < _replay.WindowStartTick && sw.ElapsedMilliseconds < 8 && !Sim.Round.IsTerminal) StepReplayTick();
                if (Sim.TicksRun >= _replay.WindowStartTick || Sim.Round.IsTerminal) { _replayReady = true; _hud.ShowHint(Loc.T("replay.hint")); }
                _arena.Render(Sim, 0f); _hud.Render(Sim, _selected); return;
            }
            if (_hitStop > 0f) { _hitStop -= unscaledDelta; _arena.Render(Sim, unscaledDelta * 0.15f); _hud.Render(Sim, _selected); return; }
            _replayAcc += Mathf.Min(unscaledDelta, 0.1f) * 0.8f;   // a little slower, so it can be seen
            while (_replayAcc >= 0.01f) { _replayAcc -= 0.01f; StepReplayTick(); if (Sim.Round.IsTerminal || Sim.TicksRun >= _replay.WindowEndTick) break; }
            _arena.Render(Sim, unscaledDelta); _hud.Render(Sim, _selected);
            for (int i = 0; i < AudioManager.BuzzSlots; i++) { var ag = i < Sim.Flies.Count ? Sim.Flies[i] : null; if (ag == null) _audio.SetBuzz(i, false, 0, 0.5f, 1f); else _audio.SetBuzz(i, ag.Active, ag.Body.Landed ? 0f : ag.Body.Speed, ag.Body.Position.X / Arena.W, ag.State.Kind.BuzzPitch); }
            if (Sim.Round.IsTerminal || Sim.TicksRun >= _replay.WindowEndTick) Finish();
        }
        void StepReplayTick()
        {
            while (_replayNext < _replay.Inputs.Count && _replay.Inputs[_replayNext].Tick <= Sim.TicksRun)
            {
                var inp = _replay.Inputs[_replayNext++];
                if (inp.ToolId == "bait") { Sim.PlaceBait(inp.Target, force: true); continue; }   // ADR-015
                var tool = _scene.ById(inp.ToolId); if (tool == null) continue;
                _selected = tool; _hud.SetSelected(tool); _arena.SelectHome(tool);
                if (Sim.Confirm(tool, inp.Target, out _) == ConfirmError.None && _replayReady) _audio.Whoosh();
            }
            Sim.Advance(0.01f, false);
        }
        void Finish() { if (_ended) return; _ended = true; SilenceBuzz(); _audio.SetAmbience(false); _audio.SetMusicLevel(1f); _hud.SetLabel(null); _hud.ShowHint(null); ReplayFinished?.Invoke(); }

        void SilenceBuzz() { for (int i = 0; i < AudioManager.BuzzSlots; i++) _audio.SetBuzz(i, false, 0, 0.5f, 1f); }

        void ObserveEvents()
        {
            var r = Sim.Round;
            if (r.AttackCount > _lastAttackCount) { _lastAttackCount = r.AttackCount; if (_hintTimer > 0f && _hud.CurrentHint == Loc.T("hint.tap")) _hud.ShowHint(null); }
            if (r.Fury > _lastFury && !_bonus)
            {
                if (!_hintFuryShown && r.Fury < 25) { _hintFuryShown = true; ShowHint(Loc.T("hint.fury"), 2f); }
                int lvl = r.FuryLevel;
                if (lvl > _lastFuryLevel)
                {
                    var role = lvl == 1 ? ToolRole.Medium : ToolRole.Giant; var unlocked = _scene.ByRole(role); var other = _scene.Other(unlocked);
                    _lastFuryLevel = lvl; _audio.FuryUp(); if (_settings.Vibration) Platform.Vibrate(false);
                    _arena.PulseHome(unlocked); if (other != unlocked) _arena.PulseHome(other);   // ADR-017: both objects of the role wake up in the scene (the lock drops)
                    _hud.Banner(Loc.F("fx.fury", r.Rules.FuryThresholds[lvl]) + " - " + Loc.T("tool." + unlocked.Id).ToUpperInvariant() + (other != unlocked ? " + " + Loc.T("tool." + other.Id).ToUpperInvariant() : "") + "!", Palette.Fury, 1.3f);
                    if (!_hintThresholdShown) { _hintThresholdShown = true; ShowHint(Loc.F("hint.try", Loc.T("tool." + unlocked.Id)), 2.5f); }
                }
                _lastFury = r.Fury;
            }
        }

        static double Hash01(ulong x) { x += 0x9E3779B97F4A7C15UL; x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL; x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL; x ^= x >> 31; return (x >> 11) * (1.0 / 9007199254740992.0); }
        int Pick(int n, ref int last) { int i = _rng.Next(n); if (i == last && n > 1) i = (i + 1) % n; last = i; return i; }

        void OnImpact(Attack a) { _audio.Impact(a.Tool.Id); if (_settings.Vibration && a.Tool.Role != ToolRole.Fast) Platform.Vibrate(false); if (a.Tool.Role != ToolRole.Fast) _arena.Shake(a.Tool.Role == ToolRole.Giant ? 0.012f : 0.006f); }
        void OnFlyCaught(FlyAgent ag, int attackId, int t)
        {
            float pitch = ag.State.Kind.Kind == FlyKind.Bluebottle ? 0.8f : (ag.State.Kind.Kind == FlyKind.FruitFly ? 1.25f : (ag.State.Kind.Kind == FlyKind.Horsefly ? 0.7f : (ag.State.Kind.Kind == FlyKind.Boss ? 0.55f : 1f)));
            _audio.Catch(pitch); if (_settings.Vibration) Platform.Vibrate(true);
            _arena.Splat(ag.Body.Position, Loc.T("fx.splat." + Pick(SplatLines, ref _lastSplat))); _hud.BumpFlies(Sim.Round.FliesCaught);
            if (!_settings.ReducedEffects) { _hitStop = 0.08f; _hud.Flash(); _arena.Punch(); }
            int combo = Sim.Round.Combo;
            if (_replay == null && combo >= Recording.BestCatchCombo) { Recording.BestCatchCombo = combo; Recording.BestCatchTick = Sim.TicksRun; }
            if (combo >= 2) { _hud.Banner(Loc.F("fx.combo", combo), Palette.Action, 1.1f, 1.25f + 0.08f * Mathf.Min(combo, 6)); _audio.Combo(combo); }
            if (_firstRun && Sim.Round.FliesCaught == 1) ShowHint(Loc.T("hint.first.catch"), 3.5f);   // ADR-016: guided first round
            if (ag.State.Kind.Kind == FlyKind.Golden) { _hud.Banner(Loc.T("fx.golden.caught"), Palette.Action, 1.4f, 1.35f); _audio.Star(); }
        }
        void OnTimeExtended(int ms, int combo) { _hud.TimeBonus(ms); _audio.Bonus(); }
        /// <summary>ADR-014: request fulfilled (star) and boss fly caught - the day closes 2 s later (domain).</summary>
        void OnObjectiveDone(int i) { if (_replay != null) return; _hud.Banner(Loc.T("fx.star"), Palette.Action, 1.3f, 1.3f); _hud.PulseObjective(); _audio.Star(); }
        void OnBossCaught() { if (_replay != null) return; _hud.Banner(Loc.T("fx.bosscaught"), Palette.Fury, 1.8f, 1.45f); _hud.ShowHint(null); if (!_settings.ReducedEffects) { _hitStop = 0.18f; _hud.Flash(); } }
        void OnFlySpawned(FlyAgent ag)
        {
            _audio.FlyIn(ag.State.Kind.BuzzPitch);
            bool two = (ag.Slot == 1 && !_twoShown) || (ag.Slot == 2 && !_threeShown); if (ag.Slot == 1) _twoShown = true; if (ag.Slot == 2) _threeShown = true;   // first time the 2nd/3rd slot opens
            if (ag.State.Kind.Kind == FlyKind.Boss)
            {   // boss fly: bigger banner, low buzz, its own hint (outside the hint limit)
                _hud.Banner(Loc.T("fx.boss"), Palette.Fury, 1.5f, 1.4f); _audio.BossIn(); _hud.ShowHint(Loc.T("hint.boss")); _hintTimer = 3.5f; return;
            }
            string text = two ? Loc.T(ag.Slot == 2 ? "fx.three" : "fx.two") : (ag.State.Kind.Kind == FlyKind.House ? Loc.T("fx.newfly") : Loc.T("fx.kind." + ag.State.Kind.Id));
            bool golden = ag.State.Kind.Kind == FlyKind.Golden; if (golden) _audio.Bonus();
            _hud.Banner(text, ag.State.Kind.Kind == FlyKind.House ? Palette.Info : (golden ? Palette.Action : Palette.Fury), golden ? 1.2f : 0.8f, golden ? 1.3f : 1f);
        }
        void OnFlyEscaped(FlyAgent ag, Attack a)
        {
            if (_escapeCooldown > 0f) return; _escapeCooldown = 1.5f;
            _audio.Escape(ag.State.Kind.BuzzPitch);
            // ADR-015: "she saw you" slow motion - at most twice per round, with an interval; never with reduced effects
            if (!_settings.ReducedEffects && _replay == null && _slowUsed < SlowMaxPerRound && _slowCooldown <= 0f)
            {
                _slowLeft = SlowSeconds; _slowUsed++; _slowCooldown = SlowCooldownSeconds; _audio.SlowMo();
                _hud.Banner(Loc.T("fx.saw"), Palette.Info, 1.0f, 1.15f);
            }
        }
        const float SlowSeconds = 0.5f, SlowFactor = 0.3f, SlowCooldownSeconds = 8f; const int SlowMaxPerRound = 2;
        float _slowLeft, _slowCooldown; int _slowUsed; bool _baitArmed;

        // ---- ADR-015: bait ----
        void OnBaitEarned() { if (_replay != null) return; _hud.Banner(Loc.T("fx.bait"), Palette.Action, 1.0f, 1.1f); _hud.PulseBait(); _audio.Bonus(); }
        void ToggleBait()
        {
            if (_ended || _aiming || _replay != null) return; _audio.Tap();
            if (Sim.BaitCharges <= 0 || Sim.BaitActive) { _hud.ShowHint(Loc.T(Sim.BaitActive ? "hint.bait.active" : "hint.bait.none")); _hintTimer = 1.6f; return; }
            _baitArmed = !_baitArmed; _hud.BaitArmed = _baitArmed;
            if (_baitArmed) { _hud.ShowHint(Loc.T("hint.bait.place")); _hintTimer = 4f; } else _hud.ShowHint(null);
        }
        void PlaceBait(Vec2 at)
        {
            _baitArmed = false; _hud.BaitArmed = false; _hud.ShowHint(null);
            if (Sim.PlaceBait(at)) Recording.Inputs.Add(new ReplayInput { Tick = Sim.TicksRun, ToolId = "bait", Target = at });
        }
        void OnFlyTookOff(FlyAgent ag)
        {
            _arena.TakeoffFx(ag);
            if (_takeoffCooldown <= 0f) { _takeoffCooldown = 0.4f; _audio.Takeoff(ag.State.Kind.BuzzPitch); }
        }

        void HandleInput()
        {
            var ptr = Pointer.current; if (ptr == null) return;
            bool pressed = ptr.press.isPressed; Vector2 pos = ptr.position.ReadValue();
            if (pressed && !_pointerDown)
            {
                _pointerDown = true;
                if (_arena.ScreenToArena(pos, out var at))
                {
                    if (_baitArmed) PlaceBait(at);
                    else if (_arena.HitHome(at, out var picked)) { _dragTool = picked; _dragStart = pos; _pressAt = at; _dragging = false; }   // ADR-017: can be a drag (choose) or a simple tap (attack) - decided on move/release
                    else if (_selected.Gesture == ToolGesture.Tap) Confirm(at);
                    else { _aiming = true; _overCancel = false; _arena.ShowAim(true, at, ScaledW(), ScaledH()); _hud.ShowCancelZone(true); }
                }
            }
            else if (pressed && _pointerDown && _dragTool != null)
            {
                if (!_dragging && (pos - _dragStart).magnitude > 14f)
                {
                    if (Sim.Round.Fury < _dragTool.MinFury) { _arena.ShakeHome(_dragTool); _hud.ShowHint(Loc.F("hud.needfury", _dragTool.MinFury)); _hintTimer = 1.5f; _dragTool = null; return; }   // locked: shakes and explains
                    _dragging = true; _arena.BeginDrag(_dragTool); _arena.DragHighlight(true); _audio.Tap();
                }
                if (_dragging) _arena.DragTo(_arena.ScreenToArenaUnclamped(pos));
            }
            else if (!pressed && _pointerDown && _dragTool != null)
            {
                _pointerDown = false; var t = _dragTool; _dragTool = null;
                if (_dragging) { _dragging = false; _arena.DragHighlight(false); bool drop = _hud.IsOverHand(pos) || _arena.IsOverHand(_arena.ScreenToArenaUnclamped(pos)); _arena.EndDrag(drop); if (drop) SelectTool(t); }
                else Confirm(_pressAt);   // simple tap on an object = attack at that point
            }
            else if (pressed && _pointerDown && _aiming)
            {
                _overCancel = _hud.IsOverCancelZone(pos);
                if (_arena.ScreenToArena(pos, out var at)) _arena.ShowAim(!_overCancel, at, ScaledW(), ScaledH()); else _arena.ShowAim(false, default, 0, 0);
                _hud.HighlightCancel(_overCancel);
            }
            else if (!pressed && _pointerDown)
            {
                _pointerDown = false;
                if (_aiming)
                {
                    _aiming = false; _arena.ShowAim(false, default, 0, 0); _hud.ShowCancelZone(false);
                    if (!_overCancel && _arena.ScreenToArena(pos, out var at)) Confirm(at);   // release outside the arena or in the cancel zone → no attack
                }
            }
        }

        float ScaledW() => _selected.RectW * (Sim.Round.Assisted ? Sim.Round.Rules.AssistScale : 1f);
        float ScaledH() => _selected.RectH * (Sim.Round.Assisted ? Sim.Round.Rules.AssistScale : 1f);

        void Confirm(Vec2 at)
        {
            int tick = Sim.TicksRun;
            var err = Sim.Confirm(_selected, at, out var attack);
            if (err == ConfirmError.FuryTooLow) _hud.ShowHint(Loc.F("hud.needfury", _selected.MinFury));
            else if (err == ConfirmError.OnCooldown && !_hintCooldownShown) { _hintCooldownShown = true; _hud.ShowHint(Loc.T("hint.cooldown")); _hintTimer = 2f; }
            else if (err == ConfirmError.None) { _audio.Whoosh(); Recording.Inputs.Add(new ReplayInput { Tick = tick, ToolId = _selected.Id, Target = at }); }
        }

        bool FlyNear(Vec2 at, float d) { foreach (var f in Sim.Flies) if (f.Active && (f.Body.Position - at).Length <= d) return true; return false; }
        public void SelectTool(ToolDefinition t)
        {
            if (_aiming || _ended) return;
            if (Sim.Round.Fury < t.MinFury) { _arena.ShakeHome(t); _hud.ShowHint(Loc.F("hud.needfury", t.MinFury)); _hintTimer = 1.5f; return; }   // ADR-017: locked - shakes and explains
            if (t == _selected) return;
            _selected = t; _hud.SetSelected(t); _arena.SelectHome(t); _audio.Whoosh(); ToolSwitched?.Invoke();
            _arena.ShowHandLabel(Loc.T("tool." + t.Id)); _hud.ShowHint(Loc.T(t.Gesture == ToolGesture.Tap ? "hud.gesture.tap" : "hud.gesture.aim")); _hintTimer = 2.5f;
            if (_firstRun && !_hintPickShown) { _hintPickShown = true; ShowHint(Loc.T("hint.picked"), 2.5f); }
            else if (t.Gesture == ToolGesture.PressAimRelease && !_hintFridgeShown) { _hintFridgeShown = true; ShowHint(Loc.T("hint.fridge"), 3f); }
        }

        public void PauseFromUser() { if (_replay != null) { Finish(); return; } if (_ended || _tutorial || Sim.Round.State == RoundState.Paused) return; CancelGesture(); Sim.Pause(); _hud.ShowPause(true, null); _audio.Tap(); }
        public void PauseFromSystem(string why) { if (_replay != null) { Finish(); return; } if (_ended || _tutorial || Sim.Round.State == RoundState.Paused) return; CancelGesture(); Sim.Pause(); _hud.ShowPause(true, why); }
        void CancelGesture() { _aiming = false; _pointerDown = false; _arena.ShowAim(false, default, 0, 0); _hud.ShowCancelZone(false); }
        public void ResumeRequested() { if (Sim.Round.State != RoundState.Paused) return; _hud.ShowPause(false, null); _resumeLeft = 3f; _lastCountdownShown = -1; _audio.Tap(); }
        public void Quit() { _hud.ShowPause(false, null); _hud.ShowTutorial(null, null, null); _tutorial = false; Sim.Interrupt("saida_voluntaria"); }
    }

    /// <summary>HUD: time (left), flies (center), pause (right); fury with 25/60 marks; three tools with states; hint; cancel zone; banner; time bonus; tutorial; optional diagnostics.</summary>
    public sealed class HudView
    {
        /// <summary>ADR-017: compact bottom block - fury bar, pill of the chosen object, hint.</summary>
        public const float BottomHeight = 104f;   // ADR-017 (5th round): Doom-style status bar (minimum height; on tall screens it grows - see SetBarHeight)
        float _barH = BottomHeight;
        /// <summary>On tall screens the bar grows to absorb the slack (the arena is 3:4); the cancel zone and the bonus follow.</summary>
        public void SetBarHeight(float h) { _barH = h; _bottom.sizeDelta = new Vector2(0, h); _cancelZone.anchoredPosition = new Vector2(0, h + 4); }
        readonly TextMeshProUGUI _time, _hint, _countdown, _pauseLabel, _cancelLabel, _flies, _banner, _bonus, _tutorialText, _tutorialBtnLabel;
        readonly RectTransform _fliesRt, _bannerRt, _bonusRt, _tutorial; float _fliesPulse, _bannerLeft, _bannerLife, _bannerScale = 1f, _bonusLeft;
        readonly RectTransform _pauseOverlay, _cancelZone, _quitConfirm; RectTransform _hintChip;
        readonly RectTransform _bottom;   // ADR-017: the hand is drawn in the arena (ArenaView.RenderHand); below, the status bar
        readonly TextMeshProUGUI _fury; readonly Image[] _gridBg = new Image[6], _gridIcon = new Image[6], _gridCool = new Image[6], _gridLock = new Image[6]; readonly ToolDefinition[] _gridTools = new ToolDefinition[6];   // Doom bar
        readonly Button _tutorialBtn; Action _tutorialDismiss;
        readonly TextMeshProUGUI _sceneLabel; SceneDef _scene = Scenes.Kitchen; string _label;
        readonly Image _brain, _brainOn, _flash; readonly RectTransform _brainRt; float _brainGlow, _flashLeft;
        readonly RectTransform _objChip; readonly TextMeshProUGUI _objText; readonly Image _objStar; int _objIdx = -2, _objProg = -1; float _objPulse;   // ADR-014
        readonly RectTransform _baitHost; readonly Image _baitFill, _baitIcon, _baitCool, _baitBadge; readonly TextMeshProUGUI _baitCount; readonly Image _slowVeil; float _baitPulse;   // ADR-015
        public Action BaitTap; public bool BaitArmed;
        readonly Image _danger; float _dangerPulse;   // ADR-016
        public readonly bool DevOverlay;
        public string CurrentHint => _hint.text;

        public HudView(RectTransform root, Action pause, Action resume, Action quit, Action<ToolDefinition> select, bool devOverlay)
        {
            DevOverlay = devOverlay;
            // ---------- top: pause, current request, label (fly of the day/replay), time bonus ----------
            var top = Ui.Empty(root, "Top"); top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1); top.pivot = new Vector2(0.5f, 1); top.anchoredPosition = new Vector2(0, -8); top.sizeDelta = new Vector2(0, 40);
            Ui.RoundButton(top, "Pause", "II", Palette.Paper, Palette.Ink, 18, pause, out var pauseRt); Ui.Place(pauseRt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(46, 40));
            _objChip = Ui.Card(top, "Objective", Palette.Paper, 2f, new Vector2(2, -2), false); Ui.Place(_objChip, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(262, 30));
            var sgo = new GameObject("Star", typeof(RectTransform), typeof(Image)); sgo.transform.SetParent(_objChip, false); _objStar = sgo.GetComponent<Image>(); _objStar.sprite = Resources.Load<Sprite>("art/icon_star"); _objStar.preserveAspect = true; _objStar.raycastTarget = false; Ui.Place(sgo.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(7, 0), new Vector2(22, 22));
            _objText = Ui.Text(_objChip, "Text", "", 12, Palette.Ink, TextAlignmentOptions.Left, FontStyles.Bold); Ui.Stretch(_objText.rectTransform, 32, 0, 8, 0);
            _sceneLabel = Ui.Text(top, "Scene", "", 11, Palette.Fury, TextAlignmentOptions.Left, FontStyles.Bold); Ui.Place(_sceneLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, -26), new Vector2(260, 16));
            _bonus = Ui.Text(root, "Bonus", "", 20, Palette.Info, TextAlignmentOptions.Center, FontStyles.Bold); _bonusRt = _bonus.rectTransform; Ui.Place(_bonusRt, new Vector2(0, 0), new Vector2(0, 0), new Vector2(60, BottomHeight + 26), new Vector2(120, 30)); _bonus.gameObject.SetActive(false);
            _banner = Ui.Text(root, "Banner", "", 26, Palette.Fury, TextAlignmentOptions.Center, FontStyles.Bold); _bannerRt = _banner.rectTransform; Ui.Place(_bannerRt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -124), new Vector2(340, 66)); _banner.enableAutoSizing = true; _banner.fontSizeMin = 16; _banner.fontSizeMax = 26; _banner.gameObject.SetActive(false);   // long texts ("FURY 60 - HAT + CHAIR!") wrap to two lines or shrink, never overflowing the box
            // hint: floating dark pill just below the top (the hand takes the bottom)
            _hintChip = Ui.Card(root, "HintChip", Palette.Ink, 0f, null, false); Ui.Place(_hintChip, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(280, 26));
            // ---------- Doom-style status bar (ADR-017, 5th round): TIME · FLIES · OBJECTS · brain · FURY · BAIT ----------
            var bottom = Ui.Empty(root, "Bottom"); _bottom = bottom; bottom.anchorMin = new Vector2(0, 0); bottom.anchorMax = new Vector2(1, 0); bottom.pivot = new Vector2(0.5f, 0); bottom.anchoredPosition = new Vector2(0, 0); bottom.sizeDelta = new Vector2(0, BottomHeight);
            var bar = Ui.Card(bottom, "Bar", Palette.Paper, 3f, null, false); Ui.Stretch(bar, 4, 8, 4, 4); var barFill = bar.Find("Fill").GetComponent<RectTransform>();
            RectTransform Cell(string name, float x0, float x1, string label)
            {
                var c = Ui.Empty(barFill, name); c.anchorMin = new Vector2(x0, 0); c.anchorMax = new Vector2(x1, 1); c.offsetMin = Vector2.zero; c.offsetMax = Vector2.zero;
                if (x0 > 0f) { var sep = Ui.Panel(barFill, "Sep" + name, new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.35f), false); sep.anchorMin = new Vector2(x0, 0.12f); sep.anchorMax = new Vector2(x0, 0.88f); sep.pivot = new Vector2(0.5f, 0.5f); sep.anchoredPosition = Vector2.zero; sep.sizeDelta = new Vector2(2, 0); }
                if (label != null) { var l = Ui.Text(c, "Label", label, 9, Palette.Muted, TextAlignmentOptions.Center, FontStyles.Bold); l.characterSpacing = 3; l.enableAutoSizing = true; l.fontSizeMin = 6; l.fontSizeMax = 9; Ui.Place(l.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 5), new Vector2(0, 12)); l.rectTransform.anchorMin = new Vector2(0, 0); l.rectTransform.anchorMax = new Vector2(1, 0); }
                return c;
            }
            var cTime = Cell("Time", 0f, 0.15f, Loc.T("bar.time"));
            _time = Ui.Text(cTime, "Time", "60", 30, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Stretch(_time.rectTransform, 2, 10, 2, 22); _time.enableAutoSizing = true; _time.fontSizeMin = 14; _time.fontSizeMax = 30;   // fills the cell, shrinks if needed (never overflows the box)
            var cFlies = Cell("Flies", 0.15f, 0.32f, Loc.T("bar.flies"));
            _fliesRt = Ui.Empty(cFlies, "Flies"); Ui.Stretch(_fliesRt, 4, 10, 4, 22);
            var fi = new GameObject("FlyIcon", typeof(RectTransform), typeof(Image)); fi.transform.SetParent(_fliesRt, false); var fim = fi.GetComponent<Image>(); fim.sprite = Resources.Load<Sprite>("art/fly_top_flat"); fim.preserveAspect = true; fim.raycastTarget = false; Ui.Place(fi.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(24, 24));
            _flies = Ui.Text(_fliesRt, "Count", "0", 28, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Stretch(_flies.rectTransform, 26, 0, 0, 0); _flies.enableAutoSizing = true; _flies.fontSizeMin = 14; _flies.fontSizeMax = 28;
            var cArms = Cell("Arms", 0.32f, 0.60f, Loc.T("bar.arms"));
            for (int i = 0; i < 6; i++)
            {   // 3 columns × 2 rows: primaries on top, alternates below; tap chooses (dragging to the hand still works)
                int idx = i; int col = i % 3, row = i / 3;
                var cellRt = Ui.Panel(cArms, "Slot" + i, Palette.Surface); var ci = cellRt.GetComponent<Image>(); ci.sprite = Ui.RoundedSprite(); ci.type = Image.Type.Sliced; Ui.Place(cellRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((col - 1) * 30f, 21f - row * 30f), new Vector2(27, 27));
                var b = cellRt.gameObject.AddComponent<Button>(); b.targetGraphic = ci; b.onClick.AddListener(() => { if (_gridTools[idx] != null) select(_gridTools[idx]); });
                var bc = b.colors; bc.highlightedColor = Color.white; bc.pressedColor = new Color(0.9f, 0.9f, 0.9f); b.colors = bc;
                var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image)); ig.transform.SetParent(cellRt, false); var img = ig.GetComponent<Image>(); img.preserveAspect = true; img.raycastTarget = false; Ui.Stretch(ig.GetComponent<RectTransform>(), 3, 3, 3, 3);
                var cg = new GameObject("Cool", typeof(RectTransform), typeof(Image)); cg.transform.SetParent(cellRt, false); var cool = cg.GetComponent<Image>(); cool.sprite = Ui.RoundedSprite(); cool.type = Image.Type.Filled; cool.fillMethod = Image.FillMethod.Vertical; cool.fillOrigin = (int)Image.OriginVertical.Top; cool.color = new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.3f); cool.raycastTarget = false; cool.fillAmount = 0f; Ui.Stretch(cg.GetComponent<RectTransform>());
                var lg = new GameObject("Lock", typeof(RectTransform), typeof(Image)); lg.transform.SetParent(cellRt, false); var lk = lg.GetComponent<Image>(); lk.sprite = Resources.Load<Sprite>("art/icon_lock"); lk.preserveAspect = true; lk.raycastTarget = false; Ui.Place(lg.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(2, -2), new Vector2(12, 12));
                _gridBg[i] = ci; _gridIcon[i] = img; _gridCool[i] = cool; _gridLock[i] = lk;
            }
            var cBrain = Cell("Brain", 0.60f, 0.72f, null);
            var bgo = new GameObject("Brain", typeof(RectTransform), typeof(Image)); bgo.transform.SetParent(cBrain, false); _brain = bgo.GetComponent<Image>(); _brain.sprite = Resources.Load<Sprite>("art/icon_brain_eyes"); _brain.preserveAspect = true; _brain.raycastTarget = false; _brain.color = new Color(1, 1, 1, 0.85f);
            _brainRt = bgo.GetComponent<RectTransform>(); Ui.Place(_brainRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(46, 46));
            var ogo = new GameObject("BrainOn", typeof(RectTransform), typeof(Image)); ogo.transform.SetParent(bgo.transform, false); _brainOn = ogo.GetComponent<Image>(); _brainOn.sprite = Resources.Load<Sprite>("art/icon_brain_eyes_on"); _brainOn.preserveAspect = true; _brainOn.raycastTarget = false; _brainOn.color = new Color(1, 1, 1, 0f); Ui.Stretch(ogo.GetComponent<RectTransform>());   // the eyes light up: the "on" version appears on top
            var cFury = Cell("Fury", 0.72f, 0.87f, Loc.T("bar.fury"));
            _fury = Ui.Text(cFury, "Fury", "0%", 26, Palette.Fury, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Stretch(_fury.rectTransform, 2, 10, 2, 22); _fury.enableAutoSizing = true; _fury.fontSizeMin = 12; _fury.fontSizeMax = 26;
            var cBait = Cell("BaitCell", 0.87f, 1f, Loc.T("bar.bait"));
            // ADR-015: bait button (outside the arena: taps do not become attacks)
            _baitHost = Ui.Card(cBait, "Bait", Palette.Paper, 2f, new Vector2(2, -2)); Ui.Place(_baitHost, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 7), new Vector2(38, 38));
            _baitFill = _baitHost.Find("Fill").GetComponent<Image>(); var bb = _baitFill.gameObject.AddComponent<Button>(); bb.targetGraphic = _baitFill; bb.onClick.AddListener(() => BaitTap?.Invoke());
            var bcol = bb.colors; bcol.highlightedColor = Color.white; bcol.pressedColor = new Color(0.92f, 0.92f, 0.92f); bb.colors = bcol;
            var bigo = new GameObject("Icon", typeof(RectTransform), typeof(Image)); bigo.transform.SetParent(_baitFill.transform, false); _baitIcon = bigo.GetComponent<Image>(); _baitIcon.sprite = Resources.Load<Sprite>("art/icon_bait"); _baitIcon.preserveAspect = true; _baitIcon.raycastTarget = false; Ui.Place(bigo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 26));
            var bcgo = new GameObject("Cool", typeof(RectTransform), typeof(Image)); bcgo.transform.SetParent(_baitFill.transform, false); _baitCool = bcgo.GetComponent<Image>(); _baitCool.sprite = Ui.CircleSprite(64); _baitCool.type = Image.Type.Filled; _baitCool.fillMethod = Image.FillMethod.Radial360; _baitCool.fillClockwise = false; _baitCool.color = new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.3f); _baitCool.raycastTarget = false; _baitCool.fillAmount = 0f; Ui.Stretch(bcgo.GetComponent<RectTransform>(), 2, 2, 2, 2);
            var bbadge = Ui.Card(_baitHost, "Badge", Palette.Fury, 2f, null, false); Ui.Place(bbadge, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-2, -2), new Vector2(18, 18)); _baitBadge = bbadge.Find("Fill").GetComponent<Image>();
            _baitCount = Ui.Text(bbadge, "N", "", 10, Palette.Paper, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Stretch(_baitCount.rectTransform);
            _hint = Ui.Text(_hintChip, "Hint", "", 14, Palette.Paper, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Stretch(_hint.rectTransform); _hintChip.gameObject.SetActive(false);
            // cancel zone of the giant (48 u, outside the arena, above the bar)
            _cancelZone = Ui.Panel(root, "CancelZone", new Color(Palette.Fury.r, Palette.Fury.g, Palette.Fury.b, 0.15f), false); _cancelZone.anchorMin = new Vector2(0, 0); _cancelZone.anchorMax = new Vector2(1, 0); _cancelZone.pivot = new Vector2(0.5f, 0); _cancelZone.anchoredPosition = new Vector2(0, BottomHeight + 4); _cancelZone.sizeDelta = new Vector2(-32, 48);
            _cancelZone.gameObject.AddComponent<Outline>().effectColor = Palette.Fury;
            _cancelLabel = Ui.Text(_cancelZone, "Label", Loc.T("hint.cancel"), 14, Palette.Fury, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Stretch(_cancelLabel.rectTransform); _cancelZone.gameObject.SetActive(false);
            _countdown = Ui.Text(root, "Countdown", "", 96, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Stretch(_countdown.rectTransform); _countdown.gameObject.SetActive(false);
            var flashRt = Ui.Panel(root, "Flash", new Color(1, 1, 1, 0f), false); Ui.Stretch(flashRt); _flash = flashRt.GetComponent<Image>(); _flash.enabled = false;
            var dgRt = Ui.Panel(root, "Danger", new Color(Palette.Fury.r, Palette.Fury.g, Palette.Fury.b, 0f), false); Ui.Stretch(dgRt); _danger = dgRt.GetComponent<Image>(); _danger.sprite = Ui.VignetteSprite(); _danger.enabled = false;   // ADR-016: vignette of the last 10 s
            var slowRt = Ui.Panel(root, "SlowVeil", new Color(Palette.Info.r, Palette.Info.g, Palette.Info.b, 0f), false); Ui.Stretch(slowRt); _slowVeil = slowRt.GetComponent<Image>(); _slowVeil.enabled = false;   // ADR-015: bluish vignette in slow motion
            // guided first round: card in a speech balloon over the arena; the clock only starts afterwards
            _tutorial = Ui.Panel(root, "Tutorial", new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.35f)); Ui.Stretch(_tutorial);
            var tcard = Ui.Panel(_tutorial, "Card", Palette.Paper); Ui.Place(tcard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(320, 230)); tcard.gameObject.AddComponent<Outline>().effectColor = Palette.Ink;
            var tail = Ui.Panel(tcard, "Tail", Palette.Paper, false); Ui.Place(tail, new Vector2(0.3f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(26, 26)); tail.localRotation = Quaternion.Euler(0, 0, 45f); tail.gameObject.AddComponent<Outline>().effectColor = Palette.Ink;
            var tcover = Ui.Panel(tcard, "TailCover", Palette.Paper, false); Ui.Place(tcover, new Vector2(0.3f, 0), new Vector2(0.5f, 0), new Vector2(0, 2), new Vector2(40, 22));
            _tutorialText = Ui.Text(tcard, "Text", "", 17, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Place(_tutorialText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(288, 130));
            _tutorialBtn = Ui.Button(tcard, "Go", "", Palette.Action, Palette.Ink, 18, () => { var d = _tutorialDismiss; ShowTutorial(null, null, null); d?.Invoke(); }); Ui.Place(_tutorialBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(240, 52));
            _tutorialBtnLabel = _tutorialBtn.GetComponentInChildren<TextMeshProUGUI>(); _tutorial.gameObject.SetActive(false);
            // O01 pause + O02 confirm quit
            _pauseOverlay = Ui.Panel(root, "PauseOverlay", new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.6f)); Ui.Stretch(_pauseOverlay);
            var card = Ui.Panel(_pauseOverlay, "Card", Palette.Paper); Ui.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 230)); card.gameObject.AddComponent<Outline>().effectColor = Palette.Ink;
            var title = Ui.Text(card, "Title", Loc.T("pause.title"), 28, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(280, 40));
            _pauseLabel = Ui.Text(card, "Label", "", 14, Palette.Muted); Ui.Place(_pauseLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(280, 40));
            var cont = Ui.Button(card, "Continue", Loc.T("pause.continue"), Palette.Action, Palette.Ink, 18, resume); Ui.Place(cont.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(260, 56));
            var q = Ui.Button(card, "Quit", Loc.T("pause.quit"), Palette.Paper, Palette.Ink, 16, () => _quitConfirm.gameObject.SetActive(true)); Ui.Place(q.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(260, 52));
            _quitConfirm = Ui.Panel(_pauseOverlay, "QuitConfirm", Palette.Paper); Ui.Place(_quitConfirm, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 200)); _quitConfirm.gameObject.AddComponent<Outline>().effectColor = Palette.Ink;
            var qt = Ui.Text(_quitConfirm, "T", Loc.T("pause.quit.confirm"), 20, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold); Ui.Place(qt.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(280, 60));
            var keep = Ui.Button(_quitConfirm, "Keep", Loc.T("pause.continue"), Palette.Action, Palette.Ink, 18, () => _quitConfirm.gameObject.SetActive(false)); Ui.Place(keep.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(260, 52));
            var yes = Ui.Button(_quitConfirm, "Yes", Loc.T("pause.quit"), Palette.Paper, Palette.Ink, 16, () => { _quitConfirm.gameObject.SetActive(false); quit(); }); Ui.Place(yes.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(260, 48));
            _quitConfirm.gameObject.SetActive(false); _pauseOverlay.gameObject.SetActive(false);
        }

        ToolDefinition _selected;
        public void SetSelected(ToolDefinition t) { _selected = t; }
        /// <summary>Releasing on the bottom block also chooses (besides the hand in the arena).</summary>
        public bool IsOverHand(Vector2 screen) => RectTransformUtility.RectangleContainsScreenPoint(_bottom, screen, null) || screen.y < RectTransformUtility.WorldToScreenPoint(null, _bottom.position).y + _bottom.rect.height;
        /// <summary>Tools, icons and scene name of this round.</summary>
        public void SetAccessibility(bool toolsRight, bool highContrast) { }   // (no effect since ADR-017: there are no cards nor bar)
        public void SetScene(SceneDef scene)
        {
            _scene = scene; _sceneLabel.text = _label ?? "";
            for (int i = 0; i < 6; i++)
            {
                var t = i < 3 ? (scene.Tools.Length > i ? scene.Tools[i] : null) : (scene.Alternates.Length > i - 3 ? scene.Alternates[i - 3] : null); _gridTools[i] = t;
                _gridIcon[i].sprite = t != null ? Resources.Load<Sprite>("art/icon_" + t.Id.Substring(t.Id.IndexOf('_') + 1)) : null; _gridIcon[i].enabled = t != null; _gridBg[i].gameObject.SetActive(t != null);
            }
        }
        public void ShowHint(string text) { _hint.text = text ?? ""; _hintChip.gameObject.SetActive(!string.IsNullOrEmpty(text)); }
        public void ShowCountdown(int n) { _countdown.gameObject.SetActive(n > 0); _countdown.text = n > 0 ? n.ToString() : ""; }
        public void ShowPause(bool on, string label) { _pauseOverlay.gameObject.SetActive(on); _quitConfirm.gameObject.SetActive(false); _pauseLabel.text = label ?? ""; }
        public void ShowCancelZone(bool on) { _cancelZone.gameObject.SetActive(on); HighlightCancel(false); }
        public void HighlightCancel(bool on) { _cancelZone.GetComponent<Image>().color = new Color(Palette.Fury.r, Palette.Fury.g, Palette.Fury.b, on ? 0.45f : 0.15f); }
        public bool IsOverCancelZone(Vector2 screen) => _cancelZone.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(_cancelZone, screen, null);
        public void BumpFlies(int n) { _flies.text = n.ToString(); _fliesPulse = 0.5f; }
        public void Banner(string text, Color color, float seconds, float scale = 1f) { _banner.text = text; _banner.color = color; _banner.gameObject.SetActive(true); _bannerLeft = seconds; _bannerLife = seconds; _bannerScale = scale; }
        public void TimeBonus(int ms) { _bonus.text = "+" + (ms / 1000f).ToString("0.#") + " s"; _bonus.gameObject.SetActive(true); _bonusLeft = 1.1f; }
        public void SetLabel(string text) { _label = text; _sceneLabel.text = text ?? ""; _sceneLabel.color = Palette.Fury; }
        public void PulseObjective() { _objPulse = 0.6f; }
        public void PulseBait() { _baitPulse = 0.8f; }
        public void DangerPulse() { _dangerPulse = 1f; }
        public void Danger(float u)
        {
            if (u <= 0f) { if (_danger.enabled) _danger.enabled = false; return; }
            _danger.enabled = true; if (_dangerPulse > 0f) _dangerPulse -= Time.unscaledDeltaTime * 2.5f;
            _danger.color = new Color(Palette.Fury.r, Palette.Fury.g, Palette.Fury.b, 0.18f * u + 0.3f * Mathf.Clamp01(_dangerPulse) * (0.4f + 0.6f * u));
        }
        public void SlowMo(float u) { _slowVeil.enabled = u > 0f; if (u > 0f) _slowVeil.color = new Color(Palette.Info.r, Palette.Info.g, Palette.Info.b, 0.16f * Mathf.Sin(u * Mathf.PI)); }
        void RenderBait(WorldSimulation sim)
        {
            int n = sim.BaitCharges; bool active = sim.BaitActive; bool has = n > 0 && !active;
            _baitFill.color = BaitArmed ? Palette.Action : (has ? Palette.Paper : Palette.Surface);
            _baitIcon.color = has || active ? Color.white : new Color(1, 1, 1, 0.4f);
            _baitCool.fillAmount = active ? sim.BaitFraction : 0f;
            _baitBadge.transform.parent.gameObject.SetActive(n > 0); _baitCount.text = n.ToString();
            if (_baitPulse > 0f) { _baitPulse -= Time.unscaledDeltaTime; _baitHost.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(Mathf.Clamp01(_baitPulse / 0.8f) * Mathf.PI)); } else if (_baitHost.localScale.x != 1f) _baitHost.localScale = Vector3.one;
        }
        /// <summary>Text of a request, with progress when it counts more than one (e.g. "Catch 4 flies  2/4").</summary>
        public static string ObjectiveText(Objective o, int prog)
        {
            string s;
            switch (o.Kind)
            {
                case ObjectiveKind.CatchN: s = Loc.F("obj.catch", o.N); break;
                case ObjectiveKind.CatchKind: s = o.N > 1 ? Loc.F("obj.kindN." + o.FlyKind.ToString().ToLowerInvariant(), o.N) : Loc.T("obj.kind." + o.FlyKind.ToString().ToLowerInvariant()); break;
                case ObjectiveKind.ComboN: s = Loc.F("obj.combo", o.N); break;
                case ObjectiveKind.CatchWithRole: s = Loc.F((o.N > 1 ? "obj.roleN." : "obj.role.") + o.Role.ToString().ToLowerInvariant(), o.N); break;
                case ObjectiveKind.CatchOnFood: s = o.N > 1 ? Loc.F("obj.foodN", o.N) : Loc.T("obj.food"); break;
                default: s = Loc.F("obj.fast", (o.Ms / 1000f).ToString("0.#")); break;
            }
            return o.N > 1 ? s + "  " + Mathf.Min(prog, o.N) + "/" + o.N : s;
        }
        void RenderObjective(RoundMachine r)
        {
            var sc = r.Scene; if (sc == null || sc.Objectives.Length == 0 || r.ObjectiveDone == null) { if (_objChip.gameObject.activeSelf) _objChip.gameObject.SetActive(false); return; }
            int idx = -1; for (int i = 0; i < sc.Objectives.Length; i++) if (!r.ObjectiveDone[i]) { idx = i; break; }
            int prog = idx >= 0 ? r.ObjectiveProgress[idx] : 0;
            if (idx != _objIdx || prog != _objProg)
            {
                _objIdx = idx; _objProg = prog; _objChip.gameObject.SetActive(true);
                _objText.text = idx < 0 ? Loc.T("obj.all") : ObjectiveText(sc.Objectives[idx], prog);
                _objStar.color = idx < 0 ? Color.white : new Color(1, 1, 1, 0.6f);
            }
            if (_objPulse > 0f) { _objPulse -= Time.unscaledDeltaTime; _objChip.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(Mathf.Clamp01(_objPulse / 0.6f) * Mathf.PI)); } else if (_objChip.localScale.x != 1f) _objChip.localScale = Vector3.one;
        }
        public void Brain(bool active) { if (active) _brainGlow = 1f; }
        public void Flash() { _flashLeft = 0.15f; _flash.enabled = true; }
        public void ShowTutorial(string body, string button, Action onDismiss)
        {
            _tutorialDismiss = onDismiss; bool on = body != null; _tutorial.gameObject.SetActive(on);
            if (on) { _tutorialText.text = body; _tutorialBtnLabel.text = button ?? "OK"; }
        }

        public void Render(WorldSimulation sim, ToolDefinition selected)
        {
            var r = sim.Round;
            int remaining = Mathf.CeilToInt(r.RemainingMs / 1000f);
            _time.text = remaining.ToString(); _time.color = (r.State == RoundState.Playing && remaining <= 10) ? Palette.Fury : Palette.Ink;
            if (_fliesPulse <= 0f && _flies.text != r.FliesCaught.ToString()) _flies.text = r.FliesCaught.ToString();   // new round: counter at zero (the catch pulse sets it first)
            _fury.text = r.Fury + "%";
            for (int i = 0; i < 6; i++)
            {   // object grid: yellow = in the hand; paper = available; grey + lock = not enough fury; veil = cooling down
                var t = _gridTools[i]; if (t == null) continue; bool eligible = r.IsEligible(t); int cd = r.CooldownRemainingMs(t.Id); bool inHand = _selected == t;
                _gridBg[i].color = inHand ? Palette.Action : (eligible ? Palette.Paper : Palette.Surface); _gridIcon[i].color = eligible ? Color.white : new Color(1, 1, 1, 0.35f);
                _gridLock[i].enabled = !eligible; _gridCool[i].fillAmount = cd > 0 ? Mathf.Clamp01(cd / (float)Mathf.Max(1, t.CooldownMs)) : 0f;
            }
            RenderObjective(r); RenderBait(sim);
            if (_fliesPulse > 0f) { _fliesPulse -= Time.unscaledDeltaTime; _fliesRt.localScale = Vector3.one * (1f + 0.35f * Mathf.Clamp01(_fliesPulse / 0.5f)); } else _fliesRt.localScale = Vector3.one;
            if (_bannerLeft > 0f)
            {
                _bannerLeft -= Time.unscaledDeltaTime; float a = Mathf.Clamp01(_bannerLeft / 0.3f); var c = _banner.color; c.a = a; _banner.color = c;
                float u = Mathf.Clamp01((_bannerLife - _bannerLeft) / 0.15f); _bannerRt.localScale = Vector3.one * Mathf.Lerp(0.6f, _bannerScale, u);
                if (_bannerLeft <= 0f) _banner.gameObject.SetActive(false);
            }
            if (_brainGlow > 0f) { _brainGlow -= Time.unscaledDeltaTime * 2.5f; float g = Mathf.Clamp01(_brainGlow); _brainOn.color = new Color(1, 1, 1, g); _brainRt.localScale = Vector3.one * (1f + 0.22f * g); } else if (_brainOn.color.a > 0f) _brainOn.color = new Color(1, 1, 1, 0f);
            if (_flashLeft > 0f) { _flashLeft -= Time.unscaledDeltaTime; _flash.color = new Color(1, 1, 1, 0.45f * Mathf.Clamp01(_flashLeft / 0.15f)); if (_flashLeft <= 0f) _flash.enabled = false; }
            if (_bonusLeft > 0f)
            {
                _bonusLeft -= Time.unscaledDeltaTime; float u = 1f - Mathf.Clamp01(_bonusLeft / 1.1f);
                _bonusRt.anchoredPosition = new Vector2(60, _barH + 26 + 22f * u); var c = _bonus.color; c.a = 1f - Mathf.Clamp01((u - 0.6f) / 0.4f); _bonus.color = c;
                if (_bonusLeft <= 0f) _bonus.gameObject.SetActive(false);
            }
        }
    }
}
