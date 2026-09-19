"""Extraction of the candidate looming-escape circuit from MaleCNS v1.0.

Output: research/out/circuit/{neurons.csv, edges.csv, circuit.json, stats.json}
Every choice (types, thresholds, signs, sectors) is recorded in circuit.json as a hypothesis.
64-bit IDs are written as strings.
"""
from __future__ import annotations
import argparse, json, os, time, hashlib
import numpy as np, pandas as pd, pyarrow as pa, pyarrow.ipc as ipc, pyarrow.compute as pc, pyarrow.feather as pf

RAW = "data/raw"
ANN = f"{RAW}/body-annotations-male-cns-v1.0-minconf-0.5.feather"
NT = f"{RAW}/body-neurotransmitters-male-cns-v1.0.feather"
WEI = f"{RAW}/connectome-weights-male-cns-v1.0-minconf-0.5.feather"

VPN_TYPES = {"LC4": "expansion", "LPLC2": "angular_size", "LPLC1": "expansion", "LPLC4": "expansion"}
VPN_GAIN = {"LC4": 1.0, "LPLC2": 1.0, "LPLC1": 0.5, "LPLC4": 0.5}
DN_TYPES = ["DNp01", "DNp02", "DNp03", "DNp04", "DNp06", "DNp11"]
ESCAPE_DN = ["DNp01", "DNp02", "DNp04", "DNp11"]
TURN_DN = ["DNp03", "DNp06"]
NT_SIGN = {"acetylcholine": +1.0, "gaba": -1.0, "glutamate": -1.0}   # H3; others → excluded (0)
LEFT_SECTORS = [0, 1, 2, 3]
RIGHT_SECTORS = [0, 7, 6, 5]


def scan_edges(pre_set: set[int] | None, post_set: set[int] | None, min_w: int = 1):
    """Returns a DataFrame of edges with pre∈pre_set (if given) OR post∈post_set (if given)."""
    reader = ipc.open_file(pa.memory_map(WEI))
    pre_arr = pa.array(sorted(pre_set), pa.int64()) if pre_set else None
    post_arr = pa.array(sorted(post_set), pa.int64()) if post_set else None
    parts = []
    for i in range(reader.num_record_batches):
        b = reader.get_batch(i)
        m = None
        if pre_arr is not None:
            m = pc.is_in(b.column("body_pre"), value_set=pre_arr)
        if post_arr is not None:
            mp = pc.is_in(b.column("body_post"), value_set=post_arr)
            m = mp if m is None else pc.or_(m, mp)
        if min_w > 1:
            m = pc.and_(m, pc.greater_equal(b.column("weight"), min_w))
        if pc.any(m).as_py():
            parts.append(b.filter(m).to_pandas())
    return pd.concat(parts, ignore_index=True) if parts else pd.DataFrame(columns=["body_pre", "body_post", "weight"])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--inter-in", type=int, default=30, help="min synapses VPN→X for an interneuron candidate")
    ap.add_argument("--inter-out", type=int, default=30, help="min synapses X→DN for an interneuron candidate")
    ap.add_argument("--min-edge", type=int, default=3, help="min synapses per included edge")
    ap.add_argument("--max-inter", type=int, default=600, help="ceiling of interneurons (by sum VPN→X + X→DN)")
    ap.add_argument("--out", default="out/circuit")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    t0 = time.time()

    ann = pf.read_table(ANN, columns=["bodyId", "type", "somaSide", "superclass", "status", "instance"]).to_pandas()
    ann = ann[ann["status"] == "Traced"].copy()
    nt = pf.read_table(NT, columns=["body", "consensus_nt", "celltype_predicted_nt", "predicted_nt_confidence"]).to_pandas()
    nt = nt.rename(columns={"body": "bodyId"})
    ann = ann.merge(nt, on="bodyId", how="left")
    typ = dict(zip(ann["bodyId"], ann["type"]))

    vpn = ann[ann["type"].isin(VPN_TYPES) & ann["somaSide"].isin(["L", "R"])]
    dn = ann[ann["type"].isin(DN_TYPES) & ann["somaSide"].isin(["L", "R"])]
    seed_ids = set(vpn["bodyId"]) | set(dn["bodyId"])
    print(f"seeds: VPN={len(vpn)} DN={len(dn)}")

    # step 1: edges with pre in the VPNs or post in the DNs → interneuron candidates
    e1 = scan_edges(set(vpn["bodyId"]), set(dn["bodyId"]))
    vpn_out = e1[e1["body_pre"].isin(vpn["bodyId"])].groupby("body_post")["weight"].sum()
    dn_in = e1[e1["body_post"].isin(dn["bodyId"])].groupby("body_pre")["weight"].sum()
    cand = pd.DataFrame({"from_vpn": vpn_out, "to_dn": dn_in}).dropna()
    cand = cand[(cand["from_vpn"] >= args.inter_in) & (cand["to_dn"] >= args.inter_out)]
    cand = cand[~cand.index.isin(seed_ids)]
    cand = cand[cand.index.isin(ann["bodyId"])]  # Traced
    cand["score"] = cand["from_vpn"] + cand["to_dn"]
    cand = cand.sort_values("score", ascending=False).head(args.max_inter)
    inter_ids = set(cand.index)
    print(f"interneuron candidates (VPN→X≥{args.inter_in} & X→DN≥{args.inter_out}): {len(inter_ids)}  [{time.time()-t0:.1f}s]")

    final_ids = seed_ids | inter_ids
    # step 2: every edge inside the final set
    e2 = scan_edges(final_ids, None, min_w=args.min_edge)
    e2 = e2[e2["body_post"].isin(final_ids)].copy()
    print(f"internal edges (w≥{args.min_edge}): {len(e2)}  [{time.time()-t0:.1f}s]")

    # neuron table
    sel = ann[ann["bodyId"].isin(final_ids)].copy()
    sel["role"] = np.where(sel["type"].isin(VPN_TYPES), "vpn", np.where(sel["type"].isin(DN_TYPES), "dn", "inter"))
    sel["side"] = sel["somaSide"].fillna("?")
    sel["nt"] = sel["consensus_nt"].fillna(sel["celltype_predicted_nt"]).fillna("unknown")
    sel["sign"] = sel["nt"].map(NT_SIGN).fillna(0.0)
    sel = sel.sort_values(["role", "type", "side", "bodyId"]).reset_index(drop=True)
    sel["local_index"] = np.arange(len(sel), dtype=np.int64)
    # sector assignment (H1): cyclic by bodyId order within (type, side)
    sel["sector"] = -1
    for (t, side), grp in sel[sel["role"] == "vpn"].groupby(["type", "side"]):
        secs = LEFT_SECTORS if side == "L" else RIGHT_SECTORS
        idx = grp.sort_values("bodyId").index
        for k, i in enumerate(idx):
            sel.loc[i, "sector"] = secs[k % len(secs)]
    sel["input_channel_kind"] = sel["type"].map(VPN_TYPES).fillna("")
    sel["input_gain"] = sel["type"].map(VPN_GAIN).fillna(0.0)

    idx_of = dict(zip(sel["bodyId"], sel["local_index"]))
    sign_of = dict(zip(sel["bodyId"], sel["sign"]))
    e2["pre_idx"] = e2["body_pre"].map(idx_of); e2["post_idx"] = e2["body_post"].map(idx_of)
    e2["sign"] = e2["body_pre"].map(sign_of)
    excluded_sign0 = int((e2["sign"] == 0).sum())
    e2 = e2[e2["sign"] != 0].copy()
    e2["pre_type"] = e2["body_pre"].map(typ); e2["post_type"] = e2["body_post"].map(typ)

    # output
    neurons = sel[["local_index", "bodyId", "type", "instance", "side", "role", "nt", "sign", "sector", "input_channel_kind", "input_gain", "superclass"]].copy()
    neurons["bodyId"] = neurons["bodyId"].astype(str)
    neurons.to_csv(f"{args.out}/neurons.csv", index=False)
    edges = e2[["pre_idx", "post_idx", "body_pre", "body_post", "weight", "sign", "pre_type", "post_type"]].copy()
    edges["body_pre"] = edges["body_pre"].astype(str); edges["body_post"] = edges["body_post"].astype(str)
    edges = edges.sort_values(["pre_idx", "post_idx"]).reset_index(drop=True)
    edges.to_csv(f"{args.out}/edges.csv", index=False)

    stats = {
        "neurons": int(len(neurons)), "edges": int(len(edges)), "synapses_total": int(edges["weight"].sum()),
        "by_role": neurons["role"].value_counts().to_dict(),
        "by_type": neurons["type"].value_counts().to_dict(),
        "by_nt": neurons["nt"].value_counts().to_dict(),
        "edges_excluded_unknown_nt": excluded_sign0,
        "vpn_to_dn_synapses": int(edges[(edges["pre_type"].isin(VPN_TYPES)) & (edges["post_type"].isin(DN_TYPES))]["weight"].sum()),
        "vpn_to_dn_edges": int(len(edges[(edges["pre_type"].isin(VPN_TYPES)) & (edges["post_type"].isin(DN_TYPES))])),
        "max_out_degree": int(edges.groupby("pre_idx").size().max()), "max_in_degree": int(edges.groupby("post_idx").size().max()),
    }
    with open(f"{args.out}/stats.json", "w", encoding="utf-8") as f: json.dump(stats, f, indent=2, ensure_ascii=False)

    src = json.load(open("data/SOURCE_MANIFEST.json", encoding="utf-8"))
    circuit = {
        "circuit_version": "0.1", "created": time.strftime("%Y-%m-%d"),
        "source": {"dataset": src["source_dataset"], "version": src["source_version"], "license": src["license_id"], "files": [{"name": f["name"], "sha256": f["sha256"]} for f in src["source_files"]]},
        "selection": {
            "vpn_types": VPN_TYPES, "vpn_gain": VPN_GAIN, "dn_types": DN_TYPES, "escape_dn": ESCAPE_DN, "turn_dn": TURN_DN,
            "interneuron_rule": f"Traced, typed, not in seeds, sum(VPN→X)≥{args.inter_in} and sum(X→DN)≥{args.inter_out}, top {args.max_inter} by sum",
            "edge_rule": f"weight≥{args.min_edge} synapses between selected bodies; sign from consensus_nt (fallback celltype_predicted_nt); ACh +1, GABA −1, Glu −1, other/unknown excluded",
            "sector_rule": "H1: per (type, side), cyclic assignment by ascending bodyId over LEFT_SECTORS [0,1,2,3] / RIGHT_SECTORS [0,7,6,5]",
            "boundary_cuts": "all inputs to the selected set from non-selected bodies are dropped (replaced by background drive); this is a documented simplification",
        },
        "stats": stats,
        "script_sha256": hashlib.sha256(open(__file__, "rb").read()).hexdigest(),
    }
    with open(f"{args.out}/circuit.json", "w", encoding="utf-8") as f: json.dump(circuit, f, indent=2, ensure_ascii=False)
    print(json.dumps(stats, indent=2, ensure_ascii=False)); print(f"done [{time.time()-t0:.1f}s]")


if __name__ == "__main__":
    main()
