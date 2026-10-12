#!/usr/bin/env python3
"""Enforce dependency direction, shared-source ownership and clean releases."""
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def violations(root=ROOT):
    errors = []
    dependencies = {
        "AlphaExchange.Core": set(),
        "AlphaExchange.Client": {"AlphaExchange.Core"},
        "AlphaExchange.Android": {"AlphaExchange.Core", "AlphaExchange.Client"},
    }
    for name, allowed in dependencies.items():
        folder = root / "src" / name
        project = ET.parse(folder / f"{name}.csproj")
        for reference in project.findall(".//ProjectReference"):
            target = Path(reference.attrib["Include"]).stem
            if target not in allowed:
                errors.append(f"{name}: disallowed project dependency {target}")
        for reference in project.findall(".//Compile"):
            if "Include" in reference.attrib:
                errors.append(f"{name}: use project references instead of linked source")
        for source in folder.rglob("*.cs"):
            if {"bin", "obj"} & set(source.parts):
                continue
            text = source.read_text(encoding="utf-8-sig")
            relative = source.relative_to(root)
            if name != "AlphaExchange.Android" and re.search(r"^using\s+(?:Android|Java|AlphaExchange\.App)\b", text, re.M):
                errors.append(f"{relative}: Android belongs in the Android adapter")
            if name == "AlphaExchange.Core" and re.search(r"\bAlphaExchange\.Client\b|^using\s+System\.Net\b", text, re.M):
                errors.append(f"{relative}: Core must remain offline and independent of Client")
            if "Domain" in source.relative_to(folder).parts and re.search(r"\b(?:GameEngine|GameStore|SqliteConnection)\b", text):
                errors.append(f"{relative}: domain state cannot depend on execution/storage owners")
            # Plain literals/comments are excluded to avoid false positives in UI
            # text; this catches the actual synchronous Task calls prohibited here.
            if name == "AlphaExchange.Android" and re.search(r"GetAwaiter\(\)\.GetResult\(\)|\bTask\.Wait(?:All|Any)?\s*\(", text):
                errors.append(f"{relative}: await work without blocking Android UI")
    for project in (root / "tests").rglob("*.csproj"):
        xml = ET.parse(project)
        for element in xml.findall(".//Compile"):
            if "Include" in element.attrib:
                errors.append(f"{project.relative_to(root)}: tests cannot compile linked app source")
    if (root / ".git").exists():
        files = subprocess.check_output(["git", "ls-files", "--cached", "--others", "--exclude-standard"], cwd=root, text=True)
        for name in set(files.splitlines()):
            if Path(name).suffix.lower() in {".apk", ".aab", ".p12", ".jks", ".keystore"}:
                errors.append(f"{name}: private keys and build outputs cannot be source files")
            if re.search(r"(?:^|/)(?:bin|obj|dist)/", name):
                errors.append(f"{name}: generated output is tracked")
    return errors


if __name__ == "__main__":
    found = violations()
    if found:
        print("\n".join("FAIL: " + error for error in found), file=sys.stderr)
        sys.exit(1)
    print("PASS: Android → Client/Core, Client → Core, shared sources and repository outputs")
