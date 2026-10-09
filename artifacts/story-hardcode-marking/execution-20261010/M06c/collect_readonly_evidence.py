"""Collect explicit M06c paths without running either asset generator.

Only writes evidence.json next to this file. No engine/editor invocation, import,
production/generated-resource write, project-design read, or Git mutation.
"""

from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path
import re
import struct
import subprocess

from PIL import Image


ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parent
EXPECTED_HEAD = "7489db56a714724eb7cb735958dd879dc662d238"
SOURCES = (
    "godot_csharp/tools/build_breakfast_exchange.gd",
    "godot_csharp/tools/build_story_tiles.gd",
)
READ_DOCS = (
    "godot_csharp/AssetsSource/Internal/Characters/DemoAnimation/breakfast_exchange.md",
    "godot_csharp/AssetsSource/Internal/Props/AcademyTileMap/README.md",
    "godot_csharp/ShareAsset/Internal/Characters/animation_v2/README.md",
    "godot_csharp/ShareAsset/Internal/Props/README.md",
)


def git(*args: str) -> bytes:
    return subprocess.check_output(["git", *args], cwd=ROOT)


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def record(path: str, *, png_header: bool = False) -> dict:
    absolute = ROOT / path
    result = {"path": path, "exists": absolute.is_file()}
    if not result["exists"]:
        return result
    data = absolute.read_bytes()
    result.update(bytes=len(data), sha256=sha(data))
    if png_header:
        if data[:8] != b"\x89PNG\r\n\x1a\n" or data[12:16] != b"IHDR":
            result["png_header_error"] = "Invalid PNG signature/IHDR"
        else:
            w, h, depth, color, compression, filtering, interlace = struct.unpack(
                ">IIBBBBB", data[16:29]
            )
            result["png_ihdr"] = {
                "width": w, "height": h, "bit_depth": depth,
                "color_type": color, "compression": compression,
                "filter_method": filtering, "interlace": interlace,
            }
    return result


head = git("rev-parse", "HEAD").decode().strip()
if head != EXPECTED_HEAD:
    raise SystemExit(f"Unexpected HEAD: {head}; no evidence written")

source_records = []
texts = {}
for path in SOURCES:
    item = record(path)
    data = (ROOT / path).read_bytes()
    text = data.decode("utf-8-sig")
    texts[path] = text
    item["line_count"] = len(text.splitlines())
    head_data = git("show", f"HEAD:{path}")
    item["matches_head_bytes"] = data == head_data
    item["matches_head_after_eol_normalization"] = data.replace(b"\r\n", b"\n") == head_data.replace(b"\r\n", b"\n")
    item["markers"] = [
        {"line": n, "id": match.group(1), "kind": match.group(2), "scope": match.group(3), "text": line}
        for n, line in enumerate(text.splitlines(), 1)
        if (match := re.search(r"STORY-HARDCODE\[([^]]+)\]\[([^]]+)\]\[([^]]+)\]", line))
    ]
    source_records.append(item)

props = "godot_csharp/ShareAsset/Internal/Props/"
source_props = "godot_csharp/AssetsSource/Internal/Props/"
characters = "godot_csharp/AssetsSource/Internal/Characters/DemoAnimation/"
compact = {
    name: (int(w), int(h)) for name, w, h in re.findall(
        r'"([^"]+)": Vector2i\((\d+), (\d+)\)', texts[SOURCES[1]].split("}\n", 1)[0]
    )
}
inputs = [
    characters + "breakfast_exchange.png",
    characters + "breakfast_exchange_between.png",
    source_props + "breakfast_table_clear.png",
    source_props + "breakfast_soup_bowl_source.png",
    source_props + "AcademyTileMap/Academy_Inside_A2.png",
    source_props + "story_parlor_projection.png",
    source_props + "story_classroom_furniture.png",
    *[props + name + ".png" for name in compact],
    *[props + f"shot01_banner_{n}.png" for n in (1, 2, 3)],
]
input_records = [record(path, png_header=True) for path in inputs]
input_by_path = {item["path"]: item for item in input_records}

output_names = [
    *["story_" + name for name in compact],
    "story_soup_bowl", "story_sofa", "story_armchair", "story_tea_table",
    "story_fireplace", "story_parlor_bookcase", "story_parlor_piano",
    "classroom_board", "classroom_lectern", "classroom_desk", "classroom_chair",
    "stone_slabs", "story_banner_1", "story_banner_2", "story_banner_3",
]
outputs = [
    "godot_csharp/ShareAsset/Internal/Characters/animation_v2/breakfast_exchange.png",
    props + "breakfast_table_clear.res",
    props + "breakfast_exchange_bowl.res",
    *[props + name + extension for name in output_names for extension in (".png", ".res")],
]
output_records = [record(path, png_header=path.endswith(".png")) for path in outputs]

checks = []


def grid_check(path: str, columns: int, rows: int):
    header = input_by_path[path].get("png_ihdr")
    ok = header is not None and header["width"] % columns == 0 and header["height"] % rows == 0
    checks.append({"kind": "source_grid_divisible", "path": path, "columns": columns, "rows": rows, "pass": ok})


def crop_check(path: str, name: str, rect: tuple[int, int, int, int]):
    header = input_by_path[path].get("png_ihdr")
    x, y, w, h = rect
    ok = header is not None and min(x, y) >= 0 and min(w, h) > 0 and x + w <= header["width"] and y + h <= header["height"]
    checks.append({"kind": "explicit_crop_inside_source", "path": path, "name": name, "rectangle": list(rect), "pass": ok})


grid_check(characters + "breakfast_exchange.png", 6, 2)
grid_check(characters + "breakfast_exchange_between.png", 2, 2)
grid_check(source_props + "story_parlor_projection.png", 3, 2)
crop_check(source_props + "AcademyTileMap/Academy_Inside_A2.png", "stone_slabs", (288, 432, 96, 96))
crop_check(source_props + "story_parlor_projection.png", "sofa", (100, 120, 445, 285))
crop_check(source_props + "story_parlor_projection.png", "armchair", (650, 120, 250, 285))
for name, rect in (
    ("classroom_board", (0, 0, 800, 627)),
    ("classroom_lectern", (800, 350, 454, 277)),
    ("classroom_desk", (0, 627, 627, 627)),
    ("classroom_chair", (627, 627, 627, 627)),
):
    crop_check(source_props + "story_classroom_furniture.png", name, rect)

audit = json.loads((ROOT / "artifacts/story-hardcode-marking/execution-20261010/issue-map.json").read_text(encoding="utf-8-sig"))
issues = [item for item in audit["items"] if item.get("current_path") in SOURCES]
task_manifest = json.loads((ROOT / "artifacts/story-hardcode-marking/execution-20261010/provided-pack/subagent-task-manifest.json").read_text(encoding="utf-8-sig"))
task = next(item for item in task_manifest["tasks"] if item["id"] == "M06c")

for item in source_records:
    issue = next(issue for issue in issues if issue["current_path"] == item["path"])
    item["matches_issue_map_byte_hash"] = item["sha256"] == issue["file_sha256"]

pixel_diagnostics = []
pose_diagnostics = []


def bbox(image: Image.Image, threshold: int = 192):
    return image.getchannel("A").point(lambda value: 255 if value >= threshold else 0).getbbox()


def positive_round(value: float) -> int:
    return math.floor(value + 0.5)


for path, columns, offset in (
    (characters + "breakfast_exchange.png", 6, 0),
    (characters + "breakfast_exchange_between.png", 2, 6),
):
    with Image.open(ROOT / path) as opened:
        image = opened.convert("RGBA")
    cell_w, cell_h = image.width // columns, image.height // 2
    unused = image.crop((cell_w * columns, 0, image.width, image.height))
    pixel_diagnostics.append({
        "kind": "integer_grid_unused_right_edge", "path": path,
        "unused_columns": image.width - cell_w * columns,
        "maximum_alpha": max(unused.getchannel("A").getdata(), default=0),
        "alpha_nonzero_pixels": sum(value > 0 for value in unused.getchannel("A").getdata()),
        "alpha_ge_192_pixels": sum(value >= 192 for value in unused.getchannel("A").getdata()),
    })
    for row in range(2):
        first = image.crop((0, row * cell_h, cell_w, (row + 1) * cell_h))
        first_bounds = bbox(first)
        factor = 39.0 / (first_bounds[3] - first_bounds[1]) if first_bounds else None
        for col in range(columns):
            part = image.crop((col * cell_w, row * cell_h, (col + 1) * cell_w, (row + 1) * cell_h))
            bounds = bbox(part)
            entry = {"path": path, "row": row, "source_column": col, "atlas_column": col + offset, "cell": [cell_w, cell_h], "alpha_ge_192_bounds": bounds, "row_scale": factor}
            if bounds and factor:
                x0, y0, x1, y1 = bounds
                feet = bbox(part.crop((0, y1 - 20, cell_w, y1)))
                entry["feet_alpha_ge_192_bounds"] = feet
                if feet:
                    anchor_x = feet[0] + (feet[2] - feet[0]) / 2.0
                    width = positive_round((x1 - x0) * factor)
                    height = positive_round((y1 - y0) * factor)
                    local_x = 32 - positive_round((anchor_x - x0) * factor)
                    local_y = 55 - height
                    entry["calculated_size_before_resize"] = [width, height]
                    entry["calculated_cell_position"] = [local_x, local_y]
                    entry["bounding_rectangle_inside_64px_cell"] = min(local_x, local_y) >= 0 and local_x + width <= 64 and local_y + height <= 64
            pose_diagnostics.append(entry)

evidence = {
    "task": "M06c", "schema": "M06c.readonly-evidence.v1", "head": head,
    "branch": git("branch", "--show-current").decode().strip(),
    "isolation": "DETECTION_ONLY: shared host identity and filesystem; no enforced read ACL or protected merge identity.",
    "collection": "Read-only bytes, PNG IHDR, source geometry checks; never execute generators.",
    "source_files": source_records, "direct_source_document_metadata": [record(path) for path in READ_DOCS],
    "task_manifest_snapshot": task, "original_issue_map_records": issues,
    "compact_prop_target_sizes": compact, "direct_input_files": input_records,
    "declared_output_files": output_records, "static_geometry_checks": checks,
    "source_pixel_diagnostics": pixel_diagnostics,
    "source_pose_geometry_diagnostics": pose_diagnostics,
    "summary": {
        "source_files": len(source_records), "markers": sum(len(item["markers"]) for item in source_records),
        "compact_props": len(compact), "direct_inputs": len(input_records), "declared_outputs": len(output_records),
        "missing_inputs": [item["path"] for item in input_records if not item["exists"]],
        "missing_existing_outputs": [item["path"] for item in output_records if not item["exists"]],
        "failed_static_geometry_checks": [item for item in checks if not item["pass"]],
        "source_matches_head": all(item["matches_head_bytes"] for item in source_records),
        "source_matches_head_after_eol_normalization": all(item["matches_head_after_eol_normalization"] for item in source_records),
        "source_matches_issue_map_byte_hash": all(item["matches_issue_map_byte_hash"] for item in source_records),
        "pose_frames_with_nonempty_body_and_feet": sum(bool(item.get("alpha_ge_192_bounds")) and bool(item.get("feet_alpha_ge_192_bounds")) for item in pose_diagnostics),
        "pose_frames_with_computed_rectangle_inside_cell": sum(item.get("bounding_rectangle_inside_64px_cell", False) for item in pose_diagnostics),
    },
    "not_executed": ["asset_generation", "resource_import", "dotnet_build", "automated_regression", "level_1", "level_2", "level_3"],
    "not_proved": ["full_generated_output_pixel_equality", "animation_continuity", "runtime_resource_consumers", "byte_identical_regeneration", "design_compliance", "behavior_reasonableness", "play_experience"],
}
(OUT / "evidence.json").write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps(evidence["summary"], ensure_ascii=False, indent=2))
