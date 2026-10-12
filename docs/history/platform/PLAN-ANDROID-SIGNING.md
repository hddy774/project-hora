# Retained Android signing preparation

## Scope

Prepare an owner-operated setup and a verified retained-key release reader. Depend on the mobile/multigame foundation in PR #11. Preserve the Android application ID and the currently accepted v1.1 public certificate pin. Do not generate a key, register credentials, run bootstrap, migrate the pin, merge, or publish a release as part of this preparation.

## Implementation

- [x] Manual-only, owner/main-gated initializer with no checkout, dependencies, artifacts, or automatic release.
- [x] One encrypted repository signing-bundle write, fail-closed existing-secret checks, no automatic retries.
- [x] Retained-bundle and legacy reader with certificate pin and exact APK signer verification.
- [x] Remove discarded-key generation from release workflow.
- [x] Fresh read-only verification that proves private-key usability.
- [x] Gate artifact recovery on repository, workflow revision, source, application/version, and reviewed public certificate.
- [x] Document mobile setup, temporary grant revocation, independent fingerprint approval, and upsert race limits.
- [x] Offline fixture-only tests and syntax review.
- [ ] Owner reviews and chooses whether to merge setup.
- [ ] Owner personally creates/registers temporary scoped grant and launches bootstrap.
- [ ] Owner approves an actual public fingerprint; separate reviewed pin change.
- [ ] Owner initiates fresh retained-key validation; only then consider an explicitly requested release.

## Validation

No production credentials or key material are available to local tests. Mocked tests cover existing identity rejection, second-check race, changed repository encryption key, API-read failure, ambiguous writes without retry, upsert status rejection, owner/ref/rerun/debug guards, strict bundle parsing, certificate mismatch, exact signer set, private-key usability, credential isolation, and cleanup. Existing catalog and mobile-capture contracts are also checked.

Real key generation, GitHub secret persistence, APK signing with a retained production key, and the owner handoff are intentionally not executed. GitHub CI remains a separate source of evidence for the final commit.
