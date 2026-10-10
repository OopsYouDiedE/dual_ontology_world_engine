import hashlib
import json
import pathlib
import shutil

HERE = pathlib.Path(__file__).resolve().parent
ROOT = pathlib.Path(__file__).resolve().parents[5]
files = [
    "csharp/SubjectiveDecision.cs",
    "godot_csharp/scenes/Shared/Story/PlayerController.cs",
    "godot_csharp/scenes/Host/GameEngine.cs",
    "godot_csharp/scenes/Host/AgentPilot.gd",
    "godot_csharp/scenes/Host/Main.gd",
    "godot_csharp/scenes/Host/PilotNavigation.gd",
    "godot_csharp/scenes/Shared/check/PlayerControllerCheck/Program.cs",
    "godot_csharp/scenes/Shared/check/PlayerControllerCheck/InteractionReceiptCases.cs",
    "godot_csharp/scenes/Shared/check/PlayerControllerCheck/PublicMenuScopeCases.cs",
    "godot_csharp/scenes/Shared/check/PlayerControllerCheck/InformationContextCases.cs",
    "godot_csharp/scenes/Shared/check/PlayerControllerCheck/RouteLearningCases.cs",
]
manifest = {"source": [], "evidence": []}
for name in files:
    source = ROOT / name
    destination = HERE / "source-snapshot" / name
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, destination)
    payload = source.read_bytes()
    manifest["source"].append({"path": name, "sha256": hashlib.sha256(payload).hexdigest(), "bytes": len(payload), "snapshot": str(destination.relative_to(HERE))})
for path in sorted(HERE.glob("*")):
    if path.is_file() and path.suffix in {".log", ".jsonl", ".py", ".gd"}:
        payload = path.read_bytes()
        manifest["evidence"].append({"path": path.name, "sha256": hashlib.sha256(payload).hexdigest(), "bytes": len(payload)})
(HERE / "source-and-evidence-hashes.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
print("Final tested source and original evidence bytes sealed")
