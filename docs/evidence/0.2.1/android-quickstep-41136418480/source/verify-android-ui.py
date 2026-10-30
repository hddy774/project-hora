#!/usr/bin/env python3
import json
import sys
from pathlib import Path

text = Path(sys.argv[1]).read_text(encoding='utf-8')
assert 'android.webkit.WebView' in text and 'org.hora.xianxiaobserver' in text, 'Foreground app WebView missing'
# Android's accessibility tree may expose only the WebView node without an enabled
# accessibility service. Require the actual native WebView's visible DOM and its
# native GameSession reply, rather than pretending that node alone proves rendering.
reports = []
for line in Path(sys.argv[2]).read_text(encoding='utf-8').splitlines():
    if 'SeonroRender:' not in line:
        continue
    reports.append(json.loads(line.split('SeonroRender:', 1)[1].strip()))
expected_version = Path('VERSION').read_text().strip()
assert any(report.get('rendered') is True and report.get('heading') == '만상의 흐름'
           and report.get('title') == '선로만상록' and report.get('version') == expected_version
           and report.get('error') is None and report.get('connection') == '연결됨'
           for report in reports), 'Native WebView visible world DOM and GameSession reply were not verified'
print(json.dumps({'android_actual_install_launch_and_render': True,
                  'core_to_native_bridge': True, 'world_view': '만상의 흐름',
                  'version': expected_version, 'accessibility_text_export': '만상의 흐름' in text}, ensure_ascii=False))
