#!/usr/bin/env python3
"""Functional shell checks against a real GameSession; no profiling or load tests.

Start Chronicles.Shell with --preview, then run this file. The default covers nine
menus, timed bilateral social workflows and scope cleanup. --smoke-only skips the
timed social and adapter checks. An isolated host can be selected with --url.
Adapter checks replay real session replies only;
they verify the JavaScript bridge and never count as domain integration evidence.
"""
import argparse
import json
import os
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


def assert_no_horizontal_overflow(page):
    overflow=page.evaluate('() => ({document:document.documentElement.scrollWidth>innerWidth,body:document.body.scrollWidth>innerWidth})')
    assert not overflow['document'] and not overflow['body'], overflow


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
        inside=page.evaluate('() => !!view.inSecretRealm')
        reply = page.evaluate("() => issueCommand(view.inSecretRealm?'returnSecret':'cancel')")
        assert reply['result']['accepted'], reply['message']
        if inside:
            page.get_by_role('button',name='1개월',exact=True).click(); settle(page)
            assert not page.evaluate('() => view.inSecretRealm')


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


def secret_workflows(page, width):
    who=page.evaluate('() => view.actor')
    page.get_by_role('button',name='관찰로 전환',exact=True).first.click(); settle(page)
    # Configure a short deterministic functional case through the real public law command.
    settings={'birth.enabled':0,'ecology.disasterChance':0,'hazard.sealChance':0,
              'exploration.openPeriod':12,'exploration.openMonths':12,'exploration.entryMonths':2,'exploration.risk':0,'exploration.capacity':2}
    reply=page.evaluate('(settings)=>issueCommand(\'lawBatch\',0,0,JSON.stringify(settings),0)',settings)
    assert reply['result']['accepted'],reply['message']
    page.evaluate('(id)=>inspect(id)',who)
    page.get_by_role('button',name='이 인물로 플레이',exact=True).click(); settle(page)

    def self_detail():
        page.evaluate('() => inspect(view.actor)')

    def finish_current():
        for _ in range(12):
            if page.evaluate("() => activePerson().action==='Idle'"):
                return
            page.get_by_role('button',name='1개월',exact=True).click(); settle(page)
            assert page.evaluate("() => activePerson().status==='Alive'")
        raise AssertionError('The real bounded action did not finish.')

    for destination in range(1,9):
        self_detail(); cancel_action(page); self_detail()
        page.get_by_role('button',name='탐색',exact=True).click(); settle(page); finish_current()
        name=page.evaluate('(id)=>place(id)?.name',destination)
        assert name,'A paid local survey did not reveal the next actual neighbor.'
        nav(page,'지도')
        page.locator('.map').get_by_role('button',name=name,exact=True).click()
        submit(page,'이동 요청'); finish_current()
        assert page.evaluate('(id)=>activePerson().place===id',destination)
    self_detail()
    page.get_by_role('button',name='탐색',exact=True).click(); settle(page); finish_current()
    assert page.evaluate('() => view.exploration.entrances.length===1&&view.exploration.entrances[0].place===8&&view.exploration.entrances[0].openNow===null&&view.exploration.entrances[0].actualParticipants===null')
    book=page.evaluate("() => view.items.find(i=>i.definition==='path.1.0')")
    open_item(page,book['name'])
    page.get_by_role('button',name='수납·꺼내기',exact=True).click()
    page.get_by_label('대상 용기').select_option('ground'); submit(page)
    assert page.evaluate('(id)=>item(id).isCarried===false&&item(id).place===8',book['id'])
    nav(page,'지도')
    gate=page.locator('[data-secret-place="8"]')
    assert gate.get_by_text('종류 미상',exact=False).count()>0
    gate.get_by_role('button',name='비경 입장 준비',exact=True).click()
    revision=page.evaluate('() => view.revision')
    page.keyboard.press('Escape')
    assert page.evaluate('() => view.revision')==revision

    def enter():
        gate.get_by_role('button',name='비경 입장 준비',exact=True).click()
        submit(page,'현재 조건으로 입장 요청')
        assert page.evaluate('() => view.inSecretRealm&&view.economy===null')
        e=page.evaluate('() => view.exploration.ownEntries.find(e=>e.status===\'Active\')')
        assert e and e['person']==who and e['actualRisk'] is None and e['actualRoll'] is None and e['actualRules'] is None
        assert page.evaluate('(id)=>!item(id)',book['id'])
        return e

    first=enter()
    assert_no_horizontal_overflow(page)
    page.screenshot(path=str(out/f'secret-active-{width}.png'),full_page=True)
    nav(page,'시장')
    assert page.locator('[data-section="economy"]').count()==0
    assert page.get_by_text('비경 안에서는 바깥의 새 출품·시세·거점을 확인할 수 없습니다.',exact=False).count()>0
    nav(page,'지도')
    page.get_by_role('button',name='귀환 준비 검토',exact=True).click()
    submit(page,'귀환 시작 요청')
    assert page.evaluate('() => view.inSecretRealm&&view.exploration.ownEntries.some(e=>e.status===\'Returning\')')
    finish_current()
    assert page.evaluate('(id)=>!view.inSecretRealm&&view.exploration.ownEntries.find(e=>e.id===id).status===\'Returned\'&&view.exploration.ownEntries.find(e=>e.id===id).rewardQuantity===0',first['id'])
    assert page.evaluate('(id)=>!!item(id)',book['id'])
    second=enter()
    page.get_by_role('button',name='1개월',exact=True).click(); settle(page)
    assert page.evaluate('(id)=>view.inSecretRealm&&view.exploration.ownEntries.find(e=>e.id===id).progress===1',second['id'])
    page.get_by_label('저장 슬롯').select_option('1')
    page.get_by_role('button',name='저장',exact=True).click(); settle(page)
    page.get_by_role('button',name='불러오기',exact=True).click()
    page.get_by_role('dialog').get_by_role('button',name='불러오기',exact=True).click(); settle(page)
    assert page.evaluate('(id)=>view.inSecretRealm&&view.exploration.ownEntries.find(e=>e.id===id).progress===1',second['id'])
    finish_current()
    ended=page.evaluate('(id)=>view.exploration.ownEntries.find(e=>e.id===id)',second['id'])
    assert ended['status']=='Completed' and ended['progress']==ended['required'] and not page.evaluate('() => view.inSecretRealm')
    if ended['rewardQuantity']:
        assert page.evaluate('(e)=>item(e.rewardItem).quantity===e.rewardQuantity&&item(e.rewardItem).qualityValue===null',ended)
    else:
        assert ended['endReason']
    assert_no_horizontal_overflow(page)
    page.screenshot(path=str(out/f'secret-completed-{width}.png'),full_page=True)
    return {'secret_paid_survey_travel_entry':True,'secret_observed_uncertainty':True,'secret_carried_space_boundary':True,
            'secret_real_return_and_preparation_cost':True,'secret_inside_save_resume':True,'secret_finite_outcome_observed':True}


def policy_workflows(page, width):
    page.evaluate('() => inspect(view.people.find(p=>p.status===\'Alive\').id)')
    page.get_by_role('button',name='이 인물로 플레이',exact=True).click(); settle(page)
    nav(page,'세력')
    home = page.evaluate("() => view.groups.find(g=>g.kind==='Family'&&g.leader===view.actor)")
    assert home
    article = page.locator('#group-'+str(home['id']))
    assert article.get_by_role('button',name='정책 변경',exact=True).count() == 10
    article.get_by_role('button',name='개인 잔액으로 예산 대여',exact=True).click()
    page.get_by_label('대여 원금 · 영석').fill('40')
    submit(page,'원금 대여·채무 기록')
    assert page.evaluate('(id)=>group(id).money===40&&group(id).debt===40',home['id'])

    def adopt_and_prepare():
        row=article.locator('.row.listrow').filter(has_text='경제·분배')
        row.get_by_role('button',name='정책 변경',exact=True).click()
        actual=page.evaluate("() => view.policyDefinitions.find(d=>d.category==='경제·분배').choices")
        assert page.locator('#policy option').all_text_contents() == actual
        page.get_by_label('채택할 세부 정책').select_option('2')
        submit(page,'정책 초안 채택')
        assert page.evaluate("(id)=>group(id).policyPreparations.some(p=>p.category==='경제·분배'&&p.stage==='Adopted')",home['id'])
        article.get_by_role('button',name='예산·인력 준비 요청',exact=True).click(); settle(page)
        assert page.evaluate("(id)=>group(id).policyPreparations.some(p=>p.category==='경제·분배'&&p.stage==='Preparing'&&p.escrow>0&&p.workers.includes(view.actor))&&group(id).policyEscrow>0&&group(id).policyDue>view.month",home['id'])
        assert page.evaluate("() => activePerson().guardTargetKind==='organization'")

    adopt_and_prepare()
    assert page.evaluate('(id)=>group(id).money+group(id).policyEscrow===40',home['id'])
    article.get_by_text('실제 정책 전이 이력',exact=True).click()
    assert_no_horizontal_overflow(page)
    page.screenshot(path=str(out / f'policy-preparation-{width}.png'),full_page=True)
    article.get_by_role('button',name='초안·예약 취소 검토',exact=True).click()
    submit(page,'초안·예약 취소 제출')
    assert page.evaluate('(id)=>group(id).money===40&&group(id).policyEscrow===0&&Object.keys(group(id).pendingPolicies).length===0',home['id'])
    adopt_and_prepare()
    page.get_by_role('button',name='1개월',exact=True).click(); settle(page)
    assert page.evaluate("(id)=>group(id).policies['경제·분배']===2&&group(id).policyEscrow===0&&group(id).policyDue===null&&Object.keys(group(id).pendingPolicies).length===0&&group(id).policyPreparations.some(p=>p.stage==='Implemented')",home['id'])
    assert article.get_by_text('후한 급여',exact=True).count() > 0
    assert_no_horizontal_overflow(page)
    page.screenshot(path=str(out / f'policy-implemented-{width}.png'),full_page=True)
    return {'policy_catalog_choices':True,'policy_adopt_prepare_cancel_implement':True,'actual_policy_budget_and_debt':True}


def social_workflows(page, width):
    def inspect_self():
        page.evaluate('() => inspect(view.actor)')

    def switch_to(person_id):
        if page.evaluate('() => view.actor') != person_id:
            page.evaluate('(id)=>inspect(id)', person_id)
            page.get_by_role('button', name='이 인물로 플레이', exact=True).click(); settle(page)
        inspect_self()

    def prepare_current():
        inspect_self(); cancel_action(page); inspect_self()

    def propose(kind, target, terms=''):
        inspect_self()
        page.get_by_role('button', name='실연·진술·서약', exact=True).click()
        page.get_by_label('제안 종류').select_option(kind)
        page.get_by_label('현지에서 인지한 상대').select_option(str(target))
        if kind == 'requestAssessment':
            page.get_by_label('상대가 실연할 영역').select_option('21')
        elif kind == 'requestOath':
            page.get_by_label('한정된 약속 내용 · 1~80자').fill(terms)
        else:
            assert page.locator('#socialClaim option').count() > 0
        submit(page, '상대에게 제안 전달')
        assert page.evaluate('() => view.peopleRequests.some(r=>r.proposer===view.actor&&r.status===0)')

    def consent(accept=True):
        page.get_by_role('button', name='참여 수락 검토' if accept else '요청 거절 검토', exact=True).click()
        submit(page, '참여 수락 제출' if accept else '거절 의사 제출')

    proposer = page.evaluate('() => view.actor')
    recipient = page.evaluate('(who)=>view.people.find(p=>p.id!==who).id', proposer)
    claims_before = page.evaluate('() => view.receivedClaims')
    propose('requestAssessment', recipient)
    assert page.evaluate('() => view.receivedClaims') == claims_before
    switch_to(recipient)
    assert page.get_by_role('button', name='받은 개인 요청 1건', exact=True).count() == 1
    page.get_by_role('button', name='받은 개인 요청 1건', exact=True).click(); settle(page)
    page.screenshot(path=str(out / f'personal-request-{width}.png'), full_page=True)
    consent()
    assert page.evaluate('() => view.peopleRequests.some(r=>r.status===1)')
    assert 'people:' not in page.locator('[data-section="people-requests"]').inner_text()
    page.get_by_role('button', name='1개월', exact=True).click(); settle(page)
    assert page.evaluate('() => view.peopleRequests.length===0 && view.receivedClaims.some(c=>c.observerId===view.actor&&c.provenance.length===1&&c.confidence<10000)')
    page.locator('[data-section="received-claims"] > summary').click()
    page.screenshot(path=str(out / f'assessment-evidence-{width}.png'), full_page=True)
    prepare_current(); switch_to(proposer); prepare_current()
    propose('requestOath', recipient, '직접 확인한 연구를 서로 돕는다')
    switch_to(recipient); consent()
    page.get_by_role('button', name='1개월', exact=True).click(); settle(page)
    assert page.evaluate('() => view.knownOaths.some(o=>o.person===view.actor&&o.knownActive&&o.observedMonth===view.month)')
    page.screenshot(path=str(out / f'known-oath-{width}.png'), full_page=True)
    page.get_by_role('button', name='대면 파기 통지 검토', exact=True).click()
    submit(page, '파기 의사 통지')
    assert page.evaluate('() => view.knownOaths.every(o=>!o.knownActive)')
    # The third person is a real explicit adult with no assets, not a fixture-backed extra NPC.
    page.get_by_role('button', name='관찰로 전환', exact=True).first.click(); settle(page)
    nav(page, '인물')
    if page.get_by_role('button', name='인물 목록', exact=True).count():
        page.get_by_role('button', name='인물 목록', exact=True).click(); settle(page)
    page.get_by_role('button', name='명시적 인물 편입', exact=True).click()
    page.get_by_label('이름', exact=True).fill('전언을 듣는 영호')
    page.get_by_label('종과 자연 육신').select_option('spirit-fox')
    submit(page)
    third = page.evaluate("() => view.people.find(p=>p.name==='전언을 듣는 영호').id")
    switch_to(recipient); prepare_current()
    page.get_by_role('button', name='탐색', exact=True).click(); settle(page)
    page.get_by_role('button', name='1개월', exact=True).click(); settle(page)
    assert page.evaluate('(id)=>!!person(id)', third)
    prepare_current()
    propose('requestReputation', third)
    switch_to(third); prepare_current(); consent()
    page.get_by_role('button', name='1개월', exact=True).click(); settle(page)
    assert page.evaluate('() => view.receivedClaims.some(c=>c.observerId===view.actor&&c.provenance.length>=2)')
    assert page.locator('[data-section="people-ecology"]').count() == 1
    assert page.evaluate('() => view.detail.peopleEcology.person===view.actor && view.detail.peopleEcology.month<=view.month && view.detail.people.innate===null')
    page.locator('[data-section="received-claims"] > summary').click()
    assert_no_horizontal_overflow(page)
    page.screenshot(path=str(out / f'relay-and-habitat-{width}.png'), full_page=True)
    prepare_current(); propose('requestAssessment', recipient)
    switch_to(recipient); consent(accept=False)
    assert page.evaluate('() => view.peopleRequests.length===0')
    return {'assessment_consent_and_actual_evidence':True,'oath_known_notice_and_formal_breach':True,
            'actual_claim_relay_provenance':True,'received_request_rejection':True,'nonhuman_dated_ecology':True}


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
        observer_reply = page.evaluate("() => send({type:'new',seed:42})")
        page.evaluate("() => issueCommand('law',0,0,'birth.enabled',0)")
        assert page.evaluate('() => view.people.length===2 && !view.groups.some(g=>g.kind===\'Sect\')')
        for label in ('인물', '지도', '수행', '물품', '세력', '시장', '연대기', '법칙', '세계'):
            nav(page, label)
            assert_no_horizontal_overflow(page)
            page.screenshot(path=str(out / f'menu-{label}-{width}.png'), full_page=True)
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
        play_reply = page.evaluate("() => send({type:'view'})")
        chosen_path = page.evaluate('() => view.detail.cultivation.path')
        assert chosen_path and page.evaluate('(path)=>view.paths.some(p=>p.id===path)&&(view.detail.understanding[path]||0)>0', chosen_path)
        def submit_current_path():
            page.get_by_role('button',name='공법 선택·수행',exact=True).click()
            assert page.locator('#cultivationPath option').count() > 0
            page.get_by_label('이해한 현재 경지 경로').select_option(chosen_path)
            submit(page)
            assert page.evaluate("(path)=>activePerson().action==='Cultivate'&&activePerson().actionData===path", chosen_path)
            practices = page.evaluate('(path)=>view.detail.knowledge.activities.filter(a=>a.workId===path&&a.kind==="Practice"&&a.status==="Active")', chosen_path)
            assert len(practices) == 1 and practices[0]['adopted']
            assert page.locator('[data-section="knowledge-activities"]').get_by_text('실습 · 진행 중', exact=False).count() > 0
            return practices[0]['id']
        cancelled_practice = submit_current_path()
        revision = page.evaluate('() => view.revision')
        page.get_by_role('button',name='작업 취소 요청',exact=True).click()
        page.keyboard.press('Escape')
        assert page.evaluate('() => view.revision') == revision
        assert page.evaluate("() => activePerson().action==='Cultivate'")
        page.get_by_role('button',name='작업 취소 요청',exact=True).click()
        submit(page,'취소 요청 제출')
        assert page.evaluate("(id)=>activePerson().action==='Idle'&&view.detail.knowledge.activities.some(a=>a.id===id&&a.status==='Cancelled')", cancelled_practice)
        completed_practice = submit_current_path()
        page.get_by_role('button',name='1개월',exact=True).click(); settle(page)
        assert page.evaluate("(id)=>activePerson().action==='Idle'&&view.detail.knowledge.activities.some(a=>a.id===id&&a.status==='Completed'&&a.ended===view.month&&a.outcome!==null)", completed_practice)
        assert page.evaluate('(path)=>view.detail.cultivation.path===path', chosen_path)

        nav(page,'수행')
        page.screenshot(path=str(out / f'personal-cultivation-{width}.png'), full_page=True)
        page.evaluate('() => inspect(view.actor)')
        assert page.get_by_role('navigation').get_by_role('button', name='법칙', exact=True).count() == 0
        other = page.evaluate('() => view.people.find(p=>p.id!==view.actor).id')
        page.evaluate('(id)=>inspect(id)', other)
        assert page.evaluate('() => view.detail.decision===null && view.detail.knowledge===null && view.detail.person.plan===null')
        page.get_by_role('button', name='이 인물로 플레이', exact=True).click(); settle(page)
        page.evaluate('() => inspect(view.actor)')
        assert page.evaluate('(id)=>view.actor===id && view.detail.person.id===id', other)
        cancel_action(page); page.evaluate('() => inspect(view.actor)')
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
        page.evaluate('() => inspect(view.actor)')
        page.get_by_role('button',name='경계·동료 호위',exact=True).click()
        guard_ally = page.locator('#guardAlly option').last.get_attribute('value')
        page.get_by_label('같은 장소로 관측한 동료').select_option(guard_ally)
        submit(page,'경계·호위 요청')
        assert page.evaluate("(id)=>activePerson().action==='Guard'&&activePerson().guardTargetKind==='person'&&activePerson().actionTarget===Number(id)", guard_ally)
        page.get_by_role('button',name='작업 취소 요청',exact=True).click()
        submit(page,'취소 요청 제출')
        social = social_workflows(page,width) if not args.smoke_only else {}
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
        page.get_by_role('button',name='값 내보내기',exact=True).click()
        exported = json.loads(page.get_by_label('복사할 값').input_value())
        assert exported['version'] == page.evaluate('() => view.version')
        assert exported['laws'] == page.evaluate('() => Object.fromEntries(view.laws.map(l=>[l.id,l.value]))')
        page.keyboard.press('Escape')
        changes = page.evaluate('() => Object.fromEntries(view.laws.slice(0,2).map(l=>[l.id,l.value===l.maximum?l.value-1:l.value+1]))')
        page.get_by_role('button', name='값 가져오기', exact=True).click()
        revision = page.evaluate('() => view.revision')
        page.get_by_label('내보낸 JSON 값 붙여넣기').fill('{"laws":{"not.a.world.law":1}}')
        page.get_by_role('button',name='차이 검토',exact=True).click()
        assert page.locator('#dialog-error').inner_text()
        assert page.locator('#dialog').evaluate('(d)=>d.open')
        assert page.evaluate('() => view.revision') == revision
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
        page.evaluate('() => inspect(view.people[0].id)')
        page.get_by_role('button',name='이 인물로 플레이',exact=True).click(); settle(page)
        page.get_by_role('button',name='인물 목록',exact=True).click(); settle(page)
        page.get_by_role('searchbox').fill('청명')
        page.get_by_role('button',name='상세',exact=True).click(); settle(page)
        assert page.evaluate('() => !view.observer && history.length>0 && Object.keys(searchByTab).length>0')

        page.get_by_role('button', name='불러오기', exact=True).click()
        page.get_by_role('dialog').get_by_role('button', name='불러오기', exact=True).click(); settle(page)
        assert page.evaluate('() => view.hash') == saved
        assert page.evaluate('() => view.observer && view.actor===0 && view.detail===null && history.length===0 && Object.keys(searchByTab).length===0 && recentCommand===null')
        assert not page.locator('#dialog').evaluate('(d)=>d.open')
        assert page.locator('#dialogform').inner_text() == ''

        policy = policy_workflows(page,width) if not args.smoke_only else {}
        secret = secret_workflows(page,width) if not args.smoke_only else {}
        if args.legacy_dom:
            assert page.evaluate("() => document.documentElement.hasAttribute('data-shell-legacy-grid') && typeof globalThis==='object' && typeof Object.fromEntries==='function' && typeof [].at==='function'")
            assert page.locator('#dialog').get_attribute('data-shell-legacy-dialog') is not None
            assert page.evaluate('() => n(1234567)') == '1,234,567'
        assert page.evaluate('() => window.seonroLastHostError==null'), page.evaluate('() => window.seonroLastHostError')
        assert not errors, errors
        assert_no_horizontal_overflow(page)
        page.screenshot(path=str(out / f'smoke-{width}.png'), full_page=True)
        results.append({'kind':'real_session_functional_smoke' if args.smoke_only else 'real_session_functional','width':width,'asset':'committed_chrome51' if args.compat else 'authoring',
                        'forced_legacy_dom':args.legacy_dom,'tabs':9,'javascript_errors':errors,'touch_targets_44px':True,
                        'known_actor_switch':True,'equipment_use':True,'known_ally_guard':True,'species_creation':True,'atomic_law_batch':True,
                        'new_world_law_choice':True,'save_load':True,'document_horizontal_overflow':False,'selected_known_path_command':True,
                        'save_load_scope_cleanup':True,'law_export_import_validation_and_apply':True,'native_api24_verified':False,**social,**policy,**secret})
        context.close()
    return observer_reply,play_reply


with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, args=['--no-sandbox'])
    observer_reply,play_reply = smoke_checks(browser)
    # Replay real, permission-shaped session replies to check both native adapters.
    for adapter in (() if args.smoke_only else ('android_sync', 'windows_async')):
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
        if args.compat:
            asset = Path(__file__).resolve().parents[1] / 'src/Chronicles.Shell/index.compat.html'
            page.route('**/*', lambda route: route.fulfill(status=200,content_type='text/html; charset=utf-8',body=asset.read_text()) if route.request.resource_type=='document' else route.abort())
        else:
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
