import hashlib,json,os,pathlib,shutil,subprocess,datetime
root=pathlib.Path('/workspace/scratch/json-decode-utf8')
probe=root/'probe/bin/Release/net10.0'
old=pathlib.Path('/workspace/scratch/stability-41163711116-c91/raw/assemblies')
candidate=root/'build/bin/Chronicles.Game.Tests/release'
provenance={'started_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'cpu':1,'wall_limit_per_process_seconds':120,'processes':[],'formal_t04_acceptance':False,'new_world_months':0}
for label,source in [('old660',old),('candidate',candidate)]:
    target=root/label
    target.mkdir(exist_ok=True)
    for path in probe.iterdir():
        if path.is_file():shutil.copy2(path,target/path.name)
    for name in ['Chronicles.Game','Chronicles.Core','Chronicles.Hosting','Chronicles.Game.Tests']:
        shutil.copy2(source/(name+'.dll'),target/(name+'.dll'))
def run(label,mode,out):
    directory=root/label
    command=['taskset','-c','1','/workspace/.dotnet/dotnet',str(directory/'DecodeProbe.dll'),mode,str(root/'inputs'),str(root/out)]
    started=datetime.datetime.now(datetime.timezone.utc).isoformat()
    (root/out).parent.mkdir(parents=True,exist_ok=True)
    with open(root/(out+'.log'),'w') as log:
        completed=subprocess.run(command,cwd=directory,stdout=log,stderr=subprocess.STDOUT,timeout=120)
    row={'label':label,'mode':mode,'command':command,'started_utc':started,'ended_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'exit_code':completed.returncode}
    provenance['processes'].append(row)
    (root/'run.json').write_text(json.dumps(provenance,indent=2)+'\n')
    if completed.returncode:raise RuntimeError(row)
run('old660','generate','inputs/manifest.json')
for label in ['old660','candidate']:
    run(label,'oracle',label+'-oracle.json')
left=json.loads((root/'old660-oracle.json').read_text())
right=json.loads((root/'candidate-oracle.json').read_text())
assert left['Runtime']==right['Runtime']=='.NET 10.0.14'
assert json.loads(left['RuntimeConfig'])['runtimeOptions']['configProperties']['System.Runtime.TieredPGO'] is False
assert json.loads(right['RuntimeConfig'])['runtimeOptions']['configProperties']['System.Runtime.TieredPGO'] is False
assert left['TieredPgoEnvironment']==right['TieredPgoEnvironment'] is None
assert left['ComPlusTieredPgoEnvironment']==right['ComPlusTieredPgoEnvironment'] is None
comparison={'scope':'separate-process exact outcome oracle','count':len(left['Rows']),'rows_equal':left['Rows']==right['Rows'],'differences':[{'old':a,'candidate':b}for a,b in zip(left['Rows'],right['Rows'])if a!=b],'old_source':left['GameSource'],'candidate_source':right['GameSource']}
(root/'oracle-comparison.json').write_text(json.dumps(comparison,ensure_ascii=False,indent=2)+'\n')
if comparison['differences']:raise RuntimeError('oracle mismatch, memory diagnostic not started')
for label in ['old660','candidate']:
    run(label,'memory',label+'-memory.json')
provenance['ended_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
provenance['oracle_rows_equal']=True
(root/'run.json').write_text(json.dumps(provenance,indent=2)+'\n')
print(json.dumps({'oracle':comparison,'memory_observation_completed':True},ensure_ascii=False))
