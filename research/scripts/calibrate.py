"""Scale calibration (before the protocol): FRONTAL open-loop stimulus, stationary fly.
Measures background and evoked rates per role/type/side and the activity of the motor channels. Uses neither lateral directions nor ablation.
Usage: python scripts/calibrate.py [--sweep w_scale=0.05,0.1,0.2] [--params params/model_params.json]
"""
from __future__ import annotations
import argparse, json, os, subprocess, sys, tempfile
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from tdf.flycore import FlyCore, Instance
from tdf.tdfm import channel_index

def export_with(params: dict, out_dir: str) -> str:
    pp = os.path.join(out_dir, "params.json"); json.dump(params, open(pp, "w", encoding="utf-8"))
    r = subprocess.run([sys.executable, "scripts/export_model.py", "--params", pp, "--out", out_dir, "--model-id", "calib"], capture_output=True, text=True, env={**os.environ, "PYTHONUTF8": "1"})
    if r.returncode != 0: raise RuntimeError(r.stderr)
    return os.path.join(out_dir, "calib.tdfm")

def measure(core: FlyCore, model_path: str, neurons: pd.DataFrame, seeds=(1, 2, 3), stim_ms=300, base_ms=300, expansion=1.0, ang=0.5, sector=0):
    mb = open(model_path, "rb").read(); rows = []
    for seed in seeds:
        inst = Instance(core, mb, seed)
        f = np.zeros(41, dtype=np.float32); f[40] = 1.0; inst.set_frame(f)
        for _ in range(base_ms // 10): inst.step(10)
        base = inst.spike_counts().astype(float); inst.clear_spike_counts()
        f[0 + sector] = expansion; f[8 + sector] = ang; inst.set_frame(f)
        acts = []
        for _ in range(stim_ms // 10): acts.append(inst.step(10))
        ev = inst.spike_counts().astype(float); acts = np.array(acts)
        inst.close()
        df = neurons[["local_index", "type", "side", "role"]].copy()
        df["base_hz"] = base / (base_ms / 1000.0); df["evoked_hz"] = ev / (stim_ms / 1000.0); df["seed"] = seed
        rows.append((df, acts))
    df = pd.concat([r[0] for r in rows]); acts = np.mean([r[1] for r in rows], axis=0)
    return df, acts

def summarize(df: pd.DataFrame, acts: np.ndarray, label: str):
    g = df.groupby("role")[["base_hz", "evoked_hz"]].mean().round(1)
    vpn = df[df.role == "vpn"].groupby(["type", "side"])[["base_hz", "evoked_hz"]].mean().round(1)
    dn = df[df.role == "dn"].groupby(["type", "side"])[["base_hz", "evoked_hz"]].mean().round(1)
    print(f"\n=== {label} ===\nby role:\n{g}\nVPN (frontal stim; sector 0 shared L/R):\n{vpn}\nDN:\n{dn}")
    print(f"motor channels (mean over stim, last 100 ms): esc_L={acts[-10:,0].mean():.3f} esc_R={acts[-10:,1].mean():.3f} turn_L={acts[-10:,2].mean():.3f} turn_R={acts[-10:,3].mean():.3f} fwd_L={acts[-10:,4].mean():.3f} fwd_R={acts[-10:,5].mean():.3f}")

def main():
    ap = argparse.ArgumentParser(); ap.add_argument("--params", default="params/model_params.json"); ap.add_argument("--sweep", default=None); ap.add_argument("--expansion", type=float, default=1.0); ap.add_argument("--ang", type=float, default=0.5)
    a = ap.parse_args()
    base = json.load(open(a.params, encoding="utf-8")); neurons = pd.read_csv("out/circuit/neurons.csv", dtype={"bodyId": str}); core = FlyCore()
    variants = [("base", base)]
    if a.sweep:
        key, vals = a.sweep.split("="); path = key.split(".")
        for v in vals.split(","):
            p = json.loads(json.dumps(base)); d = p
            for k in path[:-1]: d = d[k]
            d[path[-1]] = float(v); variants.append((f"{key}={v}", p))
    with tempfile.TemporaryDirectory() as td:
        for label, p in variants:
            mp = export_with(p, td); df, acts = measure(core, mp, neurons, expansion=a.expansion, ang=a.ang); summarize(df, acts, label)

if __name__ == "__main__":
    main()
