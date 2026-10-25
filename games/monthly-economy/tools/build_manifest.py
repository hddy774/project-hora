#!/usr/bin/env python3
"""Immutable-source provenance for unsigned build inputs or signed releases."""
import hashlib
import json
import pathlib
import subprocess
import sys

root = pathlib.Path(sys.argv[1]).resolve()
project = pathlib.Path(__file__).resolve().parents[1]
paths = [root/name for name in sys.argv[2:]]
assert paths and len({p.name for p in paths}) == len(paths), 'Pass each intended artifact exactly once'
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
    for record in records:
        if record['file'].endswith('.apk'):
            assert record['sha256'] == proof['apk_sha256'], 'Signed APK proof mismatch'
    signing_status = 'retained_publisher_verified'
manifest = {'game': 'hora-economic-lab', 'display_name': 'Hora Economic Lab · 호라경제연구실',
            'version': '0.2.0', 'application_id': 'org.hora.economiclab', 'godot': '4.6.3.stable',
            'source_commit': subprocess.check_output(['git','rev-parse','HEAD'],cwd=project,text=True).strip(),
            'source_dirty': bool(subprocess.check_output(['git','status','--porcelain'],cwd=project,text=True).strip()),
            'balance_data_status': 'experimental', 'generated_graphics': True,
            'signing_status': signing_status,
            'artifacts': records}
(root/'BUILD-MANIFEST.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
(root/'SHA256SUMS.txt').write_text(''.join(r['sha256']+'  '+r['file']+'\n' for r in records))
print('Hora immutable-source build manifest and SHA-256 checksums written.')
