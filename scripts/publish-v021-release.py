#!/usr/bin/env python3
"""Owner Actions-only APK-only draft, exact-byte readback, then stable publication."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

spec = importlib.util.spec_from_file_location("v021_release", Path(__file__).with_name("verify-v021-release.py"))
verify = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verify)
REQUIRED_JOBS = {"전체 기능 감사", "이식형 기능·브라우저 검증", "윈도우 실제 화면 검증(WebView2)", "서명 전 앱 패키지 빌드(APK)",
                 "기존 인증서 서명", "안드로이드 실제 실행(API 24)", "안드로이드 실제 실행(API 35)"}


def gh(*arguments, output=None):
    result = subprocess.run(["gh", *map(str, arguments)], stdout=output or subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    verify.require(result.returncode == 0, "GitHub release request failed: " + str(arguments[0]))
    return None if output else result.stdout.decode("utf-8")


def api(path, body=None):
    endpoint = "repos/" + verify.REPOSITORY + "/" + path
    if body is None:
        return json.loads(gh("api", endpoint))
    method, value = body
    with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", suffix=".json") as request:
        json.dump(value, request, ensure_ascii=False); request.flush()
        return json.loads(gh("api", "--method", method, endpoint, "--input", request.name))


def actual_source_run(source):
    request = verify.owner_context(); verify.check_source(source)
    run_id, attempt = os.environ["GITHUB_RUN_ID"], os.environ["GITHUB_RUN_ATTEMPT"]
    run = api("actions/runs/" + run_id)
    verify.actual_run_context(source, request, run)
    jobs = api("actions/runs/" + run_id + "/attempts/" + attempt + "/jobs?per_page=100")
    passed = {job["name"] for job in jobs["jobs"] if job["conclusion"] == "success"}
    verify.require(REQUIRED_JOBS <= passed, "Actual current-run feature/build/sign/platform jobs are incomplete")
    verify.require(api("git/ref/heads/main")["object"]["sha"] == source["source_commit"], "Main advanced after this APK was built")


def release(source, state_path):
    if state_path.is_file():
        saved = verify.read(state_path)
        verify.require(saved["tag"] == source["tag"], "Prepared release tag mismatch")
        return api("releases/" + str(saved["release_id"]))
    found = []
    page = 1
    while True:
        rows = api("releases?per_page=100&page=" + str(page))
        found.extend(row for row in rows if row["tag_name"] == source["tag"])
        if len(rows) < 100:
            break
        page += 1
    verify.require(len(found) <= 1, "Duplicate release tags")
    return found[0] if found else None


def check_release(record, source, apk, digest, asset_required, draft):
    verify.require(record["tag_name"] == source["tag"] and record["target_commitish"] == source["source_commit"] and
                   record["name"] == source["release_name"] and record["draft"] is draft and record["prerelease"] is False,
                   "Existing release differs in source/tag/status/name")
    assets = record["assets"]
    verify.require(len(assets) == 1 if asset_required else len(assets) <= 1, "Release must contain exactly one APK and no other assets")
    for item in assets:
        verify.require(item["name"] == source["apk_name"] and item["size"] == apk.stat().st_size and
                       item.get("digest") == "sha256:" + digest and item["state"] == "uploaded", "Release APK differs from the verified artifact")


def ensure_tag(source):
    response = subprocess.run(["gh", "api", "repos/" + verify.REPOSITORY + "/git/ref/tags/" + source["tag"]], capture_output=True, text=True)
    if response.returncode == 0:
        obj = json.loads(response.stdout)["object"]
        verify.require(obj["type"] == "commit" and obj["sha"] == source["source_commit"], "Existing tag is immutable and points elsewhere")
        return
    verify.require("HTTP 404" in response.stderr, "Tag lookup failed")
    created = api("git/refs", ("POST", {"ref": "refs/tags/" + source["tag"], "sha": source["source_commit"]}))
    verify.require(created["object"]["type"] == "commit" and created["object"]["sha"] == source["source_commit"], "Created tag source mismatch")


def notes(source, digest):
    text = (verify.ROOT / "docs/release-notes-v0.2.1.md").read_text(encoding="utf-8")
    text = "\n".join(line for line in text.splitlines() if line != "<!-- release-status: draft -->" and not line.startswith("> 미게시 초안"))
    url = "https://github.com/" + verify.REPOSITORY + "/actions/runs/" + source["run_id"]
    return text.rstrip() + ("\n\n<details>\n<summary>소스·서명·실행 검증 정보</summary>\n\n검증된 소스: `" + source["source_commit"] + "`\n\n"
        "제품 소스 지문(SHA-256): `" + source["product_source_fingerprint"] + "`\n\n"
        "APK SHA-256: `" + digest + "`\n\n배포 인증서 SHA-256: `" + verify.CERTIFICATE + "`\n\n"
        "앱 ID: `" + verify.PACKAGE + "` · 버전 코드: `" + str(source["version_code"]) + "`\n\n"
        "실제 API 24·35 설치/실행·화면, Windows WebView2, 기능·최종 성능 근거를 확인한 APK 한 개입니다.\n\n"
        "[빌드·서명·실제 플랫폼 검사·재다운로드 검증 기록](" + url + ")\n\n</details>\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("operation", choices=("prepare", "readback", "publish"))
    parser.add_argument("--source", required=True); parser.add_argument("--apk", required=True)
    parser.add_argument("--signed-proof", required=True); parser.add_argument("--state", required=True)
    args = parser.parse_args()
    source, proof = verify.read(args.source), verify.read(args.signed_proof)
    actual_source_run(source)
    ready = verify.feature_gate(source); verify.performance_gate(source, ready)
    verify.proof_matches(source, proof)
    apk, state_path = Path(args.apk), Path(args.state)
    digest = verify.sha(apk)
    verify.require(proof.get("kind") == "signed" and proof.get("certificate_sha256") == verify.CERTIFICATE and
                   proof["sha256"] == digest and apk.name == source["apk_name"], "Exact retained-signed artifact required")
    current = release(source, state_path)
    readback = state_path.parent / ("readback-" + source["apk_name"])
    if args.operation == "prepare":
        ensure_tag(source)
        body = notes(source, digest)
        if current is None:
            current = api("releases", ("POST", {"tag_name": source["tag"], "target_commitish": source["source_commit"],
                "name": source["release_name"], "draft": True, "prerelease": False, "body": body}))
        check_release(current, source, apk, digest, False, True)
        verify.write(state_path, {"tag": source["tag"], "release_id": current["id"], "source_commit": source["source_commit"], "apk_sha256": digest})
        api("releases/" + str(current["id"]), ("PATCH", {"body": body}))
        if not current["assets"]:
            gh("release", "upload", source["tag"], apk, "--repo", verify.REPOSITORY)
        check_release(release(source, state_path), source, apk, digest, True, True)
        print("Verified APK-only draft prepared; it is not public yet.")
    elif args.operation == "readback":
        verify.require(current is not None, "Prepared draft missing")
        check_release(current, source, apk, digest, True, True)
        readback.parent.mkdir(parents=True, exist_ok=True)
        with readback.open("wb") as destination:
            gh("api", "repos/" + verify.REPOSITORY + "/releases/assets/" + str(current["assets"][0]["id"]),
               "--header", "Accept: application/octet-stream", output=destination)
        verify.require(verify.sha(readback) == digest and readback.stat().st_size == apk.stat().st_size, "Draft readback bytes differ")
        print("Actual draft attachment read back and exact bytes verified.")
    else:
        verify.require(current is not None and readback.is_file() and verify.sha(readback) == digest, "Verified draft readback required before publication")
        check_release(current, source, apk, digest, True, True)
        api("releases/" + str(current["id"]), ("PATCH", {"draft": False, "prerelease": False, "make_latest": "true"}))
        latest = api("releases/latest"); check_release(latest, source, apk, digest, True, False)
        tag = api("git/ref/tags/" + source["tag"])["object"]
        verify.require(tag["type"] == "commit" and tag["sha"] == source["source_commit"], "Published tag source mismatch")
        final = state_path.parent / ("published-" + source["apk_name"])
        with final.open("wb") as destination:
            gh("api", "repos/" + verify.REPOSITORY + "/releases/assets/" + str(latest["assets"][0]["id"]),
               "--header", "Accept: application/octet-stream", output=destination)
        verify.require(verify.sha(final) == digest, "Published APK readback changed")
        verify.write(state_path.parent / "PUBLISHED-VERIFICATION.json", {"tag": source["tag"], "source_commit": source["source_commit"],
            "product_source_fingerprint": source["product_source_fingerprint"], "apk_sha256": digest,
            "certificate_sha256": verify.CERTIFICATE, "single_apk_asset": True, "public_readback_verified": True, "url": latest["html_url"]})
        print("Published and verified single-APK stable latest: " + latest["html_url"])


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError) as error:
        print("Release publication rejected: " + str(error), file=sys.stderr)
        sys.exit(1)
