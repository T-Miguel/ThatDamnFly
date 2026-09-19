# Reproducible Unity builds in batchmode. Usage:
#   tools/unity-build.ps1 web   [-Dev 1|0] [-Out path]
#   tools/unity-build.ps1 android [-Dev 1|0] [-Out path.apk]
#   tools/unity-build.ps1 configure
param([Parameter(Mandatory=$true)][string]$Target, [string]$Dev = "1", [string]$Out = "", [string]$ModelUrl = "")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$unity = "C:\Users\migue\Unity\Editors\6000.3.24f1\Editor\Unity.exe"
$proj = Join-Path $root "app"
$method = switch ($Target) { "web" { "ThatDamnFly.Editor.ProjectSetup.BuildWeb" } "android" { "ThatDamnFly.Editor.ProjectSetup.BuildAndroidApk" } "configure" { "ThatDamnFly.Editor.ProjectSetup.Configure" } default { throw "target desconhecido: $Target" } }
if ($Out -eq "") { $Out = switch ($Target) { "web" { Join-Path $root "dist\web-dev" } "android" { Join-Path $root "dist\android\ThatDamnFly-dev.apk" } default { "" } } }
if ($Out -ne "" -and -not [System.IO.Path]::IsPathRooted($Out)) { $Out = Join-Path $root $Out }   # Unity resolves relative paths against app/; always use an absolute one
$log = Join-Path $root "tools\unity-build-$Target.log"
# model URL for the apps: development tunnel in dev builds, real domain in release ones (ModelConfigValues.cs is generated)
if ($ModelUrl -eq "") { $ModelUrl = if ($Dev -eq "1") { "https://mitsubishi-facing-tomatoes-drawn.trycloudflare.com/model/" } else { "https://thatdamnfly.com/model/" } }
if ($Target -ne "configure") { & python (Join-Path $root "tools\gen-model-config.py") "tdf-escape-v0.1-p2" $ModelUrl | Out-Null; Write-Host "ModelConfigValues.RemoteBaseUrl = $ModelUrl" }
$args = @('-batchmode','-nographics','-quit','-projectPath',$proj,'-executeMethod',$method,'-tdfDev',$Dev,'-tdfOut',$Out,'-logFile',$log)
Write-Host "Unity $Target -> $Out (log: $log)"
$p = Start-Process -FilePath $unity -ArgumentList $args -PassThru -Wait -NoNewWindow
Select-String -Path $log -Pattern "\[TDF\]|error CS|Error building|Build completed" | ForEach-Object { $_.Line }
if ($p.ExitCode -ne 0) { throw "Unity exit $($p.ExitCode)" }
if ($Target -eq "web") {
  # static model next to the build (same origin) + credits/license
  $modelDst = Join-Path $Out "model"; New-Item -ItemType Directory -Force $modelDst | Out-Null; Copy-Item -Recurse -Force (Join-Path $root "web\model\*") $modelDst   # copy the contents (not the folder) so rebuilds do not nest model/model
  Get-ChildItem -Recurse $Out -File | Where-Object { $_.FullName -like "*\Build\*" } | ForEach-Object { "{0,12:N0}  {1}" -f $_.Length, $_.Name }
  "total Build/ (bytes): " + ((Get-ChildItem -Recurse (Join-Path $Out "Build") -File | Measure-Object Length -Sum).Sum)
}
