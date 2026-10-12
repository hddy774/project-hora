# Mobile reliability and independent APK foundation

## Scope

- Keep ALPHA EXCHANGE fully offline and preserve its application ID, namespaces, save files, SQLite/ZIP formats, and existing source paths.
- Stop foreground scheduling when Android pauses; request a best-effort checkpoint without waiting for disk I/O on the lifecycle/UI thread.
- Make stock, trader, and help modal bodies scroll on short screens while keeping dismissal controls reachable.
- Register games independently under `games/<slug>/game.json`, with per-game checks, APK output directories, and release tag resolution. Only ALPHA EXCHANGE is registered in this change.
- Route CI through the registry. Preserve historical `v1.7.0` and `AlphaExchange-v1.7.0.apk` references. New game-scoped tags must not collide.
- Require an explicit release workflow invocation instead of releasing on main changes. This PR does not merge, release, provision signing credentials, or change signing identity.

## Validation plan

1. Run registry contract tests, artwork checks, core simulation/storage checks, and focused lifecycle/layout tests.
2. Compile the Android target in CI, retaining existing old-save/portrait/high-speed native smoke coverage.
3. Review short-height modal geometry, lifecycle repetition, failed/slow saves, and no background time catch-up.
4. Verify the exact pushed head and CI results; document stages unavailable in this environment.

## Delivery

Draft PR only. PC removal is a separate change. A production APK and on-device usability checks are not claimed by this implementation plan.

## Implementation checkpoint

The planned mobile and catalog changes are implemented. See [measured checks and limits](VALIDATION-mobile-multigame.md) and the draft PR's exact-head Actions status. The release workflow is manual-only and no app version bump or APK publication is part of this change. Existing release cleanup and durable signing-key preparation are separate user requests.
