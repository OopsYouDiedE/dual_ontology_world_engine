"""Build the playable First Awakening architecture through the RPG TileMap MCP.

Run with the MCP's virtualenv Python from the repository root. The generated
TileMapWorkflow family is the architecture source used by FirstAwakening.tscn.
"""

import asyncio
import hashlib
import os
import shutil
import sys
import random
from pathlib import Path

from mcp import Client, StdioServerParameters


ROOT = Path(__file__).resolve().parents[2]
GAME = ROOT / "godot_csharp"
SERVER = GAME / "addons/rpg_asset_adapter/mcp/server.py"
MATERIALS = "res://ShareAsset/Internal/Props/TileMapWorkflow"
STUDY_A4 = GAME / "ShareAsset/Internal/Props/ReasoningStudy/Study_A4.png"
STUDY_A5 = GAME / "ShareAsset/Internal/Props/ReasoningStudy/Study_A5.png"
SOURCE_FAMILY = GAME / "AssetsSource/Internal/Props/TileMapWorkflow"
WIDTH, HEIGHT = 40, 32


def stage_material_source(source: Path, name: str) -> Path:
    """Keep an immutable, traceable copy in this workflow's source family."""
    staged = SOURCE_FAMILY / name
    if staged.exists():
        if hashlib.sha256(staged.read_bytes()).digest() != hashlib.sha256(source.read_bytes()).digest():
            raise RuntimeError(f"Refusing to replace a different workflow source: {staged}")
    else:
        shutil.copyfile(source, staged)
    return staged


def blueprint() -> list[str]:
    floor: set[tuple[int, int]] = set()
    for left, top, width, height in [
        (1, 3, 8, 6), (11, 3, 8, 6), (21, 3, 8, 6), (31, 3, 8, 6),
        (5, 9, 2, 1), (15, 9, 2, 1), (25, 9, 2, 1), (35, 9, 2, 1),
        (1, 10, 38, 4), (7, 14, 2, 1), (31, 14, 2, 1),
        (1, 15, 18, 10), (21, 15, 18, 10),
        (16, 25, 2, 1), (22, 25, 2, 1), (16, 26, 8, 5),
    ]:
        floor.update((x, y) for y in range(top, top + height) for x in range(left, left + width))
    rows = [[" " for _ in range(WIDTH)] for _ in range(HEIGHT)]
    for x, y in floor:
        rows[y][x] = "."
    for x, y in floor:
        # Include diagonal neighbours: a four-neighbour outline leaves a void
        # at convex room corners and breaks the continuous wall cap.
        for dx, dy in [(dx,dy) for dy in (-1,0,1) for dx in (-1,0,1) if dx or dy]:
            nx, ny = x + dx, y + dy
            if 0 <= nx < WIDTH and 0 <= ny < HEIGHT and (nx, ny) not in floor:
                rows[ny][nx] = "#"
    for x in range(WIDTH):
        rows[0][x] = "#"
        inside_dorm = any(left <= x < left+8 for left in (1,11,21,31))
        rows[1][x] = "W" if inside_dorm else "#"
        rows[2][x] = "w" if inside_dorm else "#"
    for left, top, width in [
        (5, 9, 2), (15, 9, 2), (25, 9, 2), (35, 9, 2),
        (7, 14, 2), (31, 14, 2), (16, 25, 2), (22, 25, 2),
    ]:
        for x in range(left, left + width):
            rows[top][x] = "D"
    # The northern wall of each southern bedroom faces into the room. Keep the
    # existing row-14 cap and door cells; extend each 64px opening through both
    # wall-face rows so walkable bedroom floor begins at row 17.
    for left, right, doorway in [(1, 19, {7, 8}), (21, 39, {31, 32})]:
        for x in range(left, right):
            rows[15][x] = "D" if x in doorway else "W"
            rows[16][x] = "D" if x in doorway else "w"
    # The dormitory south wall faces the corridor. Its cap is row 9;
    # the 64px facade below it carries the full-height door, not a loose sprite.
    dorm_doors = {5,6,15,16,25,26,35,36}
    for x in range(1,39):
        rows[10][x] = "D" if x in dorm_doors else "W"
        rows[11][x] = "D" if x in dorm_doors else "w"
    # The shared rooms have the same outward-facing south elevation. These
    # faces extend into existing void, leaving the two hall passages clear.
    for left,right in [(1,15),(25,39)]:
        for x in range(left,right):
            rows[26][x] = "W"
            rows[27][x] = "w"
        for x in (left-1,right):
            rows[26][x] = "#"
            rows[27][x] = "#"
    return ["".join(row) for row in rows]


async def call(client: Client, name: str, args: dict) -> None:
    result = await client.call_tool(name, args)
    if result.is_error:
        raise RuntimeError(f"{name}: {result.content}")
    print(f"{name}: OK", flush=True)


async def main() -> None:
    env = dict(os.environ)
    env.setdefault(
        "GODOT_BIN",
        "C:/Users/iii/Desktop/Godot_v4.6.1-stable_mono_win64/"
        "Godot_v4.6.1-stable_mono_win64_console.exe",
    )
    a4_source = stage_material_source(STUDY_A4, "study_a4_material.png")
    a5_source = stage_material_source(STUDY_A5, "study_a5_material.png")
    async with Client(StdioServerParameters(command=sys.executable, args=[str(SERVER)], env=env)) as client:
        await call(client, "tilemap_workflow", {})
        # Preserve and inspect historical study-room material sources. Current
        # terrain is drawn from the native 32px commands below, not those crops.
        await call(client, "inspect_image", {"path": str(a4_source), "native_cell": 32})
        await call(client, "inspect_image", {"path": str(a5_source), "native_cell": 32})
        # Scene art direction: quiet mineral plaster, narrow timber dado and
        # low-contrast parquet. Recipes are native 32px editable source, not
        # resized room paintings. Historical source copies remain archived.
        palettes = {
            "wall": {"base":"#202b2c", "grain":"#263132", "light":"#2c3838", "dark":"#1e282a"},
            "floor": {"base":"#5d584b", "grain":"#656050", "light":"#6d6756", "dark":"#504d43"},
            "face": {"base":"#465454", "grain":"#4a5857", "light":"#53615e", "dark":"#414e4e"},
            "face_lower": {"base":"#625f50", "grain":"#706c59", "light":"#89836b", "dark":"#41473f"},
        }
        for name, palette in palettes.items():
            rng = random.Random(27)
            commands = [{"op":"rect", "xy":[0,0,31,31], "color":"base"}]
            for _ in range(22):
                commands.append({"op":"point", "xy":[rng.randrange(32),rng.randrange(32)], "color":rng.choice(["grain","dark"])})
            if name == "floor":
                for y in [7,15,23,31]:
                    commands.append({"op":"line","xy":[0,y,31,y],"color":"dark"})
                for x,y in [(7,0),(23,8),(15,16),(31,24)]:
                    commands.append({"op":"line","xy":[x,y,x,y+6],"color":"dark"})
                for y in [3,11,19,27]:
                    commands.append({"op":"line","xy":[5,y,18,y],"color":"grain"})
            elif name == "face":
                # Mineral/plaster mottling: no regular bright brick outlines.
                for _ in range(65):
                    x,y = rng.randrange(32),rng.randrange(32)
                    commands.append({"op":"rect","xy":[x,y,min(31,x+rng.randrange(1,4)),min(31,y+rng.randrange(1,3))],"color":rng.choice(["grain","dark","base"])})
            elif name == "face_lower":
                for x in range(1,32,4):
                    commands += [{"op":"line","xy":[x,5,x,25],"color":"dark"}, {"op":"line","xy":[x+1,5,x+1,25],"color":"grain"}]
                for y,color in [(0,"dark"),(1,"light"),(2,"grain"),(3,"dark"),(27,"light"),(28,"grain"),(29,"dark"),(31,"dark")]:
                    commands.append({"op":"line","xy":[0,y,31,y],"color":color})
            await call(client,"draw_asset",{"name":name,"width":32,"height":32,"palette":palette,"commands":commands,"replace":True})
            await call(client, "adapt_asset", {
                "name": name,
                "source": str(SOURCE_FAMILY / f"{name}.png"),
                "crop": [0,0,32,32],
                "target_size": [32, 32],
                "native_cell": 32,
                "mode": "tile",
            })
        for name in ["wall", "floor", "face", "face_lower"]:
            await call(client, "inspect_image", {"path": f"{MATERIALS}/{name}.png", "native_cell": 32})
        await call(client, "build_tiles", {
            "wall": f"{MATERIALS}/wall.png",
            "floor": f"{MATERIALS}/floor.png",
            "face": f"{MATERIALS}/face.png",
            "face_lower": f"{MATERIALS}/face_lower.png",
            "trim": ["#111d22", "#28373a", "#65736b", "#3f514f"],
        })
        doors = [dict(name=f"bedroom-{i+1}",rect=[x,9,2,3],facing="south")
                 for i,x in enumerate([5,15,25,35])]
        doors += [dict(name="study",rect=[7,14,2,3],facing="south"),
                  dict(name="preparation",rect=[31,14,2,3],facing="south"),
                  dict(name="hall-west",rect=[16,25,2,1],facing="south"),
                  dict(name="hall-east",rect=[22,25,2,1],facing="south")]
        # Gameplay furniture remains owned by FirstAwakening. Its actual colliders
        # must be supplied to audit_layout separately; this export is architecture.
        await call(client, "compose_map", {"rows": blueprint(), "spawn": [4, 6], "props": [], "doors": doors})
        await call(client, "export_godot", {})


if __name__ == "__main__":
    asyncio.run(main())
