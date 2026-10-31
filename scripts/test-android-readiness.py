#!/usr/bin/env python3
"""Small parser/flow checks; these fixtures do not claim a real Android run."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("readiness", Path(__file__).with_name("android_readiness.py"))
native = importlib.util.module_from_spec(spec)
spec.loader.exec_module(native)
ACTIVITY = "crc649632d1cee893af2a.MainActivity"
VERSION = (native.ROOT / "VERSION").read_text().strip()
QUICKSTEP = Path(__file__).with_name("fixtures").joinpath("android-quickstep.xml").read_text()
WINDOWS_ONLY = Path(__file__).with_name("fixtures").joinpath("android-window-windows-only.txt").read_text()
LAUNCH_TIMEOUT = Path(__file__).with_name("fixtures").joinpath("android-launch-timeout-api24-41165712717.txt").read_bytes()
APP_UI = '<hierarchy><node package="org.hora.xianxiaobserver" class="android.webkit.WebView" text="만상의 흐름" /></hierarchy>'
APP_LOG = 'I/SeonroRender: ' + json.dumps({"rendered": True, "heading": "만상의 흐름", "title": "선로만상록",
    "version": VERSION, "error": None, "connection": "연결됨"}, ensure_ascii=False) + '\n'
FOREGROUND = "mResumedActivity: ActivityRecord{ org.hora.xianxiaobserver/" + ACTIVITY + " }"
WINDOW = "mCurrentFocus=Window{ u0 org.hora.xianxiaobserver/" + ACTIVITY + " }"


class Clock:
    value = 0

    def now(self):
        return self.value

    def sleep(self, seconds):
        self.value += seconds


class Device:
    def __init__(self, clock, xml=APP_UI, logs=APP_LOG, window=WINDOW):
        self.clock, self.xml, self.logs, self.window = clock, xml, logs, window
        self.calls, self.taps, self.checker_results = [], [], []
        self.launches, self.cleared = 0, False
        self.after_wait = None
        self.timeout_ui = False
        self.stale_only = False

    def run(self, args, **kwargs):
        self.calls.append({"args": args, "time": self.clock.now(), "timeout": kwargs["timeout"]})
        if args[0] == sys.executable:
            # Run the real unchanged verifier, not a mirrored success predicate.
            result = subprocess.run(args, **kwargs)
            self.checker_results.append(result.returncode)
            return result
        self.assert_adb(args)
        command = args[1:]
        if command[:2] == ["logcat", "-c"]:
            self.cleared = True
            output = b""
        elif command[:2] == ["logcat", "-d"]:
            output = ("" if self.stale_only and self.cleared else self.logs).encode()
        elif command[:3] == ["shell", "am", "start"]:
            self.launches += 1
            output = ("Status: ok\nActivity: " + native.PACKAGE + "/" + ACTIVITY + "\nComplete\n").encode()
        elif command[:3] == ["shell", "dumpsys", "activity"]:
            output = FOREGROUND.encode()
        elif command == ["shell", "dumpsys", "window", "windows"]:
            output = WINDOWS_ONLY.encode()
        elif command == ["shell", "dumpsys", "window"]:
            output = self.window.encode()
        elif command[:2] == ["shell", "pidof"]:
            output = b"2274\n"
        elif command[:3] == ["shell", "rm", "-f"]:
            output = b""
        elif command[:3] == ["shell", "uiautomator", "dump"]:
            if self.timeout_ui:
                self.clock.sleep(kwargs["timeout"])
                raise subprocess.TimeoutExpired(args, kwargs["timeout"], output=b"partial dump", stderr=b"dump timeout")
            output = b"UI hierarchy dumped\n"
        elif command[:2] == ["exec-out", "cat"]:
            output = self.xml.encode()
        elif command[:2] == ["exec-out", "screencap"]:
            output = b"\x89PNG\r\n\x1a\nfixture"
        elif command[:3] == ["shell", "input", "tap"]:
            self.taps.append(command[3:])
            if self.after_wait:
                self.xml, self.logs, self.window = self.after_wait
            output = b""
        elif command[:1] == ["install"]:
            output = b"Success\n"
        else:
            raise AssertionError("Unexpected operation: " + repr(args))
        return subprocess.CompletedProcess(args, 0, output, b"")

    @staticmethod
    def assert_adb(args):
        if args[0] != "adb":
            raise AssertionError("Unexpected executable: " + repr(args))


class PendingLaunchDevice(Device):
    """Actual launch stdout, then simulated readiness; this is not a device replay."""
    def __init__(self, clock, *, ready_after=None, launch_code=0, launch_output=LAUNCH_TIMEOUT,
                 launch_stderr=b"", launch_seconds=10.682253):
        super().__init__(clock, xml="<hierarchy />", logs="", window="mCurrentFocus=null")
        self.ready_after = ready_after
        self.launch_code, self.launch_output = launch_code, launch_output
        self.launch_stderr, self.launch_seconds = launch_stderr, launch_seconds

    def run(self, args, **kwargs):
        if args[1:4] == ["shell", "am", "start"]:
            super().run(args, **kwargs)
            if self.launch_seconds > kwargs["timeout"]:
                raise AssertionError("Fixture launch duration exceeds its subprocess budget")
            self.clock.sleep(self.launch_seconds)
            return subprocess.CompletedProcess(args, self.launch_code, self.launch_output, self.launch_stderr)
        if self.ready_after is not None and self.clock.now() >= self.ready_after:
            self.xml, self.logs, self.window = APP_UI, APP_LOG, WINDOW
        return super().run(args, **kwargs)


class AndroidReadinessTests(unittest.TestCase):
    def execute(self, device, out, timeout=3, **kwargs):
        return native.await_ready(ACTIVITY, VERSION, out, timeout=timeout,
            runner=device.run, clock=device.clock.now, sleep=device.clock.sleep, **kwargs)

    def test_real_windows_only_dump_cannot_supply_current_focus(self):
        self.assertTrue(WINDOWS_ONLY.startswith("WINDOW MANAGER WINDOWS (dumpsys window windows)"))
        self.assertNotIn("mCurrentFocus", WINDOWS_ONLY)
        self.assertFalse(native.foreground_app(FOREGROUND, WINDOWS_ONLY))

    def test_stale_launcher_xml_never_taps_the_current_app(self):
        self.assertIsNone(native.inspect_dialog(QUICKSTEP, WINDOW, APP_LOG))

    def test_dialog_appearing_at_screenshot_is_handled_before_acceptance(self):
        device = Device(Clock())
        device.after_wait = (APP_UI, APP_LOG, WINDOW)
        original = device.run
        popped = False
        def popup_during_screen(args, **kwargs):
            nonlocal popped
            result = original(args, **kwargs)
            if args[1:3] == ["exec-out", "screencap"] and not popped:
                popped = True
                device.xml = QUICKSTEP
                device.window = "mCurrentFocus=Window{ Application Not Responding: com.android.launcher3 }"
            return result
        device.run = popup_during_screen
        with tempfile.TemporaryDirectory() as directory:
            self.assertTrue(self.execute(device, Path(directory), timeout=5)["passed"])
            self.assertEqual(len(device.taps), 1)

    def test_launch_status_and_requested_activity_are_both_required(self):
        good = "Status: ok\nActivity: " + native.PACKAGE + "/" + ACTIVITY + "\n"
        native.check_launch(0, good, ACTIVITY)
        for code, text in [(1, good), (0, good.replace("ok", "unknown")),
                           (0, good.replace(native.PACKAGE, "com.android.launcher3")), (0, good + "Error type 3"),
                           (0, good + "java.lang.SecurityException")]:
            with self.subTest(code=code, text=text), self.assertRaises(native.ReadinessError):
                native.check_launch(code, text, ACTIVITY)

    def test_real_quickstep_xml_maps_only_the_wait_resource_and_bounds(self):
        target = native.inspect_dialog(QUICKSTEP, "mCurrentFocus=Window{ Application Not Responding: com.android.launcher3 }", APP_LOG)
        self.assertEqual(target["resource_id"], "android:id/aerr_wait")
        self.assertEqual(target["tap"], [540, 1332])
        for changed in [QUICKSTEP.replace('android:id/aerr_wait', 'android:id/aerr_close'),
                        QUICKSTEP.replace('clickable="true"', 'clickable="false"')]:
            with self.subTest(changed=changed), self.assertRaises(native.ReadinessError):
                native.inspect_dialog(changed, "mCurrentFocus=Window{ Application Not Responding: com.android.launcher3 }", APP_LOG)

    def test_launch_error_on_stderr_keeps_failure_diagnostics(self):
        device = Device(Clock())
        original = device.run
        def stderr_launch(args, **kwargs):
            result = original(args, **kwargs)
            if args[1:4] == ["shell", "am", "start"]:
                return subprocess.CompletedProcess(args, 0, result.stdout, b"java.lang.SecurityException\n")
            return result
        device.run = stderr_launch
        with tempfile.TemporaryDirectory() as directory:
            out = Path(directory)
            with self.assertRaisesRegex(native.ReadinessError, "launch"):
                self.execute(device, out)
            self.assertTrue((out / "foreground.txt").exists())
            self.assertTrue((out / "ui.xml").exists())
            self.assertEqual(device.checker_results, [])
            self.assertEqual(device.launches, 1)

    def test_app_unknown_and_mismatched_owner_dialogs_are_rejected(self):
        for xml, window, logs in [(QUICKSTEP.replace("Quickstep", "선로만상록"), "", APP_LOG),
                                 (QUICKSTEP.replace("Quickstep", "Settings"), "", APP_LOG),
                                 (QUICKSTEP, "mCurrentFocus=Window{ Application Not Responding: " + native.PACKAGE + " }", APP_LOG),
                                 (QUICKSTEP, "", APP_LOG + "ActivityManager: ANR in " + native.PACKAGE)]:
            with self.subTest(xml=xml, window=window), self.assertRaises(native.ReadinessError):
                native.inspect_dialog(xml, window, logs)

    def test_external_wait_then_real_verifier_success_preserves_before_and_after(self):
        clock = Clock()
        device = Device(clock, QUICKSTEP, window="mCurrentFocus Application Not Responding: com.android.launcher3")
        device.after_wait = (APP_UI, APP_LOG, WINDOW)
        with tempfile.TemporaryDirectory() as directory:
            out = Path(directory)
            result = self.execute(device, out, install_apk="fixture.apk")
            self.assertTrue(result["passed"])
            self.assertEqual(device.launches, 1)
            self.assertEqual(device.taps, [["540", "1332"]])
            self.assertEqual(device.checker_results, [0])
            events = json.loads((out / "launcher-waits.json").read_text())
            self.assertEqual(len(events), 1)
            event = events[0]
            self.assertTrue(event["after_capture_complete"])
            self.assertIn("Quickstep", (out / event["before"] / "ui.xml").read_text())
            self.assertIn("android.webkit.WebView", (out / event["after"] / "ui.xml").read_text())
            for folder in [event["before"], event["after"]]:
                for name in ["render-log.txt", "foreground.txt", "window.txt", "screen.png"]:
                    self.assertTrue((out / folder / name).is_file())
            self.assertEqual(json.loads((out / "dom-proof.json").read_text())["version"], VERSION)

    def test_app_anr_and_unknown_never_tap_or_run_the_verifier(self):
        for title in ["선로만상록", "Settings"]:
            device = Device(Clock(), QUICKSTEP.replace("Quickstep", title))
            with self.subTest(title=title), tempfile.TemporaryDirectory() as directory:
                out = Path(directory)
                with self.assertRaises(native.ReadinessError):
                    self.execute(device, out)
                self.assertEqual(device.taps, [])
                self.assertEqual(device.checker_results, [])
                self.assertFalse(json.loads((out / "readiness.json").read_text())["passed"])
                for name in ["render-log.txt", "ui.xml", "foreground.txt", "window.txt", "screen.png"]:
                    self.assertTrue((out / name).is_file())

    def test_own_crash_is_terminal_even_with_old_good_render_marker(self):
        device = Device(Clock(), logs=APP_LOG + "AndroidRuntime: FATAL EXCEPTION: main\n")
        with tempfile.TemporaryDirectory() as directory, self.assertRaisesRegex(native.ReadinessError, "crashed"):
            self.execute(device, Path(directory))
        self.assertEqual(device.taps, [])
        self.assertEqual(device.checker_results, [])

    def test_deadline_is_not_reset_by_repeated_launcher_wait(self):
        clock = Clock()
        device = Device(clock, QUICKSTEP, window="mCurrentFocus=Window{ Application Not Responding: com.android.launcher3 }")
        # Actual tap consumes this fixture's remaining budget; no fresh 90-second reader is created.
        original = device.run
        def timed(args, **kwargs):
            result = original(args, **kwargs)
            if args[1:4] == ["shell", "input", "tap"]:
                clock.sleep(kwargs["timeout"])
            return result
        device.run = timed
        with tempfile.TemporaryDirectory() as directory:
            out = Path(directory)
            with self.assertRaisesRegex(native.ReadinessError, "deadline"):
                self.execute(device, out, timeout=2)
            self.assertEqual(clock.now(), 2)
            self.assertEqual(len(device.taps), 1)
            self.assertEqual(device.launches, 1)
            self.assertFalse((out / "dom-proof.json").exists())
            self.assertTrue(all(call["time"] < 2 for call in device.calls))

    def test_real_verifier_rejects_wrong_dom_and_stale_prelaunch_marker(self):
        for stale in [False, True]:
            clock = Clock()
            device = Device(clock, logs=APP_LOG if stale else APP_LOG.replace("만상의 흐름", "잘못된 화면"))
            device.stale_only = stale
            with self.subTest(stale=stale), tempfile.TemporaryDirectory() as directory:
                out = Path(directory)
                with self.assertRaisesRegex(native.ReadinessError, "deadline"):
                    self.execute(device, out, timeout=2)
                self.assertEqual(device.checker_results, [1, 1])
                self.assertFalse((out / "dom-proof.json").exists())

    def test_other_foreground_does_not_pass_even_with_app_xml_and_render(self):
        device = Device(Clock(), window="mCurrentFocus=Window{ com.android.launcher3/.Launcher }")
        with tempfile.TemporaryDirectory() as directory, self.assertRaisesRegex(native.ReadinessError, "deadline"):
            self.execute(device, Path(directory), timeout=2)
        self.assertEqual(device.checker_results, [])

    def test_invalid_or_other_package_xml_cannot_pass_by_matching_raw_strings(self):
        for xml in [APP_UI[:-2], APP_UI.replace('package="org.hora.xianxiaobserver"', 'package="android"')]:
            device = Device(Clock(), xml=xml)
            with self.subTest(xml=xml), tempfile.TemporaryDirectory() as directory:
                with self.assertRaisesRegex(native.ReadinessError, "deadline"):
                    self.execute(device, Path(directory), timeout=2)
                self.assertEqual(device.checker_results, [])

    def test_timed_out_ui_preserves_partial_diagnostics_without_new_deadline(self):
        clock = Clock()
        device = Device(clock)
        device.timeout_ui = True
        with tempfile.TemporaryDirectory() as directory:
            out = Path(directory)
            with self.assertRaisesRegex(native.ReadinessError, "deadline"):
                self.execute(device, out, timeout=2)
            self.assertEqual(clock.now(), 2)
            self.assertTrue(any(c["timed_out"] for c in json.loads((out / "commands.json").read_text())))
            self.assertIn("partial dump", (out / "attempt-001/uiautomator.txt").read_text())
            self.assertTrue((out / "render-log.txt").exists())

    def test_android_launch_timeout_waits_for_delayed_real_verifier(self):
        clock = Clock()
        device = PendingLaunchDevice(clock, ready_after=13.682253)
        with tempfile.TemporaryDirectory() as directory:
            out = Path(directory)
            self.assertTrue(self.execute(device, out, timeout=90)["passed"])
            readiness = json.loads((out / "readiness.json").read_text())
            self.assertEqual((out / "launch.txt").read_bytes(), LAUNCH_TIMEOUT)
            self.assertEqual(readiness["launch"], {
                "adb_returncode": 0, "subprocess_timed_out": False,
                "raw_status_lines": ["Status: timeout"],
                "state": "wait_incomplete", "android_status": "timeout"})
            self.assertTrue(readiness["passed"])
            self.assertGreater(readiness["attempts"], 1)
            self.assertAlmostEqual(clock.now(), 13.682253)
            self.assertEqual(device.launches, 1)
            self.assertEqual(device.checker_results, [0])
            self.assertTrue(json.loads((out / "dom-proof.json").read_text())["core_to_native_bridge"])
            self.assertTrue(all(call["time"] < 90 for call in device.calls))

    def test_android_launch_timeout_never_ready_uses_original_90_second_deadline(self):
        clock = Clock()
        device = PendingLaunchDevice(clock)
        with tempfile.TemporaryDirectory() as directory:
            out = Path(directory)
            with self.assertRaisesRegex(native.ReadinessError, "readiness deadline"):
                self.execute(device, out, timeout=90)
            readiness = json.loads((out / "readiness.json").read_text())
            self.assertEqual(clock.now(), 90)
            self.assertEqual(readiness["elapsed_ms"], 90000)
            self.assertEqual(readiness["deadline_seconds"], 90)
            self.assertFalse(readiness["passed"])
            self.assertEqual(readiness["launch"]["state"], "wait_incomplete")
            self.assertEqual(device.launches, 1)
            self.assertEqual(device.checker_results, [])
            self.assertFalse((out / "dom-proof.json").exists())
            self.assertTrue(all(call["time"] < 90 for call in device.calls))
            self.assertEqual((out / "launch.txt").read_bytes(), LAUNCH_TIMEOUT)

    def test_install_log_capture_and_launch_share_original_90_second_deadline(self):
        clock = Clock()
        device = PendingLaunchDevice(clock)
        original = device.run

        def consume_startup_budget(args, **kwargs):
            if args[1:3] == ["logcat", "-d"] and not device.cleared:
                clock.sleep(4)
            elif args[1:2] == ["install"]:
                clock.sleep(40)
            elif args[1:3] == ["logcat", "-c"]:
                clock.sleep(2)
            return original(args, **kwargs)

        device.run = consume_startup_budget
        with tempfile.TemporaryDirectory() as directory:
            out = Path(directory)
            with self.assertRaisesRegex(native.ReadinessError, "readiness deadline"):
                self.execute(device, out, timeout=90, install_apk="fixture.apk")
            self.assertEqual(clock.now(), 90)
            self.assertEqual(device.launches, 1)
            commands = json.loads((out / "commands.json").read_text())
            launch = next(c for c in commands if c["path"] == "launch.txt")
            self.assertEqual(launch["elapsed_ms"], 56682.253)
            launch_call = next(c for c in device.calls if c["args"][1:4] == ["shell", "am", "start"])
            self.assertEqual(launch_call["timeout"], 44)
            self.assertTrue(all(call["time"] < 90 for call in device.calls))
            self.assertFalse(json.loads((out / "readiness.json").read_text())["passed"])

    def test_android_launch_timeout_rejects_nonzero_other_activity_errors_and_unknown_status(self):
        bad = [
            {"launch_code": 1},
            {"launch_output": LAUNCH_TIMEOUT.replace(ACTIVITY.encode(), b"com.android.launcher3.Launcher")},
            {"launch_output": LAUNCH_TIMEOUT.replace(b"timeout", b"unknown")},
            {"launch_stderr": b"Error type 3\n"},
            {"launch_stderr": b"java.lang.SecurityException\n"},
        ]
        for change in bad:
            device = PendingLaunchDevice(Clock(), **change)
            with self.subTest(change=change), tempfile.TemporaryDirectory() as directory:
                out = Path(directory)
                with self.assertRaisesRegex(native.ReadinessError, "launch"):
                    self.execute(device, out, timeout=90)
                readiness = json.loads((out / "readiness.json").read_text())
                self.assertFalse(readiness["passed"])
                self.assertEqual(readiness["attempts"], 0)
                self.assertEqual(readiness["launch"]["state"], "rejected")
                self.assertEqual(device.launches, 1)
                self.assertEqual(device.checker_results, [])

    def test_subprocess_launch_timeout_is_terminal_with_partial_android_timeout_output(self):
        clock = Clock()
        device = Device(clock)
        original = device.run

        def timeout_launch(args, **kwargs):
            result = original(args, **kwargs)
            if args[1:4] == ["shell", "am", "start"]:
                clock.sleep(kwargs["timeout"])
                raise subprocess.TimeoutExpired(args, kwargs["timeout"], output=LAUNCH_TIMEOUT, stderr=b"adb partial\n")
            return result

        device.run = timeout_launch
        with tempfile.TemporaryDirectory() as directory:
            out = Path(directory)
            with self.assertRaisesRegex(native.ReadinessError, "launch"):
                self.execute(device, out, timeout=90)
            readiness = json.loads((out / "readiness.json").read_text())
            self.assertEqual(clock.now(), 45)
            self.assertFalse(readiness["passed"])
            self.assertEqual(readiness["attempts"], 0)
            self.assertTrue(readiness["launch"]["subprocess_timed_out"])
            self.assertIsNone(readiness["launch"]["adb_returncode"])
            self.assertEqual(readiness["launch"]["state"], "rejected")
            self.assertEqual((out / "launch.txt").read_bytes(), LAUNCH_TIMEOUT)
            self.assertEqual((out / "launch.txt.stderr.txt").read_bytes(), b"adb partial\n")
            self.assertEqual(device.launches, 1)
            self.assertEqual(device.checker_results, [])

    def test_os_launch_failure_is_terminal_and_preserves_stderr(self):
        device = Device(Clock())
        original = device.run

        def broken_adb(args, **kwargs):
            result = original(args, **kwargs)
            if args[1:4] == ["shell", "am", "start"]:
                raise OSError("fixture adb unavailable")
            return result

        device.run = broken_adb
        with tempfile.TemporaryDirectory() as directory:
            out = Path(directory)
            with self.assertRaisesRegex(native.ReadinessError, "launch"):
                self.execute(device, out, timeout=90)
            readiness = json.loads((out / "readiness.json").read_text())
            self.assertEqual(readiness["attempts"], 0)
            self.assertEqual(readiness["launch"]["state"], "rejected")
            self.assertFalse(readiness["launch"]["subprocess_timed_out"])
            self.assertIsNone(readiness["launch"]["adb_returncode"])
            self.assertIn("fixture adb unavailable", (out / "launch.txt.stderr.txt").read_text())
            self.assertEqual(device.launches, 1)
            self.assertEqual(device.checker_results, [])

    def test_pending_launch_keeps_foreground_dom_crash_and_anr_requirements(self):
        frames = [
            (APP_UI, APP_LOG, "mCurrentFocus=null"),
            (APP_UI.replace("android.webkit.WebView", "android.view.View"), APP_LOG, WINDOW),
            (APP_UI, APP_LOG.replace("만상의 흐름", "잘못된 화면"), WINDOW),
            (APP_UI, APP_LOG + "AndroidRuntime: FATAL EXCEPTION: main\n", WINDOW),
            (APP_UI, APP_LOG + "ActivityManager: ANR in " + native.PACKAGE + "\n", WINDOW),
            (QUICKSTEP.replace("Quickstep", "Settings"), APP_LOG, WINDOW),
        ]
        for xml, logs, window in frames:
            device = PendingLaunchDevice(Clock())
            device.xml, device.logs, device.window = xml, logs, window
            with self.subTest(xml=xml, logs=logs, window=window), tempfile.TemporaryDirectory() as directory:
                out = Path(directory)
                with self.assertRaises(native.ReadinessError):
                    self.execute(device, out, timeout=12)
                self.assertFalse(json.loads((out / "readiness.json").read_text())["passed"])
                self.assertFalse((out / "dom-proof.json").exists())
                self.assertEqual(device.launches, 1)
                self.assertEqual(device.taps, [])

    def test_launch_parser_requires_one_known_status_and_one_exact_activity(self):
        good = LAUNCH_TIMEOUT.decode()
        for changed in [good.replace("Status: timeout\n", ""), good.replace("timeout", "Timeout"),
                        good + "Status: ok\n", good.replace("Activity: ", "Other: "),
                        good + "Activity: " + native.PACKAGE + "/" + ACTIVITY + "\n"]:
            with self.subTest(changed=changed), self.assertRaises(native.ReadinessError):
                native.check_launch(0, changed, ACTIVITY)
        for status in ["ok", "timeout"]:
            self.assertEqual(native.check_launch(0, good.replace("timeout", status), ACTIVITY), status)
            abbreviated = good.replace("timeout", status).replace(ACTIVITY, ".MainActivity")
            self.assertEqual(native.check_launch(0, abbreviated, ".MainActivity"), status)

    def test_pending_launch_cannot_pass_without_pid_or_complete_screenshot(self):
        for missing in ["pid", "screenshot"]:
            device = PendingLaunchDevice(Clock(), ready_after=10.682253)
            original = device.run

            def missing_proof(args, **kwargs):
                result = original(args, **kwargs)
                if missing == "pid" and args[1:3] == ["shell", "pidof"]:
                    return subprocess.CompletedProcess(args, 0, b"", b"")
                if missing == "screenshot" and args[1:3] == ["exec-out", "screencap"]:
                    return subprocess.CompletedProcess(args, 1, b"", b"capture failed")
                return result

            device.run = missing_proof
            with self.subTest(missing=missing), tempfile.TemporaryDirectory() as directory:
                out = Path(directory)
                with self.assertRaisesRegex(native.ReadinessError, "readiness deadline"):
                    self.execute(device, out, timeout=12)
                self.assertFalse(json.loads((out / "readiness.json").read_text())["passed"])
                self.assertFalse((out / "dom-proof.json").exists())
                self.assertEqual(device.checker_results, [])
                self.assertEqual(device.launches, 1)


if __name__ == "__main__":
    unittest.main()
