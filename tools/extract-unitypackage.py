"""Extracts a .unitypackage (tar.gz with GUID/asset, asset.meta, pathname) into the project root, preserving GUIDs.
Usage: python tools/extract-unitypackage.py <package.unitypackage> <Unity project root>
Used for the TMP Essential Resources in batchmode (AssetDatabase.ImportPackage is asynchronous and does not finish with -quit)."""
import os, sys, tarfile

pkg, root = sys.argv[1], sys.argv[2]
count = 0
with tarfile.open(pkg, "r:gz") as t:
    members = {m.name: m for m in t.getmembers()}
    for name, m in members.items():
        if not name.endswith("/pathname"): continue
        guid_dir = name[: -len("/pathname")]
        rel = t.extractfile(m).read().decode("utf-8").splitlines()[0].strip()
        dst = os.path.join(root, rel)
        asset = members.get(guid_dir + "/asset"); meta = members.get(guid_dir + "/asset.meta")
        if asset is not None and asset.isfile():
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            with open(dst, "wb") as f: f.write(t.extractfile(asset).read())
        else:
            os.makedirs(dst, exist_ok=True)   # folder
        if meta is not None:
            with open(dst + ".meta", "wb") as f: f.write(t.extractfile(meta).read())
        count += 1
print(f"extracted {count} entries into {root}")
