// Sensory encoder v1 - exact reproduction of docs/SENSORY_MODEL.md and research/tdf/sensory.py.
// Only receives materialized objects (threats) and edges. Never aim, finger, tool, fury or history.
using System;
using ThatDamnFly.Domain;

namespace ThatDamnFly.Simulation
{
    public sealed class ThreatSample
    {
        public Vec2 Position; public Vec2 Velocity; public float Radius;
        public float ThetaPrev = -1f;    // angular size on the previous tick (−1 = no history)
        public int AttackId;
    }

    public static class SensoryEncoder
    {
        public const int Channels = 41;
        const float DMin = 0.005f, ExpansionNorm = 12f, MotionNorm = 2f, SurfaceRange = 0.10f, AdjFactor = 0.5f;
        static readonly float BigAngle = (float)(Math.PI / 4);

        static float Wrap(float a) { a = (float)((a + Math.PI) % (2 * Math.PI)); if (a < 0) a += (float)(2 * Math.PI); return a - (float)Math.PI; }
        static int SectorOf(float az) { int s = (int)Math.Round(az / (Math.PI / 4)); return ((s % 8) + 8) % 8; }
        static float Clip(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);

        /// <summary>Writes 41 channels into <paramref name="f"/>: expansion[8], angular_size[8], motion_h[8], motion_v[8], surface[8], background.</summary>
        public static void Encode(float[] f, Vec2 p, float phi, Vec2 vFly, System.Collections.Generic.List<ThreatSample> threats, float dt, float background)
        {
            Array.Clear(f, 0, f.Length);
            foreach (var t in threats)
            {
                Vec2 rel = t.Position - p; float distC = rel.Length;
                float d = Math.Max(distC - t.Radius, DMin);
                float theta = 2f * (float)Math.Atan(t.Radius / d);
                float expansion = t.ThetaPrev < 0 ? 0f : Math.Max(0f, (theta - t.ThetaPrev) / dt) / ExpansionNorm;
                t.ThetaPrev = theta;
                if (expansion > 2f) expansion = 2f;
                float ang = Math.Min(1f, theta / (float)Math.PI);
                Vec2 er = distC > 1e-9f ? rel * (1f / distC) : new Vec2(1, 0); Vec2 et = new Vec2(-er.Y, er.X);
                Vec2 vrel = t.Velocity - vFly;
                float motionV = Clip(-(vrel.Dot(er)) / MotionNorm, -1, 1), motionH = Clip(vrel.Dot(et) / MotionNorm, -1, 1);
                float az = Wrap((float)Math.Atan2(rel.Y, rel.X) - phi); int s = SectorOf(az);
                Apply(f, s, 1f, expansion, ang, motionH, motionV);
                if (theta > BigAngle) { Apply(f, (s + 7) % 8, AdjFactor, expansion, ang, motionH, motionV); Apply(f, (s + 1) % 8, AdjFactor, expansion, ang, motionH, motionV); }
            }
            for (int s = 0; s < 8; s++)
            {
                float a = phi + s * (float)(Math.PI / 4); float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a);
                float dist = float.PositiveInfinity;
                if (dx > 1e-9f) dist = Math.Min(dist, (Arena.W - p.X) / dx);
                if (dx < -1e-9f) dist = Math.Min(dist, (0f - p.X) / dx);
                if (dy > 1e-9f) dist = Math.Min(dist, (Arena.H - p.Y) / dy);
                if (dy < -1e-9f) dist = Math.Min(dist, (0f - p.Y) / dy);
                f[32 + s] = Clip(1f - dist / SurfaceRange, 0, 1);
            }
            f[40] = background;
        }

        static void Apply(float[] f, int sec, float fac, float expansion, float ang, float motionH, float motionV)
        {
            f[sec] = Math.Max(f[sec], expansion * fac); f[8 + sec] = Math.Max(f[8 + sec], ang * fac);
            f[16 + sec] = Clip(f[16 + sec] + motionH * fac, -1, 1); f[24 + sec] = Clip(f[24 + sec] + motionV * fac, -1, 1);
        }
    }
}
