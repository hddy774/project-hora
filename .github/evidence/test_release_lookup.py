"""태그가 아직 없는 draft·인증 목록·고정 ID의 GitHub 조회 경계를 검사한다."""
import copy
import tempfile
import unittest

import archive_sensitivity as archive
from http_client import ArchiveError
from test_archive import FakeGitHub, dataset_fixture, event_fixture


def fixture():
    environment, event = event_fixture()
    request = archive.request_context(environment, event)
    manifest, originals = dataset_fixture()
    return manifest, FakeGitHub(manifest, originals, request), request


class ReleaseLookupTests(unittest.TestCase):
    def test_draft_tag_endpoint_404_still_uploads_publishes_and_reads_same_id(self):
        manifest, client, request = fixture()
        with tempfile.TemporaryDirectory() as directory:
            run = archive.EvidenceArchive(client, manifest, "snapshot", request, directory)
            run.guard()
            run.prepare_originals()
            run.prepare_draft()
            self.assertTrue(client.release["draft"])
            self.assertIsNone(client.api("releases/tags/" + archive.encoded(archive.TAG), missing=True))
            self.assertIsNone(client.tag)
            self.assertEqual(len(client.release["assets"]), 6)
            run.publish_zip_archive()
            run.final_index()
            self.assertEqual(run.status["state"], "완료")
            self.assertEqual(run.release["id"], 77)
            self.assertEqual(len([e for e in client.events if e[:2] == ("POST", "releases")]), 1)
            self.assertGreaterEqual(len([e for e in client.events if e[:2] == ("GET", "releases/77")]), 8)

    def test_existing_draft_after_full_page_is_reused_without_duplicate_creation(self):
        manifest, client, request = fixture()
        client.api("releases", "POST", {"tag_name": archive.TAG, "target_commitish": archive.TARGET,
                   "prerelease": True, "draft": True})
        client.other_releases = [{"id": 1000 + i, "tag_name": "unrelated-" + str(i)} for i in range(100)]
        client.events.clear()
        with tempfile.TemporaryDirectory() as directory:
            run = archive.EvidenceArchive(client, manifest, "snapshot", request, directory)
            run.execute()
            self.assertEqual(run.status["state"], "완료")
            self.assertFalse(any(e[0] == "POST" for e in client.events))
            self.assertIn(("GET", "releases?per_page=100&page=2", None), client.events)
            self.assertEqual(run.release["id"], 77)

    def test_multiple_matching_published_and_draft_releases_fail_before_any_write(self):
        manifest, client, request = fixture()
        client.api("releases", "POST", {"tag_name": archive.TAG, "target_commitish": archive.TARGET,
                   "prerelease": True, "draft": True})
        published = copy.deepcopy(client.release)
        published.update(id=78, draft=False)
        client.other_releases = [published]
        client.events.clear()
        with tempfile.TemporaryDirectory() as directory, self.assertRaises(ArchiveError):
            archive.EvidenceArchive(client, manifest, "snapshot", request, directory).execute()
        self.assertTrue(all(e[0] == "GET" for e in client.events))

    def test_fixed_id_response_cannot_switch_to_another_release_or_duplicate_page(self):
        for changed in ("id", "page"):
            manifest, client, request = fixture()
            client.api("releases", "POST", {"tag_name": archive.TAG, "target_commitish": archive.TARGET,
                       "prerelease": True, "draft": True})
            read = client.read
            def mismatch(path):
                if changed == "page" and path.startswith("releases?per_page=100&page="):
                    return [{"id": 1000 + i, "tag_name": "other"} for i in range(100)]
                result = read(path)
                if changed == "id" and path == "releases/77":
                    result["id"] = 78
                return result
            client.read = mismatch
            client.events.clear()
            with self.subTest(changed=changed), tempfile.TemporaryDirectory() as directory:
                with self.assertRaises(ArchiveError):
                    archive.EvidenceArchive(client, manifest, "snapshot", request, directory).execute()
                self.assertTrue(all(e[0] == "GET" for e in client.events))
