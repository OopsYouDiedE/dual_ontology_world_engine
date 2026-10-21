"""Real stdio discovery/calls and spatial regressions. Run after the current map rebuild."""
import asyncio
import json
import sys
import os
from mcp import Client, StdioServerParameters
from PIL import Image
import workflow as w

async def adapter(client, operation, arguments):
    return await client.call_tool('rpg_adapter', {'operation': operation, 'arguments': arguments})

async def main():
    async with Client(StdioServerParameters(command=sys.executable,args=[str(w.REPO/'mcp/server.py')],env=dict(os.environ))) as client:
        listed=await client.list_tools()
        names={tool.name for tool in listed.tools}
        assert 'rpg_adapter' in names, names
        result=await adapter(client,'tilemap_workflow',{})
        assert not result.is_error
        inspected=await adapter(client,'inspect_image',dict(path=str(w.OUTPUT/'atlas.png'),native_cell=32))
        assert not inspected.is_error
        reviewed=await adapter(client,'audit_layout',dict(rows=['#####','#...#','##D##','#...#','#####'],spawn=[2,1],
            doors=[dict(name='narrow door',rect=[2,2,1,1],facing='south')],
            interaction_points=[dict(name='far room',position=[80,124],radius=16)]))
        assert not reviewed.is_error, reviewed
        bad=await adapter(client,'adapt_asset',dict(name='../../escape',source=str(w.OUTPUT/'atlas.png'),crop=[0,0,32,32],target_size=[32,32],native_cell=32))
        assert bad.is_error, 'Path traversal must fail'
        bad=await adapter(client,'adapt_asset',dict(name='invalid',source=str(w.OUTPUT/'atlas.png'),crop=[9999,0,32,32],target_size=[32,32],native_cell=32))
        assert bad.is_error, 'Out-of-source crop must fail, not silently pad'
        exported=await adapter(client,'export_godot',{})
        assert not exported.is_error, exported
    manifest=json.loads((w.OUTPUT/'tiles.json').read_text())
    assert len({t['mask'] for t in manifest['tiles'] if t['terrain']==0})==47
    atlas=Image.open(w.OUTPUT/'atlas.png').convert('RGBA')
    def pixel(mask,x,y):
        tile=next(t for t in manifest['tiles'] if t['terrain']==0 and t['mask']==mask)
        return atlas.getpixel((tile['atlas'][0]*32+x,tile['atlas'][1]*32+y))
    # Independent cases: filled block has no border; missing NW diagonal makes an inner corner;
    # isolated wall has outside trim. Test the actual rendered pixels, not only mask numbers.
    material=Image.open(w.OUTPUT/'wall.png').convert('RGBA')
    assert pixel(255,0,0)==material.getpixel((0,0))
    assert pixel(127,0,0)==w.rgba(manifest['trim'][0])
    assert pixel(127,8,8)==material.getpixel((8,8))
    assert pixel(0,0,16)==w.rgba(manifest['trim'][0])
    rows=['#######','#.....#','#.....#','#.....#','#######']
    narrow=w.audit_layout(['#####','#...#','##D##','#...#','#####'],[2,1],
        doors=[dict(name='narrow',rect=[2,2,1,1],facing='south')],
        interaction_points=[dict(name='far room',position=[80,124],radius=16)])
    assert not narrow['errors'], narrow
    clear=w.audit_layout(rows,[1,1],interaction_points=[dict(name='far side',position=[176,92],radius=16)])
    assert not clear['errors']
    blocked=w.audit_layout(rows,[1,1],footprints=[dict(name='cupboard',position=[96,32],collision=[0,0,32,96])],
        interaction_points=[dict(name='far side',position=[176,92],radius=16)])
    assert any('far side' in error for error in blocked['errors']), blocked
    overlap=w.audit_layout(rows,[1,1],footprints=[dict(name='table',position=[96,64],collision=[0,0,32,24]),
        dict(name='cabinet',position=[112,64],collision=[0,0,32,24])])
    assert any('overlaps table' in error for error in overlap['errors']), overlap
    godot_numbers=w.audit_layout(rows,[1,1],footprints=[dict(name='Godot Rect2',position=[0,0],collision=[96.0,64.0,32.0,24.0])])
    assert not godot_numbers['errors'], godot_numbers
    bad_door=w.audit_layout(['#####','#...#','##D##','#...#','#####'],[2,1],
        doors=[dict(name='wrong facing',rect=[2,2,1,1],facing='east')])
    assert any('approach' in error for error in bad_door['errors']), bad_door
    result={'stdio':'top-level initialize, tools/list, rpg_adapter operation/arguments calls passed',
            'wall_variants':47,'pixel_cases':'filled / concave / isolated passed',
            'spatial':'32px passage, blocked route, overlapping furniture, wrong door orientation passed'}
    w.save_json(w.REPORT/'mcp-check.json',result)
    print(json.dumps(result))

if __name__=='__main__': asyncio.run(main())
