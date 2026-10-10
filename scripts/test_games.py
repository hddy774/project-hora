"""Catalog contract tests; no Android SDK or third-party packages needed."""
import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import games


class CatalogTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.game = {"schemaVersion": 1, "id": "alpha-exchange", "name": "Alpha", "applicationId": "com.alphaexchange.offline",
                     "androidProject": "src/alpha.csproj", "checksProject": "tests/check.csproj", "artworkCheck": "scripts/art.py",
                     "apkPrefix": "AlphaExchange", "releaseNotes": "docs/v{version}.md", "legacyTagPrefix": "v",
                     "signing": {"script": "scripts/sign.sh", "secretPrefix": "ANDROID"}}
        for name in ["tests/check.csproj", "scripts/art.py", "scripts/sign.sh", "docs/v1.7.0.md"]:
            self.write(name, "fixture")
        self.project("src/alpha.csproj", "com.alphaexchange.offline")
        self.manifest(self.game)

    def write(self, path, content):
        file = self.root / path
        file.parent.mkdir(parents=True, exist_ok=True)
        file.write_text(content, encoding="utf-8")

    def project(self, path, app):
        self.write(path, f"<Project><PropertyGroup><TargetFramework>net9.0-android</TargetFramework><ApplicationId>{app}</ApplicationId><ApplicationVersion>8</ApplicationVersion><ApplicationDisplayVersion>1.7.0</ApplicationDisplayVersion></PropertyGroup></Project>")

    def manifest(self, game):
        self.write(f"games/{game['id']}/game.json", json.dumps(game))

    def test_legacy_tag_syntax_and_scoped_tags_keep_apk_name(self):
        game = games.read_games(self.root)[0]
        for tag in ["v1.7.0", "alpha-exchange/v1.7.0"]:
            self.assertEqual(games.release_info(game, tag, self.root)["apk_name"], "AlphaExchange-v1.7.0.apk")
        self.assertEqual(game["output"], "dist/alpha-exchange")
        self.assertEqual(games.release_info(game, "v1.7.0", self.root)["artifact_prefix"], "v1.7.0")
        self.assertEqual(games.release_info(game, "alpha-exchange/v1.7.0", self.root)["artifact_prefix"], "alpha-exchange-v1.7.0")

    def test_wrong_game_or_version_tags_rejected(self):
        game = games.read_games(self.root)[0]
        for tag in ["other/v1.7.0", "v1.8.0", "alpha-exchange/v1.8.0", "v1.7.0\nother=value"]:
            with self.assertRaises(ValueError): games.release_info(game, tag, self.root)

    def test_application_identity_must_match(self):
        self.game["applicationId"] = "com.other.game"
        self.manifest(self.game)
        with self.assertRaisesRegex(ValueError, "disagrees"): games.read_games(self.root)

    def test_second_independent_game_and_no_runtime_sharing(self):
        second = copy.deepcopy(self.game)
        second.update(id="test-game", name="Fixture only", applicationId="com.example.fixture", androidProject="src/second.csproj", apkPrefix="Fixture")
        second.pop("legacyTagPrefix")
        second.pop("signing")
        self.project(second["androidProject"], second["applicationId"])
        self.manifest(second)
        catalog = games.read_games(self.root)
        self.assertEqual(len(catalog), 2)
        self.assertEqual(games.select(catalog, "test-game")["output"], "dist/test-game")
        with self.assertRaisesRegex(ValueError, "signing route"): games.release_info(catalog[1], "test-game/v1.7.0", self.root)

    def test_duplicate_identity_and_apk_prefix_rejected(self):
        second = copy.deepcopy(self.game)
        second.update(id="test-game", androidProject="src/second.csproj")
        second.pop("legacyTagPrefix")
        self.project(second["androidProject"], second["applicationId"])
        self.manifest(second)
        with self.assertRaisesRegex(ValueError, "Duplicate application ID"): games.read_games(self.root)
        second["applicationId"] = "com.example.second"
        self.project(second["androidProject"], second["applicationId"])
        self.manifest(second)
        with self.assertRaisesRegex(ValueError, "Duplicate APK prefix"): games.read_games(self.root)

    def test_signing_namespace_is_independent(self):
        second = copy.deepcopy(self.game)
        second.update(id="test-game", applicationId="com.example.second", androidProject="src/second.csproj", apkPrefix="Second")
        second.pop("legacyTagPrefix")
        self.project(second["androidProject"], second["applicationId"])
        self.manifest(second)
        with self.assertRaisesRegex(ValueError, "Duplicate signing secret prefix"): games.read_games(self.root)
        second["signing"]["secretPrefix"] = "TEST_GAME_ANDROID"
        self.manifest(second)
        self.assertEqual(len(games.read_games(self.root)), 2)

    def test_traversal_and_unknown_game_rejected(self):
        for path in ["../outside", "/etc/passwd", "scripts/art.py\noutput=value", "scripts\\art.py"]:
            with self.assertRaises(ValueError): games.repo_file(self.root, path)
        with self.assertRaisesRegex(ValueError, "Unknown game"): games.select(games.read_games(self.root), "missing")

    def test_notes_required_only_for_release(self):
        (self.root / "docs/v1.7.0.md").unlink()
        game = games.read_games(self.root)[0]
        with self.assertRaisesRegex(ValueError, "Missing"): games.release_info(game, "v1.7.0", self.root)

    def test_only_alpha_owns_legacy_tags(self):
        self.game["legacyTagPrefix"] = "release-"
        self.manifest(self.game)
        with self.assertRaisesRegex(ValueError, "historical"): games.read_games(self.root)

    def test_check_arguments_do_not_change_project(self):
        with patch.object(games, "ROOT", self.root), patch.object(games, "read_games", return_value=games.read_games(self.root)), patch.object(games, "run") as run, patch("sys.argv", ["games.py", "check", "alpha-exchange", "--", "--storage-load"]):
            games.main()
            self.assertEqual(run.call_args_list[0].args[0], ["dotnet", "run", "--project", "tests/check.csproj", "-c", "Release", "--", "--storage-load"])

    def test_build_selects_project_and_isolated_output(self):
        with patch.object(games, "read_games", return_value=games.read_games(self.root)), patch.object(games, "run") as run, patch.dict("os.environ", {"ANDROID_HOME": "/android sdk", "JAVA_HOME": "/jdk"}), patch("sys.argv", ["games.py", "build", "alpha-exchange"]):
            games.main()
            command = run.call_args.args[0]
            self.assertEqual(command[:3], ["dotnet", "publish", "src/alpha.csproj"])
            self.assertIn("-p:AndroidSdkDirectory=/android sdk", command)
            self.assertEqual(command[-2:], ["-o", "dist/alpha-exchange"])

    def test_missing_toolchain_fails_before_build(self):
        with patch.object(games, "read_games", return_value=games.read_games(self.root)), patch.object(games, "run") as run, patch.dict("os.environ", {}, clear=True), patch("sys.argv", ["games.py", "build", "alpha-exchange"]):
            with self.assertRaisesRegex(ValueError, "ANDROID_HOME"): games.main()
            run.assert_not_called()


if __name__ == "__main__":
    unittest.main()
