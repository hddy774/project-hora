#!/usr/bin/env python3
"""Check the experimental APK identity, ABI, portrait mode and offline policy."""
import json
import hashlib
import os
import pathlib
import re
import subprocess
import sys
import zipfile

apk = pathlib.Path(sys.argv[1]).resolve()
sdk = pathlib.Path(os.environ.get("ANDROID_HOME", os.environ.get("ANDROID_SDK_ROOT", "")))
tool_dirs = sorted((sdk / "build-tools").glob("*"), reverse=True)
tools = next((p for p in tool_dirs if (p / "aapt").is_file() and (p / "apksigner").is_file()), None)
if tools is None:
    sys.exit("Android build-tools containing aapt and apksigner are required.")
def run(*args):
    return subprocess.check_output([str(x) for x in args], text=True)
badging = run(tools / "aapt", "dump", "badging", apk)
permissions = run(tools / "aapt", "dump", "permissions", apk)
manifest = run(tools / "aapt", "dump", "xmltree", apk, "AndroidManifest.xml")
assert "package: name='org.hora.monthly.experimental'" in badging, "Wrong app identity"
assert "versionName='0.1.0-experimental'" in badging, "Wrong experimental version"
assert "android.permission.INTERNET" not in permissions, "Internet permission is forbidden"
assert "android:screenOrientation" in manifest, "Missing orientation"
assert "android:screenOrientation(0x0101001e)=(type 0x10)0x1" in manifest, "Expected portrait orientation"
with zipfile.ZipFile(apk) as z:
    abis = sorted({name.split('/')[1] for name in z.namelist() if name.startswith('lib/') and name.endswith('.so')})
    packaged_assets = {}
    project_dir = pathlib.Path(__file__).resolve().parents[1]
    for asset in ["data/experimental.json", "assets/GODOT-NOTICES.txt", "assets/FONT-LICENSE.txt"]:
        packed = z.read("assets/" + asset)
        assert packed == (project_dir / asset).read_bytes(), "Packaged asset differs: " + asset
        packaged_assets[asset] = hashlib.sha256(packed).hexdigest()
assert abis == ["arm64-v8a", "x86_64"], "Unexpected ABIs"
verification = run(tools / "apksigner", "verify", "--verbose", "--print-certs", apk)
certificate = next(line.split(": ", 1)[1] for line in verification.splitlines() if line.startswith("Signer #1 certificate SHA-256 digest:"))
report = {"application_id": "org.hora.monthly.experimental", "version": "0.1.0-experimental", "signing": "Godot local debug key; experimental only", "certificate_sha256": certificate, "abis": abis, "min_sdk": int(re.search(r"sdkVersion:'(\d+)'", badging).group(1)), "target_sdk": int(re.search(r"targetSdkVersion:'(\d+)'", badging).group(1)), "internet_permission": False, "orientation": "portrait", "packaged_asset_sha256": packaged_assets, "device_execution_tested": False}
(apk.parent / "APK-VERIFICATION.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
print("Experimental APK manifest, offline permissions, ABIs, signature and packaged fixture/notices verified.")
