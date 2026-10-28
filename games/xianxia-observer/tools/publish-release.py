#!/usr/bin/env python3
"""검증된 실행의 APK 한 개만 초안에 올리고, 재다운로드 검사 뒤 stable latest로 게시한다."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

REPOSITORY = 'hddy774/project-hora'
TAG = 'xianxia-observer/v0.1.0'
NAME = 'XianxiaObserver-v0.1.0.apk'
PIN = '0832f24441f1caf335166fbc16ae0b9536e262ca48f258926a8027fc31dca25a'
ROOT = Path('release-input')
PREPARED = Path('prepared-release.json')
PROJECT = Path(__file__).resolve().parents[1]


def gh(*args):
    """주입된 GitHub CLI 인증을 사용한다. 자격 증명은 읽거나 출력하지 않는다."""
    return subprocess.check_output(['gh', *args], text=True)


def api(path):
    """저장소 API의 JSON 응답을 읽는다."""
    return json.loads(gh('api', f'repos/{REPOSITORY}/{path}'))


def verify_source_run(run_id):
    """게시만 재시도할 때도 실제 소유자와 같은 실행의 소스·APK·Android 성공을 요구한다."""
    assert str(run_id).isdigit()
    if os.environ.get('GITHUB_ACTIONS') == 'true':
        assert os.environ['GITHUB_REPOSITORY'] == REPOSITORY
        assert os.environ['GITHUB_EVENT_NAME'] == 'workflow_dispatch'
        assert os.environ['GITHUB_ACTOR'] == 'hddy774'
        assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip() == os.environ['GITHUB_SHA']
    else:
        assert json.loads(gh('api', 'user'))['login'] == 'hddy774'
    run = api(f'actions/runs/{run_id}')
    assert run['status'] == 'completed' and run['event'] == 'workflow_dispatch'
    assert run['head_repository']['full_name'] == REPOSITORY and run['actor']['login'] == 'hddy774'
    assert run['path'] == '.github/workflows/xianxia-observer.yml'
    jobs = api(f'actions/runs/{run_id}/jobs?per_page=100')['jobs']
    required = {'format, rules, build, test', 'build Android APK', 'run APK on Android emulator'}
    assert {job['name'] for job in jobs if job['conclusion'] == 'success'} >= required
    publishers = {'publish verified APK only as latest', 'publish previously verified APK'}
    assert all(job['conclusion'] == 'success' or job['name'] in publishers for job in jobs)
    artifacts = api(f'actions/runs/{run_id}/artifacts?per_page=100')['artifacts']
    matches = [item for item in artifacts if item['name'] == f'xianxia-apk-{run_id}-{run["run_attempt"]}']
    assert len(matches) == 1 and not matches[0]['expired']
    assert matches[0]['workflow_run']['head_sha'] == run['head_sha']
    return run['head_sha']


def verify_input(source_run=None):
    """동일 실행에서 서명·에뮬레이터 검사를 마친 APK의 출처와 해시를 확인한다."""
    if source_run:
        source = verify_source_run(source_run)
    else:
        assert os.environ['GITHUB_REPOSITORY'] == REPOSITORY
        assert os.environ['GITHUB_EVENT_NAME'] == 'workflow_dispatch'
        assert os.environ['GITHUB_ACTOR'] == 'hddy774'
        source = os.environ['GITHUB_SHA']
        assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip() == source
    proof = json.loads((ROOT / 'SIGNING-VERIFICATION.json').read_text())
    assert proof['source_commit'] == source
    assert proof['application_id'] == 'org.hora.xianxiaobserver'
    assert proof['version'] == '0.1.0' and proof['version_code'] == 1
    assert proof['certificate_sha256'] == PIN
    assert proof['payload_matches_verified_unsigned'] is True
    assert proof['key_created'] is False and proof['original_bundle_modified'] is False
    apk = ROOT / 'XianxiaObserver-ci.apk'
    digest = hashlib.file_digest(apk.open('rb'), 'sha256').hexdigest()
    assert digest == proof['signed_sha256']
    return source, apk, digest


def release_or_none():
    """태그 조회 API는 공개 릴리스만 돌려주므로, 인증된 목록에서 초안도 함께 찾는다."""
    if PREPARED.exists():
        prepared = json.loads(PREPARED.read_text())
        assert prepared['tag'] == TAG and isinstance(prepared['id'], int)
        return api(f'releases/{prepared["id"]}')
    matches = []
    page = 1
    while True:
        releases = api(f'releases?per_page=100&page={page}')
        matches.extend(release for release in releases if release['tag_name'] == TAG)
        if len(releases) < 100:
            break
        page += 1
    assert len(matches) <= 1, 'Duplicate release tag'
    return matches[0] if matches else None


def verify_draft(release, source, apk, digest, require_asset):
    """다른 소스의 태그나 이미 게시된 릴리스를 덮어쓰지 않는다."""
    assert release['draft'] is True and release['prerelease'] is False
    assert release['tag_name'] == TAG and release['target_commitish'] == source
    assert len(release['assets']) <= 1
    for asset in release['assets']:
        assert asset['name'] == NAME and asset['size'] == apk.stat().st_size
        assert asset.get('digest') == 'sha256:' + digest
    if require_asset:
        assert len(release['assets']) == 1


def ensure_tag(source):
    """검증된 커밋으로 태그를 한 번만 만든다. 기존 태그는 절대 이동하지 않는다."""
    endpoint = f'repos/{REPOSITORY}/git/ref/tags/{TAG}'
    result = subprocess.run(['gh', 'api', endpoint], text=True, capture_output=True)
    if result.returncode == 0:
        obj = json.loads(result.stdout)['object']
        assert obj['type'] == 'commit' and obj['sha'] == source
        return
    assert 'HTTP 404' in result.stderr
    Path('tag-request.json').write_text(json.dumps({'ref': 'refs/tags/' + TAG, 'sha': source}))
    created = json.loads(gh('api', '--method', 'POST', f'repos/{REPOSITORY}/git/refs',
                            '--input', 'tag-request.json'))
    assert created['object']['type'] == 'commit' and created['object']['sha'] == source


def prepare(source, apk, digest, run_id):
    """검증 완료 APK 하나를 같은 소스의 초안에만 첨부한다."""
    notes = (PROJECT / 'docs/release-notes-v0.1.0.md').read_text()
    run = f'https://github.com/{REPOSITORY}/actions/runs/{run_id}'
    notes += (f'\n\n소스: `{source}`\n\nAPK SHA-256: `{digest}`\n\n'
              f'게시자 인증서 SHA-256: `{PIN}`\n\n[검사·빌드·서명·Android 실행 기록]({run})\n')
    if os.environ.get('GITHUB_ACTIONS') == 'true' and os.environ['GITHUB_RUN_ID'] != str(run_id):
        notes += (f'\n[게시·다운로드 검증](https://github.com/{REPOSITORY}/actions/runs/'
                  f'{os.environ["GITHUB_RUN_ID"]})\n')
    Path('release-notes.md').write_text(notes)
    ensure_tag(source)
    release = release_or_none()
    if release is None:
        # 기존 태그가 있으면 정확한 커밋인지 먼저 확인한다.
        tag = subprocess.run(['gh', 'api', f'repos/{REPOSITORY}/git/ref/tags/{TAG}'],
                             text=True, capture_output=True)
        if tag.returncode == 0:
            obj = json.loads(tag.stdout)['object']
            assert obj['type'] == 'commit' and obj['sha'] == source
        else:
            assert 'HTTP 404' in tag.stderr
        request = {'tag_name': TAG, 'target_commitish': source, 'name': '선협 관찰 v0.1.0',
                   'draft': True, 'prerelease': False, 'body': notes}
        Path('draft-request.json').write_text(json.dumps(request))
        # POST의 확정 응답 ID를 보존한다. 목록 캐시가 갱신되기를 기다리거나 초안을 중복 생성하지 않는다.
        release = json.loads(gh('api', '--method', 'POST', f'repos/{REPOSITORY}/releases',
                                '--input', 'draft-request.json'))
    verify_draft(release, source, apk, digest, False)
    PREPARED.write_text(json.dumps({'tag': TAG, 'id': release['id']}))
    # 확정된 태그/소스는 검증만 한다. 노트 수정에 workflow 변경 권한을 요구하지 않는다.
    Path('notes-request.json').write_text(json.dumps({'tag_name': TAG, 'body': notes}))
    gh('api', '--method', 'PATCH', f'repos/{REPOSITORY}/releases/{release["id"]}',
       '--input', 'notes-request.json')
    shutil.copyfile(apk, ROOT / NAME)
    if not release['assets']:
        # 태그를 먼저 확정해야 초안이 untagged로 바뀌거나 태그 조회에서 사라지지 않는다.
        gh('release', 'upload', TAG, str(ROOT / NAME), '--repo', REPOSITORY)
    verify_draft(release_or_none(), source, apk, digest, True)
    print('Verified APK-only draft prepared: ' + TAG)


def readback(source, apk, digest):
    """태그 조회를 거치지 않고 확정된 초안의 첨부 ID로 실제 APK를 다시 내려받는다."""
    release = release_or_none()
    verify_draft(release, source, apk, digest, True)
    output = Path('readback') / NAME
    output.parent.mkdir(exist_ok=True)
    with output.open('wb') as destination:
        subprocess.run(['gh', 'api', f'repos/{REPOSITORY}/releases/assets/{release["assets"][0]["id"]}',
                        '--header', 'Accept: application/octet-stream'], stdout=destination, check=True)
    assert output.stat().st_size == apk.stat().st_size
    assert hashlib.file_digest(output.open('rb'), 'sha256').hexdigest() == digest
    print('Draft APK downloaded and exact bytes verified.')


def publish(source, apk, digest):
    """초안의 APK 재다운로드 검사를 요구하고 최신 안정 릴리스로 게시한다."""
    downloaded = Path('readback') / NAME
    assert downloaded.stat().st_size == apk.stat().st_size
    assert hashlib.file_digest(downloaded.open('rb'), 'sha256').hexdigest() == digest
    verify_draft(release_or_none(), source, apk, digest, True)
    release = release_or_none()
    Path('publish-request.json').write_text(json.dumps(
        {'tag_name': TAG, 'draft': False, 'prerelease': False, 'make_latest': 'true'}))
    gh('api', '--method', 'PATCH', f'repos/{REPOSITORY}/releases/{release["id"]}',
       '--input', 'publish-request.json')
    latest = api('releases/latest')
    assert latest['tag_name'] == TAG and latest['target_commitish'] == source
    assert latest['draft'] is False and latest['prerelease'] is False
    assert len(latest['assets']) == 1 and latest['assets'][0]['name'] == NAME
    assert latest['assets'][0]['digest'] == 'sha256:' + digest
    assert latest['assets'][0]['size'] == apk.stat().st_size
    tag = api('git/ref/tags/' + TAG)['object']
    assert tag['type'] == 'commit' and tag['sha'] == source
    print('Published and verified APK-only stable latest: ' + latest['html_url'])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('operation', choices=('prepare', 'readback', 'publish'))
    parser.add_argument('--source-run', help='APK 빌드·서명·Android 검사가 성공한 실행으로 게시만 재시도')
    arguments = parser.parse_args()
    source_commit, input_apk, apk_digest = verify_input(arguments.source_run)
    if arguments.operation == 'prepare':
        prepare(source_commit, input_apk, apk_digest, arguments.source_run or os.environ['GITHUB_RUN_ID'])
    elif arguments.operation == 'readback':
        readback(source_commit, input_apk, apk_digest)
    else:
        publish(source_commit, input_apk, apk_digest)
