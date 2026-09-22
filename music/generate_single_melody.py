import os
import zipfile
import subprocess
import xml.etree.ElementTree as ET

# Pitch and TPC mappings
NOTE_TO_PITCH = {
    'C2': 36, 'D2': 38, 'Eb2': 39, 'E2': 40, 'F2': 41, 'F#2': 42, 'G2': 43, 'Ab2': 44, 'A2': 45, 'Bb2': 46, 'B2': 47,
    'C3': 48, 'D3': 50, 'Eb3': 51, 'E3': 52, 'F3': 53, 'F#3': 54, 'G3': 55, 'Ab3': 56, 'A3': 57, 'Bb3': 58, 'B3': 59,
    'C4': 60, 'Db4': 61, 'D4': 62, 'Eb4': 63, 'E4': 64, 'F4': 65, 'F#4': 66, 'G4': 67, 'Ab4': 68, 'A4': 69, 'Bb4': 70, 'B4': 71,
    'C5': 72, 'Db5': 73, 'D5': 74, 'Eb5': 75, 'E5': 76, 'F5': 77, 'F#5': 78, 'G5': 79, 'Ab5': 80, 'A5': 81, 'Bb5': 82, 'B5': 83,
    'C6': 84, 'Db6': 85, 'D6': 86, 'Eb6': 87, 'E6': 88, 'F6': 89, 'F#6': 90, 'G6': 91, 'Ab6': 92, 'A6': 93, 'Bb6': 94, 'B6': 95
}

NOTE_TO_TPC = {
    'C': 14, 'D': 16, 'E': 18, 'F': 13, 'G': 15, 'A': 17, 'B': 19,
    'Eb': 11, 'Ab': 10, 'Bb': 12, 'Db': 9, 'Gb': 8,
    'F#': 20, 'C#': 21, 'G#': 22, 'D#': 23, 'A#': 24
}

def p(name):
    return NOTE_TO_PITCH[name]

def get_tpc(name):
    clean = ''.join([c for c in name if not c.isdigit()])
    return NOTE_TO_TPC.get(clean, 14)

def make_note(pitch_val, tpc_val=None, is_flat=False, is_sharp=False):
    n = ET.Element('Note')
    p_el = ET.SubElement(n, 'pitch')
    p_el.text = str(pitch_val)
    if tpc_val is not None:
        t_el = ET.SubElement(n, 'tpc')
        t_el.text = str(tpc_val)
    if is_flat:
        acc = ET.SubElement(n, 'Accidental')
        s = ET.SubElement(acc, 'subtype')
        s.text = 'accidentalFlat'
    elif is_sharp:
        acc = ET.SubElement(n, 'Accidental')
        s = ET.SubElement(acc, 'subtype')
        s.text = 'accidentalSharp'
    return n

def make_chord(dur, pitch, dots=0, accent=False, tenuto=False, tremolo=False):
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

def generate_single_melody_score():
    base_mscz = 'music/The Unfallen Duet Base.mscz'
    out_mscz = 'music/The Unfallen Duet Melody.mscz'
    out_mid = 'music/The Unfallen Duet Melody.mid'
    out_mp3 = 'music/The Unfallen Duet Melody.mp3'

    with zipfile.ZipFile(base_mscz, 'r') as z:
        mscx_name = [n for n in z.namelist() if n.endswith('.mscx')][0]
        xml_content = z.read(mscx_name).decode('utf-8')
        other_files = {name: z.read(name) for name in z.namelist() if name != mscx_name}

    tree = ET.fromstring(xml_content)
    score = tree.find('Score') if tree.tag != 'Score' else tree

    # Keep only Staff 1 (Part 0: Solo Melody), remove Piano staves and part
    parts = score.findall('Part')
    for p_el in parts[1:]:
        score.remove(p_el)
    
    staves = score.findall('Staff')
    for s_el in staves[1:]:
        score.remove(s_el)

    part0 = parts[0]
    tr = part0.find('.//trackName')
    if tr is not None:
        tr.text = 'Main Melody'

    m_melody = staves[0].findall('Measure')
    total_measures = len(m_melody)

    melody_dict = {}
    dyn_dict = {}

    # === Part I: M1–12 (Theme A in C major, Piano solo in original) ===
    dyn_dict[0] = 'p'
    melody_dict[0] = [make_chord('half', 'E5'), make_chord('quarter', 'C5'), make_chord('quarter', 'E5')]
    melody_dict[1] = [make_chord('half', 'F5'), make_chord('half', 'G5')]
    melody_dict[2] = [make_chord('quarter', 'C6'), make_chord('quarter', 'G5'), make_chord('half', 'E5')]
    melody_dict[3] = [make_chord('quarter', 'F5'), make_chord('quarter', 'D5'), make_chord('half', 'D5')]
    dyn_dict[4] = 'mp'
    melody_dict[4] = [make_chord('half', 'E5'), make_chord('quarter', 'G5'), make_chord('quarter', 'C6')]
    melody_dict[5] = [make_chord('quarter', 'D6'), make_chord('quarter', 'C6'), make_chord('quarter', 'B5'), make_chord('quarter', 'A5')]
    melody_dict[6] = [make_chord('half', 'G5'), make_chord('quarter', 'F5'), make_chord('quarter', 'D5')]
    melody_dict[7] = [make_chord('half', 'E5'), make_chord('half', 'C5')]
    dyn_dict[8] = 'p'
    melody_dict[8] = [make_chord('quarter', 'B4'), make_chord('quarter', 'E5'), make_chord('half', 'G5')]
    melody_dict[9] = [make_chord('half', 'E5'), make_chord('half', 'C5')]
    melody_dict[10] = [make_chord('quarter', 'D5'), make_chord('quarter', 'F5'), make_chord('half', 'A4')]
    melody_dict[11] = [make_chord('half', 'G4'), make_chord('half', 'B4')]

    # === Part II: M13–24 (Theme B: Dorian Mode / Folk-Sacred Incantation - Occult Study with Mentor) ===
    dyn_dict[12] = 'mp'
    melody_dict[12] = [make_chord('quarter', 'D5'), make_chord('quarter', 'A5'), make_chord('half', 'G5')]
    melody_dict[13] = [make_chord('quarter', 'G5'), make_chord('half', 'E5'), make_chord('quarter', 'F5')]
    melody_dict[14] = [make_chord('quarter', 'F5'), make_chord('quarter', 'E5'), make_chord('quarter', 'D5'), make_chord('quarter', 'C5')]
    melody_dict[15] = [make_chord('half', 'B4'), make_chord('quarter', 'D5'), make_chord('quarter', 'G5')]
    melody_dict[16] = [make_chord('quarter', 'A5'), make_chord('quarter', 'C6'), make_chord('quarter', 'D6', dots=1), make_chord('eighth', 'C6')]
    melody_dict[17] = [make_chord('quarter', 'B5'), make_chord('quarter', 'A5'), make_chord('quarter', 'G5'), make_chord('quarter', 'A5')]
    melody_dict[18] = [make_chord('quarter', 'F5'), make_chord('quarter', 'A5'), make_chord('quarter', 'D6', dots=1), make_chord('eighth', 'C6')]
    melody_dict[19] = [make_chord('quarter', 'B5'), make_chord('quarter', 'G5'), make_chord('half', 'A5')]
    dyn_dict[20] = 'mf'
    melody_dict[20] = [make_chord('quarter', 'A5'), make_chord('quarter', 'C6'), make_chord('half', 'D6', accent=True)]
    melody_dict[21] = [make_chord('quarter', 'B5'), make_chord('quarter', 'B5'), make_chord('quarter', 'G5'), make_chord('quarter', 'G5')]
    melody_dict[22] = [make_chord('quarter', 'A5'), make_chord('quarter', 'C6'), make_chord('half', 'E6', accent=True)]
    melody_dict[23] = [make_chord('quarter', 'D6'), make_chord('quarter', 'B5'), make_chord('quarter', 'G5'), make_chord('quarter', 'E5')]

    # === M25–28: Melodic surge into Part III ===
    melody_dict[24] = [make_chord('half', 'A5', accent=True), make_chord('quarter', 'C6'), make_chord('quarter', 'D6')]
    melody_dict[25] = [make_chord('quarter', 'D6'), make_chord('half', 'C6'), make_chord('quarter', 'B5')]
    melody_dict[26] = [make_chord('quarter', 'B5'), make_chord('quarter', 'A5'), make_chord('quarter', 'G5'), make_chord('quarter', 'F5')]
    melody_dict[27] = [make_chord('quarter', 'F5'), make_chord('quarter', 'A5'), make_chord('half', 'G5')]

    # === Part III: M29–46 (Probing Deep Rules: Radiant Theme B2, Mini-Climax, and Fade-Out) ===
    dyn_dict[28] = 'mf'
    melody_dict[28] = [make_chord('quarter', 'E5'), make_chord('quarter', 'G5'), make_chord('half', 'C6')]
    melody_dict[29] = [make_chord('quarter', 'D6'), make_chord('quarter', 'C6'), make_chord('half', 'E6')]
    melody_dict[30] = [make_chord('half', 'D6'), make_chord('half', 'B5')]
    melody_dict[31] = [make_chord('quarter', 'C6'), make_chord('quarter', 'D6'), make_chord('half', 'B5')]

    # M33-34: Dialogue response
    melody_dict[32] = [make_chord('quarter', 'C6'), make_chord('quarter', 'E6'), make_chord('half', 'A5')]
    melody_dict[33] = [make_chord('quarter', 'D6'), make_chord('quarter', 'F6'), make_chord('half', 'D6')]

    # M35-38: Lyrical reflective sequence & modal deepening
    dyn_dict[34] = 'mp'
    melody_dict[34] = [make_chord('quarter', 'Bb5'), make_chord('eighth', 'C6'), make_chord('eighth', 'Bb5'), make_chord('half', 'G5')]
    melody_dict[35] = [make_chord('quarter', 'A5', dots=1), make_chord('eighth', 'Bb5'), make_chord('half', 'F5')]
    melody_dict[36] = [make_chord('quarter', 'F5'), make_chord('quarter', 'G5'), make_chord('half', 'D5')]
    melody_dict[37] = [make_chord('half', 'Eb5', tenuto=True), make_chord('half', 'C5', tenuto=True)]

    # M39-42: Mini-Climax (小高潮! cresc. -> f -> apex on G6)
    dyn_dict[38] = 'mf'
    melody_dict[38] = [make_chord('quarter', 'G5'), make_chord('quarter', 'Bb5'), make_chord('half', 'Eb6', accent=True)]
    dyn_dict[39] = 'f'
    melody_dict[39] = [make_chord('quarter', 'Ab5'), make_chord('quarter', 'C6'), make_chord('half', 'F6', accent=True)]
    dyn_dict[40] = 'ff'
    melody_dict[40] = [make_chord('quarter', 'G6', accent=True), make_chord('quarter', 'F6'), make_chord('half', 'Eb6', tenuto=True)]
    dyn_dict[41] = 'f'
    melody_dict[41] = [make_chord('half', 'D6', accent=True), make_chord('half', 'B5', tenuto=True)]

    # M43-46: Fade-out (淡出, morendo, dim. -> pp -> silence)
    dyn_dict[42] = 'p'
    melody_dict[42] = [make_chord('half', 'C6', tenuto=True), make_chord('half', 'G5')]
    dyn_dict[43] = 'pp'
    melody_dict[43] = [make_chord('half', 'Eb5'), make_chord('half', 'D5')]
    dyn_dict[44] = 'ppp'
    melody_dict[44] = [make_chord('half', 'C5'), make_chord('half', 'B4', tenuto=True)]
    melody_dict[45] = [make_chord('half', 'B4', tenuto=True), make_rest('half')]

    # === Part IV: M47–66 (C minor Development / Contrapuntal Depth) ===
    dyn_dict[46] = 'mp'
    melody_dict[46] = [make_chord('half', 'G4'), make_chord('half', 'C5')]
    melody_dict[47] = [make_chord('half', 'Eb5'), make_chord('half', 'D5')]
    melody_dict[48] = [make_chord('half', 'C5'), make_chord('half', 'B4')]
    melody_dict[49] = [make_chord('whole', 'C5')]
    melody_dict[50] = [make_chord('half', 'Ab4'), make_chord('quarter', 'G4'), make_chord('quarter', 'F4')]
    melody_dict[51] = [make_chord('half', 'Eb4'), make_chord('half', 'D4')]
    melody_dict[52] = [make_chord('half', 'C4'), make_chord('half', 'G4')]
    melody_dict[53] = [make_chord('whole', 'C5')]

    dyn_dict[54] = 'mf'
    melody_dict[54] = [make_chord('half', 'Eb5'), make_chord('half', 'G5')]
    melody_dict[55] = [make_chord('half', 'Ab5'), make_chord('half', 'G5')]
    melody_dict[56] = [make_chord('half', 'F5'), make_chord('half', 'Eb5')]
    melody_dict[57] = [make_chord('half', 'D5'), make_chord('half', 'G4')]
    melody_dict[58] = [make_chord('half', 'Bb4'), make_chord('half', 'Eb5')]
    melody_dict[59] = [make_chord('half', 'G5'), make_chord('half', 'F5')]
    melody_dict[60] = [make_chord('half', 'Eb5'), make_chord('half', 'D5')]
    melody_dict[61] = [make_chord('whole', 'C5')]
    melody_dict[62] = [make_chord('half', 'D5'), make_chord('quarter', 'Eb5'), make_chord('quarter', 'F5')]
    melody_dict[63] = [make_chord('half', 'G5'), make_chord('half', 'Ab5')]
    melody_dict[64] = [make_chord('half', 'F5'), make_chord('half', 'G5')]
    melody_dict[65] = [make_chord('whole', 'G5')]

    # === Part IV-B/C/D: M67–86 (Crisis & Dominant Preparation) ===
    dyn_dict[66] = 'mf'
    melody_dict[66] = [make_chord('quarter', 'C4', tenuto=True), make_chord('quarter', 'G4', tenuto=True), make_chord('quarter', 'Eb4', tenuto=True), make_chord('quarter', 'C4', tenuto=True)]
    melody_dict[67] = [make_chord('quarter', 'D4', tenuto=True), make_chord('quarter', 'G4', tenuto=True), make_chord('quarter', 'F4', tenuto=True), make_chord('quarter', 'D4', tenuto=True)]
    melody_dict[68] = [make_chord('quarter', 'Eb4', tenuto=True), make_chord('quarter', 'Ab4', tenuto=True), make_chord('quarter', 'F4', tenuto=True), make_chord('quarter', 'Eb4', tenuto=True)]
    melody_dict[69] = [make_chord('quarter', 'D4', tenuto=True), make_chord('quarter', 'G4', tenuto=True), make_chord('quarter', 'B3', tenuto=True), make_chord('quarter', 'D4', tenuto=True)]
    melody_dict[70] = [make_chord('quarter', 'C4', tenuto=True), make_chord('quarter', 'G4', tenuto=True), make_chord('quarter', 'Eb4', tenuto=True), make_chord('quarter', 'C4', tenuto=True)]
    melody_dict[71] = [make_chord('quarter', 'Db4', tenuto=True), make_chord('quarter', 'F4', tenuto=True), make_chord('quarter', 'Ab4', tenuto=True), make_chord('quarter', 'F4', tenuto=True)]
    melody_dict[72] = [make_chord('quarter', 'D4', tenuto=True), make_chord('quarter', 'F4', tenuto=True), make_chord('quarter', 'Ab4', tenuto=True), make_chord('quarter', 'D4', tenuto=True)]
    melody_dict[73] = [make_chord('quarter', 'G4', tenuto=True), make_chord('quarter', 'B4', tenuto=True), make_chord('quarter', 'D5', tenuto=True), make_chord('quarter', 'G4', tenuto=True)]

    # M75-82: Storm gathering, ascending drive
    dyn_dict[74] = 'f'
    melody_dict[74] = [make_chord('half', 'F4', tremolo=True), make_chord('half', 'Ab4', tremolo=True)]
    melody_dict[75] = [make_chord('half', 'G4', tremolo=True), make_chord('half', 'Bb4', tremolo=True)]
    melody_dict[76] = [make_chord('half', 'Ab4', tremolo=True), make_chord('half', 'C5', tremolo=True)]
    melody_dict[77] = [make_chord('half', 'B4', tremolo=True), make_chord('half', 'D5', tremolo=True)]
    melody_dict[78] = [make_chord('half', 'C5', tremolo=True), make_chord('half', 'Eb5', tremolo=True)]
    melody_dict[79] = [make_chord('half', 'D5', tremolo=True), make_chord('half', 'F5', tremolo=True)]
    melody_dict[80] = [make_chord('half', 'Eb5', tremolo=True), make_chord('half', 'G5', tremolo=True)]
    melody_dict[81] = [make_chord('half', 'F#5', tremolo=True), make_chord('half', 'A5', tremolo=True)]

    # M83-86: Dominant Preparation on G, M86 General Pause
    dyn_dict[82] = 'ff'
    melody_dict[82] = [make_chord('half', 'G4', accent=True), make_chord('half', 'B4', accent=True)]
    melody_dict[83] = [make_chord('half', 'D5', accent=True), make_chord('half', 'F5', accent=True)]
    melody_dict[84] = [make_chord('half', 'G5', accent=True), make_chord('half', 'B5', accent=True)]
    melody_dict[85] = [make_chord('half', 'D6', accent=True), make_chord('quarter', 'G5', accent=True), make_rest('quarter')]

    # === Part V & VI: M87–108 (Fourth Round Consensus: Climax Showdown & Coda) ===
    dyn_dict[86] = 'ff'
    # M87: Theme A minor antecedent begins: 3 - 1 - 3
    melody_dict[86] = [make_chord('half', 'Eb5', accent=True), make_chord('quarter', 'C5'), make_chord('quarter', 'Eb5')]
    # M88: Theme A: 4 - 5
    melody_dict[87] = [make_chord('half', 'F5'), make_chord('half', 'G5')]
    # M89: Theme A arch peak: 1' - 5 - 3
    melody_dict[88] = [make_chord('quarter', 'C6', accent=True), make_chord('quarter', 'G5'), make_chord('half', 'Eb5')]
    # M90: Theme A: 4 - 2 - 2
    melody_dict[89] = [make_chord('quarter', 'F5'), make_chord('quarter', 'D5'), make_chord('half', 'D5', tenuto=True)]
    # M91: Theme A second phrase: 3 - 5 - 1'
    melody_dict[90] = [make_chord('half', 'Eb5'), make_chord('quarter', 'G5'), make_chord('quarter', 'C6')]
    # M92: Theme A passionate cascade: 2' - 1' - 7 - 6
    melody_dict[91] = [make_chord('quarter', 'D6'), make_chord('quarter', 'C6'), make_chord('quarter', 'B5'), make_chord('quarter', 'Ab5')]
    # M93: Theme A return: 5 - 4 - 2
    melody_dict[92] = [make_chord('half', 'G5'), make_chord('quarter', 'F5'), make_chord('quarter', 'D5')]
    # M94: Theme A half-cadence: 3 - 2
    melody_dict[93] = [make_chord('half', 'Eb5'), make_chord('half', 'D5', tenuto=True)]
    # M95: Bridge surge: 3 - 5
    melody_dict[94] = [make_chord('half', 'Eb5'), make_chord('half', 'G5')]
    # M96: Bridge to confrontation: 1' - 2'
    melody_dict[95] = [make_chord('half', 'C6'), make_chord('half', 'D6', accent=True)]
    # M97: Theme B singing entry: 3 - 1
    melody_dict[96] = [make_chord('half', 'Eb5'), make_chord('half', 'C5')]
    # M98: Theme B answer: 3 - 4 - 3
    melody_dict[97] = [make_chord('quarter', 'Eb5'), make_chord('quarter', 'F5'), make_chord('half', 'Eb5')]
    # M99: Theme B rise: 5 - 3
    melody_dict[98] = [make_chord('half', 'G5'), make_chord('half', 'Eb5')]
    # M100: Theme B expansion: 6 - 7 - 5
    melody_dict[99] = [make_chord('quarter', 'Ab5'), make_chord('quarter', 'Bb5'), make_chord('half', 'G5')]
    # M101: Theme B soaring peak: 1' - 6
    melody_dict[100] = [make_chord('half', 'C6', accent=True), make_chord('half', 'Ab5')]
    # M102: Theme B core tension: 7 - 1' - 7
    melody_dict[101] = [make_chord('quarter', 'B5'), make_chord('quarter', 'C6'), make_chord('half', 'B5', accent=True)]
    # M103: Theme B highest apex: 5 - 3'
    melody_dict[102] = [make_chord('half', 'G5'), make_chord('half', 'Eb6', accent=True)]
    # M104: Theme B resolute finish: 2' - 1' - 7
    melody_dict[103] = [make_chord('quarter', 'D6'), make_chord('quarter', 'C6'), make_chord('half', 'B5', tenuto=True)]
    # M105: Collision impact crash, then cut to rest
    melody_dict[104] = [make_chord('quarter', 'C6', accent=True), make_chord('quarter', 'B5', accent=True), make_rest('half')]
    # M106: Dark pedestal & remnant gold double-ring crest
    dyn_dict[105] = 'p'
    melody_dict[105] = [make_chord('whole', 'B5', tenuto=True)]
    # M107: Title THE UNFALLEN DUET appears
    dyn_dict[106] = 'pp'
    melody_dict[106] = [make_chord('whole', 'B5', tenuto=True)]
    # M108: Cliffhanger - leading tone B5, beats 3-4 General Pause
    melody_dict[107] = [make_chord('half', 'B5', accent=True), make_rest('half')]

    dur_map = {'whole': 4.0, 'half': 2.0, 'quarter': 1.0, 'eighth': 0.5, '16th': 0.25}
    for idx in range(total_measures):
        elements = melody_dict[idx]
        total_dur = 0.0
        for el in elements:
            d_type = el.find('durationType').text
            dots = el.find('dots')
            d = dur_map[d_type]
            if dots is not None and dots.text == '1':
                d *= 1.5
            total_dur += d
        assert abs(total_dur - 4.0) < 1e-4, f"Measure {idx+1} has invalid duration: {total_dur}"
        set_measure_elements(m_melody[idx], elements, dyn_dict.get(idx, None))

    out_xml = ET.tostring(tree, encoding='utf-8', xml_declaration=True)
    tmp_out = out_mscz + '.tmp'
    with zipfile.ZipFile(tmp_out, 'w', compression=zipfile.ZIP_DEFLATED) as z:
        z.writestr(mscx_name, out_xml)
        for name, data in other_files.items():
            z.writestr(name, data)
    os.replace(tmp_out, out_mscz)
    print(f"Successfully generated single melody score: {out_mscz}")

    musescore_exe = r'C:\Program Files\MuseScore 4\bin\MuseScore4.exe'
    if os.path.exists(musescore_exe):
        print(f"Exporting melody MIDI to {out_mid}...")
        subprocess.run([musescore_exe, '-o', os.path.abspath(out_mid), os.path.abspath(out_mscz)], check=True)
        print(f"Exporting melody MP3 to {out_mp3}...")
        subprocess.run([musescore_exe, '-o', os.path.abspath(out_mp3), os.path.abspath(out_mscz)], check=True)
        print("Melody score, MIDI, and MP3 generated successfully!")

if __name__ == '__main__':
    generate_single_melody_score()
