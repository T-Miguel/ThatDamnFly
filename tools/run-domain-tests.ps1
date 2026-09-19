# Runs the domain tests loading the DLL in memory (Smart App Control blocks unsigned DLLs loaded from disk;
# an assembly loaded from bytes does not go through the file check). Requires pwsh 7 and `dotnet build -c Release`.
# Usage: pwsh tools/run-domain-tests.ps1
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "app\Tests.Domain"
& dotnet build (Join-Path $proj "Tests.Domain.csproj") -c Release --nologo -v q | Where-Object { $_ -match "error" } | ForEach-Object { Write-Host $_ }; if ($LASTEXITCODE -ne 0) { throw "build falhou" }
$dir = Join-Path $proj "bin\Release\net9.0"
$env:TDF_FIXTURE_DIR = $proj
foreach ($n in @("xunit.abstractions.dll", "xunit.assert.dll", "xunit.core.dll")) { [void][System.Reflection.Assembly]::LoadFrom((Join-Path $dir $n)) }
$asm = [System.Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $dir "Tests.Domain.dll")))
$pass = 0; $fail = 0
foreach ($t in $asm.GetTypes()) {
  foreach ($m in $t.GetMethods()) {
    if (-not ($m.GetCustomAttributes($false) | Where-Object { $_.GetType().Name -eq "FactAttribute" })) { continue }
    try { $obj = [Activator]::CreateInstance($t); $m.Invoke($obj, $null) | Out-Null; $pass++ }
    catch { $fail++; $e = $_.Exception; while ($e.InnerException) { $e = $e.InnerException }; Write-Host ("FAIL {0}.{1}: {2}" -f $t.Name, $m.Name, $e.Message.Split("`n")[0]) }
  }
}
Write-Host ("Tests: {0} passed, {1} failed" -f $pass, $fail)
if ($fail -gt 0) { exit 1 }
