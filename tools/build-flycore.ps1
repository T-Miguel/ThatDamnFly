# Reproduces the FlyCore builds used by the Unity project (Windows x64 DLL, Android ARM64 .so, wasm check) and the Web/iOS amalgamation.
# Requirements: VS Build Tools 2022 (C++), CMake (winget), Unity 6000.3.24f1 with the Android and Web modules in C:\Users\migue\Unity\Editors.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$fc = Join-Path $root "flycore"
$cmake = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Kitware.CMake_Microsoft.Winget.Source_8wekyb3d8bbwe\cmake-4.4.3-windows-x86_64\bin\cmake.exe"
$unity = "C:\Users\migue\Unity\Editors\6000.3.24f1\Editor\Data\PlaybackEngines"
$ndk = "$unity\AndroidPlayer\NDK"
$ninja = "$unity\AndroidPlayer\SDK\cmake\3.22.1\bin\ninja.exe"
$em = "$unity\WebGLSupport\BuildTools\Emscripten"

# 1) Windows x64 (editor) + tests + bench
& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\Common7\Tools\Launch-VsDevShell.ps1" -Arch amd64 -HostArch amd64 -SkipAutomaticLocation 2>$null | Out-Null
& $cmake -S $fc -B "$fc\build-win64" -G Ninja -DCMAKE_BUILD_TYPE=Release | Out-Null
& $cmake --build "$fc\build-win64" | Out-Null
& "$fc\build-win64\flycore_tests.exe" | Select-Object -Last 3
Copy-Item "$fc\build-win64\flycore.dll" "$root\app\Assets\Plugins\x86_64\flycore.dll" -Force

# 2) Android ARM64 (16 KB pages) with Unity's NDK
& $cmake -S $fc -B "$fc\build-android-arm64" -G Ninja -DCMAKE_MAKE_PROGRAM=$ninja -DCMAKE_TOOLCHAIN_FILE="$ndk\build\cmake\android.toolchain.cmake" -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-29 -DANDROID_STL=c++_static -DCMAKE_BUILD_TYPE=Release -DFC_BUILD_TESTS=OFF | Out-Null
& $cmake --build "$fc\build-android-arm64" | Out-Null
$strip = Get-ChildItem "$ndk\toolchains\llvm\prebuilt" -Filter "llvm-strip.exe" -Recurse | Select-Object -First 1 -ExpandProperty FullName
$readelf = Get-ChildItem "$ndk\toolchains\llvm\prebuilt" -Filter "llvm-readelf.exe" -Recurse | Select-Object -First 1 -ExpandProperty FullName
New-Item -ItemType Directory -Force "$root\app\Assets\Plugins\Android\arm64-v8a" | Out-Null
& $strip --strip-unneeded -o "$root\app\Assets\Plugins\Android\arm64-v8a\libflycore.so" "$fc\build-android-arm64\libflycore.so"
"LOAD alignment (must be 0x4000):"; & $readelf -l "$root\app\Assets\Plugins\Android\arm64-v8a\libflycore.so" | Select-String "LOAD"

# 3) wasm check with Unity's Emscripten (tests + bench under node)
$py = "$em\python\python.exe"; $node = "$em\node\node.exe"
$cfg = "$env:TEMP\emcfg.py"
@"
LLVM_ROOT = r'$em\llvm'
BINARYEN_ROOT = r'$em\binaryen'
NODE_JS = r'$node'
EMSCRIPTEN_ROOT = r'$em\emscripten'
CACHE = r'$env:TEMP\emcache'
"@ | Set-Content $cfg
$env:EM_CONFIG = $cfg
New-Item -ItemType Directory -Force "$fc\build-wasm-check" | Out-Null
& $py "$em\emscripten\emcc.py" -std=c++17 -O2 -fexceptions -I"$fc\include" -I"$fc\src" -I"$fc\tests" -DFC_STATIC=1 -DFC_BUILD=1 "$fc\src\flycore.cpp" "$fc\src\sha256.cpp" "$fc\tests\model_builder.cpp" "$fc\tests\test_flycore.cpp" -o "$fc\build-wasm-check\tests.js" -sALLOW_MEMORY_GROWTH=1 -sEXIT_RUNTIME=1 2>$null
& $node "$fc\build-wasm-check\tests.js" | Select-Object -Last 3

# 4) Amalgamation for Plugins/WebGL and Plugins/iOS
python "$fc\tools\amalgamate.py"
