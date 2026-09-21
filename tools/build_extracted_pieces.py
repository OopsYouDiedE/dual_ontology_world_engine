import os
import subprocess
import copy
import xml.etree.ElementTree as ET
from xml.dom import minidom

# Load source score
SRC_PATH = 'music/The Unfallen Duet v3.mscx'
MUSESCORE_EXE = r'C:\Program Files\MuseScore 4\bin\MuseScore4.exe'

dur_map = {'whole': 1.0, 'half': 0.5, 'quarter': 0.25, 'eighth': 0.125, '16th': 0.0625, '32nd': 0.03125}

def clean_element(elem):
    """Remove eids and spanners that might refer across removed measures."""
    for child in list(elem):
        if child.tag == 'eid':
            elem.remove(child)
        elif child.tag == 'RehearsalMark':
            elem.remove(child)
        else:
            clean_element(child)

def remove_spanners_without_target(measure):
    """Remove Spanner elements that have broken links."""
    for ch in measure.iter('Chord'):
        for note in ch.iter('Note'):
            for sp in list(note):
                if sp.tag == 'Spanner':
                    # Keep tie only if simple
                    stype = sp.get('type')
                    if stype != 'Tie':
                        note.remove(sp)

def build_knowledge():
    """
    1. 《知识》（Knowledge）—— 阿拉伯曲调
    小提琴独奏 (Violin Solo)，源自 Part IV: 地下密室与法则探求 (Bars 49-68，共20小节)
    调号: c小调/和声小调/希贾兹 (concertKey -3)
    速度: 4/4 拍, BPM 120 (tempo 2.0)
    """
    print('Building 《知识》...')
    tree = ET.parse(SRC_PATH)
    root = tree.getroot()
    score = root.find('Score')

    # Update metadata
    for meta in score.findall('metaTag'):
        name = meta.get('name')
        if name == 'workTitle':
            meta.text = '知识'
        elif name == 'subtitle':
            meta.text = '游戏曲目 · 小提琴独奏'
        elif name == 'composer':
            meta.text = '学院主题 · 阿拉伯调式'
        elif name == 'source':
            meta.text = '摘自 The Unfallen Duet Part IV: 地下密室与法则探求 (Bars 49-68)'

    # Remove Part 2 (Piano)
    parts = score.findall('Part')
    for p in parts:
        if p.get('id') == '2':
            score.remove(p)

    # Remove Staff 2 and Staff 3
    for s in list(score):
        if s.tag == 'Staff' and s.get('id') in ('2', '3'):
            score.remove(s)

    # In Staff 1, slice measures 49 to 68 (index 48 to 67)
    staff1 = score.find('.//Staff[@id="1"]')
    all_measures = staff1.findall('Measure')
    target_measures = all_measures[48:68]

    # Remove all existing measures from staff1
    for m in all_measures:
        staff1.remove(m)

    # Clean and add target measures
    for idx, m in enumerate(target_measures):
        clean_element(m)
        remove_spanners_without_target(m)
        voice = m.find('voice')
        if idx == 0:
            # First measure needs KeySig (-3), TimeSig (4/4), Clef (G), Tempo (120)
            # Remove any existing TimeSig, KeySig, Tempo, Clef
            for tag in ['KeySig', 'TimeSig', 'Clef', 'Tempo']:
                el = voice.find(tag)
                if el is not None:
                    voice.remove(el)

            ks = ET.Element('KeySig')
            ET.SubElement(ks, 'concertKey').text = '-3'
            ts = ET.Element('TimeSig')
            ET.SubElement(ts, 'sigN').text = '4'
            ET.SubElement(ts, 'sigD').text = '4'
            clef = ET.Element('Clef')
            ET.SubElement(clef, 'concertClefType').text = 'G'
            ET.SubElement(clef, 'transposingClefType').text = 'G'
            tempo = ET.Element('Tempo')
            ET.SubElement(tempo, 'tempo').text = '2.0'
            ET.SubElement(tempo, 'followText').text = '1'
            ET.SubElement(tempo, 'text').text = 'Andante moderato ♩ = 120'

            voice.insert(0, tempo)
            voice.insert(0, clef)
            voice.insert(0, ts)
            voice.insert(0, ks)
        
        # Add layout break every 4 measures
        if (idx + 1) % 4 == 0 and idx + 1 < len(target_measures):
            lb = ET.SubElement(m, 'LayoutBreak')
            ET.SubElement(lb, 'subtype').text = 'line'

        staff1.append(m)

    # Set title VBox
    vbox = staff1.find('VBox')
    if vbox is not None:
        staff1.remove(vbox)
    vbox = ET.Element('VBox')
    ET.SubElement(vbox, 'height').text = '10'
    t1 = ET.SubElement(vbox, 'Text')
    ET.SubElement(t1, 'style').text = 'title'
    ET.SubElement(t1, 'text').text = '知识'
    t2 = ET.SubElement(vbox, 'Text')
    ET.SubElement(t2, 'style').text = 'subtitle'
    ET.SubElement(t2, 'text').text = '小提琴独奏 (Violin Solo) · 阿拉伯调式'
    staff1.insert(0, vbox)

    # Save to music/知识.mscx
    export_score(root, '知识')


def build_mystery():
    """
    2. 《神秘》（Mystery）—— 维多利亚曲调
    钢琴独奏 (Piano Solo)，源自 Part II: 恶魔授业与法则初显 (Bars 13-28，共16小节)
    调号: C大调/自然音阶 (concertKey 0)
    速度: 4/4 拍, BPM 144 (tempo 2.4)
    包含右手发条律动 (Staff 2) 与左手沉稳低音 (Staff 3)
    """
    print('Building 《神秘》...')
    tree = ET.parse(SRC_PATH)
    root = tree.getroot()
    score = root.find('Score')

    # Update metadata
    for meta in score.findall('metaTag'):
        name = meta.get('name')
        if name == 'workTitle':
            meta.text = '神秘'
        elif name == 'subtitle':
            meta.text = '游戏曲目 · 钢琴独奏'
        elif name == 'composer':
            meta.text = '学院主题 · 维多利亚调式'
        elif name == 'source':
            meta.text = '摘自 The Unfallen Duet Part II: 恶魔授业与法则初显 (Bars 13-28)'

    # Remove Part 1 (Violin)
    parts = score.findall('Part')
    for p in parts:
        if p.get('id') == '1':
            score.remove(p)

    # Update Part 2 id to 1 if needed, or keep Part 2 and Staff 1/2
    # In MuseScore, Piano has two staves. Let's make Piano the sole Part.
    piano_part = score.find('.//Part[@id="2"]')
    piano_part.set('id', '1')

    # Staffs in score: remove Staff id="1" (violin), rename Staff id="2" -> "1", Staff id="3" -> "2"
    staves = {s.get('id'): s for s in score.findall('Staff')}
    score.remove(staves['1']) # remove violin staff

    staff_rh = staves['2']
    staff_lh = staves['3']
    staff_rh.set('id', '1')
    staff_lh.set('id', '2')

    # Slice measures 13 to 28 (index 12 to 27)
    m_rh = staff_rh.findall('Measure')[12:28]
    m_lh = staff_lh.findall('Measure')[12:28]

    for m in staff_rh.findall('Measure'): staff_rh.remove(m)
    for m in staff_lh.findall('Measure'): staff_lh.remove(m)

    for idx, (mr, ml) in enumerate(zip(m_rh, m_lh)):
        clean_element(mr)
        clean_element(ml)
        remove_spanners_without_target(mr)
        remove_spanners_without_target(ml)

        vr = mr.find('voice')
        vl = ml.find('voice')

        if idx == 0:
            for tag in ['KeySig', 'TimeSig', 'Clef', 'Tempo']:
                el = vr.find(tag)
                if el is not None: vr.remove(el)
                el2 = vl.find(tag)
                if el2 is not None: vl.remove(el2)

            ks_r = ET.Element('KeySig'); ET.SubElement(ks_r, 'concertKey').text = '0'
            ts_r = ET.Element('TimeSig'); ET.SubElement(ts_r, 'sigN').text = '4'; ET.SubElement(ts_r, 'sigD').text = '4'
            clef_r = ET.Element('Clef'); ET.SubElement(clef_r, 'concertClefType').text = 'G'; ET.SubElement(clef_r, 'transposingClefType').text = 'G'
            tempo = ET.Element('Tempo'); ET.SubElement(tempo, 'tempo').text = '2.4'; ET.SubElement(tempo, 'followText').text = '1'
            ET.SubElement(tempo, 'text').text = 'Allegretto moderato ♩ = 144'

            vr.insert(0, tempo)
            vr.insert(0, clef_r)
            vr.insert(0, ts_r)
            vr.insert(0, ks_r)

            ks_l = ET.Element('KeySig'); ET.SubElement(ks_l, 'concertKey').text = '0'
            ts_l = ET.Element('TimeSig'); ET.SubElement(ts_l, 'sigN').text = '4'; ET.SubElement(ts_l, 'sigD').text = '4'
            clef_l = ET.Element('Clef'); ET.SubElement(clef_l, 'concertClefType').text = 'F'; ET.SubElement(clef_l, 'transposingClefType').text = 'F'
            vl.insert(0, clef_l)
            vl.insert(0, ts_l)
            vl.insert(0, ks_l)

        if (idx + 1) % 4 == 0 and idx + 1 < len(m_rh):
            lb = ET.SubElement(mr, 'LayoutBreak')
            ET.SubElement(lb, 'subtype').text = 'line'

        staff_rh.append(mr)
        staff_lh.append(ml)

    # Set title VBox
    vbox = staff_rh.find('VBox')
    if vbox is not None: staff_rh.remove(vbox)
    vbox = ET.Element('VBox')
    ET.SubElement(vbox, 'height').text = '10'
    t1 = ET.SubElement(vbox, 'Text')
    ET.SubElement(t1, 'style').text = 'title'
    ET.SubElement(t1, 'text').text = '神秘'
    t2 = ET.SubElement(vbox, 'Text')
    ET.SubElement(t2, 'style').text = 'subtitle'
    ET.SubElement(t2, 'text').text = '钢琴独奏 (Piano Solo) · 维多利亚调式'
    staff_rh.insert(0, vbox)

    export_score(root, '神秘')


def build_hero():
    """
    3. 《英雄》（Hero）—— 进行曲曲调
    钢琴独奏 (Piano Solo - 进行曲版)，源自 Part V: 终极冲突与主旋律大爆发 (Bars 89-106，共18小节)
    调号: C大调 (concertKey 0)
    速度: 4/4 拍, BPM 120 (tempo 2.0，坚毅有力进行曲)
    右手: 主题号角双音与行进切分 (原小提琴主导声部)
    左手: 强劲进行曲低音步伐与切分跳跃 (原大钢琴低音声部)
    """
    print('Building 《英雄》...')
    tree = ET.parse(SRC_PATH)
    root = tree.getroot()
    score = root.find('Score')

    # Update metadata
    for meta in score.findall('metaTag'):
        name = meta.get('name')
        if name == 'workTitle':
            meta.text = '英雄'
        elif name == 'subtitle':
            meta.text = '游戏曲目 · 钢琴进行曲'
        elif name == 'composer':
            meta.text = '学院主题 · 进行曲'
        elif name == 'source':
            meta.text = '摘自 The Unfallen Duet Part V: 终极冲突与主旋律大爆发 (Bars 89-106)'

    # Convert to single piano part with 2 staves
    parts = score.findall('Part')
    # Keep part 1 as piano
    piano_part = parts[0]
    piano_part.set('id', '1')
    p_inst = piano_part.find('Instrument')
    p_inst.set('id', 'piano')
    piano_part.find('trackName').text = 'Piano'
    p_inst.find('longName').text = 'Piano'
    p_inst.find('shortName').text = 'Pno.'
    p_inst.find('trackName').text = 'Piano'
    p_inst.find('instrumentId').text = 'keyboard.piano'
    p_inst.find('Channel/program').set('value', '0')

    # Add second staff descriptor to piano_part
    s2_desc = ET.Element('Staff')
    st_norm = ET.SubElement(s2_desc, 'StaffType', group='pitched')
    ET.SubElement(st_norm, 'name').text = 'stdNormal'
    ET.SubElement(s2_desc, 'defaultClef').text = 'F'
    piano_part.append(s2_desc)

    # Remove other part
    if len(parts) > 1:
        score.remove(parts[1])

    # In score, we have Staff 1 (Vln), Staff 2 (Pno RH), Staff 3 (Pno LH)
    staves = {s.get('id'): s for s in score.findall('Staff')}
    staff_vln = staves['1']
    staff_pno_lh = staves['3']
    score.remove(staves['2']) # remove 16th-note arpeggio staff

    staff_rh = staff_vln # becomes Piano RH
    staff_lh = staff_pno_lh # becomes Piano LH
    staff_rh.set('id', '1')
    staff_lh.set('id', '2')

    # Slice measures 89 to 106 (index 88 to 105)
    m_rh = staff_rh.findall('Measure')[88:106]
    m_lh = staff_lh.findall('Measure')[88:106]

    for m in staff_rh.findall('Measure'): staff_rh.remove(m)
    for m in staff_lh.findall('Measure'): staff_lh.remove(m)

    for idx, (mr, ml) in enumerate(zip(m_rh, m_lh)):
        clean_element(mr)
        clean_element(ml)
        remove_spanners_without_target(mr)
        remove_spanners_without_target(ml)

        vr = mr.find('voice')
        vl = ml.find('voice')

        if idx == 0:
            for tag in ['KeySig', 'TimeSig', 'Clef', 'Tempo']:
                el = vr.find(tag)
                if el is not None: vr.remove(el)
                el2 = vl.find(tag)
                if el2 is not None: vl.remove(el2)

            ks_r = ET.Element('KeySig'); ET.SubElement(ks_r, 'concertKey').text = '0'
            ts_r = ET.Element('TimeSig'); ET.SubElement(ts_r, 'sigN').text = '4'; ET.SubElement(ts_r, 'sigD').text = '4'
            clef_r = ET.Element('Clef'); ET.SubElement(clef_r, 'concertClefType').text = 'G'; ET.SubElement(clef_r, 'transposingClefType').text = 'G'
            tempo = ET.Element('Tempo'); ET.SubElement(tempo, 'tempo').text = '2.0'; ET.SubElement(tempo, 'followText').text = '1'
            ET.SubElement(tempo, 'text').text = 'Alla marcia maestoso ♩ = 120'

            vr.insert(0, tempo)
            vr.insert(0, clef_r)
            vr.insert(0, ts_r)
            vr.insert(0, ks_r)

            ks_l = ET.Element('KeySig'); ET.SubElement(ks_l, 'concertKey').text = '0'
            ts_l = ET.Element('TimeSig'); ET.SubElement(ts_l, 'sigN').text = '4'; ET.SubElement(ts_l, 'sigD').text = '4'
            clef_l = ET.Element('Clef'); ET.SubElement(clef_l, 'concertClefType').text = 'F'; ET.SubElement(clef_l, 'transposingClefType').text = 'F'
            vl.insert(0, clef_l)
            vl.insert(0, ts_l)
            vl.insert(0, ks_l)

        if (idx + 1) % 4 == 0 and idx + 1 < len(m_rh):
            lb = ET.SubElement(mr, 'LayoutBreak')
            ET.SubElement(lb, 'subtype').text = 'line'

        staff_rh.append(mr)
        staff_lh.append(ml)

    # Set title VBox
    vbox = staff_rh.find('VBox')
    if vbox is not None: staff_rh.remove(vbox)
    vbox = ET.Element('VBox')
    ET.SubElement(vbox, 'height').text = '10'
    t1 = ET.SubElement(vbox, 'Text')
    ET.SubElement(t1, 'style').text = 'title'
    ET.SubElement(t1, 'text').text = '英雄'
    t2 = ET.SubElement(vbox, 'Text')
    ET.SubElement(t2, 'style').text = 'subtitle'
    ET.SubElement(t2, 'text').text = '钢琴进行曲 (Piano March)'
    staff_rh.insert(0, vbox)

    export_score(root, '英雄')


def export_score(root, name):
    xml_str = ET.tostring(root, encoding='utf-8')
    parsed = minidom.parseString(xml_str)
    pretty = parsed.toprettyxml(indent='  ', encoding='utf-8')

    os.makedirs('music', exist_ok=True)
    mscx_path = os.path.abspath(f'music/{name}.mscx')
    with open(mscx_path, 'wb') as f:
        f.write(pretty)
    print(f'Wrote {mscx_path}')

    if os.path.exists(MUSESCORE_EXE):
        mscz_path = os.path.abspath(f'music/{name}.mscz')
        mp3_path = os.path.abspath(f'music/{name}.mp3')
        pdf_path = os.path.abspath(f'music/{name}.pdf')

        print(f'Rendering {name}.mscz...')
        subprocess.run([MUSESCORE_EXE, '-o', mscz_path, mscx_path], capture_output=True)
        print(f'Rendering {name}.mp3...')
        subprocess.run([MUSESCORE_EXE, '-o', mp3_path, mscx_path], capture_output=True)
        print(f'Rendering {name}.pdf...')
        subprocess.run([MUSESCORE_EXE, '-o', pdf_path, mscx_path], capture_output=True)
        print(f'Done: {name} (mp3 exists: {os.path.exists(mp3_path)}, pdf exists: {os.path.exists(pdf_path)})')

if __name__ == '__main__':
    build_knowledge()
    build_mystery()
    build_hero()
