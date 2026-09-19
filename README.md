# That Damn Fly

**One fly. Zero patience.** A game of domestic revenge for phones and the web - against a fly whose escape is decided by a circuit of **816 neurons** extracted from a real brain (the MaleCNS connectome of a fruit fly).

- Play: **https://thatdamnfly.com** (free, no ads, no accounts; game in PT/EN, site in PT/EN/ES)
- Science: https://thatdamnfly.com/ciencia/ · causal-protocol report in `docs/reports/`
- Open source: code under **MIT**, content under **CC BY 4.0** (see [Licenses](#licenses))

## What it is
The fly sees the object coming down and flees, because a spiking neural model (FlyCore, C++) runs in real time with a circuit selected from MaleCNS v1.0 - the visual projection neurons LC4/LPLC2/LPLC1/LPLC4 and the descending neurons DNp01 ("Giant Fiber"), DNp02/03/04/06/11. A sensory encoder turns approaching objects into looming signals; the output of the descending neurons drives a virtual body. The fly never receives the position of your finger. A pre-registered protocol (12,000 trials, five conditions) verified that the circuit really is what decides.

On top of that sits a game: seven scenes ("the Day"), objects you drag into a first-person hand, fury, combos, bait, a fly of the day, a collection, and a bonus round where you keep the flies off a character's face.

## Credits
A project by **Miguel Prego**. Functional, technical and design structure and scientific analysis: GPT‑6 Astra. Implementation: Claude Opus 5 (Claude Code).

## Layout
| Folder | What it is |
|---|---|
| `app/` | Unity 6000.3.24f1 project (2D URP, IL2CPP): `Game.Domain` (rules, testable without Unity), `Game.Simulation` (body, sensory encoder, FlyCore binding, replay/save), `Game.Presentation` (screens, HUD, arena), `Tests.Domain` (xunit) |
| `flycore/` | Neural simulator in C++ (CMake): Windows, Android, WebAssembly; doctest tests |
| `research/` | Circuit extraction, `.tdfm` package export, causal protocol, Python mirror of the body (`tdf/body.py`) and parity fixtures |
| `art/`, `tools/art` | SVG sources and generators (Node + sharp) → PNG in `app/Assets/Resources/art` |
| `tools/audio` | Procedural sounds and music (NumPy) → `app/Assets/Resources/audio` |
| `web/` | Public pages, static neural model, headers; `tools/deploy-*` to publish |
| `docs/` | Body and sensory models, causal protocol, trial reports, pinned versions |

## Build
1. **FlyCore**: `cmake -S flycore -B flycore/build-win64 && cmake --build flycore/build-win64` (tests: `flycore_tests`).
2. **Research** (optional): `uv sync` in `research/`; the MaleCNS data is downloaded with `research/data/raw/download.sh` (it is not redistributed here).
3. **Game**: Unity 6000.3.24f1 with the Web/Android modules; `tools/unity-build.ps1 web -Dev 1` produces `dist/web-dev`; serve it with `node web/serve.mjs dist/web-dev 8080`.
4. **Tests**: `pwsh tools/run-domain-tests.ps1`; headless check of a build: `node tools/headless-check.mjs "http://localhost:8080/?ui=m" 25 play shots`.

## Licenses
- Code: [MIT](LICENSE), © 2026 Miguel Prego.
- Art, sounds, music and texts: [CC BY 4.0](LICENSE-ASSETS.md). The name "That Damn Fly" and the logo are not covered.
- Neural package: a derivative work of **MaleCNS v1.0** (FlyEM / HHMI Janelia Research Campus and collaborators), CC BY 4.0 - credits in `web/model/*/CREDITS.md`. The data authors do not sponsor and have not validated this game.
- Full inventory: `licenses/INVENTORY.md`.

Contributions: see `CONTRIBUTING.md`. Contact: support@thatdamnfly.com

---
*Em português:* jogo de código aberto de Miguel Prego. A documentação está em inglês; os textos do jogo e do site existem em português.
