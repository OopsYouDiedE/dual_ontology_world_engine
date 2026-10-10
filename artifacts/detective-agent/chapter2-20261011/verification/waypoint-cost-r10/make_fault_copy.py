"""Create an isolated counterfactual with the former full-trip waypoint cost."""
from pathlib import Path
import hashlib
import json

ROOT = Path(__file__).resolve().parents[5]
OUT = Path(__file__).resolve().parent
TEMP = ROOT / "godot_csharp/tmp/waypoint-cost-r10"
TEMP.mkdir(parents=True, exist_ok=True)
navigation = (ROOT / "godot_csharp/scenes/Host/PilotNavigation.gd").read_bytes()
needle = b'"travel_distance": distance'
assert navigation.count(needle) == 1
counterfactual = navigation.replace(needle, b'"travel_distance": travel_distance(field, point, reach, known)')
fixture = (ROOT / "godot_csharp/tools/check/check_virtual_navigation.gd").read_bytes().replace(
    b"res://scenes/Host/PilotNavigation.gd", b"res://tmp/waypoint-cost-r10/overcharged_navigation.gd")
outputs = {"overcharged_navigation.gd": counterfactual, "overcharged_fixture.gd": fixture}
for name, content in outputs.items():
    (OUT / name).write_bytes(content)
    (TEMP / name).write_bytes(content)
(OUT / "fault-source-hashes.json").write_text(json.dumps(
    {name: hashlib.sha256(content).hexdigest() for name, content in outputs.items()}, indent=2) + "\n", encoding="utf-8")
print("Created isolated counterfactual without changing production files")
