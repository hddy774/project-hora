from pathlib import Path
import datetime, hashlib, importlib.util, json, os, re, subprocess, sys, time

checkout = Path(sys.argv[1]).resolve()
output = Path(sys.argv[2]).resolve()
expected = sys.argv[3]
functional = Path(sys.argv[4]).resolve()
output.mkdir(exist_ok=False)

def stamp():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()

def git(*args):
    return subprocess.check_output(['git', *args], cwd=checkout, text=True).strip()

def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def save():
    pending = output / 'run.json.pending'
    pending.write_text(json.dumps(meta, ensure_ascii=False, indent=2) + '\n')
    pending.replace(output / 'run.json')

assert git('rev-parse', 'HEAD') == expected
assert not git('status', '--porcelain', '--untracked-files=all')
prior = json.loads((functional / 'run.json').read_text())
full_report = json.loads((functional / 'functional.json').read_text())
assert prior['source_commit'] == expected and prior['source_worktree_clean_after'] is True
assert all(row['exit_code'] == 0 for row in prior['runs'])
assert full_report['selection'] == 'all' and full_report['failed'] == 0 and full_report['total'] == 323
assert digest(functional / 'functional.json') == prior['report_sha256']
env = os.environ.copy()
env.update(DOTNET_ROOT='/workspace/.dotnet', DOTNET_NOLOGO='true', DOTNET_CLI_TELEMETRY_OPTOUT='true')
env['PATH'] = '/workspace/.dotnet:' + env['PATH']
overrides = {key: env.get(key) for key in ('DOTNET_TieredPGO', 'COMPlus_TieredPGO', 'DOTNET_TieredCompilation', 'COMPlus_TieredCompilation')}
assert all(value is None for value in overrides.values())
dll = checkout / 'tests/Chronicles.Game.Tests/bin/Release/net10.0/Chronicles.Game.Tests.dll'
runtime_path = dll.with_suffix('.runtimeconfig.json')
runtime = json.loads(runtime_path.read_text())
assert runtime['runtimeOptions']['configProperties']['System.Runtime.TieredPGO'] is False
baseline_path = checkout / 'docs/evidence/0.2.1/final-performance/baseline.json'
budget_path = checkout / 'docs/evidence/0.2.1/final-performance/budget.json'
assert digest(baseline_path) == '1842a4dbffb66fa9c8489ad31343d94fed933f022c8d63be430b774c37998484'
assert digest(budget_path) == '7365b80e3de602dfe331a5348f4008808f6620744577582d07bc7f33f11dc272'
command = ['taskset', '-c', '0', '/workspace/.dotnet/dotnet', 'exec', '--fx-version', '10.0.14', str(dll),
           '--filter', 'INV-001/002/011', '--report', str(output / 'preceding-functional.json'),
           '--performance', str(output / 'population.json')]
meta = {
    'kind': 'actual-final-four-population-measurement', 'status': 'running',
    'source_commit': expected, 'product_source_fingerprint': prior['product_source_fingerprint'],
    'source_worktree_clean_before': True, 'started_utc': stamp(), 'command': command,
    'full_functional_source': expected, 'full_functional_total': 323,
    'full_functional_report_sha256': prior['report_sha256'], 'tiered_environment': overrides,
    'sdk': subprocess.check_output(['/workspace/.dotnet/dotnet', '--version'], cwd=checkout, text=True).strip(),
    'runtime_configuration': runtime, 'runtime_configuration_sha256': digest(runtime_path),
    'baseline_sha256': digest(baseline_path), 'budget_sha256': digest(budget_path),
    'host_scope': '동일 Linux 호스트 CPU0 한 개 affinity, 공유 4CPU quota. 다른 로컬 대형 빌드·측정·프로파일 없음. 문서·메타데이터 작업은 허용. 열·전원 상태 미측정. EventPipe/trace 미부착. 장기 안정성은 별도 GitHub VM에서 측정.',
    'assemblies': []
}
assert meta['sdk'] == '10.0.403'
for name in ('Chronicles.Core', 'Chronicles.Game', 'Chronicles.Hosting', 'Chronicles.Game.Tests'):
    path = dll.parent / (name + '.dll')
    versions = [m.group().decode() for m in re.finditer(rb'0\.2\.1\+[0-9a-f]{40}', path.read_bytes())]
    assert versions and all(value.endswith(expected) for value in versions)
    meta['assemblies'].append({'name': name, 'bytes': path.stat().st_size, 'sha256': digest(path),
                              'informational_version_literals': versions,
                              'scope': '실제 DLL raw literal source 보조 대조; 전체 attribute constructor binding 검사는 별도'})
save()
started = time.monotonic()
with (output / 'population.log').open('wb') as log:
    child = subprocess.Popen(command, cwd=checkout, env=env, stdout=log, stderr=subprocess.STDOUT)
    meta['pid'] = child.pid
    for _ in range(100):
        try:
            maps = Path(f'/proc/{child.pid}/maps').read_text()
            libraries = sorted({line.split()[-1] for line in maps.splitlines() if '/Microsoft.NETCore.App/' in line})
            if any(path.endswith('/libcoreclr.so') for path in libraries):
                assert any('/10.0.14/libcoreclr.so' in path for path in libraries)
                meta['actual_runtime_mapped_files'] = libraries
                break
        except FileNotFoundError:
            pass
        if child.poll() is not None:
            break
        time.sleep(0.05)
    save()
    print('POPULATION_STARTED', expected, child.pid, flush=True)
    code = child.wait()
meta.update(exit_code=code, ended_utc=stamp(), seconds=time.monotonic() - started,
            status='completed' if code == 0 else 'failed',
            source_worktree_clean_after=not git('status', '--porcelain', '--untracked-files=all'))
assert git('rev-parse', 'HEAD') == expected and meta['source_worktree_clean_after'] is True
if (output / 'population.json').exists():
    meta['report_sha256'] = digest(output / 'population.json')
save()
if code:
    raise SystemExit(code)
pre = json.loads((output / 'preceding-functional.json').read_text())
assert pre['selection'] == 'INV-001/002/011' and pre['total'] == 1 and pre['failed'] == 0
spec = importlib.util.spec_from_file_location('release_check', checkout / 'scripts/verify-v021-release.py')
check = importlib.util.module_from_spec(spec)
spec.loader.exec_module(check)
baseline = check.read(baseline_path)
measured = check.read(output / 'population.json')
budget = check.read(budget_path)
before, after = check.measurement_rows(baseline), check.measurement_rows(measured)
rows = []
for population, row in after.items():
    for metric in check.PERFORMANCE_METRICS:
        absolute = budget['limits'][str(population)][metric]
        regression = before[population][metric] * (1 + budget['maximum_regression_percent'] / 100)
        rows.append({'population': population, 'metric': metric, 'actual': row[metric],
                     'absolute_limit': absolute, 'regression_limit': regression,
                     'passed': row[metric] <= absolute and row[metric] <= regression})
assessment = {'schema_version': 1, 'source_commit': expected, 'report_sha256': meta['report_sha256'],
              'baseline_sha256': digest(baseline_path), 'budget_sha256': digest(budget_path),
              'passed': all(row['passed'] for row in rows), 'checks': rows,
              'scope': '실제 네 인구·32개 단기 지표의 기존 절대/회귀 상한 비교. 장기 안정성이나 Android 수용을 대신하지 않음.'}
(output / 'assessment.json').write_text(json.dumps(assessment, ensure_ascii=False, indent=2) + '\n')
print('POPULATION_ENDED', code, 'budget_passed', assessment['passed'], flush=True)
