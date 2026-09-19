# Serves the local web build with the right headers and opens a temporary HTTPS tunnel (Cloudflare quick tunnel, no account).
# Usage: tools/serve-tunnel.ps1 [dir] [port]. The trycloudflare.com URL changes on every run and is only for tests.
param([string]$Dir = "dist/web-dev", [int]$Port = 8080)
$root = Split-Path -Parent $PSScriptRoot
$node = "C:\Program Files\nodejs\node.exe"
$cf = "$env:LOCALAPPDATA\Microsoft\WinGet\Links\cloudflared.exe"
Start-Process -FilePath $node -ArgumentList @((Join-Path $root "web\serve.mjs"), (Join-Path $root $Dir), $Port) -NoNewWindow -RedirectStandardOutput (Join-Path $root "tools\serve.log") -RedirectStandardError (Join-Path $root "tools\serve.err")
Start-Process -FilePath $cf -ArgumentList @('tunnel','--url',"http://localhost:$Port",'--no-autoupdate') -NoNewWindow -RedirectStandardOutput (Join-Path $root "tools\tunnel.log") -RedirectStandardError (Join-Path $root "tools\tunnel.err")
Start-Sleep -Seconds 6
Select-String -Path (Join-Path $root "tools\tunnel.err") -Pattern "https://[a-z0-9-]+\.trycloudflare\.com" | ForEach-Object { $_.Matches[0].Value } | Select-Object -First 1
