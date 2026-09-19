// xoshiro256** + splitmix64 - identical to FlyCore and to research/tdf/xoshiro.py (wandering parity).
namespace ThatDamnFly.Simulation
{
    public sealed class Xoshiro256
    {
        ulong s0, s1, s2, s3;
        public Xoshiro256(ulong seed)
        {
            ulong x = seed; s0 = Split(ref x); s1 = Split(ref x); s2 = Split(ref x); s3 = Split(ref x);
        }
        static ulong Split(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL; ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31);
        }
        static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));
        public ulong Next()
        {
            ulong result = Rotl(s1 * 5, 7) * 9; ulong t = s1 << 17;
            s2 ^= s0; s3 ^= s1; s1 ^= s2; s0 ^= s3; s2 ^= t; s3 = Rotl(s3, 45); return result;
        }
        public float Uniform() => (float)(Next() >> 40) * (1.0f / 16777216.0f);
        public float Normal()
        {
            float u1 = Uniform(); if (u1 < 1e-12f) u1 = 1e-12f; float u2 = Uniform();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2 * System.Math.PI * u2));
        }
    }
}
