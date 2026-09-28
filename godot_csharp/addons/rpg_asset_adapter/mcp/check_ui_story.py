"""Real MCP/Godot integration; current storyline is read only. Requires imported game assets."""
import asyncio
import hashlib
import json
import os
from pathlib import Path
import sys
import tempfile

from mcp import Client, StdioServerParameters
import workflow as w
import ui_workflow as ui
from check_native_art import payload


def check_owned_ui():
    saved=(w.REPO,w.GAME,ui.SOURCE,ui.OUTPUT,ui.SCENE,ui.MANIFEST)
    try:
        with tempfile.TemporaryDirectory() as directory:
            w.REPO=Path(directory); w.GAME=w.REPO/'godot_csharp'
            ui.SOURCE=w.GAME/'AssetsSource/Internal/Props/FirstAwakeningInterior'
            ui.OUTPUT=w.GAME/'ShareAsset/Internal/Props/FirstAwakeningInterior'
            ui.SCENE=w.GAME/'scenes/FirstAwakening/MindThemePreview.tscn'
            ui.MANIFEST=ui.SOURCE/'mind-ui.json'
            ui.build_diegetic_ui()
            ui.build_diegetic_ui(replace=True)
            source=ui.SOURCE/'mind-ui-dossier.aseprite'
            source.write_bytes(b'artist-owned edit')
            snapshot={p:p.read_bytes() for p in w.GAME.rglob('*') if p.is_file()}
            try:
                ui.build_diegetic_ui(replace=True)
                raise AssertionError('artist edit overwritten')
            except ValueError:
                pass
            assert all(p.read_bytes()==content for p,content in snapshot.items())
    finally:
        w.REPO,w.GAME,ui.SOURCE,ui.OUTPUT,ui.SCENE,ui.MANIFEST=saved


async def main():
    check_owned_ui()
    report={'transport':'real MCP stdio','godot':'actual engine, not mocks'}
    async with Client(StdioServerParameters(command=sys.executable,args=[str(w.HERE/'server.py')],env=dict(os.environ))) as client:
        names={t.name for t in (await client.list_tools()).tools}
        required={'ui_design_contract','build_diegetic_ui','inspect_godot_ui','measure_dialogue_text',
                  'story_design_contract','inspect_story','review_story_revision','apply_story_revision','review_story_beats'}
        assert required<=names
        report['tools']=sorted(names)
        report['ui_contract']=payload(await client.call_tool('ui_design_contract',{}))
        report['story_contract']=payload(await client.call_tool('story_design_contract',{}))
        report['viewports']=[]
        for size in ([1280,992],[960,640]):
            scene=payload(await client.call_tool('inspect_godot_ui',{'viewport':size}))
            assert not scene['errors'] and not scene['warnings'],scene
            assert scene['controls'][0]['size']==size,scene['controls'][0]
            report['viewports'].append(scene)
        measured=payload(await client.call_tool('measure_dialogue_text',{'texts':['杯子给我。','这是一段长句。'*30],'width':160,'height':60,'font_size':20}))
        assert measured['texts'][0]['fits_height'] and not measured['texts'][1]['fits_height']
        report['font_boundary']=measured
        assert (await client.call_tool('measure_dialogue_text',{'texts':[]})).is_error
        assert (await client.call_tool('inspect_godot_ui',{'viewport':[1,1]})).is_error
        story=payload(await client.call_tool('inspect_story',{}))
        assert story['safe_to_apply'],story['unknown_syntax']
        story_path=w.REPO/story['path']
        original=story_path.read_bytes()
        rows=[row for row in story['entries'] if row['kind']=='dialogue']
        proposal=next(row for row in rows if row['speaker']=='工程师' and '入学前' in row['text'])
        changes=[{'line':proposal['line'],'expected':proposal['raw'],
                  'replacement':'工程师: 从前做工的事，我还记得。可我怎么到了这里？'}]
        review=payload(await client.call_tool('review_story_revision',{'source_path':story['path'],'source_sha256':story['sha256'],'changes':changes}))
        assert review['control_preserved'] and review['safe_to_apply']
        assert (await client.call_tool('apply_story_revision',{'source_path':story['path'],'source_sha256':'0'*64,'changes':changes})).is_error
        assert story_path.read_bytes()==original
        report['revision_preview']=review
        report['story_source_sha256']=hashlib.sha256(original).hexdigest()
        report['dialogue_layout']=payload(await client.call_tool('measure_dialogue_text',{'texts':[r['text'] for r in rows]}))
        report['dialogue_layout_source_lines']=[r['line'] for r in rows]
        beats=payload(await client.call_tool('review_story_beats',{
            'characters':[{'id':'侦探','known_facts':['门受阻']}],
            'beats':[{'kind':'hypothesis','character':'侦探','goal':'辨别门用途','action':'提出猜测',
                      'information':'这可能是监禁','claims':[],'acquires':['监禁已证实'],'presented_as_fact':True}]}))
        assert not beats['ok']
        report['knowledge_boundary']=beats
    report['limits']=['No literary or artistic quality certification.','Current storyline was not rewritten.',
                      'Godot SystemFont metrics depend on installed fonts. Aseprite editor not tested.']
    w.save_json(w.REPORT/'ui-story-mcp-check.json',report)
    print(json.dumps({'tools':len(names),'viewports':2,'dialogue_turns':len(rows),
                      'layout_overflows':sum(not r['fits_height'] or not r['fits_width'] for r in report['dialogue_layout']['texts']),
                      'source_preserved':True},ensure_ascii=False))


if __name__=='__main__':
    asyncio.run(main())
