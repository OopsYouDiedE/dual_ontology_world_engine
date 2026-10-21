"""Engineering checks for current MCP discovery and ownership protection of art sources."""
import asyncio
import json
import os
import sys
import tempfile
from pathlib import Path
from mcp import Client, StdioServerParameters
import workflow as w
import native_art

async def adapter(client, operation, arguments):
    return await client.call_tool('rpg_adapter', {'operation': operation, 'arguments': arguments})


def payload(result):
    assert not result.is_error, result
    data = getattr(result, 'structured_content', None)
    if data is not None:
        return data
    return json.loads(next(item.text for item in result.content if item.type == 'text'))




def check_source_protection():
    original_source,original_output=native_art.SOURCE,native_art.OUTPUT
    try:
        with tempfile.TemporaryDirectory(prefix='native-source-check-') as directory:
            native_art.SOURCE=Path(directory)/'source'
            native_art.OUTPUT=Path(directory)/'runtime'
            native_art.SOURCE.mkdir()
            historical=native_art.SOURCE/'interior-sheet.png'
            historical.write_bytes(b'historical original sentinel')
            try:
                native_art.render_native_prop('interior-sheet','bed',{'room':0},replace=True)
            except ValueError:
                pass
            else:
                raise AssertionError('Historical source was overwritten')
            assert historical.read_bytes()==b'historical original sentinel'
            generated=native_art.render_native_prop('test-bed','bed',{'room':0})
            source=Path(generated['source'])
            source.write_bytes(b'artist edit sentinel')
            try:
                native_art.render_native_prop('test-bed','bed',{'room':0},replace=True)
            except ValueError:
                pass
            else:
                raise AssertionError('Artist edit was overwritten')
            assert source.read_bytes()==b'artist edit sentinel'
    finally:
        native_art.SOURCE,native_art.OUTPUT=original_source,original_output


async def main():
    check_source_protection()
    async with Client(StdioServerParameters(command=sys.executable,
        args=[str(w.REPO/'mcp/server.py')],env=dict(os.environ))) as client:
        listing = await client.list_tools()
        names = {tool.name for tool in listing.tools}
        assert 'rpg_adapter' in names, names
        catalog = payload(await adapter(client, 'native_art_catalog', {}))
        assert len(catalog['prop_recipes']) == 37
        recipe = payload(await adapter(client, 'read_native_recipe', {'module':'palette'}))
        assert 'WALNUT' in recipe['source']
        rejected = await adapter(client, 'read_native_recipe', {'module':'../../private'})
        assert rejected.is_error
        rejected = await adapter(client, 'render_native_prop', {'name':'invalid', 'recipe':'bed','parameters':{'room':99}})
        assert rejected.is_error
        inspected = payload(await adapter(client, 'inspect_pixel_style', {
            'path':str(w.GAME/'ShareAsset/Internal/Props/FirstAwakeningInterior/desk_0.png')}))
        assert inspected['size'][0] > 0
        review = payload(await adapter(client, 'native_art_contact_sheet', {}))
        dialogue = payload(await adapter(client, 'review_dialogue', {
            'characters':[{'id':'音乐家','want':'拿回桌上的杯子','secret':'昨夜来过门厅','voice':'短句','known_facts':['杯沿缺口']}],
            'turns':[{'speaker':'音乐家','text':'杯子给我。\n别碰缺口。','action':'把杯柄转向自己','claims':['杯沿缺口']}]}))
        assert dialogue['ok'], dialogue
    result = dict(transport='real MCP stdio', tools=sorted(names),
        dialogue=dialogue, review=review,
        status='Protocol/recipe checks only; no new runtime or artistic acceptance claimed.')
    w.save_json(w.REPORT/'native-mcp-check.json',result)
    print(json.dumps({'registered_tools':sorted(names), 'review':review,
        'scope':'MCP transport and source ownership only; no game session or rendered-scene acceptance'}, ensure_ascii=False))



if __name__ == '__main__':
    asyncio.run(main())
