# Contributing: That Damn Fly

Thanks for wanting to help. Short rules:

1. **The science is not touched without a trial.** The circuit (`research/`, the `.tdfm` package) and the sensory encoder only change with a pre-registered protocol and a report in `docs/reports/`. Body, rules and presentation can change freely, with tests.
2. **Tests before a pull request**: `pwsh tools/run-domain-tests.ps1` (rules domain + Python↔C# body parity), `flycore/build-win64` (C++ tests), and the headless check `node tools/headless-check.mjs` on a development build.
3. **Licenses**: nothing enters without a line in `licenses/INVENTORY.md`. Code MIT; content CC BY 4.0; no GPL in the runtime.
4. **No secrets in the repository** (keys, passwords, `.env.deploy`).
5. Write in English: documentation, code comments, commit messages. The game's player-facing strings live in `app/Assets/Resources/content/strings.*.json` (PT-PT and EN).

Open an issue before a large change; for small fixes, a direct pull request is enough.
