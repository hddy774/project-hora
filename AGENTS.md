# Project Hora development

- The user's source of truth is https://github.com/hddy774/project-hora.
- Feature releases increment the minor (middle) version: 1.1.0, 1.2.0, 1.3.0.
- Start requested feature work with a documented plan and a Draft PR. Implement and validate in that PR before merging and publishing the requested latest release.
- This is an offline C# Android observation simulation. Do not add network/API dependencies or player trading.
- Preserve the Android application ID and signing identity so installed games can upgrade.
- Never commit signing keys, passwords or credentials. Keep build outputs out of development/main branches. Release APKs belong in GitHub Releases; a temporary artifact upload branch may be used when direct upload is unavailable, then removed after verification.
- Keep financial ledger and matching invariants tested. Document measured validation and limitations accurately.
