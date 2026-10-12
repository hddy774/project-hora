# Mobile / independent APK validation

Draft implementation for [PR #11](https://github.com/hddy774/project-hora/pull/11), checked on 2026-10-10. The PR's exact-head Actions checks are the source of truth for Android build/emulator status; no production release was run.

## Local results

- `python3 scripts/test_games.py`: 12 contract tests passed, covering independent IDs/projects/APK names/signing namespaces, path bounds, selected build/check arguments, missing toolchain configuration, and scoped/legacy tag syntax.
- `python3 scripts/test_native_capture.py`: 9 coordinate regressions passed for default, letterboxed, and downscaled emulator captures, including the recorded 480-height failure case.
- `python3 scripts/games.py validate`: the single registered ALPHA EXCHANGE manifest passed.
- `python3 scripts/games.py check alpha-exchange`: 41,247,119 assertions passed across 16 seasons and 5,058,975 matched trades, including accounting, matching, economy, storage/migrations/recovery, ownership, v1.5–v1.7, and the new mobile checks.
- Focused `--mobile-only`: 2,062 assertions passed. Tests execute the production scheduler, asynchronous checkpoint ordering, and modal geometry/scroll helpers. They cover 50 resume/background cycles, late/duplicate callbacks, delayed/failing checkpoint ordering, suspension during load/migration, and repeated modal identity/dismissal/clip/scroll cases at logical heights 480/600/800.
- Artwork: 200 distinct portrait regions, 140 women / 60 men, generated originals, bounds and SHA-256 checks passed.
- Actionlint 1.7.12, Python syntax checks, `bash -n build.sh`, and `git diff --check` passed.
- Final full run with .NET SDK 9.0.318: simulation wall time 93.59s, hour p50 6.56ms / p95 12.84ms / p99 19.46ms; serialized save 14,911,780 bytes. These are cloud CPU measurements, not phone performance. A first restore reported a read-only NuGet audit-cache warning; rerunning restore with a writable cache succeeded, and the final full run had no such warning.

## CI/native coverage added

The registry-driven Android job builds each game into its own directory. ALPHA EXCHANGE retains true v1.6.0 save-fixture migration, immutable saved-hour hashes, 200 profiles/portrait zoom, and native 100× checks.

The native script additionally exercises:

- Running → Home → settled checkpoint → 8-second background hold → return paused, comparing completed minutes, queued minutes, fractional progress, transaction ID, and run ID.
- Real help-modal touch/Canvas behavior at measured logical heights 480, 600, and 800: body movement, pinned close/CTA, both dismissal paths, fresh scroll position on reopen, and an unchanged underlying page. Screenshot crops are projected from logical input coordinates into the actual framebuffer; capture dimensions/projection metadata and raw JSON evidence are retained by CI. A test-only resize can recreate the Activity, so setup verifies and continues the same saved run, then pauses before comparing images. Emulator display settings are restored in a `finally` block.

These scenarios were added here; see the exact-head Actions outcome before treating native execution as passed. Pure helper checks do not by themselves prove Android runtime wiring.

## Deliberate limits

- No physical Android device testing, broad device/API matrix, forced Android disk-full/slow-I/O fault injection, process-kill durability guarantee, or TalkBack virtual-child accessibility claim.
- Lifecycle saving is best effort. An in-flight minute can finish, and a process kill can still discard work after the last committed checkpoint. Existing transactional recovery and save/ZIP formats are unchanged.
- New-market switching retains its synchronous save barrier; lifecycle suspension does not block on snapshot/disk completion. Import/export flows retain their existing storage semantics.
- App ID, namespaces, app version, financial core, SQLite schema and ZIP compatibility are unchanged. Retaining the app ID does not fix a discarded signing key or guarantee in-place APK upgrades.
- No new game, signing credential, tag, merge, or release was created. Published-release cleanup and durable signing-key setup are separate tasks.
- The new release dispatcher expects registry-era source tags. Accepting ALPHA EXCHANGE's legacy `vX.Y.0` syntax does not make frozen pre-registry tags rebuildable through the new dispatcher.
