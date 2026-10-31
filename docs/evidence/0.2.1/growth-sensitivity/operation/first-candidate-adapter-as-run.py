"""Package validated actual9 with unmodified frozen aggregate API; no world/harness execution."""
import datetime,hashlib,importlib.util,json,pathlib,subprocess,sys
SOURCE='7ce66943b539420c9dc1e3fa8a611daae58020ff';ROOT=pathlib.Path('/workspace/seonro-sensitivity-7ce');INPUT=pathlib.Path('/workspace/chronicles-sensitivity-inputs/docs/evidence/0.2.1/growth-sensitivity/inputs')
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()==SOURCE
assert not subprocess.check_output(['git','status','--porcelain'],cwd=ROOT,text=True).strip()
prior_path=INPUT/'baseline.json';prior_bytes=prior_path.read_bytes();prior=json.loads(prior_bytes)
assert prior['complete'] is True and prior['completedCases']==9 and prior['sourceCommit']==SOURCE and prior['productSourceFingerprint']=='4e228e7ba34e538fd1a69abf05934d8162b2a65559ed4ffd50a73b00a8ba2304'
spec=importlib.util.spec_from_file_location('frozen_growth_7ce',ROOT/'scripts/aggregate-v021-growth.py');module=importlib.util.module_from_spec(spec);sys.modules[spec.name]=module;spec.loader.exec_module(module)
paths=[pathlib.Path(f'raw/{scenario}-{seed}.json') for scenario in 'abc' for seed in [42,98,2026]];payloads=[(INPUT/p).read_bytes() for p in paths];documents=[module.read(data) for data in payloads]
laws=module.law_definitions(subprocess.check_output(['git','show',SOURCE+':src/Chronicles.Game/Laws.cs'],cwd=ROOT,text=True))
# Reuse the source fingerprint just verified by exact frozen CLI. No source or validator replacement.
result=module.aggregate(documents,SOURCE,prior['productSourceFingerprint'],laws)
# Same metadata builder as frozen main302..305, with the caller's relative artifact locations at creation.
result['inputArtifacts']=[{'path':str(path),'sha256':hashlib.sha256(payload).hexdigest(),'environment':document['environment'],'cohortKeys':[list(module.key(row)) for row in document['reports']]} for path,payload,document in zip(paths,payloads,documents)]
assert {k:v for k,v in result.items() if k!='inputArtifacts'}=={k:v for k,v in prior.items() if k!='inputArtifacts'}
assert len(result['inputArtifacts'])==len(prior['inputArtifacts'])==9
for old,new in zip(prior['inputArtifacts'],result['inputArtifacts']):
 assert {k:v for k,v in old.items() if k!='path'}=={k:v for k,v in new.items() if k!='path'}
 assert not pathlib.Path(new['path']).is_absolute() and hashlib.sha256((INPUT/new['path']).read_bytes()).hexdigest()==new['sha256']
output=INPUT/'baseline-api-candidate.json'
with output.open('x',encoding='utf-8') as f:f.write(json.dumps(result,ensure_ascii=False,indent=2,allow_nan=False)+'\n')
assert all((INPUT/p).read_bytes()==data for p,data in zip(paths,payloads))
official=pathlib.Path('/workspace/scratch/growth-41111524034-7ce6694/official-aggregate/out/growth-aggregate.json');actual=json.loads(official.read_text());assert result['reports']==actual['reports'] and result['distributions']==actual['distributions']
record={'kind':'portable-aggregate-packaging-proof','sourceCommit':SOURCE,'productSourceFingerprint':result['productSourceFingerprint'],'createdUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'sourceAggregator':'scripts/aggregate-v021-growth.py','entryPoints':['read','law_definitions','aggregate'],'frozenSourceFileSha256':hashlib.sha256((ROOT/'scripts/aggregate-v021-growth.py').read_bytes()).hexdigest(),'priorCliFullValidationPassed':True,'priorCliDerivedAggregateSha256':hashlib.sha256(prior_bytes).hexdigest(),'officialAggregateSha256':hashlib.sha256(official.read_bytes()).hexdigest(),'candidateSha256':hashlib.sha256(output.read_bytes()).hexdigest(),'onlyDifferenceFromFreshValidatedCli':'inputArtifacts[].path: relative package location chosen at new artifact creation; unmodified aggregate/read/law functions and identical other metadata','allCohortRowsEqualOfficial':True,'allDistributionsEqualOfficial':True,'rawBytesUnchanged':True,'rawInputHashesEqual':True,'completedCases':9,'observations':sum(len(r['timeSeries']) for r in result['reports']),'newWorldOrCollectorExecutions':0,'officialOriginalRewritten':False,'candidatePath':str(output)}
p=pathlib.Path('/workspace/scratch/portable-growth-7ce-api-candidate-proof.json');p.write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(record,ensure_ascii=False))
