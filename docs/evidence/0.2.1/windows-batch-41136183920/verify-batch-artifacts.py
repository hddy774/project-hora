#!/usr/bin/env python3
"""Validate the already finished three artifacts, preserving the failed gate."""
from pathlib import Path
import json,hashlib,datetime,re
ROOT=Path(__file__).resolve().parent
SOURCE='35f33db919f910c9d55c42b61b76459a08688d12'
FP='528e62a918ef892ad7cf28c42f7048b7be4a656fddcb0e260f3c586b9c6e891e'
HEAD='190e016b7d996befc7d94fd095d14e4a75cb94be'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
dt=lambda s:datetime.datetime.fromisoformat(s.replace('Z','+00:00'))
metadata=json.loads((ROOT/'final-ci-metadata.json').read_text())
assert metadata['run']['id']==41136183920 and metadata['run']['run_attempt']==1
assert metadata['run']['head_sha']==HEAD and metadata['run']['status']=='completed' and metadata['run']['conclusion']=='failure'
assert len(metadata['jobs'])==3 and len(metadata['artifacts'])==3
assert len({j['runner_name'] for j in metadata['jobs']})==3
records=[];manifests={}
for sample in [1,2,3]:
    raw=ROOT/f'sample{sample}/raw'
    read=lambda n:json.loads((raw/n).read_bytes().decode('utf-8-sig'))
    request=read('request.json');source=read('source.json');obs=read('diagnostics/wrapper-observation.json')
    job=next(j for j in metadata['jobs'] if j['name'].endswith(' '+str(sample)))
    artifact=next(a for a in metadata['artifacts'] if a['name'].endswith('-sample'+str(sample)))
    archive=ROOT/f'sample{sample}.zip'
    assert archive.stat().st_size==artifact['size_in_bytes'] and sha(archive)==artifact['digest'].split(':',1)[1]
    assert request['sample']==sample and request['planned_samples']==3 and request['event_pr_head']==HEAD
    assert request['run_id']=='41136183920' and request['run_attempt']=='1'
    assert request['measured_source']==source['source_commit']==SOURCE
    assert request['expected_fingerprint']==source['product_source_fingerprint']==FP and source['checkout_clean'] is True
    assert (raw/'sdk.txt').read_bytes().decode('utf-8-sig').strip()=='10.0.403'
    assert 'Microsoft.NETCore.App 10.0.14 [' in (raw/'runtimes.txt').read_bytes().decode('utf-8-sig')
    assert obs['wait_ms']==45000 and obs['process_after_start']['product_version']=='0.2.1+'+SOURCE
    for copied in obs['copied_existing_files']:
        p=raw/'diagnostics'/copied['name'];assert p.stat().st_size==copied['bytes'] and sha(p)==copied['sha256']
    report=raw/'diagnostics/seonro-smoke-result.json';native=read('diagnostics/seonro-smoke-result.json') if report.exists() else None
    diagnostic=native['diagnostic'] if native else None
    for _ in range(4):
        if not isinstance(diagnostic,str):break
        diagnostic=json.loads(diagnostic)
    if native:
        assert native['version']=='0.2.1' and native['host']=='WPF WebView2'
        assert diagnostic['source']=='https://seonro.local/index.html'
    if sample in [1,2]:
        assert job['conclusion']=='success' and obs['timed_out'] is False and obs['exit_code']==0
        assert native['passed'] is True and native['actualRenderAndBridge'] is True
        assert diagnostic['hasView'] is True and diagnostic['error'] is None and diagnostic['connection']=='연결됨'
    else:
        # A late true JSON cannot turn the expired process-wait gate into success.
        assert job['conclusion']=='failure' and obs['timed_out'] is True and obs['exit_code'] is None
        assert obs['wrapper_error']=='Desktop smoke timeout'
        assert native['passed'] is True and native['actualRenderAndBridge'] is True and diagnostic['error'] is None
    region=next((x.split('Region: ',1)[1].strip() for x in (ROOT/f'sample{sample}-job.log').read_text(encoding='utf-8-sig').splitlines()[:25] if 'Region: ' in x),None)
    copied_report=next((x for x in obs['copied_existing_files'] if x['name']=='seonro-smoke-result.json'),None)
    report_utc=copied_report['last_write_utc'] if copied_report else None
    late_seconds=(dt(report_utc)-dt(obs['started_utc'])).total_seconds() if report_utc else None
    images=[]
    for capture in obs.get('windows_before_kill',[]):
        if 'name' in capture:
            p=raw/'diagnostics'/capture['name'];assert sha(p)==capture['sha256'];images.append(capture)
    manifest=[{'path':p.relative_to(raw).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(raw.rglob('*')) if p.is_file()]
    manifests[str(sample)]=manifest
    records.append({'sample':sample,'job_id':job['id'],'runner':job['runner_name'],'region':region,'gate_conclusion':job['conclusion'],'actual_source':SOURCE,'product_source_fingerprint':FP,'checkout_clean':True,'sdk':'10.0.403','image':obs['image'],'webview_before':obs['webview_before'],'webview_before_kill':obs.get('webview_before_kill',[]),'wait_ms':45000,'timed_out':obs['timed_out'],'exit_code':obs['exit_code'],'process_before_kill':obs.get('process_before_kill'),'native_report':{'exists':bool(native),'passed':native.get('passed') if native else None,'actualRenderAndBridge':native.get('actualRenderAndBridge') if native else None,'diagnostic':diagnostic,'last_write_utc':report_utc,'seconds_after_wrapper_start':late_seconds,'sha256':sha(report) if report.exists() else None,'accepted_by_gate':job['conclusion']=='success'},'collection_errors':obs['collection_errors'],'screenshots':images,'stdout_bytes':(raw/'diagnostics/stdout.txt').stat().st_size,'stderr_bytes':(raw/'diagnostics/stderr.txt').stat().st_size,'artifact':artifact,'raw_files':len(manifest),'job_log_sha256':sha(ROOT/f'sample{sample}-job.log')})
result={'schema':1,'artifact_data_validated':True,'run_id':41136183920,'workflow_head':HEAD,'actual_source':SOURCE,'product_source_fingerprint':FP,'fixed_samples':[1,2,3],'all_three_preserved':True,'batch_gate_conclusion':'failure','samples':records,'additional_batch_or_rerun':False,'limits':['첫93b28 단일 실행과 이 배치를 합쳐 성공률을 주장하지 않는다.','sample3의 늦은 true JSON은 timeout 실패를 성공으로 바꾸지 않는다.','sample2의 새 이미지 성공과 sample3의 같은 이미지 실패를 모두 관측하여 이미지 자체의 항상 실패/명백한 버전 비호환으로 단정할 수 없다.','sample3 지연의 상세 원인은 불명이며 기존94/215의 같은 기전이라고 확정하지 않는다.','sample3 PNG0/visible창없음 진단 오류를 그대로 보존한다.','공식60000ms/release서명/동일APK API24·35/0.2.0 업데이트/기능·성능 전체판정은 별도이다.']}
(ROOT/'raw-file-manifests.json').write_text(json.dumps(manifests,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
p=ROOT/'verification.json';p.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'verification':str(p),'sha256':sha(p),'artifact_data_validated':True,'batch_gate_conclusion':'failure','samples':[{'sample':r['sample'],'image':r['image']['ImageVersion'],'region':r['region'],'gate':r['gate_conclusion'],'webview':[x.get('file_version',x.get('version')) for x in r['webview_before']],'native_report_seconds_after_wrapper_start':r['native_report']['seconds_after_wrapper_start'],'native_report_accepted':r['native_report']['accepted_by_gate'],'collection_errors':r['collection_errors']} for r in records]},ensure_ascii=False))
