#!/usr/bin/env python3
"""원문 자연 성장 9조건의 459개 관측만 그리는 문서 전용 스크립트.

게임·성능·성장 실행, 원문 수정, 보간/평활·확률 추정은 하지 않는다.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
from pathlib import Path
import platform
import warnings

import matplotlib
matplotlib.use("Agg")
from matplotlib import font_manager, ft2font, pyplot as plt
from matplotlib.lines import Line2D
from matplotlib.ticker import MaxNLocator

HERE = Path(__file__).resolve().parent
SOURCE_COMMIT = "7ce66943b539420c9dc1e3fa8a611daae58020ff"
SOURCE_FP = "4e228e7ba34e538fd1a69abf05934d8162b2a65559ed4ffd50a73b00a8ba2304"
DOCUMENT_BASE = "3d86f6c1fd9a2aa8ebb5204b1d6b2273f797b130"
SEEDS = (42, 98, 2026)
SCENARIOS = {
    "a": "a · 사용자 기본 | 혈연 허용 · 유입 0명",
    "b": "b · 비교 | 혈연 제한 · 20년에 명시 유입 2명",
    "c": "c · 비교 | 혈연 제한 · 유입 0명",
}
COLORS = {42: "#0072B2", 98: "#D55E00", 2026: "#009E73"}
MARKERS = {42: "o", 98: "s", 2026: "^"}
STYLES = {42: "-", 98: "--", 2026: ":"}
PLOT_FIELDS = ("month", "population", "founderDescendants", "everFounderDescendants",
               "descendantGoldenCoreOrHigher", "explicitArrivals")


def load_observations(input_root: Path):
    reports, sources = [], []
    for scenario in SCENARIOS:
        for seed in SEEDS:
            relative = f"{scenario}-{seed}/out/growth.json"
            path = input_root / relative
            data = path.read_bytes()
            raw = json.loads(data)
            assert raw["sourceCommit"] == SOURCE_COMMIT
            assert raw["productSourceFingerprint"] == SOURCE_FP
            # Each selected-job report intentionally has complete:false for the whole nine-case set.
            assert raw["requested_complete"] is True and len(raw["reports"]) == 1
            report = raw["reports"][0]
            assert (report["scenario"], report["seed"]) == (scenario, seed)
            assert report["selectedDefault"] is (scenario == "a")
            assert report["status"] == "completed" and report["observedThroughMonth"] == 6000
            series = report["timeSeries"]
            assert [row["month"] for row in series] == list(range(0, 6001, 120))
            for row in series:
                assert all(type(row[field]) is int and row[field] >= 0 for field in PLOT_FIELDS)
                assert row["founderDescendants"] <= row["population"]
            arrivals = report["externalArrivals"]
            if scenario == "b":
                assert len(arrivals) == 2 and all(item["month"] == 240 for item in arrivals)
                assert all(item["realm"] == "Mortal" and
                           all(item[field] == 0 for field in ("money", "items", "skills", "knowledge"))
                           for item in arrivals)
            else:
                assert arrivals == []
            core = report["milestoneOutcomes"]["firstFounderDescendantCore"]
            assert core["historyComplete"] is True
            if (scenario, seed) == ("c", 2026):
                assert core["outcome"] == "observed-exact-record"
                assert (core["exactMonth"], core["entity"]) == (4326, 8042)
                assert "Cultivation.Formations[GoldenCore].Month" in core["source"]
            else:
                assert core["outcome"] == "not-formed-within-observed-horizon"
                assert core["exactMonth"] is None and core["entity"] is None
            reports.append(report)
            sources.append({"path_relative_to_input_root": relative,
                            "sha256": hashlib.sha256(data).hexdigest(), "bytes": len(data)})
    assert len(reports) == 9 and sum(len(r["timeSeries"]) for r in reports) == 459
    return reports, sources


def configure_font(explicit: str | None):
    candidates = ([Path(explicit)] if explicit else [])
    candidates += [Path(p) for p in sorted(font_manager.findSystemFonts())
                   if any(name in Path(p).name.lower() for name in
                          ("notosanscjk-regular", "notosanscjkkr-regular", "nanumgothic.ttf"))]
    label_text = ("자연 성장 관측 세계 개시 후 년 생존 인구 시조 후손 사용자 기본 비교 혈연 허용 제한 "
                  "명시 유입 시드 최종 관찰창 내 금단 형성 미성립 기록 표본 연결선 가이드")
    needed = set(label_text.replace(" ", ""))
    for path in candidates:
        if not path.is_file():
            continue
        font = ft2font.FT2Font(str(path))
        missing = sorted(c for c in needed if ord(c) not in font.get_charmap())
        if not missing:
            prop = font_manager.FontProperties(fname=str(path))
            matplotlib.rcParams.update({"font.family": prop.get_name(), "font.size": 11,
                "axes.unicode_minus": False, "axes.spines.top": False, "axes.spines.right": False,
                "svg.fonttype": "path", "svg.hashsalt": "seonro-natural-growth-primary-7ce6694"})
            # Register the exact available file; SVG exports outlines for portable Korean rendering.
            font_manager.fontManager.addfont(str(path))
            warnings.filterwarnings("error", message=r"Glyph .* missing from font")
            return {"family": prop.get_name(), "file": str(path), "label_glyphs_missing": [],
                    "svg_fonttype": "path"}
    raise ValueError("한글 지원 글꼴이 필요합니다. Noto Sans CJK를 설치하거나 --font 경로를 지정하세요.")


def export(fig, output: Path, stem: str, title: str):
    description = ("실측 source7ce6694/FP4e228의 9조건·459관측. 10년 간격 점이며 선은 가이드. "
                   "확률·인과·미래 예측을 뜻하지 않음. 528계열 실행 비용과 별도.")
    fig.savefig(output / f"{stem}.png", dpi=180, facecolor="white",
                metadata={"Title": title, "Description": description,
                          "Software": f"Matplotlib {matplotlib.__version__}"})
    fig.savefig(output / f"{stem}.svg", facecolor="white",
                metadata={"Title": title, "Description": description, "Date": None,
                          "Creator": f"Matplotlib {matplotlib.__version__}"})
    svg = output / f"{stem}.svg"
    svg.write_text("\n".join(line.rstrip() for line in svg.read_text(encoding="utf-8").splitlines()) + "\n", encoding="utf-8")
    plt.close(fig)


def draw_observations(reports, output: Path):
    fig, axes = plt.subplots(3, 2, figsize=(12.8, 10.8), sharex=True, sharey="col")
    fig.subplots_adjust(left=0.08, right=0.975, top=0.86, bottom=0.16, hspace=0.48, wspace=0.18)
    fig.suptitle("자연 성장 관측 · 세계 개시 후 0–500년", y=0.974, fontsize=20, fontweight="bold")
    fig.text(0.5, 0.929, "a/b/c 각각 시드 3개 · 조건·시드당 10년(120개월) 간격 51관측",
             ha="center", fontsize=12)
    handles = [Line2D([], [], color=COLORS[seed], marker=MARKERS[seed], linestyle=STYLES[seed],
                      markerfacecolor="white", label=f"시드 {seed}") for seed in SEEDS]
    fig.legend(handles=handles, loc="upper center", bbox_to_anchor=(0.5, 0.914), ncol=3, frameon=False)
    max_population = max(row["population"] for r in reports for row in r["timeSeries"])
    max_descendants = max(row["founderDescendants"] for r in reports for row in r["timeSeries"])
    for row_index, scenario in enumerate(SCENARIOS):
        for col, field in enumerate(("population", "founderDescendants")):
            ax = axes[row_index, col]
            for report in [r for r in reports if r["scenario"] == scenario]:
                seed = report["seed"]
                ax.plot([v["month"] / 12 for v in report["timeSeries"]],
                        [v[field] for v in report["timeSeries"]], color=COLORS[seed],
                        marker=MARKERS[seed], linestyle=STYLES[seed], linewidth=1.15,
                        markersize=3.8, markerfacecolor="white", markeredgewidth=0.85, alpha=0.88)
            ax.set_title(SCENARIOS[scenario], loc="left", fontsize=11, pad=11,
                         color="#174A73" if scenario == "a" else "#222222")
            ax.set_xlim(0, 500)
            ax.set_ylim(-0.25, (max_population if col == 0 else max_descendants) + 1)
            ax.set_xticks(range(0, 501, 100))
            ax.yaxis.set_major_locator(MaxNLocator(integer=True, nbins=5))
            ax.grid(axis="y", color="#D9DFE5", linewidth=0.6)
            ax.set_axisbelow(True)
            ax.set_ylabel("생존 인구 (명)" if col == 0 else "생존 시조 후손 (명)")
            if row_index == 2:
                ax.set_xlabel("세계 개시 후 (년)")
            if scenario == "b" and col == 0:
                ax.axvline(20, color="#6B7280", linestyle="--", linewidth=0.8, alpha=0.8)
                ax.text(0.095, 0.93, "20년: 명시 유입 2명", transform=ax.transAxes,
                        fontsize=9.5, color="#4B5563", va="top")
    fig.text(0.08, 0.108, "점은 실제 10년 간격 관측, 연결선은 시각적 가이드입니다. 점 사이 이력을 추정하지 않습니다.", fontsize=10.2)
    fig.text(0.08, 0.078, "후손은 생존 시조 혈통 후손입니다. 각 조건 n=3의 유한 관측이며 확률·인과·성공 보장·500년 이후 예측으로 해석하지 않습니다.", fontsize=9.5)
    fig.text(0.08, 0.045, "자연 성장 원문: source 7ce6694 · FP 4e228  |  후속 FP 528 계열의 실행 비용 측정과 별도", fontsize=9.5, color="#59616D")
    export(fig, output, "growth-observations", "자연 성장 9조건의 생존 인구·생존 시조 후손 관측")


def draw_final(reports, output: Path):
    fig, axes = plt.subplots(1, 3, figsize=(13, 7.5), gridspec_kw={"width_ratios": (1.15, 1.15, 2.05)}, sharey=True)
    fig.subplots_adjust(left=0.12, right=0.975, top=0.78, bottom=0.23, wspace=0.23)
    fig.suptitle("500년 관측 · 최종 생존 수와 후손 금단 기록", y=0.965, fontsize=20, fontweight="bold")
    fig.text(0.5, 0.91, "각 조건 n=3 · 시드별 실제 값을 표시하며 평균·확률을 추정하지 않습니다.", ha="center", fontsize=12)
    counts = [f"{scenario} {sum(r['milestoneOutcomes']['firstFounderDescendantCore']['outcome'] == 'observed-exact-record' for r in reports if r['scenario'] == scenario)}/{len(SEEDS)}"
              for scenario in SCENARIOS]
    fig.text(0.5, 0.85, "관찰창 내 후손 금단 형성 기록: " + " · ".join(counts) + " (관측 사례 수)", ha="center", fontsize=12)
    max_final = max(r["timeSeries"][-1]["population"] for r in reports)
    ypos = list(range(9))
    labels = [f"{r['scenario']} · 시드 {r['seed']}" for r in reports]
    for row_index, report in enumerate(reports):
        final = report["timeSeries"][-1]
        seed = report["seed"]
        for col, field in enumerate(("population", "founderDescendants")):
            ax = axes[col]
            value = final[field]
            ax.barh(row_index, value, height=0.52, color=COLORS[seed], alpha=0.80)
            ax.text(value + 0.12, row_index, str(value), va="center", fontsize=11)
        core = report["milestoneOutcomes"]["firstFounderDescendantCore"]
        formed = core["outcome"] == "observed-exact-record"
        text = (f"{final['descendantGoldenCoreOrHigher']}명 · 세계 {core['exactMonth'] / 12:g}년 형성\n{core['exactMonth']}개월 · 인물 {core['entity']}의 실제 형성 기록"
                if formed else "0명 · 관찰창 내 미성립")
        axes[2].text(0.04, row_index, text, ha="left", va="center", fontsize=10.6,
                     color="#08724E" if formed else "#535A64", fontweight="bold" if formed else "normal")
    for col, ax in enumerate(axes):
        ax.set_ylim(8.65, -0.65)
        for line in (2.5, 5.5):
            ax.axhline(line, color="#C6CED8", linewidth=0.8)
        if col < 2:
            ax.set_xlim(0, max_final + 1.1)
            ax.set_xticks(range(0, max_final + 2))
            ax.set_xlabel("명")
            ax.grid(axis="x", color="#E5E7EB", linewidth=0.6)
            ax.set_axisbelow(True)
        else:
            ax.set_xlim(0, 1)
            ax.set_xticks([])
            ax.spines[["bottom", "left"]].set_visible(False)
        if col:
            ax.tick_params(axis="y", left=False, labelleft=False)
    axes[0].set_yticks(ypos, labels)
    axes[0].set_title("500년의 생존 인구", loc="left", fontsize=12, pad=15)
    axes[1].set_title("500년의 생존 시조 후손", loc="left", fontsize=12, pad=15)
    axes[2].set_title("생존 금단 후손 수 · 관찰창 형성 기록", loc="left", fontsize=12, pad=15)
    fig.text(0.12, 0.155, "a: 사용자 기본·혈연 허용·유입 0명  |  b: 혈연 제한·20년에 명시 유입 2명  |  c: 혈연 제한·유입 0명", fontsize=10)
    fig.text(0.12, 0.112, "금단 기록은 사망한 신체도 포함한 정본 형성 이력입니다. 360.5년은 세계 개시 후 시간이며 몸 나이나 10년 보간값이 아닙니다.", fontsize=9.4)
    fig.text(0.12, 0.07, "source 7ce6694 · FP 4e228  |  500년 뒤 생존·성공을 보장하지 않으며, 후속 FP 528 실행 비용과 별도입니다.", fontsize=9.7, color="#59616D")
    export(fig, output, "growth-final-outcomes", "500년 관측의 최종 생존 수·후손 금단 형성 기록")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input-root", type=Path, default=HERE.parent)
    parser.add_argument("--output-dir", type=Path, default=HERE)
    parser.add_argument("--font", help="한글 지원 TTF/OTF/TTC 파일 경로")
    args = parser.parse_args()
    reports, sources = load_observations(args.input_root)
    font = configure_font(args.font)
    args.output_dir.mkdir(parents=True, exist_ok=True)
    with (args.output_dir / "observations.csv").open("w", newline="", encoding="utf-8") as stream:
        writer = csv.writer(stream, lineterminator="\n")
        writer.writerow(("scenario", "seed", "month", "world_year", *PLOT_FIELDS[1:]))
        for report in reports:
            for row in report["timeSeries"]:
                writer.writerow((report["scenario"], report["seed"], row["month"], row["month"] // 12,
                                 *(row[field] for field in PLOT_FIELDS[1:])))
    final_records = []
    for report in reports:
        final = report["timeSeries"][-1]
        core = report["milestoneOutcomes"]["firstFounderDescendantCore"]
        final_records.append({"scenario": report["scenario"], "seed": report["seed"],
            "population": final["population"], "living_founder_descendants": final["founderDescendants"],
            "ever_founder_descendants": final["everFounderDescendants"],
            "living_descendant_core_or_higher": final["descendantGoldenCoreOrHigher"],
            "first_core_outcome": core["outcome"], "first_core_exact_month": core["exactMonth"],
            "first_core_world_year": core["exactMonth"] / 12 if core["exactMonth"] is not None else None,
            "first_core_entity": core["entity"]})
    with (args.output_dir / "final-outcomes.csv").open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(final_records[0]), lineterminator="\n")
        writer.writeheader(); writer.writerows(final_records)
    draw_observations(reports, args.output_dir)
    draw_final(reports, args.output_dir)
    provenance = {"schema_version": 1, "kind": "문서 전용 자연 성장 그림",
        "source_commit": SOURCE_COMMIT, "product_source_fingerprint": SOURCE_FP,
        "document_base": DOCUMENT_BASE, "observations": 459, "cases": 9, "seeds_per_scenario": 3,
        "world_months": [0, 6000], "observation_interval_months": 120,
        "font": font, "matplotlib": matplotlib.__version__, "python": platform.python_version(),
        "input_files": sources, "final_records": final_records,
        "first_core_time_source": "실제 신체 Cultivation.Formations[GoldenCore].Month; 세계 개시 후 경과 시간",
        "lines_are_visual_guides_only": True, "interpolation_or_smoothing": False,
        "probability_or_causal_inference": False, "beyond_500_year_prediction": False,
        "sensitivity_results_included": False, "current_528_cost_measurements_included": False,
        "game_or_measurement_executed": False, "original_files_modified": False}
    (args.output_dir / "figure-provenance.json").write_text(json.dumps(provenance, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"cases": 9, "observations": 459, "png_svg_pairs": 2, "hangul_glyphs_missing": [],
                      "game_or_measurement_executed": False}, ensure_ascii=False))


if __name__ == "__main__":
    main()
