#!/usr/bin/env python3
"""Functional shell checks against a real GameSession; no profiling or load tests.

Start Chronicles.Shell with --preview, then run this file. An isolated functional
host can be selected with --url. Adapter checks replay real session replies only;
they verify the JavaScript bridge and never count as domain integration evidence.
"""
import argparse
import json
import os
import sys
from pathlib import Path
from playwright.sync_api import sync_playwright

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('output', nargs='?', default='artifacts/ui')
parser.add_argument('--url', default=os.environ.get('SEONRO_UI_BASE_URL', 'http://127.0.0.1:5211'))
parser.add_argument('--smoke-only', action='store_true', help='Current scoped-domain navigation, commands and dialogs only')
parser.add_argument('--compat', action='store_true', help='Serve committed offline Chromium 51 HTML over the real API host')
parser.add_argument('--legacy-dom', action='store_true', help='Force fallback DOM/layout/formatting APIs; requires --compat')
args = parser.parse_args()
if args.legacy_dom and not args.compat:
    parser.error('--legacy-dom requires --compat')
out = Path(args.output)
out.mkdir(parents=True, exist_ok=True)
results = []


def settle(page):
    page.evaluate('() => queue')


def nav(page, label):
    page.get_by_role('navigation', name='주 메뉴').get_by_role('button', name=label, exact=True).click()
    settle(page)


def submit(page, label='명령 제출', accepted=True):
    page.get_by_role('dialog').get_by_role('button', name=label, exact=True).click()
    settle(page)
    result = page.evaluate('() => recentCommand')
    assert result and result['phase'] == 'confirmed', result
    assert result['accepted'] is accepted, result
    return result


def cancel_action(page):
    # Setup cleanup uses the same public command as the visible cancellation UI.
    if page.evaluate("() => activePerson()?.action !== 'Idle'"):
        reply = page.evaluate("() => issueCommand('cancel')")
        assert reply['result']['accepted'], reply['message']


def open_item(page, definition):
    nav(page, '물품')
    row = page.locator('tbody tr').filter(has=page.get_by_text(definition, exact=True)).first
    row.get_by_role('button', name='실물·행동', exact=True).click()


LEGACY_APIS = r'''() => {
  function remove(object, name) { Object.defineProperty(object,name,{value:undefined,writable:true,configurable:true}); }
  ['entries','values','fromEntries','getOwnPropertyDescriptors'].forEach(name=>remove(Object,name));
  remove(Array.prototype,'at'); remove(Element.prototype,'replaceChildren'); remove(DocumentFragment.prototype,'replaceChildren');
  remove(window,'globalThis'); remove(window,'Intl');
  remove(HTMLFormElement.prototype,'reportValidity'); remove(HTMLInputElement.prototype,'reportValidity');
  remove(HTMLDialogElement.prototype,'showModal'); remove(HTMLDialogElement.prototype,'close');
  const supports=CSS.supports.bind(CSS); CSS.supports=(name,value)=>name==='display'&&value==='grid'?false:supports(name,value);
}'''


def smoke_checks(browser):
    asset = Path(__file__).resolve().parents[1] / 'src/Chronicles.Shell/index.compat.html'
    for width in (1280, 390):
        context = browser.new_context(viewport={'width': width, 'height': 900})
        if args.legacy_dom:
            context.add_init_script('(' + LEGACY_APIS + ')()')
        if args.compat:
            def document(route):
                if route.request.resource_type == 'document':
                    route.fulfill(status=200, content_type='text/html; charset=utf-8', body=asset.read_text())
                else:
                    route.continue_()
            context.route('**/*', document)
        page = context.new_page()
        errors = []
        page.on('pageerror', lambda error: errors.append(str(error)))
        page.goto(args.url)
        page.get_by_role('heading', name='만상의 흐름', exact=True).wait_for()
        settle(page)
        page.evaluate("() => send({type:'new',seed:42})")
        assert page.evaluate('() => view.people.length===2 && !view.groups.some(g=>g.kind===\'Sect\')')
        for label in ('인물', '지도', '수행', '물품', '세력', '시장', '연대기', '법칙', '세계'):
            nav(page, label)
            assert not page.evaluate('() => document.body.scrollWidth > innerWidth'), label
        nav(page, '지도')
        sizes = page.locator('.map button').evaluate_all('(nodes)=>nodes.map(n=>({w:n.getBoundingClientRect().width,h:n.getBoundingClientRect().height}))')
        assert len(sizes) == 100 and all(s['w'] >= 44 and s['h'] >= 44 for s in sizes), sizes[:10]
        nav(page, '인물')
        page.get_by_role('searchbox').press_sequentially('청명')
        assert page.locator('tbody tr').count() == 1
        assert page.get_by_role('searchbox').evaluate('(el)=>el===document.activeElement')
        page.get_by_role('button', name='상세', exact=True).click()
        settle(page)
        page.keyboard.press('Alt+ArrowLeft'); settle(page)
        assert page.get_by_role('searchbox').input_value() == '청명'
        page.get_by_role('button', name='상세', exact=True).click(); settle(page)
        page.get_by_role('button', name='이 인물로 플레이', exact=True).click(); settle(page)
        page.evaluate('() => inspect(view.actor)')
        assert page.evaluate('() => !view.observer && view.detail.roots===null && view.detail.people.innate===null && view.detail.combat.length===8')
        assert page.get_by_role('navigation').get_by_role('button', name='법칙', exact=True).count() == 0
        other = page.evaluate('() => view.people.find(p=>p.id!==view.actor).id')
        page.evaluate('(id)=>inspect(id)', other)
        assert page.evaluate('() => view.detail.decision===null && view.detail.knowledge===null && view.detail.person.plan===null')
        page.get_by_role('button', name='이 인물로 플레이', exact=True).click(); settle(page)
        page.evaluate('() => inspect(view.actor)')
        assert page.evaluate('(id)=>view.actor===id && view.detail.person.id===id', other)
        open_item(page, '채집 도구')
        page.get_by_role('button', name='도구 장착 요청', exact=True).click(); settle(page)
        assert page.evaluate('() => recentCommand.accepted')
        open_item(page, '채집 도구')
        page.get_by_role('button', name='장비 해제', exact=True).click(); settle(page)
        assert page.evaluate('() => recentCommand.accepted')
        food = page.evaluate("() => view.items.find(i=>i.definition==='food')")
        open_item(page, '영곡')
        page.get_by_role('button', name='1개 사용', exact=True).click(); settle(page)
        assert page.evaluate('(id)=>item(id).quantity', food['id']) == food['quantity'] - 1
        nav(page, '물품')
        page.get_by_role('button', name='제작 계획', exact=True).click()
        assert page.locator('#recipe option').count() == page.evaluate('() => view.recipes.length') >= 90
        page.get_by_label('제작법 검색').fill('저물대')
        assert page.locator('#recipe option').count() > 0
        revision = page.evaluate('() => view.revision')
        page.keyboard.press('Escape')
        assert not page.locator('#dialog').evaluate('(d)=>d.open')
        assert page.evaluate('() => view.revision') == revision
        nav(page, '시장')
        assert page.locator('[data-section="economy"]').count() == 1
        assert page.locator('[data-section="currency"]').count() == 1
        assert page.evaluate('() => view.economy.regions.length===0 && view.economy.naturalFlows.length===0')
        page.get_by_role('button', name='관찰로 전환', exact=True).first.click(); settle(page)
        nav(page, '인물')
        if page.get_by_role('button', name='인물 목록', exact=True).count():
            page.get_by_role('button', name='인물 목록', exact=True).click(); settle(page)
        page.get_by_role('button', name='명시적 인물 편입', exact=True).click()
        assert page.locator('#newspecies option').count() == 5
        page.get_by_label('이름', exact=True).fill('')
        page.get_by_role('button', name='명령 제출', exact=True).click()
        assert page.locator('#dialog').evaluate('(d)=>d.open')
        page.get_by_label('이름', exact=True).fill('기능 확인 영호')
        page.get_by_label('종과 자연 육신').select_option('spirit-fox')
        submit(page)
        created = page.evaluate("() => view.people.find(p=>p.name==='기능 확인 영호')")
        assert created
        page.evaluate('(id)=>inspect(id)', created['id'])
        assert page.evaluate("() => view.detail.people.actualBody.speciesId==='spirit-fox'")
        nav(page, '법칙')
        changes = page.evaluate('() => Object.fromEntries(view.laws.slice(0,2).map(l=>[l.id,l.value===l.maximum?l.value-1:l.value+1]))')
        page.get_by_role('button', name='값 가져오기', exact=True).click()
        page.get_by_label('내보낸 JSON 값 붙여넣기').fill(json.dumps({'laws': changes}))
        page.get_by_role('button', name='차이 검토', exact=True).click()
        assert page.get_by_role('heading', name='가져올 법칙 차이 검토', exact=True).count() == 1
        submit(page, '검토한 값 일괄 적용')
        assert page.evaluate('(changes)=>Object.entries(changes).every(([id,value])=>view.laws.find(l=>l.id===id).value===value)', changes)
        assert page.evaluate('() => new Set(view.lawHistory.slice(-2).map(h=>h.revision)).size===1')
        nav(page, '세계')
        page.get_by_role('button', name='새 세계', exact=True).click()
        assert page.get_by_label('새 세계의 법칙').input_value() == 'current'
        submit_new = page.get_by_role('button', name='새 세계 만들기', exact=True)
        submit_new.click(); settle(page)
        assert page.evaluate('(changes)=>Object.entries(changes).every(([id,value])=>view.laws.find(l=>l.id===id).value===value)', changes)
        assert page.evaluate('() => view.people.length===2')
        page.get_by_role('button', name='새 세계', exact=True).click()
        page.get_by_label('새 세계의 법칙').select_option('default')
        page.get_by_role('button', name='새 세계 만들기', exact=True).click(); settle(page)
        assert page.evaluate('() => view.laws.every(l=>l.value===l.defaultValue)')
        page.get_by_role('button', name='저장', exact=True).click(); settle(page)
        saved = page.evaluate('() => view.hash')
        page.get_by_role('button', name='불러오기', exact=True).click()
        page.get_by_role('dialog').get_by_role('button', name='불러오기', exact=True).click(); settle(page)
        assert page.evaluate('() => view.hash') == saved
        if args.legacy_dom:
            assert page.evaluate("() => document.documentElement.hasAttribute('data-shell-legacy-grid') && typeof globalThis==='object' && typeof Object.fromEntries==='function' && typeof [].at==='function'")
            assert page.locator('#dialog').get_attribute('data-shell-legacy-dialog') is not None
            assert page.evaluate('() => n(1234567)') == '1,234,567'
        assert page.evaluate('() => window.seonroLastHostError==null'), page.evaluate('() => window.seonroLastHostError')
        assert not errors, errors
        assert not page.evaluate('() => document.body.scrollWidth > innerWidth')
        page.screenshot(path=str(out / f'smoke-{width}.png'), full_page=True)
        results.append({'kind':'real_session_functional_smoke','width':width,'asset':'committed_chrome51' if args.compat else 'authoring',
                        'forced_legacy_dom':args.legacy_dom,'tabs':9,'javascript_errors':errors,'touch_targets_44px':True,
                        'known_actor_switch':True,'equipment_use':True,'species_creation':True,'atomic_law_batch':True,
                        'new_world_law_choice':True,'save_load':True,'native_api24_verified':False})
        context.close()


with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, args=['--no-sandbox'])
    if args.smoke_only:
        smoke_checks(browser)
        browser.close()
        (out / 'results.json').write_text(json.dumps(results, ensure_ascii=False, indent=2)+'\n')
        print(json.dumps(results, ensure_ascii=False))
        sys.exit(0)
    observer_reply = play_reply = None
    for width in (1280, 390):
        page = browser.new_page(viewport={'width': width, 'height': 900})
        errors = []
        page.on('pageerror', lambda error: errors.append(str(error)))
        page.goto(args.url)
        page.get_by_role('heading', name='만상의 흐름', exact=True).wait_for()
        observer_reply = page.evaluate("() => send({type:'new',seed:42})")
        assert len(observer_reply['view']['people']) == 2
        assert not [g for g in observer_reply['view']['groups'] if g['kind'] == 'Sect']
        for label in ('인물', '지도', '수행', '물품', '세력', '시장', '연대기', '법칙', '세계'):
            nav(page, label)
            assert not page.evaluate('() => document.body.scrollWidth > innerWidth'), label
        nav(page, '지도')
        touch_sizes = page.locator('.map button').evaluate_all('(buttons) => buttons.map(b=>({w:b.getBoundingClientRect().width,h:b.getBoundingClientRect().height}))')
        assert len(touch_sizes) == 100
        assert all(b['w'] >= 44 and b['h'] >= 44 for b in touch_sizes), touch_sizes[:10]
        page.locator('[data-section="map-table"]').get_by_text('발견한 지점의 자원·시설 수치 표', exact=True).click()
        page.get_by_role('searchbox').press_sequentially('1주')
        assert page.get_by_role('searchbox').input_value() == '1주'
        assert page.locator('[data-section="map-table"]').get_attribute('open') is not None
        assert page.get_by_role('searchbox').evaluate('(el) => el === document.activeElement')
        page.locator('[data-section="map-table"]').get_by_text('발견한 지점의 자원·시설 수치 표', exact=True).click()
        page.evaluate('() => {document.activeElement.blur();window.scrollTo(0,0)}')
        page.screenshot(path=str(out / f'map-{width}.png'), full_page=True)

        nav(page, '인물')
        search_input = page.get_by_role('searchbox')
        search_input.press_sequentially('청명')
        assert search_input.input_value() == '청명'
        assert search_input.evaluate('(el) => el === document.activeElement')
        assert page.locator('tbody tr').count() == 1
        page.get_by_role('button', name='상세', exact=True).click()
        settle(page)
        page.get_by_role('heading', name='오행 영근 · 0~10000점', exact=True).wait_for()
        page.keyboard.press('Alt+ArrowLeft')
        settle(page)
        assert page.get_by_role('searchbox').input_value() == '청명'
        page.get_by_role('button', name='상세', exact=True).click()
        settle(page)
        page.get_by_role('button', name='이 인물로 플레이', exact=True).click()
        settle(page)
        page.get_by_text('조작 중', exact=True).wait_for()
        assert page.get_by_role('navigation').get_by_role('button', name='법칙', exact=True).count() == 0
        assert page.evaluate('() => view.census.length === 0 && view.laws.length === 0 && view.souls.length === 0')
        play_reply = page.evaluate("() => send({type:'view'})")

        page.get_by_role('button', name='공법 선택·수행', exact=True).click()
        assert page.locator('#cultivationPath option').count() == 3
        chosen_path = page.locator('#cultivationPath option').last.get_attribute('value')
        page.get_by_label('이해한 현재 경지 경로').select_option(chosen_path)
        submit(page)
        assert page.evaluate("() => view.detail.person.action === 'Cultivate'")
        if 'actionData' in page.evaluate('() => view.detail.person'):
            assert page.evaluate('() => view.detail.person.actionData') == chosen_path
        page.get_by_role('button', name='작업 취소 요청', exact=True).click()
        revision = page.evaluate('() => view.revision')
        page.keyboard.press('Escape')
        assert page.evaluate('() => view.revision') == revision
        assert page.evaluate("() => view.detail.person.action === 'Cultivate'")
        page.get_by_role('button', name='작업 취소 요청', exact=True).click()
        submit(page, '취소 요청 제출')
        assert page.evaluate("() => view.detail.person.action === 'Idle'")
        assert '취소 확정' in page.locator('#feedback').inner_text()

        # A request is accepted independently of the other person's eventual choice.
        page.get_by_role('button', name='관계·양육·전수', exact=True).click()
        page.get_by_label('제안할 행동').select_option('requestTeaching')
        page.get_by_label('배움·전수할 지식').select_option('entry')
        submit(page)
        assert page.evaluate("() => activePerson().action === 'Wait'")
        page.get_by_role('button', name='1개월', exact=True).click()
        settle(page)
        cancel_action(page)
        page.get_by_role('button', name='관계·양육·전수', exact=True).click()
        page.get_by_label('제안할 행동').select_option('adopt')
        adoption_revision = page.evaluate('() => view.revision')
        submit(page, accepted=False)
        assert page.evaluate('() => view.revision') == adoption_revision

        # Existing real initial assets exercise use, equipment and book reservation.
        food_id = page.evaluate("() => view.items.find(i=>i.definition==='food').id")
        food_before = page.evaluate('(id) => item(id).quantity', food_id)
        open_item(page, '영곡')
        page.get_by_role('button', name='1개 사용', exact=True).click()
        settle(page)
        assert page.evaluate('(id) => item(id).quantity', food_id) == food_before - 1
        open_item(page, '채집 도구')
        page.get_by_role('button', name='도구 장착 요청', exact=True).click()
        settle(page)
        assert page.evaluate('() => recentCommand.accepted')
        # Produce a real container from gathered inputs, then verify both storage transitions.
        page.evaluate("() => inspect(view.actor)")
        for resource in ('herb', 'ore'):
            page.get_by_role('button', name='재료 채집', exact=True).click()
            page.get_by_label('채집 대상').select_option(resource)
            submit(page)
            page.get_by_role('button', name='1개월', exact=True).click()
            settle(page)
        for recipe in ('cloth', 'cloth', 'bag'):
            page.get_by_role('button', name='제작', exact=True).click()
            page.get_by_label('제작할 실물').select_option(recipe)
            submit(page)
            page.get_by_role('button', name='1개월', exact=True).click()
            settle(page)
        bag_id = page.evaluate("() => view.items.find(i=>i.definition==='bag').id")
        book_id = page.evaluate("() => view.items.find(i=>i.definition==='path.1.0').id")
        open_item(page, '청허감응편')
        page.get_by_role('button', name='수납·꺼내기', exact=True).click()
        page.get_by_label('대상 용기').select_option(str(bag_id))
        submit(page)
        assert page.evaluate('(id) => item(id).container', book_id) == bag_id
        open_item(page, '청허감응편')
        page.get_by_role('button', name='수납·꺼내기', exact=True).click()
        page.get_by_label('대상 용기').select_option('0')
        submit(page)
        assert page.evaluate('(id) => item(id).container', book_id) == 0
        open_item(page, '청허감응편')
        page.get_by_role('button', name='출품', exact=True).click()
        page.get_by_label('가격 · 영석').fill('19')
        submit(page)
        sale = page.evaluate("() => view.contracts.find(c=>c.kind==='sale'&&c.seller===actor()&&c.status==='open')")
        assert sale and sale['price'] == 19
        nav(page, '시장')
        page.get_by_role('button', name='철회', exact=True).first.click()
        settle(page)
        assert page.evaluate('(id) => view.contracts.find(c=>c.id===id).status', sale['id']) == 'cancelled'

        # Expose a known route by exploration, then submit real transport through UI.
        page.evaluate("() => inspect(view.actor)")
        page.get_by_role('button', name='탐색', exact=True).click()
        settle(page)
        page.get_by_role('button', name='1개월', exact=True).click()
        settle(page)
        cancel_action(page)
        nav(page, '지도')
        unknown_cells = page.locator('.map button.unknown')
        assert unknown_cells.count() > 0
        assert unknown_cells.first.is_disabled()
        open_item(page, '오행행기요결')
        page.get_by_role('button', name='운송', exact=True).click()
        page.get_by_label('인지한 인접 목적지').wait_for()
        destination = int(page.locator('#destination option').first.get_attribute('value'))
        submit(page)
        transport = page.evaluate("() => view.contracts.find(c=>c.kind==='transport'&&c.seller===actor()&&c.status==='open')")
        assert transport and transport['place'] == destination
        page.get_by_role('button', name='1개월', exact=True).click()
        settle(page)
        assert page.evaluate('() => activePerson().place') == destination
        assert page.evaluate('(id) => view.contracts.find(c=>c.id===id).status', transport['id']) == 'completed'

        # Founding and policy changes reserve real money and settle after time advances.
        nav(page, '세력')
        page.get_by_role('button', name='가문·종문 창설', exact=True).click()
        page.get_by_label('조직 종류').select_option('sect')
        submit(page)
        sect = page.evaluate("() => view.groups.find(g=>g.kind==='Sect'&&g.leader===actor()&&!g.dissolved)")
        assert sect
        page.get_by_role('button', name='1개월', exact=True).click()
        settle(page)
        cancel_action(page)
        card = page.locator('#group-' + str(sect['id']))
        card.get_by_role('button', name='정책 변경', exact=True).first.click()
        policy_key = page.get_by_role('dialog').locator('h2').inner_text()
        before_policy = page.evaluate('([id,key]) => group(id).policies[key]', [sect['id'], policy_key])
        next_policy = (before_policy + 1) % 3
        page.get_by_label('시행할 정책').select_option(str(next_policy))
        submit(page)
        assert page.evaluate('([id,key]) => group(id).policies[key]', [sect['id'], policy_key]) == before_policy
        page.get_by_role('button', name='1개월', exact=True).click()
        settle(page)
        assert page.evaluate('([id,key]) => group(id).policies[key]', [sect['id'], policy_key]) == next_policy
        home = page.evaluate("() => view.groups.find(g=>g.kind==='Family'&&g.leader===actor())")
        home_card = page.locator('#group-' + str(home['id']))
        home_card.get_by_role('button', name='봉신·독립', exact=True).click()
        page.get_by_label('상위 왕조').select_option('0')
        submit(page)
        assert page.evaluate('(id) => group(id).overlord', home['id']) == 0
        assert home_card.get_by_role('button', name='주권 반납', exact=True).count() == 0
        home_card.get_by_role('button', name='주 통치', exact=True).click()
        page.get_by_label('통치할 주').select_option('0')
        governance_revision = page.evaluate('() => view.revision')
        submit(page, accepted=False)
        assert page.evaluate('() => view.revision') == governance_revision

        # Uncertain repair data leads to an authoritative rejection, not a false success.
        open_item(page, '채집 도구')
        page.get_by_role('button', name='수리 계획', exact=True).click()
        rejected_revision = page.evaluate('() => view.revision')
        submit(page, accepted=False)
        assert page.evaluate('() => view.revision') == rejected_revision
        assert '명령 거절' in page.locator('#feedback').inner_text()

        # Save/load changes scope and clears old detail, dialogs and search state.
        page.get_by_label('저장 슬롯').select_option('2')
        page.get_by_role('button', name='저장', exact=True).click()
        settle(page)
        saved_month = page.evaluate('() => view.month')
        page.get_by_role('button', name='1개월', exact=True).click()
        settle(page)
        page.get_by_role('button', name='불러오기', exact=True).click()
        page.get_by_role('dialog').get_by_role('button', name='불러오기', exact=True).click()
        settle(page)
        assert page.evaluate('() => view.month') == saved_month
        assert page.evaluate('() => history.length === 0 && Object.keys(searchByTab).length === 0')
        page.get_by_role('button', name='관찰로 전환', exact=True).first.click()
        settle(page)
        assert page.evaluate('() => view.observer')
        assert page.get_by_role('navigation').get_by_role('button', name='법칙', exact=True).count() == 1

        nav(page, '법칙')
        law = page.evaluate("() => view.laws.find(l=>l.id==='economy.travelDuration')")
        new_value = law['value'] + 1 if law['value'] < law['maximum'] else law['value'] - 1
        page.locator('[id="law-' + law['id'] + '"]').fill(str(new_value))
        page.locator('.lawrow').filter(has=page.locator('[id="law-' + law['id'] + '"]')).get_by_role('button', name='적용', exact=True).click()
        settle(page)
        applied = page.evaluate('(id) => view.laws.find(l=>l.id===id).value', law['id'])
        assert applied == new_value
        page.get_by_role('button', name='값 내보내기', exact=True).click()
        exported = json.loads(page.get_by_label('복사할 값').input_value())
        assert exported['version'] == '0.2.1' and exported['laws'][law['id']] == applied
        page.keyboard.press('Escape')
        page.get_by_role('button', name='값 가져오기', exact=True).click()
        page.get_by_label('내보낸 JSON 값 붙여넣기').fill('{"laws":{"unknown-law":1}}')
        page.get_by_role('button', name='차이 검토', exact=True).click()
        assert page.get_by_role('dialog').is_visible()
        assert page.locator('#dialog-error').inner_text()
        page.keyboard.press('Escape')
        page.get_by_role('button', name='값 가져오기', exact=True).click()
        page.get_by_label('내보낸 JSON 값 붙여넣기').fill(json.dumps(exported))
        page.get_by_role('button', name='차이 검토', exact=True).click()
        assert page.get_by_role('dialog').is_visible()
        page.keyboard.press('Escape')
        exported['laws'][law['id']] = law['value']
        page.get_by_role('button', name='값 가져오기', exact=True).click()
        page.get_by_label('내보낸 JSON 값 붙여넣기').fill(json.dumps(exported))
        page.get_by_role('button', name='차이 검토', exact=True).click()
        page.get_by_role('button', name='검토한 값 일괄 적용', exact=True).click()
        settle(page)
        assert page.evaluate('(id) => view.laws.find(l=>l.id===id).value', law['id']) == law['value']
        nav(page, '인물')
        if page.get_by_role('button', name='인물 목록', exact=True).count():
            page.get_by_role('button', name='인물 목록', exact=True).click(); settle(page)
        page.get_by_role('button', name='명시적 인물 편입', exact=True).click()
        page.get_by_label('이름', exact=True).fill('명시 편입 검증')
        submit(page)
        created = page.evaluate("() => view.people.find(p=>p.name==='명시 편입 검증')")
        assert created and created['realm'] == '범인' and created['money'] == 0
        nav(page, '세계')
        page.evaluate('() => {document.activeElement.blur();window.scrollTo(0,0)}')
        page.screenshot(path=str(out / f'world-{width}.png'), full_page=True)
        assert not errors, errors
        results.append({'kind':'real_session_functional','width':width,'tabs':9,'javascript_errors':errors,
                        'document_horizontal_overflow':False,'touch_map_targets_at_least_44px':True,
                        'search_focus_and_back':True,'selected_known_path_command':True,
                        'cancel_draft_and_confirmed':True,'use_equip_craft_bag_store_unstore_offer_withdraw':True,
                        'explore_transport_complete':True,'repair_rejection_atomic':True,
                        'teaching_request_accepted':True,'adult_adoption_rejection_atomic':True,
                        'found_sect_policy_settlement':True,'vassal_independence':True,
                        'governance_knowledge_rejection_atomic':True,'explicit_adult_assetless':True,
                        'save_load_scope_cleanup':True,'law_export_import_validation_and_apply':True})
        page.close()

    # Replay real, permission-shaped session replies to check both native adapters.
    for adapter in ('android_sync', 'windows_async'):
        page = browser.new_page(viewport={'width':390,'height':900})
        errors = []
        page.on('pageerror', lambda error: errors.append(str(error)))
        page.add_init_script('window.__bridgeReplies = ' + json.dumps([observer_reply, play_reply, observer_reply]) + ';')
        page.add_init_script("""(() => {
          window.__bridgeRequests = []; window.__activeRequests = 0; window.__serializationViolation = false;
          const take = request => {
            window.__bridgeRequests.push(request);
            return JSON.stringify(window.__bridgeReplies.shift());
          };
          if (""" + json.dumps(adapter) + """ === 'android_sync') {
            window.seonroNative = {request: raw => take(JSON.parse(raw))};
          } else {
            const listeners = [];
            window.chrome = {webview:{addEventListener:(name,fn)=>listeners.push(fn),postMessage:message=>{
              if (++window.__activeRequests > 1) window.__serializationViolation = true;
              const payload = take(message.request);
              Promise.resolve().then(()=>{
                window.__activeRequests--;
                for (const listener of listeners) listener({data:{sequence:message.sequence,payload}});
              });
            }}};
          }
        })();""")
        page.route('**/api', lambda route: route.abort())
        page.goto(args.url)
        page.get_by_role('heading', name='만상의 흐름', exact=True).wait_for()
        nav(page, '법칙')
        page.get_by_role('searchbox').fill('시조')
        page.evaluate("() => dialog('관찰 전용 초안','<p>관찰 범위</p>',null)")
        page.evaluate("() => Promise.all([send({type:'view'}),send({type:'view'})])")
        assert page.evaluate('() => view.observer === false')
        assert page.evaluate('() => lastGeneration') == play_reply['view']['generation']
        assert page.evaluate('() => history.length === 0 && Object.keys(searchByTab).length === 0 && recentCommand === null')
        assert not page.get_by_role('dialog').is_visible()
        assert page.locator('#dialogform').inner_text() == ''
        assert page.get_by_role('navigation').get_by_role('button', name='법칙', exact=True).count() == 0
        assert page.evaluate('() => nativePending.size') == 0
        assert page.evaluate('() => !window.__serializationViolation')
        assert page.evaluate('() => window.__bridgeRequests.map(r=>r.type)') == ['view','view','view']
        assert not errors, errors
        results.append({'kind':'real_reply_adapter_replay','adapter':adapter,'native_request_response':True,
                        'serialized_requests':True,'late_observer_reply_discarded':True,
                        'scope_dialog_search_history_cleanup':True,'javascript_errors':errors,
                        'domain_integration_claim':False})
        page.close()
    browser.close()
(out / 'results.json').write_text(json.dumps(results, ensure_ascii=False, indent=2)+'\n')
print(json.dumps(results, ensure_ascii=False))
