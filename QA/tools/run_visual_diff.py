#!/usr/bin/env python3
import os
import sys
import subprocess

BASELINES = [
    ("baseline_title.png", "current_title.png", "title"),
    ("baseline_scrapyard.png", "current_scrapyard.png", "scrapyard"),
    ("baseline_nebula.png", "current_nebula.png", "nebula"),
    ("baseline_hive.png", "current_hive.png", "hive"),
    ("baseline_fortress.png", "current_fortress.png", "fortress"),
    ("baseline_core.png", "current_core.png", "core"),
]

BASELINES_DIR = os.path.join("QA", "baselines")
CAPTURES_DIR = os.path.join("QA", "reports", "captures")
DIFFS_DIR = os.path.join("QA", "reports", "diffs")

def main():
    os.makedirs(DIFFS_DIR, exist_ok=True)
    results = []

    for base_file, curr_file, name in BASELINES:
        base_path = os.path.join(BASELINES_DIR, base_file)
        curr_path = os.path.join(CAPTURES_DIR, curr_file)
        diff_path = os.path.join(DIFFS_DIR, f"diff_{name}.png")

        cmd = [
            sys.executable,
            os.path.join("QA", "tools", "visual_diff.py"),
            base_path,
            curr_path,
            "--threshold", "5.0",
            "--diff-out", diff_path
        ]

        res = subprocess.run(cmd, capture_output=True, text=True)
        print(f"=== Results for {name} ===")
        print(res.stdout)
        if res.stderr:
            print("STDERR:", res.stderr)
        results.append((name, res.returncode, res.stdout))

if __name__ == "__main__":
    main()
