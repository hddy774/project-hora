#!/usr/bin/env python3
"""Small game catalog and build router. Game runtime code stays game-specific."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SLUG = re.compile(r"[a-z][a-z0-9]*(?:-[a-z0-9]+)*")
VERSION = re.compile(r"[0-9]+\.[0-9]+\.[0-9]+")


def repo_file(root, value):
    if not isinstance(value, str) or "\\" in value or "\n" in value or "\r" in value:
        raise ValueError(f"Invalid repository file: {value!r}")
    path = Path(value)
    if path.is_absolute() or ".." in path.parts or not value or value.startswith("-"):
        raise ValueError(f"Invalid repository file: {value!r}")
    target = (root / path).resolve()
    if not target.is_relative_to(root.resolve()) or not target.is_file():
        raise ValueError(f"Missing or external repository file: {value}")
    return target


def read_games(root=ROOT):
    games = []
    identities, projects, prefixes, legacy, signing_prefixes = set(), set(), set(), set(), set()
    for manifest in sorted((root / "games").glob("*/game.json")):
        game = json.loads(manifest.read_text(encoding="utf-8"))
        slug = game.get("id", "")
        if game.get("schemaVersion") != 1 or not SLUG.fullmatch(slug) or slug != manifest.parent.name:
            raise ValueError(f"Invalid game identity/schema: {manifest}")
        if not isinstance(game.get("name"), str) or not game["name"].strip() or any(c in game["name"] for c in "\r\n"):
            raise ValueError(f"Invalid game name: {slug}")
        project = repo_file(root, game["androidProject"])
        repo_file(root, game["checksProject"])
        if game.get("artworkCheck"):
            repo_file(root, game["artworkCheck"])
        xml = ET.parse(project)
        def prop(name):
            values = {e.text for e in xml.findall(f".//{name}") if e.text}
            if len(values) != 1:
                raise ValueError(f"{slug}: {name} must have one literal value")
            return values.pop()
        app_id = prop("ApplicationId")
        if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)+", app_id):
            raise ValueError(f"Invalid application ID: {app_id}")
        if game["applicationId"] != app_id:
            raise ValueError(f"{slug}: registry applicationId disagrees with project")
        if "-android" not in prop("TargetFramework"):
            raise ValueError(f"{slug}: project must target Android")
        version, code = prop("ApplicationDisplayVersion"), prop("ApplicationVersion")
        if not VERSION.fullmatch(version) or not code.isdecimal() or not 0 < int(code) <= 2100000000:
            raise ValueError(f"{slug}: invalid Android version")
        prefix = game["apkPrefix"]
        if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]*", prefix):
            raise ValueError(f"Invalid APK prefix: {prefix}")
        for seen, value, name in [(identities, app_id, "application ID"), (projects, str(project), "project"), (prefixes, prefix, "APK prefix")]:
            if value in seen:
                raise ValueError(f"Duplicate {name}: {value}")
            seen.add(value)
        notes = game["releaseNotes"]
        if notes.count("{version}") != 1 or "{" in notes.replace("{version}", "") or "}" in notes.replace("{version}", ""):
            raise ValueError(f"{slug}: releaseNotes must contain only {{version}}")
        # Notes can be authored later; resolve-release requires the actual file.
        candidate = notes.format(version=version)
        if Path(candidate).is_absolute() or ".." in Path(candidate).parts or "\\" in candidate or any(c in candidate for c in "\r\n"):
            raise ValueError(f"{slug}: invalid release notes path")
        legacy_prefix = game.get("legacyTagPrefix")
        if legacy_prefix is not None:
            if legacy_prefix != "v" or slug != "alpha-exchange" or legacy_prefix in legacy:
                raise ValueError("Only ALPHA EXCHANGE may own historical unscoped v tags")
            legacy.add(legacy_prefix)
        signing = game.get("signing")
        if signing:
            repo_file(root, signing["script"])
            if not re.fullmatch(r"[A-Z][A-Z0-9_]*", signing["secretPrefix"]):
                raise ValueError(f"{slug}: invalid signing secret prefix")
            if signing["secretPrefix"] in signing_prefixes:
                raise ValueError(f"Duplicate signing secret prefix: {signing['secretPrefix']}")
            signing_prefixes.add(signing["secretPrefix"])
            if not isinstance(signing.get("certificateSha256"), str) or not re.fullmatch(r"[a-f0-9]{64}", signing["certificateSha256"]):
                raise ValueError(f"{slug}: a reviewed public signing certificate SHA-256 pin is required")
        games.append(dict(game, version=version, versionCode=code, output=f"dist/{slug}", tag=f"{slug}/v{version}", apkName=f"{prefix}-v{version}.apk"))
    if not games:
        raise ValueError("No games registered under games/*/game.json")
    return games


def select(games, slug):
    for game in games:
        if game["id"] == slug:
            return game
    raise ValueError(f"Unknown game {slug!r}; choose from: {', '.join(g['id'] for g in games)}")


def release_info(game, tag, root=ROOT):
    expected = game["tag"]
    historical = game.get("legacyTagPrefix", "") + game["version"]
    if tag != expected and not (game.get("legacyTagPrefix") and tag == historical):
        raise ValueError(f"Tag {tag!r} does not match {game['id']} version {game['version']} (expected {expected})")
    notes = game["releaseNotes"].format(version=game["version"])
    repo_file(root, notes)
    if not game.get("signing"):
        raise ValueError(f"{game['id']}: signing route not configured; builds/checks are available")
    return {"game": game["id"], "tag": tag, "version": game["version"], "version_code": game["versionCode"],
            "app_id": game["applicationId"], "project": game["androidProject"], "apk_name": game["apkName"],
            "artifact_prefix": tag.replace("/", "-"), "notes": notes, "signing_script": game["signing"]["script"], "signing_secret_prefix": game["signing"]["secretPrefix"], "signing_certificate": game["signing"]["certificateSha256"]}


def run(command):
    print("+ " + " ".join(command), flush=True)
    subprocess.run(command, cwd=ROOT, check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["validate", "list", "matrix", "info", "check", "build", "resolve-release"])
    parser.add_argument("game", nargs="?", help="game slug; required except for validate/list/matrix")
    parser.add_argument("--tag")
    parser.add_argument("--github-output", action="store_true", help="emit validated release fields as GitHub step outputs")
    args, extra = parser.parse_known_args()
    games = read_games()
    if args.command != "check" and extra:
        parser.error(f"unexpected arguments: {' '.join(extra)}")
    if args.command == "validate":
        print(f"PASS: {len(games)} game manifest(s); unique Android identities and build routes")
    elif args.command == "list":
        for game in games:
            print(f"{game['id']}\t{game['name']}\t{game['version']}\t{game['applicationId']}")
    elif args.command == "matrix":
        print(json.dumps({"game": [game["id"] for game in games]}, separators=(",", ":")))
    else:
        if not args.game:
            parser.error("game slug is required")
        game = select(games, args.game)
        if args.command == "info":
            print(json.dumps(game, ensure_ascii=False, indent=2))
        elif args.command == "resolve-release":
            if not args.tag:
                parser.error("--tag is required")
            info = release_info(game, args.tag)
            print("\n".join(f"{key}={value}" for key, value in info.items()) if args.github_output else json.dumps(info, ensure_ascii=False, indent=2))
        elif args.command == "check":
            run(["dotnet", "run", "--project", game["checksProject"], "-c", "Release", "--", *([x for x in extra if x != "--"])])
            if game.get("artworkCheck"):
                run([sys.executable, game["artworkCheck"]])
        elif args.command == "build":
            for name in ("ANDROID_HOME", "JAVA_HOME"):
                if not os.environ.get(name):
                    raise ValueError(f"Set {name} before building Android")
            run(["dotnet", "publish", game["androidProject"], "-c", "Release", f"-p:AndroidSdkDirectory={os.environ['ANDROID_HOME']}", f"-p:JavaSdkDirectory={os.environ['JAVA_HOME']}", "-o", game["output"]])


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, ET.ParseError, OSError, subprocess.CalledProcessError) as error:
        print(f"error: {error}", file=sys.stderr)
        sys.exit(1)
