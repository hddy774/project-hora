"""Read only preserved artifacts; do not execute a .NET assembly or alter reports."""
from pathlib import Path
import datetime
import hashlib
import importlib.util
import json
import zipfile

ROOT = Path('/workspace/scratch/sensitivity-41159864831-7ce')
OLD = Path('/workspace/scratch/sensitivity-41131479924-7ce/seed-98')
NEW = ROOT / 'seed-98'
SOURCE = '7ce66943b539420c9dc1e3fa8a611daae58020ff'
FP = '4e228e7ba34e538fd1a69abf05934d8162b2a65559ed4ffd50a73b00a8ba2304'
PLAN = '74068850c36350beaf547665fc15aeb2da799899d47fedb9bd8f2a0568a6b653'
BASELINE = '02ab5dc68175bc614fa987b22a8559f9155d4d3055627663cef2db2b36aed175'


def read(path):
    return json.loads(path.read_text())


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def non_timing(value):
    if isinstance(value, dict):
        return {k: non_timing(v) for k, v in value.items()
                if k not in {'simulationMs', 'caseElapsedMs'}}
    if isinstance(value, list):
        return [non_timing(v) for v in value]
    return value


artifact = next(a for a in read(ROOT / 'api/artifacts-20261031T050042Z.json')['artifacts']
                if a['id'] == 13030660873)
archive = ROOT / 'seed-98.zip'
assert archive.stat().st_size == artifact['size_in_bytes']
assert 'sha256:' + sha(archive) == artifact['digest']
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    for name in z.namelist():
        assert (NEW / name).read_bytes() == z.read(name)
for f in read(NEW / 'provenance/files.json')['files']:
    p = NEW / f['path']
    assert p.stat().st_size == f['bytes'] and sha(p) == f['sha256']

raw = read(NEW / 'out/sensitivity.json')
old = read(OLD / 'out/sensitivity.json')
assert sha(OLD / 'out/sensitivity.json') == '8e4763832aa14bd23d4ab528699329d14474598fdd0190b1eee234585f2fdd40'
source = read(NEW / 'provenance/source.json')
start = read(NEW / 'provenance/process-start.json')
exit_record = read(NEW / 'provenance/process-exit.json')
plan = read(NEW / 'inputs/plan.json')
old_plan = read(OLD / 'inputs/plan.json')
assert source['source_commit'] == start['source_commit'] == raw['sourceCommit'] == SOURCE
assert source['source_tree'] == '5ae1b72e2471122e61f27a4cb9c294e7b40b5793'
assert source['product_source_fingerprint'] == raw['productSourceFingerprint'] == FP
assert source['source_worktree_clean'] and raw['environment']['sourceWorktreeClean']
assert source['sdk'] == '10.0.403' and raw['environment']['runtime'] == '.NET 10.0.14'
assert source['metadata_source_commit'] == start['metadata_source_commit'] == '19c1faa3a1e6b8a921fd3cbe1642ac3824c955b6'
assert source['workflow_source_commit'] == '836ff97e930e82af36a6f5fae6dae575c109f7d9'
assert source['scenario'] == raw['selectedBloodPolicy'] == 'a'
assert source['seed'] == start['seed'] == 98
assert source['wall_minutes'] == start['wall_cap_minutes'] == 240
assert sha(NEW / 'inputs/plan.json') == raw['planSha256'] == source['plan_sha256'] == start['plan_sha256'] == PLAN
assert sha(NEW / 'inputs/baseline.json') == raw['baselineReportSha256'] == source['baseline_sha256'] == start['baseline_sha256'] == BASELINE
assert (NEW / 'inputs/cli-wall-minutes.txt').read_bytes() == b'240\n'
assert sha(NEW / 'inputs/cli-wall-minutes.txt') == source['wall_minutes_file_sha256'] == start['wall_minutes_file_sha256'] == 'acbf3426054eb2943640b7d941b8181fe3cc9c00a2f396a4ebaa7647d493e420'
assert raw['plan'] == plan and plan['values'] == [1500, 900]
assert [k for k in plan if plan[k] != old_plan[k]] == ['values']
assert sha(OLD / 'inputs/plan.json') == '2533de34465aeddefd697062dbb3f806b197f280e44d5e96cd147a5692bee882'
assert plan['seeds'] == [42, 98, 2026]
assert plan['horizonMonths'] == source['horizon_months'] == 6000
assert plan['observationIntervalMonths'] == source['observation_interval_months'] == 120
unchanged = ['baseline.json', 'cli-wall-minutes.txt'] + [str(p.relative_to(NEW / 'inputs')) for p in sorted((NEW / 'inputs/raw').glob('*.json'))]
assert len(unchanged) == 11
assert all((NEW / 'inputs' / p).read_bytes() == (OLD / 'inputs' / p).read_bytes() for p in unchanged)
assert start['command'][0] == 'dotnet' and start['command'][1].endswith('/Chronicles.Game.Tests.dll')
assert start['command'][2:4] == ['--filter', 'INV-001/002/011']
assert start['command'][4] == '--sensitivity' and start['command'][5].endswith('/out/sensitivity.json')
assert start['command'][6] == '--sensitivity-plan' and start['command'][7].endswith('/inputs/plan.json')
assert start['command'][8:] == ['--sensitivity-seed', '98']
assert exit_record['process_exit'] == exit_record['log_exit'] == 0
assert exit_record['raw_sensitivity_report_modified_by_workflow'] is False

spec = importlib.util.spec_from_file_location('preserved_pe_reader', '/workspace/scratch/read-assembly-informational-version.py')
pe = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pe)
before = read(NEW / 'provenance/assemblies-before.json')
loaded = {b['assembly']: b for b in raw['builds']}
old_loaded = {b['assembly']: b for b in old['builds']}
expected_names = {'Chronicles.Core', 'Chronicles.Game', 'Chronicles.Hosting', 'Chronicles.Game.Tests'}
assert set(loaded) == {b['assembly'] for b in before} == expected_names
assemblies = []
for b in before:
    name = b['assembly']
    actual = pe.version(NEW / 'provenance/assemblies' / (name + '.dll'))
    assert actual['bytes'] == b['bytes']
    assert actual['sha256'] == b['sha256'] == loaded[name]['sha256']
    assert actual['assemblyInformationalVersion'] == loaded[name]['informationalVersion'] == '0.2.1+' + SOURCE
    assemblies.append({'assembly': name, **actual,
                       'oldSha256': old_loaded[name]['sha256'],
                       'binaryBytesEqualOldBuild': actual['sha256'] == old_loaded[name]['sha256']})

assert raw['counterfactual'] and raw['usesProductDefaultLaws'] is False
assert raw['changedLaw'] == 'cultivation.foundation' and raw['baselineCompletedCases'] == 9
assert raw['complete'] is False and raw['requested_complete'] is True
assert raw['status'] == 'requested-completed' and raw['runningCase'] is None
assert raw['plannedCases'] == 6 and raw['completedCases'] == 2
assert raw['requestedCases'] == [{'value': 1500, 'seed': 98}, {'value': 900, 'seed': 98}]
assert [(c['lawValue'], c['seed']) for c in raw['reports']] == [(1500, 98), (900, 98)]
assert raw['sourceDeclaredPeoplePolicyDefaults'] == old['sourceDeclaredPeoplePolicyDefaults']
comparisons = []
for c in raw['reports']:
    prior = next(x for x in old['reports'] if x['lawValue'] == c['lawValue'] and x['seed'] == 98)
    assert c['status'] == 'completed' and c['error'] is None
    assert c['observedThroughMonth'] == c['progressMonth'] == c['milestonesObservedThroughMonth'] == 6000
    assert [x['month'] for x in c['timeSeries']] == list(range(0, 6001, 120))
    assert len(c['usedLaws']) == 113 and len(c['provisionalPolicies']) == 103
    assert c['usedLaws']['birth.kinship'] == c['usedLaws']['birth.bothParents'] == 1
    assert c['usedLaws']['birth.minimumRealm'] == 3 and c['usedLaws']['birth.martialEquivalent'] == 0
    assert c['lawDifferences'] == [{'id': 'cultivation.foundation', 'baseline': 1200, 'actual': c['lawValue'], 'delta': c['lawValue'] - 1200}]
    assert c['finalHash'] == prior['finalHash']
    assert non_timing(c) == non_timing(prior)
    comparisons.append({'lawValue': c['lawValue'], 'seed': 98, 'observedThroughMonth': 6000,
                        'sampleCount': len(c['timeSeries']), 'finalHash': c['finalHash'],
                        'finalHashEqualsOriginal': True, 'allNonTimingCaseFieldsEqualOriginal': True,
                        'actualDifferingCaseFields': [k for k in c if c[k] != prior[k]],
                        'excludedTimingKeysRecursively': ['simulationMs', 'caseElapsedMs'],
                        'newCaseElapsedMs': c['caseElapsedMs'], 'oldCaseElapsedMs': prior['caseElapsedMs'],
                        'newSimulationMs': c['simulationMs'], 'oldSimulationMs': prior['simulationMs'],
                        'finalPopulation': c['timeSeries'][-1]['population'],
                        'births': c['timeSeries'][-1]['births'], 'deaths': c['timeSeries'][-1]['deaths']})

out = {'verification': 'PASS', 'verifiedAtUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
       'runId': 41159864831, 'attempt': 1, 'artifactId': artifact['id'], 'artifactName': artifact['name'],
       'artifactExpiresAt': artifact['expires_at'], 'zipBytes': archive.stat().st_size,
       'zipSha256': sha(archive), 'rawSha256': sha(NEW / 'out/sensitivity.json'),
       'oldRawSha256': sha(OLD / 'out/sensitivity.json'), 'source': source, 'actualCommandAndStart': start,
       'actualExit': exit_record, 'assemblies': assemblies, 'caseComparisons': comparisons,
       'rawComplete': raw['complete'], 'rawRequestedComplete': raw['requested_complete'],
       'canonicalUniqueConditionsAdded': 0, 'canonicalUniqueCompletedStill': 4,
       'canonicalSupplement1500Seed42And2026': 'unverified;no seed artifacts at2026-10-31T05:00:42Z',
       'wholeBatchOutcome': 'in_progress at2026-10-31T05:00:42Z', 'wholePlanAccepted': False,
       'limits': ['Physical build SHA differ between runs;each is tied to its own PE/before/loadedbuild records. No binary-byte equality claim.',
                  'Two seed98 duplicate cases are not new canonical conditions.',
                  'Timing/environment differences are preserved and not treated as product performance results.',
                  'Original run failure and4complete/2partial are unchanged;new42/2026 andcollection status remain pending.'],
       'newGameMeasurementsExecutedByVerifier': 0, 'rawChanged': False}
target = ROOT / 'seed-98-duplicate-verification.json'
target.write_text(json.dumps(out, ensure_ascii=False, indent=2) + '\n')
print(json.dumps({'verification': out['verification'], 'path': str(target),
                  'rawSha256': out['rawSha256'], 'comparisons': comparisons,
                  'allFourPeVersionsValidated': len(assemblies) == 4,
                  'canonicalUniqueConditionsAdded': 0}, ensure_ascii=False))
