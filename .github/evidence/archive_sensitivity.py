#!/usr/bin/env python3
"""소유자가 명시한 라벨 요청으로 고정된 원형6ZIP만 별도 prerelease에 영속 보관한다."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import urllib.parse

from http_client import API_ROOT, ArchiveError, GitHubClient, REPOSITORY, ensure, https_url

OWNER = "hddy774"
LABEL = "evidence-archive:v021-sensitivity-20261031"
WORKFLOW = ".github/workflows/v021-evidence-archive.yml"
ROOT = Path(__file__).resolve().parents[2]
MANIFEST = Path(__file__).with_name("sensitivity-20261031") / "manifest.json"
MANIFEST_SHA256 = "1d52331871c2683f167249a7925d5fb9a3556aa62a9f990e612fc84f392d0252"
TAG = "evidence/v0.2.1-20261031"
INDEX = "sensitivity-artifact-index-20261031.json"
TARGET = "7ce66943b539420c9dc1e3fa8a611daae58020ff"


def encoded(value):
    return urllib.parse.quote(str(value), safe="")


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode()


def load_manifest(path=MANIFEST):
    raw = Path(path).read_bytes()
    ensure(hashlib.sha256(raw).hexdigest() == MANIFEST_SHA256, "고정 manifest 해시가 다릅니다")
    manifest = json.loads(raw)
    archive = manifest["archive"]
    ensure(manifest["repository"] == REPOSITORY and archive["tag"] == TAG and archive["index_name"] == INDEX,
           "근거 전용 목적지가 아닙니다")
    ensure(archive["target_commit"] == TARGET and archive["prerelease"] is True and archive["make_latest"] is False,
           "근거 보관 정책이 다릅니다")
    items = manifest["artifacts"]
    ensure(len(items) == 6 and len({a["id"] for a in items}) == 6, "원형6개 ID가 필요합니다")
    ensure(len({a["planned_asset_name"] for a in items}) == 6, "자산 이름이 중복됐습니다")
    for item in items:
        ensure(type(item["id"]) is int and item["id"] > 0 and type(item["bytes"]) is int and item["bytes"] > 0,
               "원형 ID/바이트가 잘못됐습니다")
        ensure(re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]*\.zip", item["planned_asset_name"]), "고정 ZIP 이름이 아닙니다")
        ensure(re.fullmatch(r"[0-9a-f]{64}", item["sha256"]), "원형 SHA256이 잘못됐습니다")
    snapshot = manifest["run_metadata_snapshot"]
    source = Path(path).with_name(snapshot["file"]).read_bytes()
    ensure(len(source) == snapshot["bytes"] and hashlib.sha256(source).hexdigest() == snapshot["sha256"],
           "원래 run 메타데이터가 달라졌습니다")
    return manifest, source.decode("utf-8")


def request_context(environment, event):
    ensure(environment.get("GITHUB_ACTIONS") == "true", "실제 Actions 요청만 허용합니다")
    ensure(environment.get("GITHUB_EVENT_NAME") == "pull_request" and event.get("action") == "labeled",
           "실제 pull_request labeled 이벤트가 아닙니다")
    ensure(environment.get("GITHUB_REPOSITORY") == REPOSITORY and event["repository"]["full_name"] == REPOSITORY,
           "저장소가 다릅니다")
    for identity in (environment.get("GITHUB_ACTOR"), environment.get("GITHUB_TRIGGERING_ACTOR"),
                     event.get("sender", {}).get("login"), event["repository"]["owner"]["login"]):
        ensure(identity == OWNER, "소유자 요청/재실행만 허용합니다")
    ensure(event.get("label", {}).get("name") == LABEL, "정확한 전용 보관 라벨이 아닙니다")
    pull = event["pull_request"]
    ensure(pull["head"]["repo"]["full_name"] == REPOSITORY and pull["base"]["repo"]["full_name"] == REPOSITORY,
           "같은 저장소의 PR만 허용합니다")
    ensure(re.fullmatch(r"[0-9a-f]{40}", pull["head"]["sha"]), "PR source SHA가 잘못됐습니다")
    run_id = int(environment["GITHUB_RUN_ID"])
    attempt = int(environment["GITHUB_RUN_ATTEMPT"])
    ensure(type(pull["number"]) is int and pull["number"] > 0 and event["number"] == pull["number"] and
           run_id > 0 and attempt > 0, "PR/run/attempt ID가 잘못됐습니다")
    return {"event": "pull_request", "action": "labeled", "label": LABEL, "actor": OWNER,
            "repository": REPOSITORY, "pr": pull["number"], "head_sha": pull["head"]["sha"],
            "run_id": run_id, "attempt": attempt}


def attest_request(client, request, checkout):
    ensure(checkout == request["head_sha"], "실제 checkout과 라벨 요청 source가 다릅니다")
    live = client.api("pulls/" + str(request["pr"]))
    ensure(live["head"]["sha"] == checkout and live["head"]["repo"]["full_name"] == REPOSITORY and
           live["base"]["repo"]["full_name"] == REPOSITORY, "PR source/저장소가 바뀌었습니다")
    ensure(any(label["name"] == LABEL for label in live["labels"]), "명시된 보관 라벨이 없어졌습니다")
    run = client.api("actions/runs/" + str(request["run_id"]))
    ensure(run["id"] == request["run_id"] and run["run_attempt"] == request["attempt"] and
           run["event"] == "pull_request" and run["head_sha"] == checkout and run["path"] == WORKFLOW and
           run["status"] == "in_progress",
           "실제 run/event/attempt/source가 다릅니다")
    ensure(any(pull["number"] == request["pr"] for pull in run["pull_requests"]), "실제 run의 PR이 다릅니다")
    ensure(run["repository"]["full_name"] == REPOSITORY and run["head_repository"]["full_name"] == REPOSITORY,
           "실제 run 저장소가 다릅니다")
    ensure(run["actor"]["login"] == OWNER and run["triggering_actor"]["login"] == OWNER,
           "실제 run 소유자/재실행자가 다릅니다")


class EvidenceArchive:
    def __init__(self, client, manifest, snapshot, request, output, guard=None):
        self.client, self.manifest, self.snapshot, self.request = client, manifest, snapshot, request
        self.output = Path(output)
        self.output.mkdir(parents=True, exist_ok=True)
        self.release, self.phase, self.status = None, "request", {}
        self.release_id = None
        self.guard = guard or (lambda: attest_request(client, request, request["head_sha"]))

    def record(self, state, **values):
        self.status = {**self.status, "state": state, "phase": self.phase, "request": self.request, **values}
        (self.output / "status.json").write_bytes(json_bytes(self.status))

    def release_check(self, release):
        ensure(type(release["id"]) is int and release["id"] > 0 and type(release["draft"]) is bool and
               release["tag_name"] == TAG and release["prerelease"] is True and
               release["target_commitish"] == TARGET, "근거 전용 release/tag/source가 아닙니다")
        ensure(self.release_id is None or release["id"] == self.release_id, "확정한 근거 release ID가 바뀌었습니다")
        address = https_url(release["html_url"])
        path, prefix = urllib.parse.unquote(address.path), "/" + REPOSITORY + "/releases/"
        draft_url = release["draft"] and path.startswith(prefix) and len(path) > len(prefix) and "\\" not in path
        draft_url = draft_url and not any(part in (".", "..") for part in path.split("/"))
        ensure(address.hostname == "github.com" and not address.query and not address.fragment and
               (draft_url or path == prefix + "tag/" + TAG),
               "근거 release 주소가 다릅니다")
        names = [a["name"] for a in release["assets"]]
        allowed = {a["planned_asset_name"] for a in self.manifest["artifacts"]} | {INDEX}
        ensure(len(names) == len(set(names)) and set(names) <= allowed, "기존 자산 이름이 중복되거나 허용 범위 밖입니다")

    def tag_check(self):
        ref = self.client.api("git/ref/tags/" + encoded(TAG), missing=True)
        if ref is not None:
            obj = ref["object"]
            for _ in range(8):
                if obj["type"] != "tag":
                    break
                obj = self.client.api("git/tags/" + obj["sha"])["object"]
            ensure(obj["type"] == "commit" and obj["sha"] == TARGET, "기존 보관 tag가 다른 source입니다")

    def metadata_check(self):
        source = self.manifest["source_run"]
        run = self.client.api("actions/runs/" + str(source["id"]))
        for actual, key in (("id", "id"), ("run_attempt", "attempt"), ("event", "event"),
                            ("status", "status"), ("conclusion", "conclusion"), ("head_sha", "head_sha"),
                            ("path", "workflow_path")):
            ensure(run[actual] == source[key], "원래 run 메타데이터가 고정값과 다릅니다: " + actual)
        ensure(run["repository"]["full_name"] == REPOSITORY and
               run["head_repository"]["full_name"] == REPOSITORY, "원래 run의 저장소가 다릅니다")
        for item in self.manifest["artifacts"]:
            metadata = self.client.api("actions/artifacts/" + str(item["id"]))
            ensure(metadata["id"] == item["id"] and metadata["name"] == item["name"] and
                   metadata["size_in_bytes"] == item["bytes"] and not metadata["expired"], "원형 artifact 메타데이터 불일치")
            ensure(metadata["workflow_run"]["id"] == source["id"] and
                   metadata["workflow_run"]["head_sha"] == source["head_sha"], "artifact의 원래 run/source 불일치")
            ensure(metadata.get("digest") == "sha256:" + item["sha256"], "서버 artifact SHA256 불일치")

    def verify_file(self, path, item):
        ensure(Path(path).stat().st_size == item["bytes"] and sha(path) == item["sha256"], "원형 바이트/SHA256 불일치")

    def find_release(self):
        candidate, page, seen = None, 1, set()
        while True:
            releases = self.client.api("releases?per_page=100&page=" + str(page))
            ensure(type(releases) is list and len(releases) <= 100, "release 목록 형식/페이지 크기가 다릅니다")
            for release in releases:
                identifier = release["id"]
                ensure(type(identifier) is int and identifier > 0 and identifier not in seen,
                       "release 목록 ID/페이지가 중복되거나 잘못됐습니다")
                seen.add(identifier)
                if release["tag_name"] == TAG:
                    ensure(candidate is None, "고정 tag에 여러 draft/published release가 있어 모호합니다")
                    candidate = release
            if len(releases) < 100:
                return candidate
            page += 1

    def get_release(self):
        if self.release_id is None:
            candidate = self.find_release()
            if candidate is None:
                return None
            self.release_check(candidate)
            self.release_id = candidate["id"]
        candidate = self.client.api("releases/" + str(self.release_id))
        self.release_check(candidate)
        self.release = candidate
        return self.release

    def assets(self):
        return {asset["name"]: asset for asset in self.release["assets"]}

    def asset_check(self, asset, item):
        ensure(type(asset["id"]) is int and asset["id"] > 0 and
               asset["name"] == item["planned_asset_name"] and asset["size"] == item["bytes"] and
               asset["state"] == "uploaded", "보관 asset 이름/크기/상태가 다릅니다")
        if asset.get("digest") is not None:
            ensure(asset["digest"] == "sha256:" + item["sha256"], "보관 asset SHA256이 다릅니다")

    def download_asset(self, asset, item, public):
        self.asset_check(asset, item)
        path = self.output / ("public" if public else "draft") / item["planned_asset_name"]
        url = API_ROOT + "releases/assets/" + str(asset["id"])
        if public:
            url = asset["browser_download_url"]
            parsed = https_url(url)
            ensure(parsed.hostname == "github.com" and not parsed.query and not parsed.fragment and
                   urllib.parse.unquote(parsed.path) ==
                   "/" + REPOSITORY + "/releases/download/" + TAG + "/" + item["planned_asset_name"],
                   "공개 asset 주소가 근거 release와 다릅니다")
        self.client.download(url, path, item["bytes"], public=public)
        self.verify_file(path, item)
        return path

    def prepare_originals(self):
        self.phase = "original-download"
        self.record("보관 중")
        self.metadata_check()
        for item in self.manifest["artifacts"]:
            path = self.output / "original" / item["planned_asset_name"]
            self.client.download(API_ROOT + "actions/artifacts/" + str(item["id"]) + "/zip", path, item["bytes"],
                                 media="application/json")
            self.verify_file(path, item)

    def body(self, state):
        return ("## 민감도 원형 근거 보관\n\n상태: " + state + "\n\n"
                "원래 실행은 failure이며4완주·2CLI-timeout partial입니다. 원문/complete=false 값을 보존합니다.\n"
                "제품 v0.2.1 최신 릴리스/APK와 별개인 prerelease이며 합격/보정 효과로 재라벨하지 않습니다.\n\n"
                "고정 manifest SHA256: `" + MANIFEST_SHA256 + "`\n\n"
                "현재 보관 요청: https://github.com/" + REPOSITORY + "/actions/runs/" + str(self.request["run_id"]))

    def prepare_draft(self):
        self.phase = "draft"
        self.record("보관 중")
        self.guard()
        if self.release is None:
            candidate = self.client.api("releases", "POST", {"tag_name": TAG, "target_commitish": TARGET,
                "name": self.manifest["archive"]["title"], "draft": True, "prerelease": True,
                "make_latest": "false", "body": self.body("보관 중")})
            self.release_check(candidate)
            self.release_id = candidate["id"]
            self.release = candidate
        self.release_check(self.release)
        self.tag_check()
        # 기존 이름의 충돌을 먼저 전부 확인한 뒤에만 빠진 새 자산을 올린다.
        for item in self.manifest["artifacts"]:
            asset = self.assets().get(item["planned_asset_name"])
            if asset is not None:
                self.download_asset(asset, item, public=False)
        for item in self.manifest["artifacts"]:
            asset = self.assets().get(item["planned_asset_name"])
            if asset is None:
                self.guard()
                self.client.upload(self.release["id"], item["planned_asset_name"],
                                   self.output / "original" / item["planned_asset_name"])
                self.get_release()
                asset = self.assets()[item["planned_asset_name"]]
            self.download_asset(asset, item, public=False)

    def publish_zip_archive(self):
        self.phase = "public-zip-readback"
        self.record("보관 중")
        self.guard()
        if self.release["draft"]:
            self.client.api("releases/" + str(self.release["id"]), "PATCH",
                            {"draft": False, "prerelease": True, "make_latest": "false"})
        self.get_release()
        ensure(self.release["draft"] is False, "근거 prerelease가 공개되지 않았습니다")
        for item in self.manifest["artifacts"]:
            self.download_asset(self.assets()[item["planned_asset_name"]], item, public=True)
        latest = self.client.api("releases/latest", missing=True)
        ensure(latest is None or latest["id"] != self.release["id"], "근거가 제품 latest가 되었습니다")

    def index_core(self):
        return {"schema_version": 1, "state": "complete", "repository": REPOSITORY, "tag": TAG,
                "release_id": self.release["id"], "target_commit": TARGET, "prerelease": True, "make_latest": False,
                "release_metadata": {key: self.release.get(key) for key in ("created_at", "published_at", "author")},
                "manifest_sha256": MANIFEST_SHA256, "manifest": self.manifest,
                "original_run_metadata_utf8": self.snapshot,
                "zip_assets": [{"source_artifact_id": item["id"], "name": item["planned_asset_name"],
                    "id": self.assets()[item["planned_asset_name"]]["id"], "bytes": item["bytes"],
                    "sha256": item["sha256"], "url": self.assets()[item["planned_asset_name"]]["browser_download_url"],
                    "public_bytes_verified": True} for item in self.manifest["artifacts"]]}

    def final_index(self):
        self.phase = "final-index"
        self.record("보관 중")
        core = self.index_core()
        existing = self.assets().get(INDEX)
        trace = {"verified_at": datetime.now(timezone.utc).isoformat(), "execution": self.request}
        if existing:
            ensure(type(existing["size"]) is int and 0 < existing["size"] <= 131072 and
                   re.fullmatch(r"sha256:[0-9a-f]{64}", existing.get("digest") or ""),
                   "기존 index 크기/서버 해시가 잘못됐습니다")
            item = {"planned_asset_name": INDEX, "bytes": existing["size"], "sha256": existing["digest"].removeprefix("sha256:")}
            raw = self.download_asset(existing, item, public=True).read_bytes()
            previous = json.loads(raw)
            trace = previous["verification"]
            ensure({k: v for k, v in previous.items() if k != "verification"} == core,
                   "같은 이름의 기존 index가 다른 원문을 가리킵니다")
            ensure(trace["execution"]["actor"] == OWNER and trace["execution"]["repository"] == REPOSITORY and
                   trace["execution"]["label"] == LABEL, "기존 index의 최초 요청이 보관 정책과 다릅니다")
            ensure(datetime.fromisoformat(trace["verified_at"]).tzinfo is not None, "기존 검증 시각이 잘못됐습니다")
            ensure(raw == json_bytes({**core, "verification": trace}), "기존 index 바이트가 정본 직렬화와 다릅니다")
        data = json_bytes({**core, "verification": trace})
        path = self.output / INDEX
        path.write_bytes(data)
        if existing is None:
            self.guard()
            self.client.upload(self.release["id"], INDEX, path)
            self.get_release()
        item = {"planned_asset_name": INDEX, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}
        self.download_asset(self.assets()[INDEX], item, public=True)
        self.guard()
        self.client.api("releases/" + str(self.release["id"]), "PATCH",
                        {"prerelease": True, "make_latest": "false", "body": self.body("완료")})
        self.get_release()
        latest = self.client.api("releases/latest", missing=True)
        ensure(not self.release["draft"] and (latest is None or latest["id"] != self.release["id"]),
               "최종 prerelease/latest 상태가 다릅니다")
        self.record("완료", release_url=self.release["html_url"], index_url=self.assets()[INDEX]["browser_download_url"],
                    index_asset_id=self.assets()[INDEX]["id"], index_bytes=len(data), index_sha256=item["sha256"])

    def execute(self):
        self.record("보관 중")
        try:
            self.guard()
            self.tag_check()
            self.get_release()
            zip_names = {a["planned_asset_name"] for a in self.manifest["artifacts"]}
            copied = self.release and zip_names <= set(self.assets())
            if not copied:
                ensure(self.release is None or self.release["draft"], "이미 공개된 보관에서 원형이 누락됐습니다")
                self.prepare_originals()
                self.prepare_draft()
            elif self.release["draft"]:
                # 검증된 보관 사본이 있으면 원래 Actions 만료 뒤에도 게시를 마무리할 수 있다.
                for item in self.manifest["artifacts"]:
                    self.download_asset(self.assets()[item["planned_asset_name"]], item, public=False)
            self.publish_zip_archive()
            self.final_index()
        except (ArchiveError, KeyError, ValueError, OSError, TypeError) as error:
            self.record("실패", error=str(error))
            # 미완료 보관만 실패로 표시한다. 기존 완료 index/자산은 수정하거나 지우지 않는다.
            if self.release and INDEX not in self.assets():
                try:
                    self.guard()
                    self.client.api("releases/" + str(self.release["id"]), "PATCH",
                                    {"body": self.body("실패: " + self.phase), "prerelease": True, "make_latest": "false"})
                except (ArchiveError, OSError):
                    self.record("실패", error=str(error), release_failure_note_unavailable=True)
            raise


def current_checkout():
    checkout = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    ensure(not subprocess.check_output(["git", "status", "--porcelain", "--untracked-files=no"], cwd=ROOT),
           "요청 source의 추적 파일이 바뀌었습니다")
    return checkout


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    modes = parser.add_mutually_exclusive_group(required=True)
    modes.add_argument("--self-test", action="store_true")
    modes.add_argument("--execute", action="store_true")
    parser.add_argument("--output")
    args = parser.parse_args()
    if args.self_test:
        return subprocess.call([sys.executable, str(Path(__file__).with_name("test_archive.py"))])
    ensure(args.output, "보관 상태 출력 경로가 필요합니다")
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    try:
        manifest, snapshot = load_manifest()
        event = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text())
        request = request_context(os.environ, event)
        client = GitHubClient(os.environ.get("GH_TOKEN"))
        guard = lambda: attest_request(client, request, current_checkout())
        guard()
        EvidenceArchive(client, manifest, snapshot, request, output, guard).execute()
    except (ArchiveError, KeyError, ValueError, OSError, TypeError) as error:
        if not (output / "status.json").exists():
            (output / "status.json").write_bytes(json_bytes({"state": "실패", "phase": "request-validation",
                                                            "error": str(error)}))
        raise
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ArchiveError, KeyError, ValueError, OSError, TypeError) as error:
        print("근거 보관 실패: " + str(error), file=sys.stderr)
        sys.exit(1)
