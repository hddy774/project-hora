#!/usr/bin/env python3
"""Set only SDK paths in the current user's Godot settings; no secret handling."""
import os
import pathlib
import re
import shutil
import sys

config_dir = pathlib.Path(os.environ.get("XDG_CONFIG_HOME", str(pathlib.Path.home() / ".config"))) / "godot"
settings = config_dir / "editor_settings-4.6.tres"
sdk = pathlib.Path(os.environ.get("ANDROID_HOME", os.environ.get("ANDROID_SDK_ROOT", "")))
java_command = shutil.which("java")
java = pathlib.Path(os.environ.get("JAVA_HOME", str(pathlib.Path(java_command).resolve().parent.parent) if java_command else ""))
if not (sdk / "platform-tools/adb").is_file() or not (java / "bin/java").is_file():
    sys.exit("Set ANDROID_HOME to an installed Android SDK and JAVA_HOME to a JDK.")
config_dir.mkdir(parents=True, exist_ok=True)
body = settings.read_text() if settings.exists() else '[gd_resource type="EditorSettings" format=3]\n\n[resource]\n'
for key, value in {"export/android/android_sdk_path": str(sdk.resolve()), "export/android/java_sdk_path": str(java.resolve())}.items():
    # SDK paths are configuration, never key contents or passwords.
    line = key + ' = "' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'
    pattern = r"(?m)^" + re.escape(key) + r"\s*=.*$"
    body = re.sub(pattern, lambda _: line, body) if re.search(pattern, body) else body + line + "\n"
settings.write_text(body)
print("Configured Android SDK and Java SDK paths for Godot 4.6.")
