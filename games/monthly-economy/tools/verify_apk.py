#!/usr/bin/env python3
"""Verify a release-mode Hora APK; unsigned CI inputs are never called signed."""
import argparse
import hashlib
import json
import os
import pathlib
import re
import struct
import subprocess
import sys
import zipfile

parser = argparse.ArgumentParser()
parser.add_argument('apk', type=pathlib.Path)
parser.add_argument('--unsigned', action='store_true')
parser.add_argument('--source-proof', type=pathlib.Path, help='Verified source-run report for imported texture byte provenance')
parser.add_argument('--expected-certificate', default='0832f24441f1caf335166fbc16ae0b9536e262ca48f258926a8027fc31dca25a')
args = parser.parse_args()
apk = args.apk.resolve()
source_proof = json.loads(args.source_proof.read_text()) if args.source_proof else None
project_dir = pathlib.Path(__file__).resolve().parents[1]
sdk = pathlib.Path(os.environ.get('ANDROID_HOME', os.environ.get('ANDROID_SDK_ROOT', '')))
tools = sdk / 'build-tools' / '36.0.0'
if not (tools / 'aapt').is_file() or not (tools / 'apksigner').is_file():
    sys.exit("Android build-tools 36.0.0 is required; install with sdkmanager 'build-tools;36.0.0'.")
def run(*arguments):
    return subprocess.check_output([str(x) for x in arguments], text=True)
def digest(data):
    return hashlib.sha256(data).hexdigest()
badging = run(tools/'aapt', 'dump', 'badging', apk)
permissions = run(tools/'aapt', 'dump', 'permissions', apk)
manifest = run(tools/'aapt', 'dump', 'xmltree', apk, 'AndroidManifest.xml')
assert "package: name='org.hora.economiclab'" in badging, 'Wrong application identity'
assert "versionName='0.2.0'" in badging and "versionCode='2'" in badging, 'Wrong version'
assert 'android.permission.INTERNET' not in permissions, 'Offline game must not request Internet access'
assert 'android:screenOrientation(0x0101001e)=(type 0x10)0x1' in manifest, 'Expected portrait orientation'
assert 'android:debuggable(0x0101000f)=(type 0x12)0xffffffff' not in manifest, 'Release APK must not be debuggable'
assert "sdkVersion:'24'" in badging and "targetSdkVersion:'36'" in badging, 'Unexpected SDK compatibility range'
raw_assets = [str(p.relative_to(project_dir)).replace(os.sep, '/') for p in sorted((project_dir/'data').glob('*.json'))]
raw_assets += ['assets/generated/ui_assets.json', 'assets/generated/provenance.json', 'assets/generated/app_icon.provenance.json', 'assets/map/atlas.json', 'assets/GODOT-NOTICES.txt', 'assets/FONT-LICENSE.txt']
source_images = [str(p.relative_to(project_dir)).replace(os.sep, '/') for folder in ('assets/generated', 'assets/map') for p in sorted((project_dir/folder).glob('*.png'))]
with zipfile.ZipFile(apk) as archive:
    names = archive.namelist()
    assert len(names) == len(set(names)), 'Duplicate APK entries'
    abis = sorted({n.split('/')[1] for n in names if n.startswith('lib/') and n.endswith('.so')})
    assert abis == ['arm64-v8a', 'x86_64'], 'Unexpected ABIs'
    packaged_assets = {}
    for asset in raw_assets:
        packed = archive.read('assets/'+asset)
        assert packed == (project_dir/asset).read_bytes(), 'Packaged definition/license differs: '+asset
        packaged_assets[asset] = digest(packed)
    generated_textures = {}
    for image in source_images:
        raw_name = 'assets/'+image
        source_digest = digest((project_dir/image).read_bytes())
        if raw_name in names:
            packed = archive.read(raw_name)
            assert packed == (project_dir/image).read_bytes(), 'Generated image differs: '+image
            record = {'packaged_path': image, 'sha256': digest(packed), 'representation': 'source_png', 'source_png_sha256': source_digest}
        else:
            mapping = next((name for name in (raw_name+'.import', raw_name+'.remap') if name in names), None)
            assert mapping, 'Generated graphic not packaged: '+image
            remap = archive.read(mapping).decode().rstrip('\x00')
            target = re.search(r'(?m)^path="res://([^\"]+)"$', remap)
            assert target, 'Missing imported image mapping: '+image
            packed = archive.read('assets/'+target.group(1))
            assert len(packed) > 32, 'Empty imported graphic: '+image
            imported_source = project_dir/target.group(1)
            record = {'packaged_path': target.group(1), 'sha256': digest(packed), 'representation': 'godot_imported_texture', 'source_png_sha256': source_digest}
            if imported_source.is_file():
                assert imported_source.read_bytes() == packed, 'Packaged import differs from source import: '+image
            else:
                assert source_proof and source_proof['generated_texture_sha256'].get(image) == record, 'Missing verified source texture proof: '+image
        generated_textures[image] = record
    if source_proof:
        assert source_proof['generated_texture_sha256'] == generated_textures, 'Generated texture provenance changed'
        assert source_proof['packaged_asset_sha256'] == packaged_assets, 'Packaged source definition provenance changed'
    signature_files = [n for n in names if n.startswith('META-INF/') and n.endswith(('.SF', '.RSA', '.DSA', '.EC'))]
certificate = None
if args.unsigned:
    data = apk.read_bytes()
    end = data.rfind(b'PK\x05\x06', max(0, len(data)-65557))
    assert end >= 0, 'APK ZIP directory missing'
    offset = struct.unpack_from('<I', data, end+16)[0]
    assert not signature_files and data[max(0, offset-16):offset] != b'APK Sig Block 42', 'Expected an unsigned release input'
    check = subprocess.run([str(tools/'apksigner'), 'verify', str(apk)], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    assert check.returncode != 0, 'Unsigned input unexpectedly has a valid signature'
else:
    verification = run(tools/'apksigner', 'verify', '--verbose', '--print-certs', apk)
    assert 'Verifies' in verification.splitlines(), 'Signature verification failed'
    assert re.findall(r'(?m)^Number of signers:[ \t]*(\d+)[ \t]*$', verification) == ['1'], 'Expected one publisher signer'
    certs = re.findall(r'(?m)^Signer #1 certificate SHA-256 digest:[ \t]*([a-fA-F0-9]{64})[ \t]*$', verification)
    assert len(certs) == 1, 'Missing publisher certificate digest'
    certificate = certs[0].lower()
    assert certificate == args.expected_certificate, 'Publisher certificate differs from the pinned identity'
report = {'application_id': 'org.hora.economiclab', 'version': '0.2.0', 'version_code': 2,
          'signing_status': 'unsigned_release_input' if args.unsigned else 'retained_publisher_verified',
          'certificate_sha256': certificate, 'verification_build_tools': '36.0.0', 'abis': abis,
          'min_sdk': int(re.search(r"sdkVersion:'(\d+)'", badging).group(1)),
          'target_sdk': int(re.search(r"targetSdkVersion:'(\d+)'", badging).group(1)),
          'internet_permission': False, 'debuggable': False, 'orientation': 'portrait',
          'packaged_asset_sha256': packaged_assets, 'generated_texture_sha256': generated_textures,
          'device_execution_tested': False, 'apk_sha256': hashlib.file_digest(apk.open('rb'), 'sha256').hexdigest()}
name = 'UNSIGNED-APK-VERIFICATION.json' if args.unsigned else 'APK-VERIFICATION.json'
(apk.parent/name).write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n')
print('Hora APK identity, release/offline flags, ABIs, definitions, generated graphics and '+report['signing_status']+' verified.')
