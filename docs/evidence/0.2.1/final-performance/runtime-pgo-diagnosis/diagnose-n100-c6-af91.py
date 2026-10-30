import datetime, hashlib, json, os, resource, subprocess, sys
from pathlib import Path

ROOT = Path('/workspace/scratch/n100-c6-af91-diagnosis-1')
PLAN = ROOT / 'preregistered.json'
STATE = ROOT / 'supervisor.json'
DOTNET = '/workspace/.dotnet/dotnet'
SOURCES = {
    'c6': ('/workspace/seonro-final-c6e4704', 'c6e470458ecefe055d98f89120f08c40c0c0660d'),
    'af91': ('/workspace/seonro-final-af91fb0', 'af91fb0600a7c3603eac4fd7429ec2dd54bc1e3a'),
}
CASES = [('c6', 'default'), ('af91', 'default'), ('af91', 'pgo-off'), ('c6', 'pgo-off'),
         ('c6', 'pgo-off'), ('af91', 'pgo-off'), ('af91', 'default'), ('c6', 'default')]

def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()

def write(path, data):
    pending = path.with_suffix(path.suffix + '.pending')
    pending.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n')
    pending.replace(path)

if '--prepare' in sys.argv:
    ROOT.mkdir(exist_ok=False)
    write(PLAN, {
        'kind': 'preregistered-bounded-diagnosis-not-acceptance', 'createdUtc': now(),
        'eligibleForFinalPerformanceGate': False,
        'purpose': 'Compare unchanged runtime sources and the registered-test-only revision; test one runtime PGO axis without changing fixture, laws, workload, warmup or monthly timing definition.',
        'cases': [{'index': i + 1, 'source': source, 'runtimeConfiguration': config} for i, (source, config) in enumerate(CASES)],
        'countFixedBeforeExecution': 8, 'retries': 0, 'warmupMonths': 2, 'measuredMonths': 12,
        'population': 100, 'cpuAffinity': [0], 'sourceWorktrees': SOURCES,
        'runtimeAxis': {'default': 'unmodified inherited runtime configuration', 'pgo-off': 'DOTNET_TieredPGO=0 only, per-process diagnostic configuration; not adopted product behavior'},
        'constraints': ['Do not choose a passing report from these diagnostic samples for the final gate.',
                        'Keep all raw reports and failed/time-out attempts; no repeat-until-pass.',
                        'No EventPipe on these eight runs; profiling, if needed, is a separate explicitly instrumented run.',
                        'Do not change the existing 25% budget or baseline to accept a failure.',
                        'Any product fix must be reviewed and tested before a new full four-population final measurement.'],
        'cohosts': 'CPU1/2 old926 growth restart1; CPU3 old926 seed2026. Shared4CPU quota. No additional local heavy work during these eight runs. Thermal and power state unmeasured.',
    })
    print(json.dumps({'prepared': str(PLAN), 'started': False}))
elif '--supervise' not in sys.argv:
    assert PLAN.exists() and not STATE.exists()
    with (ROOT / 'supervisor.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, __file__, '--supervise'], stdin=subprocess.DEVNULL,
                                 stdout=log, stderr=subprocess.STDOUT, start_new_session=True, close_fds=True)
    print(json.dumps({'supervisorPid': child.pid, 'plan': str(PLAN)}))
else:
    plan = json.loads(PLAN.read_text())
    assert plan['countFixedBeforeExecution'] == len(CASES) == 8
    state = {'startedUtc': now(), 'supervisorPid': os.getpid(), 'planSha256': hashlib.sha256(PLAN.read_bytes()).hexdigest(),
             'eligibleForFinalPerformanceGate': False, 'completed': [], 'status': 'running'}
    write(STATE, state)
    for index, (source, config) in enumerate(CASES, 1):
        directory, revision = SOURCES[source]
        assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=directory, text=True).strip() == revision
        assert not subprocess.check_output(['git', 'status', '--porcelain'], cwd=directory, text=True).strip()
        name = f'{index:02d}-{source}-{config}'
        report = ROOT / (name + '.json')
        logfile = ROOT / (name + '.log')
        provenance = ROOT / (name + '-run.json')
        assert not report.exists() and not logfile.exists() and not provenance.exists()
        env = dict(os.environ)
        if config == 'pgo-off':
            env['DOTNET_TieredPGO'] = '0'
        command = ['taskset', '-c', '0', DOTNET, 'tests/Chronicles.Game.Tests/bin/Release/net10.0/Chronicles.Game.Tests.dll',
                   '--filter', 'INV-001/002/011', '--performance', str(report), '--performance-size', '100']
        entry = {'index': index, 'sourceCommit': revision, 'runtimeConfiguration': config, 'startedUtc': now(),
                 'eligibleForFinalPerformanceGate': False, 'command': command, 'configuredTieredPGO': env.get('DOTNET_TieredPGO'),
                 'status': 'running'}
        before = resource.getrusage(resource.RUSAGE_CHILDREN)
        write(provenance, entry)
        with logfile.open('xb') as log:
            process = subprocess.Popen(command, cwd=directory, env=env, stdin=subprocess.DEVNULL, stdout=log, stderr=subprocess.STDOUT)
            entry['pid'] = process.pid
            write(provenance, entry)
            try:
                code = process.wait(timeout=120)
            except subprocess.TimeoutExpired:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill(); process.wait()
                code = process.returncode
                entry['timedOut'] = True
        after = resource.getrusage(resource.RUSAGE_CHILDREN)
        entry.update(endedUtc=now(), exitCode=code, status='completed' if code == 0 else 'failed',
                     processTreeUserSeconds=after.ru_utime-before.ru_utime, processTreeSystemSeconds=after.ru_stime-before.ru_stime)
        if report.exists():
            entry['reportSha256'] = hashlib.sha256(report.read_bytes()).hexdigest()
            result = json.loads(report.read_text())
            if result.get('reports'):
                row = result['reports'][0]
                entry['metrics'] = {key: row[key] for key in ('advance12MonthsMs', 'monthlyP50Ms', 'monthlyP95Ms', 'queryMs',
                    'saveMs', 'loadMs', 'allocatedBytes', 'saveBytes', 'initialHash', 'finalHash')}
        write(provenance, entry)
        state['completed'].append(entry)
        write(STATE, state)
    state.update(endedUtc=now(), status='completed')
    write(STATE, state)
