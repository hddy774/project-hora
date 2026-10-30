#!/usr/bin/env python3
"""Fail-closed provenance/evidence/APK checks for the owner-run 0.2.1 release.

This verifier never signs, reads credentials, runs performance measurements, or
publishes. Evidence-only commits do not change the product source fingerprint.
"""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import re
import subprocess
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
REPOSITORY = "hddy774/project-hora"
PACKAGE = "org.hora.xianxiaobserver"
CERTIFICATE = "0832f24441f1caf335166fbc16ae0b9536e262ca48f258926a8027fc31dca25a"
WORKFLOW = ".github/workflows/v021-release.yml"
READINESS = "docs/evidence/0.2.1/release-readiness.json"
RELEASE_ONLY = {"scripts/verify-v021-release.py", "scripts/v021-release-emulator.py", "scripts/publish-v021-release.py"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def run(*arguments, binary=False, check=True):
    result = subprocess.run([str(a) for a in arguments], cwd=ROOT, capture_output=True, check=False)
    if check:
        require(result.returncode == 0, "Command failed: " + str(arguments[0]))
    if not check:
        return result
    return result.stdout if binary else result.stdout.decode("utf-8", errors="strict")


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def read(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, "Duplicate JSON key: " + key)
            result[key] = value
        return result
    return json.loads(Path(path).read_text(encoding="utf-8"), object_pairs_hook=unique)


def write(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def version():
    value = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    require(re.fullmatch(r"\d+\.\d+\.\d+", value) is not None, "VERSION must be semantic numeric version")
    require(value == "0.2.1", "This workflow is only for VERSION 0.2.1")
    major, minor, patch = map(int, value.split("."))
    return value, major * 1000000 + minor * 1000 + patch


def product_path(name):
    roots = {"VERSION", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json",
             "NuGet.config", "NuGet.Config", "Chronicles.slnx", "package.json", "package-lock.json", ".github/workflows/v021-product.yml"}
    return name in roots or name.startswith(("src/", "tests/")) or (name.startswith("scripts/") and name not in RELEASE_ONLY)


def fingerprint(commit="HEAD"):
    require(re.fullmatch(r"HEAD|[0-9a-f]{40}", commit) is not None, "Invalid fingerprint commit")
    paths = run("git", "ls-tree", "-rz", "--name-only", commit, binary=True).split(b"\0")
    digest = hashlib.sha256()
    selected = sorted(p.decode("utf-8") for p in paths if p and product_path(p.decode("utf-8")))
    require(selected and "VERSION" in selected, "Missing product sources")
    for name in selected:
        blob = run("git", "show", commit + ":" + name, binary=True)
        digest.update(name.encode("utf-8") + b"\0" + hashlib.sha256(blob).digest())
    return digest.hexdigest()


def owner_context():
    require(os.environ.get("GITHUB_ACTIONS") == "true", "Release operations run in GitHub Actions only")
    require(os.environ.get("GITHUB_REPOSITORY") == REPOSITORY and os.environ.get("GITHUB_EVENT_NAME") == "workflow_dispatch",
            "Owner manual workflow required")
    require(os.environ.get("GITHUB_REF") == "refs/heads/main", "Release must run from merged main")
    require(os.environ.get("GITHUB_ACTOR") == "hddy774" and os.environ.get("GITHUB_TRIGGERING_ACTOR") == "hddy774",
            "Repository owner must request and rerun release")
    require(run("git", "rev-parse", "HEAD").strip() == os.environ.get("GITHUB_SHA"), "Checkout is not the exact dispatched source")


def source_record():
    v, code = version()
    commit = run("git", "rev-parse", "HEAD").strip()
    require(re.fullmatch(r"[0-9a-f]{40}", commit) is not None, "Missing source commit")
    require(not run("git", "status", "--porcelain", "--untracked-files=no").strip(), "Tracked release checkout must be clean")
    return {"schema_version": 1, "repository": REPOSITORY, "source_commit": commit,
            "product_source_fingerprint": fingerprint(), "version": v, "version_code": code, "application_id": PACKAGE,
            "certificate_sha256": CERTIFICATE, "tag": "xianxia-observer/v" + v,
            "apk_name": "SeonroMansangrok-v" + v + ".apk", "release_name": "선로만상록 v" + v,
            "workflow": WORKFLOW, "run_id": os.environ.get("GITHUB_RUN_ID"), "run_attempt": os.environ.get("GITHUB_RUN_ATTEMPT")}


def check_source(record):
    current = source_record()
    for key in ("schema_version", "repository", "source_commit", "product_source_fingerprint", "version", "version_code",
                "application_id", "certificate_sha256", "tag", "apk_name", "workflow", "run_id", "run_attempt"):
        require(record.get(key) == current[key], "Source/run mismatch: " + key)
    return current


def evidence_file(path, expected):
    require(isinstance(path, str) and path.startswith("docs/evidence/0.2.1/") and ".." not in Path(path).parts,
            "Evidence path must be a checked-in 0.2.1 report")
    file = ROOT / path
    require(file.is_file() and re.fullmatch(r"[0-9a-f]{64}", expected or "") is not None and sha(file) == expected,
            "Evidence missing or checksum mismatch: " + str(path))
    return read(file)


def functional_report(report):
    require(isinstance(report, dict) and report.get("selection") == "all",
            "A selected subset is not a complete functional acceptance report")
    require(isinstance(report, dict) and type(report.get("total")) is int and report["total"] > 0 and
            type(report.get("failed")) is int and report["failed"] == 0,
            "Functional suite did not pass")
    require(isinstance(report.get("results"), list) and len(report["results"]) == report["total"] and
            all(r.get("passed") is True for r in report["results"]), "Every functional scenario needs a real passing result")


def browser_report(reports):
    require(isinstance(reports, list), "Browser report list missing")
    for width in (1280, 390):
        require(any(r.get("kind") == "real_session_functional" and r.get("width") == width and r.get("javascript_errors") == [] and
                    r.get("document_horizontal_overflow") is False and r.get("selected_known_path_command") is True and
                    r.get("save_load_scope_cleanup") is True and r.get("law_export_import_validation_and_apply") is True for r in reports),
                "Actual browser bridge/action/save checks missing for width " + str(width))


def feature_gate(source, readiness_path=READINESS):
    ready = read(ROOT / readiness_path)
    require(ready.get("schema_version") == 1 and ready.get("repository") == REPOSITORY and ready.get("version") == source["version"],
            "Readiness contract mismatch")
    require(ready.get("feature_functional_complete") is True, "Feature audit is not complete; signing is disabled")
    require(ready.get("product_source_fingerprint") == source["product_source_fingerprint"], "Feature evidence is for different product sources")
    audited = ready.get("audited_source_commit")
    require(re.fullmatch(r"[0-9a-f]{40}", audited or "") is not None, "Audited source commit missing")
    run("git", "merge-base", "--is-ancestor", audited, source["source_commit"])
    require(fingerprint(audited) == source["product_source_fingerprint"], "Audited commit has different product inputs")
    ledger = ROOT / "docs/plans/v021-tasks.json"
    require(sha(ledger) == ready.get("task_ledger_sha256"), "Task ledger differs from audited ledger")
    tasks = read(ledger)["tasks"]
    features = [t for t in tasks if t.get("kind") == "feature"]
    require(features and all(t.get("status") == "done" for t in features), "All feature acceptance tasks must be complete")
    # T06 is the release itself. Requiring it to be done here would make publication impossible.
    require("V021-T06" not in {t["id"] for t in features}, "Release task must remain outside the pre-release feature gate")
    functional_report(evidence_file(ready.get("functional_report"), ready.get("functional_report_sha256")))
    return ready


def performance_gate(source, ready):
    require(ready.get("final_performance_complete") is True and ready.get("publication_enabled") is True,
            "Final performance/publication readiness is false")
    report = evidence_file(ready.get("final_performance_report"), ready.get("final_performance_report_sha256"))
    require(report.get("schema_version") == 1 and report.get("kind") == "seonro-final-performance" and report.get("passed") is True,
            "Final performance audit did not pass")
    require(report.get("after_feature_functional_completion") is True and
            report.get("product_source_fingerprint") == source["product_source_fingerprint"] and
            report.get("functional_report_sha256") == ready["functional_report_sha256"], "Stale or pre-feature performance evidence")
    population = evidence_file(report.get("population_report"), report.get("population_report_sha256"))
    rows = population.get("reports", [])
    require({r.get("population") for r in rows} >= {2, 100, 1000, 10000}, "Final actual population measurements are incomplete")
    limits = report.get("limits", {})
    for name in ("monthlyP95Ms", "queryMs", "saveMs", "loadMs"):
        limit = limits.get(name)
        require(type(limit) in (int, float) and math.isfinite(limit) and limit > 0, "Missing explicit final performance limit: " + name)
        require(all(type(r.get(name)) in (int, float) and math.isfinite(r[name]) and 0 <= r[name] <= limit for r in rows),
                "Measured final performance exceeded limit: " + name)
    soak = evidence_file(report.get("soak_report"), report.get("soak_report_sha256"))
    require(soak.get("passed") is True and soak.get("population") == 10000 and soak.get("living") == 10000 and
            soak.get("targetMonths", 0) >= 7200 and soak.get("completedMonths", 0) >= soak["targetMonths"] and
            type(soak.get("elapsedSeconds")) in (int, float) and 0 < soak["elapsedSeconds"] <= 60 and
            soak.get("achievedSpeed", 0) >= 100, "Actual final 10,000-person 100x/60-second soak is incomplete")


def build_tools():
    sdk = os.environ.get("ANDROID_HOME") or os.environ.get("ANDROID_SDK_ROOT")
    require(bool(sdk), "Android SDK path missing")
    path = Path(sdk) / "build-tools/36.0.0"
    require((path / "aapt").is_file() and (path / "apksigner").is_file(), "Android build-tools 36.0.0 required")
    return path


def inspect_apk(apk, kind, source=None, signing=None):
    apk = Path(apk)
    require(apk.is_file() and 0 < apk.stat().st_size <= 256 * 1024 * 1024, "Missing or oversized APK")
    tools = build_tools()
    badging = run(tools / "aapt", "dump", "badging", apk)
    package = re.search(r"package: name='([^']+)' versionCode='(\d+)' versionName='([^']+)'", badging)
    require(package is not None, "APK identity could not be read")
    expected_version, expected_code = ("0.2.0", 2) if kind == "previous" else version()
    require(package.groups() == (PACKAGE, str(expected_code), expected_version), "APK package/version/code mismatch")
    require("sdkVersion:'24'" in badging and "application-debuggable" not in badging, "APK minSdk/debug policy mismatch")
    require("android.permission.INTERNET" not in run(tools / "aapt", "dump", "permissions", apk), "APK must be offline without INTERNET")
    manifest = run(tools / "aapt", "dump", "xmltree", apk, "AndroidManifest.xml")
    require("android:debuggable(0x0101000f)=(type 0x12)0xffffffff" not in manifest, "APK is debuggable")
    require("android:screenOrientation(0x0101001e)=(type 0x10)0x1" in manifest, "APK must retain portrait orientation")
    with zipfile.ZipFile(apk) as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)), "Duplicate APK ZIP entries")
        require({n.split("/")[1] for n in names if n.startswith("lib/") and n.endswith(".so")} == {"arm64-v8a", "x86_64"}, "APK ABI set mismatch")
    signature = run(tools / "apksigner", "verify", "--verbose", "--print-certs", apk, check=False)
    certificate = None
    if kind == "unsigned":
        require(signature.returncode != 0, "Signing input must be exactly the unsigned APK")
    else:
        require(signature.returncode == 0, "APK signature does not verify")
        verification = signature.stdout.decode("utf-8")
        certificates = re.findall(r"(?m)^Signer #1 certificate SHA-256 digest:\s*([a-fA-F0-9]{64})\s*$", verification)
        require([c.lower() for c in certificates] == [CERTIFICATE] and
                re.findall(r"(?m)^Number of signers:\s*(\d+)\s*$", verification) == ["1"], "Retained publisher certificate mismatch")
        certificate = CERTIFICATE
    activity = re.search(r"launchable-activity: name='([^']+)'", badging)
    require(activity is not None, "Launchable activity missing")
    result = {"schema_version": 1, "kind": kind, "sha256": sha(apk), "bytes": apk.stat().st_size,
              "application_id": PACKAGE, "version": expected_version, "version_code": expected_code,
              "minimum_sdk": 24, "abis": ["arm64-v8a", "x86_64"], "offline": True, "debuggable": False,
              "portrait": True, "certificate_sha256": certificate, "launchable_activity": activity[1]}
    if source is not None:
        check_source(source)
        result.update({k: source[k] for k in ("source_commit", "product_source_fingerprint", "run_id", "run_attempt")})
    if signing is not None:
        require(signing.get("source_commit") == source["source_commit"] and signing.get("signed_sha256") == result["sha256"] and
                signing.get("certificate_sha256") == CERTIFICATE and signing.get("payload_matches_verified_unsigned") is True and
                signing.get("original_bundle_modified") is False and signing.get("key_created") is False and
                signing.get("key_exported_to_workspace") is False, "Retained signing provenance mismatch")
    return result


def proof_matches(source, proof):
    for name in ("source_commit", "product_source_fingerprint", "run_id", "run_attempt"):
        require(proof.get(name) == source[name], "Platform/artifact provenance mismatch: " + name)


def windows_proof(source, raw):
    proof = read(raw)
    require(proof.get("passed") is True and proof.get("actualRenderAndBridge") is True and proof.get("version") == source["version"] and
            proof.get("host") == "WPF WebView2", "Actual Windows WebView2 render/bridge check failed")
    diagnostic = proof.get("diagnostic")
    for _ in range(3):
        if not isinstance(diagnostic, str):
            break
        diagnostic = json.loads(diagnostic)
    require(isinstance(diagnostic, dict) and diagnostic.get("hasView") is True and diagnostic.get("error") is None and
            diagnostic.get("connection") == "연결됨" and diagnostic.get("heading") == "만상의 흐름",
            "Windows visible DOM/connection diagnostics did not verify")
    return {"kind": "windows-webview2", "passed": True, "actual_render_and_bridge": True, "raw_sha256": sha(raw),
            **{k: source[k] for k in ("source_commit", "product_source_fingerprint", "run_id", "run_attempt")}}


def platform_gate(source, signed, windows, android, require_upgrade=False):
    proof_matches(source, signed)
    require(signed.get("kind") == "signed" and signed.get("certificate_sha256") == CERTIFICATE, "Retained-signed APK proof missing")
    proof_matches(source, windows)
    require(windows.get("passed") is True and windows.get("actual_render_and_bridge") is True, "Windows actual render check missing")
    require(len(android) == 2 and {r.get("api") for r in android} == {24, 35}, "Both actual Android API 24 and 35 checks required")
    for report in android:
        proof_matches(source, report)
        require(report.get("signed_apk_sha256") == signed["sha256"] and report.get("passed") is True and
                report.get("fresh_install_render") is True, "Android checked a different APK or failed real render")
        if require_upgrade or report.get("upgrade_requested") is True:
            require(report.get("upgrade_requested") is True and report.get("retained_signature_upgrade") is True and report.get("legacy_save_preserved") is True,
                    "Requested actual retained-signature/data upgrade failed")


def self_test():
    # Validator fixtures are not platform/performance evidence and never enable a real readiness record.
    failures = 0
    for action in (lambda: functional_report({"selection": "all", "total": 1, "failed": 1, "results": [{"passed": True}]}),
                   lambda: functional_report({"selection": "all", "total": 1, "failed": 0, "results": []}),
                   lambda: functional_report({"selection": "currency", "total": 1, "failed": 0, "results": [{"passed": True}]}),
                   lambda: browser_report([]), lambda: platform_gate({}, {}, {}, [])):
        try:
            action()
        except (ValueError, KeyError):
            failures += 1
    require(failures == 5, "Release negative guard self-test failed")
    functional_report({"selection": "all", "total": 1, "failed": 0, "results": [{"passed": True}]})
    ready = read(ROOT / READINESS)
    if ready.get("feature_functional_complete") is not True:
        try:
            feature_gate({"version": version()[0], "product_source_fingerprint": fingerprint()})
        except ValueError as error:
            require("signing is disabled" in str(error), "Initial readiness was rejected for an unexpected reason")
        else:
            raise ValueError("Initial false readiness must prevent signing")
    require(not product_path(READINESS) and not product_path("docs/evidence/0.2.1/final-performance.json") and
            product_path("src/Chronicles.Game/World.cs") and product_path("VERSION"), "Product fingerprint scope is wrong")
    print(json.dumps({"release_guard_self_tests": "passed", "actual_platform_or_performance_claim": False}))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    p = sub.add_parser("source"); p.add_argument("--output", required=True); p.add_argument("--github-output")
    p = sub.add_parser("fingerprint"); p.add_argument("--commit", default="HEAD")
    p = sub.add_parser("gate"); p.add_argument("--stage", choices=("features", "signing", "publish"), required=True)
    p.add_argument("--source", required=True); p.add_argument("--readiness", default=READINESS)
    for field in ("functional", "browser", "signed", "windows"):
        p.add_argument("--" + field)
    p.add_argument("--android", action="append", default=[])
    p.add_argument("--require-upgrade", action="store_true")
    p = sub.add_parser("apk"); p.add_argument("--kind", choices=("unsigned", "signed", "previous"), required=True)
    p.add_argument("--apk", required=True); p.add_argument("--source"); p.add_argument("--signing-proof"); p.add_argument("--output", required=True)
    p.add_argument("--github-output")
    p = sub.add_parser("windows"); p.add_argument("--source", required=True); p.add_argument("--raw", required=True); p.add_argument("--output", required=True)
    sub.add_parser("self-test")
    args = parser.parse_args()
    if args.command == "self-test":
        self_test(); return
    if args.command == "fingerprint":
        print(fingerprint(args.commit)); return
    if args.command == "source":
        owner_context(); record = source_record(); write(args.output, record)
        if args.github_output:
            with Path(args.github_output).open("a", encoding="utf-8") as stream:
                for key in ("source_commit", "product_source_fingerprint", "version", "version_code", "apk_name", "tag"):
                    stream.write(key + "=" + str(record[key]) + "\n")
        return
    if args.command == "apk":
        record = inspect_apk(args.apk, args.kind, read(args.source) if args.source else None,
                             read(args.signing_proof) if args.signing_proof else None)
        write(args.output, record)
        if args.github_output:
            with Path(args.github_output).open("a", encoding="utf-8") as stream:
                stream.write("sha256=" + record["sha256"] + "\n")
        return
    source = check_source(read(args.source))
    if args.command == "windows":
        write(args.output, windows_proof(source, args.raw)); return
    owner_context(); ready = feature_gate(source, args.readiness)
    if args.stage in ("signing", "publish"):
        require(args.functional and args.browser, "Actual current-run functional/browser reports required")
        functional_report(read(args.functional)); browser_report(read(args.browser))
    if args.stage == "publish":
        performance_gate(source, ready)
        require(args.signed and args.windows, "Signed APK and Windows proofs missing")
        platform_gate(source, read(args.signed), read(args.windows), [read(p) for p in args.android], args.require_upgrade)
    print(json.dumps({"gate": args.stage, "passed": True, "source_commit": source["source_commit"],
                      "product_source_fingerprint": source["product_source_fingerprint"]}))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, json.JSONDecodeError, zipfile.BadZipFile) as error:
        print("Release verification rejected: " + str(error), file=sys.stderr)
        sys.exit(1)
