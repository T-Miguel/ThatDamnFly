# app/: Unity 6.3 LTS project (6000.3.24f1)

Note: the package versions in `Packages/manifest.json` are a starting point; the editor resolves the compatible versions on the first launch and writes `packages-lock.json`, which then becomes the pinned reference. Record changes in `docs/VERSIONS.md`.

Assemblies: `Game.Domain` (pure C#, testable outside Unity: `tools/run-domain-tests.ps1`), `Game.Simulation`, `Game.Presentation`, `Game.Persistence`, `Game.Content`, `Game.Platform`. FlyCore lives in `Assets/Plugins/{x86_64,Android,iOS,WebGL}`.

Builds: `tools/unity-build.ps1 web|android -Dev 1|0 -Out <dir>` (batchmode; needs an activated Unity license).
