# Assembles dist/site = web build + public pages + model + headers (Cloudflare Pages: _headers; Apache/Bluehost: .htaccess).
# Usage: tools/deploy-site.ps1 [-Build dist/web-release] [-Out dist/site]
param([string]$Build = "dist/web-release", [string]$Out = "dist/site")
$root = Split-Path -Parent $PSScriptRoot
$b = Join-Path $root $Build; $o = Join-Path $root $Out
if (-not (Test-Path (Join-Path $b "index.html"))) { throw "web build missing: $b (run tools/unity-build.ps1 web -Dev 0 -Out $Build)" }
if (Test-Path $o) { Remove-Item -Recurse -Force $o }
New-Item -ItemType Directory -Force $o | Out-Null
Copy-Item -Recurse -Force (Join-Path $b "*") $o
Get-ChildItem -Path (Join-Path $root "web\public") -Force | Copy-Item -Recurse -Force -Destination $o
New-Item -ItemType Directory -Force (Join-Path $o "model") | Out-Null; Copy-Item -Recurse -Force (Join-Path $root "web\model\*") (Join-Path $o "model")   # no model/model nesting when the build already carries the folder
# immutable cache on the folders with hashed/versioned names (Apache); _headers already does it on Cloudflare
$imm = Join-Path $o ".htaccess-immutable"
Copy-Item $imm (Join-Path $o "Build\.htaccess") -Force; Copy-Item $imm (Join-Path $o "model\.htaccess") -Force; Remove-Item $imm
# the android folder (dev APK) does not go to the site
if (Test-Path (Join-Path $o "android")) { Remove-Item -Recurse -Force (Join-Path $o "android") }
"site at $o"; Get-ChildItem -Recurse $o -File -Force | Measure-Object Length -Sum | ForEach-Object { "total {0:N0} bytes, {1} files" -f $_.Sum, $_.Count }
