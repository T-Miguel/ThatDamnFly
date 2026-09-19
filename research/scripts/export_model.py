"""Exports the extracted circuit to a .tdfm package with the simulator parameters (H1–H5) and a provenance manifest.
Usage: python scripts/export_model.py --params params/model_params.json --out out/model
"""
from __future__ import annotations
import argparse, hashlib, json, os, subprocess, time
import numpy as np, pandas as pd
import sys; sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from tdf.tdfm import write_tdfm, channel_index

LEFT = [0, 1, 2, 3]; RIGHT = [0, 7, 6, 5]

def git_commit() -> str:
    try: return subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))).decode().strip()
    except Exception: return "unknown"

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--circuit", default="out/circuit"); ap.add_argument("--params", default="params/model_params.json"); ap.add_argument("--out", default="out/model")
    ap.add_argument("--model-id", default=None)
    a = ap.parse_args(); os.makedirs(a.out, exist_ok=True)
    P = json.load(open(a.params, encoding="utf-8"))
    circuit = json.load(open(f"{a.circuit}/circuit.json", encoding="utf-8"))
    N = pd.read_csv(f"{a.circuit}/neurons.csv", dtype={"bodyId": str}); E = pd.read_csv(f"{a.circuit}/edges.csv", dtype={"body_pre": str, "body_post": str})
    n = len(N); assert (N["local_index"].values == np.arange(n)).all()
    lif = P["lif"]
    params = np.tile(np.array([lif["v_rest"], lif["v_thresh"], lif["v_reset"], lif["tau_m_ms"], lif["t_ref_ms"], lif["tau_syn_ms"], lif["bias_mv"], 0.0], dtype=np.float32), (n, 1))
    # edges: w_mv = sign × w_syn × n_syn × w_scale, capped
    syn = P["synapse"]
    w = (E["sign"].values * syn["w_syn_mv"] * E["weight"].values * syn["w_scale"]).astype(np.float32)
    w = np.clip(w, -syn["w_cap_mv"], syn["w_cap_mv"]).astype(np.float32)
    pre = E["pre_idx"].values.astype(np.uint32); post = E["post_idx"].values.astype(np.uint32)
    delay = np.full(len(E), int(syn["delay_ms"]), dtype=np.uint16)
    # inputs
    in_channel, in_neuron, in_rate, in_weight = [], [], [], []
    inp = P["input"]
    # p2 (H1b): normalization of the input gain per hemisphere - compensates the L/R count asymmetry of the reconstruction
    side_norm = {}
    if inp.get("normalize_side_counts", False):
        for t, g in N[N["role"] == "vpn"].groupby("type"):
            nL = int((g["side"] == "L").sum()); nR = int((g["side"] == "R").sum()); m = (nL + nR) / 2.0
            side_norm[(t, "L")] = m / max(nL, 1); side_norm[(t, "R")] = m / max(nR, 1)
    for _, r in N.iterrows():
        i = int(r["local_index"])
        # background for all
        in_channel.append(channel_index("background")); in_neuron.append(i); in_rate.append(inp["background_rate_per_ms"]); in_weight.append(inp["background_weight_mv"])
        if r["role"] == "vpn":
            kind = r["input_channel_kind"]; g = float(r["input_gain"]) * side_norm.get((r["type"], r["side"]), 1.0); s = int(r["sector"])
            in_channel.append(channel_index(kind, s)); in_neuron.append(i); in_rate.append(inp["vpn_rate_per_ms"] * g); in_weight.append(inp["vpn_weight_mv"])
            if s in (3, 5):  # weak coverage of the rear sector (H1)
                in_channel.append(channel_index(kind, 4)); in_neuron.append(i); in_rate.append(inp["vpn_rate_per_ms"] * g * inp["rear_gain"]); in_weight.append(inp["vpn_weight_mv"])
    # outputs (H5): 0/1 escape L/R, 2/3 turn L/R, 4/5 fwd L/R (every DN of the side)
    esc = set(circuit["selection"]["escape_dn"]); trn = set(circuit["selection"]["turn_dn"])
    out_channel, out_neuron, out_gain = [], [], []
    for _, r in N[N["role"] == "dn"].iterrows():
        i = int(r["local_index"]); side = 0 if r["side"] == "L" else 1
        if r["type"] in esc: out_channel.append(0 + side); out_neuron.append(i); out_gain.append(1.0)
        if r["type"] in trn: out_channel.append(2 + side); out_neuron.append(i); out_gain.append(1.0)
        out_channel.append(4 + side); out_neuron.append(i); out_gain.append(1.0)
    n_esc = sum(1 for _, r in N[N["role"] == "dn"].iterrows() if r["type"] in esc and r["side"] == "L")
    n_trn = sum(1 for _, r in N[N["role"] == "dn"].iterrows() if r["type"] in trn and r["side"] == "L")
    n_all = int((N["role"] == "dn").sum() // 2)
    o = P["output"]
    out_tau = [o["tau_escape_ms"]] * 2 + [o["tau_turn_ms"]] * 2 + [o["tau_fwd_ms"]] * 2 + [50.0, 50.0]
    out_scale = [o["scale_per_100hz"] * 10.0 / max(n_esc, 1)] * 2 + [o["scale_per_100hz"] * 10.0 / max(n_trn, 1)] * 2 + [o["scale_per_100hz"] * 10.0 / max(n_all, 1)] * 2 + [0.0, 0.0]
    model_id = a.model_id or f"tdf-escape-v{circuit['circuit_version']}-{P['params_version']}"
    provenance = {
        "model_id": model_id, "generated": time.strftime("%Y-%m-%dT%H:%M:%S"), "exporter_commit": git_commit(),
        "source": circuit["source"], "selection": circuit["selection"], "params": P,
        "neuron_count": n, "edge_count": int(len(E)), "body_ids": N["bodyId"].tolist(), "types": N["type"].tolist(), "sides": N["side"].tolist(),
        "notes": ["64-bit IDs as strings", "circuit selected and adapted from a male specimen (MaleCNS v1.0, CC BY 4.0)", "dynamic parameters are simulator choices"],
    }
    path = f"{a.out}/{model_id}.tdfm"
    res = write_tdfm(path, model_id=model_id, params=params, pre=pre, post=post, weight_mv=w, delay=delay,
                     in_channel=np.array(in_channel, dtype=np.uint32), in_neuron=np.array(in_neuron, dtype=np.uint32), in_rate=np.array(in_rate, dtype=np.float32), in_weight=np.array(in_weight, dtype=np.float32),
                     out_channel=np.array(out_channel, dtype=np.uint32), out_neuron=np.array(out_neuron, dtype=np.uint32), out_gain=np.array(out_gain, dtype=np.float32),
                     out_tau=out_tau, out_scale=out_scale, max_delay=max(8, int(syn["delay_ms"])), provenance=provenance)
    manifest = {k: v for k, v in provenance.items() if k not in ("body_ids", "types", "sides")}
    manifest.update({"file": os.path.basename(path), "bytes": res["bytes"], "payload_sha256": res["payload_sha256"], "file_sha256": res["file_sha256"], "license": "CC-BY-4.0 (dados) - ver CREDITS.md"})
    json.dump(manifest, open(f"{a.out}/{model_id}.manifest.json", "w", encoding="utf-8"), indent=2, ensure_ascii=False)
    # indices of the VPN→DN edges (for arm B of the protocol) in the final CSR order
    order = np.argsort(pre, kind="stable"); pre_s = pre[order]; post_s = post[order]
    role = N["role"].values; vpn_dn = (role[pre_s] == "vpn") & (role[post_s] == "dn")
    np.save(f"{a.out}/{model_id}.mask_vpn_to_dn.npy", vpn_dn.astype(np.uint8))
    print(f"wrote {path} bytes={res['bytes']} sha256={res['file_sha256']} n={n} e={len(E)} in={len(in_neuron)} out={len(out_neuron)} vpn->dn edges masked={int(vpn_dn.sum())}")

if __name__ == "__main__":
    main()
