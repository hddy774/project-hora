"""PR 본문 보존, 승인·선행·완료 게이트와 게시 실패의 로컬 상태 보존을 검사한다."""
import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SOURCE = Path(__file__).resolve().parents[1] / 'xianxia-progress.py'
SPEC = importlib.util.spec_from_file_location('xianxia_progress', SOURCE)
progress = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(progress)
FIXTURE = Path(__file__).with_name('fixtures') / 'progress-fixture.json'


class ProgressTests(unittest.TestCase):
    def setUp(self):
        self.state = progress.load(FIXTURE)

    def test_managed_block_preserves_existing_body_and_is_idempotent(self):
        block = progress.render(self.state)
        body = '기존 검토\n\n' + progress.START + '\n이전 진행표\n' + progress.END + '\n작성자 유지\n'
        updated = progress.merge_body(body, block)
        self.assertTrue(updated.startswith('기존 검토\n\n'))
        self.assertTrue(updated.endswith('\n작성자 유지\n'))
        self.assertEqual(updated, progress.merge_body(updated, block))

    def test_missing_markers_append_and_corrupt_markers_abort(self):
        block = progress.render(self.state)
        self.assertTrue(progress.merge_body('기존 설명', block).startswith('기존 설명\n\n'))
        for body in [progress.START, progress.END + progress.START, progress.START * 2 + progress.END]:
            with self.subTest(body=body), self.assertRaises(ValueError):
                progress.merge_body(body, block)

    def test_pending_task_cannot_start_and_input_stays_unchanged(self):
        original = copy.deepcopy(self.state)
        with self.assertRaisesRegex(ValueError, '승인되지'):
            progress.change(self.state, 'XO-03', 'in_progress')
        self.assertEqual(original, self.state)

    def test_dependency_must_finish_even_when_task_is_approved(self):
        task = next(task for task in self.state['tasks'] if task['id'] == 'XO-12')
        task['approval'] = 'approved'
        with self.assertRaisesRegex(ValueError, '선행'):
            progress.change(self.state, 'XO-12', 'in_progress')

    def test_completion_requires_verification_and_evidence(self):
        started = progress.change(self.state, 'XO-06', 'in_progress')
        with self.assertRaisesRegex(ValueError, '전환'):
            progress.change(started, 'XO-06', 'done', evidence='commit')
        verified = progress.change(started, 'XO-06', 'verifying')
        with self.assertRaisesRegex(ValueError, '근거'):
            progress.change(verified, 'XO-06', 'done')
        completed = progress.change(verified, 'XO-06', 'done', evidence='https://github.com/example/proof')
        self.assertIn('**1/3 완료**', progress.render(completed))

    def test_baseline_and_planning_do_not_inflate_development_count(self):
        self.assertIn('**0/3 완료**', progress.render(self.state))
        self.assertIn('- [ ] XO-06', progress.render(self.state))

    def test_invalid_id_and_missing_block_reason_are_rejected(self):
        with self.assertRaisesRegex(ValueError, '없는 작업'):
            progress.change(self.state, 'XO-999', 'in_progress')
        with self.assertRaisesRegex(ValueError, '차단 이유'):
            progress.change(self.state, 'XO-06', 'blocked', note='')

    def test_publish_conflict_does_not_patch(self):
        with patch.object(progress, 'api', side_effect=[{'body': '처음'}, {'body': '다른 작성자 수정'}]) as api:
            with self.assertRaisesRegex(ValueError, '다른 작성자'):
                progress.publish(self.state)
            self.assertEqual(api.call_count, 2)

    def test_publish_changes_body_only(self):
        original = '기존 검토 본문'
        expected = progress.merge_body(original, progress.render(self.state))
        with patch.object(progress, 'api', side_effect=[{'body': original}, {'body': original}, {'body': expected}]) as api:
            progress.publish(self.state, pr=29)
            self.assertEqual(api.call_args.args[1], {'body': expected})

    def test_publish_failure_keeps_local_json_unchanged(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'progress.json'
            data = json.dumps(self.state, ensure_ascii=False)
            path.write_text(data)
            args = ['xianxia-progress.py', 'set', 'XO-06', '--status', 'in_progress', '--publish', '--file', str(path)]
            with patch('sys.argv', args), patch.object(progress, 'publish', side_effect=ValueError('게시 실패')):
                with self.assertRaises(SystemExit):
                    progress.main()
            self.assertEqual(path.read_text(), data)


if __name__ == '__main__':
    unittest.main()
