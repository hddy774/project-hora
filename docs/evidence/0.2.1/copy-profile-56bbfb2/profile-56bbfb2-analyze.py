import collections,json,pathlib
root=pathlib.Path('/workspace/scratch');prefix='profile-56bbfb2-json1000';data=json.load(open(root/(prefix+'.speedscope.json')));frames=[f['name'] for f in data['shared']['frames']]
stats={k:dict(total=0.,inclusive=collections.Counter(),exclusive=collections.Counter(),windows=[],groups=collections.defaultdict(collections.Counter)) for k in ('all','warmup_advance','measured_advance','other')}
for p in data['profiles']:
 stack=[];prev=None
 for e in p['events']:
  at=e['at']
  if prev is not None and at>prev and stack:
   w=at-prev;n=[frames[i] for i in stack];advance=any('GameWorld.AdvanceTo(' in v for v in n);warmup=any('PerformanceScenarios.Warmup(' in v for v in n)
   measured=any('PerformanceScenarios.MeasurePopulation(' in v for v in n)
   phase='warmup_advance' if advance and warmup else 'measured_advance' if advance and measured else 'other'
   for key in ('all',phase):
    s=stats[key];s['total']+=w;s['exclusive'][next((v for v in reversed(n) if v not in ('CPU_TIME','UNMANAGED_CODE_TIME')),n[-1])]+=w
    for v in set(n):s['inclusive'][v]+=w
    if s['windows'] and s['windows'][-1][1]==prev:s['windows'][-1][1]=at
    else:s['windows'].append([prev,at])
    for label,terms in {'step_children':('GameWorld.Step(',),'sync_children':('SynchronizeWorldActions(',),'copy_parents':('InventoryState.Copy(',),'validation_parents':('InventoryEngine.Validate(',),'json_any':('System.Text.Json','JsonSerializer'),'sync_any':('SynchronizeWorldActions(',),'validation_any':('InventoryEngine.Validate(','InventoryEngine.ValidateCore(','ValidationReadIndex'),'knowledge_clone_any':('KnowledgeAuthority.Clone(',), 'knowledge_save_any':('GameWorld.SaveKnowledge(',), 'knowledge_mirror_any':('GameWorld.MirrorKnowledge(',), 'politics_context_any':('GameWorld.PoliticsContext(',), 'cultivation_context_any':('GameWorld.CultivationContextFor(',), 'talent_stamp_any':('GameWorld.ExecutionTalentStamp(',), 'people_store_any':('GameWorld.StorePeopleProfile(',)}.items():
     ix=next((i for i,v in enumerate(n) if any(term in v for term in terms)),None)
     if ix is None:continue
     if label.endswith('_any'):s['groups'][label]['matching descendant frame, deduplicated']+=w
     elif label in ('copy_parents','validation_parents'):s['groups'][label][n[ix-1] if ix else '<root>']+=w
     else:s['groups'][label][next((v for v in n[ix+1:] if v not in ('CPU_TIME','UNMANAGED_CODE_TIME')),'<self>')]+=w
  if e['type']=='O':stack.append(e['frame'])
  else:
   assert stack[-1]==e['frame'];stack.pop()
  prev=at
# Attribute JSON descendants to recognized owning call sites, preserving inlining caveats.
out={}
for k,s in stats.items():
 total=s['total']
 def rows(c):return [dict(frame=n,weighted_thread_ms=w,percent=100*w/total) for n,w in c.most_common()]
 out[k]={'weighted_thread_ms':total,'windows_ms':s['windows'],'inclusive':rows(s['inclusive']),'exclusive':rows(s['exclusive']),'callsites':{label:rows(c) for label,c in s['groups'].items()}}
(root/(prefix+'-weighted-stacks.json')).write_text(json.dumps(out,ensure_ascii=False,indent=2))
s=out['measured_advance'];print('MEASURED ADVANCE weighted_thread_ms',s['weighted_thread_ms'],'windows',s['windows_ms']);print('inclusive source top20')
for x in [r for r in s['inclusive'] if 'Chronicles.Game!' in r['frame']][:20]:print(f"{x['percent']:7.3f}% {x['weighted_thread_ms']:10.1f}ms {x['frame']}")
print('exclusive top20')
for x in s['exclusive'][:20]:print(f"{x['percent']:7.3f}% {x['weighted_thread_ms']:10.1f}ms {x['frame']}")
for label,r in s['callsites'].items():
 print(label)
 for x in r[:12]:print(f"{x['percent']:7.3f}% {x['weighted_thread_ms']:10.1f}ms {x['frame']}")
