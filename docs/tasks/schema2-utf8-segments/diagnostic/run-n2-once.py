from pathlib import Path
import subprocess,json,os,time,signal,datetime,hashlib
root=Path('/workspace/seonro-schema2-segments-5f789c8');out=Path('/workspace/scratch/schema2-segments-5f789c8');raw=out/'n2-diagnostic.json';record=out/'n2-run.json';plan=Path('/workspace/seonro-integration-5f789c8/docs/evidence/0.2.1/final-performance/stability-plan.json');freeze=json.load(open(out/'source-freeze.json'));source=freeze['source_commit']
assert not raw.exists() and not record.exists() and not (out/'n2-diagnostic-saves').exists();assert not subprocess.check_output(['git','status','--porcelain'],cwd=root,text=True);assert hashlib.sha256(plan.read_bytes()).hexdigest()==freeze['original_plan_sha256']
for row in freeze['assemblies']:
 p=root/('tests/Chronicles.Game.Tests/bin/Release/net10.0/'+row['name']+'.dll');assert hashlib.sha256(p.read_bytes()).hexdigest()==row['sha256']
cmd=['taskset','-c','3','/workspace/.dotnet/dotnet',str(root/'tests/Chronicles.Game.Tests/bin/Release/net10.0/Chronicles.Game.Tests.dll'),'--filter','INV-001/002/011','--stability',str(raw),'--stability-plan',str(plan)]
r={'source_commit':source,'source_tree':freeze['source_tree'],'product_source_fingerprint':freeze['product_source_fingerprint'],'started_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'command':cmd,'external_diagnostic_cap_seconds':200,'plan_sha256':freeze['original_plan_sha256'],'scope':'Original full plan/harness; stop externally after the first N2 case report, preserve partial whole raw. One authorized diagnostic only.','final_gate_claim':False,'poll_seconds':0.1}
start=time.monotonic();reason=None
with (out/'n2-game.log').open('w') as log:
 p=subprocess.Popen(cmd,cwd=root,env={**os.environ,'DOTNET_ROOT':'/workspace/.dotnet'},stdout=log,stderr=subprocess.STDOUT,start_new_session=True);r['pid']=p.pid;record.write_text(json.dumps(r,indent=2)+'\n')
 try:
  while p.poll() is None:
   if time.monotonic()-start>=200:reason='fixed 200-second external cap';break
   try:
    result=json.loads(raw.read_text())
    if any(t.get('population')==2 for t in result.get('reports',[])):
     reason='N2 case report written; deliberately stop before later cases complete';break
   except (FileNotFoundError,json.JSONDecodeError):pass
   time.sleep(0.1)
 finally:
  if p.poll() is None:
   os.killpg(p.pid,signal.SIGTERM)
   try:p.wait(timeout=3)
   except subprocess.TimeoutExpired:os.killpg(p.pid,signal.SIGKILL);p.wait()
  r.update({'stop_reason':reason or 'process exited before external stop','process_exit_code':p.returncode,'elapsed_seconds':time.monotonic()-start,'ended_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'source_clean_after':not subprocess.check_output(['git','status','--porcelain'],cwd=root,text=True)})
  if raw.exists():
   b=raw.read_bytes();r['raw_sha256']=hashlib.sha256(b).hexdigest()
   try:
    d=json.loads(b);r.update({'raw_complete':d['complete'],'raw_status':d['status'],'finished_cases':d['finishedCases'],'later_case_report_written':any(t['population']!=2 for t in d['reports'])})
   except json.JSONDecodeError:r['raw_parseable']=False
  record.write_text(json.dumps(r,indent=2)+'\n');print(json.dumps(r),flush=True)
