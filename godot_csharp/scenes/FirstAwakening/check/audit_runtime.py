"""Audit the latest captured First Awakening furniture and interaction layout.

Run from the repository root with the RPG TileMap MCP virtual environment's
Python. This uses the real stdio MCP server and its read-only audit_layout tool.
Capture the scene first with capture_first_awakening.gd.
"""

import asyncio
import json
import os
import sys
from pathlib import Path

from mcp import Client, StdioServerParameters


ROOT = Path(__file__).resolve().parents[4]
GAME = ROOT / "godot_csharp"
MCP_DIR = GAME / "addons/rpg_asset_adapter/mcp"
SERVER = MCP_DIR / "server.py"
LAYOUT = ROOT / "artifacts/first-awakening/runtime-layout.json"
ARCHITECTURE = GAME / "ShareAsset/Internal/Props/FirstAwakeningInterior/layout.json"
REPORT = ROOT / "artifacts/first-awakening/runtime-spatial-audit.json"
SPAWN = [4, 5]
# Every D passage with its orientation: four dormitory doors, two archways
# from the corridor, two stone flights down into the hall.
DOORS = [{"name": f"R-{index + 5:02d} door", "rect": [5 + 10 * index, 9, 2, 3], "facing": "south"} for index in range(4)] + [
    {"name": "study archway", "rect": [7, 14, 2, 3], "facing": "south"},
    {"name": "pantry archway", "rect": [31, 14, 2, 3], "facing": "south"},
    {"name": "west hall stair", "rect": [16, 25, 2, 3], "facing": "south"},
    {"name": "east hall stair", "rect": [22, 25, 2, 3], "facing": "south"},
]


def read_json(path: Path) -> dict:
    if not path.is_file():
        raise FileNotFoundError(f"Required current scene data is missing: {path}")
    return json.loads(path.read_text(encoding="utf-8"))


def audit_rows(architecture: dict) -> list[str]:
    """The audit knows wall faces as W/w; the hall gate wall is one."""
    return [row.replace("G", "W").replace("g", "w") for row in architecture["rows"]]


async def main() -> None:
    layout = read_json(LAYOUT)
    rows = audit_rows(read_json(ARCHITECTURE))
    footprints = layout.get("footprints", [])
    doors = DOORS
    interaction_points = layout.get("interaction_points", [])

    env = dict(os.environ)
    async with Client(
        StdioServerParameters(command=sys.executable, args=[str(SERVER)], env=env)
    ) as client:
        tools = await client.list_tools()
        if "audit_layout" not in {tool.name for tool in tools.tools}:
            raise RuntimeError("RPG TileMap MCP did not expose audit_layout")
        result = await client.call_tool(
            "audit_layout",
            {
                "rows": rows,
                "spawn": SPAWN,
                "footprints": footprints,
                "doors": doors,
                "interaction_points": interaction_points,
            },
        )
        if result.is_error:
            raise RuntimeError(f"audit_layout failed: {result.content}")
        payload = getattr(result, "structured_content", None)
        if not payload:
            for block in result.content:
                if block.type == "text":
                    payload = json.loads(block.text)
                    break
        if not isinstance(payload, dict):
            raise RuntimeError(f"audit_layout returned no JSON report: {result.content}")

    payload.update(
        {
            "scope": "Latest captured runtime furniture and interaction sites against the painted life-wing layout. Static spatial checks only.",
            "source": str(LAYOUT.relative_to(ROOT)).replace("\\", "/"),
            "architecture": str(ARCHITECTURE.relative_to(ROOT)).replace("\\", "/"),
            "spawn_cell": SPAWN,
            "transport": "Real MCP stdio Client; audit_layout called read-only.",
        }
    )
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(payload, ensure_ascii=False))
    if payload.get("errors"):
        raise SystemExit(1)


if __name__ == "__main__":
    asyncio.run(main())
