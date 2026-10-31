#!/usr/bin/env python3
"""Bounded, observed Android readiness shared by CI and release render checks."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
PACKAGE = "org.hora.xianxiaobserver"
LAUNCHERS = {"Quickstep": "com.android.launcher3", "Launcher3": "com.android.launcher3",
             "Pixel Launcher": "com.google.android.apps.nexuslauncher"}


class ReadinessError(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise ReadinessError(message)


def check_launch(returncode, output, activity):
    statuses = re.findall(r"(?m)^Status:([^\r\n]*)\r?$", output)
    require(returncode == 0 and len(statuses) == 1 and statuses[0].strip() in ("ok", "timeout") and
            not re.search(r"Error|Exception", output, re.IGNORECASE), "Android launch did not succeed")
    found = re.findall(r"(?m)^Activity:([^\r\n]*)\r?$", output)
    expected = PACKAGE + "/" + (PACKAGE + activity if activity.startswith(".") else activity)
    actual = found[0].strip() if len(found) == 1 else ""
    if actual.startswith(PACKAGE + "/."):
        actual = PACKAGE + "/" + PACKAGE + actual.split("/", 1)[1]
    require(actual == expected, "Android launch did not report the requested app activity")
    return statuses[0].strip()


def inspect_dialog(xml, windows, logs):
    """Return only an explicit launcher Wait target; never infer a generic dismissal."""
    require("FATAL EXCEPTION" not in logs and "Fatal signal" not in logs, "Actual Android process crashed")
    require(not re.search(r"\bANR in\s+" + re.escape(PACKAGE) + r"(?:\s|$|:)", logs), "Application ANR observed")
    focused_window = "\n".join(line for line in windows.splitlines() if "mCurrentFocus" in line)
    owners = re.findall(r"Application Not Responding:\s*([A-Za-z0-9_.]+)", focused_window)
    require(all(owner in LAUNCHERS.values() for owner in owners), "Application or unknown ANR window observed")
    require("Application Error:" not in focused_window, "Android crash dialog observed")
    try:
        nodes = list(ET.fromstring(xml).iter("node"))
    except ET.ParseError:
        return None
    titles = [n.attrib.get("text", "") for n in nodes if n.attrib.get("resource-id", "").endswith("/alertTitle")]
    dialog = titles or any(n.attrib.get("resource-id", "").endswith(("/aerr_wait", "/aerr_close", "/parentPanel")) or
                           "Dialog" in n.attrib.get("class", "") for n in nodes)
    if not dialog:
        return None
    require(len(titles) == 1, "Unknown Android dialog; no automatic dismissal allowed")
    title = titles[0]
    matches = [(label, package) for label, package in LAUNCHERS.items()
               if title in (label + " isn't responding", label + " isn’t responding", label + " is not responding")]
    require(len(matches) == 1, "Application or unknown Android dialog; no automatic dismissal allowed")
    label, package = matches[0]
    require(all(n.attrib.get("package") == "android" for n in nodes), "Unknown dialog package; no automatic dismissal allowed")
    if not owners:
        return None  # The dialog disappeared after the UI dump; never tap coordinates in a newly focused app.
    # A named ANR window, when present, must agree with the exact launcher label.
    require(not owners or all(owner == package for owner in owners), "ANR window does not belong to the observed launcher")
    waits = [n for n in nodes if n.attrib.get("resource-id") == "android:id/aerr_wait"]
    require(len(waits) == 1, "Launcher ANR has no unique Wait button")
    button = waits[0].attrib
    require(button.get("class") == "android.widget.Button" and button.get("enabled") == "true" and
            button.get("clickable") == "true" and button.get("text") in ("Wait", "대기"), "Launcher Wait button is not actionable")
    bounds = re.fullmatch(r"\[(\d+),(\d+)\]\[(\d+),(\d+)\]", button.get("bounds", ""))
    require(bounds is not None, "Launcher Wait bounds missing")
    x1, y1, x2, y2 = map(int, bounds.groups())
    require(0 <= x1 < x2 <= 10000 and 0 <= y1 < y2 <= 10000, "Launcher Wait bounds invalid")
    return {"label": label, "package": package, "title": title, "resource_id": "android:id/aerr_wait",
            "bounds": [x1, y1, x2, y2], "tap": [(x1 + x2) // 2, (y1 + y2) // 2]}


def foreground_app(activities, windows):
    resumed = any(PACKAGE + "/" in line and re.search(r"(?:mResumedActivity|mFocusedActivity|[Tt]opResumedActivity)", line)
                  for line in activities.splitlines())
    focused = any("mCurrentFocus" in line and PACKAGE + "/" in line for line in windows.splitlines())
    return resumed and focused


def _bytes(value):
    return value.encode("utf-8") if isinstance(value, str) else value or b""


class Reader:
    def __init__(self, out, timeout, runner, clock):
        self.out, self.runner, self.clock = Path(out), runner, clock
        self.started = clock()
        self.deadline = self.started + timeout  # One deadline for install, launch, polling, Wait and final diagnostics.
        self.commands, self.waits, self.latest = [], [], None
        self.preinstall = None
        self.out.mkdir(parents=True, exist_ok=True)

    def remaining(self):
        return self.deadline - self.clock()

    def command(self, directory, name, args, maximum=10):
        directory.mkdir(parents=True, exist_ok=True)
        remaining = self.remaining()
        stdout, stderr, code, timed_out = b"", b"", None, False
        if remaining > 0:
            try:
                result = self.runner(list(map(str, args)), capture_output=True, timeout=min(maximum, remaining), cwd=ROOT)
                stdout, stderr, code = _bytes(result.stdout), _bytes(result.stderr), result.returncode
            except subprocess.TimeoutExpired as error:
                stdout, stderr, timed_out = _bytes(error.stdout), _bytes(error.stderr), True
            except OSError as error:
                stderr = str(error).encode("utf-8")
        (directory / name).write_bytes(stdout)
        (directory / (name + ".stderr.txt")).write_bytes(stderr)
        self.commands.append({"path": str((directory / name).relative_to(self.out)), "command": list(map(str, args)),
                              "returncode": code, "timed_out": timed_out, "started_before_deadline": remaining > 0,
                              "elapsed_ms": round((self.clock() - self.started) * 1000, 3)})
        return code, stdout.decode("utf-8", errors="replace"), stdout

    def snapshot(self, name, *, require_app_pid=True):
        path = self.out / name
        log = self.command(path, "render-log.txt", ["adb", "logcat", "-d", "-v", "tag"])
        activities = self.command(path, "foreground.txt", ["adb", "shell", "dumpsys", "activity", "activities"])
        pid = self.command(path, "pid.txt", ["adb", "shell", "pidof", PACKAGE]) if require_app_pid else (None, "", b"")
        remote = "/sdcard/v021-readiness-ui.xml"
        removed = self.command(path, "remove-stale-ui.txt", ["adb", "shell", "rm", "-f", remote])
        dumped = self.command(path, "uiautomator.txt", ["adb", "shell", "uiautomator", "dump", remote])
        ui = self.command(path, "ui.xml", ["adb", "exec-out", "cat", remote]) if removed[0] == dumped[0] == 0 else (None, "", b"")
        if not (path / "ui.xml").exists():
            (path / "ui.xml").write_bytes(b"")
        screen = self.command(path, "screen.png", ["adb", "exec-out", "screencap", "-p"])
        # Verify focus after UI/screenshot capture so a newly appeared dialog cannot use an older app focus.
        windows = self.command(path, "window.txt", ["adb", "shell", "dumpsys", "window"])
        try:
            tree = ET.fromstring(ui[1])
            valid_ui = tree.tag == "hierarchy"
            webview = any(n.attrib.get("class") == "android.webkit.WebView" and n.attrib.get("package") == PACKAGE
                          for n in tree.iter("node"))
        except ET.ParseError:
            valid_ui, webview = False, False
        required = (log, activities, windows, pid, ui, screen) if require_app_pid else (log, activities, windows, ui, screen)
        value = {"path": path, "logs": log[1], "activities": activities[1], "windows": windows[1],
                 "pid": pid[1].strip() if pid[0] == 0 else "", "xml": ui[1], "webview": webview,
                 "complete": all(x[0] == 0 for x in required) and
                             valid_ui and screen[2].startswith(b"\x89PNG\r\n\x1a\n")}
        self.latest = value
        return value

    def publish_diagnostics(self):
        if self.latest:
            for name in ("render-log.txt", "foreground.txt", "window.txt", "pid.txt", "ui.xml", "screen.png"):
                source = self.latest["path"] / name
                if source.exists():
                    shutil.copyfile(source, self.out / name)
        (self.out / "commands.json").write_text(json.dumps(self.commands, ensure_ascii=False, indent=2) + "\n")
        (self.out / "launcher-waits.json").write_text(json.dumps(self.waits, ensure_ascii=False, indent=2) + "\n")
        if self.preinstall is not None:
            (self.out / "preinstall.json").write_text(json.dumps(self.preinstall, ensure_ascii=False, indent=2) + "\n")


def _preinstall_probe(reader, path, name, args, allowed):
    code, output, _ = reader.command(path, name, args)
    record = reader.commands[-1]
    stderr = (path / (name + ".stderr.txt")).read_text(encoding="utf-8", errors="replace").strip()
    require(not record["timed_out"] and code is not None, "Preinstall ADB probe timed out or could not execute")
    # 전송이 명시적으로 offline인 읽기 probe만 기다린다. 설치 오류를 재시도하지 않는다.
    if code in (1, 255) and not output.strip() and stderr in ("adb: device offline", "error: device offline"):
        return "offline"
    require(code == 0 and not stderr and output.strip() in allowed, "Unknown preinstall ADB probe result: " + name)
    return output.strip()


def _normal_device_ui(observed):
    nodes = list(ET.fromstring(observed["xml"]).iter("node"))
    focused = [line for line in observed["windows"].splitlines() if "mCurrentFocus" in line]
    normal_focus = any("Window{" in line and "Application Not Responding:" not in line and
                       "Application Error:" not in line for line in focused)
    dialog_xml = any(n.attrib.get("resource-id", "").endswith(("/alertTitle", "/aerr_wait", "/aerr_close", "/parentPanel")) or
                     "Dialog" in n.attrib.get("class", "") for n in nodes)
    return bool(nodes) and normal_focus and not dialog_xml


def _prepare_install(reader, sleep):
    """설치 전에 기기만 준비한다. 앱 PID나 render 성공을 뜻하지 않는다."""
    state = {"state": "waiting", "attempts": 0, "transport": None, "boot_completed": None, "ready_at_ms": None}
    reader.preinstall = state
    while reader.remaining() > 0:
        state["attempts"] += 1
        path = reader.out / ("preinstall-" + str(state["attempts"]).zfill(3))
        state["state"] = "transport_wait"
        state["transport"] = _preinstall_probe(reader, path, "transport.txt", ["adb", "get-state"], ("device", "offline"))
        if state["transport"] == "device":
            state["state"] = "framework_wait"
            state["boot_completed"] = _preinstall_probe(reader, path, "boot.txt",
                ["adb", "shell", "getprop", "sys.boot_completed"], ("", "0", "1"))
            if state["boot_completed"] == "offline":
                state.update(state="transport_wait", transport="offline")
            if state["boot_completed"] == "1":
                state["state"] = "device_ui"
                first_command = len(reader.commands)
                observed = reader.snapshot(str(path.relative_to(reader.out) / "device"), require_app_pid=False)
                wait = inspect_dialog(observed["xml"], observed["windows"], observed["logs"])
                require(observed["complete"] and not any(c["timed_out"] for c in reader.commands[first_command:]),
                        "Preinstall device observation incomplete or timed out")
                if wait:
                    state["state"] = "launcher_wait"
                    event = {**wait, "number": len(reader.waits) + 1, "phase": "preinstall",
                             "before": str(observed["path"].relative_to(reader.out)),
                             "observed_utc": datetime.now(timezone.utc).isoformat(),
                             "elapsed_before_ms": round((reader.clock() - reader.started) * 1000, 3)}
                    reader.waits.append(event)
                    event["tap_started_utc"] = datetime.now(timezone.utc).isoformat()
                    code, _, _ = reader.command(observed["path"], "wait-tap.txt", ["adb", "shell", "input", "tap", *wait["tap"]])
                    event["tap_completed_utc"] = datetime.now(timezone.utc).isoformat()
                    event["tap_returncode"] = code
                    require(code == 0, "External launcher Wait action failed before install")
                    require(reader.remaining() > 0, "Preinstall device readiness deadline exceeded")
                    first_command = len(reader.commands)
                    after = reader.snapshot("preinstall-wait-after-" + str(event["number"]).zfill(3), require_app_pid=False)
                    event.update(after=str(after["path"].relative_to(reader.out)),
                                 after_utc=datetime.now(timezone.utc).isoformat(),
                                 elapsed_after_ms=round((reader.clock() - reader.started) * 1000, 3),
                                 after_capture_complete=after["complete"])
                    inspect_dialog(after["xml"], after["windows"], after["logs"])
                    require(after["complete"] and not any(c["timed_out"] for c in reader.commands[first_command:]),
                            "Preinstall Wait observation incomplete or timed out")
                    # 다음 회차에서 새 transport/UI를 확인하며 이전 좌표를 다시 쓰지 않는다.
                elif _normal_device_ui(observed) and reader.remaining() > 0:
                    state.update(state="ready", ready_at_ms=round((reader.clock() - reader.started) * 1000, 3))
                    return
        sleep(max(0, min(1, reader.remaining())))
    raise ReadinessError("Preinstall device readiness deadline exceeded")


def await_ready(activity, version, out, *, install_apk=None, timeout=90, runner=subprocess.run,
                clock=time.monotonic, sleep=time.sleep):
    require(0 < timeout <= 90, "Readiness deadline must be positive and at most 90 seconds")
    require(re.fullmatch(r"[A-Za-z0-9_.$]+", activity or ""), "Launch activity is invalid")
    reader = Reader(out, timeout, runner, clock)
    passed, reason, attempts = False, "", 0
    launch = None
    try:
        # Preserve the previous log before clearing stale render markers for this one launch.
        reader.command(reader.out, "prelaunch-logcat.txt", ["adb", "logcat", "-d", "-v", "tag"])
        if install_apk is not None:
            _prepare_install(reader, sleep)
            code, installed, _ = reader.command(reader.out, "install.txt", ["adb", "install", "-r", "--no-streaming", install_apk], 45)
            install_stderr = (reader.out / "install.txt.stderr.txt").read_text(encoding="utf-8", errors="replace")
            require(code == 0 and re.search(r"(?m)^Success\s*$", installed) and
                    not re.search(r"Failure|Error|Exception", installed + "\n" + install_stderr, re.IGNORECASE),
                    "Actual APK installation failed")
        require(reader.command(reader.out, "clear-logcat.txt", ["adb", "logcat", "-c"])[0] == 0, "Could not clear stale Android render log")
        code, launched, _ = reader.command(reader.out, "launch.txt", ["adb", "shell", "am", "start", "-W", "-n", PACKAGE + "/" + activity], 45)
        launch = {"adb_returncode": code, "subprocess_timed_out": reader.commands[-1]["timed_out"],
                  "raw_status_lines": [line for line in launched.splitlines() if line.startswith("Status:")],
                  "state": "rejected", "android_status": None}
        launch_stderr = (reader.out / "launch.txt.stderr.txt").read_text(encoding="utf-8", errors="replace")
        status = check_launch(code, launched + "\n" + launch_stderr, activity)
        # Android 시작 대기는 앱 준비보다 먼저 끝날 수 있다. 두 status 모두 준비 증거가 아니며,
        # 같은 reader/deadline에서 아래의 기존 전경·DOM 검사를 통과해야 한다.
        launch.update(android_status=status, state="wait_incomplete" if status == "timeout" else "wait_complete")
        pending = None
        while reader.remaining() > 0:
            attempts += 1
            observed = pending or reader.snapshot("attempt-" + str(attempts).zfill(3))
            pending = None
            wait = inspect_dialog(observed["xml"], observed["windows"], observed["logs"])
            if wait:
                event = {**wait, "number": len(reader.waits) + 1,
                         "before": str(observed["path"].relative_to(reader.out)),
                         "observed_utc": datetime.now(timezone.utc).isoformat(),
                         "elapsed_before_ms": round((clock() - reader.started) * 1000, 3)}
                reader.waits.append(event)
                event["tap_started_utc"] = datetime.now(timezone.utc).isoformat()
                code, _, _ = reader.command(observed["path"], "wait-tap.txt", ["adb", "shell", "input", "tap", *wait["tap"]])
                event["tap_completed_utc"] = datetime.now(timezone.utc).isoformat()
                event["tap_returncode"] = code
                # Capture after the only permitted action, under the very same deadline.
                after = reader.snapshot("wait-after-" + str(event["number"]).zfill(3))
                event["after"] = str(after["path"].relative_to(reader.out))
                event["after_utc"] = datetime.now(timezone.utc).isoformat()
                event["elapsed_after_ms"] = round((clock() - reader.started) * 1000, 3)
                event["after_capture_complete"] = after["complete"]
                require(code == 0, "External launcher Wait action failed")
                if (after["complete"] and after["webview"] and foreground_app(after["activities"], after["windows"]) and
                        inspect_dialog(after["xml"], after["windows"], after["logs"]) is None):
                    pending = after  # Reuse only a ready, dialog-free observation; never reuse it for another tap.
                else:
                    sleep(max(0, min(1, reader.remaining())))
                continue
            if observed["complete"] and observed["webview"] and observed["pid"] and foreground_app(observed["activities"], observed["windows"]):
                code, checked, _ = reader.command(observed["path"], "dom-checker.txt",
                    [sys.executable, ROOT / "scripts/verify-android-ui.py", observed["path"] / "ui.xml", observed["path"] / "render-log.txt"])
                if code == 0 and reader.remaining() > 0:
                    dom = json.loads(checked)
                    require(dom.get("android_actual_install_launch_and_render") is True and
                            dom.get("core_to_native_bridge") is True and dom.get("version") == version,
                            "Native DOM checker did not prove the expected app bridge")
                    (reader.out / "dom-proof.json").write_text(checked, encoding="utf-8")
                    shutil.copyfile(observed["path"] / "dom-checker.txt", reader.out / "dom-checker.txt")
                    passed = True
                    return {"passed": True, **{key: hashlib.sha256((observed["path"] / name).read_bytes()).hexdigest()
                        for key, name in (("render_log_sha256", "render-log.txt"), ("ui_sha256", "ui.xml"), ("screenshot_sha256", "screen.png"))},
                        "dom_proof_sha256": hashlib.sha256((reader.out / "dom-proof.json").read_bytes()).hexdigest()}
            sleep(max(0, min(1, reader.remaining())))
        raise ReadinessError("Actual app WebView/foreground/DOM readiness deadline exceeded")
    except (ValueError, OSError) as error:
        reason = str(error)
        raise
    finally:
        if not passed and reader.remaining() > 0:
            reader.snapshot("final")
        reader.publish_diagnostics()
        (reader.out / "readiness.json").write_text(json.dumps({"passed": passed, "reason": reason,
            "launch": launch,
            "deadline_seconds": timeout, "attempts": attempts, "launcher_wait_count": len(reader.waits),
            "elapsed_ms": round((clock() - reader.started) * 1000, 3),
            "last_snapshot": str(reader.latest["path"].relative_to(reader.out)) if reader.latest else None,
            "final_diagnostics_share_launch_deadline": True}, ensure_ascii=False, indent=2) + "\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--activity", required=True)
    parser.add_argument("--apk", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    apk = Path(args.apk)
    digest = hashlib.sha256(apk.read_bytes()).hexdigest()
    result = await_ready(args.activity, (ROOT / "VERSION").read_text().strip(), Path(args.output), install_apk=apk)
    require(hashlib.sha256(apk.read_bytes()).hexdigest() == digest, "APK bytes changed during readiness check")
    result["apk_sha256"] = digest
    print(json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError) as error:
        print("Actual Android readiness failed: " + str(error), file=sys.stderr)
        sys.exit(1)
