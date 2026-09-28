"""Real stdio discovery and recipe parity. Never rewrites current game textures."""
import asyncio
import json
import os
import sys
import struct
import zlib
import tempfile
from pathlib import Path
from PIL import Image
from mcp import Client, StdioServerParameters
import workflow as w
import native_art


def payload(result):
    assert not result.is_error, result
    data = getattr(result, 'structured_content', None)
    if data is not None:
        return data
    return json.loads(next(item.text for item in result.content if item.type == 'text'))


def check_saved_handoff():
    """Read published binary offsets, decompress cels, compare independently with runtime PNGs."""
    source = w.GAME/'AssetsSource/Internal/Props/FirstAwakeningInterior'
    runtime = w.GAME/'ShareAsset/Internal/Props/FirstAwakeningInterior'
    if not (source/'native-handoff.json').exists():
        return {'status':'not exported'}
    manifest=json.loads((source/'native-handoff.json').read_text(encoding='utf-8'))
    matches=0
    for filename in manifest['source_files']:
        path=Path(filename)
        data=path.read_bytes()
        assert int.from_bytes(data[0:4],'little') == len(data)
        assert data[4:6] == b'\xe0\xa5'
        width,height=struct.unpack_from('<HH',data,8)
        assert int.from_bytes(data[12:14],'little') == 32
        assert int.from_bytes(data[14:18],'little') == 1
        cursor=144
        count=int.from_bytes(data[134:136],'little')
        layers=[]
        composite=Image.new('RGBA',(width,height))
        for _ in range(count):
            size,kind=struct.unpack_from('<IH',data,cursor)
            body=data[cursor+6:cursor+size]
            if kind==0x2004:
                n=int.from_bytes(body[16:18],'little')
                layers.append(body[18:18+n].decode('utf-8'))
            elif kind==0x2005:
                index,x,y,opacity,celtype=struct.unpack_from('<HhhBH',body)
                assert 0<=index<len(layers) and opacity==255 and celtype==2
                cw,ch=struct.unpack_from('<HH',body,16)
                rgba=zlib.decompress(body[20:])
                assert len(rgba)==cw*ch*4
                composite.alpha_composite(Image.frombytes('RGBA',(cw,ch),rgba),(x,y))
            cursor+=size
        assert cursor==len(data)
        png=runtime/(path.stem+'.png')
        if png.exists():
            with Image.open(png) as im:
                expected=im.convert('RGBA')
                assert expected.size==composite.size and expected.tobytes()==composite.tobytes(), path
            matches+=1
        else:
            assert path.stem=='room-05-handoff' and len(layers)==13
    atlas_data=json.loads((runtime/'native-props-atlas.json').read_text(encoding='utf-8'))
    with Image.open(runtime/'native-props-atlas.png') as atlas:
        for frame in atlas_data['frames']:
            r=frame['frame']
            crop=atlas.crop((r['x'],r['y'],r['x']+r['w'],r['y']+r['h'])).convert('RGBA')
            with Image.open(runtime/(frame['filename']+'.png')) as im:
                assert crop.tobytes()==im.convert('RGBA').tobytes()
    assert matches==57 and len(atlas_data['frames'])==56
    report=dict(aseprite_files=len(manifest['source_files']),runtime_pixel_matches=matches,
        room_layers=13,atlas_frames=56,aseprite_application='not available; structure/pixel checks only')
    w.save_json(w.REPORT/'native-handoff-check.json',report)
    return report


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
        args=[str(w.HERE/'server.py')],env=dict(os.environ))) as client:
        listing = await client.list_tools()
        names = {tool.name for tool in listing.tools}
        required = {'native_art_catalog','read_native_recipe','render_native_prop','render_native_material','export_native_handoff','export_aseprite',
                    'rebuild_native_interior','native_art_contact_sheet','inspect_pixel_style',
                    'quantize_asset','layered_sprite','review_dialogue','create_plan',
                    'read_plan','update_task','next_task','audit_layout'}
        assert required <= names, required - names
        catalog = payload(await client.call_tool('native_art_catalog', {}))
        assert len(catalog['prop_recipes']) == 37
        recipe = payload(await client.call_tool('read_native_recipe', {'module':'palette'}))
        assert 'WALNUT' in recipe['source']
        rejected = await client.call_tool('read_native_recipe', {'module':'../../private'})
        assert rejected.is_error
        rejected = await client.call_tool('render_native_prop', {'name':'invalid', 'recipe':'bed','parameters':{'room':99}})
        assert rejected.is_error
        inspected = payload(await client.call_tool('inspect_pixel_style', {
            'path':str(w.GAME/'ShareAsset/Internal/Props/FirstAwakeningInterior/desk_0.png')}))
        assert inspected['size'][0] > 0
        parity = payload(await client.call_tool('rebuild_native_interior', {'mode':'verify'}))
        review = payload(await client.call_tool('native_art_contact_sheet', {}))
        handoff = payload(await client.call_tool('export_native_handoff', {})) if '--handoff' in sys.argv else None
        dialogue = payload(await client.call_tool('review_dialogue', {
            'characters':[{'id':'音乐家','want':'拿回桌上的杯子','secret':'昨夜来过门厅','voice':'短句','known_facts':['杯沿缺口']}],
            'turns':[{'speaker':'音乐家','text':'杯子给我。\n别碰缺口。','action':'把杯柄转向自己','claims':['杯沿缺口']}]}))
        assert dialogue['ok'], dialogue
    handoff_check=check_saved_handoff()
    result = dict(transport='real MCP stdio', tools=sorted(names),
        parity=parity, dialogue=dialogue, review=review,handoff=handoff_check,
        status='Protocol/recipe checks only; no new runtime or artistic acceptance claimed.')
    w.save_json(w.REPORT/'native-mcp-check.json',result)
    print(json.dumps({'tools':len(names),'sprites':parity['sprites'],
        'runtime_parity':parity['matches_before_write'],'review':review}, ensure_ascii=False))
    assert parity['matches_before_write'], 'Current runtime differs from the recipe; inspect report, do not overwrite blindly'


if __name__ == '__main__':
    asyncio.run(main())
