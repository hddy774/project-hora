import datetime, hashlib, json, os, resource, subprocess, sys, time
from pathlib import Path

ROOT = Path('/workspace/scratch/population-sequence-af91-diagnosis-1')
WORK = '/workspace/seonro-final-af91fb0'
REVISION = 'af91fb0600a7c3603eac4fd7429ec2dd54bc1e3a'
CASES = ['default', 'pgo-off', 'pgo-off', 'default']

def now(): return datetime.datetime.now(datetime.timezone.utc).isoformat()
def write(path, data):
    pending = path.with_suffix(path.suffix + '.pending')
    pending.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n'); pending.replace(path)

if '--prepare' in sys.argv:
    ROOT.mkdir(exist_ok=False)
    write(ROOT/'preregistered.json', {
        'kind': 'preregistered-population-sequence-runtime-diagnosis-not-acceptance', 'createdUtc': now(),
        'eligibleForFinalPerformanceGate': False, 'sourceCommit': REVISION,
        'basis': 'The preceding fixed eight single-N100 comparisons completed. Both source revisions have similar default single-N100 timing; PGO-off was lower in both repetitions. This follow-up tests the actual prior-N2 sequence, not a selected passing single-size result.',
        'cases': CASES, 'countFixedBeforeExecution': 4, 'retries': 0,
        'protocol': 'Use the unchanged full four-population CLI. Preserve actual N2 and N100 warmup/12-month results. Terminate deliberately only after PERF-BEGIN 1000 is emitted (following the completed N100 row and progress write). Retain partial raw and actual termination code, never mark full completion.',
        'runtimeAxis': 'DOTNET_TieredPGO=0 versus inherited default only', 'cpuAffinity': [0],
        'constraints': ['No profiler attached', 'No fixture/law/measurement code changes', 'No budget or baseline change',
                       'No diagnostic result may satisfy the final four-population gate', 'No repeat until pass'],
        'cohosts': 'CPU1/2 old926 growth restart1, CPU3 old926 seed2026, same4CPUquota; no additional local heavy work. Thermal/power unmeasured.',
    })
    print(json.dumps({'prepared': str(ROOT), 'started': False}))
elif '--supervise' not in sys.argv:
    assert (ROOT/'preregistered.json').exists() and not (ROOT/'supervisor.json').exists()
    with (ROOT/'supervisor.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, __file__, '--supervise'], stdin=subprocess.DEVNULL, stdout=log,
                                 stderr=subprocess.STDOUT, start_new_session=True, close_fds=True)
    print(json.dumps({'supervisorPid': child.pid}))
else:
    state = {'startedUtc': now(), 'status': 'running', 'completed': [], 'eligibleForFinalPerformanceGate': False,
             'planSha256': hashlib.sha256((ROOT/'preregistered.json').read_bytes()).hexdigest()}
    write(ROOT/'supervisor.json', state)
    for index, config in enumerate(CASES, 1):
        assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=WORK,text=True).strip() == REVISION
        assert not subprocess.check_output(['git','status','--porcelain'],cwd=WORK,text=True).strip()
        stem=f'{index:02d}-{config}'; report=ROOT/(stem+'.json'); logfile=ROOT/(stem+'.log'); meta=ROOT/(stem+'-run.json')
        assert not report.exists() and not logfile.exists() and not meta.exists()
        env=dict(os.environ)
        if config=='pgo-off': env['DOTNET_TieredPGO']='0'
        command=['taskset','-c','0','/workspace/.dotnet/dotnet','tests/Chronicles.Game.Tests/bin/Release/net10.0/Chronicles.Game.Tests.dll',
                 '--filter','INV-001/002/011','--performance',str(report)]
        entry={'index':index,'runtimeConfiguration':config,'sourceCommit':REVISION,'command':command,'startedUtc':now(),
               'eligibleForFinalPerformanceGate':False,'configuredTieredPGO':env.get('DOTNET_TieredPGO'),'status':'running'}
        write(meta,entry); before=resource.getrusage(resource.RUSAGE_CHILDREN)
        with logfile.open('x',encoding='utf-8') as log:
            process=subprocess.Popen(command,cwd=WORK,env=env,stdin=subprocess.DEVNULL,stdout=subprocess.PIPE,
                                     stderr=subprocess.STDOUT,text=True,encoding='utf-8',bufsize=1)
            entry['pid']=process.pid;write(meta,entry); intentional=False
            for line in process.stdout:
                log.write(line);log.flush()
                if line.startswith('PERF-BEGIN 1000:'):
                    intentional=True;entry['stopBoundaryObservedUtc']=now();entry['stopBoundaryLine']=line.strip()
                    process.terminate();break
            try:code=process.wait(timeout=10)
            except subprocess.TimeoutExpired:process.kill();code=process.wait();entry['killAfterGrace']=True
        after=resource.getrusage(resource.RUSAGE_CHILDREN)
        entry.update(endedUtc=now(),exitCode=code,intentionalDiagnosticStop=intentional,
                     status='diagnostic-boundary-collected' if intentional else 'unexpected-end',
                     processTreeUserSeconds=after.ru_utime-before.ru_utime,processTreeSystemSeconds=after.ru_stime-before.ru_stime)
        if report.exists():
            raw=json.loads(report.read_text());entry['reportSha256']=hashlib.sha256(report.read_bytes()).hexdigest()
            entry['rawComplete']=raw['complete'];entry['completedSizes']=raw['completedSizes']
            entry['metrics']=[{k:row[k] for k in ('population','advance12MonthsMs','monthlyP50Ms','monthlyP95Ms','queryMs','saveMs','loadMs',
                             'allocatedBytes','saveBytes','initialHash','finalHash')} for row in raw['reports']]
        write(meta,entry);state['completed'].append(entry);write(ROOT/'supervisor.json',state)
    state.update(endedUtc=now(),status='completed');write(ROOT/'supervisor.json',state)
