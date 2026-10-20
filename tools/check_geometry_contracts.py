"""CI Static Geometry Contract Validator.
Verifies that semantic stations, slots, and interaction sites declared in DV Core (60-game.dv)
have matching geometry markers in Godot .tscn map scenes.
"""
from __future__ import annotations
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MAPS_DIR = ROOT / "godot_csharp" / "scenes" / "Chapter2" / "Maps"
GAME_DV = ROOT / "language_core" / "60-game.dv"

INTERIOR_MAPS = {
    "living_hall", "zone_jia", "zone_yi", "zone_bing", "zone_ding",
    "central_hall", "lecture", "archive", "counterweight", "resonance",
    "clinic", "logistics", "granary", "greenhouse"
}


def parse_tscn_markers(tscn_path: Path) -> dict[str, set[str]]:
    """Extract node markers categorized by parent (Sites, Stations, Spawns, Stands)."""
    text = tscn_path.read_text(encoding="utf-8")
    categories: dict[str, set[str]] = {
        "Sites": set(),
        "Stations": set(),
        "Spawns": set(),
        "Stands": set(),
    }
    node_pattern = re.compile(r'\[node\s+name="([^"]+)"\s+type="Marker2D"\s+parent="([^"]+)"\]')
    for match in node_pattern.finditer(text):
        name, parent = match.group(1), match.group(2)
        if parent in categories:
            categories[parent].add(name)
    return categories


def main() -> int:
    if not GAME_DV.exists():
        print(f"ERROR: {GAME_DV} not found", file=sys.stderr)
        return 1
    if not MAPS_DIR.exists():
        print(f"ERROR: {MAPS_DIR} not found", file=sys.stderr)
        return 1

    # Contracts declared in 60-game.dv
    # (map_key, category, marker_name, description)
    contracts = [
        # Initial anchors (dvg_initial_anchor)
        ("zone_yi", "Sites", "bed", "detective R05 start bed"),
        ("zone_yi", "Sites", "hall_copper_seat", "huo hall copper seat slot"),
        ("zone_yi", "Sites", "hall_tea_side", "shen hall tea side slot"),
        ("zone_yi", "Sites", "hall_window_side", "su hall window side slot"),
        ("living_hall", "Sites", "living_south_table", "quer living hall table slot"),
        ("living_hall", "Sites", "living_piano", "lucian living hall piano slot"),
        # Interaction sites (dvg_interaction)
        ("zone_yi", "Sites", "E1", "isolation gate card E1"),
        ("zone_yi", "Sites", "E2", "wedge and dust card E2"),
        ("zone_yi", "Sites", "E6", "sheet music card E6"),
        ("zone_yi", "Sites", "E7", "slate card E7"),
        ("zone_yi", "Sites", "E10", "jar card E10"),
        ("zone_yi", "Sites", "E11", "bookshelf card E11"),
        ("zone_yi", "Sites", "transmitter", "copper mouthpiece transmitter"),
        ("zone_yi", "Sites", "zone_rules", "zone rules notice board"),
        # Living hall interaction sites
        ("living_hall", "Sites", "watch_point", "living hall watch point"),
        ("living_hall", "Sites", "hall_notice", "living hall notice board"),
        ("living_hall", "Sites", "fireplace", "living hall fireplace"),
    ]

    map_markers: dict[str, dict[str, set[str]]] = {}
    errors: list[str] = []

    for map_key, cat, marker, desc in contracts:
        if map_key not in map_markers:
            tscn_file = MAPS_DIR / f"{map_key}.tscn"
            if not tscn_file.exists():
                errors.append(f"Map scene not found: {tscn_file}")
                continue
            map_markers[map_key] = parse_tscn_markers(tscn_file)

        markers = map_markers[map_key]
        present = marker in markers.get(cat, set()) or marker in markers.get("Stations", set()) or marker in markers.get("Sites", set())
        if not present:
            errors.append(f"Missing marker '{marker}' in map '{map_key}' ({desc})")

    # Verify Stands exist in all interior room maps
    for room_key in sorted(INTERIOR_MAPS):
        tscn_file = MAPS_DIR / f"{room_key}.tscn"
        if not tscn_file.exists():
            errors.append(f"Interior map scene not found: {tscn_file}")
            continue
        if room_key not in map_markers:
            map_markers[room_key] = parse_tscn_markers(tscn_file)
        stands = map_markers[room_key].get("Stands", set())
        if not stands:
            errors.append(f"Interior map '{room_key}' has no Stands markers for character placement fallback")

    if errors:
        print(f"FAILED: {len(errors)} geometry contract violation(s) found:", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print(f"SUCCESS: All {len(contracts)} geometry contracts and {len(INTERIOR_MAPS)} interior stand networks verified.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
