import os
import subprocess
import copy
import xml.etree.ElementTree as ET
from xml.dom import minidom

SRC_FILE = 'music/The Unfallen Duet.mscx'
OUT_FILE = 'music/The Unfallen Duet (Enhanced Treble).mscx'
MUSESCORE_EXE = r'C:\Program Files\MuseScore 4\bin\MuseScore4.exe'

dur_map = {'whole': 1.0, 'half': 0.5, 'quarter': 0.25, 'eighth': 0.125, '16th': 0.0625, '32nd': 0.03125}
tpc_map = {0: 14, 1: 9, 2: 16, 3: 11, 4: 18, 5: 13, 6: 20, 7: 15, 8: 10, 9: 17, 10: 12, 11: 19}

def get_note_dur(chord):
    d = chord.findtext('durationType')
    dots = int(chord.findtext('dots', '0'))
    val = dur_map[d]
    if dots == 1: val *= 1.5
    elif dots == 2: val *= 1.75
    return val

def add_pitch_to_chord(chord, p):
    existing = [int(n.findtext('pitch')) for n in chord.iter('Note')]
    if p in existing:
        return
    n = ET.SubElement(chord, 'Note')
    ET.SubElement(n, 'pitch').text = str(p)
    ET.SubElement(n, 'tpc').text = str(tpc_map[p % 12])

def enhance_score():
    print(f'Loading {SRC_FILE}...')
    tree = ET.parse(SRC_FILE)
    root = tree.getroot()
    score = root.find('Score')

    # Update metadata for the new enhanced version
    for meta in score.findall('metaTag'):
        name = meta.get('name')
        if name == 'workTitle':
            meta.text = 'The Unfallen Duet (Piano Treble Enhanced)'
        elif name == 'subtitle':
            meta.text = 'Piano & Violin Duet — Treble Reinforced with Full Harmony'
        elif name == 'source':
            meta.text = 'The Unfallen Duet (Violin track preserved, Piano Treble expanded with harmonic chords)'

    staffs = {s.get('id'): s for s in score.findall('Staff')}
    s1 = staffs['1'] # Violin (PRESERVED INTACT)
    s2 = staffs['2'] # Piano RH (ENHANCED)
    s3 = staffs['3'] # Piano LH

    # 1. Section M25-M28: B-motive connection over G dominant
    # M25 (Vln: E4, C4) -> Pno RH: [C4, E4, G4, C5] / [C4, E4, G4, C5] -> Add lower triad tones
    m25_ch = [c for c in s2.findall('Measure')[24].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m25_ch[0], 52) # E3
    add_pitch_to_chord(m25_ch[0], 55) # G3
    add_pitch_to_chord(m25_ch[0], 60) # C4
    add_pitch_to_chord(m25_ch[0], 67) # G4
    add_pitch_to_chord(m25_ch[1], 48) # C3
    add_pitch_to_chord(m25_ch[1], 55) # G3
    add_pitch_to_chord(m25_ch[1], 64) # E4

    # M26 (Vln: D4, E4, D4)
    m26_ch = [c for c in s2.findall('Measure')[25].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m26_ch[0], 55) # G3
    add_pitch_to_chord(m26_ch[0], 59) # B3
    add_pitch_to_chord(m26_ch[1], 55) # G3
    add_pitch_to_chord(m26_ch[1], 60) # C4
    add_pitch_to_chord(m26_ch[2], 55) # G3
    add_pitch_to_chord(m26_ch[2], 59) # B3

    # M27 (Vln: D4, F4)
    m27_ch = [c for c in s2.findall('Measure')[26].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m27_ch[0], 55) # G3
    add_pitch_to_chord(m27_ch[0], 59) # B3
    add_pitch_to_chord(m27_ch[1], 55) # G3
    add_pitch_to_chord(m27_ch[1], 59) # B3

    # M28 (Vln: D4, G4)
    m28_ch = [c for c in s2.findall('Measure')[27].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m28_ch[0], 55) # G3
    add_pitch_to_chord(m28_ch[0], 59) # B3
    add_pitch_to_chord(m28_ch[1], 55) # G3
    add_pitch_to_chord(m28_ch[1], 59) # B3
    add_pitch_to_chord(m28_ch[1], 62) # D4

    # 2. Section M37-M43: Sequential descent
    # M37: B4, G4 (G major) -> add D4, G3
    m37_ch = [c for c in s2.findall('Measure')[36].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m37_ch[1], 55) # G3
    add_pitch_to_chord(m37_ch[1], 59) # B3

    # M38: A4, B4, A4 (D7 / G) -> add F#4 / D4
    m38_ch = [c for c in s2.findall('Measure')[37].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m38_ch[0], 57) # A3
    add_pitch_to_chord(m38_ch[0], 62) # D4
    add_pitch_to_chord(m38_ch[2], 57) # A3
    add_pitch_to_chord(m38_ch[2], 62) # D4

    # M39: A4, F4 (Dm) -> add D4, F3
    m39_ch = [c for c in s2.findall('Measure')[38].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m39_ch[0], 57) # A3
    add_pitch_to_chord(m39_ch[0], 62) # D4
    add_pitch_to_chord(m39_ch[1], 57) # A3
    add_pitch_to_chord(m39_ch[1], 60) # C4

    # M40: G4, A4, G4 (C major) -> add E4, C4
    m40_ch = [c for c in s2.findall('Measure')[39].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m40_ch[0], 55) # G3
    add_pitch_to_chord(m40_ch[0], 60) # C4
    add_pitch_to_chord(m40_ch[1], 57) # A3
    add_pitch_to_chord(m40_ch[1], 60) # C4
    add_pitch_to_chord(m40_ch[2], 55) # G3
    add_pitch_to_chord(m40_ch[2], 60) # C4

    # M41: G4, E4 (C major / Em) -> add B3, E4
    m41_ch = [c for c in s2.findall('Measure')[40].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m41_ch[0], 52) # E3
    add_pitch_to_chord(m41_ch[0], 59) # B3
    add_pitch_to_chord(m41_ch[1], 52) # E3
    add_pitch_to_chord(m41_ch[1], 55) # G3

    # M42: F4, E4 (Dm) -> add D4, A3
    m42_ch = [c for c in s2.findall('Measure')[41].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m42_ch[0], 53) # F3
    add_pitch_to_chord(m42_ch[0], 57) # A3
    add_pitch_to_chord(m42_ch[1], 52) # E3
    add_pitch_to_chord(m42_ch[1], 57) # A3

    # M43: D4, B4 (G7) -> add G3, F4
    m43_ch = [c for c in s2.findall('Measure')[42].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m43_ch[0], 55) # G3
    add_pitch_to_chord(m43_ch[0], 59) # B3
    add_pitch_to_chord(m43_ch[1], 62) # D4
    add_pitch_to_chord(m43_ch[1], 65) # F4

    # M47: C5, Ab4 (Fm) -> add F4, C4
    m47_ch = [c for c in s2.findall('Measure')[46].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m47_ch[1], 56) # Ab3
    add_pitch_to_chord(m47_ch[1], 60) # C4
    add_pitch_to_chord(m47_ch[1], 65) # F4

    # M48: A4, B4, G4 (G7) -> add D4, F4
    m48_ch = [c for c in s2.findall('Measure')[47].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m48_ch[0], 57) # A3
    add_pitch_to_chord(m48_ch[0], 62) # D4
    add_pitch_to_chord(m48_ch[2], 55) # G3
    add_pitch_to_chord(m48_ch[2], 59) # B3

    # 3. Section M49-M68: Underground Mystery & Law Exploration (Part IV)
    # The piano RH was in unison with violin single notes!
    # Let's compress violin melody into treble and add supporting rich harmony underneath:
    harmonies_m49_68 = [
        # M49 (c minor: G4, C5, D5, Eb5)
        [[60, 63], [60, 67], [62, 67], [63, 67]],
        # M50 (c minor / G7: D5, C5, B4, C5)
        [[62, 67], [60, 67], [59, 65], [60, 67]],
        # M51 (Fm/Ab: F5, Eb5, D5)
        [[60, 65], [60, 68], [59, 65]],
        # M52 (G7: D5, C5, B4)
        [[59, 65], [60, 65], [55, 59, 65]],
        # M53 (Cm / Bbm: G5, C6, Bb5)
        [[63, 67], [67, 72], [65, 70]],
        # M54 (Fm/Ab: Ab5, G5, F5)
        [[65, 68], [63, 67], [60, 65]],
        # M55 (Fm -> G: F5, Eb5, D5, C5)
        [[60, 65], [60, 63], [59, 65], [60, 67]],
        # M56 (G7 -> Cm: B4, C5)
        [[55, 59, 65], [60, 63, 67]],
        # M57 (Cm: G4)
        [[48, 55, 60, 63]],
        # M58 (Cm: C5, D5, Eb5, D5, C5, B4)
        [[60, 63], [62, 67], [63, 67], [62, 67], [60, 63], [55, 59]],
        # M59 (Gm / Bb: D5, Eb5, D5, C5, Bb4)
        [[58, 62], [60, 63], [58, 62], [57, 60], [55, 58]],
        # M60 (Bb: A4, Bb4, D5)
        [[57, 62], [58, 62], [58, 65]],
        # M61 (Eb: Bb4, Eb5, F5, G5, Ab5, G5)
        [[55, 58], [58, 63], [60, 65], [63, 67], [63, 68], [63, 67]],
        # M62 (Ab / Eb: F5, Eb5, D5, Eb5)
        [[60, 65], [58, 63], [58, 62], [58, 63]],
        # M63 (Fm -> C#dim / G: C5, C#5, C5, Bb4, Ab4, G4)
        [[56, 60], [57, 61], [56, 60], [55, 58], [53, 56], [51, 55]],
        # M64 (Fm -> G: Ab4, Bb4, C5)
        [[53, 56], [55, 58], [56, 60]],
        # M65 (Cm: C5, D5, Eb5, F5, G5)
        [[55, 60], [57, 62], [60, 63], [62, 65], [63, 67]],
        # M66 (Fm / Ddim: Ab5, G5, F5, Eb5, D5)
        [[65, 68], [63, 67], [60, 65], [60, 63], [59, 62]],
        # M67 (Db / G: C#5, C5, Bb4)
        [[57, 61], [56, 60], [55, 58]],
        # M68 (G7 / Bdim: B4, D5, F5, Ab5, G5)
        [[55, 59], [59, 62], [62, 65], [65, 68], [55, 59, 67]],
    ]

    for bar_offset, harm_list in enumerate(harmonies_m49_68):
        bar_idx = 48 + bar_offset
        m_rh = s2.findall('Measure')[bar_idx]
        chords = [c for c in m_rh.find('voice') if c.tag == 'Chord']
        for c_idx, chord in enumerate(chords):
            if c_idx < len(harm_list):
                for p in harm_list[c_idx]:
                    add_pitch_to_chord(chord, p)

    # 4. Section M69-M76: Machine Undercurrent (Clockwork Perpetuum Mobile)
    # The violin plays 8th notes. Piano RH was bare unison.
    # Add steady harmonic 3rd/6th intervals and counter-rhythm chords to match piano volume:
    harmonies_m69_76 = [
        # M69 (Cm: Eb4, C4, Eb4, F4, Eb4, C4, Eb4, C4)
        [55, 55, 55, 57, 55, 55, 55, 55], # G3/A3 underneath
        # M70 (Dm: F4, D4, F4, G4, F4, D4, F4, D4)
        [57, 57, 57, 58, 57, 57, 57, 57], # A3/Bb3 underneath
        # M71 (Fm: F4, D4, F4, G4, F4, D4, F4, D4)
        [56, 56, 56, 58, 56, 56, 56, 56], # Ab3 underneath
        # M72 (G7: D4, B3, D4, E4, D4, B3, D4, B3)
        [53, 53, 53, 55, 53, 53, 53, 53], # F3 underneath
        # M73 (Cm: Eb4, C4, Eb4, F4, Eb4, C4, Eb4, C4)
        [55, 55, 55, 57, 55, 55, 55, 55],
        # M74 (Db: F4, D4, F4, G4, F4, D4, F4, D4)
        [57, 57, 57, 58, 57, 57, 57, 57],
        # M75 (Fm: F4, D4, F4, G4, F4, D4, F4, D4)
        [56, 56, 56, 58, 56, 56, 56, 56],
        # M76 (G7: D4, B3, D4, E4, D4, B3, D4, B3)
        [53, 53, 53, 55, 53, 53, 53, 53],
    ]
    for bar_offset, harm_list in enumerate(harmonies_m69_76):
        bar_idx = 68 + bar_offset
        m_rh = s2.findall('Measure')[bar_idx]
        chords = [c for c in m_rh.find('voice') if c.tag == 'Chord']
        for c_idx, chord in enumerate(chords):
            if c_idx < len(harm_list):
                add_pitch_to_chord(chord, harm_list[c_idx])

    # 5. Section M77-M84: Crisis Accumulation
    # Add full-bodied chord foundations to the treble runs in Piano RH
    harmonies_m77_84 = [
        # M77 (Fm: C5, Bb4, Ab4, G4, F4, Eb4, D4)
        [[60, 65], [60, 65], [60, 65], [57, 60], [57, 60], [55, 58], [53, 57]],
        # M78 (Fm: C4, F4, Ab4, C5, Bb4, Ab4, G4, F4)
        [[53, 56], [56, 60], [60, 65], [65, 68], [60, 65], [60, 65], [56, 60], [53, 56]],
        # M79 (Ab: C5, Eb4, Ab4, Bb4, C5, Bb4)
        [[56, 63, 68], [51, 56, 60], [56, 63], [58, 65], [60, 68], [58, 65]],
        # M80 (Ab -> Ddim: Ab4, F4, D4, F4, Ab4, G4, F4, Eb4)
        [[60, 63], [56, 60], [53, 56], [56, 60], [60, 65], [58, 63], [56, 60], [55, 58]],
        # M81 (Ddim -> G7: D4, F4, Ab4, B3, C4, D4, Eb4, F4)
        [[50, 56], [53, 59], [56, 62], [47, 53], [48, 55], [50, 56], [51, 58], [53, 59]],
        # M82 (G7: F4, Ab4, B4, D5, B4, Ab4, F4, D4)
        [[53, 59], [56, 62], [59, 65], [62, 67], [59, 65], [56, 62], [53, 59], [50, 56]],
        # M83 (G7: B3, D4, F4, Ab4, G4, F4, Eb4, D4)
        [[47, 53, 59], [50, 56, 62], [53, 59, 65], [56, 62, 68], [55, 62, 67], [53, 59, 65], [51, 58, 63], [50, 56, 62]],
        # M84 (G7: B3, G4, F4, D4, B3, D4, F4, G4)
        [[47, 55, 59], [55, 62, 67], [53, 59, 65], [50, 56, 62], [47, 53, 59], [50, 56, 62], [53, 59, 65], [55, 62, 67]],
    ]
    for bar_offset, harm_list in enumerate(harmonies_m77_84):
        bar_idx = 76 + bar_offset
        m_rh = s2.findall('Measure')[bar_idx]
        chords = [c for c in m_rh.find('voice') if c.tag == 'Chord']
        for c_idx, chord in enumerate(chords):
            if c_idx < len(harm_list):
                for p in harm_list[c_idx]:
                    add_pitch_to_chord(chord, p)

    # 6. Section M85-M88: Massive Dominant Preparation (G7)
    # M85 & M86: Piano RH had [G4, D5]. Add B4 and F5 to make powerful 4-note fortissimo G7 chord:
    for b in [85, 86]:
        chords = [c for c in s2.findall('Measure')[b-1].find('voice') if c.tag == 'Chord']
        for chord in chords:
            add_pitch_to_chord(chord, 71) # B4
            add_pitch_to_chord(chord, 77) # F5

    # M88: [G4, G5], [F4, D5], [D4, B4] -> Add inner 3rds:
    m88_ch = [c for c in s2.findall('Measure')[87].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m88_ch[0], 71) # B4
    add_pitch_to_chord(m88_ch[0], 74) # D5
    add_pitch_to_chord(m88_ch[1], 69) # A4 (or B4)
    add_pitch_to_chord(m88_ch[1], 71) # B4
    add_pitch_to_chord(m88_ch[2], 67) # G4

    # 7. Section M93-M96 & M104-M106: Grand Climax
    # M93-M96: Violin plays double stops [C5, E5], [G4, C5], etc.
    # Piano RH had identical notes! Enrich Piano RH to four-part grand chords [C4, G4, C5, E5], [C4, F4, C5, F5], [B3, G4, D5, G5], [C4, G4, E5, C6]:
    for b in [93, 94, 95, 96]:
        chords = [c for c in s2.findall('Measure')[b-1].find('voice') if c.tag == 'Chord']
        for chord in chords:
            existing = [int(n.findtext('pitch')) for n in chord.iter('Note')]
            # Add one octave lower root/fifth
            for p in existing:
                if p - 12 >= 48:
                    add_pitch_to_chord(chord, p - 12)

    # M100: [74, 62], [71] -> add 59(B3), 67(G4) to chord 2
    m100_ch = [c for c in s2.findall('Measure')[99].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m100_ch[1], 59) # B3
    add_pitch_to_chord(m100_ch[1], 67) # G4

    # M104-M106: Final cadence expansion
    m104_ch = [c for c in s2.findall('Measure')[103].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m104_ch[0], 62) # D4
    add_pitch_to_chord(m104_ch[0], 67) # G4
    add_pitch_to_chord(m104_ch[1], 59) # B3
    add_pitch_to_chord(m104_ch[1], 65) # F4

    m105_ch = [c for c in s2.findall('Measure')[104].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m105_ch[0], 62) # D4
    add_pitch_to_chord(m105_ch[0], 69) # A4
    add_pitch_to_chord(m105_ch[1], 65) # F4
    add_pitch_to_chord(m105_ch[1], 74) # D5

    m106_ch = [c for c in s2.findall('Measure')[105].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m106_ch[0], 52) # E3
    add_pitch_to_chord(m106_ch[0], 60) # C4
    add_pitch_to_chord(m106_ch[0], 64) # E4
    add_pitch_to_chord(m106_ch[0], 67) # G4
    add_pitch_to_chord(m106_ch[0], 72) # C5

    # M107-M110: Coda
    m107_ch = [c for c in s2.findall('Measure')[106].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m107_ch[0], 64) # E4
    add_pitch_to_chord(m107_ch[1], 60) # C4
    add_pitch_to_chord(m107_ch[2], 64) # E4

    m108_ch = [c for c in s2.findall('Measure')[107].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m108_ch[0], 65) # F4
    add_pitch_to_chord(m108_ch[1], 62) # D4
    add_pitch_to_chord(m108_ch[2], 59) # B3

    m109_ch = [c for c in s2.findall('Measure')[108].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m109_ch[0], 60) # C4
    add_pitch_to_chord(m109_ch[0], 64) # E4
    add_pitch_to_chord(m109_ch[0], 67) # G4

    m110_ch = [c for c in s2.findall('Measure')[109].find('voice') if c.tag == 'Chord']
    add_pitch_to_chord(m110_ch[0], 60) # C4
    add_pitch_to_chord(m110_ch[0], 64) # E4
    add_pitch_to_chord(m110_ch[0], 67) # G4

    # Validate durations across all 110 measures on all 3 staves
    for sid in ['1', '2', '3']:
        staff = staffs[sid]
        for bar_idx, m in enumerate(staff.findall('Measure'), 1):
            tot = 0.0
            for el in m.find('voice'):
                if el.tag in ('Chord', 'Rest'):
                    d = el.findtext('durationType')
                    if d == 'measure':
                        tot = 1.0
                        break
                    val = dur_map[d]
                    dots = int(el.findtext('dots', '0'))
                    if dots == 1: val *= 1.5
                    elif dots == 2: val *= 1.75
                    tot += val
            if abs(tot - 1.0) > 1e-4:
                raise ValueError(f'Staff {sid} Measure {bar_idx} duration error: {tot}')

    print('All 110 measures across all 3 staves verified (exact 4 beats per bar)!')

    # Save to new file
    xml_bytes = ET.tostring(root, encoding='utf-8')
    parsed = minidom.parseString(xml_bytes)
    pretty = parsed.toprettyxml(indent='  ', encoding='utf-8')

    with open(OUT_FILE, 'wb') as f:
        f.write(pretty)
    print(f'Saved new score to {OUT_FILE}')

    # Export to .mscz, .mp3, .pdf
    if os.path.exists(MUSESCORE_EXE):
        mscz_path = os.path.abspath('music/The Unfallen Duet (Enhanced Treble).mscz')
        mp3_path = os.path.abspath('music/The Unfallen Duet (Enhanced Treble).mp3')
        pdf_path = os.path.abspath('music/The Unfallen Duet (Enhanced Treble).pdf')

        print('Exporting .mscz...')
        subprocess.run([MUSESCORE_EXE, '-o', mscz_path, os.path.abspath(OUT_FILE)], capture_output=True)
        print('Exporting .mp3...')
        subprocess.run([MUSESCORE_EXE, '-o', mp3_path, os.path.abspath(OUT_FILE)], capture_output=True)
        print('Exporting .pdf...')
        subprocess.run([MUSESCORE_EXE, '-o', pdf_path, os.path.abspath(OUT_FILE)], capture_output=True)
        print(f'Render complete! (mp3={os.path.exists(mp3_path)}, pdf={os.path.exists(pdf_path)})')

if __name__ == '__main__':
    enhance_score()
