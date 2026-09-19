"""Body controller v1.6 - reference implementation of docs/BODY_MODEL.md.
Reads no threats: only the neural output (motor frame) and its own wandering seed.
v1.3: agitation levels (successive flies), short darts, entry from the edge, livelier escape gains.
v1.4 (ADR-003): landing/take-off (pauses become longer landings; the neural escape interrupts the landing), factors per fly kind, body radius per kind.
v1.5 (ADR-007): food attraction (wandering bias and more landings near the food spots) and short landings with a dart ("fakes a landing"). Still reads no threats.
v1.6 (ADR-015): bait - temporary food spot (set from outside, `body.bait = (x, y, r)` or None) that wins over the scene's food, with range 0.6 W and bias 4.5."""
from __future__ import annotations
import math
import numpy as np
from .xoshiro import Xoshiro256

W = 1.0; H = 4.0 / 3.0; DT = 0.01; BODY_R = 0.02

class BodyParams:
    level = 0                     # index of the fly in the round (0 = first)
    body_r = 0.02
    speed_factor = 1.0; dart_factor = 1.0; land_factor = 1.0   # per fly kind (1 = house fly)
    tau_w = 0.25; sigma_w = 4.0
    v_wander_base = 0.22; v_wander_per_level = 0.08; v_wander_cap = 0.6
    p_land_base = 0.003; p_land_per_level = -0.0003; p_land_min = 0.0008
    land_min = 0.5; land_max_base = 1.6; land_max_per_level = -0.15; land_max_min = 0.5
    takeoff_threshold = 0.25; land_refractory = 1.0
    p_dart_base = 0.005; p_dart_per_level = 0.0025; dart_speed_base = 0.75; dart_speed_per_level = 0.1; dart_duration = 0.25
    entry_speed = 0.6; entry_duration = 0.4
    k_turn = 0.0; k_esc = 2.0; c_back = 0.5; v_max = 1.3; tau_omega = 0.03; tau_v = 0.04; a_max = 16.0
    escape_enabled = True         # arm C of the protocol: False → neural output ignored
    # v1.5: food (list of (x, y, radius)) and short landing
    food = ()
    attract_range = 0.35; k_attract = 3.0; land_boost = 6.0; land_at_food_factor = 1.5
    land_short = False; land_short_min = 0.2; land_short_max = 0.4
    bait_range = 0.6; k_bait = 4.5   # v1.6
    def v_wander(self): return min(self.v_wander_cap, self.v_wander_base + self.v_wander_per_level * self.level) * self.speed_factor
    def p_land(self): return max(self.p_land_min, self.p_land_base + self.p_land_per_level * self.level) * self.land_factor
    def land_max(self): return max(self.land_max_min, self.land_max_base + self.land_max_per_level * self.level)
    def p_dart(self): return (self.p_dart_base + self.p_dart_per_level * self.level) * self.dart_factor
    def dart_speed(self): return (self.dart_speed_base + self.dart_speed_per_level * self.level) * self.speed_factor

KINDS = {  # mirror of Game.Domain FlyKinds (only the body changes; the circuit is the same)
    "house": dict(body_r=0.02, speed_factor=1.0, dart_factor=1.0, land_factor=1.0),
    "bluebottle": dict(body_r=0.028, speed_factor=0.8, dart_factor=0.7, land_factor=1.4),
    "fruitfly": dict(body_r=0.015, speed_factor=1.25, dart_factor=1.6, land_factor=0.6),
    "horsefly": dict(body_r=0.03, speed_factor=1.35, dart_factor=2.2, land_factor=1.6, land_short=True),
    "boss": dict(body_r=0.034, speed_factor=1.5, dart_factor=2.5, land_factor=1.2, land_short=True),   # ADR-014: boss fly
    "golden": dict(body_r=0.02, speed_factor=1.15, dart_factor=1.3, land_factor=0.8),   # ADR-016: golden fly
}

def params_for(kind: str = "house", level: int = 0, food=()) -> BodyParams:
    p = BodyParams(); p.level = level; p.food = tuple(food)
    for k, v in KINDS[kind].items(): setattr(p, k, v)
    return p

def ticks(seconds: float) -> int: return int(round(seconds / DT))

def wrap(a: float) -> float:
    return (a + math.pi) % (2 * math.pi) - math.pi

class Body:
    def __init__(self, seed: int, p=(0.5, H / 2), phi: float | None = None, params: BodyParams | None = None, entry: bool = False):
        self.prm = params or BodyParams()
        self.rng = Xoshiro256(seed ^ 0xB0D7)          # own wandering seed (derived from the round's seed)
        self.p = np.array(p, dtype=float)
        self.phi = self.rng.uniform() * 2 * math.pi if phi is None else float(phi)
        self.omega = 0.0; self.omega_w = 0.0; self.land_ticks = 0; self.dart_ticks = 0; self.dart_speed = 0.0; self.no_land_ticks = 0   # durations in integer ticks (exact C#/Python parity)
        self.took_off = False; self.escape_drive = 0.0; self.lateral = 0.0
        self.bait = None   # v1.6: (x, y, r) or None
        self.vel = np.zeros(2)
        if entry:
            self.dart_ticks = ticks(self.prm.entry_duration); self.dart_speed = self.prm.entry_speed

    @property
    def landed(self): return self.land_ticks > 0
    @property
    def darting(self): return self.dart_ticks > 0

    @staticmethod
    def edge_entry(seed: int, params: BodyParams | None = None):
        """Fly entering from a random point of the edge, facing inwards."""
        params = params or BodyParams(); r0 = params.body_r
        rng = Xoshiro256(seed ^ 0xE0DE); side = int(rng.uniform() * 4); u = 0.15 + 0.7 * rng.uniform()
        if side == 0: p, phi = (r0, u * H), 0.0
        elif side == 1: p, phi = (W - r0, u * H), math.pi
        elif side == 2: p, phi = (u * W, r0), math.pi / 2
        else: p, phi = (u * W, H - r0), -math.pi / 2
        phi += (rng.uniform() - 0.5) * math.radians(60)
        return Body(seed, p=p, phi=phi, params=params, entry=True)

    def step(self, motor: np.ndarray):
        prm = self.prm; self.took_off = False
        # wandering (independent of threats)
        self.omega_w += (-self.omega_w * DT / prm.tau_w) + prm.sigma_w * math.sqrt(DT) * self.rng.normal()
        # escape (from the circuit): vector in the body frame e_b = (−c_back·E, B)
        if prm.escape_enabled:
            a_l, a_r = float(motor[0]), float(motor[1]); E = a_l + a_r; B = a_r - a_l
        else: E = 0.0; B = 0.0
        self.escape_drive = E; self.lateral = B
        # food attraction: heading bias towards the nearest spot (within range) and more landings next to it
        omega_bias = 0.0; near_food = False
        best = None; best_d = float("inf"); rng_ = prm.attract_range; k_ = prm.k_attract; baited = False
        if self.bait is not None:   # v1.6: the bait wins over the scene's food
            d = math.hypot(self.bait[0] - self.p[0], self.bait[1] - self.p[1])
            if d < prm.bait_range: baited = True; best_d = d; best = tuple(self.bait); rng_ = prm.bait_range; k_ = prm.k_bait
        if not baited:
            for (fx, fy, fr) in prm.food:
                d = math.hypot(fx - self.p[0], fy - self.p[1])
                if d < best_d: best_d = d; best = (fx, fy, fr)
        if best is not None and best_d < rng_:
            w = 1.0 - best_d / rng_; ang = math.atan2(best[1] - self.p[1], best[0] - self.p[0])
            omega_bias = k_ * math.sin(ang - self.phi) * w; near_food = best_d <= best[2] + 0.04
        if self.dart_ticks > 0:
            self.dart_ticks -= 1; v_w = self.dart_speed
        elif self.land_ticks > 0 and E > prm.takeoff_threshold:
            self.land_ticks = 0; self.no_land_ticks = ticks(prm.land_refractory); self.took_off = True; v_w = prm.v_wander()     # take-off: the escape interrupts the landing
        elif self.land_ticks > 0:
            self.land_ticks -= 1; v_w = 0.0
            if self.land_ticks == 0 and prm.land_short:   # "faked a landing": darts
                self.dart_ticks = ticks(prm.dart_duration); self.dart_speed = prm.dart_speed()
                self.phi = wrap(self.phi + (1 if self.rng.uniform() < 0.5 else -1) * math.radians(60 + 120 * self.rng.uniform()))
        else:
            if self.no_land_ticks > 0: self.no_land_ticks -= 1
            v_w = prm.v_wander()
            r = self.rng.uniform()
            may_land = self.no_land_ticks <= 0 and E <= prm.takeoff_threshold   # under threat or right after take-off it does not land
            p_land = prm.p_land() * (prm.land_boost if near_food else 1.0)
            if r < p_land:
                if may_land:
                    u = self.rng.uniform()
                    if prm.land_short: self.land_ticks = ticks(prm.land_short_min + (prm.land_short_max - prm.land_short_min) * u)
                    else: self.land_ticks = ticks((prm.land_min + (prm.land_max() - prm.land_min) * u) * (prm.land_at_food_factor if near_food else 1.0))
                else: self.rng.uniform()
            elif r < p_land + prm.p_dart():
                self.dart_ticks = ticks(prm.dart_duration); self.dart_speed = prm.dart_speed()
                self.phi = wrap(self.phi + (1 if self.rng.uniform() < 0.5 else -1) * math.radians(60 + 120 * self.rng.uniform()))
        c, s = math.cos(self.phi), math.sin(self.phi)
        fwd = np.array([c, s]); left = np.array([-s, c])
        v_des = v_w * fwd + prm.k_esc * (-prm.c_back * E * fwd + B * left)
        n = float(np.hypot(v_des[0], v_des[1]))
        if n > prm.v_max: v_des *= prm.v_max / n
        omega_des = self.omega_w + prm.k_turn * B + omega_bias
        # integration
        self.omega += (omega_des - self.omega) * (DT / prm.tau_omega)
        dv = (v_des - self.vel) * (DT / prm.tau_v)
        dn = float(np.hypot(dv[0], dv[1])); lim = prm.a_max * DT
        if dn > lim: dv *= lim / dn
        vel = self.vel + dv
        self.phi = wrap(self.phi + self.omega * DT)
        newp = self.p + vel * DT
        # edges: tangential projection (no teleporting) and turn towards the interior
        r0 = prm.body_r
        for i, lim_p in ((0, W), (1, H)):
            if newp[i] < r0: newp[i] = r0; vel[i] = 0.0; self.phi = wrap(self.phi + math.pi * 0.5 * (1 if self.rng.uniform() < 0.5 else -1))
            if newp[i] > lim_p - r0: newp[i] = lim_p - r0; vel[i] = 0.0; self.phi = wrap(self.phi + math.pi * 0.5 * (1 if self.rng.uniform() < 0.5 else -1))
        self.vel = vel; self.p = newp
        return self.p, self.phi, self.vel
