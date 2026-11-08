import asyncio
import json
import os
import subprocess
import sys
import time
from pathlib import Path

PYTHON_EXE = r"c:\Users\iii\Desktop\dual_ontology_world_engine\mcp\game\.venv\Scripts\python.exe"
RECONSTRUCT_DIR = r"c:\Users\iii\Desktop\dual_ontology_world_reconstruct"
CLIENT_SCRIPT = "mcp/game/input_client.py"

def main():
    print("=== [Continuous MCP Gameplay & Audit Engine] ===", flush=True)
    env = os.environ.copy()
    env["PYTHONUNBUFFERED"] = "1"
    
    proc = subprocess.Popen(
        [PYTHON_EXE, "-u", CLIENT_SCRIPT],
        cwd=RECONSTRUCT_DIR,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=1,
        env=env
    )
    
    ready_line = proc.stdout.readline()
    print("MCP Client ready:", ready_line.strip(), flush=True)
    ready_data = json.loads(ready_line)
    receipt_dir = Path(ready_data["receipt_directory"])
    print(f"Receipt Directory: {receipt_dir}", flush=True)
    
    def send_cmd(tool, args):
        payload = {"tool": tool, "arguments": args}
        t0 = time.time()
        proc.stdin.write(json.dumps(payload, ensure_ascii=False) + "\n")
        proc.stdin.flush()
        line = proc.stdout.readline()
        elapsed = time.time() - t0
        data = json.loads(line) if line.strip() else {}
        if data.get("is_error"):
            print(f"  [ERROR] {tool} returned error: {data.get('error')}", flush=True)
        return elapsed, data

    game_log = []
    
    try:
        # Phase 1: On
        print("\n>>> Phase 1: Starting Host & Title...", flush=True)
        el, resp = send_cmd("on", {
            "backend": "godot",
            "seed": 1,
            "viewer": "detective",
            "background_capture": True,
            "content_path": "assets/data/scenarios/campaign.dv",
            "save_slot": "qa-gameplay-slot"
        })
        game_log.append({"action": "on", "elapsed": el, "resp": resp})
        print(f"  on result ({el:.2f}s): is_error={resp.get('is_error')} host_errors={resp.get('host_errors')}", flush=True)
        
        # Phase 2: Start Game -> Enter R01 Field
        print("\n>>> Phase 2: Starting New Game (Enter R01)...", flush=True)
        el, resp = send_cmd("step", {
            "operation": {"type": "start"},
            "wait_ms": 2500,
            "capture_image": True
        })
        game_log.append({"action": "step_start", "elapsed": el, "resp": resp})
        print(f"  step start result ({el:.2f}s): started={resp.get('started')} host_errors={resp.get('host_errors')}", flush=True)
        
        # Phase 3: Observe 3D Field State
        print("\n>>> Phase 3: Initial Observe in R01...", flush=True)
        el, resp = send_cmd("observe", {"wait_ms": 1000, "capture_image": True})
        game_log.append({"action": "observe_field_initial", "elapsed": el, "resp": resp})
        print(f"  observe result: host_errors={resp.get('host_errors')}", flush=True)
        
        # Phase 4: Walk around room (Pick click)
        print("\n>>> Phase 4: Click to walk near desk (x=450, y=340)...", flush=True)
        el, resp = send_cmd("step", {
            "operation": {"type": "click", "x": 450, "y": 340},
            "wait_ms": 1800,
            "capture_image": True
        })
        game_log.append({"action": "click_walk_desk", "elapsed": el, "resp": resp})
        print(f"  click walk desk result: host_errors={resp.get('host_errors')}", flush=True)
        
        # Phase 5: Keyboard movement (move_right towards bed)
        print("\n>>> Phase 5: Keyboard movement (move_right, frames=30)...", flush=True)
        el, resp = send_cmd("step", {
            "operation": {"type": "input_action", "action": "move_right", "frames": 30},
            "wait_ms": 1500,
            "capture_image": True
        })
        game_log.append({"action": "input_move_right", "elapsed": el, "resp": resp})
        print(f"  move right result: host_errors={resp.get('host_errors')}", flush=True)
        
        # Phase 6: Interact action
        print("\n>>> Phase 6: Interact trigger (action=interact)...", flush=True)
        el, resp = send_cmd("step", {
            "operation": {"type": "input_action", "action": "interact", "frames": 2},
            "wait_ms": 1200,
            "capture_image": True
        })
        game_log.append({"action": "input_interact", "elapsed": el, "resp": resp})
        print(f"  interact result: host_errors={resp.get('host_errors')}", flush=True)
        
        # Phase 7: Toggle Casebook
        print("\n>>> Phase 7: Toggle Casebook (Tab)...", flush=True)
        el, resp = send_cmd("step", {
            "operation": {"type": "input_action", "action": "toggle_casebook", "frames": 2},
            "wait_ms": 1500,
            "capture_image": True
        })
        game_log.append({"action": "toggle_casebook_open", "elapsed": el, "resp": resp})
        print(f"  toggle casebook result: host_errors={resp.get('host_errors')}", flush=True)
        
        # Phase 8: Flip to Rule Bookmark (Bookmark2)
        # Find Bookmark2 in controls
        controls = resp.get("controls", [])
        bm2_pos = None
        bm3_pos = None
        close_stamp_pos = None
        for c in controls:
            text = c.get("text", "")
            path = c.get("path", "")
            rect = c.get("rect", [0, 0, 0, 0])
            cx = rect[0] + rect[2] / 2
            cy = rect[1] + rect[3] / 2
            if "Bookmark2" in path or text == "规则":
                bm2_pos = (cx, cy)
            if "Bookmark3" in path or text == "日程":
                bm3_pos = (cx, cy)
            if "CloseStamp" in path or text == "合上":
                close_stamp_pos = (cx, cy)
        
        print(f"  Found controls: Bookmark2={bm2_pos}, Bookmark3={bm3_pos}, CloseStamp={close_stamp_pos}", flush=True)
        
        if bm2_pos:
            print(f"\n>>> Phase 8: Clicking Bookmark2 (规则) at {bm2_pos}...", flush=True)
            el, resp = send_cmd("step", {
                "operation": {"type": "click", "x": bm2_pos[0], "y": bm2_pos[1]},
                "wait_ms": 1200,
                "capture_image": True
            })
            game_log.append({"action": "click_bookmark2", "elapsed": el, "resp": resp})
            print(f"  click bookmark2 result: host_errors={resp.get('host_errors')}", flush=True)
            
        if bm3_pos:
            print(f"\n>>> Phase 9: Clicking Bookmark3 (日程) at {bm3_pos}...", flush=True)
            el, resp = send_cmd("step", {
                "operation": {"type": "click", "x": bm3_pos[0], "y": bm3_pos[1]},
                "wait_ms": 1200,
                "capture_image": True
            })
            game_log.append({"action": "click_bookmark3", "elapsed": el, "resp": resp})
            print(f"  click bookmark3 result: host_errors={resp.get('host_errors')}", flush=True)
            
        if close_stamp_pos:
            print(f"\n>>> Phase 10: Closing Casebook via CloseStamp at {close_stamp_pos}...", flush=True)
            el, resp = send_cmd("step", {
                "operation": {"type": "click", "x": close_stamp_pos[0], "y": close_stamp_pos[1]},
                "wait_ms": 1500,
                "capture_image": True
            })
            game_log.append({"action": "click_close_stamp", "elapsed": el, "resp": resp})
            print(f"  close stamp result: host_errors={resp.get('host_errors')}", flush=True)
        else:
            print("\n>>> Phase 10: Closing Casebook via Tab key...", flush=True)
            el, resp = send_cmd("step", {
                "operation": {"type": "input_action", "action": "toggle_casebook", "frames": 2},
                "wait_ms": 1500,
                "capture_image": True
            })
            game_log.append({"action": "toggle_casebook_close", "elapsed": el, "resp": resp})
            print(f"  toggle casebook close result: host_errors={resp.get('host_errors')}", flush=True)
            
        # Phase 11: Walk towards Door (x=1150, y=240 in R01 view)
        print("\n>>> Phase 11: Clicking to walk to door (x=1150, y=240)...", flush=True)
        el, resp = send_cmd("step", {
            "operation": {"type": "click", "x": 1150, "y": 240},
            "wait_ms": 2500,
            "capture_image": True
        })
        game_log.append({"action": "click_walk_door", "elapsed": el, "resp": resp})
        print(f"  walk door result: host_errors={resp.get('host_errors')}", flush=True)
        
        # Phase 12: Try to interact with Door
        print("\n>>> Phase 12: Interacting with Door...", flush=True)
        el, resp = send_cmd("step", {
            "operation": {"type": "input_action", "action": "interact", "frames": 2},
            "wait_ms": 1500,
            "capture_image": True
        })
        game_log.append({"action": "door_interact", "elapsed": el, "resp": resp})
        print(f"  door interact result: host_errors={resp.get('host_errors')}", flush=True)
        
        # Phase 13: Final observe before close
        print("\n>>> Phase 13: Final observe...", flush=True)
        el, resp = send_cmd("observe", {"wait_ms": 1000, "capture_image": True})
        game_log.append({"action": "final_observe", "elapsed": el, "resp": resp})
        print(f"  final observe: host_errors={resp.get('host_errors')}", flush=True)

    except Exception as e:
        print(f"Exception during gameplay: {e}", flush=True)
    finally:
        print("\n>>> Exiting Game (Close)...", flush=True)
        el, resp = send_cmd("close", {})
        print(f"  close result: {resp}", flush=True)
        proc.stdin.close()
        proc.wait(timeout=10)
        
    summary_file = receipt_dir / "continuous_gameplay_summary.json"
    summary_file.write_text(json.dumps(game_log, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"\nAll logs saved to {summary_file}", flush=True)

if __name__ == "__main__":
    main()
