from pathlib import Path
import datetime
import hashlib
import json

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[4]
for metadata in OUT.glob("*.json"):
    record = json.loads(metadata.read_text(encoding="utf-8"))
    if "command" in record:
        digest = hashlib.sha256((OUT / record["log"]).read_bytes()).hexdigest()
        if digest != record["sha256"]:
            raise SystemExit("Evidence changed: " + record["log"])
paths = [
    "godot_csharp/scenes/Shared/check/PlayerControllerCheck/Program.cs",
    "godot_csharp/scenes/Shared/Story/PlayerController.cs",
    "godot_csharp/scenes/Shared/Game/GameSession.cs",
    "godot_csharp/scenes/Host/GameEngine.cs",
    "godot_csharp/scenes/Host/AgentPilot.gd",
    "godot_csharp/scenes/Host/PilotNavigation.gd",
    "godot_csharp/scenes/Shared/Field/WorldView.gd",
    "godot_csharp/scenes/Chapter2/Field/CampusMapView.gd",
    "godot_csharp/tools/check/check_virtual_navigation.gd",
    "godot_csharp/tools/check/check_campus_access.gd",
]
record = {
    "sealed_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "sources": [{"path": path, "sha256": hashlib.sha256((ROOT / path).read_bytes()).hexdigest()} for path in paths],
}
(OUT / "source-hashes.json").write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
manifest = "".join(hashlib.sha256(path.read_bytes()).hexdigest() + "  " + path.name + "\n"
    for path in sorted(OUT.iterdir()) if path.is_file() and path.name != "evidence.sha256")
(OUT / "evidence.sha256").write_bytes(manifest.encode("utf-8"))
print("Evidence hashes verified and written.")
