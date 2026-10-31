#!/usr/bin/env python3
"""가짜 GitHub/byte 자료로 실제 보관 흐름을 검사한다. 실제 네트워크·게임·게시 호출은 없다."""
import copy
from dataclasses import replace
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import urllib.parse

import archive_sensitivity as archive
from http_client import ArchiveError
from test_http_client import HttpClientTests


def event_fixture(batch=archive.ORIGINAL_BATCH):
    repository = {"full_name": archive.REPOSITORY, "owner": {"login": archive.OWNER}}
    event = {"action": "labeled", "number": 900, "repository": repository,
             "sender": {"login": archive.OWNER}, "label": {"name": batch.label},
             "pull_request": {"number": 900, "head": {"sha": "1" * 40, "repo": repository},
                              "base": {"repo": repository}}}
    environment = {"GITHUB_ACTIONS": "true", "GITHUB_EVENT_NAME": "pull_request",
                   "GITHUB_REPOSITORY": archive.REPOSITORY, "GITHUB_ACTOR": archive.OWNER,
                   "GITHUB_TRIGGERING_ACTOR": archive.OWNER, "GITHUB_RUN_ID": "999999",
                   "GITHUB_RUN_ATTEMPT": "1"}
    return environment, event


def dataset_fixture():
    manifest, _ = archive.load_manifest()
    originals = {}
    for item in manifest["artifacts"]:
        data = b"PK\x03\x04fixture-" + item["name"].encode()
        item["bytes"] = len(data)
        item["sha256"] = hashlib.sha256(data).hexdigest()
        originals[item["id"]] = data
    return manifest, originals


class FakeGitHub:
    def __init__(self, manifest, originals, request, batch=archive.ORIGINAL_BATCH):
        self.batch = batch
        self.manifest, self.originals, self.request = manifest, originals, request
        self.release, self.tag = None, None
        self.other_releases = []
        self.data, self.events = {}, []
        self.source_unavailable, self.corrupt_public = False, None
        self.official = {"id": 500, "tag_name": "v0.2.1", "assets": [{"name": "official.apk", "digest": "unchanged"}]}
        self.live = {"head": {"sha": request["head_sha"], "repo": {"full_name": archive.REPOSITORY}},
                     "base": {"repo": {"full_name": archive.REPOSITORY}}, "labels": [{"name": self.batch.label}]}
        self.run = {"id": request["run_id"], "run_attempt": request["attempt"], "event": "pull_request",
                    "status": "in_progress", "head_sha": request["head_sha"], "path": self.batch.workflow,
                    "repository": {"full_name": archive.REPOSITORY}, "head_repository": {"full_name": archive.REPOSITORY},
                    "actor": {"login": archive.OWNER}, "triggering_actor": {"login": archive.OWNER},
                    "pull_requests": [{"number": request["pr"]}]}

    def api(self, path, method="GET", value=None, missing=False):
        self.events.append((method, path, copy.deepcopy(value)))
        if method == "GET":
            return self.read(path)
        if method == "POST" and path == "releases":
            assert self.release is None, "같은 근거의 draft를 중복 생성하면 안 됩니다"
            self.release = {**value, "id": 77, "assets": [],
                            "html_url": "https://github.com/" + archive.REPOSITORY + "/releases/untagged-fixture-77"}
            return copy.deepcopy(self.release)
        if method == "PATCH" and path == "releases/77":
            assert value["prerelease"] is True and value["make_latest"] == "false"
            self.release.update(value)
            if not self.release["draft"]:
                self.tag = self.batch.target
                self.release["html_url"] = "https://github.com/" + archive.REPOSITORY + "/releases/tag/" + archive.encoded(self.batch.tag)
            return copy.deepcopy(self.release)
        raise AssertionError("허용하지 않은 변경: " + method + " " + path)

    def read(self, path):
        if path.startswith("pulls/"):
            return copy.deepcopy(self.live)
        if path == "actions/runs/" + str(self.request["run_id"]):
            return copy.deepcopy(self.run)
        if path.startswith("git/ref/tags/"):
            return {"object": {"type": "commit", "sha": self.tag}} if self.tag else None
        if path.startswith("releases/tags/"):
            return copy.deepcopy(self.release) if self.release and not self.release["draft"] else None
        if path == "releases/77":
            return copy.deepcopy(self.release)
        if path.startswith("releases?per_page=100&page="):
            page = int(path.rsplit("=", 1)[1])
            rows = self.other_releases + ([self.release] if self.release else [])
            return copy.deepcopy(rows[(page - 1) * 100:page * 100])
        if path == "releases/latest":
            return copy.deepcopy(self.official)
        assert not self.source_unavailable, "완료된 보관은 만료된 Actions 원형을 요구하면 안 됩니다"
        source = self.manifest["source_run"]
        if path == "actions/runs/" + str(source["id"]):
            return {"id": source["id"], "run_attempt": source["attempt"], "event": source["event"],
                    "status": source["status"], "conclusion": source["conclusion"], "head_sha": source["head_sha"],
                    "path": source["workflow_path"], "repository": {"full_name": archive.REPOSITORY},
                    "head_repository": {"full_name": archive.REPOSITORY}}
        if path.startswith("actions/artifacts/"):
            item = next(i for i in self.manifest["artifacts"] if path == "actions/artifacts/" + str(i["id"]))
            return {"id": item["id"], "name": item["name"], "size_in_bytes": item["bytes"], "expired": False,
                    "digest": "sha256:" + item["sha256"], "workflow_run": {"id": source["id"], "head_sha": source["head_sha"]}}
        raise AssertionError("예상하지 않은 읽기: " + path)

    def upload(self, release_id, name, path):
        assert release_id == 77 and not any(a["name"] == name for a in self.release["assets"])
        payload = Path(path).read_bytes()
        identifier = 100 + len(self.data)
        asset = {"id": identifier, "name": name, "size": len(payload), "state": "uploaded",
                 "digest": "sha256:" + hashlib.sha256(payload).hexdigest(),
                 "browser_download_url": "https://github.com/" + archive.REPOSITORY + "/releases/download/" +
                    archive.encoded(self.batch.tag) + "/" + archive.encoded(name)}
        self.release["assets"].append(asset)
        self.data[identifier] = payload
        self.events.append(("UPLOAD", name, None))
        return copy.deepcopy(asset)

    def download(self, url, destination, expected_bytes, public=False, media="application/octet-stream"):
        self.events.append(("PUBLIC" if public else "DOWNLOAD", url, None))
        if "/actions/artifacts/" in url:
            assert not self.source_unavailable
            payload = self.originals[int(url.split("/artifacts/", 1)[1].split("/", 1)[0])]
        else:
            if public:
                assert not self.release["draft"]
                name = urllib.parse.unquote(url.rsplit("/", 1)[1])
                asset = next(a for a in self.release["assets"] if a["name"] == name)
            else:
                identifier = int(url.rsplit("/", 1)[1])
                asset = next(a for a in self.release["assets"] if a["id"] == identifier)
            payload = self.data[asset["id"]]
            if public and asset["name"] == self.corrupt_public:
                payload = b"!" + payload[1:]
        assert len(payload) == expected_bytes
        path = Path(destination)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(payload)


class ArchiveTests(unittest.TestCase):
    def fixture(self):
        environment, event = event_fixture()
        request = archive.request_context(environment, event)
        manifest, originals = dataset_fixture()
        client = FakeGitHub(manifest, originals, request)
        return manifest, client, request

    def test_fixed_six_manifest_and_original_failed_run_snapshot(self):
        manifest, snapshot = archive.load_manifest()
        self.assertEqual(len(manifest["artifacts"]), 6)
        self.assertEqual(sum(a["bytes"] for a in manifest["artifacts"]), 9937563)
        self.assertEqual(json.loads(snapshot)["conclusion"], "failure")
        with tempfile.TemporaryDirectory() as directory:
            damaged = Path(directory) / "manifest.json"
            damaged.write_bytes(archive.MANIFEST.read_bytes() + b" ")
            with self.assertRaises(ArchiveError):
                archive.load_manifest(damaged)
            damaged.write_bytes(archive.MANIFEST.read_bytes())
            (Path(directory) / "run-metadata.json").write_bytes(snapshot.encode() + b" ")
            with self.assertRaises(ArchiveError):
                archive.load_manifest(damaged)

    def test_owner_event_label_repository_and_ids_fail_closed(self):
        changes = [("env", "GITHUB_ACTIONS", "false"), ("env", "GITHUB_EVENT_NAME", "workflow_dispatch"),
                   ("env", "GITHUB_REPOSITORY", "other/repo"), ("env", "GITHUB_ACTOR", "other"),
                   ("env", "GITHUB_TRIGGERING_ACTOR", "other"), ("env", "GITHUB_RUN_ID", "0"),
                   ("env", "GITHUB_RUN_ATTEMPT", "0"), ("event", "action", "opened")]
        for kind, key, value in changes:
            environment, event = event_fixture()
            (environment if kind == "env" else event)[key] = value
            with self.subTest(key=key), self.assertRaises(ArchiveError):
                archive.request_context(environment, event)
        for field in ["label", "sender", "head-repo", "base-repo", "head-sha", "number"]:
            environment, event = event_fixture()
            if field == "label":
                event["label"]["name"] += "-other"
            elif field == "sender":
                event["sender"]["login"] = "other"
            elif field.endswith("-repo"):
                event["pull_request"][field.split("-", 1)[0]]["repo"] = {"full_name": "fork/repo"}
            elif field == "head-sha":
                event["pull_request"]["head"]["sha"] = "wrong"
            else:
                event["number"] += 1
            with self.subTest(field=field), self.assertRaises(ArchiveError):
                archive.request_context(environment, event)

    def test_actual_run_and_live_pr_must_match_without_writes(self):
        for key, value in [("run_attempt", 2), ("event", "workflow_dispatch"), ("status", "completed"),
                           ("head_sha", "2" * 40), ("path", ".github/workflows/other.yml")]:
            _, client, request = self.fixture()
            client.run[key] = value
            with self.subTest(key=key), self.assertRaises(ArchiveError):
                archive.attest_request(client, request, request["head_sha"])
            self.assertTrue(all(e[0] == "GET" for e in client.events))
        _, client, request = self.fixture()
        client.live["head"]["sha"] = "3" * 40
        with self.assertRaises(ArchiveError):
            archive.attest_request(client, request, request["head_sha"])

    def test_actual_actor_pr_and_removed_label_fail_before_writes(self):
        for field in ["actor", "triggering_actor", "pull_requests", "head_repository", "label"]:
            _, client, request = self.fixture()
            if field in ("actor", "triggering_actor"):
                client.run[field]["login"] = "other"
            elif field == "pull_requests":
                client.run[field] = [{"number": 901}]
            elif field == "head_repository":
                client.run[field]["full_name"] = "fork/repo"
            else:
                client.live["labels"] = []
            with self.subTest(field=field), self.assertRaises(ArchiveError):
                archive.attest_request(client, request, request["head_sha"])
            self.assertTrue(all(e[0] == "GET" for e in client.events))

    def test_complete_flow_keeps_failed_results_and_latest_product_untouched(self):
        manifest, client, request = self.fixture()
        before = copy.deepcopy(client.official)
        with tempfile.TemporaryDirectory() as directory:
            archive.attest_request(client, request, request["head_sha"])
            run = archive.EvidenceArchive(client, manifest, "원래 run JSON", request, directory)
            run.execute()
            self.assertEqual(run.status["state"], "완료")
            self.assertEqual(len(client.release["assets"]), 7)
            index = json.loads((Path(directory) / archive.INDEX).read_text())
            self.assertEqual(index["manifest"]["source_run"]["conclusion"], "failure")
            self.assertTrue(index["manifest"]["original_result"]["reports_complete_fields_remain_false"])
            index_upload = next(i for i, e in enumerate(client.events) if e[:2] == ("UPLOAD", archive.INDEX))
            prior_public = [e for e in client.events[:index_upload] if e[0] == "PUBLIC"]
            self.assertEqual(len(prior_public), 6)
            self.assertFalse(client.release["draft"])
            self.assertTrue(client.release["prerelease"])
            self.assertEqual(client.official, before)

    def test_byte_corruption_stops_before_release_creation(self):
        manifest, client, request = self.fixture()
        item = manifest["artifacts"][0]
        client.originals[item["id"]] = b"!" + client.originals[item["id"]][1:]
        with tempfile.TemporaryDirectory() as directory:
            run = archive.EvidenceArchive(client, manifest, "snapshot", request, directory)
            with self.assertRaises(ArchiveError):
                run.execute()
            self.assertEqual(run.status["state"], "실패")
            self.assertFalse(any(e[0] in ("POST", "PATCH", "UPLOAD") for e in client.events))

    def test_source_change_after_draft_upload_blocks_publish_and_failure_patch(self):
        manifest, client, request = self.fixture()
        def guard():
            if client.release and len(client.release["assets"]) == 6:
                client.live["head"]["sha"] = "4" * 40
            archive.attest_request(client, request, request["head_sha"])
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(ArchiveError):
                archive.EvidenceArchive(client, manifest, "snapshot", request, directory, guard).execute()
            self.assertTrue(client.release["draft"])
            self.assertFalse(any(e[0] == "PATCH" for e in client.events))

    def test_wrong_original_metadata_stops_before_release_creation(self):
        for field in ["size", "digest", "source", "run-conclusion"]:
            manifest, client, request = self.fixture()
            original = client.read
            def mismatched(path):
                value = original(path)
                if path.startswith("actions/artifacts/"):
                    if field == "size":
                        value["size_in_bytes"] += 1
                    elif field == "digest":
                        value["digest"] = "sha256:" + "0" * 64
                    elif field == "source":
                        value["workflow_run"]["head_sha"] = "0" * 40
                if field == "run-conclusion" and path == "actions/runs/41131479924":
                    value["conclusion"] = "success"
                return value
            client.read = mismatched
            with self.subTest(field=field), tempfile.TemporaryDirectory() as directory:
                with self.assertRaises(ArchiveError):
                    archive.EvidenceArchive(client, manifest, "snapshot", request, directory).execute()
                self.assertTrue(all(e[0] == "GET" for e in client.events))

    def test_public_corruption_has_no_complete_index_and_can_resume(self):
        manifest, client, request = self.fixture()
        client.corrupt_public = manifest["artifacts"][0]["planned_asset_name"]
        with tempfile.TemporaryDirectory() as directory:
            run = archive.EvidenceArchive(client, manifest, "snapshot", request, directory)
            with self.assertRaises(ArchiveError):
                run.execute()
            self.assertFalse(any(a["name"] == archive.INDEX for a in client.release["assets"]))
            client.corrupt_public = None
            client.source_unavailable = True
            run = archive.EvidenceArchive(client, manifest, "snapshot", request, directory)
            run.execute()
            self.assertEqual(run.status["state"], "완료")

    def test_identical_completed_copy_is_reused_after_actions_expiry(self):
        manifest, client, request = self.fixture()
        with tempfile.TemporaryDirectory() as first, tempfile.TemporaryDirectory() as second:
            archive.EvidenceArchive(client, manifest, "snapshot", request, first).execute()
            original = copy.deepcopy(client.data)
            client.source_unavailable = True
            request = {**request, "attempt": 2}
            client.run["run_attempt"] = 2
            archive.attest_request(client, request, request["head_sha"])
            run = archive.EvidenceArchive(client, manifest, "snapshot", request, second)
            run.execute()
            self.assertEqual(client.data, original)
            self.assertEqual(len([e for e in client.events if e[0] == "UPLOAD"]), 7)

    def test_wrong_existing_tag_or_asset_cannot_be_overwritten(self):
        manifest, client, request = self.fixture()
        client.tag = "f" * 40
        with tempfile.TemporaryDirectory() as directory, self.assertRaises(ArchiveError):
            archive.EvidenceArchive(client, manifest, "snapshot", request, directory).execute()
        self.assertFalse(any(e[0] != "GET" for e in client.events))
        manifest, client, request = self.fixture()
        client.api("releases", "POST", {"tag_name": archive.TAG, "target_commitish": archive.TARGET,
                   "prerelease": True, "draft": True})
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "bad"
            item = manifest["artifacts"][0]
            path.write_bytes(b"!" + client.originals[item["id"]][1:])
            client.upload(77, manifest["artifacts"][0]["planned_asset_name"], path)
            client.release["assets"][0]["digest"] = "sha256:" + item["sha256"]
            before = copy.deepcopy(client.data)
            with self.assertRaises(ArchiveError):
                archive.EvidenceArchive(client, manifest, "snapshot", request, directory).execute()
            self.assertEqual(client.data, before)

    def test_public_wrong_host_or_latest_misclassification_never_completes(self):
        for changed in ["host", "latest"]:
            manifest, client, request = self.fixture()
            original = client.upload
            def uploaded(release_id, name, path):
                value = original(release_id, name, path)
                if changed == "host":
                    client.release["assets"][-1]["browser_download_url"] = value["browser_download_url"].replace("github.com", "other.example")
                else:
                    client.official["id"] = 77
                return value
            client.upload = uploaded
            with self.subTest(changed=changed), tempfile.TemporaryDirectory() as directory:
                run = archive.EvidenceArchive(client, manifest, "snapshot", request, directory)
                with self.assertRaises(ArchiveError):
                    run.execute()
                self.assertEqual(run.status["state"], "실패")
                self.assertFalse(any(a["name"] == archive.INDEX for a in client.release["assets"]))

    def test_wrong_release_cannot_receive_even_a_failure_body_patch(self):
        manifest, client, request = self.fixture()
        client.release = {**client.official, "tag_name": archive.TAG,
                          "target_commitish": "main", "prerelease": False, "draft": False}
        with tempfile.TemporaryDirectory() as directory, self.assertRaises(ArchiveError):
            archive.EvidenceArchive(client, manifest, "snapshot", request, directory).execute()
        self.assertTrue(all(e[0] == "GET" for e in client.events))

    def test_index_collision_and_missing_public_zip_are_not_repaired_by_overwrite(self):
        manifest, client, request = self.fixture()
        with tempfile.TemporaryDirectory() as directory:
            archive.EvidenceArchive(client, manifest, "snapshot", request, directory).execute()
            index = next(a for a in client.release["assets"] if a["name"] == archive.INDEX)
            invalid = json.loads(client.data[index["id"]])
            invalid["target_commit"] = "f" * 40
            payload = archive.json_bytes(invalid)
            client.data[index["id"]] = payload
            index["size"] = len(payload)
            index["digest"] = "sha256:" + hashlib.sha256(payload).hexdigest()
            before = copy.deepcopy(client.data)
            with self.assertRaises(ArchiveError):
                archive.EvidenceArchive(client, manifest, "snapshot", request, directory).execute()
            self.assertEqual(client.data, before)
            client.release["assets"] = [a for a in client.release["assets"] if a["name"] != manifest["artifacts"][0]["planned_asset_name"]]
            with self.assertRaises(ArchiveError):
                archive.EvidenceArchive(client, manifest, "snapshot", request, directory).execute()
            self.assertEqual(client.data, before)



def supplemental_dataset_fixture(directory, mutate=None):
    """실제 새 ID를 추측하지 않는 mock6ZIP. SHA는 실제 fixture bytes에서 계산한다."""
    manifest, _ = archive.load_manifest()
    batch = replace(archive.SUPPLEMENTAL_BATCH, manifest=Path(directory) / "manifest.json",
                    artifact_ids=(70042, 13030660873, 72026, 13025494772, 13026322503, 73000))
    manifest["archive"].update(tag=batch.tag, index_name=batch.index)
    manifest["source_run"].update(id=batch.run_id, head_sha="19c1faa3a1e6b8a921fd3cbe1642ac3824c955b6")
    manifest["artifacts"] = []
    originals = {}
    for identifier, name in zip(batch.artifact_ids, batch.artifact_names):
        data = b"PK\x03\x04mock-only-" + name.encode()
        originals[identifier] = data
        manifest["artifacts"].append({"id": identifier, "name": name, "bytes": len(data),
            "sha256": hashlib.sha256(data).hexdigest(), "planned_asset_name": name + ".zip"})
    if mutate:
        mutate(manifest)
    source = manifest["source_run"]
    snapshot = archive.json_bytes({actual: source[key] for actual, key in (
        ("id", "id"), ("run_attempt", "attempt"), ("event", "event"), ("status", "status"),
        ("conclusion", "conclusion"), ("head_sha", "head_sha"), ("path", "workflow_path"))})
    Path(directory, "run-metadata.json").write_bytes(snapshot)
    manifest["run_metadata_snapshot"] = {"file": "run-metadata.json", "bytes": len(snapshot),
                                          "sha256": hashlib.sha256(snapshot).hexdigest()}
    raw = archive.json_bytes(manifest)
    batch.manifest.write_bytes(raw)
    batch = replace(batch, manifest_sha256=hashlib.sha256(raw).hexdigest())
    return batch, manifest, snapshot.decode(), originals


class SupplementalArchiveTests(unittest.TestCase):
    def client(self, batch, manifest, originals):
        environment, event = event_fixture(batch)
        request = archive.request_context(environment, event, batch)
        return FakeGitHub(manifest, originals, request, batch), request

    def test_original_is_always_default_and_supplement_is_explicit_and_unready(self):
        self.assertIs(archive.selected_batch(), archive.ORIGINAL_BATCH)
        self.assertIs(archive.selected_batch(True), archive.SUPPLEMENTAL_BATCH)
        self.assertEqual(archive.load_manifest()[0]["archive"]["tag"], archive.TAG)
        self.assertNotEqual(archive.ORIGINAL_BATCH.label, archive.SUPPLEMENTAL_BATCH.label)
        self.assertNotEqual(archive.ORIGINAL_BATCH.workflow, archive.SUPPLEMENTAL_BATCH.workflow)
        self.assertNotEqual(archive.ORIGINAL_BATCH.tag, archive.SUPPLEMENTAL_BATCH.tag)
        self.assertNotEqual(archive.ORIGINAL_BATCH.index, archive.SUPPLEMENTAL_BATCH.index)
        unready_batch = replace(archive.SUPPLEMENTAL_BATCH, manifest_sha256=None)
        with self.assertRaises(ArchiveError):
            archive.load_manifest(batch=unready_batch)
        with tempfile.TemporaryDirectory() as directory, patch.object(archive, "GitHubClient") as constructor:
            with patch("sys.argv", ["archive", "--execute", "--supplemental-41159864831", "--output", directory]), \
                    patch.object(archive, "selected_batch", return_value=unready_batch) as selection:
                with self.assertRaises(ArchiveError):
                    archive.main()
                selection.assert_called_once_with(True)
            constructor.assert_not_called()
            self.assertEqual(json.loads(Path(directory, "status.json").read_text())["phase"], "request-validation")

    def test_arbitrary_manifest_url_tag_run_cli_is_not_available(self):
        for option in ["--manifest", "--url", "--tag", "--run"]:
            with self.subTest(option=option), patch("sys.stderr"), patch.object(archive, "GitHubClient") as client:
                with patch("sys.argv", ["archive", "--execute", "--output", "unused", option, "other"]):
                    with self.assertRaises(SystemExit) as rejected:
                        archive.main()
                self.assertEqual(rejected.exception.code, 2)
                client.assert_not_called()

    def test_complete_six_fixture_has_pinned_bytes_and_final_snapshot(self):
        with tempfile.TemporaryDirectory() as directory:
            batch, manifest, snapshot, _ = supplemental_dataset_fixture(directory)
            self.assertEqual(archive.load_manifest(batch=batch), (manifest, snapshot))
            Path(directory, "run-metadata.json").write_text(snapshot + " ")
            with self.assertRaises(ArchiveError):
                archive.load_manifest(batch=batch)

    def test_missing_ids_bytes_hashes_count_and_old_batch_are_rejected(self):
        mutations = {
            "missing-id": lambda m: m["artifacts"][0].update(id=None),
            "missing-bytes": lambda m: m["artifacts"][0].update(bytes=None),
            "missing-sha": lambda m: m["artifacts"][0].update(sha256=None),
            "missing-item": lambda m: m["artifacts"].pop(),
            "extra-item": lambda m: m["artifacts"].append(copy.deepcopy(m["artifacts"][0])),
            "old-id": lambda m: m["artifacts"][0].update(id=13023894081),
            "other-name": lambda m: m["artifacts"][0].update(name="other-run-seed-42"),
            "old-tag": lambda m: m["archive"].update(tag=archive.TAG),
            "old-index": lambda m: m["archive"].update(index_name=archive.INDEX),
            "old-run": lambda m: m["source_run"].update(id=41131479924),
            "other-head": lambda m: m["source_run"].update(head_sha="f" * 40),
            "other-workflow": lambda m: m["source_run"].update(workflow_path=".github/workflows/other.yml"),
            "other-measured-source": lambda m: m["source_run"].update(measured_source_commit="f" * 40),
            "running": lambda m: m["source_run"].update(status="in_progress"),
            "unknown-result": lambda m: m["source_run"].update(conclusion=None),
        }
        for name, mutate in mutations.items():
            with self.subTest(name=name), tempfile.TemporaryDirectory() as directory:
                batch, _, _, _ = supplemental_dataset_fixture(directory, mutate)
                with self.assertRaises(ArchiveError):
                    archive.load_manifest(batch=batch)
        with tempfile.TemporaryDirectory() as directory:
            batch, _, _, _ = supplemental_dataset_fixture(directory)
            with self.assertRaises(ArchiveError):
                archive.load_manifest(batch=replace(batch, artifact_ids=(None, *batch.artifact_ids[1:])))

    def test_old_label_and_wrong_workflow_or_changed_pr_cannot_write(self):
        with tempfile.TemporaryDirectory() as directory:
            batch, manifest, _, originals = supplemental_dataset_fixture(directory)
            environment, event = event_fixture()
            with self.assertRaises(ArchiveError):
                archive.request_context(environment, event, batch)
            for field in ["workflow", "pr", "label", "actor"]:
                client, request = self.client(batch, manifest, originals)
                if field == "workflow":
                    client.run["path"] = archive.WORKFLOW
                elif field == "pr":
                    client.live["head"]["sha"] = "f" * 40
                elif field == "label":
                    client.live["labels"] = [{"name": archive.LABEL}]
                else:
                    client.run["actor"]["login"] = "other"
                with self.subTest(field=field), self.assertRaises(ArchiveError):
                    archive.attest_request(client, request, request["head_sha"], batch)
                self.assertTrue(all(e[0] == "GET" for e in client.events))

    def test_new_six_flow_preserves_old_six_and_product_latest(self):
        with tempfile.TemporaryDirectory() as directory:
            batch, manifest, snapshot, originals = supplemental_dataset_fixture(directory)
            client, request = self.client(batch, manifest, originals)
            old_manifest, _ = archive.load_manifest()
            old_archive = {"id": 800, "tag_name": archive.TAG, "assets": [
                {"name": a["planned_asset_name"], "digest": "sha256:" + a["sha256"]}
                for a in old_manifest["artifacts"]] + [{"name": archive.INDEX, "digest": "old-index-unchanged"}]}
            client.other_releases = [old_archive]
            previous, official = copy.deepcopy(old_archive), copy.deepcopy(client.official)
            run = archive.EvidenceArchive(client, manifest, snapshot, request, directory, batch=batch)
            run.execute()
            self.assertEqual(run.status["state"], "완료")
            self.assertEqual(len(client.release["assets"]), 7)
            self.assertEqual(client.other_releases, [previous])
            self.assertEqual(client.official, official)
            index = json.loads(Path(directory, batch.index).read_text())
            self.assertEqual(len(index["zip_assets"]), 6)
            self.assertEqual(index["manifest"]["source_run"]["conclusion"], "failure")
            self.assertEqual(index["original_run_metadata_utf8"], snapshot)
            self.assertFalse(any(e[0] in ("POST", "PATCH") and e[1] == "releases/800" for e in client.events))
            before = copy.deepcopy(client.data)
            client.source_unavailable = True
            archive.EvidenceArchive(client, manifest, snapshot, request, directory, batch=batch).execute()
            self.assertEqual(client.data, before)
            self.assertEqual(len([e for e in client.events if e[0] == "UPLOAD"]), 7)

    def test_wrong_metadata_and_original_bytes_stop_before_draft(self):
        for changed in ["conclusion", "original-bytes"]:
            with self.subTest(changed=changed), tempfile.TemporaryDirectory() as directory:
                batch, manifest, snapshot, originals = supplemental_dataset_fixture(directory)
                client, request = self.client(batch, manifest, originals)
                if changed == "original-bytes":
                    identifier = batch.artifact_ids[0]
                    client.originals[identifier] = b"!" + client.originals[identifier][1:]
                else:
                    previous = client.read
                    def changed_read(path):
                        value = previous(path)
                        if path == "actions/runs/" + str(batch.run_id):
                            value["conclusion"] = "success"
                        return value
                    client.read = changed_read
                with self.assertRaises(ArchiveError):
                    archive.EvidenceArchive(client, manifest, snapshot, request, directory, batch=batch).execute()
                self.assertFalse(any(e[0] in ("POST", "PATCH", "UPLOAD") for e in client.events))

    def test_public_corruption_or_latest_selection_cannot_complete(self):
        for changed in ["public-bytes", "latest"]:
            with self.subTest(changed=changed), tempfile.TemporaryDirectory() as directory:
                batch, manifest, snapshot, originals = supplemental_dataset_fixture(directory)
                client, request = self.client(batch, manifest, originals)
                if changed == "public-bytes":
                    client.corrupt_public = manifest["artifacts"][0]["planned_asset_name"]
                else:
                    previous = client.upload
                    def selected_latest(release_id, name, path):
                        result = previous(release_id, name, path)
                        client.official["id"] = 77
                        return result
                    client.upload = selected_latest
                run = archive.EvidenceArchive(client, manifest, snapshot, request, directory, batch=batch)
                with self.assertRaises(ArchiveError):
                    run.execute()
                self.assertEqual(run.status["state"], "실패")
                self.assertFalse(any(a["name"] == batch.index for a in client.release["assets"]))





if __name__ == "__main__":
    from test_release_lookup import ReleaseLookupTests
    unittest.main()
