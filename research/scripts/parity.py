"""Python (refsim) ↔ C++ (flycore_cli run) parity, open loop, same stimuli and seeds.
Criteria (CAUSAL_PROTOCOL §6): relative error of the mean rate of the active populations < 5 %; median latency of the 1st DN spike within 10 ms; no lateralization inversion.
"""
from __future__ import annotations
import json, os, subprocess, sys, tempfile, time
import numpy as np, pandas as pd
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from tdf.refsim import RefModel, RefInstance

CLI = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "flycore", "build-win64", "flycore_cli.exe")

def looming_frames(T=100, sector=1, onset=30, peak=1.0):
    f = np.zeros((T, 41), np.float32); f[:, 40] = 1.0
    for t in range(onset, T):
        u = (t - onset) / (T - onset); f[t, 0 + sector] = peak * min(1.0, u * 2.0); f[t, 8 + sector] = 0.6 * min(1.0, u * 1.5)
    return f

def main(n_seeds=20):
    model_path = sorted([f for f in os.listdir("out/model") if f.endswith(".tdfm")])[-1]; mp = os.path.join("out/model", model_path)
    data = open(mp, "rb").read(); M = RefModel(data)
    N = pd.read_csv("out/circuit/neurons.csv", dtype={"bodyId": str}); role = N["role"].values; side = N["side"].values
    frames = looming_frames(); T = len(frames)
    rows = []
    with tempfile.TemporaryDirectory() as td:
        stim = os.path.join(td, "stim.bin"); frames.tobytes(); open(stim, "wb").write(frames.astype("<f4").tobytes())
        for seed in range(1, n_seeds + 1):
            t0 = time.time()
            # C++
            outp = os.path.join(td, f"out{seed}.bin"); r = subprocess.run([CLI, "run", mp, str(seed), stim, outp], capture_output=True, text=True); assert r.returncode == 0, r.stdout + r.stderr
            raw = open(outp, "rb").read(); rec = np.frombuffer(raw[: T * 36], dtype=np.dtype([("ch", "<f4", (8,)), ("spk", "<u4")])); cpp_counts = np.frombuffer(raw[T * 36:], dtype="<u4")
            cpp_ch = rec["ch"]; cpp_spk_per_tick = rec["spk"]
            # Python
            inst = RefInstance(M, seed); py_ch = []; py_spk = []
            for t in range(T):
                a, s = inst.step(frames[t], 10); py_ch.append(a); py_spk.append(s)
            py_ch = np.array(py_ch); py_counts = inst.spike_counts
            # metrics
            def rate(counts, mask): return counts[mask].sum() / max(mask.sum(), 1) / (T * 0.01)
            for r_ in ["vpn", "inter", "dn"]:
                m = role == r_; rows.append(dict(seed=seed, pop=r_, cpp_hz=rate(cpp_counts, m), py_hz=rate(py_counts, m)))
            for sd in ["L", "R"]:
                m = (role == "dn") & (side == sd); rows.append(dict(seed=seed, pop=f"dn_{sd}", cpp_hz=rate(cpp_counts, m), py_hz=rate(py_counts, m)))
            # latency of the 1st tick with escape activity > 0.05 (channels 0+1)
            def lat(ch): idx = np.nonzero((ch[:, 0] + ch[:, 1]) > 0.05)[0]; return (idx[0] * 10) if len(idx) else np.nan
            rows.append(dict(seed=seed, pop="latency_ms", cpp_hz=lat(cpp_ch), py_hz=lat(py_ch)))
            rows.append(dict(seed=seed, pop="exact_spike_total", cpp_hz=int(cpp_counts.sum()), py_hz=int(py_counts.sum())))
            print(f"seed {seed}: cpp spikes={cpp_counts.sum()} py spikes={py_counts.sum()} lat cpp={lat(cpp_ch)} py={lat(py_ch)} [{time.time()-t0:.1f}s]", flush=True)
    df = pd.DataFrame(rows); os.makedirs("out/parity", exist_ok=True); df.to_csv("out/parity/parity.csv", index=False)
    summ = df.groupby("pop")[["cpp_hz", "py_hz"]].mean(); summ["rel_err"] = (summ["cpp_hz"] - summ["py_hz"]).abs() / summ["py_hz"].abs().clip(lower=1e-9)
    lat = df[df["pop"] == "latency_ms"]; lat_dev = abs(np.nanmedian(lat["cpp_hz"]) - np.nanmedian(lat["py_hz"]))
    liL = df[df["pop"] == "dn_L"]; liR = df[df["pop"] == "dn_R"]
    lat_cpp = (liL["cpp_hz"].values - liR["cpp_hz"].values) / (liL["cpp_hz"].values + liR["cpp_hz"].values + 1e-9); lat_py = (liL["py_hz"].values - liR["py_hz"].values) / (liL["py_hz"].values + liR["py_hz"].values + 1e-9)
    inversions = int(np.sum(np.sign(lat_cpp) != np.sign(lat_py)))
    verdict = {"rate_rel_err": {k: float(v) for k, v in summ["rel_err"].items() if k in ("vpn", "inter", "dn", "dn_L", "dn_R")}, "latency_median_dev_ms": float(lat_dev), "lateralization_inversions": inversions,
               "pass": bool(all(v < 0.05 for k, v in summ["rel_err"].items() if k in ("vpn", "inter", "dn")) and lat_dev < 10 and inversions == 0)}
    json.dump(verdict, open("out/parity/verdict.json", "w"), indent=2); print(summ.round(4).to_string()); print(json.dumps(verdict, indent=2))

if __name__ == "__main__":
    main(int(sys.argv[1]) if len(sys.argv) > 1 else 20)
