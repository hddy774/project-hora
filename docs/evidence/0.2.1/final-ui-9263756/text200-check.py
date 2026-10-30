import hashlib,json,pathlib,sys,traceback,subprocess
script=pathlib.Path('/workspace/seonro-final-ui-9263756/scripts/verify-v021-ui.py')
out=pathlib.Path(sys.argv[1]);out.mkdir(parents=True,exist_ok=True)
base_url=sys.argv[2] if len(sys.argv)>2 else 'http://127.0.0.1:5227'
zoom_records=[];diagnostics=[];console_errors=[]
SCALE=r'''() => {
 const selectors=['body','h1','#run','nav button','h2'];
 const before=selectors.map(selector=>({selector,size:parseFloat(getComputedStyle(document.querySelector(selector)).fontSize)}));
 let count=0; const seen=new WeakSet();
 function visit(rules){for(const rule of Array.from(rules)){
  if(rule.cssRules)visit(rule.cssRules);
  if(!rule.style||seen.has(rule))continue;seen.add(rule);
  const value=rule.style.getPropertyValue('font-size').trim();
  if(/^\d+(?:\.\d+)?px$/.test(value)){rule.style.setProperty('font-size',(parseFloat(value)*2)+'px',rule.style.getPropertyPriority('font-size'));count++;}
 }}
 for(const sheet of Array.from(document.styleSheets))visit(sheet.cssRules);
 const samples=before.map(x=>({...x,after:parseFloat(getComputedStyle(document.querySelector(x.selector)).fontSize)}));
 if(count<30||samples.some(x=>Math.abs(x.after/x.size-2)>.001))throw new Error('Text font sizes did not double: '+JSON.stringify({count,samples}));
 window.__seonroTextZoom200={width:innerWidth,count,samples};
 return {...window.__seonroTextZoom200,devicePixelRatio,method:'test-only CSSOM absolute-px font-size multiplier2; inherited values retained'};
}'''
def apply_text_scale(page,width):
    result=page.evaluate(SCALE);assert result['width']==width;zoom_records.append(result)
    page.screenshot(path=str(out/f'initial-text200-{width}.png'))

def verify_text_scale(page):
    result=page.evaluate('''() => {const expected=window.__seonroTextZoom200;if(!expected)return null;return {width:innerWidth,samples:expected.samples.map(x=>({...x,current:document.querySelector(x.selector)?parseFloat(getComputedStyle(document.querySelector(x.selector)).fontSize):null}))};}''')
    if result:
        assert all(x['current'] is None or abs(x['current']/x['size']-2)<.001 for x in result['samples']),result

def capture_overflow(page,overflow):
    data=page.evaluate('''() => ({width:innerWidth,scrollX,body:document.body.scrollWidth,root:document.documentElement.scrollWidth,elements:Array.from(document.querySelectorAll('body *')).map(e=>{const r=e.getBoundingClientRect();return {tag:e.tagName,id:e.id,cls:String(e.className||''),text:(e.innerText||'').slice(0,100),left:r.left,right:r.right,width:r.width,height:r.height,overflow:getComputedStyle(e).overflowX};}).filter(e=>e.width>0&&e.height>0&&(e.left < -1||e.right>innerWidth+1)).slice(0,100)})''')
    diagnostics.append(data);(out/f'overflow-{data["width"]}.json').write_text(json.dumps(data,ensure_ascii=False,indent=2))
    page.screenshot(path=str(out/f'overflow-text200-{data["width"]}.png'))
    page.screenshot(path=str(out/f'overflow-text200-full-{data["width"]}.png'),full_page=True)

def zoom_touch_navigation(page):
    from playwright.sync_api import expect
    touched=[]
    for label in ('지도','세계'):
        button=page.get_by_role('navigation',name='주 메뉴').get_by_role('button',name=label,exact=True)
        button.tap();page.evaluate('() => queue');verify_text_scale(page)
        rect=button.bounding_box();assert rect and rect['width']>=44 and rect['height']>=44,rect
        touched.append({'label':label,'tap':True,'rect':rect})
    zoom_records[-1]['actual_touch_navigation']=touched

original=script.read_text();code=original
console_hook='page = context.new_page()'
assert code.count(console_hook)==1
code=code.replace(console_hook,console_hook+";page.on('console',lambda m:console_errors.append(dict(text=m.text,location=m.location)) if m.type=='error' else None)")
hook="        page.get_by_role('heading', name='만상의 흐름', exact=True).wait_for()\n"
assert code.count(hook)==2
code=code.replace(hook,hook+'        apply_text_scale(page,width)\n',1)
ctx="context = browser.new_context(viewport={'width': width, 'height': 900})"
assert code.count(ctx)==1
code=code.replace(ctx,"context = browser.new_context(viewport={'width': width, 'height': 900},has_touch=True)")
hook="        page.evaluate(\"() => issueCommand('law',0,0,'birth.enabled',0)\")\n"
assert code.count(hook)==1
code=code.replace(hook,hook+'        zoom_touch_navigation(page)\n')
hook="def assert_no_horizontal_overflow(page):\n"
assert code.count(hook)==1
code=code.replace(hook,hook+'    verify_text_scale(page)\n')
hook="    assert not overflow['document'] and not overflow['body'], overflow\n"
assert code.count(hook)==1
code=code.replace(hook,"    if overflow['document'] or overflow['body']:\n        capture_overflow(page,overflow)\n"+hook)
sys.argv=[str(script),str(out),'--url',base_url,'--compat','--legacy-dom']
error=None
try:
    exec(compile(code,str(script),'exec'),dict(globals(),__file__=str(script),__name__='__main__'))
    assert {x['width'] for x in zoom_records}=={1280,390}
except BaseException as ex:
    error=str(ex);raise
finally:
    report={'kind':'real-session-text-scale-functional','passed':error is None,'source_commit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=script.parents[1],text=True).strip(),'original_verifier_sha256':hashlib.sha256(original.encode()).hexdigest(),'harness_sha256':hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(),'viewport_widths_requested':[1280,390],'font_multiplier':2,'method':'test-only CSSOM px font-size declarations including media/shorthand; no device-scale substitution','standard_verifier_unchanged_on_disk':True,'in_memory_test_adaptations':['font-size scaling and persistence assertions','touch-capable contexts and actual navigation taps','overflow diagnostic capture'],'records':zoom_records,'error':error,'overflow_diagnostics':diagnostics,'console_errors':console_errors,'native_api24_verified':False,'performance_measurement':False}
    (out/'text-scale-results.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
