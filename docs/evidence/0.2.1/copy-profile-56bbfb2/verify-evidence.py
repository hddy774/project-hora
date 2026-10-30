"""보존 자료의 해시·압축·정적 수치만 확인한다. 앱이나 측정을 실행하지 않는다."""
import gzip
import hashlib
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parent


def read(name):
    return json.loads((ROOT / name).read_text(encoding="utf-8"))


def need(condition, message):
    if not condition:
        raise ValueError(message)


index = read("evidence-sha256.json")
for row in index["artifacts"]:
    raw = (ROOT / row["file"]).read_bytes()
    need(len(raw) == row["bytes"] and hashlib.sha256(raw).hexdigest() == row["sha256"], row["file"])

manifest = read("artifact-manifest.json")
for row in manifest["artifacts"]:
    raw = (ROOT / row["file"]).read_bytes()
    need(len(raw) == row["bytes"] and hashlib.sha256(raw).hexdigest() == row["sha256"], row["file"])
    if row["encoding"] == "gzip":
        need(raw[4:8] == b"\0\0\0\0" and raw[3] & 8 == 0, "gzip 헤더")
        original = gzip.decompress(raw)
        need(len(original) == row["originalBytes"] and hashlib.sha256(original).hexdigest() == row["originalSha256"], "압축 해제 원본")
        json.loads(original)

left = read("copy-56bbfb2-json100.json")
right = read("copy-56bbfb2-typed100.json")
a, b = left["reports"][0], right["reports"][0]
comparison = read("n100-comparison.json")
for key, equal in comparison["exactEqualityChecks"].items():
    need(equal and a[key] == b[key], key)
for metric in comparison["metrics"]:
    key = metric["metric"]
    x, y = (left[key], right[key]) if key == "totalElapsedSeconds" else (a[key], b[key])
    need(metric["json15"] == x and metric["typed15"] == y, key)
    need(math.isclose(metric["typedRelativePercent"], 100 * (y - x) / x, abs_tol=1e-12), key)
need(a["inventoryTypedCopySnapshots"] == {"warmup": 0, "measured": 0}, "기본 경로")
need(b["inventoryTypedCopySnapshots"] == {"warmup": 2, "measured": 13}, "실제 형식화 경로")
need(not left["complete"] and not right["complete"] and right["experimental_configuration"], "원본 판정 보존")
need(not comparison["decision"]["typedDefaultAdopted"], "비채택 판정")

diagnosis = read("profile-diagnosis.json")
need(diagnosis["instrumentedDuringRun"] and not diagnosis["eligibleAsUnprofiledBaseline"], "N1000 추적 한계")
need(not diagnosis["eligibleForJsonTypedCostComparison"], "비용 비교 제외")
weighted = json.loads(gzip.decompress((ROOT / "profile-56bbfb2-json1000-weighted-stacks.json.gz").read_bytes()))
need(weighted["warmup_advance"]["weighted_thread_ms"] == 0, "예열 표본 범위")
for group, row in diagnosis["measuredAdvanceGroupsInclusive"].items():
    need(row == weighted["measured_advance"]["callsites"][group][0], group)

source = "56bbfb2ff3cec2df79fa28234d61a03fe4a50e63"
provenance = read("execution-provenance.json")
need(all(row["sourceRevisionId"] == source for row in provenance["builds"]), "빌드 출처")
need(provenance["documentationScope"]["newBenchmarkRuns"] == 0, "문서 작업 범위")

followup = read("followup-9263756/provenance-and-comparison.json")
new = read("followup-9263756/copy-9263756-json100.json")["reports"][0]
for key, equal in followup["exactEqualityChecks"].items():
    need(equal and a[key] == new[key], "후속 " + key)
for metric in followup["metrics"]:
    key = metric["metric"]
    need(metric["json56bbfb2"] == a[key] and metric["json9263756"] == new[key], key)
    need(math.isclose(metric["relativePercent"], 100 * (new[key] - a[key]) / a[key], abs_tol=1e-12), key)
need(not followup["finalPerformanceGateSatisfied"], "후속 단일 규모 범위")
print("정적 확인 통과: 원본 해시·압축 해제·동일 입력·지표·경로 카운터·진단 한계")
