import os
import sys
import json
import time
import subprocess

UNITY_CLI = os.path.expandvars(r"%LOCALAPPDATA%\Unity\bin\unity.exe")
CAPTURES_DIR = os.path.abspath(os.path.join("QA", "reports", "captures"))
SMOKE_1_PATH = os.path.join(CAPTURES_DIR, "smoke_1.png")
SMOKE_2_PATH = os.path.join(CAPTURES_DIR, "smoke_2.png")

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

def get_console_entries():
    data = run_unity_cmd(["console"], format_json=True)
    if data and data.get("success") and "data" in data and "result" in data["data"]:
        return data["data"]["result"].get("entries", [])
    return []

def main():
    os.makedirs(CAPTURES_DIR, exist_ok=True)
    print("=== Starting 60s Smoke Test ===", flush=True)
    print(f"Capture 1 path: {SMOKE_1_PATH}")
    print(f"Capture 2 path: {SMOKE_2_PATH}")
    
    # 1. Fetch initial console seq number
    initial_entries = get_console_entries()
    start_seq = initial_entries[-1]["seq"] if initial_entries else 0
    print(f"Initial console sequence index: {start_seq}")
    
    # Ensure editor is stopped first
    run_unity_cmd(["editor_stop"])
    time.sleep(1.0)
    
    # 2. Open Battle Scene & Start Play Mode
    print("Opening Battle Scene...", flush=True)
    run_unity_cmd(["open_scene", "Assets/Scenes/Battle.unity"])
    time.sleep(1.0)
    
    print("Starting Play Mode (editor_play)...", flush=True)
    run_unity_cmd(["editor_play"])
    time.sleep(2.0)
    
    # 3. Set timeScale to 4 via eval
    print("Setting timeScale = 4.0 via eval...", flush=True)
    eval_res = run_unity_cmd(["eval", "UnityEngine.Time.timeScale = 4.0f;"])
    print(f"timeScale eval output: {eval_res.strip() if eval_res else 'None'}")
    
    start_time = time.time()
    captured_1 = False
    captured_2 = False
    
    print("Running 60 seconds real-time smoke simulation...", flush=True)
    
    while True:
        elapsed = time.time() - start_time
        if elapsed >= 60.0:
            break
            
        if elapsed >= 20.0 and not captured_1:
            print(f"[{elapsed:.1f}s] Taking screenshot 1 (smoke_1.png)...", flush=True)
            code = f'UnityEngine.ScreenCapture.CaptureScreenshot(@"{SMOKE_1_PATH}");'
            run_unity_cmd(["eval", code])
            captured_1 = True
            
        if elapsed >= 45.0 and not captured_2:
            print(f"[{elapsed:.1f}s] Taking screenshot 2 (smoke_2.png)...", flush=True)
            code = f'UnityEngine.ScreenCapture.CaptureScreenshot(@"{SMOKE_2_PATH}");'
            run_unity_cmd(["eval", code])
            captured_2 = True
            
        time.sleep(1.0)
        
    print(f"Smoke simulation completed after {time.time() - start_time:.1f}s.", flush=True)
    
    # Wait 2 seconds for screenshot buffer flush before stopping play mode
    time.sleep(2.0)
    
    # 4. Collect Console logs (errors & warnings)
    print("Collecting console log entries during smoke test...", flush=True)
    all_entries = get_console_entries()
    
    new_entries = [e for e in all_entries if e.get("seq", 0) > start_seq]
    errors = [e for e in new_entries if e.get("level") in ("error", "exception")]
    warnings = [e for e in new_entries if e.get("level") == "warning"]
    logs = [e for e in new_entries if e.get("level") == "log"]
    
    print(f"Total new log entries collected: {len(new_entries)}")
    print(f"Errors/Exceptions: {len(errors)}")
    print(f"Warnings: {len(warnings)}")
    print(f"Info Logs: {len(logs)}")
    
    # 5. Stop Play Mode
    print("\nStopping Play Mode (editor_stop)...", flush=True)
    run_unity_cmd(["editor_stop"])
    time.sleep(1.5)
    
    # Check screenshot files
    s1_exists = os.path.exists(SMOKE_1_PATH)
    s2_exists = os.path.exists(SMOKE_2_PATH)
    print(f"smoke_1.png created: {s1_exists} ({os.path.getsize(SMOKE_1_PATH) if s1_exists else 0} bytes)")
    print(f"smoke_2.png created: {s2_exists} ({os.path.getsize(SMOKE_2_PATH) if s2_exists else 0} bytes)")
    
    # Save log summary JSON for report reference
    summary = {
        "start_seq": start_seq,
        "total_new_entries": len(new_entries),
        "error_count": len(errors),
        "warning_count": len(warnings),
        "errors": errors,
        "warnings": warnings,
        "smoke_1_exists": s1_exists,
        "smoke_2_exists": s2_exists
    }
    summary_path = os.path.join(CAPTURES_DIR, "smoke_log_summary.json")
    with open(summary_path, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=2, ensure_ascii=False)
    print(f"Summary saved to {summary_path}")

if __name__ == "__main__":
    main()
