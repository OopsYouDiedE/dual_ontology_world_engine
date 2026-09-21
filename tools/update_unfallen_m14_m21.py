import os
import subprocess
import zipfile
import xml.etree.ElementTree as ET
from xml.dom import minidom

SRC_MSCZ = 'music/The Unfallen Duet.mscz'
TEMP_MSCX = 'music/The Unfallen Duet.mscx'
MUSESCORE_EXE = r'C:\Program Files\MuseScore 4\bin\MuseScore4.exe'

dur_map = {'whole': 1.0, 'half': 0.5, 'quarter': 0.25, 'eighth': 0.125, '16th': 0.0625}

tpc_map = {'C': 14, 'D': 16, 'E': 18, 'F': 13, 'G': 15, 'A': 17, 'B': 19}
semitones = {'C': 0, 'D': 2, 'E': 4, 'F': 5, 'G': 7, 'A': 9, 'B': 11}

def pitch_tpc(note_name):
    # note_name like 'E5', 'C4'
    name = note_name[0]
    octave = int(note_name[1:])
    p = 12 * (octave + 1) + semitones[name]
    tpc = tpc_map[name]
    return p, tpc

def add_chord(voice, notes, dur, dots=0):
    chord = ET.SubElement(voice, 'Chord')
    if dots > 0:
        ET.SubElement(chord, 'dots').text = str(dots)
    ET.SubElement(chord, 'durationType').text = dur
    if not isinstance(notes, list):
        notes = [notes]
    for n in notes:
        p, tpc = pitch_tpc(n)
        note_el = ET.SubElement(chord, 'Note')
        ET.SubElement(note_el, 'pitch').text = str(p)
        ET.SubElement(note_el, 'tpc').text = str(tpc)

def add_rest(voice, dur, dots=0):
    rest = ET.SubElement(voice, 'Rest')
    if dots > 0:
        ET.SubElement(rest, 'dots').text = str(dots)
    ET.SubElement(rest, 'durationType').text = dur

def update_unfallen_duet():
    # 1. Extract The Unfallen Duet.mscx from zip
    with zipfile.ZipFile(SRC_MSCZ, 'r') as z:
        mscx_content = None
        for name in z.namelist():
            if name.endswith('.mscx'):
                mscx_content = z.read(name)
                break
    
    if not mscx_content:
        raise ValueError('Could not find .mscx inside .mscz')

    root = ET.fromstring(mscx_content)
    staffs = {s.get('id'): s for s in root.iter('Staff')}
    
    s1 = staffs['1'] # Violin
    s2 = staffs['2'] # Piano RH
    s3 = staffs['3'] # Piano LH

    # Define the new piano arrangement for M14-M21 (index 13 to 20)
    # Right Hand (Staff 2):
    # M14 (Vln: E4 half, C4 half) -> C major: E5(half), G4(quarter)+C5(quarter)
    # M15 (Vln: E4 quarter, F4 quarter, E4 half) -> C -> F -> C: G4+E5(quarter), A4+F5(quarter), G4+E5(half)
    # M16 (Vln: E4 half, C4 half) -> Am: C5+E5(half), A4+C5(half)
    # M17 (Vln: F4 whole) -> F: C5+F5(half), A4+C5(half)
    # M18 (Vln: E4 half, C4 half) -> C: E5(half), G4+C5(half)
    # M19 (Vln: E4 quarter, F4 quarter, E4 half) -> G4+E5(quarter), A4+F5(quarter), G4+E5(half)
    # M20 (Vln: E4 quarter, C4 quarter, D4 quarter, E4 quarter) -> C -> Dm: G4+E5(quarter), E4+C5(quarter), F4+D5(quarter), G4+E5(quarter)
    # M21 (Vln: D4 whole) -> G dominant: F4+B4+D5(whole)
    
    pno_rh_measures = [
        # M14
        [('chord', ['G4', 'E5'], 'half', 0), ('chord', ['E4', 'C5'], 'half', 0)],
        # M15
        [('chord', ['G4', 'E5'], 'quarter', 0), ('chord', ['A4', 'F5'], 'quarter', 0), ('chord', ['G4', 'E5'], 'half', 0)],
        # M16
        [('chord', ['A4', 'E5'], 'half', 0), ('chord', ['E4', 'C5'], 'half', 0)],
        # M17
        [('chord', ['A4', 'F5'], 'half', 0), ('chord', ['F4', 'C5'], 'half', 0)],
        # M18
        [('chord', ['G4', 'E5'], 'half', 0), ('chord', ['E4', 'C5'], 'half', 0)],
        # M19
        [('chord', ['G4', 'E5'], 'quarter', 0), ('chord', ['A4', 'F5'], 'quarter', 0), ('chord', ['G4', 'E5'], 'half', 0)],
        # M20
        [('chord', ['G4', 'E5'], 'quarter', 0), ('chord', ['E4', 'C5'], 'quarter', 0), ('chord', ['F4', 'D5'], 'quarter', 0), ('chord', ['G4', 'E5'], 'quarter', 0)],
        # M21
        [('chord', ['F4', 'B4', 'D5'], 'whole', 0)],
    ]

    # Left Hand (Staff 3):
    # M14 -> C: C3(quarter), G3(quarter), E3(quarter), G3(quarter)
    # M15 -> C-F-C: C3(quarter), F3(quarter), G3(quarter), C3(quarter)
    # M16 -> Am: A2(quarter), E3(quarter), C3(quarter), E3(quarter)
    # M17 -> F: F2(quarter), C3(quarter), A3(quarter), C3(quarter)
    # M18 -> C/Am: C3(quarter), G3(quarter), A2(quarter), E3(quarter)
    # M19 -> F-G: F2(quarter), C3(quarter), G2(quarter), D3(quarter)
    # M20 -> C-Am-Dm-G: C3(quarter), A2(quarter), D3(quarter), E3(quarter)
    # M21 -> G: G2(quarter), D3(quarter), B2(quarter), G2(quarter)

    pno_lh_measures = [
        # M14
        [('chord', 'C3', 'quarter', 0), ('chord', 'G3', 'quarter', 0), ('chord', 'E3', 'quarter', 0), ('chord', 'G3', 'quarter', 0)],
        # M15
        [('chord', 'C3', 'quarter', 0), ('chord', 'F3', 'quarter', 0), ('chord', 'G3', 'quarter', 0), ('chord', 'C3', 'quarter', 0)],
        # M16
        [('chord', 'A2', 'quarter', 0), ('chord', 'E3', 'quarter', 0), ('chord', 'C3', 'quarter', 0), ('chord', 'E3', 'quarter', 0)],
        # M17
        [('chord', 'F2', 'quarter', 0), ('chord', 'C3', 'quarter', 0), ('chord', 'A2', 'quarter', 0), ('chord', 'C3', 'quarter', 0)],
        # M18
        [('chord', 'C3', 'quarter', 0), ('chord', 'G3', 'quarter', 0), ('chord', 'A2', 'quarter', 0), ('chord', 'E3', 'quarter', 0)],
        # M19
        [('chord', 'F2', 'quarter', 0), ('chord', 'C3', 'quarter', 0), ('chord', 'G2', 'quarter', 0), ('chord', 'D3', 'quarter', 0)],
        # M20
        [('chord', 'C3', 'quarter', 0), ('chord', 'A2', 'quarter', 0), ('chord', 'D3', 'quarter', 0), ('chord', 'E3', 'quarter', 0)],
        # M21
        [('chord', 'G2', 'quarter', 0), ('chord', 'D3', 'quarter', 0), ('chord', 'B2', 'quarter', 0), ('chord', 'G2', 'quarter', 0)],
    ]

    m_rh_list = s2.findall('Measure')
    m_lh_list = s3.findall('Measure')

    for bar_idx in range(13, 21): # M14 to M21
        # Update RH
        mr = m_rh_list[bar_idx]
        vr = mr.find('voice')
        # Remove old chords and rests
        for el in list(vr):
            if el.tag in ('Chord', 'Rest'):
                vr.remove(el)
        rh_events = pno_rh_measures[bar_idx - 13]
        for ev in rh_events:
            notes, dur, dots = ev[1], ev[2], ev[3]
            add_chord(vr, notes, dur, dots=dots)

        # Update LH
        ml = m_lh_list[bar_idx]
        vl = ml.find('voice')
        for el in list(vl):
            if el.tag in ('Chord', 'Rest'):
                vl.remove(el)
        lh_events = pno_lh_measures[bar_idx - 13]
        for ev in lh_events:
            notes, dur, dots = ev[1], ev[2], ev[3]
            add_chord(vl, notes, dur, dots=dots)

    # Validate measure durations
    for sid, staff in [('2', s2), ('3', s3)]:
        for bar_idx in range(13, 21):
            m = staff.findall('Measure')[bar_idx]
            tot = 0.0
            for el in m.find('voice'):
                if el.tag in ('Chord', 'Rest'):
                    d = dur_map[el.findtext('durationType')]
                    dots = int(el.findtext('dots', '0'))
                    if dots == 1: d *= 1.5
                    tot += d
            if abs(tot - 1.0) > 1e-4:
                raise ValueError(f'Staff {sid} Measure {bar_idx+1} duration invalid: {tot}')

    # Write out modified score
    xml_bytes = ET.tostring(root, encoding='utf-8')
    parsed = minidom.parseString(xml_bytes)
    pretty = parsed.toprettyxml(indent='  ', encoding='utf-8')
    with open(TEMP_MSCX, 'wb') as f:
        f.write(pretty)
    print(f'Wrote updated score to {TEMP_MSCX}')

    # Re-render to .mscz and .mp3
    if os.path.exists(MUSESCORE_EXE):
        mscz_out = os.path.abspath(SRC_MSCZ)
        mp3_out = os.path.abspath('music/The Unfallen Duet.mp3')
        pdf_out = os.path.abspath('music/The Unfallen Duet.pdf')
        
        print('Rendering to The Unfallen Duet.mscz...')
        subprocess.run([MUSESCORE_EXE, '-o', mscz_out, os.path.abspath(TEMP_MSCX)], capture_output=True)
        print('Rendering to The Unfallen Duet.mp3...')
        subprocess.run([MUSESCORE_EXE, '-o', mp3_out, os.path.abspath(TEMP_MSCX)], capture_output=True)
        print('Rendering to The Unfallen Duet.pdf...')
        subprocess.run([MUSESCORE_EXE, '-o', pdf_out, os.path.abspath(TEMP_MSCX)], capture_output=True)
        print('All exports complete!')

if __name__ == '__main__':
    update_unfallen_duet()
