"""릴리스 입력 판정이 앱 존재와 실제 포커스를 혼동하지 않는지 확인한다."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SOURCE = Path(__file__).resolve().parents[2] / 'games/xianxia-observer/tools/emulator-smoke.py'
SPEC = importlib.util.spec_from_file_location('emulator_smoke', SOURCE)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class FocusTests(unittest.TestCase):
    def test_launcher_focus_fails_even_when_app_is_resumed(self):
        with tempfile.TemporaryDirectory() as directory:
            smoke = MODULE.Smoke(directory)
            state = ('mFocusedApp=org.hora.xianxiaobserver/.MainActivity\n'
                     'mCurrentFocus=Window{123 com.google.android.apps.nexuslauncher/.NexusLauncher}')
            with patch.object(MODULE, 'adb', return_value=state):
                with self.assertRaisesRegex(AssertionError, '앱이 아닌 창'):
                    smoke.focus()

    def test_actual_app_window_focus_passes(self):
        with tempfile.TemporaryDirectory() as directory:
            with patch.object(MODULE, 'adb', return_value='mCurrentFocus=Window{123 org.hora.xianxiaobserver/.MainActivity}'):
                MODULE.Smoke(directory).focus()

    def test_tap_clears_old_logs_before_input_and_requires_transition(self):
        with tempfile.TemporaryDirectory() as directory:
            smoke = MODULE.Smoke(directory)
            events = []
            smoke.find = lambda _: {'x': 12, 'y': 34}
            smoke.focus = lambda: events.append('focus')
            smoke.clear = lambda: events.append('clear')
            smoke.trace = lambda expression: events.append('fresh:' + expression)
            smoke.controls = lambda: events.append('controls')
            with patch.object(MODULE, 'adb', side_effect=lambda *args: events.append(args)):
                smoke.tap('nav-people', 'tab 2')
            self.assertEqual(events, ['focus', 'clear', ('shell', 'input', 'tap', 12, 34),
                                      'fresh:tab 2', 'controls'])


if __name__ == '__main__':
    unittest.main()
