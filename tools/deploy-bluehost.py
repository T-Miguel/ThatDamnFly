"""Publishes dist/site to the Bluehost (Apache) hosting over FTP with TLS. No dependencies beyond the standard library.
Credentials NEVER in the repository: read from .env.deploy at the root (ignored by Git) or from the environment variables
  TDF_FTP_HOST, TDF_FTP_USER, TDF_FTP_PASS, TDF_FTP_DIR (default: public_html)
Usage: python tools/deploy-bluehost.py [dist/site] [--dry-run] [--prune] [--test]
  --test only connects, lists the remote folder and exits (check credentials without sending anything).
  --prune deletes in Build/ the remote files that no longer exist locally (old builds with hashed names).
Upload order: everything except index.html, then index.html last (the page only points to the new build once the new build is fully there)."""
from __future__ import annotations
import ftplib, os, posixpath, ssl, sys, time
try: sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception: pass

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
args = [a for a in sys.argv[1:] if not a.startswith("--")]; flags = {a for a in sys.argv[1:] if a.startswith("--")}
site = os.path.join(root, args[0] if args else "dist/site")
dry = "--dry-run" in flags; prune = "--prune" in flags; test = "--test" in flags

def load_env():
    env = {}
    p = os.path.join(root, ".env.deploy")
    if os.path.exists(p):
        for line in open(p, encoding="utf-8"):
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                k, v = line.split("=", 1); env[k.strip()] = v.strip().strip('"').strip("'")
    for k in ("TDF_FTP_HOST", "TDF_FTP_USER", "TDF_FTP_PASS", "TDF_FTP_DIR"):
        if os.environ.get(k): env[k] = os.environ[k]
    return env

env = load_env()
host, user, pw = env.get("TDF_FTP_HOST"), env.get("TDF_FTP_USER"), env.get("TDF_FTP_PASS"); remote_dir = env.get("TDF_FTP_DIR", "public_html")
if not (host and user and pw):
    sys.exit("missing credentials: create .env.deploy with TDF_FTP_HOST=..., TDF_FTP_USER=..., TDF_FTP_PASS=... (file ignored by Git)")
if not test and not os.path.exists(os.path.join(site, "index.html")): sys.exit(f"site missing: {site} (run tools/deploy-site.ps1)")

# local list
files = []
for dp, _, fs in os.walk(site):
    for f in fs:
        full = os.path.join(dp, f); rel = os.path.relpath(full, site).replace(os.sep, "/")
        files.append((rel, full, os.path.getsize(full)))
files.sort(key=lambda t: (t[0] == "index.html", t[0]))   # index.html last
total = sum(s for _, _, s in files)
print(f"{len(files)} files, {total/1e6:.1f} MB → ftps://{host}/{remote_dir}" + (" (dry-run)" if dry else ""))
if dry:
    for rel, _, s in files: print(f"  {s:>10}  {rel}")
    sys.exit(0)

def connect(check_hostname=True):
    ctx = ssl.create_default_context()
    if not check_hostname: ctx.check_hostname = False   # the certificate of Bluehost's FTP server is for the machine name, not the domain; the chain is still verified
    f = ftplib.FTP_TLS(context=ctx, timeout=60); f.connect(host, 21); f.auth(); f.login(user, pw); f.prot_p(); f.set_pasv(True); return f
try: ftp = connect(True)
except ssl.SSLCertVerificationError as e:
    print("warning: the TLS certificate does not match the host name (", str(e)[:80], ") - connecting with chain verification but without name verification"); ftp = connect(False)
print("connected:", ftp.getwelcome()[:60])
ftp.cwd(remote_dir)
base = ftp.pwd()
if test:
    print("remote folder:", base); names = ftp.nlst(base)
    for n in names[:40]: print("  ", posixpath.basename(n))
    if len(names) > 40: print(f"   … {len(names) - 40} more")
    ftp.quit(); sys.exit(0)

made = set()
def ensure_dir(rel_dir):
    if not rel_dir or rel_dir in made: return
    parent = posixpath.dirname(rel_dir); ensure_dir(parent)
    try: ftp.mkd(posixpath.join(base, rel_dir))
    except ftplib.error_perm: pass
    made.add(rel_dir)

def remote_size(path):
    try: return ftp.size(path)
    except ftplib.error_perm: return None

t0 = time.time(); sent = 0; skipped = 0
for rel, full, size in files:
    ensure_dir(posixpath.dirname(rel))
    rpath = posixpath.join(base, rel)
    ftp.voidcmd("TYPE I")
    if rel != "index.html" and not rel.endswith(".htaccess") and remote_size(rpath) == size:
        skipped += 1; continue
    with open(full, "rb") as fh: ftp.storbinary(f"STOR {rpath}", fh, blocksize=1 << 16)
    sent += 1; print(f"  ↑ {rel} ({size} B)")

if prune:
    try:
        remote_build = posixpath.join(base, "Build"); local_build = {rel.split("/", 1)[1] for rel, _, _ in files if rel.startswith("Build/")}
        for name in ftp.nlst(remote_build):
            n = posixpath.basename(name)
            if n not in local_build and n not in (".", ".."):
                ftp.delete(posixpath.join(remote_build, n)); print(f"  ✕ Build/{n} (obsolete)")
    except ftplib.error_perm as e: print("prune:", e)
ftp.quit()
print(f"done: {sent} sent, {skipped} unchanged, {time.time()-t0:.0f} s")
