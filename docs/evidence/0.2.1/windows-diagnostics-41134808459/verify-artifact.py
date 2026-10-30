#!/usr/bin/env python3
"""Read existing CI bytes only. This does not run a product, test, or retry."""
import json,hashlib,pathlib,datetime
ROOT=pathlib.Path(__file__).resolve().parent
RAW=ROOT/'raw'
SOURCE='35f33db919f910c9d55c42b61b76459a08688d12'
FP='528e62a918ef892ad7cf28c42f7048b7be4a656fddcb0e260f3c586b9c6e891e'
def read(n):return json.loads((RAW/n).read_bytes().decode('utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
request=read('request.json');source=read('source.json')
wrapper=read('diagnostics/wrapper-observation.json');smoke=read('diagnostics/seonro-smoke-result.json')
metadata=json.loads((ROOT/'final-ci-metadata.json').read_text())
assert request['event_pr_head']=='93b28c20b50cfae58de6e2992b1320d295ef06a3'
assert request['run_id']=='41134808459' and request['run_attempt']=='1'
assert request['measured_source']==source['source_commit']==SOURCE
assert request['expected_fingerprint']==source['product_source_fingerprint']==FP
assert source['checkout_clean'] is True
assert (RAW/'sdk.txt').read_bytes().decode('utf-8-sig').strip()=='10.0.403'
assert 'Microsoft.NETCore.App 10.0.14 [' in (RAW/'runtimes.txt').read_bytes().decode('utf-8-sig')
assert wrapper['wait_ms']==45000 and wrapper['timed_out'] is False and wrapper['exit_code']==0
assert wrapper['process_after_start']['product_version']=='0.2.1+'+SOURCE
assert wrapper['collection_errors']==[]
assert smoke['passed'] is True and smoke['actualRenderAndBridge'] is True
assert smoke['version']=='0.2.1' and smoke['host']=='WPF WebView2'
diagnostic=smoke['diagnostic']
for _ in range(4):
    if isinstance(diagnostic,dict):break
    diagnostic=json.loads(diagnostic)
assert isinstance(diagnostic,dict)
assert diagnostic['hasView'] is True and diagnostic['error'] is None
assert diagnostic['connection']=='연결됨' and diagnostic['heading']=='만상의 흐름'
assert diagnostic['source']=='https://seonro.local/index.html'
for copied in wrapper['copied_existing_files']:
    p=RAW/'diagnostics'/copied['name']
    assert p.stat().st_size==copied['bytes'] and sha(p)==copied['sha256']
assert metadata['run']['conclusion']=='success' and metadata['jobs'][0]['conclusion']=='success'
zip_path=ROOT/'artifact13014867456.zip'
assert sha(zip_path)==metadata['artifacts'][0]['digest'].split(':',1)[1]
assert zip_path.stat().st_size==metadata['artifacts'][0]['size_in_bytes']
manifest=[{'path':p.relative_to(RAW).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(RAW.rglob('*')) if p.is_file()]
(ROOT/'raw-file-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
began=datetime.datetime.fromisoformat(wrapper['started_utc']);ended=datetime.datetime.fromisoformat(wrapper['ended_utc'])
result={'schema':1,'verified':True,'run_id':41134808459,'job_id':124334521684,'workflow_head':request['event_pr_head'],'workflow_event_merge':request['event_sha'],'actual_source':SOURCE,'product_source_fingerprint':FP,'source_clean':True,'sdk':'10.0.403','runtime_10_0_14_in_installed_list':True,'image':wrapper['image'],'region':'eastus','native_smoke':{'wait_ms':45000,'timed_out':False,'exit_code':0,'wrapper_elapsed_seconds':(ended-began).total_seconds(),'passed':True,'actualRenderAndBridge':True,'diagnostic':diagnostic,'raw_sha256':sha(RAW/'diagnostics/seonro-smoke-result.json')},'webview_installed_executables':wrapper['webview_before'],'webview_processes':wrapper['webview_processes'],'stdout_bytes':(RAW/'diagnostics/stdout.txt').stat().st_size,'stderr_bytes':(RAW/'diagnostics/stderr.txt').stat().st_size,'artifact':metadata['artifacts'][0],'files':manifest,'limits':['이번 실행은 이전 이미지20261021.278.1이었다. 새 이미지20261030.294.1의 실패 재현이 아니다.','94/215의 45초 timeout 실제 원인은 여전히 불명이다.','Edge 공개 목록을 WebView2 버전으로 대체하지 않았고 실제 설치 및 실행 exe155.0.4283.62를 관측했다.','추가 설치/업데이트/pin/rerun/새 앱 실행 없음.','전체 기능/성능, 공식 보유 인증서/동일 APK API24+35/0.2.0 업데이트/게시 판정과 별개다.']}
p=ROOT/'verification.json';p.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'path':str(p),'sha256':sha(p),'verified':True,'image':wrapper['image'],'smoke':result['native_smoke'],'raw_files':len(manifest)},ensure_ascii=False))
