import datetime,hashlib,json,os,pathlib,signal,time
ROOT=pathlib.Path('/workspace/scratch');PRIMARY=ROOT/'growth-41111524034-7ce6694';OFFICIAL=PRIMARY/'official-aggregate/out/growth-aggregate.json';SOURCE='9263756b1d00b8696fddecacb077f55d95141f26'
EXPECTED_OFFICIAL='bd592d544766dfff2ba39ece2a531513610ccca3b0c24b86061e9d9b0626e0ab';EXPECTED_VERIFY='c74f85bd5c1aab95d403498039c1c400cab658618478c834608903a0d59196a0'
def utc():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert digest(OFFICIAL)==EXPECTED_OFFICIAL and digest(PRIMARY/'final-verification.json')==EXPECTED_VERIFY
primary=json.loads(OFFICIAL.read_text());assert primary['complete'] is True and primary['completedCases']==9 and primary['sourceCommit']=='7ce66943b539420c9dc1e3fa8a611daae58020ff'
rows={(c['scenario'],c['seed']):c for c in primary['reports']}
owned=[(42,120460,'-restart1'),(98,120473,'-restart1'),(2026,132980,'')];checks=[];snapshots=[]
for seed,pid,suffix in owned:
 stem=ROOT/f'growth-9263756-seed{seed}{suffix}';path=pathlib.Path(str(stem)+'.json');data=path.read_bytes();r=json.loads(data);assert r['sourceCommit']==SOURCE
 snapshots.append({'seed':seed,'pid':pid,'stem':str(stem),'beforeRawSha256':hashlib.sha256(data).hexdigest(),'rawCompletedCases':r['completedCases'],'rawComplete':r['complete'],'rawRequestedComplete':r['requested_complete'],'lastCase':r.get('runningCase'),'beforeObservedThrough':r['reports'][-1]['observedThroughMonth']})
 for local in r['reports']:
  key=(local['scenario'],seed);full=rows[key];through=local['observedThroughMonth'];expected=[p for p in full['timeSeries'] if p['month']<=through]
  strip=lambda p:{k:v for k,v in p.items() if k not in ('simulationMs','caseElapsedMs')}
  differences=[]
  if [strip(p) for p in local['timeSeries']]!=[strip(p) for p in expected]:differences.append('non-timing time-series prefix')
  for name in ['scenario','selectedDefault','assumption','seed','usedLaws','provisionalPolicies','externalArrivals']:
   if local[name]!=full[name]:differences.append(name)
  for name,m in local['milestoneOutcomes'].items():
   if m['outcome'].startswith('observed') and m!=full['milestoneOutcomes'][name]:differences.append('observed milestone '+name)
  complete=local['status']=='completed'
  if complete:
   def semantic(c):
    return {k:([strip(p) for p in v] if k=='timeSeries' else v) for k,v in c.items() if k not in ('simulationMs','commandMs','caseElapsedMs','timingScope')}
   if semantic(local)!=semantic(full):differences.append('completed cohort non-timing fields')
  has_hash=bool(local.get('finalHash'))
  hash_comparable=has_hash and through==full['observedThroughMonth']
  if hash_comparable and local['finalHash']!=full['finalHash']:differences.append('same-month available finalHash')
  checks.append({'scenario':key[0],'seed':seed,'observedThrough':through,'status':local['status'],'samples':len(local['timeSeries']),'completed':complete,'availableFinalHash':local.get('finalHash'),'hashComparableAtSameMonth':hash_comparable,'hashScope':'GrowthScenarios.cs74 updates this field at every checkpoint; partial hashes cannot be compared to a6000 snapshot.','differences':differences,'same':not differences})
record={'kind':'intentional-supplementary-duplicate-stop-after-verified-primary9','comparisonObservedUtc':utc(),'supplementarySourceCommit':SOURCE,'primarySourceCommit':primary['sourceCommit'],'primaryOfficialAggregateSha256':EXPECTED_OFFICIAL,'primaryFinalVerificationSha256':EXPECTED_VERIFY,'primaryComplete':True,'primaryCases':9,'comparisonScope':'All retained non-timing time-series prefixes, actual laws, stable inputs, already-observed milestones and all available final hashes. Completed cohorts also compare all non-timing cohort fields. Partial future cohort aggregates/unobserved states are not claimed. No world re-execution.','checks':checks,'snapshots':snapshots,'intent':'Root explicitly authorized ending redundant supplementary926 only after equality. Original raw/flags/logs are never rewritten; new primary7ce/long-running tasks are untouched.','terminationSender':'politics owned observer after exact PID/command/path verification','signals':[],'after':[]}
record['initialObserverCorrection']={'record':'/workspace/scratch/growth-9263756-intentional-duplicate-stop-after-primary9.json','signalsSent':0,'reason':'First observer compared partial checkpoint hashes to final6000. That is not a valid same-time comparison; all semantic prefixes and completed hashes matched. Original failed guard record preserved.'}
output=ROOT/'growth-9263756-intentional-duplicate-stop-after-primary9-v2.json'
assert not output.exists()
if not all(c['same'] for c in checks):
 record['result']='mismatch-no-signals-sent';output.write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'result':record['result'],'checks':checks},ensure_ascii=False));raise SystemExit(2)
# Capture all command identities before sending any signal.
identities=[]
for snapshot in snapshots:
 pid=snapshot['pid'];proc=pathlib.Path(f'/proc/{pid}');meta=json.loads(pathlib.Path(snapshot['stem']+'-run.json').read_text());assert meta['pid']==pid and meta['sourceCommit']==SOURCE
 if not proc.exists():identities.append({'pid':pid,'state':'already-absent','signalNeeded':False});continue
 state=proc.joinpath('stat').read_text().rsplit(')',1)[1].split()[0]
 if state=='Z':identities.append({'pid':pid,'state':'already-zombie','signalNeeded':False});continue
 argv=[x.decode() for x in proc.joinpath('cmdline').read_bytes().split(b'\0') if x]
 assert argv[0]=='/workspace/.dotnet/dotnet' and argv[1]=='/workspace/seonro-growth-9263756/tests/Chronicles.Game.Tests/bin/Release/net10.0/Chronicles.Game.Tests.dll'
 assert argv[argv.index('--growth')+1]==snapshot['stem']+'.json' and argv[argv.index('--growth-seed')+1]==str(snapshot['seed'])
 assert os.readlink(proc/'cwd')=='/workspace/seonro-growth-9263756'
 identities.append({'pid':pid,'state':state,'signalNeeded':True,'argv':argv,'cwd':os.readlink(proc/'cwd'),'supervisor':meta['wrapperPid']})
record['identities']=identities;record['stopStartedUtc']=utc();output.write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')
for identity in identities:
 if identity['signalNeeded']:
  os.kill(identity['pid'],signal.SIGTERM);record['signals'].append({'pid':identity['pid'],'signal':'SIGTERM','sentUtc':utc()})
deadline=time.monotonic()+15
while time.monotonic()<deadline:
 statuses=[json.loads(pathlib.Path(s['stem']+'-run.json').read_text())['status'] for s in snapshots]
 if all(status!='running' for status in statuses):break
 time.sleep(.2)
for snapshot in snapshots:
 stem=snapshot['stem'];meta=json.loads(pathlib.Path(stem+'-run.json').read_text());raw=pathlib.Path(stem+'.json');r=json.loads(raw.read_text())
 artifacts=[]
 for path in sorted(ROOT.glob(pathlib.Path(stem).name+'*')):
  if path.is_file():artifacts.append({'path':str(path),'bytes':path.stat().st_size,'sha256':digest(path)})
 record['after'].append({'seed':snapshot['seed'],'pid':snapshot['pid'],'actualSupervisorStatus':meta['status'],'actualSupervisorExitCode':meta.get('exitCode'),'actualSupervisorFinishedUtc':meta.get('finishedUtc'),'rawStatus':r.get('status'),'rawCompletedCases':r['completedCases'],'rawComplete':r['complete'],'rawRequestedComplete':r['requested_complete'],'rawRunningCase':r.get('runningCase'),'rawObservedThrough':r['reports'][-1]['observedThroughMonth'],'rawChangedSinceCompared':digest(raw)!=snapshot['beforeRawSha256'],'artifacts':artifacts})
 if record['after'][-1]['rawChangedSinceCompared']:
  for local in r['reports']:
   expected=[p for p in rows[(local['scenario'],snapshot['seed'])]['timeSeries'] if p['month']<=local['observedThroughMonth']]
   assert [strip(p) for p in local['timeSeries']]==[strip(p) for p in expected], 'new final prefix differs; report root'
record.update(result='owned-supplementary-sigterm-preserved',stoppedObservedUtc=utc(),completedSupplementaryCohorts=sum(a['rawCompletedCases'] for a in record['after']),rawRewrittenByObserver=False,primaryOrOtherProcessSignalsSent=False)
output.write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'result':record['result'],'record':str(output),'sha256':digest(output),'comparisons':[{k:c[k] for k in ['scenario','seed','observedThrough','completed','same']} for c in checks],'signals':record['signals'],'exits':[{k:a[k] for k in ['seed','pid','actualSupervisorStatus','actualSupervisorExitCode','actualSupervisorFinishedUtc','rawCompletedCases','rawComplete','rawRequestedComplete','rawObservedThrough','rawChangedSinceCompared']} for a in record['after']]},ensure_ascii=False))
