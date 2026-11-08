"""Audit actor fixed-room target interactions against authored presentation.

Audits whether interaction focuses point to published room components or co-located characters.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]


def inspect(view: dict, view_path: str = "") -> dict:
    presentation = view.get("presentation", {})
    rooms = presentation.get("rooms", {})
    focus_bindings = presentation.get("focus_bindings", {})

    missing = []
    warnings = []
    rooms_missing_visible_door = []

    # Check each published room has a door
    for room_id, room_data in rooms.items():
        components = room_data.get("components", [])
        has_door = any(
            comp.get("kind") in ("door", "portal", "exit", "doorway")
            or "door" in comp.get("asset_id", "").lower()
            for comp in components
        )
        if not has_door and not room_data.get("is_outdoor", False):
            rooms_missing_visible_door.append(room_id)

    # Check actor action focuses
    actors = view.get("declaration", {}).get("actors", [])
    for actor in actors:
        for action in actor.get("actions", []):
            focus = action.get("focus")
            room = action.get("room") or action.get("location")
            if not focus or not room:
                continue

            # If focus is a known binding or character or component
            binding = focus_bindings.get(focus)
            room_info = rooms.get(room, {})
            room_comps = [c.get("entity_id") or c.get("id") for c in room_info.get("components", [])]

            if not binding and focus not in room_comps and focus != actor.get("id"):
                missing.append({
                    "room": room,
                    "focus": focus,
                    "actions": [action.get("id", "")]
                })

    return {
        "missing": missing,
        "warnings": warnings,
        "rooms_missing_visible_door": rooms_missing_visible_door,
        "scope": "actor_fixed_room_targets_presentation_audit",
    }


def main():
    parser = argparse.ArgumentParser(description="Audit actor targets against presentation")
    parser.add_argument("--view", required=True)
    parser.add_argument("--output", default="focus-audit.json")
    args = parser.parse_args()

    vpath = Path(args.view)
    if not vpath.is_absolute():
        vpath = ROOT / vpath
    with open(vpath, "r", encoding="utf-8") as f:
        data = json.load(f)

    result = inspect(data, str(vpath))
    opath = Path(args.output)
    if not opath.is_absolute():
        opath = ROOT / opath
    opath.parent.mkdir(parents=True, exist_ok=True)
    with open(opath, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=2)

    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
