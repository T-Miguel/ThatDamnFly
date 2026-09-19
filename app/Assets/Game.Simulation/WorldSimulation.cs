// World at 100 Hz: per tick it applies commands, advances tools, encodes stimuli, 10 neural sub-steps per fly, integrates bodies,
// resolves swept contacts, fury and time. Clocks only advance when the presentation calls Advance (tab visible/focused).
// rulesVersion 3: successive flies with up to two simultaneous slots (ADR-003); each fly has its own circuit (own instance) and its own body.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ThatDamnFly.Domain;

namespace ThatDamnFly.Simulation
{
    /// <summary>A live fly: rules state + circuit + body + sensory channels.</summary>
    public sealed class FlyAgent
    {
        public FlyState State; public FlyCoreInstance Core; public BodyController Body;
        public readonly float[] Channels = new float[SensoryEncoder.Channels];
        public readonly List<ThreatSample> Threats = new List<ThreatSample>();
        public readonly Dictionary<int, ThreatSample> ThreatByAttack = new Dictionary<int, ThreatSample>();
        /// <summary>Largest escape drive observed while each attack was visible (for "escaped").</summary>
        public readonly Dictionary<int, float> EscapePeakByAttack = new Dictionary<int, float>();
        public Vec2 Prev;
        public int Slot => State.Slot;
        public bool Active => State.Active;
    }

    public sealed class WorldSimulation : IDisposable
    {
        public const int TickMs = 10;
        public const float MaxLagSeconds = 0.100f;
        public readonly RoundMachine Round;
        public readonly List<FlyAgent> Flies = new List<FlyAgent>();
        public readonly ulong Seed;
        readonly byte[] _model;
        readonly Stopwatch _sw = new Stopwatch();
        float _accum;
        public int TicksRun { get; private set; }
        public double NeuralMsLastTick { get; private set; }
        public double NeuralMsMax { get; private set; }
        public double NeuralMsTotal { get; private set; }
        public int TechnicalPauses { get; private set; }
        public bool NumericFailure { get; private set; }
        /// <summary>Fly caught.</summary>
        public event Action<FlyAgent, int, int> FlyCaught;
        public event Action<FlyAgent> FlySpawned;
        /// <summary>The fly escaped an attack: it fled (neural drive above threshold) and the attack narrowly missed.</summary>
        public event Action<FlyAgent, Attack> FlyEscaped;
        public event Action<FlyAgent> FlyTookOff;
        public event Action<int, int> TimeExtended;
        public Vec2 LastCatchPosition { get; private set; }
        public FlyAgent LastCaught { get; private set; }
        /// <summary>Distance (edge to edge) below which a miss counts as "escaped" if the fly fled.</summary>
        public float EscapeMissDistance = 0.16f;
        public float EscapeDriveThreshold = 0.25f;

        // ---- ADR-015: bait (temporary food spot; a charge is earned on reaching combo 3) ----
        public const int BaitAtCombo = 3, BaitMaxCharges = 2, BaitTicks = 500; public const float BaitRadius = 0.05f;
        public int BaitCharges { get; private set; }
        FoodSpot _bait; int _baitUntilTick = -1;
        public bool BaitActive => _baitUntilTick > TicksRun;
        public Vec2 BaitPos => _bait.Pos;
        public float BaitFraction => BaitActive ? (_baitUntilTick - TicksRun) / (float)BaitTicks : 0f;
        public event Action BaitEarned; public event Action<Vec2> BaitPlaced;
        /// <summary>Places the bait (spends a charge). In the replay it is forced: the charge is rebuilt by the same catches, but the recording rules.</summary>
        public bool PlaceBait(Vec2 pos, bool force = false)
        {
            if (!force && (BaitCharges <= 0 || BaitActive)) return false;
            if (Round.State != RoundState.Playing && Round.State != RoundState.Ready) return false;
            if (BaitCharges > 0) BaitCharges--; _bait = new FoodSpot(pos.X, pos.Y, BaitRadius); _baitUntilTick = TicksRun + BaitTicks; BaitPlaced?.Invoke(pos); return true;
        }

        // ---- ADR-015: round sheet - perception (attacks seen, mean reflex, escapes) ----
        public int AttacksSeen { get; private set; } public int ReflexMsTotal { get; private set; } public int Escapes { get; private set; }
        public int ReflexMeanMs => AttacksSeen > 0 ? ReflexMsTotal / AttacksSeen : 0;
        readonly Dictionary<int, int> _attackStartTick = new Dictionary<int, int>(); readonly HashSet<int> _attackSeen = new HashSet<int>();

        // compatibility with a single fly (slot 0)
        public FlyAgent First => Flies[0];
        public BodyController Body => Flies[0].Body;
        public FlyCoreInstance Core => Flies[0].Core;
        public int FlyIndex => Flies[0].State.Index;
        public Vec2 FlyPrev => Flies[0].Prev;
        public IReadOnlyList<ThreatSample> Threats => Flies[0].Threats;

        readonly FoodSpot[] _food;
        public WorldSimulation(byte[] model, ulong seed, RulesConfig rules, bool assisted, FoodSpot[] food = null, SceneDef scene = null)
        {
            Seed = seed; _model = model; _food = food ?? new FoodSpot[0]; Round = new RoundMachine(rules, seed, scene) { Assisted = assisted };
            var first = Round.Flies[0];
            Flies.Add(new FlyAgent { State = first, Core = new FlyCoreInstance(model, seed), Body = BodyController.EdgeEntry(seed, BodyParams.For(first.Kind, Level(0), _food)) });
            Flies[0].Prev = Flies[0].Body.Position;
            Round.FlyCaught += (fly, attackId, t) => { var ag = Agent(fly); LastCaught = ag; LastCatchPosition = ag.Body.Position; FlyCaught?.Invoke(ag, attackId, t); if (Round.Combo == BaitAtCombo && BaitCharges < BaitMaxCharges) { BaitCharges++; BaitEarned?.Invoke(); } };
            Round.FlySpawned += fly =>
            {
                var ag = Agent(fly);
                ag.Core.Reset(fly.Seed); ag.Body = BodyController.EdgeEntry(fly.Seed, BodyParams.For(fly.Kind, Level(fly.Index), _food)); ag.Prev = ag.Body.Position;
                ag.Threats.Clear(); ag.ThreatByAttack.Clear(); ag.EscapePeakByAttack.Clear();
                FlySpawned?.Invoke(ag);
            };
            Round.TimeExtended += (ms, combo) => TimeExtended?.Invoke(ms, combo);
            Round.AttackMissed += OnAttackMissed;
        }

        int Level(int index) => Math.Max(0, index + Round.Rules.LevelOffset);

        FlyAgent Agent(FlyState fly)
        {
            while (Flies.Count <= fly.Slot)
            {
                var s = Round.Flies[Flies.Count];
                var ag = new FlyAgent { State = s, Core = new FlyCoreInstance(_model, Seed ^ (ulong)Flies.Count), Body = BodyController.EdgeEntry(Seed, BodyParams.For(s.Kind, Level(s.Index), _food)) };
                ag.Prev = ag.Body.Position; Flies.Add(ag);
            }
            return Flies[fly.Slot];
        }

        public bool FlyActive => Round.FlyActive;
        public void MarkReady() => Round.MarkReady();

        public int Advance(float realDeltaSeconds, bool allowTechnicalPause = true)
        {
            if (Round.State != RoundState.Ready && Round.State != RoundState.Playing) { _accum = 0f; return 0; }
            _accum += realDeltaSeconds;
            if (_accum > MaxLagSeconds)
            {
                if (allowTechnicalPause) { TechnicalPauses++; Round.Pause(); _accum = 0f; return 0; }
                _accum = MaxLagSeconds;
            }
            int ticks = 0;
            while (_accum >= TickMs / 1000f)
            {
                _accum -= TickMs / 1000f;
                if (!Tick()) break;
                ticks++;
                if (Round.IsTerminal) { _accum = 0f; break; }
            }
            return ticks;
        }

        public ConfirmError Confirm(ToolDefinition tool, Vec2 target, out Attack attack) => Round.TryConfirm(tool, target, out attack);
        public void Pause() { Round.Pause(); _accum = 0f; }
        public void Resume() { Round.Resume(); _accum = 0f; }
        public void Interrupt(string reason) => Round.Interrupt(reason);

        bool Tick()
        {
            double neural = 0;
            for (int i = 0; i < Round.Flies.Count; i++)
            {
                var ag = Agent(Round.Flies[i]);
                if (!ag.Active) continue;
                ag.Body.Bait = BaitActive ? _bait : (FoodSpot?)null;   // v1.6
                SyncThreats(ag);
                SensoryEncoder.Encode(ag.Channels, ag.Body.Position, ag.Body.Phi, ag.Body.Velocity, ag.Threats, TickMs / 1000f, 1f);
                _sw.Restart();
                bool ok = ag.Core.Step(ag.Channels, TickMs);
                _sw.Stop(); neural += _sw.Elapsed.TotalMilliseconds;
                if (!ok) { NumericFailure = true; Round.Interrupt("neural_numeric_failure"); return false; }
                ag.Prev = ag.Body.Position; ag.Body.Step(ag.Core.Motor);
                if (ag.Body.TookOff) FlyTookOff?.Invoke(ag);
                float e = ag.Body.EscapeDrive;
                foreach (var t in ag.Threats) { ag.EscapePeakByAttack.TryGetValue(t.AttackId, out var pk); if (e > pk) ag.EscapePeakByAttack[t.AttackId] = e; if (e > EscapeDriveThreshold && _attackSeen.Add(t.AttackId)) { AttacksSeen++; ReflexMsTotal += (TicksRun - _attackStartTick[t.AttackId]) * TickMs; } }
                ag.State.Prev = ag.Prev; ag.State.Pos = ag.Body.Position;
            }
            NeuralMsLastTick = neural; NeuralMsTotal += neural; if (neural > NeuralMsMax) NeuralMsMax = neural;
            Round.Tick();
            TicksRun++;
            return true;
        }

        void OnAttackMissed(Attack a)
        {
            // "escaped": the fly closest to the attack fled (drive above threshold) and the attack passed close by
            if (a.MinEdgeDistance > EscapeMissDistance) return;
            FlyAgent best = null; float bestD = float.MaxValue;
            foreach (var ag in Flies)
            {
                if (!ag.Active) continue;
                ag.EscapePeakByAttack.TryGetValue(a.Id, out var pk); if (pk < EscapeDriveThreshold) continue;
                float d = (ag.Body.Position - a.Target).Length; if (d < bestD) { bestD = d; best = ag; }
            }
            if (best != null) { Escapes++; FlyEscaped?.Invoke(best, a); }
        }

        void SyncThreats(FlyAgent ag)
        {
            ag.Threats.Clear();
            foreach (var a in Round.Attacks)
            {
                if (a.Phase == AttackPhase.Done) { ag.ThreatByAttack.Remove(a.Id); continue; }
                if (!ag.ThreatByAttack.TryGetValue(a.Id, out var t)) { t = new ThreatSample { AttackId = a.Id }; ag.ThreatByAttack[a.Id] = t; if (!_attackStartTick.ContainsKey(a.Id)) _attackStartTick[a.Id] = TicksRun; }
                float baseR = a.Tool.SensoryRadius * (Round.Assisted ? Round.Rules.AssistScale : 1f);
                t.Position = a.Target; t.Velocity = new Vec2(0, 0); t.Radius = baseR * a.ThreatRadiusFraction(Round.SimMs);
                ag.Threats.Add(t);
            }
        }

        public void Dispose() { foreach (var ag in Flies) ag.Core.Dispose(); }
    }
}
