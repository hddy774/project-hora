#!/usr/bin/env python3
"""Validate the PC 0.2.0 dependency ledger and render its PR progress section."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LEDGER = ROOT / 'docs/plans/pc-v020-tasks.json'
STATES = {'planned': '대기', 'in_progress': '진행 중', 'verifying': '검토',
          'blocked': '차단', 'done': '완료'}
START = '<!-- pc-v020-progress:start -->'
END = '<!-- pc-v020-progress:end -->'


def load():
    data = json.loads(LEDGER.read_text(encoding='utf-8'))
    tasks = data['tasks']
    by_id = {task['id']: task for task in tasks}
    assert len(tasks) == len(by_id) == 55, 'Expected 55 unique tasks'
    assert data['release'] == '0.2.0' and data['tracking_pr'] == 2
    assert data['automatic_deferral_to_030'] is False
    for task in tasks:
        assert task['release'] == '0.2.0', task['id']
        assert task['status'] in STATES, task['id']
        assert task['owner'] and task['acceptance'], task['id']
        assert all(dep in by_id for dep in task['depends_on']), task['id']
        assert task['id'] not in task['depends_on'], task['id']
        if task['status'] == 'done':
            assert task['evidence'] and task['verification_sha'], task['id']
            assert all(by_id[dep]['status'] == 'done' for dep in task['depends_on']), task['id']
    remaining = set(by_id)
    complete = set()
    waves = []
    while remaining:
        ready = sorted(key for key in remaining if set(by_id[key]['depends_on']) <= complete)
        assert ready, 'Dependency cycle'
        waves.append(ready)
        complete.update(ready)
        remaining.difference_update(ready)
    return tasks, waves


def render(tasks):
    done = sum(task['status'] == 'done' for task in tasks)
    lines = [START, '## 0.2.0 구현 진행', '',
             f'**{done}/{len(tasks)} 완료** · 기획 반영과 구현 완료는 별도로 집계합니다.', '',
             '| 작업 | 개발 중 | 검증 중 | 완료 | 상태 | 서브 PR | 검증 SHA |',
             '|---|:---:|:---:|:---:|---|---|---|']
    for task in tasks:
        marks = ['☑' if task['status'] == state else '☐'
                 for state in ('in_progress', 'verifying', 'done')]
        sub = f"#{task['sub_pr']}" if task['sub_pr'] else '—'
        sha = task['verification_sha'][:12] or '—'
        lines.append('| ' + ' | '.join([task['id'], *marks, STATES[task['status']], sub, sha]) + ' |')
    lines.extend(['', '상태 원본: `docs/plans/pc-v020-tasks.json` · 독립 서브 PR 여러 개를 병렬 운영합니다.', END])
    return '\n'.join(lines)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['validate', 'render', 'waves'])
    args = parser.parse_args()
    tasks, waves = load()
    if args.command == 'render':
        print(render(tasks))
    elif args.command == 'waves':
        for index, wave in enumerate(waves, 1):
            print(f"{index:02}: {', '.join(wave)}")
    else:
        print(f'{len(tasks)} tasks; missing dependencies 0; cycles 0; {len(waves)} waves')
