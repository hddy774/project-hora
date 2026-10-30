"""릴리스 입력/게시자 연결의 순수 가드 검사. API·서명·게시·앱 실행은 금지한다."""
import ast
import copy
import importlib.util
import json
import os
from pathlib import Path
import subprocess
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[4]
BASE = 'ffd83f96ae1bdd60ccac60df7e0116abcde6f827'

def load(name, path):
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

def node_map(text):
    return {n.name: ast.dump(n, include_attributes=False) for n in ast.parse(text).body if isinstance(n, (ast.FunctionDef, ast.AsyncFunctionDef))}

def main():
    verify = load('release_guard', 'scripts/verify-v021-release.py')
    publisher = load('release_publisher', 'scripts/publish-v021-release.py')
    protected = {
        'scripts/verify-v021-release.py': ['version', 'product_path', 'fingerprint', 'evidence_file', 'functional_report',
            'browser_report', 'feature_gate', 'measurement_rows', 'canonical_performance_reports', 'performance_gate',
            'legacy_speed_comparison', 'inspect_apk', 'proof_matches', 'windows_proof', 'platform_gate'],
        'scripts/publish-v021-release.py': ['release', 'check_release', 'ensure_tag', 'notes', 'main'],
    }
    unchanged = []
    for path, names in protected.items():
        old = subprocess.check_output(['git', 'show', BASE + ':' + path], cwd=ROOT, text=True)
        new = (ROOT / path).read_text()
        old_nodes, new_nodes = node_map(old), node_map(new)
        for name in names:
            assert old_nodes[name] == new_nodes[name], (path, name)
            unchanged.append(path + ':' + name)
        ast.parse(new)
    old_verify = ast.parse(subprocess.check_output(['git', 'show', BASE + ':scripts/verify-v021-release.py'], cwd=ROOT, text=True))
    old_exclusions = next(ast.literal_eval(n.value) for n in old_verify.body if isinstance(n, ast.Assign) and
                          any(isinstance(t, ast.Name) and t.id == 'RELEASE_ONLY' for t in n.targets))
    assert verify.RELEASE_ONLY == old_exclusions
    assert all(not verify.product_path(p) for p in (verify.WORKFLOW, 'scripts/verify-v021-release.py', 'scripts/publish-v021-release.py'))
    workflow = (ROOT / verify.WORKFLOW).read_text()
    original_workflow = subprocess.check_output(['git', 'show', BASE + ':' + verify.WORKFLOW], cwd=ROOT, text=True)
    assert workflow.split('  workflow_dispatch:', 1)[1].split('  issue_comment:', 1)[0].strip() == original_workflow.split('  workflow_dispatch:', 1)[1].split('permissions:', 1)[0].strip()
    assert 'GITHUB_EVENT_NAME:' not in workflow and 'types: [created]' in workflow
    assert workflow.count('needs.review.outputs.verify_upgrade') == 3
    for command in ('scripts/sign-retained-apk.sh', 'scripts/v021-release-emulator.py', ' --stage features ', ' --stage signing ', ' --stage publish ',
                    'scripts/publish-v021-release.py prepare ', 'scripts/publish-v021-release.py readback ', 'scripts/publish-v021-release.py publish '):
        assert command in workflow
    assert 'api: [24, 35]' in workflow and 'ref: ${{ github.sha }}' in workflow
    commit = 'a' * 40
    def exercise(event, attempt=1, mutation=None):
        request = {'event': event, 'mode': 'owner-comment' if event == 'issue_comment' else 'manual',
                   'source_commit': commit, 'publish': True, 'verify_upgrade': True, 'comment_id': 321 if event == 'issue_comment' else None}
        source = {'event': event, 'request': request, 'source_commit': commit, 'run_id': '123', 'run_attempt': str(attempt)}
        metadata = {'id': 123, 'event': event, 'actor': {'login': verify.OWNER}, 'triggering_actor': {'login': verify.OWNER},
                    'head_repository': {'full_name': verify.REPOSITORY}, 'repository': {'full_name': verify.REPOSITORY},
                    'head_sha': commit, 'head_branch': 'main', 'path': verify.WORKFLOW, 'run_attempt': attempt}
        passed = set(publisher.REQUIRED_JOBS)
        main_sha = commit
        if mutation == 'actual_event': metadata['event'] = 'workflow_dispatch'
        if mutation == 'rerun_actor': metadata['triggering_actor']['login'] = 'other'
        if mutation == 'attempt': metadata['run_attempt'] = attempt + 1
        if mutation == 'missing_job': passed.remove(sorted(passed)[0])
        if mutation == 'main_advanced': main_sha = 'b' * 40
        def forbidden(*args, **kwargs):
            raise AssertionError('실제 GitHub/릴리스 호출은 검사에서 금지한다')
        def fake_api(path, body=None):
            assert body is None
            if path == 'actions/runs/123': return copy.deepcopy(metadata)
            if path == 'actions/runs/123/attempts/' + str(attempt) + '/jobs?per_page=100':
                return {'jobs': [{'name': name, 'conclusion': 'success'} for name in sorted(passed)]}
            if path == 'git/ref/heads/main': return {'object': {'sha': main_sha}}
            raise AssertionError('예상하지 않은 읽기/쓰기: ' + path)
        with patch.object(publisher.verify, 'owner_context', return_value=request), patch.object(publisher.verify, 'check_source', return_value=source), \
             patch.object(publisher, 'api', side_effect=fake_api), patch.object(publisher, 'gh', side_effect=forbidden), \
             patch.dict(os.environ, {'GITHUB_RUN_ID': '123', 'GITHUB_RUN_ATTEMPT': str(attempt)}):
            publisher.actual_source_run(source)
    record_request = {'event': 'issue_comment', 'mode': 'owner-comment', 'source_commit': commit, 'publish': True, 'verify_upgrade': True}
    def record_at(head):
        def fake_run(*args, **kwargs):
            if args == ('git', 'rev-parse', 'HEAD'): return head + '\n'
            if args == ('git', 'status', '--porcelain', '--untracked-files=no'): return ''
            raise AssertionError('예상하지 않은 소스기록 호출')
        with patch.object(verify, 'release_request_context', return_value=record_request), patch.object(verify, 'run', side_effect=fake_run), \
             patch.object(verify, 'fingerprint', return_value='c' * 64):
            return verify.source_record()
    assert record_at(commit)['request'] == record_request
    try:
        record_at('b' * 40)
    except ValueError:
        pass
    else:
        raise AssertionError('검사 뒤 checkout 변경을 거부해야 한다')
    for event in ('workflow_dispatch', 'issue_comment'):
        exercise(event)
        exercise(event, 2)
    for mutation in ('actual_event', 'rerun_actor', 'attempt', 'missing_job', 'main_advanced'):
        try:
            exercise('issue_comment', mutation=mutation)
        except (ValueError, KeyError):
            continue
        raise AssertionError('게시자 실제연결 부정검사 미거부: ' + mutation)
    print(json.dumps({'base_commit': BASE, 'python_ast': 'passed', 'unchanged_protected_functions': unchanged,
        'product_fingerprint_scope_unchanged': True, 'manual_dispatch_options_preserved': True,
        'source_record_positive_cases': 1, 'source_record_checkout_change_negative_cases': 1,
        'actual_source_run_wrapper_positive_cases': 4, 'actual_source_run_wrapper_negative_cases': 5,
        'actual_event_name_overridden': False, 'api_signing_publication_or_app_executed': False}, ensure_ascii=False))

if __name__ == '__main__':
    main()
