"""Audit actual saved MSCZ ranges, native mix, clefs and percussion bindings.

python music/audit_score_assets.py --out artifacts/music-audit.json
This is an asset check, not a listening evaluation or gameplay test.
"""
from __future__ import annotations
import argparse
import json
from pathlib import Path
import xml.etree.ElementTree as ET
import zipfile
import cues
import score_io
from build_soundtrack import SCORE_NAMES, SOUNDFONT


def audit() -> list[dict]:
    here = Path(__file__).resolve().parent
    rows = []
    font = None
    for make in cues.CUES:
        piece = make()
        piece.validate()
        path = here / f'Blessed Sleepers - {SCORE_NAMES[piece.slug]}.mscz'
        with zipfile.ZipFile(path) as archive:
            root = ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.mscx'))))
            audio = json.loads(archive.read('audiosettings.json'))
        problems = score_io.compare_with_score(piece, str(path))
        if problems:
            raise ValueError(f'{piece.slug}: {problems[:5]}')
        score_io.validate_score_ranges(root, piece.slug)
        staff_iter = iter(root.find('Score').findall('Staff'))
        instruments = []
        for model, saved in zip(piece.parts, root.find('Score').findall('Part')):
            staves = [next(staff_iter) for _ in saved.findall('Staff')]
            pitches = [int(n.findtext('pitch')) for st in staves for n in st.findall('.//Note')]
            info = {'name': model.name, 'range': [min(pitches), max(pitches)] if pitches else [],
                    'clef': model.initial_clef or ('F' if model.bass_clef else 'instrument default')}
            if model.drum_map:
                defined = {int(n.get('pitch')) for n in saved.findall('Instrument/Drum')}
                if not set(pitches) <= defined:
                    raise ValueError(f'{piece.slug}/{model.name}: undefined percussion key')
            if model.explicit_sound:
                if font is None:
                    import sf2
                    font = sf2.SoundFont(SOUNDFONT)
                presets = {(b, p): name for b, p, name in font.preset_names()}
                if presets.get((model.bank, model.program)) != model.explicit_sound:
                    raise ValueError(f'{piece.slug}/{model.name}: wrong SoundFont preset')
                track = next(t for t in audio['tracks'] if t.get('partId') == saved.get('id'))
                expected = f'MS Basic\\{model.bank}\\{model.program}'
                if track['in']['resourceMeta']['id'] != expected:
                    raise ValueError(f'{piece.slug}/{model.name}: native preset binding not saved')
                regions = font.regions(model.bank, model.program)
                for key in set(pitches):
                    for vel in (33, 49, 64, 80):
                        if not any(g['keyRange'][0] <= key <= g['keyRange'][1] and
                                   g['velRange'][0] <= vel <= g['velRange'][1] for g in regions):
                            raise ValueError(f'{piece.slug}/{model.name}: missing sample for {key}/{vel}')
                info['sound'] = model.explicit_sound
            instruments.append(info)
        rows.append({'cue': piece.slug, 'bars': len(piece.bars), 'range_issues': [], 'instruments': instruments})
    return rows


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path)
    args = parser.parse_args()
    rows = audit()
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding='utf-8')
    for row in rows:
        print(f"{row['cue']}: {row['bars']} bars; stored ranges, native mix and sample bindings checked")


if __name__ == '__main__':
    main()
