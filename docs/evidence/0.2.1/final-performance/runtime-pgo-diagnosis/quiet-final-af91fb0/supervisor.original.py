import subprocess,json,hashlib,os,sys,datetime
from pathlib import Path
W=Path('/workspace/seonro-final-af91fb0');S=Path('/workspace/scratch');P=S/'final-population-af91fb0-quiet1-run.json';L=S/'final-population-af91fb0-quiet1.log';R=S/'final-population-af91fb0-quiet1.json'
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def git(*a):return subprocess.check_output(['git',*a],cwd=W,text=True).strip()
def save(d):t=P.with_suffix('.pending');t.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n');t.replace(P)
if '--supervise' not in sys.argv:
 assert not P.exists() and not R.exists() and not L.exists()
 with (S/'final-population-af91fb0-quiet1-supervisor.log').open('wb') as f:
  q=subprocess.Popen([sys.executable,__file__,'--supervise'],start_new_session=True,stdin=subprocess.DEVNULL,stdout=f,stderr=subprocess.STDOUT,close_fds=True)
 print(json.dumps({'supervisorPid':q.pid,'supervisorSeparateSession':True}))
else:
 prior=json.loads((S/'functional-final-af91fb0-run.json').read_text());assert prior.get('exitCode')==0 and prior.get('functionalSummary')=={'selection':'all','total':314,'failed':0}; source=git('rev-parse','HEAD');clean=not git('status','--porcelain');assert source=='af91fb0600a7c3603eac4fd7429ec2dd54bc1e3a' and clean
 fp=subprocess.check_output(['python3','scripts/verify-v021-release.py','fingerprint','--commit',source],cwd=W,text=True).strip();assert len(fp)==64 and all(x in '0123456789abcdef' for x in fp)
 cmd=['taskset','-c','0','/workspace/.dotnet/dotnet','tests/Chronicles.Game.Tests/bin/Release/net10.0/Chronicles.Game.Tests.dll','--filter','INV-001/002/011','--report',str(S/'final-population-af91fb0-quiet1-preceding-functional.json'),'--performance',str(R)]
 d={'kind':'root-preregistered-one-quiet-population-verification','sourceCommit':source,'fingerprintCommandResult':fp,'worktree':str(W),'sourceWorktreeCleanAtLaunch':clean,'startedUtc':now(),'supervisorPid':os.getpid(),'supervisorSession':os.getsid(0),'command':cmd,'requestedPopulations':[2,100,1000,10000],'controlProtocol':'docs/evidence/0.2.1/final-performance/quiet-verification-preregistered.json','firstFailedAttemptKept':True,'status':'running','cohosts':'CPU1/2 exact926 growth restart1; CPU3 exact926 AI108 restart1. CPU0 reserved for this functional run. CPU0 pinned one CPU, host4CPU quota; growth and AI cohost external load. No EventPipe/trace attached to this process. Thermal and power state not measured; not Android or exclusive physical-host CPU latency.'};save(d)
 with L.open('wb') as f:
  child=subprocess.Popen(cmd,cwd=W,stdin=subprocess.DEVNULL,stdout=f,stderr=subprocess.STDOUT);d['pid']=child.pid;save(d);code=child.wait()
 d.update(exitCode=code,endedUtc=now(),status='completed' if code==0 else 'failed',sourceCommitAtEnd=git('rev-parse','HEAD'),sourceWorktreeCleanAtEnd=not git('status','--porcelain'))
 if R.exists():d['reportSha256']=hashlib.sha256(R.read_bytes()).hexdigest();raw=json.loads(R.read_text());d['measurementSummary']={k:raw.get(k) for k in ['complete','completedSizes','inProgress','totalElapsedSeconds']}
 d['assemblies']=[]
 for base,name in [('src/Chronicles.Core','Chronicles.Core'),('src/Chronicles.Game','Chronicles.Game'),('src/Chronicles.Hosting','Chronicles.Hosting'),('tests/Chronicles.Game.Tests','Chronicles.Game.Tests')]:
  q=W/base/'bin/Release/net10.0'/(name+'.dll');a=W/base/'obj/Release/net10.0'/(name+'.AssemblyInfo.cs');d['assemblies'].append({'path':str(q),'sha256':hashlib.sha256(q.read_bytes()).hexdigest() if q.exists() else None,'bytes':q.stat().st_size if q.exists() else None,'generatedInformationalVersionLines':[x.strip() for x in a.read_text().splitlines() if 'AssemblyInformationalVersion' in x] if a.exists() else []})
 save(d)
