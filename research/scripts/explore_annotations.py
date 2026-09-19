"""Initial exploration of the MaleCNS v1.0 annotations (read only). Prints the schema and candidates."""
import sys, re
import pyarrow.feather as pf
import pandas as pd
pd.set_option("display.width", 200); pd.set_option("display.max_columns", 40); pd.set_option("display.max_colwidth", 60)

ann = pf.read_table("data/raw/body-annotations-male-cns-v1.0-minconf-0.5.feather").to_pandas()
print("ANNOTATIONS rows:", len(ann)); print("columns:", list(ann.columns)); print(ann.dtypes.to_string()); print()
print(ann.head(3).T.to_string()); print()
for col in ["class", "superclass", "flow", "somaSide", "side", "rootSide", "status"]:
    if col in ann.columns:
        print(f"--- {col} value counts ---"); print(ann[col].value_counts(dropna=False).head(25).to_string()); print()
tcol = "type" if "type" in ann.columns else None
if tcol:
    pat = re.compile(r"^(LC4|LPLC2|LC6|LPLC1|GF|Giant|DNp0[1-9]|DNp1[0-9]|DNa0[1-9]|DNb0[1-9]|DNp02|DNp04|DNp11|DNp06|DNp03|PLP|LC22|LC16|DNg)", re.I)
    m = ann[ann[tcol].fillna("").str.match(pat)]
    print("--- candidate types (regex) counts ---")
    print(m[tcol].value_counts().to_string())
    if "instance" in ann.columns:
        print(); print(m.groupby([tcol, ann.get("somaSide", ann.get("side"))]).size().to_string() if ("somaSide" in ann.columns or "side" in ann.columns) else "")
