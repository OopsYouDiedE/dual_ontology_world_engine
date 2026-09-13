"""Export complete Wilson poses. Usage: python generate_godot_bank.py <DST data/anim>"""
import json
import sys
import zipfile
from pathlib import Path
from krane_extract import KAnimReader, extract_zip

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'godot_csharp/ShareAsset/extracted_wilson_anim/wilson_poses.json'
TEXTURES = ROOT / 'godot_csharp/ShareAsset/assets/character/wilson_official'


def read_build(path):
    r = KAnimReader(zipfile.ZipFile(path).read('build.bin'))
    r.offset = 4
    assert r.read_u32() == 6
    count, _ = r.read_u32(), r.read_u32()
    r.read_str()
    for _ in range(r.read_u32()):
        r.read_str()
    symbols = {}
    for _ in range(count):
        symbol, frames = r.read_u32(), r.read_u32()
        symbols[symbol] = []
        for _ in range(frames):
            start, duration = r.read_u32(), r.read_u32()
            x, y, w, h = [r.read_f32() for _ in range(4)]
            r.read_u32()
            r.read_u32()
            symbols[symbol].append((start, duration, x-w/2, y-h/2, w, h))
    return symbols


def main():
    source = Path(sys.argv[1])
    build = read_build(source / 'wilson.zip')
    animations = [a for file in ('player_basic.zip', 'player_idles_wilson.zip', 'player_attacks.zip')
                  for a in extract_zip(source / file)]
    clips = []
    faces = {}
    # Hidden equipment variants must not be drawn alongside the normal bareheaded body.
    visible = {'face', 'headbase', 'hair', 'torso', 'torso_pelvis', 'arm_upper', 'arm_lower', 'hand', 'leg', 'foot'}
    for facing, suffix in ((8, 'front'), (5, 'side'), (2, 'back')):
        for state, name, duration in (('idle', 'idle_wilson', None), ('walk', 'run_loop', .75),
                                      ('run', 'run_loop', None), ('action', 'punch', None)):
            anim = next(a for a in animations if a['name'] == name and a['facing'] == facing)
            poses = []
            for frame in anim['frames']:
                pose = []
                # Klei depth increases towards the back; Godot draws later items on top.
                for e in sorted(frame['elements'], key=lambda e: -e['z']):
                    if e['symbol'] not in visible or e['layer'] == 'ARM_carry':
                        continue
                    start, _, x, y, w, h = next(f for f in build[e['symbol_hash']]
                        if f[0] <= e['build_frame'] < f[0] + f[1])
                    texture = f"{e['symbol']}/{e['symbol']}-{start}.png"
                    if not (TEXTURES / texture).is_file():
                        raise FileNotFoundError(TEXTURES / texture)
                    if e['symbol'] == 'face' and not faces:
                        faces = {str(f[0]): list(f[2:]) for f in build[e['symbol_hash']]}
                    pose.append({'texture': texture, 'matrix': e['matrix'],
                                 'rect': [x, y, w, h], 'face': e['symbol'] == 'face'})
                poses.append(pose)
            clips.append({'name': f'{state}_{suffix}', 'length': duration or len(poses)/anim['frame_rate'],
                          'loop': state != 'action', 'frames': poses})
    OUTPUT.write_text(json.dumps({'clips': clips, 'faces': faces}, separators=(',', ':')), encoding='utf-8')
    print(f'Exported {len(clips)} clips, {sum(len(c["frames"]) for c in clips)} poses to {OUTPUT}')


if __name__ == '__main__':
    main()
