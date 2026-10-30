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


def replace_block(body, block):
    if START in body or END in body:
        if body.count(START) != 1 or body.count(END) != 1 or body.index(START) > body.index(END):
            raise ValueError("진행도 구간 표시가 손상되어 기존 본문을 보존합니다.")
        return re.sub(re.escape(START) + r"[\s\S]*?" + re.escape(END), lambda _: block.strip(), body, count=1)
    return block + "\n" + body


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
    parser.add_argument("--output", default="docs/progress-v0.2.1.md")
    parser.add_argument("--json-output", default="docs/evidence/0.2.1/progress.json")
    args = parser.parse_args()
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
            updated = replace_block(entity.get("body") or "", block)
            if updated != entity.get("body"):
                request("PATCH", path, token, {"body": updated})
    print(json.dumps({key: summary[key] for key in ("completed", "total", "completion_percent", "phase")}, ensure_ascii=False))


if __name__ == "__main__":
    main()
