# Pinned versions: That Damn Fly

| Component | Version | Source | Date |
|---|---|---|---|
| Unity Editor | 6000.3.24f1 (6.3 LTS) | Unity Hub 3.21.2 (winget, official MSIX) | Sept 13, 2026 |
| Unity modules | Android Build Support (SDK Platforms 34–37, Build Tools, NDK **r27c 27.2.12479018**, OpenJDK), Web Build Support (Emscripten **3.1.39**), iOS Build Support, Windows IL2CPP | Hub headless install | Sept 13, 2026 |
| MSVC | 14.44.35207 (VS Build Tools 2022 17.14) | winget Microsoft.VisualStudio.2022.BuildTools | Sept 13, 2026 |
| CMake | 4.4.3 (winget, user scope) | Kitware | Sept 13, 2026 |
| uv / Python | 0.12.13 / 3.12.14 | winget astral-sh.uv | Sept 13, 2026 |
| Git / Git LFS | 2.55.0 / 3.7.1 | pre-existing | - |
| Node (host) | 24.19.0 | pre-existing | - |
| FlyCore ABI | 1 · `.tdfm` format 1 | own | - |
| Neural model | `tdf-escape-v0.1-p2` · SHA-256 `6ed1086387d81c9ce4508a05768f359c126a6271393b943f9ec941def494b7e8` · 452,132 bytes | research/models | Sept 13, 2026 |
| Android | minSdk 29 · target 36 · arm64-v8a · 16 KB pages verified in the `.so` | - | - |
| iOS | deployment target 15 (to be fixed in the project) · Xcode 26 required for the archive | - | - |
| App | 1.2.4 (`app/Assets/Editor/ProjectSetup.cs`) · build numbers per platform from 1 | - | - |

Rule: do not update the toolchain during a sprint without a demonstrated need; record any change here with date and reason.
