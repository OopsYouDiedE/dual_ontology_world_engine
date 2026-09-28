"""Exercise narrative review through the registered stdio MCP transport."""
import asyncio
import json
import os
import sys
from pathlib import Path

from mcp import Client, StdioServerParameters

def payload(result):
    assert not result.is_error, result
    return result.structured_content or json.loads(next(item.text for item in result.content if item.type == 'text'))


async def main():
    server = Path(__file__).with_name('server.py')
    async with Client(StdioServerParameters(command=sys.executable, args=[str(server)], env=dict(os.environ))) as client:
        tools = {tool.name for tool in (await client.list_tools()).tools}
        required = {'story_scene_design_contract', 'inventory_story_project',
                    'audit_story_presentation', 'audit_story_project'}
        assert required <= tools, required - tools
        contract = payload(await client.call_tool('story_scene_design_contract', {}))
        inventory = payload(await client.call_tool('inventory_story_project', {}))
        audit = payload(await client.call_tool('audit_story_presentation', {}))
        project = payload(await client.call_tool('audit_story_project', {}))
        assert contract['scene_template']['beats'][0]['player_action'] == ''
        assert any(item['path'] == audit['path'] for item in inventory['files'])
        assert any(section['label'] == 'mind_intro' and section['candidates'] for section in audit['sections'])
        assert any(section['label'] == 'transmitter' and section['choices'] for section in audit['sections'])
        assert len(project['audits']) == len(inventory['files'])
        assert (await client.call_tool('audit_story_presentation', {'path': '../README.md'})).is_error
        print(f"MCP story review OK: {len(tools)} tools, {len(inventory['files'])} dialogue files, "
              f"{len(audit['sections'])} First Awakening sections, {audit['candidate_count']} review candidates")


if __name__ == '__main__':
    asyncio.run(main())
