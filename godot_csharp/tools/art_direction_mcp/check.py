"""Smoke test over real stdio: python godot_csharp/tools/art_direction_mcp/check.py"""
import asyncio
import json
import sys
from pathlib import Path

from mcp import Client, StdioServerParameters

HERE = Path(__file__).resolve().parent
PROPS = "godot_csharp/ShareAsset/Internal/Props/FirstAwakeningInterior"


def data(result):
    if result.is_error:
        raise AssertionError(result.content[0].text)
    return result.structured_content or json.loads(result.content[0].text)


async def main():
    async with Client(StdioServerParameters(command=sys.executable, args=[str(HERE / "server.py")])) as client:
        names = {t.name for t in (await client.list_tools()).tools}
        assert {"art_spec_catalog", "art_brief", "validate_png", "validate_directory"} <= names, names
        cat = data(await client.call_tool("art_spec_catalog", {}))
        assert cat["kinds"]["character_atlas"]["frames"] == [1, 8, 3, 4, 4, 4, 6, 7] and cat["conflicts"]
        b = data(await client.call_tool("art_brief", {"kind": "prop", "name": "umbrella_stand",
                                                      "purpose": "门厅空伞架", "ramps": ["BRASS"], "similar_to": "coat_stand"}))
        assert b["ready"] and b["canvas"] == [18, 50] and b["must_not"], b
        ok = data(await client.call_tool("validate_png", {"path": f"{PROPS}/bed_0.png", "kind": "prop"}))
        assert ok["verdict"] == "pass", ok
        bad = data(await client.call_tool("validate_png", {"path": f"{PROPS}/bed_0.png", "kind": "character_atlas"}))
        assert bad["verdict"] == "fail" and bad["failed"][0].get("fix"), bad
        esc = await client.call_tool("validate_png", {"path": "../outside.png", "kind": "prop"})
        assert esc.is_error, "path guard failed"
        d = data(await client.call_tool("validate_directory", {
            "directory": "godot_csharp/ShareAsset/Internal/Characters/animation_v2", "kind": "character_atlas",
            "exclude": ["breakfast*"]}))
        assert d["counts"]["fail"] == 0, d["counts"]
        print(json.dumps({"tools": sorted(names), "atlas_dir": d["counts"]}, ensure_ascii=False))


asyncio.run(main())
