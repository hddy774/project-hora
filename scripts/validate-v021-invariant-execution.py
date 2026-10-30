#!/usr/bin/env python3
"""실제 C# 등록 이름·불변식 매핑·원본 전체 기능 보고서의 일치를 검사한다."""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_MAP = ROOT / "docs/evidence/0.2.1/invariant-execution-map.json"
TEST_ROOT = "tests/Chronicles.Game.Tests/"
INVARIANTS = {f"INV-{number:03d}" for number in range(1, 25)}
BOUNDARIES = {"public_world", "module", "module_and_world", "mechanics", "host"}
CHECK_KINDS = {"positive", "negative", "save_resume", "public_command", "query_purity", "determinism", "metadata"}
LITERAL = r'"((?:[^"\\]|\\.)*)"'


class ValidationError(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise ValidationError(message)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, f"중복 JSON 필드: {key}")
        result[key] = value
    return result


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=unique_object)


def source_files(root):
    return {p.relative_to(root).as_posix(): p.read_bytes()
            for p in (root / TEST_ROOT).rglob("*")
            if p.is_file() and not {"obj", "bin"}.intersection(p.relative_to(root).parts)
            and p.suffix in {".cs", ".csproj"}}


def fingerprint(files):
    digest = hashlib.sha256()
    for path, data in sorted(files.items()):
        digest.update(path.encode("utf-8") + b"\0" + data + b"\0")
    return digest.hexdigest()


def committed_test_fingerprint(root, revision):
    require(bool(re.fullmatch(r"[0-9a-f]{40}", revision)), "실행 출처는 40자리 Git SHA여야 합니다.")
    paths = subprocess.check_output(["git", "ls-tree", "-r", "--name-only", revision, "--", TEST_ROOT], cwd=root, text=True).splitlines()
    files = {path: subprocess.check_output(["git", "show", f"{revision}:{path}"], cwd=root)
             for path in paths if Path(path).suffix in {".cs", ".csproj"} and not {"obj", "bin"}.intersection(Path(path).parts)}
    require(bool(files), "실행 출처에서 실제 테스트 소스를 찾지 못했습니다.")
    return fingerprint(files)


def catalog(root):
    """현재 runner의 정적 이름·명시적 13경로 생성만 지원한다. 새 형식은 조용히 생략하지 않는다."""
    entries = []
    files = source_files(root)
    for path, data in sorted(files.items()):
        if not path.endswith(".cs"):
            continue
        text = data.decode("utf-8-sig")
        classes = list(re.finditer(r"(?:public|internal)\s+(?:sealed\s+)?class\s+(\w+)\s*:\s*IFeatureScenario\b", text))
        implementations = len(re.findall(r":\s*IFeatureScenario\b", text))
        require(implementations == len(classes), f"지원하지 않는 시나리오 선언 형식: {path}")
        for match in classes:
            following = re.search(r"\n(?:public|internal)\s+(?:static\s+|sealed\s+)?class\s+", text[match.end():])
            end = match.end() + following.start() if following else len(text)
            body = text[match.start():end]
            names = list(re.finditer(r"public\s+string\s+Name\s*=>\s*" + LITERAL + r"\s*;", body))
            require(len(names) == 1, f"정적 Name을 정확히 하나 요구합니다: {path}:{match.group(1)}")
            name = json.loads('"' + names[0].group(1) + '"')
            entries.append({"name": name, "source": {"path": path, "symbol": match.group(1),
                            "line": text.count("\n", 0, match.start()) + 1}, "_body": body})

    path = TEST_ROOT + "Program.cs"
    text = files[path].decode("utf-8-sig")
    direct = list(re.finditer(r"(?m)^Test\(" + LITERAL + r"\s*,", text))
    require(len(re.findall(r"\bTest\(", text)) == len(direct) + 3,
            "Program.Test의 새 동적 등록 형식입니다. 카탈로그 추출기를 검토하세요.")
    for index, match in enumerate(direct):
        end = direct[index + 1].start() if index + 1 < len(direct) else text.index("foreach (var feature")
        name = json.loads('"' + match.group(1) + '"')
        entries.append({"name": name, "source": {"path": path, "symbol": "Program.Test",
                        "line": text.count("\n", 0, match.start()) + 1}, "_body": text[match.start():end]})
    route_registration = 'Test($"RC-{route:00} 실제 원인 생산자·자동 Gate·여정 왕복", () => ReincarnationScenarios.Run(route));'
    require("foreach (var route in Enumerable.Range(1, 13))" in text and route_registration in text,
            "실제 윤회 경로 등록을 추정하지 않습니다. 변경된 등록을 검토하세요.")
    route_path = TEST_ROOT + "ReincarnationScenarios.cs"
    route_body = files[route_path].decode("utf-8-sig")
    route_line = route_body.count("\n", 0, route_body.index("public static void Run")) + 1
    for route in range(1, 14):
        entries.append({"name": f"RC-{route:02d} 실제 원인 생산자·자동 Gate·여정 왕복",
                        "source": {"path": route_path, "symbol": f"ReincarnationScenarios.Run({route})", "line": route_line},
                        "_body": route_body})
    names = [entry["name"] for entry in entries]
    require(len(names) == len(set(names)), "실제 runner에 중복 시나리오 이름이 있습니다.")
    return sorted(entries, key=lambda row: row["name"]), fingerprint(files)


def validate_map(mapping, root):
    require(mapping.get("schema") == "seonro.actual-invariant-execution-map.v1", "실제 실행 매핑 schema가 아닙니다.")
    require(mapping.get("execution_status") == "not_asserted", "실행 매핑 자체를 통과 결과로 바꿀 수 없습니다.")
    require(bool(re.fullmatch(r"[0-9a-f]{40}", mapping.get("mapping_source_commit", ""))), "매핑 출처는 전체 Git SHA여야 합니다.")
    actual, actual_fingerprint = catalog(root)
    require(mapping.get("test_sources_sha256") == actual_fingerprint, "테스트 소스가 매핑 작성 시점과 다릅니다. 실제 검사와 매핑을 다시 검토하세요.")
    features = sum(row["source"]["symbol"] != "Program.Test" and not row["source"]["symbol"].startswith("ReincarnationScenarios.Run(") for row in actual)
    require(mapping.get("catalog") == {"feature_scenarios": features, "program_scenarios": len(actual) - features, "total": len(actual)},
            "카탈로그 집계가 실제 등록 수와 다릅니다.")
    entries = mapping.get("scenarios", [])
    names = [entry.get("name") for entry in entries]
    require(len(names) == len(set(names)), "매핑에 중복 시나리오 이름이 있습니다.")
    actual_by_name = {entry["name"]: entry for entry in actual}
    require(set(names) == set(actual_by_name), "매핑 이름 집합이 실제 등록과 다릅니다: " +
            json.dumps({"missing": sorted(set(actual_by_name) - set(names)), "unregistered": sorted(set(names) - set(actual_by_name))}, ensure_ascii=False))
    mapped = {entry["name"]: entry for entry in entries}
    for name, entry in mapped.items():
        require("passed" not in entry and "test_status" not in entry, f"매핑은 실행 통과를 저장하지 않습니다: {name}")
        require(entry.get("source") == actual_by_name[name]["source"], f"이름/소스 위치가 다릅니다: {name}")
        require(entry.get("boundary") in BOUNDARIES, f"실행 경계를 명시하세요: {name}")
        invs = entry.get("invariants", [])
        require(bool(invs) and len(invs) == len(set(invs)) and set(invs) <= INVARIANTS, f"유효한 관련 불변식을 요구합니다: {name}")
    invs = mapping.get("invariants", [])
    require(len(invs) == 24 and {row.get("id") for row in invs} == INVARIANTS, "24개 불변식을 정확히 한 번 매핑해야 합니다.")
    canonical = read_json(root / "docs/plans/v021-tasks.json")["invariants"]
    canonical_titles = {row["id"]: row["title"] for row in canonical}
    for invariant in invs:
        require(invariant.get("title") == canonical_titles[invariant["id"]], "불변식 정본 제목을 다른 요구로 바꿀 수 없습니다.")
        require("passed" not in invariant and "test_status" not in invariant, "불변식 매핑을 제품 수용 통과로 바꿀 수 없습니다.")
        require(bool(invariant.get("limits")), f"검사 범위를 명시하세요: {invariant['id']}")
        require(bool(invariant.get("witnesses")), f"실제 검사 증인을 요구합니다: {invariant['id']}")
        for witness in invariant["witnesses"]:
            name = witness.get("scenario")
            require(name in mapped and invariant["id"] in mapped[name]["invariants"], f"등록되지 않은 검사/불변식 연결: {name}")
            require(bool(witness.get("claim")) and bool(witness.get("kinds")) and set(witness["kinds"]) <= CHECK_KINDS,
                    f"부정·저장·명령 등 실제 검사 범위를 요구합니다: {name}")
            require(bool(witness.get("anchors")), f"실제 코드 호출/검사 메시지 앵커를 요구합니다: {name}")
            for anchor in witness["anchors"]:
                require(anchor in actual_by_name[name]["_body"], f"실제 검사 본문에 없는 앵커: {name}: {anchor}")
    return actual, actual_fingerprint


def validate_report(report, expected_names):
    require(report.get("selection") == "all", "선택 실행 보고서로 전체 기능 통과를 주장할 수 없습니다.")
    results = report.get("results")
    require(isinstance(results, list) and bool(results), "실제 결과 배열을 요구합니다.")
    names = [row.get("name") for row in results]
    require(all(isinstance(name, str) for name in names) and len(names) == len(set(names)), "보고서 이름이 비었거나 중복되었습니다.")
    require(type(report.get("total")) is int and report["total"] == len(results), "보고서 total과 실제 결과 수가 다릅니다.")
    require(all(type(row.get("passed")) is bool for row in results), "passed는 true/false 실제 bool이어야 합니다.")
    require(all(not row.get("error") for row in results if row["passed"]), "통과 행에 실패 오류를 함께 기록할 수 없습니다.")
    failed = [row["name"] for row in results if row["passed"] is not True]
    require(type(report.get("failed")) is int and report["failed"] == len(failed), "보고서 failed 집계가 실제 결과와 다릅니다.")
    require(not failed, "실패한 실제 검사: " + json.dumps(failed, ensure_ascii=False))
    require(set(names) == set(expected_names), "전체 보고서 이름 집합이 실제 등록/매핑과 다릅니다: " +
            json.dumps({"missing": sorted(set(expected_names) - set(names)), "unregistered": sorted(set(names) - set(expected_names))}, ensure_ascii=False))
    return {row["name"]: row for row in results}


def self_test(mapping, root):
    base = {"selection": "all", "total": 2, "failed": 0, "results": [{"name": "실제 A", "passed": True}, {"name": "실제 B", "passed": True}]}
    validate_report(base, {"실제 A", "실제 B"})
    rejected = 0
    mutations = [
        lambda d: d.update(selection="INV-"),
        lambda d: (d["results"][0].update(passed=False), d.update(failed=1)),
        lambda d: d["results"][0].update(passed=1),
        lambda d: d["results"][0].update(name="아직 구현하지 않은 제안 검사"),
        lambda d: (d["results"].pop(), d.update(total=1)),
        lambda d: d["results"][1].update(name="실제 A"),
        lambda d: d.update(total=20),
        lambda d: d.update(failed=1),
        lambda d: d["results"][0].update(error="실패인데 true로 표시"),
    ]
    for mutate in mutations:
        changed = copy.deepcopy(base)
        mutate(changed)
        try:
            validate_report(changed, {"실제 A", "실제 B"})
        except ValidationError:
            rejected += 1
        else:
            raise ValidationError("검증기 자체 시험이 잘못된 실행 주장을 허용했습니다.")
    validate_map(mapping, root)
    map_mutations = [
        lambda d: d.update(execution_status="passed"),
        lambda d: d["scenarios"][0].update(name="아직 구현하지 않은 제안 검사"),
        lambda d: d["scenarios"].pop(),
        lambda d: d["scenarios"][0]["source"].update(line=99999),
        lambda d: d["invariants"].pop(),
        lambda d: d["invariants"][0].update(test_status="passed"),
        lambda d: d["invariants"][0]["witnesses"][0].update(anchors=["존재하지 않는 실제 assertion"]),
        lambda d: d.update(test_sources_sha256="0" * 64),
    ]
    rejected_maps = 0
    for mutate in map_mutations:
        changed = copy.deepcopy(mapping)
        mutate(changed)
        try:
            validate_map(changed, root)
        except ValidationError:
            rejected_maps += 1
        else:
            raise ValidationError("검증기 자체 시험이 잘못된 매핑 주장을 허용했습니다.")
    try:
        json.loads('{"passed": true, "passed": false}', object_pairs_hook=unique_object)
    except ValidationError:
        rejected_duplicate_json = True
    else:
        raise ValidationError("중복 JSON 필드로 통과를 위조할 수 있습니다.")
    return {"validator_self_test_only": True, "accepted_synthetic_report": 1,
            "rejected_invalid_reports": rejected, "rejected_invalid_maps": rejected_maps,
            "rejected_duplicate_json": rejected_duplicate_json}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--map", type=Path, default=DEFAULT_MAP)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--executed-source", help="보고서를 실제 실행한 출처의 40자리 Git SHA. 호출자의 명시적 출처 선언이다.")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    try:
        mapping = read_json(args.map)
        result = self_test(mapping, args.root) if args.self_test else {}
        if not args.self_test or args.report:
            actual, test_fingerprint = validate_map(mapping, args.root)
            result.update(mapped_scenarios=len(actual), mapped_invariants=24, test_sources_sha256=test_fingerprint,
                          scope="실제 이름·소스·검사 범위의 연결. 정본 전체 절 또는 최신 제품 수용 판정이 아니다.")
            if args.report:
                require(args.executed_source is not None, "--report에는 실제 실행 출처 --executed-source가 필요합니다.")
                report = read_json(args.report)
                outcomes = validate_report(report, {row["name"] for row in actual})
                require(committed_test_fingerprint(args.root, args.executed_source) == test_fingerprint,
                        "선언한 실행 SHA의 테스트가 매핑과 다릅니다. 이전/다른 소스의 보고서를 재사용할 수 없습니다.")
                result.update(report_sha256=hashlib.sha256(args.report.read_bytes()).hexdigest(),
                              declared_executed_source=args.executed_source, source_attribution="호출자 선언·Git 테스트 소스 대조; 실행 진위는 원본/별도 provenance로 확인",
                              all_registered_scenarios_passed=len(outcomes),
                              invariant_witnesses=[{"id": inv["id"], "all_named_witnesses_passed": True,
                                                   "count": len(inv["witnesses"])} for inv in mapping["invariants"]])
        rendered = json.dumps(result, ensure_ascii=False, indent=2) + "\n"
        if args.output:
            args.output.write_text(rendered, encoding="utf-8")
        print(rendered, end="")
        return 0
    except (ValidationError, OSError, ValueError, KeyError, TypeError, subprocess.CalledProcessError) as error:
        print(json.dumps({"validation_rejected": True, "error": str(error)}, ensure_ascii=False), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
