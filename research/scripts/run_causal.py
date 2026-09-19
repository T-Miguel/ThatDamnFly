"""Pre-registered causal protocol (docs/CAUSAL_PROTOCOL.md v1.1) - closed loop with the real FlyCore runtime.
Usage: python scripts/run_causal.py --protocol v1 [--seeds 100] [--arms A,B,C,D,E] [--out out/causal]
"""
from __future__ import annotations
import argparse, json, math, os, sys, time
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from tdf.flycore import FlyCore, Instance
from tdf.sensory import Threat, encode, wrap
from tdf.body import Body, BodyParams, H

DIRS_DEG = [0, 45, 90, 135, 180, 225, 270, 315]
SPEEDS = {"slow": 0.6, "medium": 1.2, "fast": 2.4}
THREAT_R = 0.09; START_DIST = 0.45; CONTACT = 0.02 + THREAT_R
PRE_TICKS = 30; POST_TICKS = 70; N_DN_WINDOW_TICKS = 40; D_AWAY_TICKS = 60
ARMS = {"A": "intact", "B": "ablate_vpn_to_dn", "C": "ablate_motor_output", "D": "shuffled_weights", "E": "no_stimulus"}


def shuffled_model(model_bytes: bytes, seed: int = 999) -> bytes:
    """Arm D: permutes the post-synaptic indices inside the payload (preserves out-degrees, weights and delays)."""
    import struct
    hdr = model_bytes[:144]; n, e = struct.unpack_from("<II", hdr, 16)
    payload = bytearray(model_bytes[144:])
    off = n * 8 * 4 + (n + 1) * 4  # after params and row_ptr
    post = np.frombuffer(bytes(payload[off:off + e * 4]), dtype="<u4").copy()
    rng = np.random.default_rng(seed); post = post[rng.permutation(e)]
    payload[off:off + e * 4] = post.astype("<u4").tobytes()
    import hashlib
    sha = hashlib.sha256(bytes(payload)).digest()
    hdr = bytearray(hdr); hdr[48:80] = sha
    return bytes(hdr) + bytes(payload)


def run_trial(core: FlyCore, model_bytes: bytes, mask: np.ndarray | None, arm: str, seed: int, dir_deg: int, speed: float, dn_idx: dict, level: int = 0) -> dict:
    inst = Instance(core, model_bytes, seed)
    if mask is not None: inst.set_edge_mask(mask)
    prm = BodyParams(); prm.escape_enabled = (arm != "C"); prm.level = level
    body = Body(seed, p=(0.5, H / 2), params=prm)
    phi0 = body.phi; p0 = body.p.copy()
    # threat: starts at START_DIST at azimuth dir relative to the initial heading, moves towards the INITIAL POSITION of the fly
    az = phi0 + math.radians(dir_deg); q0 = p0 + START_DIST * np.array([math.cos(az), math.sin(az)])
    u_away = (p0 - q0) / np.linalg.norm(p0 - q0)
    threats = []
    counts_pre = None; motor = np.zeros(8, dtype=np.float32)
    traj_p = []; traj_phi = []; traj_v = []; esc_hist = []
    t_onset = PRE_TICKS
    for t in range(PRE_TICKS + POST_TICKS):
        if t == t_onset:
            inst.clear_spike_counts()
            if arm != "E":
                thr = Threat(q0, u_away * speed, THREAT_R); threats = [thr]  # moves from the threat's initial position towards the fly
        # move the threat (constant speed until the contact distance with the CURRENT position of the fly)
        for thr in threats:
            d_now = np.linalg.norm(thr.q - body.p) - THREAT_R
            if d_now > 0.02: thr.q = thr.q + thr.v * 0.01
            else: thr.v = np.zeros(2)
        frame = encode(body.p, body.phi, body.vel, threats, dt=0.01, background=1.0)
        inst.set_frame(frame); motor = inst.step(10)
        body.step(motor)
        if t == t_onset - 1: counts_pre = inst.spike_counts().copy()
        if t == t_onset + N_DN_WINDOW_TICKS - 1: counts_win = inst.spike_counts().copy()
        traj_p.append(body.p.copy()); traj_phi.append(body.phi); traj_v.append(float(np.hypot(*body.vel))); esc_hist.append((float(motor[0]), float(motor[1])))
    inst.close()
    traj_p = np.array(traj_p); traj_phi = np.array(traj_phi); traj_v = np.array(traj_v)
    # neural metrics (escape group)
    L = dn_idx["escL"]; R = dn_idx["escR"]
    n_base = float(counts_pre[L].sum() + counts_pre[R].sum()) * (N_DN_WINDOW_TICKS / PRE_TICKS)
    nL = float(counts_win[L].sum()); nR = float(counts_win[R].sum()); n_dn = nL + nR
    side = "L" if 0 < dir_deg < 180 else ("R" if dir_deg > 180 else None)
    li = np.nan
    if side and (nL + nR) >= 3:
        ipsi, contra = (nL, nR) if side == "L" else (nR, nL); li = (ipsi - contra) / (ipsi + contra)
    # body metrics
    p_on = traj_p[t_onset - 1]; p_end = traj_p[min(t_onset - 1 + D_AWAY_TICKS, len(traj_p) - 1)]
    d_away = float((p_end - p_on) @ u_away); disp = float(np.linalg.norm(p_end - p_on))
    dphi = wrap(traj_phi[min(t_onset - 1 + D_AWAY_TICKS, len(traj_phi) - 1)] - traj_phi[t_onset - 1])
    ta = 1.0 if d_away > 0.02 else 0.0  # v1.1: "flee"
    v_peak = float(traj_v[t_onset:t_onset + D_AWAY_TICKS].max())
    esc = np.array(esc_hist[t_onset:t_onset + D_AWAY_TICKS]); esc_peak = float(esc.sum(axis=1).max())
    return dict(arm=arm, seed=seed, dir_deg=dir_deg, speed=speed, n_dn=n_dn, n_base=n_base, n_L=nL, n_R=nR, li=li, d_away=d_away, disp=disp, dphi=dphi, ta=ta, v_peak=v_peak, esc_peak=esc_peak)


def bootstrap_ci(x: np.ndarray, n=1000, seed=2026):
    rng = np.random.default_rng(seed); x = np.asarray(x, dtype=float); x = x[~np.isnan(x)]
    if len(x) == 0: return (np.nan, np.nan)
    m = np.array([rng.choice(x, len(x)).mean() for _ in range(n)]); return (float(np.percentile(m, 2.5)), float(np.percentile(m, 97.5)))


def main():
    ap = argparse.ArgumentParser(); ap.add_argument("--protocol", default="v1.1"); ap.add_argument("--seeds", type=int, default=100); ap.add_argument("--arms", default="A,B,C,D,E")
    ap.add_argument("--model", default=None); ap.add_argument("--level", type=int, default=0); ap.add_argument("--out", default="out/causal"); ap.add_argument("--dirs", default=None); ap.add_argument("--speeds", default=None)
    a = ap.parse_args(); os.makedirs(a.out, exist_ok=True)
    model_path = a.model or sorted([f for f in os.listdir("out/model") if f.endswith(".tdfm")])[-1]
    if not os.path.isabs(model_path) and not os.path.exists(model_path): model_path = os.path.join("out/model", model_path)
    mb = open(model_path, "rb").read(); mid = os.path.basename(model_path).replace(".tdfm", "")
    mask_vpn_dn = np.load(os.path.join(os.path.dirname(model_path), mid + ".mask_vpn_to_dn.npy"))
    N = pd.read_csv("out/circuit/neurons.csv", dtype={"bodyId": str}); circ = json.load(open("out/circuit/circuit.json", encoding="utf-8"))
    esc = set(circ["selection"]["escape_dn"])
    dn_idx = {"escL": N[(N.role == "dn") & (N.type.isin(esc)) & (N.side == "L")].local_index.values, "escR": N[(N.role == "dn") & (N.type.isin(esc)) & (N.side == "R")].local_index.values}
    core = FlyCore(); info = core.inspect(mb)
    dirs = [int(x) for x in a.dirs.split(",")] if a.dirs else DIRS_DEG; speeds = {k: v for k, v in SPEEDS.items() if (not a.speeds or k in a.speeds.split(","))}
    arms = a.arms.split(","); shuffled = shuffled_model(mb) if "D" in arms else None
    rows = []; t0 = time.time(); total = len(arms) * len(dirs) * len(speeds) * a.seeds; done = 0
    for arm in arms:
        model = shuffled if arm == "D" else mb; mask = mask_vpn_dn if arm == "B" else None
        for d in dirs:
            for sname, sp in speeds.items():
                for seed in range(1, a.seeds + 1):
                    r = run_trial(core, model, mask, arm, seed, d, sp, dn_idx, a.level); r["speed_name"] = sname; r["level"] = a.level; rows.append(r); done += 1
                if done % 300 == 0: print(f"{done}/{total} trials [{time.time()-t0:.0f}s]", flush=True)
    df = pd.DataFrame(rows); df["model_id"] = mid; df["protocol"] = a.protocol
    df.to_parquet(f"{a.out}/trials.parquet", index=False); df.to_csv(f"{a.out}/trials.csv", index=False)
    # summary per condition
    summ = []
    for (arm, d, sn), g in df.groupby(["arm", "dir_deg", "speed_name"]):
        row = dict(arm=arm, dir_deg=d, speed=sn, n=len(g))
        for m in ["n_dn", "n_base", "li", "d_away", "disp", "ta", "v_peak", "esc_peak"]:
            x = g[m].values.astype(float); row[f"{m}_mean"] = float(np.nanmean(x)) if np.any(~np.isnan(x)) else np.nan; row[f"{m}_sd"] = float(np.nanstd(x)) if np.any(~np.isnan(x)) else np.nan
            row[f"{m}_med"] = float(np.nanmedian(x)) if np.any(~np.isnan(x)) else np.nan
            lo, hi = bootstrap_ci(x); row[f"{m}_ci_lo"] = lo; row[f"{m}_ci_hi"] = hi
        summ.append(row)
    S = pd.DataFrame(summ); S.to_csv(f"{a.out}/summary.csv", index=False)
    # B vs A reduction per condition (primary metric d_away)
    red = []
    if "A" in arms and "B" in arms:
        for (d, sn), gA in df[df.arm == "A"].groupby(["dir_deg", "speed_name"]):
            gB = df[(df.arm == "B") & (df.dir_deg == d) & (df.speed_name == sn)]
            xa = gA.sort_values("seed")["d_away"].values; xb = gB.sort_values("seed")["d_away"].values
            mA = xa.mean(); mB = xb.mean(); R = 1 - mB / mA if mA > 0 else np.nan
            rng = np.random.default_rng(2026); boots = []
            for _ in range(1000):
                idx = rng.integers(0, len(xa), len(xa)); ma = xa[idx].mean(); mb_ = xb[idx].mean(); boots.append(1 - mb_ / ma if ma > 0 else np.nan)
            boots = np.array(boots); boots = boots[~np.isnan(boots)]
            red.append(dict(dir_deg=d, speed=sn, d_away_A=mA, d_away_B=mB, R=R, R_ci_lo=float(np.percentile(boots, 2.5)) if len(boots) else np.nan, R_ci_hi=float(np.percentile(boots, 97.5)) if len(boots) else np.nan,
                            n_dn_A=gA.n_dn.mean(), n_dn_B=gB.n_dn.mean(), ta_A=np.nanmean(gA.ta.values.astype(float)), ta_B=np.nanmean(gB.ta.values.astype(float)), eligible=bool(mA >= 0.05)))
        Rdf = pd.DataFrame(red); Rdf.to_csv(f"{a.out}/reduction_B_vs_A.csv", index=False)
    print(f"done {len(df)} trials in {time.time()-t0:.0f}s → {a.out}")

if __name__ == "__main__":
    main()
