import pyarrow.feather as pf, pandas as pd
pd.set_option("display.width", 220)
nt = pf.read_table("data/raw/body-neurotransmitters-male-cns-v1.0.feather").to_pandas()
print("NT rows:", len(nt)); print(list(nt.columns)); print(nt.head(3).to_string())
ann = pf.read_table("data/raw/body-annotations-male-cns-v1.0-minconf-0.5.feather", columns=["bodyId","type"]).to_pandas()
m = nt.merge(ann, left_on=nt.columns[0], right_on="bodyId", how="left") if "bodyId" not in nt.columns else nt.merge(ann, on="bodyId", how="left")
for t in ["LC4","LPLC2","LPLC1","LPLC4","DNp01","DNp02","DNp04","DNp11","DNp06","DNp03","PVLP122","PVLP123","PVLP010","PVLP151"]:
    s = m[m["type"]==t]
    cols = [c for c in nt.columns if c not in ("bodyId",) and s[c].dtype.kind in "fi"]
    if len(s): print(t, "n=",len(s), "|", s[cols].mean(numeric_only=True).round(3).sort_values(ascending=False).head(4).to_dict())
