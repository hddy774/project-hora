#!/usr/bin/env python3
"""Use an existing, publicly pinned Android identity. Never generate a key."""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile

HEX256 = re.compile(r"[a-f0-9]{64}")
LEGACY_FIELDS = ("KEYSTORE_BASE64", "STORE_PASSWORD", "KEY_ALIAS", "KEY_PASSWORD")
BUNDLE_FIELDS = {"schemaVersion", "game", "applicationId", "keystoreBase64", "storePassword", "keyAlias", "keyPassword", "certificateSha256"}


def mask(value):
    if os.environ.get("GITHUB_ACTIONS") == "true":
        escaped = value.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")
        print("::add-mask::" + escaped, flush=True)


def unique_object(pairs):
    result = {}
    for name, value in pairs:
        if name in result:
            raise ValueError("Duplicate signing bundle field")
        result[name] = value
    return result


def load_identity(env):
    if env.get("GENERATE_SIGNING_KEY", "false") != "false":
        raise ValueError("Only the owner-run bootstrap may create a new identity")
    pin = env.get("EXPECTED_SIGNING_CERTIFICATE", "")
    if not HEX256.fullmatch(pin):
        raise ValueError("A reviewed public SHA-256 certificate pin is required")
    raw = env.get("SIGNING_BUNDLE", "")
    if raw:
        if any(env.get(name) for name in LEGACY_FIELDS):
            raise ValueError("Bundled and legacy signing identities are both configured")
        if len(raw.encode("utf-8")) > 48000:
            raise ValueError("Signing bundle is too large")
        mask(raw)
        bundle = json.loads(raw, object_pairs_hook=unique_object)
        if not isinstance(bundle, dict) or set(bundle) != BUNDLE_FIELDS or type(bundle["schemaVersion"]) is not int or bundle["schemaVersion"] != 1:
            raise ValueError("Unsupported signing bundle schema")
        if bundle["game"] != env.get("GAME") or bundle["applicationId"] != env.get("APP_ID"):
            raise ValueError("Signing bundle belongs to a different game or application")
        if bundle["certificateSha256"] != pin:
            raise ValueError("Stored identity differs from reviewed public certificate pin")
        identity = {"KEYSTORE_BASE64": bundle["keystoreBase64"], "STORE_PASSWORD": bundle["storePassword"],
                    "KEY_ALIAS": bundle["keyAlias"], "KEY_PASSWORD": bundle["keyPassword"]}
        mode = "Retained per-game signing bundle"
    else:
        identity = {name: env.get(name, "") for name in LEGACY_FIELDS}
        mode = "Existing retained signing configuration"
    for name, value in identity.items():
        if not isinstance(value, str) or not value or len(value) > 44000 or "\x00" in value:
            raise ValueError("Missing or invalid signing field: " + name)
        mask(value)
    data = base64.b64decode("".join(identity.pop("KEYSTORE_BASE64").split()), validate=True)
    if not 1 <= len(data) <= 32768:
        raise ValueError("Invalid keystore size")
    return data, identity, pin, mode


def quiet(command, env):
    # Never replay output from a process that received credentials, even on failure.
    result = subprocess.run(command, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    if result.returncode:
        raise ValueError("Signing/certificate command failed")
    return result.stdout


def verified_signer(output, pin):
    lines = output.decode("utf-8", errors="strict").splitlines()
    signers = [line for line in lines if re.fullmatch(r"Signer #[0-9]+ certificate SHA-256 digest: .*", line)]
    return signers == ["Signer #1 certificate SHA-256 digest: " + pin]


def operate(args, env):
    data, identity, pin, mode = load_identity(env)
    runner_temp = env.get("RUNNER_TEMP")
    if not runner_temp or not Path(runner_temp).is_dir():
        raise ValueError("RUNNER_TEMP is required")
    os.umask(0o077)
    child_env = dict(env, **identity)
    for name in ("SIGNING_BUNDLE", "KEYSTORE_BASE64", "GH_TOKEN", "GITHUB_TOKEN", "GH_DEBUG"):
        child_env.pop(name, None)
    with tempfile.TemporaryDirectory(prefix="hora-signing-", dir=runner_temp) as temporary:
        keystore, certificate = Path(temporary) / "identity.p12", Path(temporary) / "public.der"
        keystore.write_bytes(data)
        quiet(["keytool", "-exportcert", "-keystore", str(keystore), "-alias", identity["KEY_ALIAS"],
               "-storepass:env", "STORE_PASSWORD", "-file", str(certificate)], child_env)
        actual = hashlib.sha256(certificate.read_bytes()).hexdigest()
        if actual != pin:
            raise ValueError("Actual signing certificate differs from reviewed pin")
        if args.verify_only:
            # Exporting a certificate alone does not prove private-key usability.
            # A CSR signs public data using the retained private key; it creates no key.
            quiet(["keytool", "-certreq", "-keystore", str(keystore), "-alias", identity["KEY_ALIAS"],
                   "-storepass:env", "STORE_PASSWORD", "-keypass:env", "KEY_PASSWORD"], child_env)
            print("Retained signing identity verified. Certificate SHA-256: " + actual)
            return
        if not all((args.unsigned_apk, args.signed_apk, args.signing_info, env.get("ANDROID_HOME"))):
            raise ValueError("APK paths and ANDROID_HOME are required")
        signer = str(Path(env["ANDROID_HOME"]) / "build-tools/35.0.0/apksigner")
        output, temporary_apk = Path(args.signed_apk), Path(temporary) / "signed.apk"
        quiet([signer, "sign", "--ks", str(keystore), "--ks-key-alias", identity["KEY_ALIAS"],
               "--ks-pass", "env:STORE_PASSWORD", "--key-pass", "env:KEY_PASSWORD",
               "--out", str(temporary_apk), args.unsigned_apk], child_env)
        verification = quiet([signer, "verify", "--verbose", "--print-certs", str(temporary_apk)], child_env)
        if not verified_signer(verification, pin):
            raise ValueError("APK signer set does not match the reviewed identity")
        source = subprocess.run(["git", "rev-parse", "HEAD"], capture_output=True, text=True, check=True).stdout.strip()
        public = ["Signing mode: " + mode, "Certificate SHA-256: " + actual,
                  "Source commit: " + source, "Workflow commit: " + env.get("GITHUB_SHA", "local-verification"),
                  "Workflow run: " + env.get("GITHUB_RUN_ID", "local-verification")]
        Path(args.signing_info).write_text("\n".join(public) + "\n", encoding="utf-8")
        # Artifact text is an allowlisted public summary, not arbitrary tool output.
        (output.parent / "APK-VERIFICATION.txt").write_text("APK signature verified\nSigner #1 certificate SHA-256 digest: " + actual + "\n", encoding="utf-8")
        output.write_bytes(temporary_apk.read_bytes())
        print("APK verified. Certificate SHA-256: " + actual)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("unsigned_apk", "signed_apk", "signing_info"):
        parser.add_argument(name, nargs="?")
    parser.add_argument("--verify-only", action="store_true")
    try:
        operate(parser.parse_args(), os.environ)
    except (ValueError, OSError, subprocess.CalledProcessError, UnicodeError, TypeError):
        print("::error::Signing identity validation failed. Check configuration and the reviewed public certificate pin; no new key was generated.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
