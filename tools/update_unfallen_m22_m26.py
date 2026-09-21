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

def update_unfallen_transition():
    # 1. Read existing The Unfallen Duet.mscx (which already has M14-M21 updated)
    tree = ET.parse(TEMP_MSCX)
    root = tree.getroot()
    staffs = {s.get('id'): s for s in root.iter('Staff')}
    
    s1 = staffs['1'] # Violin
    s2 = staffs['2'] # Piano RH
    s3 = staffs['3'] # Piano LH

    # 1. Update Violin in M22-M26:
    # M22-M26 is transition (tacet/rest for violin, letting piano lead the transition just like M9-M13)
    # M22 to M26 in Staff 1 -> whole rests
    m_vln_list = s1.findall('Measure')
    for bar_idx in range(21, 26): # M22 to M26 (index 21 to 25)
        m = m_vln_list[bar_idx]
        v = m.find('voice')
        for el in list(v):
            if el.tag in ('Chord', 'Rest'):
                v.remove(el)
        add_rest(v, 'measure')

    # 2. Update Piano RH in M22-M26 (modeled on M9-M13):
    # M9-M13 pattern:
    # M9: A5(quarter) G5(quarter) F5(half)
    # M10: E5(half) C5(half)
    # M11: D5(quarter) E5(quarter) C5(half)
    # M12: D5(half) B4(half)
    # M13: B4(quarter) A4(quarter) G4(quarter) E4(quarter) -> lead into next section
    #
    # For M22-M26, leading gracefully into Part III (G major / B5 in M30/M31):
    # M22: A5(quarter) G5(quarter) F5(half)
    # M23: E5(half) C5(half)
    # M24: D5(quarter) E5(quarter) C5(half)
    # M25: D5(half) B4(half)
    # M26: G4(quarter) B4(quarter) D5(quarter) G5(quarter) [Arpeggio transition upwards to G/B]
    pno_rh_measures = [
        # M22 (corresponds to M9)
        [('chord', 'A5', 'quarter', 0), ('chord', 'G5', 'quarter', 0), ('chord', 'F5', 'half', 0)],
        # M23 (corresponds to M10)
        [('chord', 'E5', 'half', 0), ('chord', 'C5', 'half', 0)],
        # M24 (corresponds to M11)
        [('chord', 'D5', 'quarter', 0), ('chord', 'E5', 'quarter', 0), ('chord', 'C5', 'half', 0)],
        # M25 (corresponds to M12)
        [('chord', 'D5', 'half', 0), ('chord', 'B4', 'half', 0)],
        # M26 (corresponds to M13, leading into G major / Part III)
        [('chord', 'G4', 'quarter', 0), ('chord', 'B4', 'quarter', 0), ('chord', 'D5', 'quarter', 0), ('chord', 'G5', 'quarter', 0)],
    ]

    # 3. Update Piano LH in M22-M26 (modeled on M9-M13):
    # M9: F3(quarter) C4(quarter) A4(quarter) C4(quarter)
    # M10: E3(quarter) G3(quarter) C4(quarter) E4(quarter)
    # M11: D3(quarter) A3(quarter) F4(quarter) C4(quarter)
    # M12: G2(quarter) D3(quarter) B3(quarter) D4(quarter)
    # M13: G2(half) C2(half)
    pno_lh_measures = [
        # M22
        [('chord', 'F3', 'quarter', 0), ('chord', 'C4', 'quarter', 0), ('chord', 'A3', 'quarter', 0), ('chord', 'C4', 'quarter', 0)],
        # M23
        [('chord', 'E3', 'quarter', 0), ('chord', 'G3', 'quarter', 0), ('chord', 'C4', 'quarter', 0), ('chord', 'E4', 'quarter', 0)],
        # M24
        [('chord', 'D3', 'quarter', 0), ('chord', 'A3', 'quarter', 0), ('chord', 'F3', 'quarter', 0), ('chord', 'C4', 'quarter', 0)],
        # M25
        [('chord', 'G2', 'quarter', 0), ('chord', 'D3', 'quarter', 0), ('chord', 'B2', 'quarter', 0), ('chord', 'D3', 'quarter', 0)],
        # M26
        [('chord', 'G2', 'half', 0), ('chord', 'G2', 'half', 0)],
    ]

    m_rh_list = s2.findall('Measure')
    m_lh_list = s3.findall('Measure')

    for bar_idx in range(21, 26): # M22 to M26
        # Update RH
        mr = m_rh_list[bar_idx]
        vr = mr.find('voice')
        for el in list(vr):
            if el.tag in ('Chord', 'Rest'):
                vr.remove(el)
        rh_events = pno_rh_measures[bar_idx - 21]
        for ev in rh_events:
            notes, dur, dots = ev[1], ev[2], ev[3]
            add_chord(vr, notes, dur, dots=dots)

        # Update LH
        ml = m_lh_list[bar_idx]
        vl = ml.find('voice')
        for el in list(vl):
            if el.tag in ('Chord', 'Rest'):
                vl.remove(el)
        lh_events = pno_lh_measures[bar_idx - 21]
        for ev in lh_events:
            notes, dur, dots = ev[1], ev[2], ev[3]
            add_chord(vl, notes, dur, dots=dots)

    # Validate measure durations
    for sid, staff in [('1', s1), ('2', s2), ('3', s3)]:
        for bar_idx in range(21, 26):
            m = staff.findall('Measure')[bar_idx]
            tot = 0.0
            for el in m.find('voice'):
                if el.tag in ('Chord', 'Rest'):
                    dtype = el.findtext('durationType')
                    if dtype == 'measure':
                        tot = 1.0
                        break
                    d = dur_map[dtype]
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
    print(f'Wrote updated transition score to {TEMP_MSCX}')

    # Re-render to .mscz, .mp3, .pdf
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
    update_unfallen_transition()
