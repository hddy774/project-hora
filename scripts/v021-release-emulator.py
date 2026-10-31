#!/usr/bin/env python3
"""Actual disposable-emulator fresh install/render and retained-signature upgrade.

No signing material is used. Only the CI emulator's generated legacy world.sav
is hashed for preservation; it is not exported or claimed to be migrated.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys
import time

spec = importlib.util.spec_from_file_location("v021_release", Path(__file__).with_name("verify-v021-release.py"))
verify = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verify)
PACKAGE = verify.PACKAGE

readiness_spec = importlib.util.spec_from_file_location("android_readiness", Path(__file__).with_name("android_readiness.py"))
readiness = importlib.util.module_from_spec(readiness_spec)
readiness_spec.loader.exec_module(readiness)


def adb(*args, binary=False, optional=False, timeout=45):
    result = subprocess.run(["adb", *map(str, args)], capture_output=True, timeout=timeout)
    if not optional:
        verify.require(result.returncode == 0, "Actual adb operation failed: " + str(args[0]))
    return result.stdout if binary else result.stdout.decode("utf-8", errors="replace")


def package_state():
    text = adb("shell", "dumpsys", "package", PACKAGE)
    uid = re.search(r"\buserId=(\d+)", text)
    first = re.search(r"firstInstallTime=([^\r\n]+)", text)
    verify.require(uid is not None and first is not None, "Installed package identity/timestamp missing")
    return {"uid": uid[1], "first_install_time": first[1].strip()}


def launch(activity, transcript=None):
    adb("logcat", "-c")
    result = subprocess.run(["adb", "shell", "am", "start", "-W", "-n", PACKAGE + "/" + activity],
                            capture_output=True, text=True, timeout=45)
    output = result.stdout + result.stderr
    if transcript is not None:
        Path(transcript).write_text(output, encoding="utf-8")
    verify.require(result.returncode == 0 and "Status: ok" in output and "Error:" not in output and "Exception" not in output,
                   "Android launch did not succeed")
    return output


def actual_render(apk, proof, out):
    out.mkdir(parents=True, exist_ok=True)
    verify.require(verify.sha(apk) == proof["sha256"], "APK bytes changed before installation")
    return readiness.await_ready(proof["launchable_activity"], proof["version"], out)


def old_world_proof(activity):
    # The root operation is strictly limited to a disposable GitHub Actions emulator.
    verify.require(adb("shell", "getprop", "ro.kernel.qemu").strip() == "1", "Legacy preservation check requires a disposable emulator")
    adb("root"); adb("wait-for-device")
    launch(activity)
    time.sleep(12)
    adb("shell", "input", "keyevent", "KEYCODE_HOME")
    deadline = time.monotonic() + 90
    prefix = "/data/user/0/" + PACKAGE + "/"
    while time.monotonic() < deadline:
        paths = adb("shell", "find", prefix, "-type", "f", "-name", "world.sav", optional=True).splitlines()
        for path in paths:
            path = path.strip()
            if path.startswith(prefix) and re.fullmatch(r"[A-Za-z0-9_./-]+/world\.sav", path):
                blob = adb("exec-out", "cat", path, binary=True)
                verify.require(0 < len(blob) <= 64 * 1024 * 1024, "Legacy generated world file size is invalid")
                return {"path": path, "sha256": hashlib.sha256(blob).hexdigest(), "bytes": len(blob)}
        launch(activity); time.sleep(3); adb("shell", "input", "keyevent", "KEYCODE_HOME"); time.sleep(1)
    raise ValueError("Previous public APK did not create its actual legacy world.sav")


def retained_upgrade(apk, current, previous, out):
    old = verify.inspect_apk(previous, "previous")
    adb("shell", "am", "force-stop", PACKAGE)
    adb("uninstall", PACKAGE)
    adb("install", "--no-streaming", previous)
    before = package_state()
    saved = old_world_proof(old["launchable_activity"])
    adb("shell", "am", "force-stop", PACKAGE)
    verify.require(verify.sha(apk) == current["sha256"], "APK changed before actual upgrade")
    adb("install", "-r", "--no-streaming", apk)
    verify.require(package_state() == before, "In-place update did not retain the existing package UID/install identity")
    preserved = hashlib.sha256(adb("exec-out", "cat", saved["path"], binary=True)).hexdigest()
    verify.require(preserved == saved["sha256"], "In-place upgrade changed the legacy saved world")
    render = actual_render(apk, current, out)
    adb("shell", "input", "keyevent", "KEYCODE_HOME"); time.sleep(2)
    after = hashlib.sha256(adb("exec-out", "cat", saved["path"], binary=True)).hexdigest()
    verify.require(after == saved["sha256"], "New application launch/save changed the preserved legacy source")
    return {"passed": True, "previous_apk_sha256": old["sha256"], "previous_version": old["version"],
            "retained_certificate_sha256": verify.CERTIFICATE, "uid_and_first_install_retained": True,
            "legacy_save_preserved": True, "legacy_save": saved, "legacy_save_migration_claim": False, "render": render}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apk", required=True); parser.add_argument("--source", required=True)
    parser.add_argument("--signed-proof", required=True); parser.add_argument("--api", type=int, choices=(24, 35), required=True)
    parser.add_argument("--previous"); parser.add_argument("--output", required=True)
    args = parser.parse_args()
    verify.owner_context(); source = verify.check_source(verify.read(args.source)); proof = verify.read(args.signed_proof)
    verify.proof_matches(source, proof)
    verify.require(proof.get("kind") == "signed" and proof.get("certificate_sha256") == verify.CERTIFICATE and verify.sha(args.apk) == proof["sha256"],
                   "Exact retained-signed source APK required")
    verify.require(adb("shell", "getprop", "ro.kernel.qemu").strip() == "1" and
                   adb("shell", "getprop", "ro.build.version.sdk").strip() == str(args.api), "Wrong actual Android emulator API")
    out = Path(args.output); out.mkdir(parents=True, exist_ok=True)
    adb("uninstall", PACKAGE, optional=True)
    adb("install", "--no-streaming", args.apk)
    fresh = actual_render(args.apk, proof, out / "fresh")
    upgrade = retained_upgrade(args.apk, proof, args.previous, out / "upgrade") if args.previous else None
    verify.write(out / "android-proof.json", {"schema_version": 1, "passed": True, "api": args.api,
        "signed_apk_sha256": proof["sha256"], "fresh_install_render": fresh["passed"], "fresh": fresh,
        "upgrade_requested": args.previous is not None, "retained_signature_upgrade": upgrade is not None and upgrade["passed"],
        "legacy_save_preserved": upgrade is not None and upgrade["legacy_save_preserved"], "upgrade": upgrade,
        **{k: source[k] for k in ("source_commit", "product_source_fingerprint", "run_id", "run_attempt")}})


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, subprocess.TimeoutExpired) as error:
        print("Actual Android release verification failed: " + str(error), file=sys.stderr)
        sys.exit(1)
