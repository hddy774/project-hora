#!/usr/bin/env python3
"""실제 앱 포커스·새 전환·실제 조작 좌표가 일치해야 성공하는 Android 수용 검사."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import time

PACKAGE = 'org.hora.xianxiaobserver'


def adb(*args):
    return subprocess.check_output(['adb', *map(str, args)], timeout=45).decode(errors='replace')


class Smoke:
    def __init__(self, out):
        self.out = Path(out)
        self.out.mkdir(parents=True, exist_ok=True)
        self.positions = {}
        self.viewport = None
        self.context = {}
        self.step = 0
        self.keyboard_top = None

    def focus(self):
        state = adb('shell', 'dumpsys', 'window')
        lines = [line for line in state.splitlines() if 'mCurrentFocus=' in line]
        assert lines and PACKAGE in lines[-1], '앱이 아닌 창에 포커스: ' + str(lines)

    def logs(self):
        return adb('logcat', '-d', '-v', 'brief', '-s', 'XianxiaUi:I', '*:S')

    def clear(self):
        with (self.out / 'logcat.txt').open('a') as stream:
            stream.write(adb('logcat', '-d', '-v', 'threadtime'))
        adb('logcat', '-c')

    def trace(self, expression, timeout=2):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            if re.search(expression, self.logs()):
                return
            time.sleep(.1)
        self.shot('failed-transition')
        self.clear()
        raise AssertionError('새 전환 로그 없음: ' + expression)

    def controls(self, timeout=6):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            log = self.logs()
            start = log.rfind('[xo-ui] controls-begin')
            end = log.rfind('[xo-ui] controls-end')
            if start >= 0 and end > start:
                self.positions = {}
                for line in log[start:end].splitlines():
                    for kind in ['control', 'viewport', 'context', 'keyboard']:
                        marker = '[xo-ui] ' + kind + ' '
                        if marker not in line:
                            continue
                        value = json.loads(line.split(marker, 1)[1])
                        if kind == 'keyboard':
                            self.keyboard_top = value['top']
                        elif kind == 'control':
                            self.positions[value['id']] = value
                        elif kind == 'viewport':
                            self.viewport = value
                        else:
                            self.context = value
                return
            time.sleep(.15)
        self.shot('failed-controls')
        self.clear()
        raise AssertionError('현재 화면 조작 좌표 없음')

    def shot(self, name):
        self.step += 1
        path = self.out / f'{self.step:03d}-{name}.png'
        path.write_bytes(subprocess.check_output(['adb', 'exec-out', 'screencap', '-p'], timeout=45))
        print(path, flush=True)

    def scroll(self, down=True):
        self.focus()
        assert self.viewport, '스크롤 가능한 실제 영역 없음'
        area = self.viewport
        x = (area['left'] + area['right']) // 2
        top, bottom = area['top'] + 24, area['bottom'] - 24
        assert bottom > top, '본문 스크롤 영역이 가려짐'
        self.clear()
        adb('shell', 'input', 'swipe', x, bottom if down else top, x, top if down else bottom, 600)
        self.controls()

    def find(self, expression):
        for direction in [True, False]:
            for attempt in range(10):
                matches = [value for key, value in self.positions.items() if re.fullmatch(expression, key)]
                if matches:
                    return matches[0]
                self.scroll(direction)
        raise AssertionError('실제 보이는 조작을 찾지 못함: ' + expression)

    def tap(self, expression, expected=None):
        position = self.find(expression)
        self.focus()
        self.clear()
        # API24의 input tap은 DOWN/UP 타임스탬프가 같다. 실제 손가락처럼120ms 정지 접촉한다.
        adb('shell', 'input', 'swipe', position['x'], position['y'], position['x'], position['y'], 120)
        if expected:
            self.trace(expected)
        self.controls()

    def back(self, expected=None):
        self.focus()
        self.clear()
        adb('shell', 'input', 'keyevent', 'KEYCODE_BACK')
        if expected:
            self.trace(expected)
        self.controls()

    def start(self):
        self.clear()
        adb('shell', 'am', 'start', '-W', '-n', PACKAGE + '/.MainActivity')
        self.trace(r'\[xo-ui\] session loaded [1-9][0-9]*', 35)
        self.controls(15)
        self.focus()

    def pause(self):
        if self.context.get('running'):
            self.tap('play-toggle')
        assert self.context.get('running') is False

    def routes(self, suffix):
        for destination, index in [('world', 0), ('sects', 1), ('people', 2), ('history', 3), ('more', 7)]:
            self.tap('nav-' + destination, rf'\[xo-ui\] tab {index} ')
            self.shot(suffix + '-' + destination)
        for destination, index in [('legacy', 4), ('stats', 5), ('log', 6)]:
            self.tap('more-' + destination, rf'\[xo-ui\] tab {index} ')
            self.shot(suffix + '-' + destination)
            self.back(r'\[xo-ui\] tab 7 ')

    def details(self):
        self.tap('nav-people', r'\[xo-ui\] tab 2 ')
        self.tap('button:청운종', r'\[xo-ui\] choice 1')
        self.tap(r'link-Cultivator-[1-9][0-9]*', r'\[xo-ui\] sheet Cultivator ')
        for index, name in enumerate(['개요', '수행', '인연', '생애']):
            self.tap('button:' + name, rf'\[xo-ui\] choice {index}')
            self.shot('person-' + name)
        self.tap('button:개요', r'\[xo-ui\] choice 0')
        favor = self.context['favor']
        self.tap('fortune-request')
        self.tap('fortune-cancel')
        assert self.context['favor'] == favor, '취소에 천기 소비'
        self.tap('fortune-request')
        self.tap('fortune-confirm')
        assert self.context['favor'] == favor - 2, '확인1회 천기2 차감 불일치'
        self.tap(r'button:.* ›', r'\[xo-ui\] sheet Region ')
        self.shot('region')
        self.back(r'\[xo-ui\] close Region')
        self.tap(r'button:청운종.*', r'\[xo-ui\] sheet Sect ')
        self.shot('sect')
        self.back(r'\[xo-ui\] close Sect')
        self.back(r'\[xo-ui\] close Cultivator')
        self.tap('nav-history', r'\[xo-ui\] tab 3 ')
        for name, kind, index in [('전쟁사', 'War', 2), ('대비무', 'Tournament', 3)]:
            self.tap('button:' + name, rf'\[xo-ui\] choice {index}')
            self.tap('link-' + kind + '-[0-9]+', r'\[xo-ui\] sheet ' + kind + ' ')
            self.shot(kind)
            self.back(r'\[xo-ui\] close ' + kind)
        self.tap('nav-more', r'\[xo-ui\] tab 7 ')
        for name, kind in [('설정 · 저장 ›', 'Settings'), ('도감 ›', 'Help')]:
            self.tap('button:' + name, r'\[xo-ui\] sheet ' + kind + ' ')
            self.shot(kind)
            self.back(r'\[xo-ui\] close ' + kind)

    def keyboard(self):
        self.tap('nav-people', r'\[xo-ui\] tab 2 ')
        self.tap('people-search')
        self.focus()
        adb('shell', 'input', 'text', 'hora')
        self.controls()
        state = adb('shell', 'dumpsys', 'input_method')
        assert re.search(r'(mInputShown=true|isInputViewShown\(\)=true|mIsInputViewShown=true)', state), '검색 IME 미표시'
        self.trace(r'\[xo-ui\] keyboard ', 6)
        self.controls()
        assert self.keyboard_top is not None, '키보드 가림 영역 없음'
        assert 'play-toggle' in self.positions, 'IME 중 진행 조작이 보이지 않음'
        assert self.positions['play-toggle']['y'] < self.keyboard_top, 'IME가 진행 조작을 가림'
        self.shot('search-ime')
        self.back()
        self.focus()
        adb('shell', 'input', 'keyevent', 'KEYCODE_MOVE_END')
        for _ in range(4):
            adb('shell', 'input', 'keyevent', 'KEYCODE_DEL')
        self.controls()


def main():
    apk, out, legacy = sys.argv[1:4]
    smoke = Smoke(out)
    adb('wait-for-device')
    adb('shell', 'settings', 'put', 'system', 'font_scale', '1.0')
    adb('shell', 'settings', 'put', 'secure', 'show_ime_with_hard_keyboard', '1')
    adb('uninstall', PACKAGE) if PACKAGE in adb('shell', 'pm', 'list', 'packages', PACKAGE) else None
    adb('install', legacy)
    adb('shell', 'am', 'start', '-W', '-n', PACKAGE + '/.MainActivity')
    time.sleep(25)
    adb('shell', 'input', 'keyevent', 'KEYCODE_HOME')
    time.sleep(4)
    adb('shell', 'am', 'force-stop', PACKAGE)
    adb('install', '-r', apk)
    smoke.start()  # 이전 공개0.1.0이 만든 저장을 새 APK가 실제로 복구해야 한다.
    smoke.shot('upgrade-loaded')
    adb('shell', 'input', 'keyevent', 'KEYCODE_HOME')
    time.sleep(2)
    try:
        smoke.focus()
    except AssertionError as error:
        (smoke.out / 'negative-launcher-focus.txt').write_text(str(error))
    else:
        raise AssertionError('런처 포커스 음성 대조가 입력을 허용함')
    adb('shell', 'am', 'force-stop', 'com.google.android.apps.nexuslauncher')
    adb('shell', 'am', 'force-stop', PACKAGE)
    smoke.start()
    smoke.tap('speed-picker', r'\[xo-ui\] sheet Speed ')
    smoke.tap('speed-5', r'\[xo-ui\] close Speed')
    if not smoke.context.get('running'):
        smoke.tap('play-toggle')
    deadline = time.monotonic() + 180
    while smoke.context['year'] < 4500 and time.monotonic() < deadline:
        time.sleep(4)
        smoke.tap('nav-world', r'\[xo-ui\] tab 0 ')
    assert smoke.context['year'] >= 4500, '실제 전쟁/대회 기록까지 진행하지 못함'
    smoke.pause()
    smoke.routes('100')
    smoke.details()
    smoke.keyboard()
    for scale in ['1.3', '2.0', '1.0']:
        smoke.focus()
        smoke.clear()
        adb('shell', 'settings', 'put', 'system', 'font_scale', scale)
        smoke.controls(12)
        smoke.routes(scale)
    smoke.tap('nav-world', r'\[xo-ui\] tab 0 ')
    smoke.tap('play-toggle')
    smoke.clear()
    time.sleep(60)
    assert '[xo-ui]' not in smoke.logs(), '연간 진행에 UI 전환 로그가 반복됨'
    adb('shell', 'input', 'keyevent', 'KEYCODE_HOME')
    time.sleep(3)
    adb('shell', 'am', 'force-stop', PACKAGE)
    smoke.start()
    smoke.shot('relaunch')
    smoke.clear()
    log = (smoke.out / 'logcat.txt').read_text()
    assert not re.search(r'FATAL EXCEPTION|ANR in ' + re.escape(PACKAGE), log), '치명적 앱 오류/ANR'
    assert adb('shell', 'pidof', PACKAGE).strip(), '앱 프로세스 없음'
    (smoke.out / 'tested-apk-sha256.txt').write_text(hashlib.sha256(Path(apk).read_bytes()).hexdigest() + '\n')
    print('실제 포커스·7데이터 경로·7상세·4인물 분류·IME·3배율·저장 업데이트/재실행 통과', flush=True)


if __name__ == '__main__':
    try:
        main()
    except BaseException:
        if len(sys.argv) > 2:
            out = Path(sys.argv[2])
            out.mkdir(parents=True, exist_ok=True)
            (out / 'failure-logcat.txt').write_text(adb('logcat', '-d', '-v', 'threadtime'))
            (out / 'failure-window.txt').write_text(adb('shell', 'dumpsys', 'window'))
        raise
