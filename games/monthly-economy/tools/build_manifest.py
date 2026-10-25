#!/usr/bin/env python3
"""Actions-only build inventory with an explicit APK-only publication policy."""
import hashlib
import json
import pathlib
import subprocess
import sys

VERSION = '0.2.1'
APPLICATION_ID = 'org.hora.economiclab'
STEM = 'HoraEconomicLab-v' + VERSION
PUBLIC_APK = STEM + '.apk'
ALLOWED_FILES = {PUBLIC_APK, STEM + '-UNSIGNED.apk', STEM + '-linux.zip', STEM + '-linux.x86_64'}

root = pathlib.Path(sys.argv[1]).resolve()
project = pathlib.Path(__file__).resolve().parents[1]
paths = [root/name for name in sys.argv[2:]]
assert paths and len({p.name for p in paths}) == len(paths), 'Pass each intended artifact exactly once'
assert {p.name for p in paths} <= ALLOWED_FILES, 'Unexpected build inventory filename'
assert not ({PUBLIC_APK, STEM + '-UNSIGNED.apk'} <= {p.name for p in paths}), 'Keep signed and unsigned APK inventories separate'
records = []
for path in paths:
    assert path.is_file() and path.parent == root
    with path.open('rb') as stream:
        checksum = hashlib.file_digest(stream, 'sha256').hexdigest()
    records.append({'file': path.name, 'bytes': path.stat().st_size, 'sha256': checksum})
signing_status = 'not_applicable'
if any(p.name.endswith('-UNSIGNED.apk') for p in paths):
    signing_status = 'unsigned_release_input'
elif any(p.suffix == '.apk' for p in paths):
    proof = json.loads((root/'APK-VERIFICATION.json').read_text())
    assert proof['signing_status'] == 'retained_publisher_verified'
    assert proof['application_id'] == APPLICATION_ID and proof['version'] == VERSION and proof['version_code'] == 3
    for record in records:
        if record['file'].endswith('.apk'):
            assert record['sha256'] == proof['apk_sha256'], 'Signed APK proof mismatch'
    signing_status = 'retained_publisher_verified'
manifest = {'game': 'hora-economic-lab', 'display_name': 'Hora Economic Lab · 호라경제연구실',
            'version': VERSION, 'application_id': APPLICATION_ID, 'godot': '4.6.3.stable',
            'source_commit': subprocess.check_output(['git','rev-parse','HEAD'],cwd=project,text=True).strip(),
            'source_dirty': bool(subprocess.check_output(['git','status','--porcelain'],cwd=project,text=True).strip()),
            'balance_data_status': 'experimental', 'generated_graphics': True,
            'signing_status': signing_status,
            'artifact_scope': 'actions_internal_inventory',
            'publication_policy': 'apk_only', 'public_release_assets': [PUBLIC_APK],
            'artifacts': records}
(root/'BUILD-MANIFEST.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
(root/'SHA256SUMS.txt').write_text(''.join(r['sha256']+'  '+r['file']+'\n' for r in records))
print('Actions build inventory and SHA-256 checksums written; the only public release attachment is ' + PUBLIC_APK + '.')
