# Project Hora development

- The user's source of truth is https://github.com/hddy774/project-hora.
- Feature releases increment the minor (middle) version: 1.1.0, 1.2.0, 1.3.0.
- Start requested feature work with a documented plan and a Draft PR. Implement and validate in that PR before merging and publishing the requested latest release.
- This is an offline C# Android observation simulation. Do not add network/API dependencies or player trading.
- Preserve the Android application ID and signing identity so installed games can upgrade.
- Never commit signing keys, passwords or credentials. Keep build outputs out of development/main branches. Release APKs belong in GitHub Releases; a temporary artifact upload branch may be used when direct upload is unavailable, then removed after verification.
- Keep financial ledger and matching invariants tested. Document measured validation and limitations accurately.
- Follow [the development standard](docs/guides/development-standard.md) for every future change. Keep Core, shared Client, and Android responsibilities and dependency direction explicit; do not link Android source into checks.
- Prefer feature folders, named navigation states and small functions with one responsibility. Remove duplicated calculations and unused layers; add abstractions only for demonstrated uses or execution/lifetime boundaries.
- Prioritize Android responsiveness: prepare/write checkpoints off the UI thread, preserve the asynchronous Activity save/load barrier, release the simulation gate between minutes, bound and invalidate display caches, and decode images for actual screen density. Never discard accepted time or ledger/history to improve throughput.
- Run `python3 scripts/check-architecture.py`, relevant regression checks, full game checks and Android Release/native validation before merging structural changes. Compare deterministic outcomes and fixed-input measurements for performance changes.

- Market AI is investor-only: 100 institutions and trend/news-following retail. Keep 15 security slots across five sectors; management, policy and finance change through seeded random events. Preserve order-book price discovery and funded ledger invariants. Do not reintroduce removed people, hiring, voting or business-project systems.
- State 10/SQLite 6 preserves and upgrades the v1.10 state-9 ledger; daily price bands, AI allocation policy and outcome counts require regression checks. The earlier state-9 boundary starts a new market; preserve prior 30-stock databases and WAL bytes and provide legacy archive export. Never silently reinterpret legacy portfolios.
