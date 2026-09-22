import zipfile
import xml.etree.ElementTree as ET
import os
import subprocess

base_pitch = {'C': 0, 'D': 2, 'E': 4, 'F': 5, 'G': 7, 'A': 9, 'B': 11}

def p(name):
    letter = name[0].upper()
    acc = name[1:-1] if len(name) > 2 else ''
    octave = int(name[-1])
    offset = base_pitch[letter]
    for ch in acc:
        if ch == '#':
            offset += 1
        elif ch == 'b':
            offset -= 1
    return (octave + 1) * 12 + offset

tpc_map = {
    'C': 14, 'Db': 9, 'C#': 21, 'D': 16, 'Eb': 11, 'D#': 23, 'E': 18, 'F': 13, 'Gb': 8, 'F#': 20,
    'G': 15, 'Ab': 10, 'G#': 22, 'A': 17, 'Bb': 12, 'A#': 24, 'B': 19
}

def get_tpc(name):
    clean = name.rstrip('0123456789')
    return tpc_map.get(clean, 14)

def make_note(pitch_val, tpc_val=None, has_flat=False, has_sharp=False):

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
    elif has_sharp:
        acc = ET.SubElement(n, 'Accidental')
        s_el = ET.SubElement(acc, 'subtype')
        s_el.text = 'accidentalSharp'
    return n

def make_chord(dur, pitches, dots=0, accent=False, tenuto=False, tremolo=False):
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
    if tenuto:
        art = ET.SubElement(ch, 'Articulation')
        sub = ET.SubElement(art, 'subtype')
        sub.text = 'articTenutoAbove'
    if tremolo:
        trem = ET.SubElement(ch, 'Tremolo')
        sub = ET.SubElement(trem, 'subtype')
        sub.text = 'r16'
    if isinstance(pitches, (int, str)):
        pitches = [pitches]
    for pitch in pitches:
        val = p(pitch) if isinstance(pitch, str) else pitch
        tpc_v = get_tpc(pitch) if isinstance(pitch, str) else None
        is_flat = isinstance(pitch, str) and ('b' in pitch)
        is_sharp = isinstance(pitch, str) and ('#' in pitch)
        ch.append(make_note(val, tpc_v, is_flat, is_sharp))
    return ch

def make_rest(dur, dots=0):
    r = ET.Element('Rest')
    d_el = ET.SubElement(r, 'durationType')
    d_el.text = dur
    if dots > 0:
        dot_el = ET.SubElement(r, 'dots')
        dot_el.text = str(dots)
    return r

def make_dynamic(dyn_str):
    d = ET.Element('Dynamic')
    st = ET.SubElement(d, 'subtype')
    st.text = dyn_str
    v_map = {'ppp': 16, 'pp': 33, 'p': 49, 'mp': 64, 'mf': 80, 'f': 96, 'ff': 112, 'fff': 124}
    vel = ET.SubElement(d, 'velocity')
    vel.text = str(v_map.get(dyn_str, 80))
    return d

def set_measure_elements(measure, elements, dynamic=None):
    preserved_head = []
    preserved_tail = []
    voices = measure.findall('voice')
    if not voices:
        v = ET.SubElement(measure, 'voice')
        voices = [v]
    v0 = voices[0]
    for el in list(v0):
        if el.tag in ('KeySig', 'TimeSig', 'Clef', 'Tempo', 'RehearsalMark', 'StaffText'):
            preserved_head.append(el)
        elif el.tag in ('BarLine', 'Spanner'):
            preserved_tail.append(el)
    v0.clear()
    for el in preserved_head:
        v0.append(el)
    if dynamic:
        v0.append(make_dynamic(dynamic))
    for el in elements:
        v0.append(el)
    for el in preserved_tail:
        v0.append(el)
    for v in voices[1:]:
        measure.remove(v)

def generate_reconstructed_score():
    base_mscz = 'music/The Unfallen Duet Base.mscz'
    out_mscz = 'music/The Unfallen Duet.mscz'
    
    with zipfile.ZipFile(base_mscz, 'r') as z:
        mscx_name = [n for n in z.namelist() if n.endswith('.mscx')][0]
        xml_content = z.read(mscx_name).decode('utf-8')
        other_files = {name: z.read(name) for name in z.namelist() if name != mscx_name}

    tree = ET.fromstring(xml_content)
    score = tree.find('Score') if tree.tag != 'Score' else tree
    staves = score.findall('Staff')
    s_vln = staves[0]
    s_pno_r = staves[1]
    s_pno_l = staves[2]

    m_vln = s_vln.findall('Measure')
    m_pno_r = s_pno_r.findall('Measure')
    m_pno_l = s_pno_l.findall('Measure')

    total_measures = len(m_vln)
    print(f"Total measures to reconstruct: {total_measures}")

    vln_dict = {}
    pno_r_dict = {}
    pno_l_dict = {}
    dyn_vln = {}
    dyn_pno = {}

    # === Part I: M1–12 (Exordium, Piano Solo, Violin Rests) ===
    for idx in range(0, 12):
        vln_dict[idx] = [make_rest('whole')]

    dyn_pno[0] = 'p'
    pno_r_dict[0] = [make_chord('half', 'E5'), make_chord('quarter', 'C5'), make_chord('quarter', 'E5')]
    pno_r_dict[1] = [make_chord('half', 'F5'), make_chord('half', 'G5')]
    pno_r_dict[2] = [make_chord('quarter', 'C6'), make_chord('quarter', 'G5'), make_chord('half', 'E5')]
    pno_r_dict[3] = [make_chord('quarter', 'F5'), make_chord('quarter', 'D5'), make_chord('half', 'D5')]
    dyn_pno[4] = 'mp'
    pno_r_dict[4] = [make_chord('half', 'E5'), make_chord('quarter', 'G5'), make_chord('quarter', 'C6')]
    pno_r_dict[5] = [make_chord('quarter', 'D6'), make_chord('quarter', 'C6'), make_chord('quarter', 'B5'), make_chord('quarter', 'A5')]
    pno_r_dict[6] = [make_chord('half', 'G5'), make_chord('quarter', 'F5'), make_chord('quarter', 'D5')]
    pno_r_dict[7] = [make_chord('half', 'E5'), make_chord('half', 'C5')]
    dyn_pno[8] = 'p'
    pno_r_dict[8] = [make_chord('quarter', 'B4'), make_chord('quarter', 'E5'), make_chord('half', 'G5')]
    pno_r_dict[9] = [make_chord('half', 'E5'), make_chord('half', 'C5')]
    pno_r_dict[10] = [make_chord('quarter', 'D5'), make_chord('quarter', 'F5'), make_chord('half', 'A4')]
    pno_r_dict[11] = [make_chord('half', 'G4'), make_chord('half', 'B4')]

    pno_l_dict[0] = [make_chord('half', ['C3', 'G3']), make_chord('half', ['E3', 'G3'])]
    pno_l_dict[1] = [make_chord('half', ['F2', 'C3']), make_chord('half', ['A2', 'C3'])]
    pno_l_dict[2] = [make_chord('half', ['C3', 'G3']), make_chord('half', ['E3', 'G3'])]
    pno_l_dict[3] = [make_chord('whole', ['G2', 'D3', 'B3'])]
    pno_l_dict[4] = [make_chord('half', ['C3', 'G3']), make_chord('half', ['E3', 'G3'])]
    pno_l_dict[5] = [make_chord('half', ['A2', 'E3']), make_chord('half', ['C3', 'E3'])]
    pno_l_dict[6] = [make_chord('half', ['F2', 'C3']), make_chord('half', ['G2', 'D3'])]
    pno_l_dict[7] = [make_chord('whole', ['C3', 'G3', 'C4'])]
    pno_l_dict[8] = [make_chord('whole', ['E2', 'B2', 'G3'])]
    pno_l_dict[9] = [make_chord('whole', ['A2', 'E3', 'C4'])]
    pno_l_dict[10] = [make_chord('whole', ['D2', 'A2', 'F3'])]
    pno_l_dict[11] = [make_chord('whole', ['G2', 'D3', 'B3'])]

    # === Part II: M13–28 (Theme B: Dorian Mode / Folk-Sacred Incantation - Occult Study with Mentor) ===
    # M13-20: Piano RH presents reformed Theme B (music1 arch: 5th leap + stepwise descent) while Violin weaves counterpoint dialogue
    dyn_vln[12] = 'p'
    vln_dict[12] = [make_chord('half', 'D4', tenuto=True), make_chord('half', 'F4', tenuto=True)]
    vln_dict[13] = [make_chord('half', 'E4', tenuto=True), make_chord('half', 'G4', tenuto=True)]
    vln_dict[14] = [make_chord('quarter', 'D4', tenuto=True), make_chord('quarter', 'F4', tenuto=True), make_chord('half', 'A4', tenuto=True)]
    vln_dict[15] = [make_chord('half', 'G4', tenuto=True), make_chord('half', 'B4', tenuto=True)]
    # M17-20: Violin introduces music1 m.26 stepwise motif in counterpoint dialogue
    vln_dict[16] = [make_chord('quarter', 'D4'), make_chord('quarter', 'E4'), make_chord('quarter', 'F4'), make_chord('quarter', 'E4')]
    vln_dict[17] = [make_chord('quarter', 'F4'), make_chord('quarter', 'E4'), make_chord('half', 'D4')]
    vln_dict[18] = [make_chord('quarter', 'D4'), make_chord('quarter', 'E4'), make_chord('quarter', 'F4'), make_chord('quarter', 'E4')]
    vln_dict[19] = [make_chord('quarter', 'C4'), make_chord('quarter', 'D4'), make_chord('half', 'E4')]

    # M21-28: Violin takes over the main melody in high register (music1 m.5-7 & m.44-46 arch)
    dyn_vln[20] = 'mf'
    vln_dict[20] = [make_chord('quarter', 'A5'), make_chord('quarter', 'C6'), make_chord('half', 'D6', accent=True)]
    vln_dict[21] = [make_chord('quarter', 'B5'), make_chord('quarter', 'B5'), make_chord('quarter', 'G5'), make_chord('quarter', 'G5')]
    vln_dict[22] = [make_chord('quarter', 'A5'), make_chord('quarter', 'C6'), make_chord('half', 'E6', accent=True)]
    vln_dict[23] = [make_chord('quarter', 'D6'), make_chord('quarter', 'B5'), make_chord('quarter', 'G5'), make_chord('quarter', 'E5')]
    vln_dict[24] = [make_chord('half', 'A5', accent=True), make_chord('quarter', 'C6'), make_chord('quarter', 'D6')]
    vln_dict[25] = [make_chord('quarter', 'D6'), make_chord('half', 'C6'), make_chord('quarter', 'B5')]
    vln_dict[26] = [make_chord('quarter', 'B5'), make_chord('quarter', 'A5'), make_chord('quarter', 'G5'), make_chord('quarter', 'F5')]
    vln_dict[27] = [make_chord('quarter', 'F5'), make_chord('quarter', 'A5'), make_chord('half', 'G5')]

    # Piano RH M13-20: Main Melody restructured with music1's clean modal arch
    dyn_pno[12] = 'mp'
    pno_r_dict[12] = [make_chord('quarter', 'D5'), make_chord('quarter', 'A5'), make_chord('half', 'G5')]
    pno_r_dict[13] = [make_chord('quarter', 'G5'), make_chord('half', 'E5'), make_chord('quarter', 'F5')]
    pno_r_dict[14] = [make_chord('quarter', 'F5'), make_chord('quarter', 'E5'), make_chord('quarter', 'D5'), make_chord('quarter', 'C5')]
    pno_r_dict[15] = [make_chord('half', 'B4'), make_chord('quarter', 'D5'), make_chord('quarter', 'G5')]
    pno_r_dict[16] = [make_chord('quarter', 'A5'), make_chord('quarter', 'C6'), make_chord('quarter', 'D6', dots=1), make_chord('eighth', 'C6')]
    pno_r_dict[17] = [make_chord('quarter', 'B5'), make_chord('quarter', 'A5'), make_chord('quarter', 'G5'), make_chord('quarter', 'A5')]
    pno_r_dict[18] = [make_chord('quarter', 'F5'), make_chord('quarter', 'A5'), make_chord('quarter', 'D6', dots=1), make_chord('eighth', 'C6')]
    pno_r_dict[19] = [make_chord('quarter', 'B5'), make_chord('quarter', 'G5'), make_chord('half', 'A5')]

    # Piano RH M21-28: Supporting counter-melody and arpeggio dialogue beneath Violin lead
    dyn_pno[20] = 'mf'
    pno_r_dict[20] = [make_chord('quarter', 'F4'), make_chord('quarter', 'A4'), make_chord('quarter', 'D5'), make_chord('quarter', 'F5')]
    pno_r_dict[21] = [make_chord('quarter', 'D4'), make_chord('quarter', 'G4'), make_chord('quarter', 'B4'), make_chord('quarter', 'D5')]
    pno_r_dict[22] = [make_chord('quarter', 'C4'), make_chord('quarter', 'E4'), make_chord('quarter', 'A4'), make_chord('quarter', 'C5')]
    pno_r_dict[23] = [make_chord('quarter', 'B3'), make_chord('quarter', 'E4'), make_chord('quarter', 'G4'), make_chord('quarter', 'B4')]
    pno_r_dict[24] = [make_chord('quarter', 'C5'), make_chord('eighth', 'D5'), make_chord('eighth', 'E5'), make_chord('quarter', 'F5'), make_chord('quarter', 'E5')]
    pno_r_dict[25] = [make_chord('quarter', 'D5'), make_chord('quarter', 'B4'), make_chord('quarter', 'G4'), make_chord('quarter', 'B4')]
    pno_r_dict[26] = [make_chord('quarter', 'C5'), make_chord('quarter', 'A4'), make_chord('quarter', 'F4'), make_chord('quarter', 'A4')]
    pno_r_dict[27] = [make_chord('quarter', 'B4'), make_chord('quarter', 'D5'), make_chord('half', 'F5')]

    # Piano LH M13-28: Flowing Acoustic Bass Accompaniment (D Dorian Broken Chords)
    pno_l_dict[12] = [make_chord('quarter', 'D2'), make_chord('quarter', 'A2'), make_chord('quarter', ['F3', 'A3']), make_chord('quarter', 'A2')]
    pno_l_dict[13] = [make_chord('quarter', 'C2'), make_chord('quarter', 'G2'), make_chord('quarter', ['E3', 'G3']), make_chord('quarter', 'G2')]
    pno_l_dict[14] = [make_chord('quarter', 'D2'), make_chord('quarter', 'A2'), make_chord('quarter', 'F2'), make_chord('quarter', ['A2', 'C3'])]
    pno_l_dict[15] = [make_chord('quarter', 'G2'), make_chord('quarter', 'D3'), make_chord('quarter', ['B2', 'D3']), make_chord('quarter', 'A2')]
    pno_l_dict[16] = [make_chord('quarter', 'F2'), make_chord('quarter', 'C3'), make_chord('quarter', 'C2'), make_chord('quarter', ['G2', 'E3'])]
    pno_l_dict[17] = [make_chord('quarter', 'G2'), make_chord('quarter', 'D3'), make_chord('quarter', 'A2'), make_chord('quarter', ['E3', 'A3'])]
    pno_l_dict[18] = [make_chord('quarter', 'D2'), make_chord('quarter', 'A2'), make_chord('quarter', 'C2'), make_chord('quarter', ['G2', 'C3'])]
    pno_l_dict[19] = [make_chord('quarter', 'B2'), make_chord('quarter', 'D3'), make_chord('quarter', 'D2'), make_chord('quarter', ['A2', 'F3'])]
    pno_l_dict[20] = [make_chord('quarter', 'D2'), make_chord('quarter', 'A2'), make_chord('quarter', ['F3', 'A3']), make_chord('quarter', 'D3')]
    pno_l_dict[21] = [make_chord('quarter', 'G2'), make_chord('quarter', 'D3'), make_chord('quarter', ['B2', 'D3']), make_chord('quarter', 'G3')]
    pno_l_dict[22] = [make_chord('quarter', 'F2'), make_chord('quarter', 'C3'), make_chord('quarter', 'C2'), make_chord('quarter', ['E3', 'G3'])]
    pno_l_dict[23] = [make_chord('quarter', 'E2'), make_chord('quarter', 'B2'), make_chord('quarter', ['G3', 'B3']), make_chord('quarter', 'E3')]
    pno_l_dict[24] = [make_chord('quarter', 'A2'), make_chord('quarter', 'E3'), make_chord('quarter', ['C3', 'E3']), make_chord('quarter', 'E3')]
    pno_l_dict[25] = [make_chord('quarter', 'G2'), make_chord('quarter', 'D3'), make_chord('quarter', ['B2', 'D3']), make_chord('quarter', 'D3')]
    pno_l_dict[26] = [make_chord('quarter', 'F2'), make_chord('quarter', 'C3'), make_chord('quarter', ['A2', 'C3']), make_chord('quarter', 'C3')]
    pno_l_dict[27] = [make_chord('quarter', 'G2'), make_chord('quarter', 'D3'), make_chord('quarter', ['B2', 'F3']), make_chord('quarter', 'G3')]

    # === Part III: M29–46 (Probing Deep Rules: Radiant Theme B2, Mini-Climax, and Fade-Out) ===
    # Seamless resolution from M28 (G7) into M29 (C major)
    dyn_vln[28] = 'mf'
    dyn_pno[28] = 'mp'

    # Violin M29-32: Radiant Theme B2 entry (C major -> G major)
    vln_dict[28] = [make_chord('quarter', 'E5'), make_chord('quarter', 'G5'), make_chord('half', 'C6')]
    vln_dict[29] = [make_chord('quarter', 'D6'), make_chord('quarter', 'C6'), make_chord('half', 'E6')]
    vln_dict[30] = [make_chord('half', 'D6'), make_chord('half', 'B5')]
    vln_dict[31] = [make_chord('quarter', 'C6'), make_chord('quarter', 'D6'), make_chord('half', 'B5')]

    # Violin M33-34: Dialogue counter-line with Piano
    vln_dict[32] = [make_chord('half', 'E5', tenuto=True), make_chord('half', 'C5', tenuto=True)]
    vln_dict[33] = [make_chord('half', 'F5', tenuto=True), make_chord('half', 'D5', tenuto=True)]

    # Violin M35-38: Lyrical reflective sequence & modal deepening (Mixolydian/Dorian color)
    dyn_vln[34] = 'mp'
    vln_dict[34] = [make_chord('quarter', 'Bb5'), make_chord('eighth', 'C6'), make_chord('eighth', 'Bb5'), make_chord('half', 'G5')]
    vln_dict[35] = [make_chord('quarter', 'A5', dots=1), make_chord('eighth', 'Bb5'), make_chord('half', 'F5')]
    vln_dict[36] = [make_chord('quarter', 'F5'), make_chord('quarter', 'G5'), make_chord('half', 'D5')]
    vln_dict[37] = [make_chord('half', 'Eb5', tenuto=True), make_chord('half', 'C5', tenuto=True)]

    # Violin M39-42: THE MINI-CLIMAX! (小高潮! cresc. -> f -> apex on G6)
    dyn_vln[38] = 'mf'
    dyn_pno[38] = 'mf'
    vln_dict[38] = [make_chord('quarter', 'G5'), make_chord('quarter', 'Bb5'), make_chord('half', 'Eb6', accent=True)]
    dyn_vln[39] = 'f'
    dyn_pno[39] = 'f'
    vln_dict[39] = [make_chord('quarter', 'Ab5'), make_chord('quarter', 'C6'), make_chord('half', 'F6', accent=True)]
    dyn_vln[40] = 'ff'
    dyn_pno[40] = 'ff'
    vln_dict[40] = [make_chord('quarter', 'G6', accent=True), make_chord('quarter', 'F6'), make_chord('half', 'Eb6', tenuto=True)]
    dyn_vln[41] = 'f'
    dyn_pno[41] = 'f'
    vln_dict[41] = [make_chord('half', 'D6', accent=True), make_chord('half', 'B5', tenuto=True)]

    # Violin M43-46: FADE-OUT (淡出, calando, morendo, dim. -> pp -> silence)
    dyn_vln[42] = 'p'
    dyn_pno[42] = 'p'
    vln_dict[42] = [make_chord('half', 'C6', tenuto=True), make_chord('half', 'G5')]
    dyn_vln[43] = 'pp'
    dyn_pno[43] = 'pp'
    vln_dict[43] = [make_chord('half', 'Eb5'), make_chord('half', 'D5')]
    dyn_vln[44] = 'ppp'
    dyn_pno[44] = 'ppp'
    vln_dict[44] = [make_chord('half', 'C5'), make_chord('half', 'B4', tenuto=True)]
    vln_dict[45] = [make_chord('half', 'B4', tenuto=True), make_rest('half')]

    # Piano RH M29-32: Flowing, glittering accompaniment supporting Violin's luminous B2 melody
    pno_r_dict[28] = [make_chord('eighth', p) for p in ['C4', 'E4', 'G4', 'C5', 'E5', 'C5', 'G4', 'E4']]
    pno_r_dict[29] = [make_chord('eighth', p) for p in ['C4', 'F4', 'A4', 'C5', 'F5', 'C5', 'A4', 'F4']]
    pno_r_dict[30] = [make_chord('eighth', p) for p in ['D4', 'G4', 'B4', 'D5', 'G5', 'D5', 'B4', 'G4']]
    pno_r_dict[31] = [make_chord('eighth', p) for p in ['D4', 'G4', 'B4', 'D5', 'F5', 'D5', 'B4', 'G4']]

    # Piano RH M33-34: Piano takes lead melody in dialogue
    pno_r_dict[32] = [make_chord('quarter', 'C6'), make_chord('quarter', 'E6'), make_chord('half', 'A5')]
    pno_r_dict[33] = [make_chord('quarter', 'D6'), make_chord('quarter', 'F6'), make_chord('half', 'D6')]

    # Piano RH M35-38: Expressive modal arpeggios
    pno_r_dict[34] = [make_chord('eighth', p) for p in ['D4', 'G4', 'Bb4', 'D5', 'G5', 'D5', 'Bb4', 'G4']]
    pno_r_dict[35] = [make_chord('eighth', p) for p in ['C4', 'F4', 'A4', 'C5', 'F5', 'C5', 'A4', 'F4']]
    pno_r_dict[36] = [make_chord('eighth', p) for p in ['D4', 'F4', 'A4', 'D5', 'F5', 'D5', 'A4', 'F4']]
    pno_r_dict[37] = [make_chord('eighth', p) for p in ['C4', 'Eb4', 'G4', 'C5', 'Eb5', 'C5', 'G4', 'Eb4']]

    # Piano RH M39-42: Mini-Climax Piano accompaniment & grand chords
    pno_r_dict[38] = [make_chord('eighth', p) for p in ['Eb4', 'G4', 'Bb4', 'Eb5', 'G5', 'Eb5', 'Bb4', 'G4']]
    pno_r_dict[39] = [make_chord('eighth', p) for p in ['F4', 'Ab4', 'C5', 'F5', 'Ab5', 'F5', 'C5', 'Ab4']]
    pno_r_dict[40] = [make_chord('half', ['F5', 'Ab5', 'B5', 'D6'], accent=True), make_chord('half', ['Eb5', 'G5', 'C6'], accent=True)]
    pno_r_dict[41] = [make_chord('half', ['D5', 'F5', 'B5'], accent=True), make_chord('half', ['D5', 'G5', 'B5'], tenuto=True)]

    # Piano RH M43-46: Fade-out delicate chords and whisper into silence
    pno_r_dict[42] = [make_chord('half', ['C5', 'E5', 'G5']), make_chord('half', ['C5', 'E5'])]
    pno_r_dict[43] = [make_chord('half', ['C5', 'Eb5']), make_chord('half', ['B4', 'D5'])]
    pno_r_dict[44] = [make_chord('half', ['G4', 'C5']), make_chord('half', ['F#4', 'C5'])]
    pno_r_dict[45] = [make_chord('half', ['F4', 'G4', 'B4']), make_rest('half')]

    # Piano LH M29-46
    pno_l_dict[28] = [make_chord('quarter', 'C2'), make_chord('quarter', 'G2'), make_chord('quarter', ['E3', 'G3']), make_chord('quarter', 'G2')]
    pno_l_dict[29] = [make_chord('quarter', 'F2'), make_chord('quarter', 'C3'), make_chord('quarter', ['F3', 'A3']), make_chord('quarter', 'C3')]
    pno_l_dict[30] = [make_chord('quarter', 'G2'), make_chord('quarter', 'D3'), make_chord('quarter', ['G3', 'B3']), make_chord('quarter', 'D3')]
    pno_l_dict[31] = [make_chord('quarter', 'G2'), make_chord('quarter', 'D3'), make_chord('quarter', ['F3', 'B3']), make_chord('quarter', 'G2')]
    pno_l_dict[32] = [make_chord('quarter', 'A2'), make_chord('quarter', 'E3'), make_chord('quarter', ['C3', 'E3']), make_chord('quarter', 'E3')]
    pno_l_dict[33] = [make_chord('quarter', 'D2'), make_chord('quarter', 'A2'), make_chord('quarter', ['F3', 'A3']), make_chord('quarter', 'A2')]
    pno_l_dict[34] = [make_chord('quarter', 'G2'), make_chord('quarter', 'D3'), make_chord('quarter', ['Bb2', 'D3']), make_chord('quarter', 'D3')]
    pno_l_dict[35] = [make_chord('quarter', 'F2'), make_chord('quarter', 'C3'), make_chord('quarter', ['A2', 'C3']), make_chord('quarter', 'C3')]
    pno_l_dict[36] = [make_chord('quarter', 'D2'), make_chord('quarter', 'A2'), make_chord('quarter', ['F2', 'A2']), make_chord('quarter', 'A2')]
    pno_l_dict[37] = [make_chord('quarter', 'C2'), make_chord('quarter', 'G2'), make_chord('quarter', ['Eb3', 'G3']), make_chord('quarter', 'G2')]
    pno_l_dict[38] = [make_chord('half', ['Eb2', 'Eb3'], accent=True), make_chord('half', ['G2', 'Bb2'])]
    pno_l_dict[39] = [make_chord('half', ['F2', 'F3'], accent=True), make_chord('half', ['Ab2', 'C3'])]
    pno_l_dict[40] = [make_chord('whole', ['G1', 'G2'], accent=True)]
    pno_l_dict[41] = [make_chord('whole', ['G1', 'G2'], accent=True)]
    pno_l_dict[42] = [make_chord('whole', ['C2', 'G2'])]
    pno_l_dict[43] = [make_chord('whole', ['G1', 'D2'])]
    pno_l_dict[44] = [make_chord('whole', ['G1', 'G2'])]
    pno_l_dict[45] = [make_chord('half', ['G1', 'G2']), make_rest('half')]

    # === Part IV: M47–66 (C minor Development / Contrapuntal Depth) ===
    dyn_vln[46] = 'mp'
    vln_dict[46] = [make_chord('half', 'G4'), make_chord('half', 'C5')]
    vln_dict[47] = [make_chord('half', 'Eb5'), make_chord('half', 'D5')]
    vln_dict[48] = [make_chord('half', 'C5'), make_chord('half', 'B4')]
    vln_dict[49] = [make_chord('whole', 'C5')]
    vln_dict[50] = [make_chord('half', 'Ab4'), make_chord('quarter', 'G4'), make_chord('quarter', 'F4')]
    vln_dict[51] = [make_chord('half', 'Eb4'), make_chord('half', 'D4')]
    vln_dict[52] = [make_chord('half', 'C4'), make_chord('half', 'G4')]
    vln_dict[53] = [make_chord('whole', 'C5')]

    dyn_vln[54] = 'mf'
    vln_dict[54] = [make_chord('half', 'Eb5'), make_chord('half', 'G5')]
    vln_dict[55] = [make_chord('half', 'Ab5'), make_chord('half', 'G5')]
    vln_dict[56] = [make_chord('half', 'F5'), make_chord('half', 'Eb5')]
    vln_dict[57] = [make_chord('half', 'D5'), make_chord('half', 'G4')]
    vln_dict[58] = [make_chord('half', 'Bb4'), make_chord('half', 'Eb5')]
    vln_dict[59] = [make_chord('half', 'G5'), make_chord('half', 'F5')]
    vln_dict[60] = [make_chord('half', 'Eb5'), make_chord('half', 'D5')]
    vln_dict[61] = [make_chord('whole', 'C5')]
    vln_dict[62] = [make_chord('half', 'D5'), make_chord('quarter', 'Eb5'), make_chord('quarter', 'F5')]
    vln_dict[63] = [make_chord('half', 'G5'), make_chord('half', 'Ab5')]
    vln_dict[64] = [make_chord('half', 'F5'), make_chord('half', 'G5')]
    vln_dict[65] = [make_chord('whole', 'G5')]

    # Piano RH M47-66 Minor mode counterpoint
    dyn_pno[46] = 'mp'
    pno_r_dict[46] = [make_chord('eighth', p) for p in ['C4', 'Eb4', 'G4', 'C5', 'Eb4', 'G4', 'C5', 'Eb5']]
    pno_r_dict[47] = [make_chord('eighth', p) for p in ['C4', 'F4', 'Ab4', 'C5', 'F4', 'Ab4', 'C5', 'F5']]
    pno_r_dict[48] = [make_chord('eighth', p) for p in ['D4', 'F4', 'G4', 'B4', 'D4', 'F4', 'G4', 'B4']]
    pno_r_dict[49] = [make_chord('eighth', p) for p in ['C4', 'Eb4', 'G4', 'C5', 'Eb4', 'G4', 'C5', 'Eb5']]
    pno_r_dict[50] = [make_chord('eighth', p) for p in ['F3', 'C4', 'Eb4', 'Ab4', 'C4', 'Eb4', 'Ab4', 'C5']]
    pno_r_dict[51] = [make_chord('eighth', p) for p in ['G3', 'D4', 'F4', 'B4', 'D4', 'F4', 'B4', 'D5']]
    pno_r_dict[52] = [make_chord('eighth', p) for p in ['C4', 'Eb4', 'G4', 'C5', 'Eb4', 'G4', 'C5', 'Eb5']]
    pno_r_dict[53] = [make_chord('eighth', p) for p in ['C4', 'Eb4', 'G4', 'C5', 'G4', 'C5', 'Eb5', 'G5']]

    dyn_pno[54] = 'mf'
    pno_r_dict[54] = [make_chord('eighth', p) for p in ['C4', 'Eb4', 'G4', 'C5', 'Eb5', 'C5', 'G4', 'Eb4']]
    pno_r_dict[55] = [make_chord('eighth', p) for p in ['C4', 'F4', 'Ab4', 'C5', 'F5', 'C5', 'Ab4', 'F4']]
    pno_r_dict[56] = [make_chord('eighth', p) for p in ['Bb3', 'D4', 'F4', 'Bb4', 'D5', 'Bb4', 'F4', 'D4']]
    pno_r_dict[57] = [make_chord('eighth', p) for p in ['G3', 'B3', 'D4', 'G4', 'B4', 'G4', 'D4', 'B3']]
    pno_r_dict[58] = [make_chord('eighth', p) for p in ['Eb3', 'G3', 'Bb3', 'Eb4', 'G4', 'Eb4', 'Bb3', 'G3']]
    pno_r_dict[59] = [make_chord('eighth', p) for p in ['F3', 'Ab3', 'C4', 'F4', 'Ab4', 'F4', 'C4', 'Ab3']]
    pno_r_dict[60] = [make_chord('eighth', p) for p in ['G3', 'C4', 'Eb4', 'G4', 'C5', 'G4', 'Eb4', 'C4']]
    pno_r_dict[61] = [make_chord('eighth', p) for p in ['G3', 'B3', 'D4', 'G4', 'B4', 'G4', 'D4', 'B3']]
    pno_r_dict[62] = [make_chord('eighth', p) for p in ['Ab3', 'C4', 'Eb4', 'Ab4', 'C5', 'Ab4', 'Eb4', 'C4']]
    pno_r_dict[63] = [make_chord('eighth', p) for p in ['F3', 'A3', 'C4', 'F4', 'A4', 'F4', 'C4', 'A3']]
    pno_r_dict[64] = [make_chord('eighth', p) for p in ['G3', 'B3', 'D4', 'G4', 'B4', 'G4', 'D4', 'G4']]
    pno_r_dict[65] = [make_chord('eighth', p) for p in ['G3', 'B3', 'D4', 'F4', 'G4', 'B4', 'D5', 'F5']]

    pno_l_dict[46] = [make_chord('whole', ['C2', 'G2'])]
    pno_l_dict[47] = [make_chord('whole', ['F2', 'C3'])]
    pno_l_dict[48] = [make_chord('whole', ['G2', 'D3'])]
    pno_l_dict[49] = [make_chord('whole', ['C2', 'G2'])]
    pno_l_dict[50] = [make_chord('whole', ['Ab1', 'Eb2'])]
    pno_l_dict[51] = [make_chord('whole', ['G1', 'D2'])]
    pno_l_dict[52] = [make_chord('whole', ['C2', 'G2'])]
    pno_l_dict[53] = [make_chord('whole', ['C2', 'G2', 'C3'])]
    pno_l_dict[54] = [make_chord('whole', ['C2', 'G2'])]
    pno_l_dict[55] = [make_chord('whole', ['F2', 'C3'])]
    pno_l_dict[56] = [make_chord('whole', ['Bb1', 'F2'])]
    pno_l_dict[57] = [make_chord('whole', ['G1', 'D2'])]
    pno_l_dict[58] = [make_chord('whole', ['Eb2', 'Bb2'])]
    pno_l_dict[59] = [make_chord('whole', ['F2', 'C3'])]
    pno_l_dict[60] = [make_chord('whole', ['C2', 'G2'])]
    pno_l_dict[61] = [make_chord('whole', ['G1', 'D2'])]
    pno_l_dict[62] = [make_chord('whole', ['Ab1', 'Eb2'])]
    pno_l_dict[63] = [make_chord('whole', ['A1', 'F2'])]
    pno_l_dict[64] = [make_chord('whole', ['G1', 'D2'])]
    pno_l_dict[65] = [make_chord('whole', ['G1', 'D2', 'G2'])]

    # === Part IV-B/C/D: M67–86 (Crisis & Dominant Preparation) ===
    # M67-74: Violin tenuto quarter notes anchor, Piano leggiero 16th motor
    dyn_vln[66] = 'mf'
    vln_dict[66] = [make_chord('quarter', 'C4', tenuto=True), make_chord('quarter', 'G4', tenuto=True), make_chord('quarter', 'Eb4', tenuto=True), make_chord('quarter', 'C4', tenuto=True)]
    vln_dict[67] = [make_chord('quarter', 'D4', tenuto=True), make_chord('quarter', 'G4', tenuto=True), make_chord('quarter', 'F4', tenuto=True), make_chord('quarter', 'D4', tenuto=True)]
    vln_dict[68] = [make_chord('quarter', 'Eb4', tenuto=True), make_chord('quarter', 'Ab4', tenuto=True), make_chord('quarter', 'F4', tenuto=True), make_chord('quarter', 'Eb4', tenuto=True)]
    vln_dict[69] = [make_chord('quarter', 'D4', tenuto=True), make_chord('quarter', 'G4', tenuto=True), make_chord('quarter', 'B3', tenuto=True), make_chord('quarter', 'D4', tenuto=True)]
    vln_dict[70] = [make_chord('quarter', 'C4', tenuto=True), make_chord('quarter', 'G4', tenuto=True), make_chord('quarter', 'Eb4', tenuto=True), make_chord('quarter', 'C4', tenuto=True)]
    vln_dict[71] = [make_chord('quarter', 'Db4', tenuto=True), make_chord('quarter', 'F4', tenuto=True), make_chord('quarter', 'Ab4', tenuto=True), make_chord('quarter', 'F4', tenuto=True)]
    vln_dict[72] = [make_chord('quarter', 'D4', tenuto=True), make_chord('quarter', 'F4', tenuto=True), make_chord('quarter', 'Ab4', tenuto=True), make_chord('quarter', 'D4', tenuto=True)]
    vln_dict[73] = [make_chord('quarter', 'G4', tenuto=True), make_chord('quarter', 'B4', tenuto=True), make_chord('quarter', 'D5', tenuto=True), make_chord('quarter', 'G4', tenuto=True)]

    pno_r_notes_67_74 = {
        66: ['C4', 'Eb4', 'D4', 'C4', 'G3', 'C4', 'Eb4', 'G4', 'C4', 'D4', 'Eb4', 'F#4', 'G4', 'D4', 'Eb4', 'D4'],
        67: ['C4', 'G3', 'C4', 'Eb4', 'D4', 'G3', 'D4', 'F4', 'Eb4', 'C4', 'G4', 'Eb4', 'D4', 'Bb3', 'G3', 'Bb3'],
        68: ['C4', 'Ab3', 'F4', 'C4', 'D4', 'Ab3', 'F4', 'D4', 'B3', 'D4', 'F4', 'Ab4', 'G4', 'F4', 'D4', 'B3'],
        69: ['C4', 'G3', 'D4', 'G3', 'Eb4', 'G3', 'D4', 'G3', 'F4', 'G3', 'Eb4', 'G3', 'D4', 'G3', 'B3', 'D4'],
        70: ['C4', 'Eb4', 'G4', 'C5', 'D5', 'C5', 'Bb4', 'Ab4', 'G4', 'Eb4', 'C4', 'G3', 'C4', 'Eb4', 'G4', 'C5'],
        71: ['Db4', 'F4', 'Ab4', 'Db5', 'Db4', 'F4', 'Ab4', 'Db5', 'Db4', 'F4', 'Ab4', 'Db5', 'Db4', 'F4', 'Ab4', 'Db5'],
        72: ['Ab4', 'G4', 'F4', 'Eb4', 'D4', 'F4', 'Ab4', 'C5', 'Bb4', 'Ab4', 'G4', 'F4', 'Eb4', 'G4', 'Bb4', 'D5'],
        73: ['C5', 'B4', 'C5', 'D5', 'G4', 'F#4', 'G4', 'Ab4', 'D4', 'G3', 'B3', 'D4', 'G4', 'D4', 'B3', 'G3'],
    }
    for idx, notes in pno_r_notes_67_74.items():
        pno_r_dict[idx] = [make_chord('16th', pt) for pt in notes]
    dyn_pno[66] = 'mf'

    pno_l_dict[66] = [make_chord('whole', ['C2', 'G2'])]
    pno_l_dict[67] = [make_chord('whole', ['G1', 'D2'])]
    pno_l_dict[68] = [make_chord('whole', ['F1', 'C2'])]
    pno_l_dict[69] = [make_chord('whole', ['G1', 'D2'])]
    pno_l_dict[70] = [make_chord('whole', ['C2', 'G2'])]
    pno_l_dict[71] = [make_chord('whole', ['Db2', 'Ab2'])]
    pno_l_dict[72] = [make_chord('whole', ['D2', 'Ab2'])]
    pno_l_dict[73] = [make_chord('whole', ['G1', 'G2'])]

    # M75-82: Storm gathering, Violin Tremolo (dyn 'f')
    dyn_vln[74] = 'f'
    vln_dict[74] = [make_chord('half', 'F4', tremolo=True), make_chord('half', 'Ab4', tremolo=True)]
    vln_dict[75] = [make_chord('half', 'G4', tremolo=True), make_chord('half', 'Bb4', tremolo=True)]
    vln_dict[76] = [make_chord('half', 'Ab4', tremolo=True), make_chord('half', 'C5', tremolo=True)]
    vln_dict[77] = [make_chord('half', 'B4', tremolo=True), make_chord('half', 'D5', tremolo=True)]
    vln_dict[78] = [make_chord('half', 'C5', tremolo=True), make_chord('half', 'Eb5', tremolo=True)]
    vln_dict[79] = [make_chord('half', 'D5', tremolo=True), make_chord('half', 'F5', tremolo=True)]
    vln_dict[80] = [make_chord('half', 'Eb5', tremolo=True), make_chord('half', 'G5', tremolo=True)]
    vln_dict[81] = [make_chord('half', 'F#5', tremolo=True), make_chord('half', 'A5', tremolo=True)]

    dyn_pno[74] = 'f'
    for idx in range(74, 82):
        pno_r_dict[idx] = [make_chord('quarter', ['G4', 'B4', 'D5', 'F5'], accent=True),
                           make_chord('quarter', ['G4', 'B4', 'D5', 'F5']),
                           make_chord('half', ['G4', 'B4', 'D5', 'F5'])]
    pno_l_dict[74] = [make_chord('whole', ['F1', 'F2'])]
    pno_l_dict[75] = [make_chord('whole', ['G1', 'G2'])]
    pno_l_dict[76] = [make_chord('whole', ['Ab1', 'Ab2'])]
    pno_l_dict[77] = [make_chord('whole', ['B1', 'B2'])]
    pno_l_dict[78] = [make_chord('whole', ['C2', 'C3'])]
    pno_l_dict[79] = [make_chord('whole', ['D2', 'D3'])]
    pno_l_dict[80] = [make_chord('whole', ['Eb2', 'Eb3'])]
    pno_l_dict[81] = [make_chord('whole', ['D2', 'D3'])]

    # M83-86: Dominant Preparation on G, M86 General Pause
    dyn_vln[82] = 'ff'
    vln_dict[82] = [make_chord('half', 'G4', accent=True), make_chord('half', 'B4', accent=True)]
    vln_dict[83] = [make_chord('half', 'D5', accent=True), make_chord('half', 'F5', accent=True)]
    vln_dict[84] = [make_chord('half', 'G5', accent=True), make_chord('half', 'B5', accent=True)]
    # M86: D6 half, G5 quarter, quarter REST (General Pause on beat 4!)
    vln_dict[85] = [make_chord('half', 'D6', accent=True), make_chord('quarter', 'G5', accent=True), make_rest('quarter')]

    dyn_pno[82] = 'ff'
    pno_r_dict[82] = [make_chord('16th', pt) for pt in ['G4', 'B4', 'D5', 'F5', 'G5', 'B5', 'D6', 'F6', 'D6', 'B5', 'G5', 'F5', 'D5', 'B4', 'G4', 'F4']]
    pno_r_dict[83] = [make_chord('16th', pt) for pt in ['Ab4', 'B4', 'D5', 'F5', 'Ab5', 'B5', 'D6', 'F6', 'D6', 'B5', 'Ab5', 'F5', 'D5', 'B4', 'Ab4', 'F4']]
    pno_r_dict[84] = [make_chord('16th', pt) for pt in ['G4', 'B4', 'D5', 'F5', 'G5', 'B5', 'D6', 'F6', 'G6', 'F6', 'D6', 'B5', 'G5', 'F5', 'D5', 'B4']]
    # M86: half chord, quarter chord, quarter rest!
    pno_r_dict[85] = [make_chord('half', ['G5', 'B5', 'D6', 'F6'], accent=True),
                      make_chord('quarter', ['G4', 'B4', 'D5', 'F5'], accent=True),
                      make_rest('quarter')]

    pno_l_dict[82] = [make_chord('whole', ['G1', 'G2'], accent=True)]
    pno_l_dict[83] = [make_chord('whole', ['G1', 'G2'], accent=True)]
    pno_l_dict[84] = [make_chord('whole', ['G1', 'G2'], accent=True)]
    pno_l_dict[85] = [make_chord('half', ['G1', 'G2'], accent=True),
                      make_chord('quarter', ['G1', 'G2'], accent=True),
                      make_rest('quarter')]

    # === Part V & VI: M87–108 (Fourth Round Consensus: Familiar Melody A & B Embedded in Unresolved Harmony) ===
    dyn_vln[86] = 'ff'
    dyn_pno[86] = 'ff'

    # M87 (Shot 24: Anchor 1 - Theme A minor antecedent begins: 3 - 1 - 3)
    vln_dict[86] = [make_chord('half', 'Eb5', accent=True), make_chord('quarter', 'C5'), make_chord('quarter', 'Eb5')]
    pno_l_dict[86] = [make_chord('whole', ['G1', 'G2'], accent=True)]
    pno_r_dict[86] = [make_chord('16th', pt) for pt in ['G3', 'C4', 'Eb4', 'G4', 'C5', 'Eb5', 'G5', 'C6', 'G5', 'Eb5', 'C5', 'G4', 'Eb4', 'C4', 'G3', 'Eb3']]

    # M88 (Shot 24: First ring spreading - Theme A: 4 - 5)
    vln_dict[87] = [make_chord('half', 'F5'), make_chord('half', 'G5')]
    pno_l_dict[87] = [make_chord('half', ['G1', 'G2']), make_chord('half', ['G1', 'G2'])]
    pno_r_dict[87] = [make_chord('16th', pt) for pt in ['C5', 'Ab4', 'F4', 'D4', 'C4', 'Ab3', 'F3', 'D3', 'F3', 'Ab3', 'C4', 'D4', 'F4', 'Ab4', 'C5', 'D5']]

    # M89 (Shot 24: Anchor 2 - Theme A arch peak: 1' - 5 - 3)
    vln_dict[88] = [make_chord('quarter', 'C6', accent=True), make_chord('quarter', 'G5'), make_chord('half', 'Eb5')]
    pno_l_dict[88] = [make_chord('whole', ['G1', 'G2'], accent=True)]
    pno_r_dict[88] = [make_chord('quarter', ['F4', 'Ab4', 'C5', 'D5'], accent=True), make_chord('quarter', ['F4', 'Ab4', 'C5', 'D5']), make_chord('half', ['F4', 'Ab4', 'C5', 'D5'])]

    # M90 (Shot 24: Second ring resonance - Theme A: 4 - 2 - 2)
    vln_dict[89] = [make_chord('quarter', 'F5'), make_chord('quarter', 'D5'), make_chord('half', 'D5', tenuto=True)]
    pno_l_dict[89] = [make_chord('whole', ['G1', 'G2'], accent=True)]
    pno_r_dict[89] = [make_chord('half', ['F4', 'B4', 'D5']), make_chord('half', ['F4', 'Ab4', 'B4', 'D5'])]

    # M91 (Shot 24: Anchor 3 - Theme A second phrase: 3 - 5 - 1')
    vln_dict[90] = [make_chord('half', 'Eb5'), make_chord('quarter', 'G5'), make_chord('quarter', 'C6')]
    pno_l_dict[90] = [make_chord('whole', ['G1', 'G2'], accent=True)]
    pno_r_dict[90] = [make_chord('16th', pt) for pt in ['Eb4', 'G4', 'C5', 'Eb5', 'G5', 'Eb5', 'C5', 'G4', 'Eb4', 'G4', 'C5', 'Eb5', 'G5', 'Eb5', 'C5', 'G4']]

    # M92 (Shot 24: Third ring overload - Theme A passionate cascade: 2' - 1' - 7 - 6)
    vln_dict[91] = [make_chord('quarter', 'D6'), make_chord('quarter', 'C6'), make_chord('quarter', 'B5'), make_chord('quarter', 'Ab5')]
    pno_l_dict[91] = [make_chord('half', ['G1', 'G2'], accent=True), make_chord('half', ['G1', 'G2'], accent=True)]
    pno_r_dict[91] = [make_chord('16th', pt) for pt in ['B4', 'Eb5', 'F5', 'Ab5', 'B5', 'Ab5', 'F5', 'Eb5', 'B4', 'Eb5', 'F5', 'Ab5', 'B5', 'Ab5', 'F5', 'Eb5']]

    # M93 (Shot 24: Anchor 4 - Theme A return: 5 - 4 - 2)
    vln_dict[92] = [make_chord('half', 'G5'), make_chord('quarter', 'F5'), make_chord('quarter', 'D5')]
    pno_l_dict[92] = [make_chord('whole', ['G1', 'G2'])]
    pno_r_dict[92] = [make_chord('16th', pt) for pt in ['Ab5', 'F5', 'C5', 'Ab4', 'F4', 'D4', 'B3', 'G3', 'F3', 'G3', 'B3', 'D4', 'F4', 'Ab4', 'C5', 'D5']]

    # M94 (Shot 24: Array boundary locked - Theme A half-cadence: 3 - 2 suspended on dominant!)
    vln_dict[93] = [make_chord('half', 'Eb5'), make_chord('half', 'D5', tenuto=True)]
    pno_l_dict[93] = [make_chord('whole', ['G1', 'G2'], accent=True)]
    pno_r_dict[93] = [make_chord('half', ['G4', 'B4', 'D5', 'F5'], accent=True), make_chord('half', ['G4', 'B4', 'D5', 'F5'], accent=True)]

    # M95 (Shot 24: Bridge surge: 3 - 5)
    vln_dict[94] = [make_chord('half', 'Eb5'), make_chord('half', 'G5')]
    pno_l_dict[94] = [make_chord('whole', ['G1', 'G2'], accent=True)]
    pno_r_dict[94] = [make_chord('16th', pt) for pt in ['C4', 'F4', 'Ab4', 'C5', 'D5', 'F5', 'Ab5', 'C6', 'Ab5', 'F5', 'D5', 'C5', 'Ab4', 'F4', 'D4', 'C4']]

    # M96 (Shot 24: Bridge to confrontation: 1' - 2')
    vln_dict[95] = [make_chord('half', 'C6'), make_chord('half', 'D6', accent=True)]
    pno_l_dict[95] = [make_chord('quarter', ['G1', 'G2'], accent=True), make_chord('quarter', ['G1', 'G2']), make_chord('half', ['G1', 'G2'])]
    pno_r_dict[95] = [make_chord('half', ['B4', 'F5', 'Ab5']), make_chord('half', ['B4', 'F5', 'Ab5'])]

    # M97 (Shot 25: Devil's red line - Theme B singing entry: 3 - 1)
    vln_dict[96] = [make_chord('half', 'Eb5'), make_chord('half', 'C5')]
    pno_l_dict[96] = [make_chord('whole', ['G1', 'G2'])]
    pno_r_dict[96] = [make_chord('16th', pt) for pt in ['Ab4', 'C5', 'E5', 'F5', 'Ab5', 'F5', 'E5', 'C5', 'Ab4', 'C5', 'E5', 'F5', 'Ab5', 'F5', 'E5', 'C5']]

    # M98 (Shot 25: Red line spreads - Theme B answer: 3 - 4 - 3)
    vln_dict[97] = [make_chord('quarter', 'Eb5'), make_chord('quarter', 'F5'), make_chord('half', 'Eb5')]
    pno_l_dict[97] = [make_chord('half', ['G1', 'G2'], accent=True), make_chord('half', ['G1', 'G2'], accent=True)]
    pno_r_dict[97] = [make_chord('half', ['F4', 'Ab4', 'B4', 'Eb5'], accent=True), make_chord('half', ['F4', 'Ab4', 'B4', 'Eb5'], accent=True)]

    # M99 (Shot 25: Red & gold lines close in - Theme B rise: 5 - 3)
    vln_dict[98] = [make_chord('half', 'G5'), make_chord('half', 'Eb5')]
    pno_l_dict[98] = [make_chord('whole', ['Ab1', 'Ab2'], accent=True)]
    pno_r_dict[98] = [make_chord('half', ['C5', 'F5', 'Ab5', 'C6'], accent=True), make_chord('half', ['C5', 'F5', 'Ab5', 'C6'], accent=True)]

    # M100 (Shot 25: Convergence eve - Theme B expansion: 6 - 7 - 5)
    vln_dict[99] = [make_chord('quarter', 'Ab5'), make_chord('quarter', 'Bb5'), make_chord('half', 'G5')]
    pno_l_dict[99] = [make_chord('whole', ['Ab1', 'Ab2'], accent=True)]
    pno_r_dict[99] = [make_chord('half', ['C4', 'F#4', 'C5'], accent=True), make_chord('half', ['C4', 'F#4', 'C5'], accent=True)]

    # M101 (Shot 25: Counter-circuits engage - Theme B soaring peak: 1' - 6 over It+6)
    vln_dict[100] = [make_chord('half', 'C6', accent=True), make_chord('half', 'Ab5')]
    pno_l_dict[100] = [make_chord('whole', ['G1', 'G2'], accent=True)]
    pno_r_dict[100] = [make_chord('half', ['B3', 'D4', 'G4', 'B4'], accent=True), make_chord('half', ['B3', 'D4', 'G4', 'B4'], accent=True)]

    # M102 (Shot 25: Clash of will - Theme B core tension: 7 - 1' - 7)
    vln_dict[101] = [make_chord('quarter', 'B5'), make_chord('quarter', 'C6'), make_chord('half', 'B5', accent=True)]
    pno_l_dict[101] = [make_chord('half', ['G1', 'G2'], accent=True), make_chord('half', ['G1', 'G2'], accent=True)]
    pno_r_dict[101] = [make_chord('half', ['G4', 'C5', 'F5', 'Ab5']), make_chord('half', ['G4', 'B4', 'F5', 'Ab5'], accent=True)]

    # M103 (Shot 25: Flash of impact - Theme B highest apex: 5 - 3')
    vln_dict[102] = [make_chord('half', 'G5'), make_chord('half', 'Eb6', accent=True)]
    pno_l_dict[102] = [make_chord('whole', ['G1', 'G2'], accent=True)]
    pno_r_dict[102] = [make_chord('16th', pt) for pt in ['G4', 'B4', 'D5', 'F5', 'Ab5', 'B5', 'D6', 'F6', 'D6', 'B5', 'Ab5', 'F5', 'D5', 'B4', 'G4', 'F4']]

    # M104 (Shot 25: 3-line knot locked - Theme B resolute finish: 2' - 1' - 7)
    vln_dict[103] = [make_chord('quarter', 'D6'), make_chord('quarter', 'C6'), make_chord('half', 'B5', tenuto=True)]
    pno_l_dict[103] = [make_chord('half', ['G1', 'G2'], accent=True), make_chord('half', ['G1', 'G2'], accent=True)]
    pno_r_dict[103] = [make_chord('quarter', ['B4', 'F5', 'Ab5', 'D6'], accent=True), make_chord('quarter', ['B4', 'F5', 'Ab5', 'D6']), make_chord('half', ['B4', 'F5', 'Ab5', 'D6'], accent=True)]

    # M105 (Shot 25->26: Beat 1 collision impact crash, then abrupt cut to black)
    vln_dict[104] = [make_chord('quarter', 'C6', accent=True), make_chord('quarter', 'B5', accent=True), make_rest('half')]
    pno_l_dict[104] = [make_chord('quarter', ['G0', 'G1'], accent=True), make_rest('quarter'), make_rest('half')]
    pno_r_dict[104] = [make_chord('quarter', ['Ab4', 'B4', 'Eb5', 'F5', 'B5'], accent=True), make_rest('quarter'), make_rest('half')]

    # M106 (Shot 26: Dark stone pedestal & remnant gold double-ring crest, p)
    dyn_vln[105] = 'p'
    dyn_pno[105] = 'p'
    vln_dict[105] = [make_chord('whole', 'B5', tenuto=True)]
    pno_l_dict[105] = [make_chord('whole', ['G1', 'G2'])]
    pno_r_dict[105] = [make_chord('half', ['F5', 'Ab5', 'B5']), make_chord('half', ['F5', 'Ab5', 'B5'])]

    # M107 (Shot 26: Title THE UNFALLEN DUET appears in center, pp)
    dyn_vln[106] = 'pp'
    dyn_pno[106] = 'pp'
    vln_dict[106] = [make_chord('whole', 'B5', tenuto=True)]
    pno_l_dict[106] = [make_chord('whole', ['G0', 'G1'])]
    pno_r_dict[106] = [make_chord('half', ['B3', 'D4', 'F4', 'Ab4', 'B4']), make_chord('half', ['B3', 'D4', 'F4', 'Ab4', 'B4'])]

    # M108 (Shot 26: Cliffhanger - beats 1-2 unresolved G7(b9) on leading tone B5, beats 3-4 General Pause)
    vln_dict[107] = [make_chord('half', 'B5', accent=True), make_rest('half')]
    pno_l_dict[107] = [make_chord('half', ['G0', 'G1'], accent=True), make_rest('half')]
    pno_r_dict[107] = [make_chord('half', ['B3', 'D4', 'F4', 'Ab4', 'B4'], accent=True), make_rest('half')]

    # Apply all elements across all 108 measures
    for idx in range(total_measures):
        set_measure_elements(m_vln[idx], vln_dict[idx], dyn_vln.get(idx, None))
        set_measure_elements(m_pno_r[idx], pno_r_dict[idx], dyn_pno.get(idx, None))
        set_measure_elements(m_pno_l[idx], pno_l_dict[idx], None)

    # Save to new file: The Unfallen Duet Reconstructed.mscz
    out_xml = ET.tostring(tree, encoding='utf-8', xml_declaration=True)
    tmp_out = out_mscz + '.tmp'
    with zipfile.ZipFile(tmp_out, 'w', compression=zipfile.ZIP_DEFLATED) as z:
        z.writestr(mscx_name, out_xml)
        for name, data in other_files.items():
            z.writestr(name, data)
    os.replace(tmp_out, out_mscz)
    print(f"Successfully generated new score file: {out_mscz}")

    # Export MIDI and MP3 via MuseScore 4
    musescore_exe = r'C:\Program Files\MuseScore 4\bin\MuseScore4.exe'
    if os.path.exists(musescore_exe):
        import time
        out_mid = 'music/The Unfallen Duet.mid'
        out_mp3 = 'music/The Unfallen Duet.mp3'
        print(f"Exporting reconstructed MIDI to {out_mid}...")
        subprocess.run([musescore_exe, '-o', os.path.abspath(out_mid), os.path.abspath(out_mscz)], check=True)
        time.sleep(1)
        print(f"Exporting reconstructed MP3 to {out_mp3}...")
        subprocess.run([musescore_exe, '-o', os.path.abspath(out_mp3), os.path.abspath(out_mscz)], check=True)
        print("Reconstructed score, MIDI and MP3 exports completed successfully.")

if __name__ == '__main__':
    generate_reconstructed_score()
