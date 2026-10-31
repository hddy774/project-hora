#!/usr/bin/env python3
"""토큰·redirect·미디어 형식의 순수 HTTP 경계 검사. 외부 네트워크를 호출하지 않는다."""
import io
from pathlib import Path
import tempfile
import unittest
import urllib.error
import urllib.parse
import urllib.request

import archive_sensitivity as archive
from http_client import ArchiveError, GitHubClient, NoCrossHostAuthorization, https_url


class HttpClientTests(unittest.TestCase):
    def test_actions_zip_entry_json_and_release_asset_octet_are_distinct(self):
        from test_release_lookup import fixture
        manifest, client, request = fixture()
        captured = []
        class MediaBoundary:
            def open(self, message, **_kwargs):
                accept = message.get_header("Accept")
                captured.append((message.full_url, accept))
                if "/actions/artifacts/" in message.full_url:
                    if accept != "application/json":
                        raise urllib.error.HTTPError(message.full_url, 415, "Unsupported Accept", {}, None)
                    identifier = int(message.full_url.split("/artifacts/", 1)[1].split("/", 1)[0])
                    return io.BytesIO(client.originals[identifier])
                assert accept == "application/octet-stream", "release 자산의 byte 미디어를 바꾸면 안 됩니다"
                return io.BytesIO(b"ZIP")
        transport = GitHubClient("unit-secret", MediaBoundary())
        client.download = transport.download
        with tempfile.TemporaryDirectory() as directory:
            run = archive.EvidenceArchive(client, manifest, "snapshot", request, directory)
            run.prepare_originals()
            self.assertEqual(len(captured), 6)
            self.assertTrue(all(accept == "application/json" for _, accept in captured))
            path = Path(directory) / "release.zip"
            transport.download(archive.API_ROOT + "releases/assets/100", path, 3)
            transport.download("https://github.com/" + archive.REPOSITORY + "/releases/download/tag/file.zip",
                               path, 3, public=True)
            self.assertEqual([accept for _, accept in captured[-2:]], ["application/octet-stream"] * 2)

    def test_delete_and_asset_replacement_are_rejected_before_transport(self):
        class NoTransport:
            def open(self, *_args, **_kwargs):
                raise AssertionError("외부 요청이 발생하면 안 됩니다")
        for method, path in [("DELETE", "releases/77"), ("DELETE", "releases/assets/100"),
                             ("PATCH", "releases/assets/100"), ("POST", "git/refs")]:
            with self.subTest(method=method, path=path), self.assertRaises(ArchiveError):
                GitHubClient("unit-secret", NoTransport()).api(path, method, {})

    def test_bad_address_never_echoes_a_secret_port(self):
        with self.assertRaises(ArchiveError) as failure:
            https_url("https://api.github.com:unit-secret/path")
        self.assertNotIn("unit-secret", str(failure.exception))

    def test_cross_host_redirect_strips_auth_and_never_allows_http(self):
            handler = NoCrossHostAuthorization()
            request = urllib.request.Request("https://api.github.com/file", headers={"Authorization": "Bearer unit-secret"})
            redirected = handler.redirect_request(request, None, 302, "", {}, "https://blob.example/file?signature=temporary")
            self.assertIsNone(redirected.get_header("Authorization"))
            same = handler.redirect_request(request, None, 302, "", {}, "https://api.github.com/next")
            self.assertEqual(same.get_header("Authorization"), "Bearer unit-secret")
            with self.assertRaises(ArchiveError):
                handler.redirect_request(request, None, 302, "", {}, "http://blob.example/file")

    def test_transport_failure_does_not_echo_token(self):
            class FailedOpener:
                def open(self, *_args, **_kwargs):
                    raise urllib.error.URLError("unit-secret")
            with self.assertRaises(ArchiveError) as failure:
                GitHubClient("unit-secret", FailedOpener()).api("releases/latest")
            self.assertNotIn("unit-secret", str(failure.exception))

    def test_upload_uses_json_response_and_correct_payload_media(self):
            class CapturedOpener:
                def open(self, request, **_kwargs):
                    self.request = request
                    return io.BytesIO(b'{"id": 1}')
            opener = CapturedOpener()
            with tempfile.TemporaryDirectory() as directory:
                path = Path(directory) / "index.json"
                path.write_bytes(b"{}")
                GitHubClient("unit-secret", opener).upload(77, archive.INDEX, path)
                self.assertEqual(opener.request.get_header("Accept"), "application/vnd.github+json")
                self.assertEqual(opener.request.get_header("Content-type"), "application/json")
                self.assertEqual(urllib.parse.urlsplit(opener.request.full_url).hostname, "uploads.github.com")


if __name__ == "__main__":
    unittest.main()
