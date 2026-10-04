#!/usr/bin/env python3
"""Render the recorded audit CSV; this script never runs the game engine."""

import argparse
import csv
import math
from collections import defaultdict
from pathlib import Path
from statistics import median

import matplotlib

matplotlib.use("Agg")
matplotlib.rcParams["svg.hashsalt"] = "project-hora-market-audit-v1.2.0"
import matplotlib.pyplot as plt

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("csv_path", type=Path)
parser.add_argument("output_path", type=Path)
args = parser.parse_args()

groups = defaultdict(list)
with args.csv_path.open(newline="", encoding="utf-8") as file:
    for row in csv.DictReader(file):
        groups[(row["scenario"], row["seed"])].append(row)

baseline = [sorted(rows, key=lambda row: int(row["season"]))
            for (scenario, _), rows in groups.items() if scenario == "baseline"]
comparisons = [sorted(rows, key=lambda row: int(row["season"]))
               for (scenario, _), rows in groups.items() if scenario == "zero-fee"]
if len(baseline) != 5 or len(comparisons) != 1:
    raise ValueError("Expected five baseline seeds and one zero-fee comparison")

seasons = [int(row["season"]) for row in baseline[0]]
for rows in baseline + comparisons:
    if [int(row["season"]) for row in rows] != seasons:
        raise ValueError("Audit scenarios have different season coverage")

maximum_index = max(
    100 * int(row[column]) / int(row[initial])
    for rows in baseline + comparisons for row in rows
    for column, initial in [("capitalization", "initial_capitalization"),
                            ("investor_cash", "initial_investor_cash")]
)
upper_limit = max(110, math.ceil(maximum_index * 1.04 / 10) * 10)

fig, axes = plt.subplots(1, 2, figsize=(11.5, 4.5), sharey=True, layout="constrained")
for ax, (column, initial, title) in zip(axes, [
    ("capitalization", "initial_capitalization", "Market capitalization"),
    ("investor_cash", "initial_investor_cash", "Investor cash"),
]):
    indices = [[100 * int(row[column]) / int(row[initial]) for row in rows]
               for rows in baseline]
    medians = [median(values) for values in zip(*indices)]
    lows = [min(values) for values in zip(*indices)]
    highs = [max(values) for values in zip(*indices)]
    comparison = [100 * int(row[column]) / int(row[initial]) for row in comparisons[0]]
    ax.fill_between(seasons, lows, highs, color="#31688e", alpha=0.18,
                    label="Baseline: 5-seed min/max")
    ax.plot(seasons, medians, color="#174a70", linewidth=2.2,
            label="Baseline: 5-seed median")
    ax.plot(seasons, comparison, color="#b75b13", linewidth=1.8, linestyle="--",
            label="Zero fee: seed 20261004")
    ax.axhline(100, color="#78818a", linewidth=0.8, linestyle=":")
    ax.set(title=title, xlabel="Season (30 game days)", xlim=(0, 38), ylim=(0, upper_limit))
    ax.grid(alpha=0.18)
    ax.annotate(f"{medians[-1]:.1f}", (36, medians[-1]), xytext=(4, -13),
                textcoords="offset points", color="#174a70", fontsize=9)
    ax.annotate(f"{comparison[-1]:.1f}", (36, comparison[-1]), xytext=(4, 5),
                textcoords="offset points", color="#b75b13", fontsize=9)
axes[0].set_ylabel("Index (initial = 100)")
axes[0].legend(loc="lower left", fontsize=8, framealpha=0.9)
fig.suptitle("Project Hora v1.2.0: 36-season market audit", fontsize=14)
fig.supxlabel("Monthly observations; zero-fee intervention can change subsequent random paths.",
              fontsize=9, color="#4b5563")
args.output_path.parent.mkdir(parents=True, exist_ok=True)
metadata = ({"Date": "2026-10-04", "Creator": "Project Hora MarketAudit / Matplotlib"}
            if args.output_path.suffix.lower() == ".svg" else {})
fig.savefig(args.output_path, dpi=160, metadata=metadata)
plt.close(fig)
if args.output_path.suffix.lower() == ".svg":
    # Matplotlib emits trailing spaces in SVG path data; newlines still delimit it.
    svg = args.output_path.read_text(encoding="utf-8")
    args.output_path.write_text("\n".join(line.rstrip() for line in svg.splitlines()) + "\n",
                                encoding="utf-8")
print(args.output_path)
