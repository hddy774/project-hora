import argparse,ast,json,pathlib,traceback,subprocess
from playwright.sync_api import sync_playwright
p=argparse.ArgumentParser();p.add_argument('output');p.add_argument('--width',type=int,default=1280);p.add_argument('--scale',type=int,default=1);a=p.parse_args();out=pathlib.Path(a.output);out.mkdir(parents=True,exist_ok=True);root=pathlib.Path('/workspace/seonro-final-ui-9263756')
def literal(path,name):
 for n in ast.parse(path.read_text()).body:
  if isinstance(n,ast.Assign) and any(isinstance(v,ast.Name) and v.id==name for v in n.targets):return ast.literal_eval(n.value)
legacy=literal(root/'scripts/verify-v021-ui.py','LEGACY_APIS');scale=literal(pathlib.Path('/workspace/scratch/ui-final-9263756-text200.py'),'SCALE');commands=[];dialog_checks=[];stage='initial';result={'kind':'real-session-entry-prepare','width':a.width,'font_multiplier':a.scale,'source_commit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'passed':False}
with sync_playwright() as pw:
 browser=pw.chromium.launch(headless=True,args=['--no-sandbox']);ctx=browser.new_context(viewport={'width':a.width,'height':900},has_touch=True);ctx.add_init_script('('+legacy+')()')
 ctx.route('**/*',lambda route:route.fulfill(status=200,content_type='text/html; charset=utf-8',body=(root/'src/Chronicles.Shell/index.compat.html').read_text()) if route.request.resource_type=='document' else route.continue_());page=ctx.new_page();errors=[];console_errors=[];page.on('pageerror',lambda e:errors.append(str(e)));page.on('console',lambda m:console_errors.append(dict(text=m.text,location=m.location)) if m.type=='error' else None)
 def record(req):
  if req.method=='POST' and req.url.endswith('/api'):
   body=req.post_data_json
   if body.get('type')=='command':commands.append(body['command'])
 page.on('request',record)
 def settle():page.evaluate('() => queue')
 def cmd(kind,target=0,value=0,data='',who=0,accepted=True):
  r=page.evaluate('(x)=>issueCommand(x.kind,x.target,x.value,x.data,x.who)',dict(kind=kind,target=target,value=value,data=data,who=who));assert r['result']['accepted'] is accepted,r;return r
 def own():return page.evaluate('() => view.detail')
 def show():
  page.evaluate('() => inspect(view.actor)');page.get_by_role('navigation',name='주 메뉴').get_by_role('button',name='수행',exact=True).click();settle()
 def submit(accepted):
  button=page.get_by_role('dialog').get_by_role('button',name='준비 명령 제출',exact=True)
  geometry=page.evaluate("() => ({root:document.documentElement.scrollWidth,body:document.body.scrollWidth,viewport:innerWidth,targets:['plannedPath','plannedSupport','confirm'].map(id=>{const r=document.getElementById(id).getBoundingClientRect();return {id,width:r.width,height:r.height};})})");assert geometry['root']<=a.width and geometry['body']<=a.width,geometry;assert all(x['width']>=44 and x['height']>=44 for x in geometry['targets']),geometry
  if accepted:button.tap()
  else:
   page.locator('#plannedPath').focus();page.keyboard.press('Tab');assert page.evaluate('() => document.activeElement.id')=='plannedSupport';page.keyboard.press('Tab');assert page.evaluate('() => document.activeElement.textContent')=='닫기';page.keyboard.press('Tab');assert page.evaluate('() => document.activeElement.id')=='confirm';page.keyboard.press('Enter')
  dialog_checks.append(dict(geometry=geometry,submission='touch tap' if accepted else 'keyboard Tab and Enter'));settle();r=page.evaluate('() => recentCommand');assert r['phase']=='confirmed' and r['accepted'] is accepted,r;return r
 def prepare(accepted):
  page.get_by_role('button',name='돌파 준비 계획',exact=True).click();page.get_by_role('dialog').wait_for();options=page.locator('#plannedPath option').all_text_contents();assert len(options)==1 and '입문 감응 시도' in options[0],options
  assert page.get_by_role('dialog').get_by_text('연기 운기 공법은 입문 후 별도로 배워야 합니다.',exact=False).is_visible();page.locator('#plannedPath').select_option('path.1.0');page.locator('#plannedSupport').select_option('0');r=submit(accepted);request=commands[-1];assert request['kind']=='cultivationprepare' and request['target']==0 and request['data']=='path.1.0' and request['actor']==pupil,request
  if page.get_by_role('dialog').count() and page.get_by_role('dialog').is_visible():page.keyboard.press('Escape')
  return r
 try:
  page.goto('http://127.0.0.1:5227');page.get_by_role('heading',name='만상의 흐름',exact=True).wait_for();settle()
  if a.scale==2:result['font_verification']=page.evaluate(scale)
  page.evaluate('() => send({type:"new",seed:42,useCurrentLaws:false})');cmd('lawBatch',data=json.dumps({'birth.enabled':0,'ecology.foodUse':0,'ecology.disasterChance':0,'knowledge.misreadChance':0,'cultivation.entry':1,'cultivation.qi':2,'cultivation.foundation':2,'cultivation.breakthroughRisk':0},separators=(',',':')))
  stage='public arrival';cmd('create',data='입문 화면 도제');teacher=page.evaluate('() => view.people[0].id');pupil=page.evaluate('() => view.people.find(p=>p.name==="입문 화면 도제").id');cmd('possess',who=pupil);show();assert not own()['potential']['knownNextRoutes'];page.get_by_role('button',name='1개월',exact=True).click();settle();cmd('observe',who=pupil)
  teacher_action=page.evaluate('(id)=>view.people.find(p=>p.id===id).action',teacher)
  if teacher_action!='Idle':cmd('cancel',who=teacher)
  stage='actual ten-month teaching';cmd('teach',target=pupil,value=10,data='entry',who=teacher);cmd('possess',who=pupil);before_month=page.evaluate('() => view.month');assert page.evaluate('() => activePerson().action')=='Wait';page.evaluate('() => advance(10)');settle();show();assert page.evaluate('() => view.month')==before_month+10
  d=own();assert d['understanding'].get('entry',0)>=1000 and d['understanding'].get('path.1.0',0)==0;assert all(x['workId']!='path.1.0' for x in d['knowledge']['chapters']);routes=d['potential']['knownNextRoutes'];assert len(routes)==1 and routes[0]['path']=='path.1.0';assert routes[0]['missing'] is None and routes[0]['minimumSupplyToStart'] is None and routes[0]['parentQualityCarried'] is None and d['potential']['hasActualRecoverySupply'] is None
  page.get_by_role('heading',name='입문 감응 시도',exact=True).wait_for();stage='incomplete real preparation rejected';result['insufficient_feedback']=prepare(False)['message'];assert own()['person']['realm']=='범인' and own()['cultivation']['phase'] is None
  stage='ordinary practice stays Mortal'
  for i in range(4):
   page.get_by_role('button',name='공법 선택·수행',exact=True).click();assert '입문 토납 준비' in page.locator('#cultivationPath option').inner_text();page.get_by_role('dialog').get_by_role('button',name='명령 제출',exact=True).click();settle();assert page.evaluate('() => recentCommand.accepted') is True;page.get_by_role('button',name='1개월',exact=True).click();settle();show();assert own()['person']['realm']=='범인' and own()['cultivation']['phase'] is None
  stage='ready entry through visible prepare';prepare(True);d=own();assert d['cultivation']['phase']==0 and d['understanding'].get('path.1.0',0)==0
  page.screenshot(path=str(out/'entry-prepared.png'),full_page=True);stage='actual save and restore';page.locator('#slot').select_option('0');page.get_by_role('button',name='저장',exact=True).click();settle();page.get_by_role('button',name='불러오기',exact=True).click();page.get_by_role('dialog').get_by_role('button',name='불러오기',exact=True).click();settle();show();assert page.evaluate('() => view.actor')==pupil and own()['cultivation']['phase']==0
  stage='actual paid formation start';page.get_by_role('button',name='준비한 형성 개시',exact=True).click();settle();assert page.evaluate('() => recentCommand.accepted') is True;assert own()['cultivation']['phase']==1 and own()['understanding'].get('path.1.0',0)==0;assert all(x['workId']!='path.1.0' for x in own()['knowledge']['chapters'])
  overflow=page.evaluate('() => ({root:document.documentElement.scrollWidth,body:document.body.scrollWidth,viewport:innerWidth})');assert overflow['root']<=a.width and overflow['body']<=a.width,overflow;assert not errors,errors;page.screenshot(path=str(out/'entry-started.png'),full_page=True)
  result.update(passed=True,actual_entry_teaching_months=10,actual_ordinary_practice_commands=4,ordinary_practice_did_not_auto_sense=True,entry_only_default_route=True,unobserved_site_fields_unknown=True,actual_incomplete_prepare_rejected=True,actual_ui_target0_data_path10=True,prepared_save_restore=True,actual_start_accepted=True,qi_method_understanding_or_personal_chapter_created=False,prepare_dialog_checks=dialog_checks,document_horizontal_overflow=False,javascript_errors=errors,console_errors=console_errors,commands=commands)
 except BaseException as e:
  result.update(error=str(e),failed_stage=stage,javascript_errors=errors,console_errors=console_errors,commands=commands);page.screenshot(path=str(out/'failure.png'),full_page=True);raise
 finally:
  (out/'results.json').write_text(json.dumps(result,ensure_ascii=False,indent=2));browser.close()
