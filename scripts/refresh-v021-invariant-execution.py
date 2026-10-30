#!/usr/bin/env python3
"""실제 불변식 매핑의 변경 검토 계획을 만들고, 검토한 기존 이름의 지문/행만 갱신한다."""

from __future__ import annotations

import argparse
import copy
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("invariant_execution", ROOT / "scripts/validate-v021-invariant-execution.py")
VALIDATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VALIDATOR)


def git(root, *args):
    return subprocess.check_output(["git", *args], cwd=root, text=True).strip()


def source_commit(root, value):
    VALIDATOR.require(bool(re.fullmatch(r"[0-9a-f]{7,40}", value)), "실제 소스의 Git SHA를 지정하세요.")
    return git(root, "rev-parse", "--verify", value + "^{commit}")


def review_plan(mapping, root, target):
    actual, fingerprint = VALIDATOR.catalog(root)
    VALIDATOR.require(VALIDATOR.committed_test_fingerprint(root, target) == fingerprint,
                      "작업트리 테스트가 지정한 소스와 다릅니다. 실제 최종 소스를 checkout한 뒤 검토하세요.")
    differing = git(root, "diff", "--name-only", target, "--", "src/Chronicles.Core", "src/Chronicles.Game", "src/Chronicles.Hosting", "tests/Chronicles.Game.Tests").splitlines()
    untracked = git(root, "ls-files", "--others", "--exclude-standard", "--", "src/Chronicles.Core", "src/Chronicles.Game", "src/Chronicles.Hosting", "tests/Chronicles.Game.Tests").splitlines()
    differing += [path for path in untracked if Path(path).suffix in {".cs", ".csproj", ".props", ".targets"}]
    VALIDATOR.require(not differing, "작업트리 제품/테스트가 대상 SHA와 다릅니다: " + json.dumps(differing, ensure_ascii=False))
    changed = git(root, "diff", "--name-only", mapping["mapping_source_commit"], target, "--",
                  "src/Chronicles.Core", "src/Chronicles.Game", "src/Chronicles.Hosting",
                  "tests/Chronicles.Game.Tests", "Directory.Build.props", "Directory.Build.targets", "global.json").splitlines()
    changed = sorted(path for path in changed if Path(path).suffix in {".cs", ".csproj", ".props", ".targets"} or path == "global.json")
    old = {row["name"]: row for row in mapping["scenarios"]}
    current = {row["name"]: row for row in actual}
    path_reviews = []
    for path in changed:
        names = sorted(name for name, row in current.items() if row["source"]["path"] == path)
        witnesses = []
        invs = set()
        topics = set()
        for name, row in old.items():
            if row["source"]["path"] == path:
                invs.update(row["invariants"])
        for inv in mapping["invariants"]:
            for witness in inv["witnesses"]:
                related = [old.get(witness["scenario"], {}).get("source", {}).get("path")]
                related += witness.get("supporting_sources", [])
                related += [check["path"] for check in witness.get("supporting_checks", [])]
                if path in related:
                    invs.add(inv["id"]); witnesses.append(witness["scenario"])
        for area in mapping.get("source_review_areas", []):
            if path.startswith(area["path_prefix"]):
                invs.update(area["invariants"]); topics.add(area["focus"])
        if path.startswith("tests/Chronicles.Game.Tests/") and not names:
            topics.add("등록 외 helper/fixture/계측: 실제 호출·입력·명령/보고서 형식 변경을 검토. 실측 통과로 간주하지 않음.")
        path_reviews.append({"path": path, "invariants": sorted(invs), "registered_scenarios": names,
                             "unclassified_product_source": path.startswith("src/") and not invs and not topics,
                             "named_witnesses": sorted(set(witnesses)), "review_focus": sorted(topics)})
    return {"schema": "seonro.invariant-map-review-plan.v1", "from_source": mapping["mapping_source_commit"],
            "source_commit": target, "test_sources_sha256": fingerprint, "actual_scenarios": len(actual),
            "added_names": sorted(set(current) - set(old)), "removed_names": sorted(set(old) - set(current)),
            "changed_source_paths": path_reviews,
            "rule": "새/삭제 이름과 의미/경계/앵커는 수동 검토한다. 이 helper는 승인된 기존 연결의 지문·행 갱신만 한다."}, actual


def validate_review(plan, acknowledgement):
    VALIDATOR.require(acknowledgement.get("source_commit") == plan["source_commit"], "검토 출처가 이번 대상 SHA와 다릅니다.")
    VALIDATOR.require(acknowledgement.get("scenario_bindings_reviewed") is True and bool(acknowledgement.get("review_note", "").strip()),
                      "실제 이름·관련 절·부정/저장/명령 범위를 검토한 메모가 필요합니다.")
    expected = {row["path"] for row in plan["changed_source_paths"]}
    provided = acknowledgement.get("reviewed_paths", [])
    VALIDATOR.require(isinstance(provided, list) and all(isinstance(path, str) for path in provided) and
                      len(provided) == len(set(provided)) and set(provided) == expected,
                      "변경 소스 검토 경로가 정확히 일치하지 않습니다. 누락/이전 SHA 검토를 재사용할 수 없습니다.")
    VALIDATOR.require(not plan["added_names"] and not plan["removed_names"],
                      "새/삭제 실제 검사 이름이 있습니다. 관련 INV/경계/증인·앵커를 먼저 수동 갱신하세요.")
    VALIDATOR.require(not any(row.get("unclassified_product_source") for row in plan["changed_source_paths"]),
                      "아직 관련 INV/검토 초점을 분류하지 않은 제품 소스가 있습니다. source_review_areas를 먼저 검토하세요.")


def refreshed(mapping, actual, plan, acknowledgement, root):
    validate_review(plan, acknowledgement)
    result = copy.deepcopy(mapping)
    by_name = {row["name"]: row for row in actual}
    for row in result["scenarios"]:
        row["source"] = by_name[row["name"]]["source"]
    result["mapping_source_commit"] = plan["source_commit"]
    result["test_sources_sha256"] = plan["test_sources_sha256"]
    features = sum(row["source"]["symbol"] != "Program.Test" and not row["source"]["symbol"].startswith("ReincarnationScenarios.Run(") for row in actual)
    result["catalog"] = {"feature_scenarios": features, "program_scenarios": len(actual) - features, "total": len(actual)}
    result["last_source_review"] = acknowledgement
    VALIDATOR.validate_map(result, root)
    for invariant in result["invariants"]:
        for witness in invariant["witnesses"]:
            for check in witness.get("supporting_checks", []):
                path = root / check["path"]
                VALIDATOR.require(root.resolve() in path.resolve().parents and path.is_file(), "저장소 안 실제 보조 소스가 필요합니다.")
                text = path.read_text(encoding="utf-8")
                for anchor in check["anchors"]:
                    VALIDATOR.require(anchor in text, "보조 실제 검사에 없는 앵커: " + check["path"] + ": " + anchor)
    return result


def refreshed_markdown(text, mapping):
    metadata = ("<!-- actual-map-metadata:begin -->\n"
                + "매핑 소스: `" + mapping["mapping_source_commit"] + "`  \n"
                + "테스트 소스 지문: `" + mapping["test_sources_sha256"] + "`\n"
                + "<!-- actual-map-metadata:end -->")
    text = re.sub(r"<!-- actual-map-metadata:begin -->.*?<!-- actual-map-metadata:end -->", lambda _: metadata, text, flags=re.S)
    for row in mapping["scenarios"]:
        label = row["name"].replace("|", "\\|")
        pattern = r"\[" + re.escape(label) + r"\]\(\.\./\.\./\.\./[^)]+#L\d+\)"
        replacement = "[" + label + "](../../../" + row["source"]["path"] + "#L" + str(row["source"]["line"]) + ")"
        text = re.sub(pattern, lambda _, value=replacement: value, text)
    return text


def self_test():
    plan = {"source_commit": "a" * 40, "added_names": [], "removed_names": [], "changed_source_paths": [{"path": "test.cs"}]}
    valid = {"source_commit": "a" * 40, "scenario_bindings_reviewed": True, "review_note": "실제 검사 범위 검토", "reviewed_paths": ["test.cs"]}
    validate_review(plan, valid); rejected = 0
    for change in [lambda row: row.update(source_commit="b" * 40), lambda row: row.update(scenario_bindings_reviewed="true"),
                   lambda row: row.update(review_note=""), lambda row: row.update(reviewed_paths=[]),
                   lambda row: row.update(reviewed_paths=["test.cs", "test.cs"]), lambda row: row.update(reviewed_paths=["other.cs"])]:
        candidate = copy.deepcopy(valid); change(candidate)
        try:
            validate_review(plan, candidate)
        except VALIDATOR.ValidationError:
            rejected += 1
        else:
            raise VALIDATOR.ValidationError("검토되지 않은 지문 갱신을 허용했습니다.")
    for field in ("added_names", "removed_names"):
        candidate = copy.deepcopy(plan); candidate[field] = ["검토하지 않은 새/삭제 검사"]
        try:
            validate_review(candidate, valid)
        except VALIDATOR.ValidationError:
            rejected += 1
        else:
            raise VALIDATOR.ValidationError("실제 이름 추가/삭제를 자동 승인했습니다.")
    candidate = copy.deepcopy(plan); candidate["changed_source_paths"][0]["unclassified_product_source"] = True
    try:
        validate_review(candidate, valid)
    except VALIDATOR.ValidationError:
        rejected += 1
    else:
        raise VALIDATOR.ValidationError("미분류 제품 소스 변경을 자동 승인했습니다.")
    markdown = "의미를 자동 변경하지 않음\n[실제 검사](../../../old.cs#L1)"
    updated = refreshed_markdown(markdown, {"mapping_source_commit": "a" * 40, "test_sources_sha256": "b" * 64,
                                          "scenarios": [{"name": "실제 검사", "source": {"path": "new.cs", "line": 8}}]})
    VALIDATOR.require(updated == "의미를 자동 변경하지 않음\n[실제 검사](../../../new.cs#L8)", "문서의 실제 소스 링크 외 의미를 자동 변경했습니다.")
    return {"refresh_helper_self_test_only": True, "valid_review": 1, "rejected_unreviewed_updates": rejected,
            "markdown_link_update_preserved_prose": True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", help="실제 검토할 Git SHA")
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--map", type=Path, default=VALIDATOR.DEFAULT_MAP)
    parser.add_argument("--plan", type=Path)
    parser.add_argument("--reviewed", type=Path)
    parser.add_argument("--markdown", type=Path, help="기본은 map과 같은 이름의 .md. 출처 metadata/실제 이름 소스 링크만 갱신한다.")
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    try:
        if args.self_test:
            print(json.dumps(self_test(), ensure_ascii=False, indent=2)); return 0
        VALIDATOR.require(args.source is not None, "--source에 검토 대상 SHA를 지정하세요.")
        mapping = VALIDATOR.read_json(args.map)
        plan, actual = review_plan(mapping, args.root, source_commit(args.root, args.source))
        if args.plan:
            args.plan.write_text(json.dumps(plan, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        if args.apply:
            VALIDATOR.require(args.reviewed is not None, "--apply에는 변경 소스 검토 기록 --reviewed가 필요합니다.")
            result = refreshed(mapping, actual, plan, VALIDATOR.read_json(args.reviewed), args.root)
            markdown = args.markdown or args.map.with_suffix(".md")
            updated_markdown = refreshed_markdown(markdown.read_text(encoding="utf-8"), result) if markdown.is_file() else None
            pending = args.map.with_suffix(args.map.suffix + ".pending")
            pending.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"); pending.replace(args.map)
            if updated_markdown is not None:
                pending_markdown = markdown.with_suffix(markdown.suffix + ".pending")
                pending_markdown.write_text(updated_markdown, encoding="utf-8"); pending_markdown.replace(markdown)
            print(json.dumps({"map_refreshed": True, "source_commit": plan["source_commit"],
                              "actual_scenarios": len(actual), "markdown_links_refreshed": updated_markdown is not None,
                              "product_pass_claimed": False}, ensure_ascii=False, indent=2))
        else:
            print(json.dumps(plan, ensure_ascii=False, indent=2))
        return 0
    except (VALIDATOR.ValidationError, OSError, ValueError, KeyError, TypeError, subprocess.CalledProcessError) as error:
        print(json.dumps({"refresh_rejected": True, "error": str(error)}, ensure_ascii=False), file=sys.stderr); return 1


if __name__ == "__main__":
    sys.exit(main())
