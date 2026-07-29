#!/usr/bin/env python3
import os
import sys
import json
import base64
import time
import subprocess

UNITY_CLI = os.path.expandvars(r"%LOCALAPPDATA%\Unity\bin\unity.exe")
OUT_DIR = os.path.join("QA", "reports", "captures")

THEMES = [
    ("scrapyard", "Background_Scrapyard"),
    ("nebula", "Background_Nebula"),
    ("hive", "Background_Hive"),
    ("fortress", "Background_Fortress"),
    ("core", "Background_Core"),
]

def run_unity_cmd(cmd_args, format_json=False):
    full_cmd = [UNITY_CLI, "command"] + cmd_args
    if format_json and "--format" not in cmd_args:
        full_cmd += ["--format", "json"]
    
    res = subprocess.run(full_cmd, capture_output=True, text=True, encoding="utf-8")
    if res.returncode != 0:
        print(f"Error running command {cmd_args}: {res.stderr}", file=sys.stderr, flush=True)
        return None
    
    if format_json:
        try:
            return json.loads(res.stdout)
        except json.JSONDecodeError as e:
            print(f"JSON decode error: {e}\nOutput was: {res.stdout}", file=sys.stderr, flush=True)
            return None
    return res.stdout

def capture_and_save(filename):
    out_path = os.path.join(OUT_DIR, filename)
    data = run_unity_cmd(["capture_game_view"], format_json=True)
    if not data or not data.get("success"):
        print(f"Failed to capture game view for {filename}", file=sys.stderr, flush=True)
        return False
    
    try:
        b64_str = data["data"]["result"]["base64"]
        img_data = base64.b64decode(b64_str)
        with open(out_path, "wb") as f:
            f.write(img_data)
        print(f"Successfully saved current capture: {out_path} ({len(img_data)} bytes)", flush=True)
        return True
    except KeyError as e:
        print(f"KeyError accessing base64 image data: {e}", file=sys.stderr, flush=True)
        return False

def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    print("=== Starting Current Screenshot Capture ===", flush=True)
    
    # 1. Title Scene
    print("\n--- Capturing Title Scene ---", flush=True)
    run_unity_cmd(["open_scene", "Assets/Scenes/Title.unity"])
    time.sleep(1.0)
    run_unity_cmd(["editor_play"])
    time.sleep(1.5)
    
    capture_and_save("current_title.png")
    
    run_unity_cmd(["editor_stop"])
    time.sleep(1.5)
    
    # 2. Battle Scene Background Themes
    print("\n--- Capturing Battle Scene Background Themes ---", flush=True)
    run_unity_cmd(["open_scene", "Assets/Scenes/Battle.unity"])
    time.sleep(1.0)
    run_unity_cmd(["editor_play"])
    time.sleep(2.0)
    
    for theme_name, go_name in THEMES:
        print(f"Activating theme: {theme_name} ({go_name})...", flush=True)
        eval_script = (
            f'foreach(var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) '
            f'{{ if(r.name.StartsWith("Background_")) r.SetActive(r.name == "{go_name}"); }}'
        )
        run_unity_cmd(["eval", eval_script])
        time.sleep(0.5)
        capture_and_save(f"current_{theme_name}.png")
    
    print("\n--- Cleaning up Editor Play State ---", flush=True)
    run_unity_cmd(["editor_stop"])
    time.sleep(1.0)
    print("=== Current Screenshot Capture Complete ===", flush=True)

if __name__ == "__main__":
    main()
