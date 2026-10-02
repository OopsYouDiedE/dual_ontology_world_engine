"""Smoke test over real stdio: python tools/deduction_mcp/check.py"""
import asyncio
import json
import sys
from pathlib import Path

from mcp import Client, StdioServerParameters

HERE = Path(__file__).resolve().parent


def data(result):
    if result.is_error:
        raise AssertionError(result.content[0].text)
    return result.structured_content or json.loads(result.content[0].text)


async def main():
    server = StdioServerParameters(command=sys.executable, args=[str(HERE / "server.py")])
    async with Client(server) as client:
        names = {t.name for t in (await client.list_tools()).tools}
        need = {"case_contract", "list_cases", "read_case", "audit_case", "diagnose_hypothesis",
                "truth_identifiability", "search_alternative_histories", "preview_case_patch",
                "apply_case_patch", "create_case"}
        assert need <= names, need - names
        cases = data(await client.call_tool("list_cases", {}))
        cases = cases.get("result", cases)
        assert any(c["id"] == "demo_door_case" and c["valid"] for c in cases)
        rep = data(await client.call_tool("audit_case", {"case_id": "demo_door_case"}))
        kinds = {r["round"]: r["kind"] for r in rep["rounds"]}
        assert kinds["R1"] == "refute" and kinds["R6"] == "answer", kinds
        assert rep["hypotheses"]["TRUE"]["status"] == "compatible"
        assert all(r["status"] != "compatible" for h, r in rep["hypotheses"].items() if h != "TRUE")
        d = data(await client.call_tool("diagnose_hypothesis", {
            "case_id": "demo_door_case", "round_id": "R2",
            "steps": [{"use": "NIGHT_A"}, {"use": "RULE3"}, ["student_exit_carry", "侦探"], ["pin_in", "侦探"],
                      ["bar_down"], ["engineer_today"], ["finish"]]}))
        assert any(p["evidence"] == "spokes4_over_wipe" for p in d["next_pointer"]), d
        prev = data(await client.call_tool("preview_case_patch", {
            "case_id": "demo_door_case",
            "ops": [{"op": "set", "path": "/evidence/rooms_cleared/available", "value": "R2"}]}))
        assert "R2" in json.dumps(prev["diff"]["resolved_findings"], ensure_ascii=False), prev["diff"]
        bad = await client.call_tool("apply_case_patch", {"case_id": "demo_door_case", "ops": [],
                                                          "expected_sha256": "0" * 64})
        assert bad.is_error, "sha guard failed"
        print(json.dumps({"tools": len(names), "audit": rep["summary"], "rounds": kinds,
                          "preview_resolves": prev["diff"]["resolved_findings"]}, ensure_ascii=False))


asyncio.run(main())
