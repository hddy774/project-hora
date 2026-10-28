#!/usr/bin/env python3
"""검증된 실행의 APK 한 개만 초안에 올리고, 재다운로드 검사 뒤 stable latest로 게시한다."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

REPOSITORY = 'hddy774/project-hora'
TAG = 'xianxia-observer/v0.1.0'
NAME = 'XianxiaObserver-v0.1.0.apk'
PIN = '0832f24441f1caf335166fbc16ae0b9536e262ca48f258926a8027fc31dca25a'
ROOT = Path('release-input')


def gh(*args):
    """주입된 GitHub CLI 인증을 사용한다. 자격 증명은 읽거나 출력하지 않는다."""
    return subprocess.check_output(['gh', *args], text=True)


def api(path):
    """저장소 API의 JSON 응답을 읽는다."""
    return json.loads(gh('api', f'repos/{REPOSITORY}/{path}'))


def verify_input():
    """동일 실행에서 서명·에뮬레이터 검사를 마친 APK의 출처와 해시를 확인한다."""
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


def prepare(source, apk, digest):
    """검증 완료 APK 하나를 같은 소스의 초안에만 첨부한다."""
    notes = Path('games/xianxia-observer/docs/release-notes-v0.1.0.md').read_text()
    run = f'https://github.com/{REPOSITORY}/actions/runs/{os.environ["GITHUB_RUN_ID"]}'
    notes += (f'\n\n소스: `{source}`\n\nAPK SHA-256: `{digest}`\n\n'
              f'게시자 인증서 SHA-256: `{PIN}`\n\n[검사·빌드·서명·Android 실행 기록]({run})\n')
    Path('release-notes.md').write_text(notes)
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
        gh('release', 'create', TAG, '--repo', REPOSITORY, '--target', source, '--draft',
           '--title', '선협 관찰 v0.1.0', '--notes-file', 'release-notes.md')
        release = release_or_none()
    verify_draft(release, source, apk, digest, False)
    gh('release', 'edit', TAG, '--repo', REPOSITORY, '--notes-file', 'release-notes.md')
    shutil.copyfile(apk, ROOT / NAME)
    if not release['assets']:
        gh('release', 'upload', TAG, str(ROOT / NAME), '--repo', REPOSITORY)
    verify_draft(release_or_none(), source, apk, digest, True)
    print('Verified APK-only draft prepared: ' + TAG)


def publish(source, apk, digest):
    """초안의 APK 재다운로드 검사를 요구하고 최신 안정 릴리스로 게시한다."""
    downloaded = Path('readback') / NAME
    assert downloaded.stat().st_size == apk.stat().st_size
    assert hashlib.file_digest(downloaded.open('rb'), 'sha256').hexdigest() == digest
    verify_draft(release_or_none(), source, apk, digest, True)
    gh('release', 'edit', TAG, '--repo', REPOSITORY, '--draft=false', '--prerelease=false', '--latest=true')
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
    assert len(sys.argv) == 2 and sys.argv[1] in ('prepare', 'publish')
    source_commit, input_apk, apk_digest = verify_input()
    if sys.argv[1] == 'prepare':
        prepare(source_commit, input_apk, apk_digest)
    else:
        publish(source_commit, input_apk, apk_digest)
