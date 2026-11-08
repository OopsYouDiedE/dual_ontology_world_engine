"""Aggregate original Godot stage-one receipts. Never submits gameplay or fabricates telemetry."""
import argparse
import csv
import json
import math
from pathlib import Path
from statistics import mean

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument("observations", type=Path)
parser.add_argument("--destination", type=Path)
arguments = parser.parse_args()
observations = sorted(arguments.observations.glob("observation-*.json"))
records = [json.loads(path.read_text(encoding="utf-8")) for path in observations]
records = [record for record in records if record.get("room_streaming", {}).get("scope") == "stage1-zone-b-observer"]
if not records:
    raise SystemExit("No original stage-one Godot observations in this directory.")
latest = records[-1]["room_streaming"]
timeline = Path(latest["receipt_directory"]) / "timeline.jsonl"
events = [json.loads(line) for line in timeline.read_text(encoding="utf-8").splitlines()]
source_receipts = ROOT / "artifacts/coregodot-stage1"
destination = arguments.destination or source_receipts
destination.mkdir(parents=True, exist_ok=True)

def export(name, fields, rows):
    with (destination / name).open("w", newline="", encoding="utf-8-sig") as output:
        writer = csv.DictWriter(output, fields)
        writer.writeheader()
        writer.writerows(rows)

frames = [event for event in events if event["kind"] == "frame"]
slow_frames = [event for event in events if event["kind"] == "slow_frame"]
export("slow-frames.csv", ["time_us", "interval_ms", "previous_streaming_work_ms", "process_ms", "physics_ms", "gc0", "gc1", "gc2", "managed_heap_bytes", "pipeline_surface", "pipeline_draw"],
       [dict(time_us=event["time_us"], **event["details"]) for event in slow_frames])
export("frame-measurements.csv", ["time_us", "interval_ms", "capture_excluded"],
       [{field: event[field] for field in ("time_us", "interval_ms", "capture_excluded")} for event in frames])
assembly = [event for event in events if event["kind"] == "assembly"]
measurement_start = next(event["time_us"] for event in events if event["kind"] == "measured_run_begin")
measured_assembly = [event for event in assembly if event["time_us"] >= measurement_start]
export("assembly-measurements.csv", ["time_us", "bundle", "instantiate_ms", "attach_ms", "total_ms"],
       [dict(time_us=event["time_us"], **event["details"]) for event in assembly])
resource_events = [event for event in events if event["kind"] in {"baseline", "resources", "shutdown", "measured_run_begin"}]
resource_fields = ["nodes", "orphan_nodes", "active_collision_objects", "loaded_bundles", "pending_loads", "static_memory_bytes"]
export("resource-audit.csv", ["time_us", "kind"] + resource_fields,
       [dict(time_us=event["time_us"], kind=event["kind"], **{field:event["details"][field] for field in resource_fields}) for event in resource_events])
export("phase-transitions.csv", ["time_us", "room", "phase", "effect", "generation"],
       [dict(time_us=event["time_us"], **event["details"]) for event in events if event["kind"] == "room_transition"])
preparing = {}
room_preparations = []
for event in events:
    details = event.get("details", {})
    if event["kind"] == "room_transition" and details["effect"] == "Prepare":
        preparing[(details["room"], details["generation"])] = event["time_us"]
    if event["kind"] == "preparation_complete":
        key = (details["room"], details["generation"])
        if key in preparing:
            room_preparations.append(dict(room=key[0], generation=key[1], prepare_time_us=preparing[key],
                                          ready_time_us=event["time_us"], prepare_to_ready_ms=(event["time_us"]-preparing[key])/1000))
export("room-preparation.csv", ["room", "generation", "prepare_time_us", "ready_time_us", "prepare_to_ready_ms"], room_preparations)
export("bundle-activation.csv", ["time_us", "bundle", "enabled", "ms"],
       [dict(time_us=event["time_us"], **event["details"]) for event in events if event["kind"] == "bundle_activation"])
values = sorted(frame["interval_ms"] for frame in frames if not frame["capture_excluded"])
inclusive_values = sorted(frame["interval_ms"] for frame in frames)
inclusive_statistics = {"samples":len(inclusive_values), "mean_ms":mean(inclusive_values),
                        "fps":1000/mean(inclusive_values),
                        "p99_ms":inclusive_values[max(0,math.ceil(len(inclusive_values)*.99)-1)],
                        "maximum_ms":max(inclusive_values)}
raw_statistics = {"samples":len(values), "mean_ms":mean(values),
                  "p99_ms":values[max(0,math.ceil(len(values)*.99)-1)], "maximum_ms":max(values)}
summary = {
    "scope":"formal Godot stage-one observer scene; Agent input; no characters or campaign acceptance",
    "observations":str(arguments.observations.resolve()), "timeline":str(timeline),
    "last_observation":records[-1]["observation_id"],
    "reported_frame_mean_ms":latest["frame_mean_ms"], "reported_frame_p99_ms":latest["frame_p99_ms"],
    "reported_frame_max_ms":latest["frame_max_ms"], "raw_recalculation":raw_statistics,
    "raw_inclusive_recalculation":inclusive_statistics,
    "diagnostic_slow_frames":len(slow_frames),
    "baseline":latest["baseline"], "last_resources":latest["resources"],
    "sampled_peaks":{field:max(event["details"][field] for event in resource_events) for field in resource_fields},
    "assembly":{field:{"mean":mean(event["details"][field] for event in assembly),
                       "maximum":max(event["details"][field] for event in assembly)}
                for field in ("instantiate_ms", "attach_ms", "total_ms")},
    "assembly_after_reveal":{field:{"mean":mean(event["details"][field] for event in measured_assembly),
                                    "maximum":max(event["details"][field] for event in measured_assembly)}
                             for field in ("instantiate_ms", "attach_ms", "total_ms")} if measured_assembly else {},
    "observation_files":[str(path.resolve()) for path in observations]
}
(destination / "receipt-summary.json").write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding="utf-8")
commands = [
    ("构建", "dotnet build DualOntologyWorldEngine.csproj --no-restore", "godot-build-raw.txt"),
    ("生命周期与空间边界单测", "dotnet run --project core/cli/DuelView.Cli.csproj -- --verify-room-lifecycle", "lifecycle-final-raw.txt"),
    ("编译资源与拓扑单测", "dotnet run --project core/cli/DuelView.Cli.csproj -- --verify-room-bundles", "bundle-checks-raw.txt"),
    ("DV 建筑资源编译", ".runtime/godot/4.6.1/Godot_v4.6.1-stable_mono_win64_console.exe --headless --path . --script tools/building/CompileRoomBundles.gd", "bundle-compile-raw.txt")
]
text = ["# 第 1 阶段原始回执\n", "以下命令在项目根目录运行。控制台输出按原文保留。实机操作使用正式输入 MCP。编译过程不计为实机验收。\n"]
for title, command, filename in commands:
    text.extend(["## " + title + "\n", "```text\n" + command + "\n```\n",
                 "```text\n" + (source_receipts/filename).read_text(encoding="utf-8").rstrip() + "\n```\n"])
text.extend(["## 实机原始文件\n", "观察目录：`"+str(arguments.observations.resolve())+"`。\n",
             "逐帧与事件原文：`"+str(timeline)+"`。\n",
             "frame-measurements.csv、assembly-measurements.csv、resource-audit.csv 和 phase-transitions.csv 逐项转录原始事件。room-preparation.csv 按房间代次关联准备触发和全部资源就绪，包含异步等待。bundle-activation.csv 转录渲染、碰撞与 Process 启停事件，ms 为当帧调度起点至该事件的累计耗时。receipt-summary.json 独立汇总原始数值。\n",
             "原始逐帧文件在末次 MCP 观察后继续记录到宿主关闭前，独立复算样本数与末次观察样本数可能不同。\n"])
(destination/"raw-receipts.md").write_text("\n".join(text),encoding="utf-8")
print(json.dumps({"raw_inclusive_recalculation":inclusive_statistics,"raw_recalculation":raw_statistics,"last_resources":summary["last_resources"],"assembly":summary["assembly"]},ensure_ascii=False))
