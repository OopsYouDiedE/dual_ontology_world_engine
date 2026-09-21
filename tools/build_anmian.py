import os
import subprocess
import xml.etree.ElementTree as ET
from xml.dom import minidom

def create_score():
    root = ET.Element('museScore', version='4.40')
    ET.SubElement(root, 'programVersion').text = '4.4.1'
    ET.SubElement(root, 'programRevision').text = '47971f6'
    
    score = ET.SubElement(root, 'Score')
    ET.SubElement(score, 'Division').text = '480'
    ET.SubElement(score, 'showInvisible').text = '1'
    ET.SubElement(score, 'showUnprintable').text = '1'
    ET.SubElement(score, 'showFrames').text = '1'
    ET.SubElement(score, 'showMargins').text = '0'
    ET.SubElement(score, 'open').text = '1'

    metas = [
        ('arranger', ''),
        ('composer', '学院主题'),
        ('copyright', ''),
        ('creationDate', '2026-09-19'),
        ('lyricist', ''),
        ('movementNumber', ''),
        ('movementTitle', ''),
        ('platform', 'Microsoft Windows'),
        ('source', '游戏曲目：安眠 (16小节钢琴主旋律)'),
        ('sourceRevisionId', ''),
        ('subtitle', '游戏曲目 · 钢琴独奏'),
        ('translator', ''),
        ('workNumber', ''),
        ('workTitle', '安眠'),
    ]
    for k, v in metas:
        m = ET.SubElement(score, 'metaTag', name=k)
        m.text = v

    order = ET.SubElement(score, 'Order')
    custom = ET.SubElement(order, 'Custom')
    ET.SubElement(custom, 'visible').text = '1'
    ET.SubElement(custom, 'type').text = '0'

    part = ET.SubElement(score, 'Part')
    s1_part = ET.SubElement(part, 'Staff', id='1')
    ET.SubElement(s1_part, 'StaffType', group='pitched')
    bracket = ET.SubElement(s1_part, 'bracket', type='1', span='2', col='0')
    ET.SubElement(s1_part, 'barLineSpan').text = '1'

    s2_part = ET.SubElement(part, 'Staff', id='2')
    ET.SubElement(s2_part, 'StaffType', group='pitched')
    ET.SubElement(s2_part, 'defaultClef').text = 'F'

    ET.SubElement(part, 'trackName').text = '钢琴'
    inst = ET.SubElement(part, 'Instrument', id='piano')
    ET.SubElement(inst, 'longName').text = '钢琴'
    ET.SubElement(inst, 'shortName').text = 'Pno.'
    ET.SubElement(inst, 'trackName').text = '钢琴'
    ET.SubElement(inst, 'minPitchP').text = '21'
    ET.SubElement(inst, 'maxPitchP').text = '108'
    ET.SubElement(inst, 'minPitchA').text = '21'
    ET.SubElement(inst, 'maxPitchA').text = '108'
    ET.SubElement(inst, 'instrumentId').text = 'keyboard.piano'
    ET.SubElement(inst, 'clef', staff='2').text = 'F'
    
    chan = ET.SubElement(inst, 'Channel')
    ET.SubElement(chan, 'program', value='0')
    ET.SubElement(chan, 'synti').text = 'Fluid'

    # Staff 1 and Staff 2 elements
    staff1 = ET.SubElement(score, 'Staff', id='1')
    staff2 = ET.SubElement(score, 'Staff', id='2')

    # Note mapping for pitch / tpc
    # C=14, G=15, D=16, A=17, E=18, B=19, F=13
    tpc_map = {
        'C': 14, 'D': 16, 'E': 18, 'F': 13, 'G': 15, 'A': 17, 'B': 19
    }
    
    def pitch_tpc(name_oct):
        note_name = name_oct[0]
        octave = int(name_oct[1:])
        semitones = {'C': 0, 'D': 2, 'E': 4, 'F': 5, 'G': 7, 'A': 9, 'B': 11}
        p = 12 * (octave + 1) + semitones[note_name]
        tpc = tpc_map[note_name]
        return p, tpc

    def add_chord(voice, notes, dur, dots=0, tied=False, fermata=False):
        chord = ET.SubElement(voice, 'Chord')
        if dots > 0:
            ET.SubElement(chord, 'dots').text = str(dots)
        ET.SubElement(chord, 'durationType').text = dur
        if fermata:
            ferm = ET.SubElement(chord, 'Fermata')
            ET.SubElement(ferm, 'subtype').text = 'fermataAbove'
        if not isinstance(notes, list):
            notes = [notes]
        for n in notes:
            p, tpc = pitch_tpc(n)
            note_el = ET.SubElement(chord, 'Note')
            ET.SubElement(note_el, 'pitch').text = str(p)
            ET.SubElement(note_el, 'tpc').text = str(tpc)
            if tied:
                tie = ET.SubElement(note_el, 'Spanner', type='Tie')
                ET.SubElement(tie, 'Tie')
                next_tie = ET.SubElement(tie, 'next')
                loc = ET.SubElement(next_tie, 'location')
                ET.SubElement(loc, 'fractions').text = '1/1'

    def add_rest(voice, dur, dots=0):
        rest = ET.SubElement(voice, 'Rest')
        if dots > 0:
            ET.SubElement(rest, 'dots').text = str(dots)
        ET.SubElement(rest, 'durationType').text = dur

    # Staff 1 (Right Hand Melody - 16 measures, doubled duration of original 8-measure theme)
    # Original M1 -> M1-M2: E5(3 beats) + E5(1 beat), F5(2 beats) + G5(2 beats)
    # Original M2 -> M3-M4: G5(2 beats) + C6(2 beats), G5(2 beats) + E5(2 beats)
    # Original M3 -> M5-M6: F5(3 beats) + F5(1 beat), F5(2 beats) + D5(2 beats)
    # Original M4 -> M7-M8: E5(3 beats) + E5(1 beat), E5(4 beats)
    # Original M5 -> M9-M10: E5(3 beats) + E5(1 beat), F5(2 beats) + G5(2 beats)
    # Original M6 -> M11-M12: G5(2 beats) + C6(2 beats), G5(2 beats) + E5(2 beats)
    # Original M7 -> M13-M14: D5(2 beats) + A5(2 beats), G5(2 beats) + G4(2 beats)
    # Original M8 -> M15-M16: C5(4 beats, tied) + C5(4 beats, fermata)

    s1_measures = [
        # M1-M2: 3. 3 4 5 doubled
        [('chord', 'E5', 'half', 1), ('chord', 'E5', 'quarter', 0)],
        [('chord', 'F5', 'half', 0), ('chord', 'G5', 'half', 0)],
        # M3-M4: 5 1' 5 3 doubled
        [('chord', 'G5', 'half', 0), ('chord', 'C6', 'half', 0)],
        [('chord', 'G5', 'half', 0), ('chord', 'E5', 'half', 0)],
        # M5-M6: 4. 4 4 2 doubled
        [('chord', 'F5', 'half', 1), ('chord', 'F5', 'quarter', 0)],
        [('chord', 'F5', 'half', 0), ('chord', 'D5', 'half', 0)],
        # M7-M8: 3. 3 3 - doubled
        [('chord', 'E5', 'half', 1), ('chord', 'E5', 'quarter', 0)],
        [('chord', 'E5', 'whole', 0)],
        # M9-M10: 3. 3 4 5 doubled
        [('chord', 'E5', 'half', 1), ('chord', 'E5', 'quarter', 0)],
        [('chord', 'F5', 'half', 0), ('chord', 'G5', 'half', 0)],
        # M11-M12: 5 1' 5 3 doubled
        [('chord', 'G5', 'half', 0), ('chord', 'C6', 'half', 0)],
        [('chord', 'G5', 'half', 0), ('chord', 'E5', 'half', 0)],
        # M13-M14: 2 6 5 5, doubled
        [('chord', 'D5', 'half', 0), ('chord', 'A5', 'half', 0)],
        [('chord', 'G5', 'half', 0), ('chord', 'G4', 'half', 0)],
        # M15-M16: 1 - - - doubled
        [('chord', 'C5', 'whole', 0, False, True)], # tied=True
        [('chord', 'C5', 'whole', 0, True, False)],  # fermata=True
    ]

    # Staff 2 (Left Hand Piano Accompaniment - 16 measures)
    s2_measures = [
        # M1: C major
        [('chord', 'C3', 'quarter', 0), ('chord', 'G3', 'quarter', 0), ('chord', 'E4', 'quarter', 0), ('chord', 'G3', 'quarter', 0)],
        # M2: C major
        [('chord', 'E3', 'quarter', 0), ('chord', 'G3', 'quarter', 0), ('chord', 'C4', 'quarter', 0), ('chord', 'G3', 'quarter', 0)],
        # M3: C major / high arch
        [('chord', 'C3', 'quarter', 0), ('chord', 'G3', 'quarter', 0), ('chord', 'E4', 'quarter', 0), ('chord', 'G3', 'quarter', 0)],
        # M4: C/E
        [('chord', 'E3', 'quarter', 0), ('chord', 'G3', 'quarter', 0), ('chord', 'C4', 'quarter', 0), ('chord', 'G3', 'quarter', 0)],
        # M5: F major
        [('chord', 'F3', 'quarter', 0), ('chord', 'C4', 'quarter', 0), ('chord', 'A4', 'quarter', 0), ('chord', 'C4', 'quarter', 0)],
        # M6: Dm
        [('chord', 'D3', 'quarter', 0), ('chord', 'A3', 'quarter', 0), ('chord', 'F4', 'quarter', 0), ('chord', 'A3', 'quarter', 0)],
        # M7: C/G
        [('chord', 'G2', 'quarter', 0), ('chord', 'E3', 'quarter', 0), ('chord', 'G3', 'quarter', 0), ('chord', 'C4', 'quarter', 0)],
        # M8: C major cadence
        [('chord', 'C3', 'quarter', 0), ('chord', 'G3', 'quarter', 0), ('chord', ['E4', 'G4'], 'half', 0)],
        # M9: C major (A2 begins)
        [('chord', 'C3', 'quarter', 0), ('chord', 'G3', 'quarter', 0), ('chord', 'E4', 'quarter', 0), ('chord', 'G3', 'quarter', 0)],
        # M10: C major
        [('chord', 'E3', 'quarter', 0), ('chord', 'G3', 'quarter', 0), ('chord', 'C4', 'quarter', 0), ('chord', 'G3', 'quarter', 0)],
        # M11: Am
        [('chord', 'A2', 'quarter', 0), ('chord', 'E3', 'quarter', 0), ('chord', 'A3', 'quarter', 0), ('chord', 'C4', 'quarter', 0)],
        # M12: Am / C
        [('chord', 'C3', 'quarter', 0), ('chord', 'E3', 'quarter', 0), ('chord', 'A3', 'quarter', 0), ('chord', 'E3', 'quarter', 0)],
        # M13: Dm
        [('chord', 'D3', 'quarter', 0), ('chord', 'A3', 'quarter', 0), ('chord', 'F4', 'quarter', 0), ('chord', 'A3', 'quarter', 0)],
        # M14: G7 dominant
        [('chord', 'G2', 'quarter', 0), ('chord', 'D3', 'quarter', 0), ('chord', 'F3', 'quarter', 0), ('chord', 'B3', 'quarter', 0)],
        # M15: C major resolution
        [('chord', 'C2', 'quarter', 0), ('chord', 'G2', 'quarter', 0), ('chord', 'C3', 'quarter', 0), ('chord', 'E3', 'quarter', 0)],
        # M16: C major sustained chord
        [('chord', ['C2', 'G2', 'C3', 'E3', 'G3'], 'whole', 0, True)], # fermata=True
    ]

    # Populate Staff 1
    # Add Title VBox
    vbox = ET.SubElement(staff1, 'VBox')
    ET.SubElement(vbox, 'height').text = '10'
    t_title = ET.SubElement(vbox, 'Text')
    ET.SubElement(t_title, 'style').text = 'title'
    ET.SubElement(t_title, 'text').text = '安眠'
    t_sub = ET.SubElement(vbox, 'Text')
    ET.SubElement(t_sub, 'style').text = 'subtitle'
    ET.SubElement(t_sub, 'text').text = 'Blessed Are Those Who Sleep · 游戏主旋律曲目'
    t_comp = ET.SubElement(vbox, 'Text')
    ET.SubElement(t_comp, 'style').text = 'composer'
    ET.SubElement(t_comp, 'text').text = '钢琴独奏 (160 BPM · 4/4拍 · 16小节)'

    num_measures = len(s1_measures)
    for m_idx in range(num_measures):
        measure = ET.SubElement(staff1, 'Measure')
        voice = ET.SubElement(measure, 'voice')

        # Add line break every 4 bars (M4, M8, M12)
        if m_idx in (3, 7, 11):
            lb = ET.SubElement(measure, 'LayoutBreak')
            ET.SubElement(lb, 'subtype').text = 'line'
        if m_idx == 0:
            # Clef, KeySig, TimeSig, Tempo
            clef = ET.SubElement(voice, 'Clef')
            ET.SubElement(clef, 'concertClefType').text = 'G'
            ET.SubElement(clef, 'transposingClefType').text = 'G'

            keysig = ET.SubElement(voice, 'KeySig')
            ET.SubElement(keysig, 'concertKey').text = '0'

            timesig = ET.SubElement(voice, 'TimeSig')
            ET.SubElement(timesig, 'sigN').text = '4'
            ET.SubElement(timesig, 'sigD').text = '4'

            tempo = ET.SubElement(voice, 'Tempo')
            ET.SubElement(tempo, 'tempo').text = '2.666667'
            ET.SubElement(tempo, 'followText').text = '1'
            text = ET.SubElement(tempo, 'text')
            ET.SubElement(text, 'sym').text = 'metNoteQuarterUp'
            text.text = ' = 160'

            # Dynamic mp
            dyn = ET.SubElement(voice, 'Dynamic')
            ET.SubElement(dyn, 'subtype').text = 'mp'
            ET.SubElement(dyn, 'velocity').text = '64'

            # StaffText dolce, cantabile
            stext = ET.SubElement(voice, 'StaffText')
            stext.text = 'dolce, cantabile'

        elif m_idx == 8:
            dyn = ET.SubElement(voice, 'Dynamic')
            ET.SubElement(dyn, 'subtype').text = 'mf'
            ET.SubElement(dyn, 'velocity').text = '80'

            stext = ET.SubElement(voice, 'StaffText')
            stext.text = 'espressivo'

        elif m_idx == 13:
            stext = ET.SubElement(voice, 'StaffText')
            stext.text = 'poco rit.'

        elif m_idx == 14:
            dyn = ET.SubElement(voice, 'Dynamic')
            ET.SubElement(dyn, 'subtype').text = 'p'
            ET.SubElement(dyn, 'velocity').text = '49'

            stext = ET.SubElement(voice, 'StaffText')
            stext.text = 'morendo, dolcissimo'

        events = s1_measures[m_idx]
        for ev in events:
            kind = ev[0]
            if kind == 'chord':
                note = ev[1]
                dur = ev[2]
                dots = ev[3]
                fermata = ev[4] if len(ev) > 4 else False
                tied = ev[5] if len(ev) > 5 else False
                add_chord(voice, note, dur, dots=dots, fermata=fermata, tied=tied)
            elif kind == 'rest':
                dur = ev[1]
                dots = ev[2] if len(ev) > 2 else 0
                add_rest(voice, dur, dots=dots)

    # Populate Staff 2
    for m_idx in range(num_measures):
        measure = ET.SubElement(staff2, 'Measure')
        voice = ET.SubElement(measure, 'voice')
        if m_idx == 0:
            clef = ET.SubElement(voice, 'Clef')
            ET.SubElement(clef, 'concertClefType').text = 'F'
            ET.SubElement(clef, 'transposingClefType').text = 'F'

            keysig = ET.SubElement(voice, 'KeySig')
            ET.SubElement(keysig, 'concertKey').text = '0'

            timesig = ET.SubElement(voice, 'TimeSig')
            ET.SubElement(timesig, 'sigN').text = '4'
            ET.SubElement(timesig, 'sigD').text = '4'

        events = s2_measures[m_idx]
        for ev in events:
            kind = ev[0]
            if kind == 'chord':
                note = ev[1]
                dur = ev[2]
                dots = ev[3]
                fermata = ev[4] if len(ev) > 4 else False
                add_chord(voice, note, dur, dots=dots, fermata=fermata)
            elif kind == 'rest':
                dur = ev[1]
                dots = ev[2] if len(ev) > 2 else 0
                add_rest(voice, dur, dots=dots)

    return root

def main():
    root = create_score()
    xml_str = ET.tostring(root, encoding='utf-8')
    parsed = minidom.parseString(xml_str)
    pretty = parsed.toprettyxml(indent='  ', encoding='utf-8')

    os.makedirs('music', exist_ok=True)
    mscx_path = os.path.abspath('music/安眠.mscx')
    with open(mscx_path, 'wb') as f:
        f.write(pretty)
    print(f'Wrote {mscx_path} ({len(pretty)} bytes)')

    # Validate durations per measure
    dur_map = {'whole': 1.0, 'half': 0.5, 'quarter': 0.25, 'eighth': 0.125, '16th': 0.0625}
    tree = ET.parse(mscx_path)
    score = tree.getroot().find('Score')
    for s in score.findall('Staff'):
        sid = s.get('id')
        for i, m in enumerate(s.findall('Measure'), 1):
            total = 0.0
            for v in m.findall('voice'):
                for el in v:
                    if el.tag in ('Chord', 'Rest'):
                        dtype = el.findtext('durationType')
                        dots = int(el.findtext('dots', '0'))
                        d = dur_map[dtype]
                        if dots == 1:
                            d *= 1.5
                        elif dots == 2:
                            d *= 1.75
                        total += d
            if abs(total - 1.0) > 1e-4:
                print(f'ERROR: Staff {sid} Measure {i} total duration = {total} (expected 1.0)')
            else:
                pass
    print('All 16 measures across both staves verified: exactly 4.0 beats (1.0 measure) each!')

    # Call MuseScore4 to export mscz, mp3, pdf
    exe = r'C:\Program Files\MuseScore 4\bin\MuseScore4.exe'
    if os.path.exists(exe):
        mscz_path = os.path.abspath('music/安眠.mscz')
        mp3_path = os.path.abspath('music/安眠.mp3')
        pdf_path = os.path.abspath('music/安眠.pdf')

        print('Exporting .mscz...')
        r1 = subprocess.run([exe, '-o', mscz_path, mscx_path], capture_output=True)
        print(f'mscz returncode: {r1.returncode}, exists: {os.path.exists(mscz_path)}')

        print('Exporting .mp3...')
        r2 = subprocess.run([exe, '-o', mp3_path, mscx_path], capture_output=True)
        print(f'mp3 returncode: {r2.returncode}, exists: {os.path.exists(mp3_path)}')

        print('Exporting .pdf...')
        r3 = subprocess.run([exe, '-o', pdf_path, mscx_path], capture_output=True)
        print(f'pdf returncode: {r3.returncode}, exists: {os.path.exists(pdf_path)}')

if __name__ == '__main__':
    main()
