// Body controller v1.4 - reproduction of docs/BODY_MODEL.md and research/tdf/body.py.
// Reads no threats, aim, tool or fury: only the motor frame and its wandering seed.
// v1.4 (ADR-003): landing/take-off (pauses become longer landings; the neural escape interrupts the landing), factors per fly kind, body radius per kind.
// v1.5 (ADR-007): food attraction (wandering bias and more landings near the scene's food spots) and short landings with a dart ("fakes a landing"). Still reads no threats.
using System;
using ThatDamnFly.Domain;

namespace ThatDamnFly.Simulation
{
    public sealed class BodyParams
    {
        public int Level = 0;
        public float BodyR = 0.02f;
        public float SpeedFactor = 1f, DartFactor = 1f, LandFactor = 1f;   // per fly kind (1 = house fly)
        public float TauW = 0.25f, SigmaW = 4f;
        public float VWanderBase = 0.22f, VWanderPerLevel = 0.08f, VWanderCap = 0.6f;
        public float PLandBase = 0.003f, PLandPerLevel = -0.0003f, PLandMin = 0.0008f;
        public float LandMin = 0.5f, LandMaxBase = 1.6f, LandMaxPerLevel = -0.15f, LandMaxMin = 0.5f;
        public float TakeoffThreshold = 0.25f, LandRefractory = 1.0f;
        public float PDartBase = 0.005f, PDartPerLevel = 0.0025f, DartSpeedBase = 0.75f, DartSpeedPerLevel = 0.1f, DartDuration = 0.25f;
        public float EntrySpeed = 0.6f, EntryDuration = 0.4f;
        public float KTurn = 0f, KEsc = 2.0f, CBack = 0.5f, VMax = 1.3f, TauOmega = 0.03f, TauV = 0.04f, AMax = 16f;
        public bool EscapeEnabled = true;
        // v1.5: food (scene spots) and short landing
        public FoodSpot[] Food = new FoodSpot[0];
        public float AttractRange = 0.35f, KAttract = 3.0f, LandBoost = 6f, LandAtFoodFactor = 1.5f;
        public bool LandShort = false; public float LandShortMin = 0.2f, LandShortMax = 0.4f;
        // v1.6 (ADR-015): bait - temporary food spot that wins over the scene's food, with a larger range and bias
        public float BaitRange = 0.6f, KBait = 4.5f;
        public float VWander => Math.Min(VWanderCap, VWanderBase + VWanderPerLevel * Level) * SpeedFactor;
        public float PLand => Math.Max(PLandMin, PLandBase + PLandPerLevel * Level) * LandFactor;
        public float LandMax => Math.Max(LandMaxMin, LandMaxBase + LandMaxPerLevel * Level);
        public float PDart => (PDartBase + PDartPerLevel * Level) * DartFactor;
        public float DartSpeed => (DartSpeedBase + DartSpeedPerLevel * Level) * SpeedFactor;

        public static BodyParams For(FlyKindDef kind, int level, FoodSpot[] food = null) => new BodyParams { Level = level, BodyR = kind.ContactRadius, SpeedFactor = kind.SpeedFactor, DartFactor = kind.DartFactor, LandFactor = kind.LandFactor, LandShort = kind.LandShort, Food = food ?? new FoodSpot[0] };
    }

    public sealed class BodyController
    {
        public const float Dt = 0.01f;
        readonly BodyParams _p; readonly Xoshiro256 _rng;
        public Vec2 Position; public float Phi; public Vec2 Velocity;
        float _omega, _omegaW, _dartSpeed; int _landTicks, _dartTicks, _noLandTicks;   // durations in integer ticks (exact C#/Python parity)
        public float Speed => Velocity.Length;
        public bool Darting => _dartTicks > 0;
        /// <summary>Landed (wandering landing). The neural escape stays active: a stimulus makes it take off.</summary>
        public bool Landed => _landTicks > 0;
        /// <summary>true only on the tick in which the escape interrupted a landing.</summary>
        public bool TookOff { get; private set; }
        /// <summary>E = a_escL + a_escR (escape drive) and B = a_escR − a_escL on the last tick.</summary>
        /// <summary>v1.6: active bait (temporary spot); null without bait. Set by the simulation every tick.</summary>
        public FoodSpot? Bait;
        public float EscapeDrive { get; private set; }
        public float Lateral { get; private set; }
        public float BodyR => _p.BodyR;
        public float VisualHeading => Speed > 0.3f ? (float)Math.Atan2(Velocity.Y, Velocity.X) : Phi;

        public BodyController(ulong seed, Vec2 start, float? phi, BodyParams p, bool entry)
        {
            _p = p ?? new BodyParams(); _rng = new Xoshiro256(seed ^ 0xB0D7UL);
            Position = start; Phi = phi ?? _rng.Uniform() * 2f * (float)Math.PI;
            if (entry) { _dartTicks = Ticks(_p.EntryDuration); _dartSpeed = _p.EntrySpeed; }
        }

        /// <summary>Fly entering from a random point of the edge, facing inwards.</summary>
        public static BodyController EdgeEntry(ulong seed, BodyParams p)
        {
            p = p ?? new BodyParams(); float r0 = p.BodyR;
            var rng = new Xoshiro256(seed ^ 0xE0DEUL); int side = (int)(rng.Uniform() * 4f); float u = 0.15f + 0.7f * rng.Uniform();
            Vec2 pos; float phi;
            switch (side)
            {
                case 0: pos = new Vec2(r0, u * Arena.H); phi = 0f; break;
                case 1: pos = new Vec2(Arena.W - r0, u * Arena.H); phi = (float)Math.PI; break;
                case 2: pos = new Vec2(u * Arena.W, r0); phi = (float)Math.PI / 2f; break;
                default: pos = new Vec2(u * Arena.W, Arena.H - r0); phi = -(float)Math.PI / 2f; break;
            }
            phi += (rng.Uniform() - 0.5f) * (float)(Math.PI / 3);
            return new BodyController(seed, pos, phi, p, true);
        }

        static int Ticks(float seconds) => (int)Math.Round(seconds / Dt);
        static float Wrap(float a) { a = (float)((a + Math.PI) % (2 * Math.PI)); if (a < 0) a += (float)(2 * Math.PI); return a - (float)Math.PI; }

        public void Step(float[] motor)
        {
            var p = _p; TookOff = false;
            _omegaW += (-_omegaW * Dt / p.TauW) + p.SigmaW * (float)Math.Sqrt(Dt) * _rng.Normal();
            float E = 0f, B = 0f;
            if (p.EscapeEnabled) { float aL = motor[0], aR = motor[1]; E = aL + aR; B = aR - aL; }
            EscapeDrive = E; Lateral = B;
            // food attraction: heading bias towards the nearest spot (within range) and more landings next to it
            float omegaBias = 0f; bool nearFood = false;
            {
                float bestD = float.MaxValue; FoodSpot best = default; float range = p.AttractRange, k = p.KAttract; bool baited = false;
                if (Bait.HasValue) { float d = (Bait.Value.Pos - Position).Length; if (d < p.BaitRange) { baited = true; bestD = d; best = Bait.Value; range = p.BaitRange; k = p.KBait; } }   // v1.6: the bait wins over the scene's food
                if (!baited) foreach (var f in p.Food) { float d = (f.Pos - Position).Length; if (d < bestD) { bestD = d; best = f; } }
                if (bestD < range)
                {
                    float w = 1f - bestD / range; float ang = (float)Math.Atan2(best.Pos.Y - Position.Y, best.Pos.X - Position.X);
                    omegaBias = k * (float)Math.Sin(ang - Phi) * w; nearFood = bestD <= best.Radius + 0.04f;
                }
            }
            float vW;
            if (_dartTicks > 0) { _dartTicks--; vW = _dartSpeed; }
            else if (_landTicks > 0 && E > p.TakeoffThreshold) { _landTicks = 0; _noLandTicks = Ticks(p.LandRefractory); TookOff = true; vW = p.VWander; }   // take-off: the escape interrupts the landing
            else if (_landTicks > 0)
            {
                _landTicks--; vW = 0f;
                if (_landTicks == 0 && p.LandShort) { _dartTicks = Ticks(p.DartDuration); _dartSpeed = p.DartSpeed; Phi = Wrap(Phi + (_rng.Uniform() < 0.5f ? 1f : -1f) * (float)(Math.PI / 180.0 * (60.0 + 120.0 * _rng.Uniform()))); }   // "faked a landing": darts
            }
            else
            {
                if (_noLandTicks > 0) _noLandTicks--;
                vW = p.VWander; float r = _rng.Uniform();
                bool mayLand = _noLandTicks <= 0 && E <= p.TakeoffThreshold;   // under threat or right after take-off it does not land
                float pLand = p.PLand * (nearFood ? p.LandBoost : 1f);
                if (r < pLand)
                {
                    if (mayLand)
                    {
                        float u = _rng.Uniform();
                        _landTicks = p.LandShort ? Ticks(p.LandShortMin + (p.LandShortMax - p.LandShortMin) * u) : Ticks((p.LandMin + (p.LandMax - p.LandMin) * u) * (nearFood ? p.LandAtFoodFactor : 1f));
                    }
                    else _rng.Uniform();
                }
                else if (r < pLand + p.PDart)
                {
                    _dartTicks = Ticks(p.DartDuration); _dartSpeed = p.DartSpeed;
                    Phi = Wrap(Phi + (_rng.Uniform() < 0.5f ? 1f : -1f) * (float)(Math.PI / 180.0 * (60.0 + 120.0 * _rng.Uniform())));
                }
            }
            float c = (float)Math.Cos(Phi), s = (float)Math.Sin(Phi);
            Vec2 fwd = new Vec2(c, s), left = new Vec2(-s, c);
            Vec2 vDes = fwd * vW + (fwd * (-p.CBack * E) + left * B) * p.KEsc;
            float n = vDes.Length; if (n > p.VMax) vDes = vDes * (p.VMax / n);
            float omegaDes = _omegaW + p.KTurn * B + omegaBias;
            _omega += (omegaDes - _omega) * (Dt / p.TauOmega);
            Vec2 dv = (vDes - Velocity) * (Dt / p.TauV);
            float dn = dv.Length, lim = p.AMax * Dt; if (dn > lim) dv = dv * (lim / dn);
            Vec2 vel = Velocity + dv;
            Phi = Wrap(Phi + _omega * Dt);
            Vec2 np = Position + vel * Dt; float r0 = p.BodyR;
            if (np.X < r0) { np.X = r0; vel.X = 0; Phi = Wrap(Phi + (float)Math.PI * 0.5f * (_rng.Uniform() < 0.5f ? 1f : -1f)); }
            if (np.X > Arena.W - r0) { np.X = Arena.W - r0; vel.X = 0; Phi = Wrap(Phi + (float)Math.PI * 0.5f * (_rng.Uniform() < 0.5f ? 1f : -1f)); }
            if (np.Y < r0) { np.Y = r0; vel.Y = 0; Phi = Wrap(Phi + (float)Math.PI * 0.5f * (_rng.Uniform() < 0.5f ? 1f : -1f)); }
            if (np.Y > Arena.H - r0) { np.Y = Arena.H - r0; vel.Y = 0; Phi = Wrap(Phi + (float)Math.PI * 0.5f * (_rng.Uniform() < 0.5f ? 1f : -1f)); }
            Velocity = vel; Position = np;
        }
    }
}
