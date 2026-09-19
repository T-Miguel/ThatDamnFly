"""Release checklist (AC13): no stub, no diagnostics panel in release, no secrets, valid links, consistent versions. Usage: python tools/release-check.py [dist/site]"""
import os, re, sys, json
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); site = sys.argv[1] if len(sys.argv) > 1 else "dist/site"; ok = True
def fail(m): global ok; ok = False; print("FAIL:", m)
# 1) no neural stub in the code
for dp, _, fs in os.walk(os.path.join(root, "app", "Assets")):
    for f in fs:
        if f.endswith(".cs"):
            t = open(os.path.join(dp, f), encoding="utf-8").read()
            if re.search(r"class\s+\w*Stub\w*", t): fail(f"stub found in {f}")
# 2) secrets
for dp, _, fs in os.walk(root):
    if any(x in dp for x in (os.sep + ".git", "node_modules", os.sep + "Library", os.sep + "dist", os.sep + ".venv", os.sep + "build-")): continue
    for f in fs:
        if f.endswith((".cs", ".json", ".js", ".mjs", ".html", ".ps1", ".py", ".md", ".yml")):
            t = open(os.path.join(dp, f), encoding="utf-8", errors="ignore").read()
            if re.search(r"(AKIA[0-9A-Z]{16}|sk_live_[0-9a-zA-Z]{10,}|-----BEGIN (RSA |EC )?PRIVATE KEY-----|ghp_[0-9A-Za-z]{20,})", t): fail(f"possible secret in {os.path.relpath(os.path.join(dp, f), root)}")
# 3) consistent version and model
mc = open(os.path.join(root, "app", "Assets", "Game.Simulation", "ModelConfigValues.cs"), encoding="utf-8").read()
mid = re.search(r'ModelId = "([^"]+)"', mc).group(1); sha = re.search(r'ExpectedFileSha256 = "([0-9a-f]{64})"', mc).group(1)
man = json.load(open(os.path.join(root, "web", "model", mid, mid + ".manifest.json"), encoding="utf-8"))
if man["file_sha256"] != sha: fail("model hash in ModelConfigValues differs from the manifest")
rb = re.search(r'RemoteBaseUrl = "([^"]+)"', mc).group(1)
if rb != "https://thatdamnfly.com/model/": fail(f"RemoteBaseUrl points to {rb} (dev build); regenerate with tools/gen-model-config.py before the release")
# 4) site: expected files and internal links
if os.path.isdir(os.path.join(root, site)):
    need = ["index.html", "_headers", ".htaccess", "Build/.htaccess", "model/.htaccess", "termos/index.html", "privacidade/index.html", "apoio/index.html", "ciencia/index.html", "robots.txt", "sitemap.xml", "manifest.webmanifest", "TemplateData/og.png", f"model/{mid}/{mid}.tdfm", f"model/{mid}/CREDITS.md", f"model/{mid}/LICENSE-CC-BY-4.0.txt"]
    for n in need:
        if not os.path.exists(os.path.join(root, site, n)): fail(f"site without {n}")
    for page in ["index.html", "privacidade/index.html", "apoio/index.html", "ciencia/index.html", "termos/index.html"]:
        p = os.path.join(root, site, page)
        if os.path.exists(p):
            for href in re.findall(r'href="(/[^"#]+)"', open(p, encoding="utf-8").read()):
                target = os.path.join(root, site, href.lstrip("/"))
                if not (os.path.exists(target) or os.path.exists(os.path.join(target, "index.html"))): fail(f"{page}: broken internal link {href}")
    idx = open(os.path.join(root, site, "index.html"), encoding="utf-8").read() if os.path.exists(os.path.join(root, site, "index.html")) else ""
    if "TDF_DEVBUILD" in idx: fail("index carries the dev build marker")
else:
    print("warning: site not assembled; skipping site checks")
print("OK" if ok else "FAILED")
sys.exit(0 if ok else 1)
