import datetime, hashlib, json, os, pathlib, subprocess, time
root = pathlib.Path('/workspace/scratch')
work = pathlib.Path('/workspace/seonro-agent-9263756')
plan = root / 'agent-metrics-plan-9263756.json'
source = '9263756b1d00b8696fddecacb077f55d95141f26'
prefix = root / 'agent-metrics-9263756-restart1'
def utc(): return datetime.datetime.now(datetime.timezone.utc).isoformat()
def digest(path):
    h = hashlib.sha256()
    with open(path,'rb') as f:
        while b := f.read(1048576): h.update(b)
    return h.hexdigest()
def write_new(path, obj):
    with open(path,'x') as f: json.dump(obj, f, ensure_ascii=False, indent=2); f.write('\n')
old = root / 'agent-metrics-9263756.json'
old_run = root / 'agent-metrics-9263756-run.json'
old_data = json.loads(old.read_bytes())
old_meta = json.loads(old_run.read_text())
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=work,text=True).strip() == source
assert not subprocess.check_output(['git','status','--porcelain'],cwd=work,text=True).strip()
assert digest(plan) == 'f0df62e9edc7cd1855148dda4d9c6ee99d0d0a32d063f7e32016071feb93122b'
assert digest(root/'agent-metrics-plan-pending-source.json') == 'fbc43182e094a33d63e9d31a0232a8fe430704be22ed6b11dfa201f7d6a4c843'
for assembly in old_meta['assemblies']: assert digest(assembly['path']) == assembly['sha256']
for suffix in ('.json','.log','.pid','-run.json','-exit.json','-preceding-functional.json'):
    assert not pathlib.Path(str(prefix)+suffix).exists(), suffix
preserved = []
for name in ('agent-metrics-9263756.json','agent-metrics-9263756-run.json','agent-metrics-9263756.log','agent-metrics-9263756-preceding-functional.json','agent-metrics-plan-9263756.json','agent-metrics-plan-pending-source.json','observed-measurement-interruption-20261030T1806Z.json'):
    p = root/name
    preserved.append({'path':str(p),'bytes':p.stat().st_size,'sha256':digest(p)})
write_new(root/'agent-metrics-9263756-interruption-provenance.json',{
    'kind':'seonro-agent-measurement-external-interruption','recordedUtc':utc(),
    'sourceCommit':source,'originalPid':113736,'oldExecSession':16080,'oldExecWait':'Unknown process id 16080',
    'observedProcessState':'Z/PPID1','procExitCode':9,'exactInterruptionUtc':None,
    'externalObservation':str(root/'observed-measurement-interruption-20261030T1806Z.json'),
    'cause':'미확정. 외부 signal9 상태만 관측, cgroup oom=0/oom_kill=0. 주체를 단정하지 않음.',
    'originalStatus':old_data['executionStatus'],'originalReports':len(old_data['reports']),'originalFailedCases':old_data['failedCases'],
    'originalReportMtimeUtc':datetime.datetime.fromtimestamp(old.stat().st_mtime,datetime.timezone.utc).isoformat(),
    'resumeReview':{'file':'tests/Chronicles.Game.Tests/AgentMeasurements.cs','lines':'61-138','supported':False,
                    'reason':'Run은 빈 reports/axes로 시작하고 Save가 path를 전체 대체한다. 기존 결과 읽기/skip/resume 코드 없음.'},
    'recovery':'동일926·동일계획108cases를 새 restart1 경로에서 처음부터 실행. 원본 부분 결과 합성/완료 승격 없음.',
    'preservedArtifacts':preserved})
command = ['/workspace/.dotnet/dotnet','tests/Chronicles.Game.Tests/bin/Release/net10.0/Chronicles.Game.Tests.dll',
           '--filter','INV-001/002/011','--report',str(prefix)+'-preceding-functional.json',
           '--agent-metrics',str(prefix)+'.json','--agent-metrics-plan',str(plan)]
log = open(str(prefix)+'.log','xb',buffering=0)
started = utc()
process = subprocess.Popen(['taskset','-c','3',*command],cwd=work,stdin=subprocess.DEVNULL,stdout=log,stderr=subprocess.STDOUT,close_fds=True)
pathlib.Path(str(prefix)+'.pid').write_text(str(process.pid)+'\n')
time.sleep(0.25)
actual = pathlib.Path(f'/proc/{process.pid}/cmdline').read_bytes().decode().split('\0')[:-1]
status = pathlib.Path(f'/proc/{process.pid}/status').read_text()
write_new(pathlib.Path(str(prefix)+'-run.json'),{
    'kind':'seonro-agent-108-restart-provenance','statusAtLaunch':'running','recordedStartUtc':started,
    'pid':process.pid,'supervisorPid':os.getpid(),'sessionId':os.getsid(process.pid),'supervisorSessionId':os.getsid(0),
    'cwd':str(work),'actualCommand':actual,'requestedCommand':command,
    'affinity':next(line for line in status.splitlines() if line.startswith('Cpus_allowed_list:')),
    'sourceCommit':source,'productSourceFingerprint':old_meta['product_source_fingerprint'],
    'sourceCleanAtLaunch':True,'planPath':str(plan),'planSha256':digest(plan),'assemblies':old_meta['assemblies'],
    'output':str(prefix)+'.json','consoleLog':str(prefix)+'.log','precedingFunctional':str(prefix)+'-preceding-functional.json',
    'oldPartial':str(old),'oldPartialSha256':digest(old),'oldPartialReports':78,
    'interruptionProvenance':str(root/'agent-metrics-9263756-interruption-provenance.json'),
    'rootAuthorization':'외부중단 보존 후 동일926/108계획을 새경로 CPU3에서 재시작하라는 명시 지시',
    'restartScope':'원본78건과 합치지 않는 독립 전체108 재실행; seed/기간/예산/소스/성공조건 변경 없음',
    'cpuMax':pathlib.Path('/sys/fs/cgroup/cpu.max').read_text().strip(),
    'concurrentEnvironment':'CPU0 inventory 예약, CPU1/2 동일926 성장restart1, 본AI CPU3. 공유4CPU quota이며 독점CPU/순수추론지연 아님.',
    'separation':'9704/이후 소스 최적화와 분리. 새소스 실측으로 승격하지 않음.',
    'notes':'분리된 supervisor 세션에서 실제 child를 wait하고 종료 상태를 별도 -exit.json에 보존. 종료 뒤 root/politics CPU3해제 통지.'})
code = process.wait()
write_new(pathlib.Path(str(prefix)+'-exit.json'),{'observedEndUtc':utc(),'pid':process.pid,'returnCode':code,'terminatedBySignal':-code if code < 0 else None})
log.close()
