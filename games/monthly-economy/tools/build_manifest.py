#!/usr/bin/env python3
"""Record artifact hashes without embedding developer paths or credentials."""
import hashlib
import json
import pathlib
import subprocess
import sys

build_dir = pathlib.Path(sys.argv[1])
project_dir = pathlib.Path(__file__).resolve().parents[1]
artifacts = [build_dir / name for name in sys.argv[2:]] if len(sys.argv) > 2 else [p for p in sorted(build_dir.iterdir()) if p.is_file() and p.suffix in {".apk", ".zip", ".x86_64"}]
manifest = {"game": "monthly-economy", "version": "0.1.0-experimental", "godot": "4.6.3.stable", "source_commit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=project_dir, text=True).strip(), "source_dirty": bool(subprocess.check_output(["git", "status", "--porcelain", "--", "."], cwd=project_dir, text=True).strip()), "experimental": True, "artifacts": [{"file": p.name, "bytes": p.stat().st_size, "sha256": hashlib.file_digest(p.open("rb"), "sha256").hexdigest()} for p in artifacts]}
(build_dir / "BUILD-MANIFEST.json").write_text(json.dumps(manifest, indent=2) + "\n")
(build_dir / "SHA256SUMS.txt").write_text("".join(a["sha256"] + "  " + a["file"] + "\n" for a in manifest["artifacts"]))
print("Artifact SHA-256 manifest written.")
