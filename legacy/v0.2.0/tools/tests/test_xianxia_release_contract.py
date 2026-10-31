"""APK 버전·서명·게시·표시의 불일치와 공개 첨부 변경을 배포 전에 잡는다."""
from pathlib import Path
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
GAME = ROOT / 'games/xianxia-observer'


class ReleaseContractTests(unittest.TestCase):
    def test_publisher_requires_direct_success_without_inheriting_skipped_qa(self):
        workflow = (ROOT / '.github/workflows/xianxia-observer.yml').read_text()
        job = workflow.split('\n  publish:\n', 1)[1].split('\n  publish_existing:\n', 1)[0]
        condition = next(line.strip() for line in job.splitlines() if line.strip().startswith('if:'))
        self.assertIn('always()', condition, 'Skipped optional QA must not suppress publication.')
        self.assertIn('!cancelled()', condition)
        for dependency in ('check', 'android', 'emulator'):
            self.assertIn("needs." + dependency + ".result == 'success'", condition)
        self.assertIn('inputs.publish', condition)
        self.assertIn("inputs.source_run_id == ''", condition)

    def test_version_identity_and_release_targets_agree(self):
        android = ET.parse(GAME / 'src/Xianxia.Android/Xianxia.Android.csproj').getroot()
        version = android.findtext('.//ApplicationDisplayVersion')
        code = android.findtext('.//ApplicationVersion')
        self.assertEqual(version, '0.2.0')
        self.assertEqual(code, '2')
        self.assertEqual(android.findtext('.//ApplicationId'), 'org.hora.xianxiaobserver')
        self.assertEqual(ET.parse(GAME / 'Directory.Build.props').findtext('.//Version'), version)
        workflow = (ROOT / '.github/workflows/xianxia-observer.yml').read_text()
        self.assertRegex(workflow, rf'APP_VERSION: {re.escape(version)}\n')
        self.assertRegex(workflow, rf'APP_VERSION_CODE: {code}\n')
        self.assertIn('RELEASE_TAG: xianxia-observer/v' + version, workflow)
        self.assertIn('ARTIFACT_STEM: XianxiaObserver-v' + version, workflow)
        sign = (GAME / 'tools/retained-sign.sh').read_text()
        self.assertIn("VERSION='" + version + "'", sign)
        self.assertIn("versionCode='" + code + "'", sign)
        self.assertIn("'version_code':" + code, sign)
        publish = (GAME / 'tools/publish-release.py').read_text()
        self.assertIn("TAG = 'xianxia-observer/v" + version + "'", publish)
        self.assertIn("NAME = 'XianxiaObserver-v" + version + ".apk'", publish)
        self.assertIn("proof['version_code'] == " + code, publish)
        self.assertIn('run APK on Android emulator (api 24)', publish)
        self.assertIn('run APK on Android emulator (api 34)', publish)
        self.assertIn('선로만상록 프로젝트 ' + version,
                      (GAME / 'src/Xianxia.App/Sheets/SettingsSheet.cs').read_text())
        self.assertTrue((GAME / f'docs/release-notes-v{version}.md').is_file())
        self.assertIn('xianxia-observer/v0.1.0 --repo', workflow, '업데이트 기준은 기존 공개 버전을 유지한다.')


if __name__ == '__main__':
    unittest.main()
