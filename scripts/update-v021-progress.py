#!/usr/bin/env python3
"""정본 작업 원장으로 0.2.1 진행도를 생성하고 지정된 GitHub 본문 구간만 갱신한다."""
import argparse
from collections import Counter
import json
import os
from pathlib import Path
import re
import subprocess
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
REPOSITORY = "hddy774/project-hora"
START = "<!-- v021-progress:start -->"
END = "<!-- v021-progress:end -->"
LEGACY_START = "<!-- v021-live-measurement:start -->"
LEGACY_END = "<!-- v021-live-measurement:end -->"
STATUS_NAMES = {"done": "완료", "validated": "자체 검증 완료·선행 마감 대기", "in_progress": "진행 중", "planned": "예정", "blocked": "차단"}


def progress(data):
    tasks = data["tasks"]
    if len(tasks) != 111 or len({task["id"] for task in tasks}) != 111:
        raise ValueError("진행도는 정본의 서로 다른 111개 작업으로 계산해야 합니다.")
    counts = Counter(task["status"] for task in tasks)
    if set(counts) - STATUS_NAMES.keys():
        raise ValueError("알 수 없는 작업 상태입니다.")
    completed = counts["done"]
    return {"total": len(tasks), "completed": completed, "completion_percent": round(completed * 100 / len(tasks), 1),
            "own_acceptance_count": completed + counts["validated"],
            "own_acceptance_percent": round((completed + counts["validated"]) * 100 / len(tasks), 1),
            "counts": {key: counts[key] for key in STATUS_NAMES},
            "remaining": [{"id": task["id"], "title": task["title"], "status": task["status"]}
                          for task in tasks if task["status"] != "done"]}


def render(summary, phase, commit):
    percent = summary["completion_percent"]
    cells = min(20, summary["completed"] * 20 // summary["total"])
    lines = [START, "**선로만상록 0.2.1 전체 진행률: " + f'{percent:.1f}% ({summary["completed"]}/{summary["total"]}개 완료)**',
             "", "`" + "█" * cells + "░" * (20 - cells) + "`", "",
             "완료율은 정본 작업의 수용 근거와 모든 선행 작업이 확인된 `완료`만 계산합니다. 진행 중인 작업에 임의의 부분 점수를 부여하지 않습니다.",
             "", f'자체 수용 근거 확보: {summary["own_acceptance_count"]}/{summary["total"]}개 ({summary["own_acceptance_percent"]:.1f}%). '
             "선행 대기와 최종 전체 회귀·측정·릴리스는 구분합니다.",
             "", "| 상태 | 작업 수 |", "| --- | ---: |"]
    lines.extend(f'| {STATUS_NAMES[key]} | {summary["counts"][key]} |' for key in STATUS_NAMES)
    lines.extend(["", "**현재 단계:** " + phase["phase"], ""])
    lines.extend("- " + item for item in phase["current_work"])
    lines.extend(["", "**다음 단계:** " + phase["next_phase"], "",
                  "성능·성장·장기 안정성 검사는 전체 기능 개발 이후에 진행합니다.", "",
                  f'기준 소스: [{commit[:8]}](https://github.com/{REPOSITORY}/commit/{commit}) · '
                  f'[작업 원장](https://github.com/{REPOSITORY}/blob/{commit}/docs/plans/v021-tasks.json) · '
                  f'[현재 진행도](https://github.com/{REPOSITORY}/blob/codex/v0.2.1/docs/progress-v0.2.1.md)', END])
    return "\n".join(lines) + "\n"


def render_summary(summary, phase, commit):
    """GitHub에는 대표 진행률과 현재 판단에 필요한 세 줄만 게시한다."""
    remaining = phase.get("remaining_work", phase["phase"])
    blocker = phase.get("blocked_by", "최종 수용 근거 확인 중")
    next_action = phase.get("next_action", phase["next_phase"])
    lines = [START,
             f'**전체 {summary["completion_percent"]:.1f}% ({summary["completed"]}/{summary["total"]}개 완료)** · 남은 항목: {remaining}',
             f'**막힌 것:** {blocker}',
             f'**다음 행동:** {next_action}', "",
             f'[상세 진행도](https://github.com/{REPOSITORY}/blob/codex/v0.2.1/docs/progress-v0.2.1.md) · '
             f'[리뷰 안내](https://github.com/{REPOSITORY}/blob/codex/v0.2.1/docs/reviews/v0.2.1-review-guide.md) · '
             f'[기준 소스 {commit[:8]}](https://github.com/{REPOSITORY}/commit/{commit})', END]
    return "\n".join(lines) + "\n"


def replace_block(body, block):
    """알려진 자동 영역만 교체하고 잘못된 표시가 있으면 원문을 보존한다."""
    for begin, end in ((START, END), (LEGACY_START, LEGACY_END)):
        if begin in body or end in body:
            if body.count(begin) != 1 or body.count(end) != 1 or body.index(begin) > body.index(end):
                raise ValueError("진행도 구간 표시가 손상되어 기존 본문을 보존합니다.")
    if LEGACY_START in body:
        first = body.index(LEGACY_START)
        last = body.index(LEGACY_END) + len(LEGACY_END)
        body = body[:first] + body[last:]
    if START in body:
        return re.sub(re.escape(START) + r"[\s\S]*?" + re.escape(END), lambda _: block.strip(), body, count=1)
    return block + "\n" + body


def self_test():
    payload = START + "\nNEW\n" + END
    original = "사용자 설명 앞\n" + START + "\nOLD\n" + END + "\n검증 링크 뒤"
    expected = "사용자 설명 앞\n" + payload + "\n검증 링크 뒤"
    assert replace_block(original, payload) == expected
    mixed = original + "\n" + LEGACY_START + "\nOLD MEASUREMENTS\n" + LEGACY_END
    migrated = replace_block(mixed, payload)
    assert migrated == expected + "\n" and migrated.count(START) == 1 and LEGACY_START not in migrated
    assert replace_block("본문 그대로", payload) == payload + "\n본문 그대로"
    for malformed in ("앞" + START, END + START, START + END + START + END,
                      LEGACY_START, LEGACY_END + LEGACY_START,
                      LEGACY_START + LEGACY_END + LEGACY_START + LEGACY_END):
        try:
            replace_block(malformed, payload)
        except ValueError:
            pass
        else:
            raise AssertionError("잘못된 자동 영역을 덮어썼습니다.")
    return {"body_preservation_and_marker_checks": 9, "external_writes": 0}


def domain_body(body, tasks):
    states = {task["id"]: task["status"] for task in tasks}
    def checkbox(match):
        identifier = match.group(1)
        return "- [" + ("x" if states[identifier] == "done" else " ") + "] " + identifier + ":" if identifier in states else match.group(0)
    body = re.sub(r"- \[[ xX]\] (V021-[A-Z]\d\d):", checkbox, body)
    begin, end = "<!-- v021-domain-progress:start -->", "<!-- v021-domain-progress:end -->"
    completed = sum(task["status"] == "done" for task in tasks)
    block = (begin + f'\n**분야 진행률: {completed}/{len(tasks)}개 완료 ({completed * 100 / len(tasks):.1f}%)**\n\n'
             + "수용 근거와 선행 작업의 마감을 확인한 정본 상태입니다. "
             + f'[전체 진행률](https://github.com/{REPOSITORY}/issues/73)에서 현재 단계와 남은 작업을 확인할 수 있습니다.\n' + end)
    if begin in body or end in body:
        if body.count(begin) != 1 or body.count(end) != 1 or body.index(begin) > body.index(end):
            raise ValueError("분야 진행도 구간 표시가 손상되어 기존 본문을 보존합니다.")
        return re.sub(re.escape(begin) + r"[\s\S]*?" + re.escape(end), lambda _: block, body, count=1)
    return block + "\n\n" + body


def request(method, path, token, payload=None):
    data = None if payload is None else json.dumps(payload, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request("https://api.github.com/repos/" + REPOSITORY + path, data=data, method=method,
                                 headers={"Authorization": "Bearer " + token, "Accept": "application/vnd.github+json",
                                          "Content-Type": "application/json", "X-GitHub-Api-Version": "2022-11-28"})
    with urllib.request.urlopen(req, timeout=30) as response:
        return json.load(response)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--publish-github", action="store_true")
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--output", default="docs/progress-v0.2.1.md")
    parser.add_argument("--json-output", default="docs/evidence/0.2.1/progress.json")
    args = parser.parse_args()
    if args.self_test:
        if args.publish_github:
            raise ValueError("자체 검사는 외부 게시와 함께 실행할 수 없습니다.")
        print(json.dumps(self_test(), ensure_ascii=False))
        return
    data = json.loads((ROOT / "docs/plans/v021-tasks.json").read_text(encoding="utf-8"))
    phase = json.loads((ROOT / "docs/evidence/0.2.1/work-status.json").read_text(encoding="utf-8"))
    commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    summary = progress(data)
    summary.update(schema_version=1, source_commit=commit, phase=phase["phase"], calculation="수용 근거와 모든 선행 작업이 확인된 완료 작업 수 / 정본111개")
    block = render(summary, phase, commit)
    for target, content in ((args.output, block), (args.json_output, json.dumps(summary, ensure_ascii=False, indent=2) + "\n")):
        destination = ROOT / target
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(content, encoding="utf-8")
    if args.publish_github:
        if os.environ.get("GITHUB_ACTIONS") != "true" or os.environ.get("GITHUB_REPOSITORY") != REPOSITORY:
            raise ValueError("지정 저장소의 자동화에서만 GitHub 진행도를 게시합니다.")
        token = os.environ["GH_TOKEN"]
        for path in ("/issues/73", "/pulls/84"):
            entity = request("GET", path, token)
            updated = replace_block(entity.get("body") or "", render_summary(summary, phase, commit))
            if updated != entity.get("body"):
                request("PATCH", path, token, {"body": updated})
        for number in range(74, 84):
            tasks = [task for task in data["tasks"] if task.get("github_issue") == number]
            if not tasks:
                continue
            path = "/issues/" + str(number)
            entity = request("GET", path, token)
            updated = domain_body(entity.get("body") or "", tasks)
            if updated != entity.get("body"):
                request("PATCH", path, token, {"body": updated})
    print(json.dumps({key: summary[key] for key in ("completed", "total", "completion_percent", "phase")}, ensure_ascii=False))


if __name__ == "__main__":
    main()
