#!/usr/bin/env python3
"""Static ownership, project graph, entry and archive checks; no game execution.

Does not build, inspect measurement reports, or establish Unity/native runtime
compatibility. Unsupported MSBuild indirection fails closed.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
CONTRACT = "SEONRO_V021_PRODUCT_BOUNDARY_V1"
EXPECTED = {"Core": set(), "Game": {"Core"}, "Hosting": {"Game"},
            "Shell": {"Hosting"}, "Android": {"Hosting"}, "Desktop": {"Hosting"},
            "GameTests": {"Game", "Hosting"}}
HOSTS = ("Desktop", "Android", "Shell")
NEUTRAL = ("Core", "Game")
LEGACY = "legacy/windows-pre-v021"
PLATFORM = re.compile(r"\b(?:UnityEngine|UnityEditor|MonoBehaviour|Godot|Unreal|Android|Java|System\.Windows|Microsoft\.Win32|Microsoft\.Web\.WebView2)\b|\b(?:DllImport|LibraryImport)\b")
OLD_NAMESPACES = re.compile(r"\bChronicles\.(?:Simulation|Queries|Runtime|Persistence|Presentation)\b|\bGameApplicationSession\b")
FILESYSTEM = re.compile(r"\b(?:System\.IO\.)?(?:File|Directory|Path)\s*\.|\b(?:FileStream|FileInfo|DirectoryInfo|StreamReader|StreamWriter)\b|\bEnvironment\.(?:GetFolderPath|SpecialFolder)\b|\bOperatingSystem\.")
WALL_CLOCK = re.compile(r"\bDateTime(?:Offset)?\.(?:Now|UtcNow)\b|\bEnvironment\.TickCount\b|\bGuid\.NewGuid\b|\bRandom\.Shared\b|\bnew\s+(?:System\.)?Random\s*\(|\b(?:Task\.(?:Run|Delay)|Thread\.)")


def require(value, message):
    if not value:
        raise ValueError(message)


def text(path):
    return (ROOT / path).read_text(encoding="utf-8")


def csharp_code(value):
    """Hide comments/literals while preserving line positions (not a C# parser)."""
    pattern = r'//[^\n]*|/\*[\s\S]*?\*/|\$?@"(?:""|[^"])*"|\$?"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\''
    return re.sub(pattern, lambda m: re.sub(r"[^\n]", " ", m[0]), value)


def source_files(directory):
    return sorted(p for p in (ROOT / directory).rglob("*.cs")
                  if not {"bin", "obj"}.intersection(p.relative_to(ROOT / directory).parts))


def refs_from_project(project):
    path = ROOT / project
    xml = ET.fromstring(path.read_text(encoding="utf-8"))
    # Every possible literal edge is checked, even one under an MSBuild condition.
    require(not xml.findall("Import"), "Explicit project imports need an evaluated-graph review: " + project)
    require(not xml.findall(".//Compile"), "Custom compile items need an ownership review: " + project)
    refs = []
    for item in xml.findall(".//ProjectReference"):
        include = item.get("Include", "")
        require(include and not any(c in include for c in "$*?;"), "Opaque project reference: " + project)
        target = (path.parent / include.replace("\\", "/")).resolve()
        require(target.is_relative_to(ROOT), "Project reference escapes repository: " + project)
        refs.append(target.relative_to(ROOT).as_posix())
    return xml, refs


def closure(graph, entry):
    result, pending = set(), [entry]
    while pending:
        node = pending.pop()
        if node not in result:
            result.add(node)
            pending.extend(graph[node])
    return sorted(result)


def locations(path, pattern):
    code = csharp_code(path.read_text(encoding="utf-8"))
    return [path.relative_to(ROOT).as_posix() + ":" + str(code.count("\n", 0, m.start()) + 1)
            for m in pattern.finditer(code)]


def validate():
    for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"):
        path = ROOT / name
        if path.is_file():
            xml = ET.fromstring(path.read_text(encoding="utf-8"))
            require(not any(xml.findall(".//" + item) for item in ("Import", "ProjectReference", "PackageReference", "Compile")),
                    "Shared build indirection needs an evaluated-graph review: " + name)
    manifest = json.loads(text("docs/architecture/repository_manifest.json"))
    require(manifest.get("contract") == CONTRACT and manifest.get("schema_version") == 2, "Wrong product boundary manifest")
    modules = {m["id"]: m for m in manifest["modules"]}
    require(len(modules) == len(manifest["modules"]) and set(modules) == set(EXPECTED), "Current module owner set changed")
    projects = {m["project"]: name for name, m in modules.items()}
    require(len(projects) == len(modules), "Duplicate project owners")
    solution = ET.fromstring(text("Chronicles.slnx"))
    require({p.get("Path") for p in solution.findall(".//Project")} == set(projects), "Solution includes an unowned, missing or archived project")
    actual = {p.relative_to(ROOT).as_posix() for base in ("src", "tests")
              for p in (ROOT / base).glob("*/*.csproj")}
    require(actual == set(projects), "Unowned current project remains outside the product solution")
    require(not any(p.startswith("legacy/") for p in projects), "Archive included in current product")
    graph, inventory, scans = {}, [], []
    for name, module in modules.items():
        project = module["project"]
        require(set(module["allowed_dependencies"]) == EXPECTED[name], "Manifest allows a forbidden edge: " + name)
        xml, refs = refs_from_project(project)
        require(all(ref in projects for ref in refs), "Unknown/archive project edge: " + name)
        graph[name] = {projects[ref] for ref in refs}
        require(graph[name] == EXPECTED[name], "Actual dependency edges differ from product policy: " + name)
        require(module["roots"] == [str(Path(project).parent).replace("\\", "/")], "Source owner does not match its project: " + name)
        tfms = [e.text for e in xml.findall(".//TargetFramework") + xml.findall(".//TargetFrameworks")]
        require(len(tfms) == 1 and tfms[0], "Missing/opaque target framework: " + name)
        packages = [p.get("Include") for p in xml.findall(".//PackageReference")]
        require(not packages or (name == "Desktop" and packages == ["Microsoft.Web.WebView2"]), "Unreviewed package dependency: " + name)
        files = source_files(module["roots"][0])
        inventory.append({"module": name, "project": project, "framework": tfms[0], "references": sorted(graph[name]),
                          "packages": packages, "manual_csharp_files": len(files)})
        for path in files:
            require(not locations(path, OLD_NAMESPACES), "Old authority namespace/type still compiled: " + str(path.relative_to(ROOT)))
            if name in NEUTRAL:
                for label, pattern in (("platform API", PLATFORM), ("filesystem API", FILESYSTEM), ("implicit time/RNG/worker API", WALL_CLOCK)):
                    found = locations(path, pattern)
                    require(not found, label + " in portable authority: " + ", ".join(found))
            elif name == "Hosting":
                require(not locations(path, PLATFORM), "Native UI API in neutral Hosting: " + str(path.relative_to(ROOT)))
            if name in HOSTS:
                found = locations(path, re.compile(r"\b(?:GameWorld|GameState|GameCodec|GameSaveStore)\b"))
                require(not found, "Native/UI host bypasses session: " + ", ".join(found))
        scans.append({"module": name, "source_owner_checked": True, "no_old_authority_namespace": True})
    require(graph["Core"] == set() and graph["Game"] == {"Core"}, "Portable game has a reverse host dependency")
    require(all(closure(graph, host) == sorted([host, "Hosting", "Game", "Core"]) for host in HOSTS), "Host closure includes a second engine")
    require(set(p.name for p in source_files("src/Chronicles.Core")) == {"ProductInfo.cs", "DeterministicRandom.cs"}, "Unused historical Core types remain current")
    require("GameSession" not in text("src/Chronicles.Game/GameCodec.cs"), "Byte codec depends on host session")
    require(not (ROOT / "src/Chronicles.Game/GameSession.cs").exists(), "File-backed session still belongs to portable authority")
    session = csharp_code(text("src/Chronicles.Hosting/GameSession.cs"))
    require(re.search(r"private\s+GameWorld\s+world\s*;", session) is not None, "Hosting must own one private world")
    require("UseCurrentLaws" in session and "CopyLawsForHost" in session, "Current-law new-world contract was dropped")
    require("GameSaveStore.LoadWithSource" in session and "GameSaveStore.SaveAtomic" in session, "Session bypasses file adapter")
    app = csharp_code(text("src/Chronicles.Desktop/App.xaml.cs"))
    require(re.search(r"new\s+EcologyWindow\s*\(", app) is not None and not re.search(r"new\s+MainWindow\s*\(", app), "Wrong WPF entry window")
    require("StartupUri" not in ET.fromstring(text("src/Chronicles.Desktop/App.xaml")).attrib, "WPF has a second XAML startup route")
    require("EcologyApplicationSession" in csharp_code(text("src/Chronicles.Desktop/EcologyWindow.cs")), "WPF facade missing")
    require("GameSession" in csharp_code(text("src/Chronicles.Hosting/EcologyApplicationSession.cs")), "Facade does not select shared session")
    android = csharp_code(text("src/Chronicles.Android/MainActivity.cs"))
    require("MainLauncher = true" in android and "new GameSession(" in android, "Android does not select shared session")
    require("new GameSession(" in csharp_code(text("src/Chronicles.Shell/Program.cs")), "Browser host does not select shared session")
    for name, entry in manifest["entrypoints"].items():
        require((ROOT / entry).is_file(), "Missing manifest entry: " + name)
    for document in ("AGENTS.md", "docs/architecture/README.md", "README.md"):
        require("v021-product-boundary.md" in text(document), "Current boundary link missing: " + document)
    archive = json.loads(text(LEGACY + "/source-provenance.json"))
    require(archive.get("archive_is_current_product") is False and archive.get("files"), "Archive provenance missing")
    for entry in archive["files"]:
        path = Path(entry["archive_path"])
        require(path.as_posix().startswith(LEGACY + "/") and ".." not in path.parts, "Archive path escapes its boundary")
        require(hashlib.sha256((ROOT / path).read_bytes()).hexdigest() == entry["sha256"], "Archived source changed: " + str(path))
        require(re.fullmatch(r"[0-9a-f]{40}", entry["source_commit"]) is not None, "Archive source commit missing")
    archive_projects = {p.get("Path") for p in ET.fromstring(text(LEGACY + "/Chronicles.slnx")).findall(".//Project")}
    for project in archive_projects:
        path = ROOT / LEGACY / project
        xml = ET.fromstring(path.read_text(encoding="utf-8"))
        for ref in xml.findall(".//ProjectReference"):
            target = (path.parent / ref.get("Include").replace("\\", "/")).resolve()
            require(target.is_relative_to(ROOT / LEGACY) and target.is_file(), "Archive project links current/missing code: " + project)
    workflow = text(".github/workflows/v021-product.yml")
    require("--project tests/Chronicles.Tests" not in workflow and "--project legacy/windows-pre-v021/tests/Chronicles.Tests" in workflow,
            "Optional historical CI suite points to current product")
    require("validate-v021-product-boundary.py" in workflow, "Product CI does not run static boundary check")
    return {"schema_version": 1, "kind": "seonro-v021-static-product-boundary", "passed": True,
            "source_commit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
            "working_tree_dirty": bool(subprocess.check_output(["git", "status", "--porcelain", "--untracked-files=no"], cwd=ROOT, text=True).strip()),
            "projects": inventory, "host_closures": {host: closure(graph, host) for host in HOSTS},
            "source_checks": scans, "archive_files_verified": len(archive["files"]),
            "execution_claims": {"game_functional_tests": False, "native_runtime": False, "unity": False, "performance": False},
            "limitations": ["Text/XML source guard, not a C# semantic or evaluated MSBuild graph proof.",
                            "SDK-generated code and transitive framework internals require build/runtime validation.",
                            "net10.0 and reflection-based serializer do not establish Unity/IL2CPP compatibility."]}


def self_test():
    sample = '/* File.ReadAllBytes(x); */ var s = "UnityEngine File.Open(x)";\nFile.ReadAllBytes(x);'
    code = csharp_code(sample)
    require(code.count("\n") == sample.count("\n"), "Source guard lost evidence line positions")
    require(len(FILESYSTEM.findall(code)) == 1 and not PLATFORM.search(code), "Source guard confused code and literal/comment")
    require(PLATFORM.search("using UnityEngine;") and WALL_CLOCK.search("DateTime.UtcNow") and
            OLD_NAMESPACES.search("using Chronicles.Simulation;"), "Forbidden-source negative guard failed")
    require(closure({"a": {"b"}, "b": {"a"}}, "a") == ["a", "b"], "Dependency closure guard failed")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report"); parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        self_test()
    report = validate()
    value = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
    if args.report:
        path = Path(args.report); path.parent.mkdir(parents=True, exist_ok=True); path.write_text(value, encoding="utf-8")
    print(value, end="")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, ET.ParseError) as error:
        print("Static product boundary failed: " + str(error), file=sys.stderr)
        sys.exit(1)
