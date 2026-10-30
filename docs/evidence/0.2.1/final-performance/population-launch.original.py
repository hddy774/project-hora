import subprocess,json,hashlib,os,sys,datetime
from pathlib import Path
W=Path('/workspace/seonro-stable-runtime');S=Path('/workspace/scratch');P=S/'final-population-94ccb5d-run.json';L=S/'final-population-94ccb5d.log';R=S/'final-population-94ccb5d.json'
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def git(*a):return subprocess.check_output(['git',*a],cwd=W,text=True).strip()
def save(d):t=P.with_suffix('.pending');t.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n');t.replace(P)
if '--supervise' not in sys.argv:
 assert not P.exists() and not R.exists() and not L.exists()
 with (S/'final-population-94ccb5d-supervisor.log').open('wb') as f:
  q=subprocess.Popen([sys.executable,__file__,'--supervise'],start_new_session=True,stdin=subprocess.DEVNULL,stdout=f,stderr=subprocess.STDOUT,close_fds=True)
 print(json.dumps({'supervisorPid':q.pid,'supervisorSeparateSession':True}))
else:
 prior=json.loads((S/'functional-final-94ccb5d-run.json').read_text());assert prior.get('exitCode')==0 and prior.get('functionalSummary')=={'selection':'all','total':314,'failed':0}; source=git('rev-parse','HEAD');clean=not git('status','--porcelain');assert source=='94ccb5d16f0c0d7bba0fd14aa8b0451dfe15bdd5' and clean
 fp=subprocess.check_output(['python3','scripts/verify-v021-release.py','fingerprint','--commit',source],cwd=W,text=True).strip();assert len(fp)==64 and all(x in '0123456789abcdef' for x in fp)
 assert os.environ.get('DOTNET_TieredPGO') is None and os.environ.get('COMPlus_TieredPGO') is None
 runtime_path=W/'tests/Chronicles.Game.Tests/bin/Release/net10.0/Chronicles.Game.Tests.runtimeconfig.json'
 runtime_config=json.loads(runtime_path.read_text());assert runtime_config['runtimeOptions']['configProperties']['System.Runtime.TieredPGO'] is False
 cmd=['taskset','-c','0','/workspace/.dotnet/dotnet','tests/Chronicles.Game.Tests/bin/Release/net10.0/Chronicles.Game.Tests.dll','--filter','INV-001/002/011','--report',str(S/'final-population-94ccb5d-preceding-functional.json'),'--performance',str(R)]
 d={'kind':'root-final-four-population-verification-after-runtime-pgo-fix','sourceCommit':source,'fingerprintCommandResult':fp,'worktree':str(W),'sourceWorktreeCleanAtLaunch':clean,'startedUtc':now(),'supervisorPid':os.getpid(),'supervisorSession':os.getsid(0),'command':cmd,'requestedPopulations':[2,100,1000,10000],'status':'running','cohosts':'CPU1/2 exact926 growth restart1; CPU3 exact926 growth2026. Large local postprocessing/build/profile paused; small document/map validation and metadata work may occur on CPU4 within shared4CPU quota. CPU0 reserved for this functional run. CPU0 pinned one CPU, host4CPU quota; growth and AI cohost external load. No EventPipe/trace attached to this process. Thermal and power state not measured; not Android or exclusive physical-host CPU latency.'};save(d)
 with L.open('wb') as f:
  child=subprocess.Popen(cmd,cwd=W,stdin=subprocess.DEVNULL,stdout=f,stderr=subprocess.STDOUT);d['pid']=child.pid;save(d);code=child.wait()
 d.update(exitCode=code,endedUtc=now(),status='completed' if code==0 else 'failed',sourceCommitAtEnd=git('rev-parse','HEAD'),sourceWorktreeCleanAtEnd=not git('status','--porcelain'))
 if R.exists():d['reportSha256']=hashlib.sha256(R.read_bytes()).hexdigest();raw=json.loads(R.read_text());d['measurementSummary']={k:raw.get(k) for k in ['complete','completedSizes','inProgress','totalElapsedSeconds']}
 d['runtimeConfiguration']={'path':str(runtime_path),'sha256':hashlib.sha256(runtime_path.read_bytes()).hexdigest(),'content':runtime_config}
 d['runtimePGOEnvironmentOverrides']={k:os.environ.get(k) for k in ['DOTNET_TieredPGO','COMPlus_TieredPGO']}
 d['assemblies']=[]
 for base,name in [('src/Chronicles.Core','Chronicles.Core'),('src/Chronicles.Game','Chronicles.Game'),('src/Chronicles.Hosting','Chronicles.Hosting'),('tests/Chronicles.Game.Tests','Chronicles.Game.Tests')]:
  q=W/base/'bin/Release/net10.0'/(name+'.dll');a=W/base/'obj/Release/net10.0'/(name+'.AssemblyInfo.cs');d['assemblies'].append({'path':str(q),'sha256':hashlib.sha256(q.read_bytes()).hexdigest() if q.exists() else None,'bytes':q.stat().st_size if q.exists() else None,'generatedInformationalVersionLines':[x.strip() for x in a.read_text().splitlines() if 'AssemblyInformationalVersion' in x] if a.exists() else []})
 save(d)
