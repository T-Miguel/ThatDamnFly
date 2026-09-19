import re, pyarrow.feather as pf, pandas as pd
pd.set_option("display.width", 220)
ann = pf.read_table("data/raw/body-annotations-male-cns-v1.0-minconf-0.5.feather").to_pandas()
ann = ann[ann["status"] == "Traced"]
pat = re.compile(r"^(LC4|LPLC2|LPLC1|LC6|LC16|LC22|LPLC4|DNp01|DNp02|DNp03|DNp04|DNp06|DNp11|DNp09|DNp10|DNa0[1-9]|DNb0[1-9]|PLP|GF)\b", re.I)
m = ann[ann["type"].fillna("").str.match(pat)]
print("--- candidate types: count by type × somaSide ---")
print(m.groupby(["type", m["somaSide"].fillna("?")]).size().unstack(fill_value=0).to_string())
print()
print("--- hemibrainType / flywireType / synonyms for DN candidates ---")
dn = m[m["type"].str.startswith("DN")]
print(dn[["bodyId","type","instance","hemibrainType","flywireType","superclass","synonyms"]].drop_duplicates("type").to_string(index=False))
print()
print("--- all descending_neuron types with 'DNp' prefix (count) ---")
dnp = ann[(ann["superclass"]=="descending_neuron") & ann["type"].fillna("").str.startswith("DNp")]
print(dnp["type"].value_counts().to_string())
print()
print("--- visual_projection types starting with LC/LPLC (count) ---")
vp = ann[(ann["superclass"]=="visual_projection") & ann["type"].fillna("").str.match(r"^(LC|LPLC)")]
print(vp["type"].value_counts().to_string())
