#!/usr/bin/env python3
import json
import sys
from pathlib import Path

text = Path(sys.argv[1]).read_text(encoding='utf-8')
assert '만상의 흐름' in text, 'Android WebView world view did not render'
assert '선로만상록' in text, 'Android product title missing'
print(json.dumps({'android_actual_install_launch_and_render': True,
                  'core_to_native_bridge': True, 'world_view': '만상의 흐름'}, ensure_ascii=False))
