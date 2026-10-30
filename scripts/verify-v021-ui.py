#!/usr/bin/env python3
"""Run against `dotnet run --project src/Chronicles.Shell -- --preview`."""
import json
import sys
from pathlib import Path
from playwright.sync_api import sync_playwright

out = Path(sys.argv[1] if len(sys.argv) > 1 else 'artifacts/ui')
out.mkdir(parents=True, exist_ok=True)
results = []
with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, args=['--no-sandbox'])
    for width in (1280, 390):
        page = browser.new_page(viewport={'width': width, 'height': 900})
        errors = []
        page.on('pageerror', lambda e: errors.append(str(e)))
        page.goto('http://127.0.0.1:5211')
        page.wait_for_selector('text=만상의 흐름')
        page.evaluate("send({type:'new', seed:42})")
        page.wait_for_timeout(200)
        for label in ('인물', '지도', '수행', '물품', '세력', '시장', '연대기', '법칙', '세계'):
            page.get_by_role('button', name=label, exact=True).first.click()
            assert not page.evaluate('document.body.scrollWidth > innerWidth'), label
        page.get_by_role('button', name='인물', exact=True).first.click()
        page.get_by_role('button', name='상세', exact=True).first.click()
        page.wait_for_selector('text=오행 영근')
        page.get_by_role('button', name='이 인물로 플레이', exact=True).click()
        page.wait_for_selector('text=조작 중')
        assert page.get_by_role('button', name='법칙', exact=True).count() == 0
        page.get_by_role('button', name='식량 채집', exact=True).click()
        page.get_by_role('button', name='1개월', exact=True).click()
        page.get_by_role('button', name='저장', exact=True).click()
        page.wait_for_timeout(200)
        assert not errors, errors
        page.screenshot(path=str(out / f'{width}.png'), full_page=True)
        results.append({'width': width, 'tabs': 9, 'javascript_errors': errors,
                        'horizontal_overflow': False, 'play_law_hidden': True,
                        'action_and_save': True})
        page.close()
    browser.close()
(out / 'results.json').write_text(json.dumps(results, ensure_ascii=False, indent=2)+'\n')
print(json.dumps(results, ensure_ascii=False))
