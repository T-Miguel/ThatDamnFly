"""Sensory encoder v1 - reference implementation of docs/SENSORY_MODEL.md.
The C# implementation in the game must reproduce this function (fixtures in out/fixtures)."""
from __future__ import annotations
import math
import numpy as np

W = 1.0; H = 4.0 / 3.0
D_MIN = 0.005
EXPANSION_NORM = 12.0      # rad/s
MOTION_NORM = 2.0          # W/s
SURFACE_RANGE = 0.10       # W
BIG_ANGLE = math.radians(45.0)
ADJ_FACTOR = 0.5

def wrap(a: float) -> float:
    return (a + math.pi) % (2 * math.pi) - math.pi

def sector_of(azimuth: float) -> int:
    return int(round(azimuth / (math.pi / 4))) % 8

class Threat:
    """Disc of radius r with center q and velocity v (W, W/s). theta_prev keeps the angular size of the previous tick."""
    def __init__(self, q, v, r):
        self.q = np.array(q, dtype=float); self.v = np.array(v, dtype=float); self.r = float(r); self.theta_prev = None

def encode(p, phi, v_fly, threats: list[Threat], dt: float = 0.01, background: float = 1.0) -> np.ndarray:
    """Returns a vector of 41 channels: expansion[8], angular_size[8], motion_h[8], motion_v[8], surface[8], background."""
    f = np.zeros(41, dtype=np.float32)
    p = np.asarray(p, dtype=float); v_fly = np.asarray(v_fly, dtype=float)
    for t in threats:
        rel = t.q - p; dist_c = float(np.hypot(rel[0], rel[1]))
        d = max(dist_c - t.r, D_MIN)
        theta = 2.0 * math.atan(t.r / d)
        expansion = 0.0 if t.theta_prev is None else max(0.0, (theta - t.theta_prev) / dt) / EXPANSION_NORM
        t.theta_prev = theta
        expansion = min(expansion, 2.0)
        ang = min(1.0, theta / math.pi)
        e_r = rel / dist_c if dist_c > 1e-9 else np.array([1.0, 0.0]); e_t = np.array([-e_r[1], e_r[0]])
        v_rel = t.v - v_fly
        motion_v = float(np.clip(-(v_rel @ e_r) / MOTION_NORM, -1, 1)); motion_h = float(np.clip((v_rel @ e_t) / MOTION_NORM, -1, 1))
        az = wrap(math.atan2(rel[1], rel[0]) - phi); s = sector_of(az)
        targets = [(s, 1.0)]
        if theta > BIG_ANGLE: targets += [((s - 1) % 8, ADJ_FACTOR), ((s + 1) % 8, ADJ_FACTOR)]
        for sec, fac in targets:
            f[0 + sec] = max(f[0 + sec], expansion * fac); f[8 + sec] = max(f[8 + sec], ang * fac)
            f[16 + sec] = float(np.clip(f[16 + sec] + motion_h * fac, -1, 1)); f[24 + sec] = float(np.clip(f[24 + sec] + motion_v * fac, -1, 1))
    # edges: distance to the boundary in the central direction of each sector
    for s in range(8):
        a = phi + s * math.pi / 4; dx, dy = math.cos(a), math.sin(a)
        dist = float("inf")
        if dx > 1e-9: dist = min(dist, (W - p[0]) / dx)
        if dx < -1e-9: dist = min(dist, (0.0 - p[0]) / dx)
        if dy > 1e-9: dist = min(dist, (H - p[1]) / dy)
        if dy < -1e-9: dist = min(dist, (0.0 - p[1]) / dy)
        f[32 + s] = float(np.clip(1.0 - dist / SURFACE_RANGE, 0, 1))
    f[40] = background
    return f
