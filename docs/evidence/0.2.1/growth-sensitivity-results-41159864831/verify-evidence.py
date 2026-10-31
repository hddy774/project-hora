"""Verify preserved documentation and optional original ZIPs; execute no Game code."""
from pathlib import Path
import argparse
import hashlib
import importlib.util
import json
import re
import sys
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parent
SOURCE = '7ce66943b539420c9dc1e3fa8a611daae58020ff'
FP = '4e228e7ba34e538fd1a69abf05934d8162b2a65559ed4ffd50a73b00a8ba2304'
sys.dont_write_bytecode = True


def read(relative):
    return json.loads((ROOT / relative).read_text())


def digest(data):
    return hashlib.sha256(data).hexdigest()


def non_timing(point):
    return {k: v for k, v in point.items() if k not in {'simulationMs', 'caseElapsedMs'}}


def verify_prefix(current, previous, expected_count):
    a, b = [{p['month']: non_timing(p) for p in points} for points in [current, previous]]
    shared = sorted(a.keys() & b.keys())
    assert len(shared) == expected_count
    assert all(a[m] == b[m] for m in shared)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--archives', type=Path)
    parser.add_argument('--old-archives', type=Path)
    args = parser.parse_args()
    assert args.archives or not args.old_archives, '--old-archives requires --archives'
    manifest = read('manifest.json')
    actual = {str(p.relative_to(ROOT)) for p in ROOT.rglob('*') if p.is_file()}
    assert actual == {f['path'] for f in manifest['files']} | {'manifest.json'}
    for f in manifest['files']:
        data = (ROOT / f['path']).read_bytes()
        assert len(data) == f['bytes'] and digest(data) == f['sha256'], f['path']
    run = read('metadata/run.json')
    assert run['id'] == 41159864831 and run['run_attempt'] == 1
    assert run['status'] == 'completed' and run['conclusion'] == 'failure'
    assert run['head_sha'] == '19c1faa3a1e6b8a921fd3cbe1642ac3824c955b6'
    plan = read('plan.json')
    assert digest((ROOT / 'plan.json').read_bytes()) == read('provenance.json')['planSha256']
    assert plan['sourceCommit'] == SOURCE and plan['productSourceFingerprint'] == FP
    assert plan['values'] == [1500, 900] and plan['seeds'] == [42, 98, 2026]
    assert plan['horizonMonths'] == 6000 and plan['observationIntervalMonths'] == 120
    assert (ROOT / 'cli-wall-minutes.txt').read_bytes() == b'240\n'
    index = read('unique-conditions.json')
    assert index['notCollectorAggregatePass'] and index['selectedUniqueConditionCount'] == 6
    assert index['oldRunConclusion'] == index['newRunConclusion'] == 'failure'
    expected = {(900, 42), (900, 98), (900, 2026), (1500, 98), (1500, 42), (1500, 2026)}
    assert {(c['lawValue'], c['seed']) for c in index['selectedCases']} == expected
    for c in index['selectedCases']:
        assert c['actualCaseStatus'] == 'completed' and c['observedThroughMonth'] == 6000 and c['samples'] == 51
        assert c['rawComplete'] is False and all(c['actualPeoplePoliciesEqualDeclared'].values())
    evidence = {seed: read(f'verification/seed-{seed}.json') for seed in [42, 98, 2026]}
    for seed, progress, sample, old_count, new_count in [(42, 3550, 3480, 22, 30), (2026, 5052, 5040, 46, 43)]:
        v = evidence[seed]
        assert v['actualExit']['process_exit'] == 124 and v['rawComplete'] is False and v['rawRequestedComplete'] is False
        assert v['canonicalUniqueConditionsAdded'] == 1
        first, second = v['cases']
        assert first['lawValue'] == 1500 and first['actual6000Complete'] and first['sampleCount'] == 51
        assert first['baseline1200Prefix']['allNonTimingPointsEqual'] and first['baseline1200Prefix']['matchedMonths'] == 51
        assert first['oldSameValuePrefix']['allNonTimingPointsEqual'] and first['oldSameValuePrefix']['matchedMonths'] == old_count
        assert first['foundationRecords'] == first['coreRecords'] == 0
        assert second['lawValue'] == 900 and not second['actual6000Complete']
        assert second['progressMonth'] == progress and second['observedThroughMonth'] == sample
        assert second['oldSameValuePrefix']['allNonTimingPointsEqual'] and second['oldSameValuePrefix']['matchedMonths'] == new_count
    assert evidence[98]['actualExit']['process_exit'] == 0 and evidence[98]['canonicalUniqueConditionsAdded'] == 0
    assert evidence[98]['rawComplete'] is False and evidence[98]['rawRequestedComplete'] is True
    assert all(c['finalHashEqualsOriginal'] and c['allNonTimingCaseFieldsEqualOriginal'] for c in evidence[98]['caseComparisons'])
    collector = read('collector/index.json')
    assert collector['outcome'] == collector['matrixResult'] == 'failure'
    assert collector['collectionMembers'] == 90 and collector['duplicateSeedMembers'] == 89
    assert collector['allCollectionSeedFilesByteEqualDirectSeedZIPs'] and not collector['verifiedSixFileExists']
    log = (ROOT / 'collector/job-124465144337.log').read_bytes()
    assert digest(log) == collector['failureLogSha256'] and b'AssertionError' in log and b'exit code 1' in log
    publication = read('publication.json')
    assert publication['publicationStatus'] == 'pending' and publication['publicAssetBytesVerified'] is False
    for label, target in re.findall(r'\[([^\]]+)\]\(([^)]+)\)', (ROOT / 'README.md').read_text()):
        if not target.startswith(('https://', 'http://')):
            assert (ROOT / target.split('#')[0]).exists(), (label, target)
    artifact_records = read('verification/artifacts.json')['artifacts']
    assert len(artifact_records) == 6 and len({a['artifactId'] for a in artifact_records}) == 6
    if args.archives:
        spec = importlib.util.spec_from_file_location('pe_reader', ROOT / 'read-pe-version.py')
        pe = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(pe)
        for artifact in artifact_records:
            local = args.archives / Path(artifact['zipPath']).name
            if not local.exists():
                local = args.archives / (artifact['name'] + '.zip')
            data = local.read_bytes()
            assert len(data) == artifact['bytes'] and digest(data) == artifact['sha256']
            with zipfile.ZipFile(local) as z:
                assert z.testzip() is None
                if '-seed-' not in artifact['name']:
                    continue
                seed = int(artifact['name'].rsplit('-', 1)[1])
                raw_bytes = z.read('out/sensitivity.json')
                assert digest(raw_bytes) == evidence[seed]['rawSha256']
                raw = json.loads(raw_bytes)
                assert raw['sourceCommit'] == SOURCE and raw['productSourceFingerprint'] == FP
                with tempfile.TemporaryDirectory() as temp:
                    for build in raw['builds']:
                        name = build['assembly'] + '.dll'
                        dll = z.read('provenance/assemblies/' + name)
                        assert digest(dll) == build['sha256']
                        p = Path(temp) / name
                        p.write_bytes(dll)
                        assert pe.version(p)['assemblyInformationalVersion'] == '0.2.1+' + SOURCE
                if args.old_archives:
                    old_path = args.old_archives / f'seed-{seed}.zip'
                    if not old_path.exists():
                        old_path = args.old_archives / f'v021-sensitivity-41131479924-1-seed-{seed}.zip'
                    with zipfile.ZipFile(old_path) as old_zip:
                        old_bytes = old_zip.read('out/sensitivity.json')
                        expected_raw_sha = next(c['rawSha256'] for c in index['selectedCases']
                                                if c['selectedBatch'] == 'old' and c['seed'] == seed)
                        assert digest(old_bytes) == expected_raw_sha
                        prior = json.loads(old_bytes)
                    for case in raw['reports']:
                        previous = next(c for c in prior['reports'] if c['lawValue'] == case['lawValue'])
                        count = len(previous['timeSeries']) if case['lawValue'] == 1500 else len(case['timeSeries'])
                        verify_prefix(case['timeSeries'], previous['timeSeries'], count)
                        if seed == 98:
                            assert case['finalHash'] == previous['finalHash']
    print(f'PASS: document manifest/source/failure/partial/unique6; archives={bool(args.archives)}; old-prefix-recheck={bool(args.old_archives)}; newGameExecutions=0')


if __name__ == '__main__':
    main()
