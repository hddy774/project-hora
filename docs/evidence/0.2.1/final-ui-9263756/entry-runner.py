import subprocess,pathlib,json,sys,datetime
cases=[('normal-1280',1280,1),('normal-390',390,1),('text200-1280',1280,2),('text200-390',390,2)]
for name,width,scale in cases:
 out='/workspace/scratch/ui-final-9263756-entry-'+name
 print(datetime.datetime.now(datetime.timezone.utc).isoformat(), 'START',name,flush=True)
 with open(out+'.log','w') as log:
  result=subprocess.run([sys.executable,'/workspace/scratch/ui-final-9263756-entry.py',out,'--width',str(width),'--scale',str(scale)],stdout=log,stderr=subprocess.STDOUT)
 if result.returncode:print('FAIL',name,flush=True);sys.exit(result.returncode)
 row=json.loads(pathlib.Path(out+'/results.json').read_text());assert row['passed']
 print(datetime.datetime.now(datetime.timezone.utc).isoformat(),'PASS',name,'console_errors',len(row.get('console_errors',[])),flush=True)
