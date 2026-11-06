#!/usr/bin/env python3
"""
tools/ci/verify_repo_hygiene.py
Dual-Ontology World Engine Repository Hygiene and Anti-Rot Gate.
Enforces the 6 hard rules defined in goal.md.
"""

import os
import re
import sys
import yaml
from pathlib import Path

def main():
    root = Path(__file__).resolve().parent.parent.parent
    errors = []

    print(f"=== Running Repo Hygiene Gate on {root} ===")

    # Rule 1: Blacklist directories check
    blacklist_dirs = [
        "godot_csharp",
        "dv_core",
        "scenes_temp",
        "artifacts",
        "scenes/components/maps",
        "scenes/components/maps3d",
        "tools/web_building_editor",
    ]
    for b_dir in blacklist_dirs:
        target = root / b_dir
        if target.exists():
            errors.append(f"[Rule 1 Violation] Blacklisted directory exists: {b_dir}")

    # Rule 2: assets/ purity guard
    assets_dir = root / "assets"
    if assets_dir.exists():
        for ext in [".gd", ".cs", ".tscn"]:
            for p in assets_dir.rglob(f"*{ext}"):
                errors.append(f"[Rule 2 Violation] Impure asset found in assets/: {p.relative_to(root)}")

    # Rule 3: Rooms cannot directly reference raw .glb
    rooms_dir = root / "scenes" / "rooms"
    if rooms_dir.exists():
        glb_ext_res_pattern = re.compile(r'type="PackedScene"[^>]*path="res://assets/meshes/[^"]+\.glb"', re.IGNORECASE)
        for tscn in rooms_dir.glob("*.tscn"):
            try:
                content = tscn.read_text(encoding="utf-8")
                if glb_ext_res_pattern.search(content):
                    errors.append(f"[Rule 3 Violation] Room {tscn.relative_to(root)} directly references raw .glb without a smart prefab wrapper!")
            except Exception as e:
                errors.append(f"Error reading {tscn}: {e}")

    # Rule 4: 1:1 UI scene pairing (<= 12 pairs) and no orphan scripts in prefabs
    ui_dir = root / "scenes" / "UIpanel"
    if ui_dir.exists():
        tscn_files = list(ui_dir.rglob("*.tscn"))
        gd_files = list(ui_dir.rglob("*.gd"))
        if len(tscn_files) > 12:
            errors.append(f"[Rule 4 Violation] UIpanel has {len(tscn_files)} .tscn files (maximum allowed is 12)!")
        tscn_stems = {p.stem for p in tscn_files}
        gd_stems = {p.stem for p in gd_files}
        for s in tscn_stems - gd_stems:
            errors.append(f"[Rule 4 Violation] UI scene without matching .gd: {s}.tscn")
        for s in gd_stems - tscn_stems:
            errors.append(f"[Rule 4 Violation] UI script without matching .tscn: {s}.gd")

    prefabs_dir = root / "scenes" / "prefabs"
    if prefabs_dir.exists():
        orphan_gds = list(prefabs_dir.rglob("*.gd"))
        if orphan_gds:
            for g in orphan_gds:
                errors.append(f"[Rule 4 Violation] Orphan .gd found in prefabs/: {g.relative_to(root)}")

    # Rule 5: res:// dead-link static scan
    res_pattern = re.compile(r'res://([a-zA-Z0-9_\-/\.\[\]]+)')
    scan_exts = {".cs", ".gd", ".tscn", ".dv"}
    for root_scan in [root / "scenes", root / "core", root / "config", root / "tools"]:
        if not root_scan.exists():
            continue
        for file_path in root_scan.rglob("*"):
            if file_path.suffix in scan_exts and file_path.is_file():
                try:
                    text = file_path.read_text(encoding="utf-8", errors="ignore")
                    for match in res_pattern.finditer(text):
                        rel_path = match.group(1).rstrip(",;)\"\'}")
                        if not rel_path:
                            continue
                        # Skip dynamic string templates if any
                        if "{" in rel_path or "$" in rel_path or "%" in rel_path:
                            continue
                        resolved = root / rel_path
                        if not resolved.exists():
                            # Ignore .uid checks or wildcards
                            if not rel_path.endswith((".uid", ".import")):
                                errors.append(f"[Rule 5 Dead Link] In {file_path.relative_to(root)}: 'res://{rel_path}' does not exist on disk!")
                except Exception as e:
                    pass

    # Rule 6: @mechanism contract bidirectional verification
    mechanism_map_file = root / "MECHANISM_MAP.yaml"
    if mechanism_map_file.exists():
        try:
            with open(mechanism_map_file, "r", encoding="utf-8") as f:
                map_data = yaml.safe_load(f)
            mechanisms = map_data.get("mechanisms", {})
            registered_keys = set(mechanisms.keys())
            
            # Check all paths in map exist
            for m_key, m_val in mechanisms.items():
                for p_key in ["csharp_authority", "godot_presenters"]:
                    for item_path in m_val.get(p_key, []):
                        if not (root / item_path).exists():
                            errors.append(f"[Rule 6 Inconsistency] MECHANISM_MAP.yaml lists missing file: {item_path} under {m_key}")
                if "workbench_tool" in m_val:
                    wb = m_val["workbench_tool"]
                    if not (root / wb).exists():
                        errors.append(f"[Rule 6 Inconsistency] MECHANISM_MAP.yaml lists missing workbench_tool: {wb}")

            # Check core/ .cs tags
            core_dir = root / "core"
            if core_dir.exists():
                mechanism_tag_pattern = re.compile(r'//\s*@mechanism:\s*(\w+)')
                for cs_file in core_dir.rglob("*.cs"):
                    if "obj" in cs_file.parts or "bin" in cs_file.parts:
                        continue
                    text = cs_file.read_text(encoding="utf-8", errors="ignore")
                    match = mechanism_tag_pattern.search(text)
                    if match:
                        tag = match.group(1)
                        if tag not in registered_keys:
                            errors.append(f"[Rule 6 Tag Violation] {cs_file.relative_to(root)} has unregistered @mechanism: {tag}")
                    else:
                        errors.append(f"[Rule 6 Missing Tag] {cs_file.relative_to(root)} missing // @mechanism: header tag")
        except Exception as e:
            errors.append(f"[Rule 6 Parse Error] Error checking MECHANISM_MAP.yaml: {e}")

    if errors:
        print(f"FAILED: Found {len(errors)} hygiene violations:")
        for err in errors[:30]:
            print("  -", err)
        if len(errors) > 30:
            print(f"  ... and {len(errors) - 30} more violations.")
        return 1
    else:
        print("SUCCESS: All repo hygiene checks passed!")
        return 0

if __name__ == "__main__":
    sys.exit(main())
