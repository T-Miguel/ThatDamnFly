"""xoshiro256** + splitmix64 - identical to FlyCore (for the body's wandering and for parity)."""
M64 = (1 << 64) - 1

def splitmix64(x: int):
    x = (x + 0x9E3779B97F4A7C15) & M64
    z = x
    z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & M64
    z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & M64
    return x, (z ^ (z >> 31)) & M64

def rotl(x, k): return ((x << k) | (x >> (64 - k))) & M64

class Xoshiro256:
    def __init__(self, seed: int):
        x = seed & M64; self.s = []
        for _ in range(4):
            x, v = splitmix64(x); self.s.append(v)
    def next(self) -> int:
        s = self.s
        result = (rotl((s[1] * 5) & M64, 7) * 9) & M64
        t = (s[1] << 17) & M64
        s[2] ^= s[0]; s[3] ^= s[1]; s[1] ^= s[2]; s[0] ^= s[3]; s[2] ^= t; s[3] = rotl(s[3], 45)
        return result
    def uniform(self) -> float:
        return (self.next() >> 40) * (1.0 / 16777216.0)
    def normal(self) -> float:
        # Box–Muller with two uniforms (for the wandering; it does not need to match the C++ neural one)
        import math
        u1 = max(self.uniform(), 1e-12); u2 = self.uniform()
        return math.sqrt(-2.0 * math.log(u1)) * math.cos(2 * math.pi * u2)
