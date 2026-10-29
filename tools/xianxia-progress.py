#!/usr/bin/env python3
"""선협 개발 상태를 기록하고 PR의 지정 영역만 갱신한다. 기본 동작은 로컬 출력이다."""
import argparse
import copy
from datetime import datetime, timezone
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STATE = ROOT / 'docs/tasks/xianxia-progress.json'
START, END = '<!-- xianxia-progress:start -->', '<!-- xianxia-progress:end -->'
STATUS = {'planned': '예정', 'in_progress': '개발 중', 'verifying': '검증 중', 'done': '완료', 'blocked': '차단'}
APPROVAL = {'approved': '승인', 'pending': '승인 대기', 'proposal': '개선 제안', 'not_required': '기획/기반'}
NEXT = {'planned': {'in_progress', 'blocked'}, 'in_progress': {'planned', 'verifying', 'blocked'},
        'verifying': {'in_progress', 'done', 'blocked'}, 'done': {'in_progress'},
        'blocked': {'planned', 'in_progress'}}


def load(path):
    """중복 ID와 잘못된 상태를 쓰기 전에 거부한다."""
    state = json.loads(path.read_text())
    if state.get('schema_version') != 1 or state.get('repository') != 'hddy774/project-hora':
        raise ValueError('지원하지 않는 진행표 형식 또는 저장소입니다.')
    tasks = state['tasks']
    ids = [task['id'] for task in tasks]
    if len(set(ids)) != len(ids):
        raise ValueError('작업 ID가 중복됐습니다.')
    for task in tasks:
        if task['status'] not in STATUS or task['approval'] not in APPROVAL:
            raise ValueError('지원하지 않는 상태 또는 승인 값입니다.')
        if not set(task['depends_on']).issubset(ids):
            raise ValueError('없는 선행 작업을 참조합니다.')
    return state


def cell(value):
    """사용자 메모로 표의 열이나 줄이 늘어나지 않게 한다."""
    return str(value).replace('\n', ' ').replace('\r', ' ').replace('|', '\\|').replace('<', '&lt;')


def render(state):
    """문서 완료와 게임 구현 완료를 따로 집계한다."""
    work = [task for task in state['tasks'] if task['phase'] not in {'baseline', 'planning'}]
    done = sum(task['status'] == 'done' for task in work)
    lines = [START, '## 개발 순서·진행 체크표', '',
             f"최종 갱신: **{state['updated_at']}** · 신규 개발/검증 **{done}/{len(work)} 완료**.",
             '기존 배포·문서·검토 완료는 신규 개발률에서 제외한다. 아래 순서는 권장 통합 순서다.', '',
             '| 순서 | ID · 작업 | 승인 | 상태 | 개발 중 | 검증 중 | 완료 | 근거·메모 |',
             '|---|---|---|---|---|---|---|---|']
    for task in sorted(state['tasks'], key=lambda item: item['order']):
        flags = ['☑' if task['status'] == status else '☐' for status in ('in_progress', 'verifying', 'done')]
        label = STATUS[task['status']]
        if task['phase'] == 'planning' and task['status'] == 'in_progress':
            label, flags[0] = '기획 중', '—'
        evidence = ' · '.join(filter(None, [task['evidence'], task['note']])) or '—'
        lines.append('| ' + ' | '.join(map(cell, [task['order'] or '기반/기획',
                     task['id'] + ' · ' + task['title'], APPROVAL[task['approval']], label,
                     *flags, evidence])) + ' |')
    lines += ['', '### 완료 체크', '']
    for task in work:
        lines.append(f"- [{'x' if task['status'] == 'done' else ' '}] {task['id']} · {task['title']}")
    lines += ['', '상태 원본: `docs/tasks/xianxia-progress.json`. 착수 → 개발 중 → 검증 중 → 완료로 갱신한다.',
              '기획 요청이나 테마 선택은 코드 작업 승인을 뜻하지 않는다. 완료에는 수용 기준·검사 근거가 필요하다.',
              '상태 변경 명령의 `--publish`가 이 영역을 즉시 반영한다. PR 체크만 수동 변경하면 다음 동기화 때 원본 값이 반영된다.',
              'XO-09 완료와 ARM64 실기기 확인·출시 승인은 별도다. 출시 전에 실기기 결과와 D-07·D-08을 확인한다.', END]
    return '\n'.join(lines)


def change(state, task_id, status, note=None, evidence=None):
    """승인·선행·완료 근거를 확인한 뒤 새 상태를 만든다."""
    result = copy.deepcopy(state)
    tasks = {task['id']: task for task in result['tasks']}
    if task_id not in tasks:
        raise ValueError('없는 작업 ID입니다: ' + task_id)
    task = tasks[task_id]
    if status != task['status'] and status not in NEXT[task['status']]:
        raise ValueError('허용하지 않는 상태 전환입니다. 개발 중 → 검증 중 → 완료 순서를 사용하세요.')
    if status in {'in_progress', 'verifying', 'done'}:
        if task['approval'] not in {'approved', 'not_required'}:
            raise ValueError('승인되지 않은 코드 작업입니다. 명세와 승인 근거를 먼저 갱신하세요.')
        if any(tasks[dependency]['status'] != 'done' for dependency in task['depends_on']):
            raise ValueError('선행 작업이 완료되지 않았습니다.')
    if note is not None:
        task['note'] = note
    if evidence is not None:
        task['evidence'] = evidence
    if status == 'done' and not task['evidence'].strip():
        raise ValueError('완료 근거 URL 또는 커밋이 필요합니다.')
    if status == 'blocked' and not task['note'].strip():
        raise ValueError('차단 이유가 필요합니다.')
    task['status'] = status
    result['updated_at'] = datetime.now(timezone.utc).isoformat(timespec='seconds').replace('+00:00', 'Z')
    return result


def merge_body(body, block):
    """표식 밖의 기존 PR 설명과 리뷰 기록은 그대로 둔다."""
    if body.count(START) == body.count(END) == 0:
        return body.rstrip() + '\n\n' + block + '\n'
    if body.count(START) != 1 or body.count(END) != 1 or body.index(END) < body.index(START):
        raise ValueError('PR 진행표 표식이 손상됐습니다. 본문을 덮어쓰지 않았습니다.')
    return body[:body.index(START)] + block + body[body.index(END) + len(END):]


def api(endpoint, payload=None):
    """기존 gh 인증을 사용하고 JSON을 stdin으로 전달한다."""
    command = ['gh', 'api', endpoint]
    if payload is not None:
        command += ['--method', 'PATCH', '--input', '-']
    result = subprocess.run(command, input=None if payload is None else json.dumps(payload),
                            capture_output=True, text=True, check=True)
    return json.loads(result.stdout)


def publish(state, pr=None):
    """최신 본문을 읽고 변경 충돌을 확인한 뒤 지정 영역을 바꾼다."""
    number = pr if pr is not None else state['tracking_pr']
    if not isinstance(number, int) or number < 1:
        raise ValueError('PR 번호는 양의 정수여야 합니다.')
    endpoint = f"repos/{state['repository']}/pulls/{number}"
    current = api(endpoint)
    original = current.get('body') or ''
    updated = merge_body(original, render(state))
    if (api(endpoint).get('body') or '') != original:
        raise ValueError('다른 작성자가 본문을 갱신했습니다. 최신 상태에서 다시 실행하세요.')
    response = api(endpoint, {'body': updated})
    if response.get('body') != updated:
        raise ValueError('게시 응답이 예상과 다릅니다. PR 본문을 확인하세요.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['show', 'set', 'publish'])
    parser.add_argument('task', nargs='?')
    parser.add_argument('--status', choices=STATUS)
    parser.add_argument('--note')
    parser.add_argument('--evidence')
    parser.add_argument('--publish', action='store_true')
    parser.add_argument('--pr', type=int)
    parser.add_argument('--file', type=Path, default=STATE)
    args = parser.parse_args()
    try:
        state = load(args.file)
        if args.command == 'set':
            if not args.task or not args.status:
                parser.error('set에는 작업 ID와 --status가 필요합니다.')
            state = change(state, args.task, args.status, args.note, args.evidence)
        if args.command == 'publish' or args.publish:
            publish(state, args.pr)
        if args.command == 'set':
            temporary = args.file.with_suffix('.json.tmp')
            temporary.write_text(json.dumps(state, ensure_ascii=False, indent=2) + '\n')
            temporary.replace(args.file)
        print(render(state))
    except (ValueError, KeyError, OSError, subprocess.CalledProcessError) as error:
        parser.exit(1, '진행표 갱신 실패: ' + str(error) + '\n')


if __name__ == '__main__':
    main()
