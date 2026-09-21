import zipfile
import xml.etree.ElementTree as ET
import copy
import os
import subprocess

pitch_map = {
    'C1': 24, 'D1': 26, 'E1': 28, 'F1': 29, 'G1': 31, 'A1': 33, 'B1': 35,
    'C2': 36, 'Db2': 37, 'D2': 38, 'Eb2': 39, 'E2': 40, 'F2': 41, 'F#2': 42, 'Gb2': 42, 'G2': 43, 'Ab2': 44, 'A2': 45, 'Bb2': 46, 'B2': 47,
    'C3': 48, 'Db3': 49, 'D3': 50, 'Eb3': 51, 'E3': 52, 'F3': 53, 'F#3': 54, 'Gb3': 54, 'G3': 55, 'Ab3': 56, 'A3': 57, 'Bb3': 58, 'B3': 59,
    'C4': 60, 'Db4': 61, 'C#4': 61, 'D4': 62, 'Eb4': 63, 'E4': 64, 'F4': 65, 'F#4': 66, 'Gb4': 66, 'G4': 67, 'Ab4': 68, 'A4': 69, 'Bb4': 70, 'B4': 71,
    'C5': 72, 'Db5': 73, 'C#5': 73, 'D5': 74, 'Eb5': 75, 'E5': 76, 'F5': 77, 'F#5': 78, 'Gb5': 78, 'G5': 79, 'Ab5': 80, 'A5': 81, 'Bb5': 82, 'B5': 83,
    'C6': 84, 'Db6': 85, 'D6': 86, 'Eb6': 87, 'E6': 88, 'F6': 89, 'F#6': 90, 'Gb6': 90, 'G6': 91, 'Ab6': 92, 'A6': 93, 'Bb6': 94, 'B6': 95, 'C7': 96
}

tpc_map = {
    'C': 14, 'Db': 9, 'D': 16, 'Eb': 11, 'E': 18, 'F': 13, 'Gb': 8, 'F#': 20,
    'G': 15, 'Ab': 10, 'A': 17, 'Bb': 12, 'B': 19
}

def get_tpc(name):
    clean = name.rstrip('0123456789')
    return tpc_map.get(clean, None)

def p(name):
    return pitch_map[name]

def make_note(pitch_val, tpc_val=None, has_flat=False):
    n = ET.Element('Note')
    p_el = ET.SubElement(n, 'pitch')
    p_el.text = str(pitch_val)
    if tpc_val is not None:
        t_el = ET.SubElement(n, 'tpc')
        t_el.text = str(tpc_val)
    if has_flat:
        acc = ET.SubElement(n, 'Accidental')
        s_el = ET.SubElement(acc, 'subtype')
        s_el.text = 'accidentalFlat'
    return n

def make_chord(dur, pitches, dots=0, accent=False, fermata=False):
    ch = ET.Element('Chord')
    d_el = ET.SubElement(ch, 'durationType')
    d_el.text = dur
    if dots > 0:
        dot_el = ET.SubElement(ch, 'dots')
        dot_el.text = str(dots)
    if accent:
        art = ET.SubElement(ch, 'Articulation')
        sub = ET.SubElement(art, 'subtype')
        sub.text = 'articAccentAbove'
    if fermata:
        art = ET.SubElement(ch, 'Articulation')
        sub = ET.SubElement(art, 'subtype')
        sub.text = 'fermataAbove'
    if isinstance(pitches, (int, str)):
        pitches = [pitches]
    for pitch in pitches:
        val = p(pitch) if isinstance(pitch, str) else pitch
        tpc_v = get_tpc(pitch) if isinstance(pitch, str) else None
        is_flat = isinstance(pitch, str) and ('b' in pitch)
        ch.append(make_note(val, tpc_v, is_flat))
    return ch

def make_rest(dur, dots=0):
    r = ET.Element('Rest')
    d_el = ET.SubElement(r, 'durationType')
    d_el.text = dur
    if dots > 0:
        dot_el = ET.SubElement(r, 'dots')
        dot_el.text = str(dots)
    return r

def make_tempo(bpm):
    t = ET.Element('Tempo')
    t_val = ET.SubElement(t, 'tempo')
    t_val.text = str(bpm / 60.0)
    f_text = ET.SubElement(t, 'followText')
    f_text.text = '1'
    txt = ET.SubElement(t, 'text')
    sym = ET.SubElement(txt, 'sym')
    sym.text = 'metNoteQuarterUp'
    fnt = ET.SubElement(txt, 'font')
    fnt.set('face', 'Edwin')
    fnt.tail = f' = {bpm}'
    return t

def replace_measure_elements(voice, new_elements):
    preserved = [el for el in voice if el.tag not in ('Chord', 'Rest')]
    voice.clear()
    for el in preserved:
        voice.append(el)
    for el in new_elements:
        voice.append(el)

def get_or_create_voice(measure, voice_idx=0):
    voices = measure.findall('voice')
    while len(voices) <= voice_idx:
        new_v = ET.SubElement(measure, 'voice')
        voices.append(new_v)
    return voices[voice_idx]

def set_single_voice(measure, new_elements):
    voices = measure.findall('voice')
    if voices:
        v0 = voices[0]
        preserved = [el for el in v0 if el.tag not in ('Chord', 'Rest')]
        v0.clear()
        for el in preserved:
            v0.append(el)
        for el in new_elements:
            v0.append(el)
        for v in voices[1:]:
            measure.remove(v)
    else:
        v = ET.SubElement(measure, 'voice')
        for el in new_elements:
            v.append(el)

# Data definition for M67-74 swap (indices 66 to 73)
def get_m67_74_elements():
    # Piano RH: 16th notes motor
    pno_r_notes = {
        66: ['C4', 'Eb4', 'D4', 'C4', 'G3', 'C4', 'Eb4', 'G4', 'C4', 'D4', 'Eb4', 'F#4', 'G4', 'D4', 'Eb4', 'D4'],
        67: ['C4', 'G3', 'C4', 'Eb4', 'D4', 'G3', 'D4', 'F4', 'Eb4', 'C4', 'G4', 'Eb4', 'D4', 'Bb3', 'G3', 'Bb3'],
        68: ['C4', 'Ab3', 'F4', 'C4', 'D4', 'Ab3', 'F4', 'D4', 'B3', 'D4', 'F4', 'Ab4', 'G4', 'F4', 'D4', 'B3'],
        69: ['C4', 'G3', 'D4', 'G3', 'Eb4', 'G3', 'D4', 'G3', 'F4', 'G3', 'Eb4', 'G3', 'D4', 'G3', 'B3', 'D4'],
        70: ['C4', 'Eb4', 'G4', 'C5', 'D5', 'C5', 'Bb4', 'Ab4', 'G4', 'Eb4', 'C4', 'G3', 'C4', 'Eb4', 'G4', 'C5'],
        71: ['Db4', 'F4', 'Ab4', 'Db5', 'Db4', 'F4', 'Ab4', 'Db5', 'Db4', 'F4', 'Ab4', 'Db5', 'Db4', 'F4', 'Ab4', 'Db5'],
        72: ['Ab4', 'G4', 'F4', 'Eb4', 'D4', 'F4', 'Ab4', 'C5', 'Bb4', 'Ab4', 'G4', 'F4', 'Eb4', 'G4', 'Bb4', 'D5'],
        73: ['C5', 'B4', 'C5', 'D5', 'G4', 'F#4', 'G4', 'Ab4', 'D4', 'G3', 'B3', 'D4', 'G4', 'D4', 'B3', 'G3'],
    }
    
    # Violin: 8th notes and quarter/half notes
    vln_elements = {
        66: [make_chord('eighth', p) for p in ['Eb5', 'G5', 'C6', 'G5', 'Eb5', 'G5', 'C6', 'Eb6']],
        67: [make_chord('eighth', p) for p in ['G5', 'Bb5', 'D6', 'Bb5', 'G5', 'C6', 'Bb5', 'G5']],
        68: [make_chord('eighth', p) for p in ['C5', 'F5', 'Ab5', 'C6', 'D5', 'F5', 'Ab5', 'C6']],
        69: [make_chord('eighth', p) for p in ['B4', 'D5', 'F5', 'G5', 'B5', 'G5', 'F5', 'D5']],
        70: [make_chord('eighth', p) for p in ['Eb5', 'G5', 'C6', 'Eb6', 'C6', 'G5', 'Eb5', 'G5']],
        71: [make_chord('quarter', 'F5'), make_chord('quarter', 'Ab5'), make_chord('half', 'Db6')],
        72: [make_chord('eighth', p) for p in ['C5', 'Eb5', 'Ab5', 'C6', 'D5', 'F5', 'Ab5', 'D6']],
        73: [make_chord('quarter', 'C6'), make_chord('quarter', 'B5'), make_chord('half', 'G5')],
    }
    
    pno_r_elements = {}
    for idx, notes in pno_r_notes.items():
        pno_r_elements[idx] = [make_chord('16th', p) for p in notes]
        
    return pno_r_elements, vln_elements

def update_scores():
    duet_path = 'music/The Unfallen Duet.mscz'
    solo_path = 'music/The Unfallen Piano Solo.mscz'
    
    # 1. Update Duet
    with zipfile.ZipFile(duet_path, 'r') as z:
        mscx_name = [n for n in z.namelist() if n.endswith('.mscx')][0]
        duet_xml_content = z.read(mscx_name).decode('utf-8')
        thumb = z.read('Thumbnails/thumbnail.png')
        auto = z.read('automation.json')
        aud = z.read('audiosettings.json')
        view = z.read('viewsettings.json')
        meta = z.read('META-INF/container.xml')
        style = z.read('score_style.mss')

    duet_tree = ET.fromstring(duet_xml_content)
    duet_score = duet_tree.find('Score') if duet_tree.tag != 'Score' else duet_tree
    d_staves = duet_score.findall('Staff')
    s_vln = d_staves[0]
    s_pno_r = d_staves[1]
    s_pno_l = d_staves[2]

    m_vln = s_vln.findall('Measure')
    m_pno_r = s_pno_r.findall('Measure')
    m_pno_l = s_pno_l.findall('Measure')

    print(f"Duet score total measures: {len(m_vln)}")

    pno_r_elements, vln_elements = get_m67_74_elements()

    # Apply M67-74 swap (indices 66 to 73)
    for idx in range(66, 74):
        set_single_voice(m_vln[idx], vln_elements[idx])
        set_single_voice(m_pno_r[idx], pno_r_elements[idx])

    # Save updated Duet
    out_duet_xml = ET.tostring(duet_tree, encoding='utf-8', xml_declaration=True)
    duet_tmp = duet_path + '.tmp'
    with zipfile.ZipFile(duet_tmp, 'w', compression=zipfile.ZIP_DEFLATED) as z:
        z.writestr(mscx_name, out_duet_xml)
        z.writestr('Thumbnails/thumbnail.png', thumb)
        z.writestr('automation.json', auto)
        z.writestr('audiosettings.json', aud)
        z.writestr('viewsettings.json', view)
        z.writestr('META-INF/container.xml', meta)
        z.writestr('score_style.mss', style)
    os.replace(duet_tmp, duet_path)
    print("Successfully updated The Unfallen Duet.mscz with M67-74 swap.")

    # 2. Update Piano Solo
    with zipfile.ZipFile(solo_path, 'r') as z:
        solo_mscx_name = [n for n in z.namelist() if n.endswith('.mscx')][0]
        solo_xml_content = z.read(solo_mscx_name).decode('utf-8')
        solo_thumb = z.read('Thumbnails/thumbnail.png')
        solo_auto = z.read('automation.json')
        solo_aud = z.read('audiosettings.json')
        solo_view = z.read('viewsettings.json')
        solo_meta = z.read('META-INF/container.xml')
        solo_style = z.read('score_style.mss')

    solo_tree = ET.fromstring(solo_xml_content)
    solo_score = solo_tree.find('Score') if solo_tree.tag != 'Score' else solo_tree
    s_staves_solo = solo_score.findall('Staff')
    s_solo_treble = s_staves_solo[0]
    s_solo_bass = s_staves_solo[1]

    measures_t = s_solo_treble.findall('Measure')
    measures_b = s_solo_bass.findall('Measure')

    # Remove extra measures 35 and 32 if total measures is 110
    if len(measures_t) == 110:
        s_solo_treble.remove(measures_t[35])
        s_solo_treble.remove(measures_t[32])
        s_solo_bass.remove(measures_b[35])
        s_solo_bass.remove(measures_b[32])
        print("Removed extra measures 35 and 32 from Piano Solo, synced to 108 measures.")

    m_solo_t = s_solo_treble.findall('Measure')
    m_solo_b = s_solo_bass.findall('Measure')
    print(f"Solo score total measures: {len(m_solo_t)}")

    # Update M67-74 in Piano Solo
    for idx in range(66, 74):
        v0 = get_or_create_voice(m_solo_t[idx], 0)
        v1 = get_or_create_voice(m_solo_t[idx], 1)
        replace_measure_elements(v0, copy.deepcopy(vln_elements[idx]))
        replace_measure_elements(v1, copy.deepcopy(pno_r_elements[idx]))

        vb = get_or_create_voice(m_solo_b[idx], 0)
        # Take piano bass line from Duet
        duet_bass_elements = [copy.deepcopy(el) for el in m_pno_l[idx].find('voice') if el.tag in ('Chord', 'Rest')]
        replace_measure_elements(vb, duet_bass_elements)

    out_solo_xml = ET.tostring(solo_tree, encoding='utf-8', xml_declaration=True)
    solo_tmp = solo_path + '.tmp'
    with zipfile.ZipFile(solo_tmp, 'w', compression=zipfile.ZIP_DEFLATED) as z:
        z.writestr(solo_mscx_name, out_solo_xml)
        z.writestr('Thumbnails/thumbnail.png', solo_thumb)
        z.writestr('automation.json', solo_auto)
        z.writestr('audiosettings.json', solo_aud)
        z.writestr('viewsettings.json', solo_view)
        z.writestr('META-INF/container.xml', solo_meta)
        z.writestr('score_style.mss', solo_style)
    os.replace(solo_tmp, solo_path)
    print("Successfully updated The Unfallen Piano Solo.mscz with M67-74 swap.")

    # 3. Export audio and midi via MuseScore CLI
    musescore_exe = r'C:\Program Files\MuseScore 4\bin\MuseScore4.exe'
    if os.path.exists(musescore_exe):
        print("Exporting The Unfallen Duet MP3 and MIDI...")
        subprocess.run([musescore_exe, '-o', os.path.abspath('music/The Unfallen Duet.mp3'), os.path.abspath(duet_path)], check=True)
        subprocess.run([musescore_exe, '-o', os.path.abspath('music/The Unfallen Duet.mid'), os.path.abspath(duet_path)], check=True)
        
        print("Exporting The Unfallen Piano Solo MP3 and MIDI...")
        subprocess.run([musescore_exe, '-o', os.path.abspath('music/The Unfallen Piano Solo.mp3'), os.path.abspath(solo_path)], check=True)
        subprocess.run([musescore_exe, '-o', os.path.abspath('music/The Unfallen Piano Solo.mid'), os.path.abspath(solo_path)], check=True)
        print("All MuseScore audio exports completed successfully.")
    else:
        print(f"MuseScore executable not found at {musescore_exe}")

if __name__ == '__main__':
    update_scores()
