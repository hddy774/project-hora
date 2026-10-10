# One-time retained Android signing setup

This is an opt-in owner-operated setup. Adding these files does not generate a key, change an existing key, grant access, or publish a release. The existing ALPHA EXCHANGE v1.1 certificate pin remains:

`9fd5aa86793e26beec38948dfae79d1981581f8e54abe0047025a863cc64f722`

A newly generated key is a different installation identity. Android cannot install it as an in-place update of an app signed with the old key. Uninstalling the old app can erase local saves. The owner must explicitly choose this transition before the recorded public pin is changed. An old private key cannot be reconstructed from an APK or public certificate.

## Owner steps from a mobile browser

1. Review and approve the setup pull request. It must be merged into `main` before the manual workflows appear. Merging does not execute bootstrap.
2. On GitHub, open Settings → Developer settings → Personal access tokens → Fine-grained tokens → Generate new token. Use resource owner `hddy774`, expiration **1 day**, repository access **Only select repositories → project-hora**, and repository permission **Secrets → Read and write**. Metadata read access is automatic. Leave all other optional permissions unset. Do not use a classic token or grant all repositories.
3. Copy the token directly into `project-hora` → Settings → Secrets and variables → Actions → New repository secret, named `SIGNING_BOOTSTRAP_TOKEN`. Never paste it into a chat, issue, workflow input, or repository file.
4. Do not edit any signing secrets while bootstrap runs. Open Actions → **Owner bootstrap Android signing identity** → Run workflow → branch `main`. Enter `CREATE_ALPHA_EXCHANGE_KEY` and personally press **Run workflow**. Do not enable debug logging. The confirmation means create one new ALPHA EXCHANGE signing identity and retain its key and password in this repository's Actions Secrets.
5. A successful run reports **only the public certificate SHA-256 fingerprint**. It creates `ANDROID_SIGNING_BUNDLE`; the signing key is never a release attachment. Revoke/delete the temporary PAT in your personal settings and remove the `SIGNING_BOOTSTRAP_TOKEN` repository secret. Deleting its secret alone does not revoke a PAT.
6. Explicitly approve the displayed public fingerprint as ALPHA EXCHANGE's new identity. Only then update `games/alpha-exchange/game.json` → `signing.certificateSha256` to that exact value in a reviewed change. This is public information, so it can be shared for review. Do not copy the private bundle anywhere.
7. After that change is merged, run **Verify retained Android signing identity** from `main`. This fresh run loads the retained secret, checks the actual certificate against the reviewed pin, and proves the private key works by creating and discarding a public certificate-signing request. It neither creates a key nor builds/publishes an APK.
8. Subsequent explicitly requested releases use the retained identity. Release tags must contain the new reader and reviewed pin. Old tags with the ephemeral signer are deliberately rejected. Updating from any release signed with this new identity can use the same retained key.

If the old four signing secrets already work, no bootstrap or migration is needed. The release reader still supports them and verifies the original public pin. A bundle and legacy signing values configured together are rejected as ambiguous.

## Existing secrets, failures, and retries

Bootstrap checks every existing ALPHA EXCHANGE signing-secret name and stops before key generation if any is present, including legacy aliases. It does not retrieve their values. It stops on API errors instead of interpreting access errors as absence. All bootstrap runs share one concurrency group, and rerunning a workflow attempt is rejected.

GitHub offers a **create-or-update** endpoint, not atomic create-only. Two cooperating bootstrap runs cannot race, but a simultaneous manual/API writer is outside that lock. Therefore do not edit signing secrets during initialization. HTTP 204 indicates an update collision and is reported as failure, never success; this detects the collision after GitHub's write and cannot undo it. There is no claim of atomic no-overwrite protection.

An ambiguous network failure after the single write may mean the bundle was saved. The workflow never retries that write, deletes a stored identity, or generates a replacement automatically. Check whether `ANDROID_SIGNING_BUNDLE` exists and the run's public status first. If it exists, preserve it. If a public fingerprint was not printed, a reviewed read-only recovery/verification change can export that public certificate without exposing or replacing the private key. Do not delete/recreate the bundle as troubleshooting.

REST secret reads reveal only metadata. A successful create plus metadata check proves the entry exists, not that a later workflow can decrypt and use it; the fresh verification run provides that evidence.

## Security boundaries

- Bootstrap is owner-only, fresh manual dispatch, `main` only, and repository-fixed. Its `GITHUB_TOKEN` permissions are empty. The separate one-time PAT has only repository Secrets write access.
- The initializer has no checkout, build, downloaded action, dependency installation, cache, artifact upload, or chained release. It uses the GitHub-hosted Ubuntu image's JDK, Python and GitHub CLI.
- Passwords and encoded keystore material are masked immediately. The bundle is passed to GitHub CLI through stdin for local encryption, then only encrypted data is submitted to the Secrets API. No private key material, password, or token is placed in a command argument, step output, summary, artifact, or release asset. The alias is an identifier passed to keytool, not private key material.
- Temporary private files are permission-restricted and removed on normal completion/failure. A forced runner termination relies on disposal of the hosted runner; this is not a secure-erasure guarantee.
- Only the public certificate fingerprint and allowlisted signing metadata are published. Base64 is an encoding, not encryption.
- A private repository is not a key vault for release attachments. All repository writers must be trusted during the temporary grant window: another modified workflow can use the repository-level `SIGNING_BOOTSTRAP_TOKEN` and its Secrets-write permission, despite this workflow's owner/main guard. Use a protected main-only environment with owner approval instead if those writers are not trusted; that is a separate setup decision, not claimed protection here. Anyone who can run or alter privileged workflows may also be able to use retained signing secrets.
- The release reader compares the actual certificate and APK signer set to the separately reviewed registry pin. The bundle's own fingerprint cannot establish trust by itself.
- Recovery requires a completed manual run in this repository with the same reviewed workflow revision and matching source, application, version, and exact signer. An artifact cannot select its own trusted certificate.

## Local validation

Run `python3 scripts/test_android_signing.py` and `python3 scripts/test_games.py`. Signing tests use conspicuous, non-secret byte fixtures and mocks for every keytool, cryptographic-randomness, subprocess, and HTTP operation. They generate no real keys, read no tokens, and make no network calls.

Live bootstrap and retained-key validation remain intentionally unexecuted until the owner performs the handoff. No test result here claims a real keystore was created or stored.

## Official references

- [Secrets API and required permissions](https://docs.github.com/en/rest/actions/secrets#create-or-update-a-repository-secret)
- [GITHUB_TOKEN permission list](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#permissions)
- [Fine-grained token setup](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens)
- [GitHub CLI secret encryption and stdin](https://cli.github.com/manual/gh_secret_set)
- [Manually running a workflow](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow)
- [Android signing identity and updates](https://developer.android.com/studio/publish/app-signing)
