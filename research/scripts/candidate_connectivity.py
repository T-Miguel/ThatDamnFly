"""Counts effective connections between candidate populations of the escape pathway (MaleCNS v1.0).
Reads the weights file in batches (memory map) and filters pre/post within the candidate set."""
import re, time, pyarrow as pa, pyarrow.ipc as ipc, pyarrow.compute as pc, pyarrow.feather as pf, pandas as pd, numpy as np
pd.set_option("display.width", 240); pd.set_option("display.max_rows", 200)
ann = pf.read_table("data/raw/body-annotations-male-cns-v1.0-minconf-0.5.feather", columns=["bodyId","type","somaSide","superclass","status"]).to_pandas()
ann = ann[ann["status"]=="Traced"]
VPN = ["LC4","LPLC2","LPLC1","LC6","LC16","LC22","LPLC4"]
DN  = ["DNp01","DNp02","DNp03","DNp04","DNp06","DNp09","DNp10","DNp11"]
cand = ann[ann["type"].isin(VPN+DN)].copy()
cand["pop"] = cand["type"] + "_" + cand["somaSide"].fillna("?")
ids = pa.array(cand["bodyId"].to_numpy(), pa.int64())
idset = set(cand["bodyId"].tolist())
t0=time.time()
reader = ipc.open_file(pa.memory_map("data/raw/connectome-weights-male-cns-v1.0-minconf-0.5.feather"))
parts=[]
post_in=[]  # all inputs to DN candidates (any pre)
for i in range(reader.num_record_batches):
    b = reader.get_batch(i)
    m_pre = pc.is_in(b.column("body_pre"), value_set=ids); m_post = pc.is_in(b.column("body_post"), value_set=ids)
    both = pc.and_(m_pre, m_post)
    if pc.any(both).as_py(): parts.append(b.filter(both).to_pandas())
    # inputs to DN from anywhere
    dnids = pa.array(cand[cand["type"].isin(DN)]["bodyId"].to_numpy(), pa.int64())
    mp = pc.is_in(b.column("body_post"), value_set=dnids)
    if pc.any(mp).as_py(): post_in.append(b.filter(mp).to_pandas())
E = pd.concat(parts, ignore_index=True); IN = pd.concat(post_in, ignore_index=True)
print("elapsed s:", round(time.time()-t0,1), "| edges within candidate set:", len(E), "| all inputs to DN candidates:", len(IN))
pop = dict(zip(cand["bodyId"], cand["pop"]))
E["pre_pop"]=E["body_pre"].map(pop); E["post_pop"]=E["body_post"].map(pop)
agg = E.groupby(["pre_pop","post_pop"])["weight"].agg(["sum","count"]).reset_index()
agg = agg[agg["post_pop"].str.startswith("DN")].sort_values("sum", ascending=False)
print("\n--- VPN/DN -> DN candidate connections (synapse sum, #pre bodies) ---")
print(agg.head(60).to_string(index=False))
# Top inputs to GF (DNp01) by pre type
typ = dict(zip(ann["bodyId"], ann["type"])); side = dict(zip(ann["bodyId"], ann["somaSide"]))
for dn in ["DNp01","DNp02","DNp04","DNp11","DNp06"]:
    sub = IN[IN["body_post"].isin(cand[cand["type"]==dn]["bodyId"])].copy()
    sub["pre_type"]=sub["body_pre"].map(typ).fillna("(untyped)")
    g = sub.groupby("pre_type")["weight"].sum().sort_values(ascending=False)
    print(f"\n--- top 15 input types to {dn} (total synapses {int(g.sum())}) ---"); print(g.head(15).to_string())
E.to_parquet("out_candidate_edges.parquet") if False else None
import os; os.makedirs("out", exist_ok=True); E.to_csv("out/candidate_edges.csv", index=False); IN.to_csv("out/dn_inputs.csv", index=False)
