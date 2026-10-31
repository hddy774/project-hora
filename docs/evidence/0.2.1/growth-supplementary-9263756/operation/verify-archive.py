#!/usr/bin/env python3
"""Checks copied evidence only. Never starts a world/process or changes raw files."""
from pathlib import Path
import hashlib
import json

root=Path(__file__).resolve().parent.parent
manifest=json.loads((root/'manifest.json').read_text())
for record in manifest['files']:
    path=(root/record['path']).resolve()
    assert path.is_relative_to(root)
    data=path.read_bytes()
    assert len(data)==record['bytes']
    assert hashlib.sha256(data).hexdigest()==record['sha256'],record['path']
read=lambda name:json.loads((root/'raw'/name).read_text())
initial=read('growth-9263756-interruption-20261030T1810Z.json')
for record in initial['artifacts']:
    data=(root/'raw'/Path(record['path']).name).read_bytes()
    assert hashlib.sha256(data).hexdigest()==record['sha256']
assert initial['termination'].startswith('SIGKILL') and initial['resumeSupported'] is False
failed=read('growth-9263756-intentional-duplicate-stop-after-primary9.json')
assert failed['signals']==[]
stopped=read('growth-9263756-intentional-duplicate-stop-after-primary9-v2.json')
assert stopped['rawRewrittenByObserver'] is False and stopped['primaryOrOtherProcessSignalsSent'] is False
assert len(stopped['signals'])==2 and {r['pid'] for r in stopped['signals']}=={120460,132980}
assert all(r['signal']=='SIGTERM' for r in stopped['signals'])
assert all(r['same'] and not r['differences'] for r in stopped['checks'])
assert sum(r['completed'] for r in stopped['checks'])==6
for record in stopped['after']:
    assert record['rawChangedSinceCompared'] is False and record['rawComplete'] is False
    expected=0 if record['seed']==98 else -15
    assert record['actualSupervisorExitCode']==expected
    for artifact in record['artifacts']:
        data=(root/'raw'/Path(artifact['path']).name).read_bytes()
        assert hashlib.sha256(data).hexdigest()==artifact['sha256']
completed=set()
for name in ['growth-9263756-seed42-restart1.json','growth-9263756-seed98-restart1.json','growth-9263756-seed2026.json']:
    report=read(name)
    assert report['sourceCommit']==manifest['sourceCommit'] and report['productSourceFingerprint']==manifest['productSourceFingerprint']
    assert report['complete'] is False
    for row in report['reports']:
        if row['status']=='completed':
            assert row['observedThroughMonth']==6000
            assert [p['month'] for p in row['timeSeries']]==list(range(0,6001,120))
            key=(row['scenario'],row['seed'])
            assert key not in completed
            completed.add(key)
assert len(completed)==6
print(json.dumps({'archiveChecks':'PASS','copiedFiles':len(manifest['files']),
    'completedDistinctSupplementaryCohorts':len(completed),'primaryCasesFromArchive':0,
    'newMeasurementsExecuted':0,'rawChanged':False},ensure_ascii=False))
