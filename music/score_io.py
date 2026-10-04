"""《安眠者有福》原声的记谱与导出：MuseScore 4 曲谱（.mscz）与 MIDI（.mid）。

记谱写法（一个小节一行，空格分隔）：
    'D5:e C#5:e B4:q F#5:q> E5:e F#5:e'   音名:时值，'+' 连成和弦，'r' 为休止
时值：s 十六分、e 八分、e. 附点八分、q 四分、q. 附点四分、h 二分、h. 附点二分、w 全音符。
后缀：> 重音，- 保持音，^ 延长记号。不用连音线与三连音，长音都写在小节之内。
"""
import os
import re
import json
import struct
import zipfile
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from fractions import Fraction
from xml.sax.saxutils import escape

HERE = os.path.dirname(os.path.abspath(__file__))

STEP_SEMI = {'C': 0, 'D': 2, 'E': 4, 'F': 5, 'G': 7, 'A': 9, 'B': 11}
STEP_TPC = {'F': 13, 'C': 14, 'G': 15, 'D': 16, 'A': 17, 'E': 18, 'B': 19}
SHARP_ORDER = 'FCGDAEB'
DUR = {  # 记号 -> (durationType, 附点数, 以 480 为四分音符的 tick)
    's': ('16th', 0, 120), 'e': ('eighth', 0, 240), 'e.': ('eighth', 1, 360),
    'q': ('quarter', 0, 480), 'q.': ('quarter', 1, 720),
    'h': ('half', 0, 960), 'h.': ('half', 1, 1440), 'w': ('whole', 0, 1920),
}
VELOCITY = {'ppp': 20, 'pp': 33, 'p': 49, 'mp': 64, 'mf': 80, 'f': 96, 'ff': 112}
ACC = {-1: 'accidentalFlat', 0: 'accidentalNatural', 1: 'accidentalSharp'}
FLAG = {'>': 'accent', '^': 'fermata', '-': 'tenuto'}


def parse_pitch(name):
    step, rest, alter = name[0], name[1:], 0
    while rest and rest[0] in '#b':
        alter += 1 if rest[0] == '#' else -1
        rest = rest[1:]
    octave = int(rest)
    return step, alter, octave, 12 * (octave + 1) + STEP_SEMI[step] + alter


def midi_of(name):
    return parse_pitch(name)[3]


def key_alters(fifths):
    alters = {s: 0 for s in STEP_SEMI}
    order = SHARP_ORDER if fifths > 0 else SHARP_ORDER[::-1]
    for s in order[:abs(fifths)]:
        alters[s] = 1 if fifths > 0 else -1
    return alters


def parse_line(line):
    """'A4:q D5+F5:h r:q' -> [(音高列表或 None, 时值记号, 标记集合)]"""
    events = []
    for tok in line.split():
        flags = set()
        while tok[-1] in FLAG:
            flags.add(FLAG[tok[-1]])
            tok = tok[:-1]
        notes, dur = tok.split(':')
        events.append((None if notes == 'r' else notes.split('+'), dur, flags))
    return events


def line_ticks(line):
    return sum(DUR[d][2] for _, d, _ in parse_line(line))


def shift(line, octaves):
    """整行移八度。"""
    out = []
    for pitches, dur, flags in parse_line(line):
        mark = ''.join(k for k, v in FLAG.items() if v in flags)
        if pitches is None:
            out.append(f'r:{dur}{mark}')
            continue
        moved = []
        for p in pitches:
            step, alter, octave, _ = parse_pitch(p)
            acc = '#' * alter if alter > 0 else 'b' * -alter
            moved.append(f'{step}{acc}{octave + octaves}')
        out.append('+'.join(moved) + f':{dur}{mark}')
    return ' '.join(out)


def octaves(line, below=-1):
    """旋律加八度叠置（默认向下）。"""
    out = []
    for pitches, dur, flags in parse_line(line):
        mark = ''.join(k for k, v in FLAG.items() if v in flags)
        if pitches is None:
            out.append(f'r:{dur}{mark}')
            continue
        extra = shift('+'.join(pitches) + ':q', below).split(':')[0].split('+')
        out.append('+'.join(extra + pitches) + f':{dur}{mark}')
    return ' '.join(out)


# ---------------------------------------------------------------- 乐曲结构

@dataclass
class Part:
    key: str                 # 程序内的名字，也是合成器音色
    name: str                # 谱面乐器名
    short: str
    instrument_id: str       # MuseScore instrumentId
    program: int             # General MIDI 音色
    lo: int = 21
    hi: int = 108
    staves: int = 1
    bass_clef: bool = False  # 单行谱是否用低音谱号
    pan: float = 0.0         # -1 左 … 1 右
    gain: float = 1.0
    voice: str = ''          # 合成器音色，默认同 key
    template_id: str = ''    # MuseScore 本机乐器模板 ID
    bank: int = 0            # SoundFont/MIDI bank
    drum_map: dict = field(default_factory=dict)
    channel_name: str = ''
    soundfont: str = ''       # music/ 下的专用采样库，相对路径
    transpose: tuple = (0, 0)  # 记谱移调（半音, 音级），如降 B 调单簧管 (-2, -1)、钢片琴 (12, 7)
    amateur: tuple = None      # 业余音域 (低, 高)；缺省同专业音域 lo–hi
    channels: tuple = ()       # 多通道模板的全部通道 ((名称, bank, program), ...)；channel_name 为当前所用

    def playback_pitch(self, pitch):
        return self.drum_map[pitch] if self.drum_map else pitch


@dataclass
class Bar:
    lines: dict              # 谱表序号 -> 一行记谱；缺省为整小节休止
    dyn: dict = field(default_factory=dict)
    mark: str = ''
    text: dict = field(default_factory=dict)
    tempo: tuple = None      # (每分钟四分音符数, 谱面文字)
    harm: list = None        # 本小节和弦（等分），供演奏层换踏板与强调紧张和弦；不写入谱面
    lead: int = None         # 本小节旋律所在谱表，供演奏层突出旋律
    tech: dict = field(default_factory=dict)  # 谱表序号 -> 演奏法（如 'pizzicato'），写为 PlayTechAnnotation


@dataclass
class Piece:
    slug: str                # 文件名，也是游戏内的曲目 id
    title: str
    subtitle: str
    key: int                 # 调号：正数升号、负数降号
    time: tuple
    parts: list
    bars: list
    loop: bool = True
    reverb: float = 2.2      # 混响时间（秒）
    wet: float = 0.24
    mix: dict = field(default_factory=dict)  # 谱表序号 -> 增益（dB），只影响合成音频
    style: dict = None       # 演奏诠释参数（interpret.Style），只影响合成音频
    squeeze: float = 1.6     # 总线压缩比：作为背景循环的曲子收得更紧

    def staff_parts(self):
        """全局谱表序号 -> (声部, 声部内谱表序号)。"""
        out = []
        for part in self.parts:
            for i in range(part.staves):
                out.append((part, i))
        return out

    @property
    def measure_ticks(self):
        num, den = self.time
        return 1920 * num // den

    def full_rest(self):
        num, den = self.time
        if (num, den) == (4, 4):
            return 'r:w'
        if (num, den) in ((3, 4), (6, 8)):
            return 'r:h.'
        raise ValueError(self.time)

    def line(self, bar, staff):
        return bar.lines.get(staff) or self.full_rest()

    def tempo_map(self):
        """[(小节序号, 每分钟四分音符数)]"""
        return [(i, b.tempo[0]) for i, b in enumerate(self.bars) if b.tempo]

    def validate(self):
        n = len(self.staff_parts())
        for bi, bar in enumerate(self.bars):
            for si in bar.lines:
                if si >= n:
                    raise ValueError(f'{self.slug} 第 {bi + 1} 小节：没有第 {si} 行谱表')
            for si in range(n):
                t = line_ticks(self.line(bar, si))
                if t != self.measure_ticks:
                    raise ValueError(f'{self.slug} 第 {bi + 1} 小节第 {si} 行：{t} != {self.measure_ticks}')
        if not self.bars[0].tempo:
            raise ValueError(f'{self.slug}：第一小节须有速度')


# ---------------------------------------------------------------- MSCX 输出

# 框鼓、大鼓、木块复用本机击打定义；塔布拉沿用结构并采用 Tabla.sf2 的击法键位。
DRUM_DEFINITIONS = {
    'frame-drum': '<Drum pitch="45"><head>normal</head><line>0</line><voice>0</voice><name>Frame Drum</name><stem>1</stem><shortcut>A</shortcut></Drum>',
    'tablas': '<Drum pitch="73"><head>normal</head><line>1</line><voice>0</voice><name>Ge (open bass)</name><stem>1</stem><shortcut>A</shortcut><panelRow>0</panelRow><panelColumn>0</panelColumn></Drum><Drum pitch="71"><head>normal</head><line>-1</line><voice>0</voice><name>Na</name><stem>1</stem><shortcut>B</shortcut><panelRow>0</panelRow><panelColumn>1</panelColumn></Drum><Drum pitch="87"><head>normal</head><line>-1</line><voice>0</voice><name>Tin</name><stem>1</stem><shortcut>C</shortcut><panelRow>0</panelRow><panelColumn>2</panelColumn></Drum><Drum pitch="84"><head>normal</head><line>-1</line><voice>0</voice><name>Te</name><stem>1</stem><shortcut>D</shortcut><panelRow>0</panelRow><panelColumn>3</panelColumn></Drum>',
    'bass-drum': '<Drum pitch="36"><head>normal</head><line>0</line><voice>0</voice><name>Concert Bass Drum</name><stem>2</stem><shortcut>A</shortcut></Drum>',
    'temple-blocks': '<Drum pitch="58"><head>normal</head><line>8</line><voice>0</voice><name>Low Temple Block</name><stem>1</stem><shortcut>A</shortcut><panelRow>0</panelRow><panelColumn>0</panelColumn></Drum><Drum pitch="59"><head>normal</head><line>6</line><voice>0</voice><name>Low-Mid Temple Block</name><stem>1</stem><shortcut>B</shortcut><panelRow>0</panelRow><panelColumn>1</panelColumn></Drum><Drum pitch="60"><head>normal</head><line>4</line><voice>0</voice><name>Mid Temple Block</name><stem>1</stem><shortcut>C</shortcut><panelRow>0</panelRow><panelColumn>2</panelColumn></Drum><Drum pitch="61"><head>normal</head><line>2</line><voice>0</voice><name>High-Mid Temple Block</name><stem>1</stem><shortcut>D</shortcut><panelRow>0</panelRow><panelColumn>3</panelColumn></Drum><Drum pitch="62"><head>normal</head><line>0</line><voice>0</voice><name>High Temple Block</name><stem>1</stem><shortcut>E</shortcut><panelRow>0</panelRow><panelColumn>4</panelColumn></Drum>',
    'dagu': '<Drum pitch="87"><head>normal</head><line>0</line><voice>0</voice><name>Dagu</name><stem>1</stem><shortcut>A</shortcut><panelRow>0</panelRow><panelColumn>0</panelColumn></Drum>',
    'wood-blocks': '<Drum pitch="76"><head>normal</head><line>-1</line><voice>0</voice><name>High Wood Block</name><stem>1</stem><shortcut>A</shortcut></Drum><Drum pitch="77"><head>normal</head><line>1</line><voice>0</voice><name>Low Wood Block</name><stem>1</stem><shortcut>B</shortcut></Drum>',
}


def _note_xml(name, state, kalt, part):
    step, alter, octave, midi = parse_pitch(name)
    if part.drum_map:
        return f'<Note><pitch>{part.playback_pitch(midi)}</pitch><tpc>14</tpc></Note>'
    tpc = STEP_TPC[step] + 7 * alter
    expected = state.get((step, octave), kalt[step])
    acc = ''
    if alter != expected:
        acc = f'<Accidental><subtype>{ACC[alter]}</subtype></Accidental>'
        state[(step, octave)] = alter
    return f'<Note><pitch>{midi}</pitch><tpc>{tpc}</tpc>{acc}</Note>'


def _events_xml(line, kalt, part):
    state, out = {}, []
    for pitches, dur, flags in parse_line(line):
        dtype, dots, _ = DUR[dur]
        dxml = (f'<dots>{dots}</dots>' if dots else '') + f'<durationType>{dtype}</durationType>'  # MuseScore 要求附点在前
        if pitches is None:
            out.append(f'<Rest>{dxml}</Rest>')
            continue
        arts = ''
        if 'accent' in flags:
            arts += '<Articulation><subtype>articAccentAbove</subtype></Articulation>'
        if 'tenuto' in flags:
            arts += '<Articulation><subtype>articTenutoAbove</subtype></Articulation>'
        if 'fermata' in flags:
            out.append('<Fermata><subtype>fermataAbove</subtype></Fermata>')
        out.append(f'<Chord>{dxml}{arts}' + ''.join(_note_xml(p, state, kalt, part) for p in pitches) + '</Chord>')
    return ''.join(out)


def _part_xml(pid, part):
    staves = []
    percussion = bool(part.drum_map)
    group = 'percussion' if percussion else 'pitched'
    staff_name = 'perc1Line' if percussion else 'stdNormal'
    lines = '<lines>1</lines>' if percussion else ''
    for i in range(part.staves):
        extra = ''
        if part.staves == 2 and i == 0:
            extra = '\n        <bracket type="1" span="2" col="0" visible="1" />\n        <barLineSpan>1</barLineSpan>'
        if not percussion and ((part.staves == 2 and i == 1) or (part.staves == 1 and part.bass_clef)):
            extra += '\n        <defaultClef>F</defaultClef>'
        if percussion:
            extra += '\n        <defaultClef>PERC</defaultClef>'
        staves.append(f'''      <Staff>
        <StaffType group="{group}">
          <name>{staff_name}</name>{lines}
          </StaffType>{extra}
        </Staff>
''')
    clef = ''
    if part.staves == 2:
        clef = '\n        <clef staff="2">F</clef>'
    elif part.bass_clef:
        clef = '\n        <clef>F</clef>'
    if percussion:
        clef = '\n        <clef>PERC</clef>'
    drumset = ('<useDrumset>1</useDrumset>' + DRUM_DEFINITIONS[part.template_id]) if percussion else ''
    channel_name = f' name="{part.channel_name}"' if part.channel_name else ''
    bank = f'<controller ctrl="0" value="{part.bank >> 7}" /><controller ctrl="32" value="{part.bank & 127}" />'
    a_lo, a_hi = part.amateur or (part.lo, part.hi)
    transpose = ''
    if part.transpose != (0, 0):
        transpose = (f'\n        <transposeDiatonic>{part.transpose[1]}</transposeDiatonic>'
                     f'\n        <transposeChromatic>{part.transpose[0]}</transposeChromatic>')
    instrument_id = f'\n        <instrumentId>{part.instrument_id}</instrumentId>' if part.instrument_id else ''
    if part.channels:
        channels = ''.join(
            f'\n        <Channel name="{n}">\n          <controller ctrl="0" value="{b >> 7}" />'
            f'<controller ctrl="32" value="{b & 127}" />\n          <program value="{pr}" />\n          </Channel>'
            for n, b, pr in part.channels)
    else:
        channels = (f'\n        <Channel{channel_name}>\n          {bank}\n'
                    f'          <program value="{part.program}" />\n          </Channel>')
    return (f'    <Part id="{pid}">\n' + ''.join(staves) + f'''      <trackName>{part.name}</trackName>
      <Instrument id="{part.template_id}">
        <longName>{part.name}</longName>
        <shortName>{part.short}</shortName>
        <trackName>{part.name}</trackName>
        <minPitchP>{part.lo}</minPitchP>
        <maxPitchP>{part.hi}</maxPitchP>
        <minPitchA>{a_lo}</minPitchA>
        <maxPitchA>{a_hi}</maxPitchA>{transpose}{instrument_id}{clef}{drumset}{channels}
        </Instrument>
      </Part>
''')


def build_mscx(piece):
    piece.validate()
    kalt = key_alters(piece.key)
    num, den = piece.time
    staff_parts = piece.staff_parts()
    staves_xml = []
    for si, (part, inner) in enumerate(staff_parts):
        rows = [f'    <Staff id="{si + 1}">\n']
        if si == 0:
            rows.append('      <VBox>\n        <height>12</height>\n'
                        f'        <Text><style>title</style><text>{escape(piece.title)}</text></Text>\n'
                        f'        <Text><style>subtitle</style><text>{escape(piece.subtitle)}</text></Text>\n'
                        '        <Text><style>composer</style><text>Claude</text></Text>\n'
                        '        </VBox>\n')
        for bi, bar in enumerate(piece.bars):
            head = ''
            if bi == 0:
                head += (('' if part.drum_map else f'<KeySig><concertKey>{piece.key}</concertKey></KeySig>') +
                         f'<TimeSig><sigN>{num}</sigN><sigD>{den}</sigD></TimeSig>')
            if si == 0 and bar.mark:
                head += f'<RehearsalMark><text>{escape(bar.mark)}</text></RehearsalMark>'
            if si == 0 and bar.tempo:
                bpm, label = bar.tempo
                head += (f'<Tempo><tempo>{bpm / 60:.6f}</tempo><followText>1</followText>'
                         f'<text>{label}</text></Tempo>')
            if si in bar.text:
                head += f'<StaffText><text><i>{escape(bar.text[si])}</i></text></StaffText>'
            if si in bar.dyn:
                d = bar.dyn[si]
                head += f'<Dynamic><subtype>{d}</subtype><velocity>{VELOCITY[d]}</velocity></Dynamic>'
            if si in bar.tech:  # MuseScore 把演奏法写在力度之后
                label = {'pizzicato': 'pizz.', 'arco': 'arco'}[bar.tech[si]]
                head += (f'<PlayTechAnnotation><playTechType>{bar.tech[si]}</playTechType>'
                         f'<text>{label}</text></PlayTechAnnotation>')
            body = _events_xml(piece.line(bar, si), kalt, part)
            tail = '<BarLine><subtype>end</subtype></BarLine>' if bi == len(piece.bars) - 1 else ''
            rows.append(f'      <Measure><voice>{head}{body}{tail}</voice></Measure>\n')
        rows.append('      </Staff>\n')
        staves_xml.append(''.join(rows))
    parts_xml = ''.join(_part_xml(i + 1, p) for i, p in enumerate(piece.parts))
    mscx = ('<?xml version="1.0" encoding="UTF-8"?>\n'
            '<museScore version="4.70">\n'
            '  <programVersion>4.7.5</programVersion>\n'
            '  <Score>\n'
            '    <Division>480</Division>\n'
            '    <open>1</open>\n'
            f'    <metaTag name="workTitle">{escape(piece.title)}</metaTag>\n'
            f'    <metaTag name="subtitle">{escape(piece.subtitle)}</metaTag>\n'
            '    <metaTag name="composer">Claude</metaTag>\n'
            '    <metaTag name="creationDate">2026-10-04</metaTag>\n'
            + parts_xml + ''.join(staves_xml) +
            '    </Score>\n'
            '  </museScore>\n')
    return apply_expression_playback(mscx, piece)


def apply_expression_playback(mscx, piece):
    """补谱中文字尚未表达的播放控制；也可用于保留用户编辑的现有总谱。"""
    if piece.slug not in ('main_theme', 'door_side', 'day_afternoon', 'day_evening'):
        return mscx
    root = ET.fromstring(mscx)
    score = root.find('Score')
    lengths = {'16th': .25, 'eighth': .5, 'quarter': 1., 'half': 2., 'whole': 4.}
    def connect(kind, start, end, properties):
        start_item, sm, sq = start
        end_item, em, eq = end
        fraction = Fraction(eq - sq).limit_denominator() / 4
        ET.SubElement(start_item, 'Spanner', type=kind).extend([
            ET.fromstring(f'<{kind}><visible>0</visible>{properties}</{kind}>'),
            ET.fromstring(f'<next><location><measures>{em-sm}</measures><fractions>{fraction.numerator}/{fraction.denominator}</fractions></location></next>')])
        ET.SubElement(end_item, 'Spanner', type=kind).append(ET.fromstring(
            f'<prev><location><measures>{sm-em}</measures><fractions>{-fraction.numerator}/{fraction.denominator}</fractions></location></prev>'))
    if piece.slug == 'main_theme':
        measures = score.findall('Staff')[3].findall('Measure')
        start = measures[34].find('voice')
        end = measures[35].find('voice')
        # 钢琴最后一个整音以 p 起音，随后自然衰减；只影响右手谱表。
        props = '<subtype>1</subtype><veloChange>-31</veloChange><singleNoteDynamics>0</singleNoteDynamics><voiceAssignment>allInStaff</voiceAssignment>'
        start.insert(list(start).index(start.find('Chord')), ET.fromstring(
            f'<Spanner type="HairPin"><HairPin><visible>0</visible>{props}</HairPin><next><location><measures>1</measures><fractions>0/1</fractions></location></next></Spanner>'))
        end.insert(list(end).index(end.find('Chord')), ET.fromstring(
            '<Spanner type="HairPin"><prev><location><measures>-1</measures><fractions>0/1</fractions></location></prev></Spanner>'))
    ranges = {'main_theme': [(3, 4, 12), (2, 12, 20)], 'door_side': [(3, 0, 16)]}
    for si, begin, finish in ranges.get(piece.slug, []):
        run = []
        for bi, measure in enumerate(score.findall('Staff')[si].findall('Measure')[begin:finish], begin):
            beat = 0.
            for child in measure.find('voice'):
                if child.tag not in ('Chord', 'Rest'):
                    continue
                if child.tag == 'Chord':
                    run.append((child, bi, beat))
                else:
                    if len(run) > 1:
                        connect('Slur', run[0], run[-1], '')
                    run = []
                dots = int(child.findtext('dots', '0'))
                beat += lengths[child.findtext('durationType')] * (2 - .5 ** dots)
        if len(run) > 1:
            connect('Slur', run[0], run[-1], '')
    if piece.slug in ('day_afternoon', 'day_evening'):
        # 复用已有演奏层的乐句呼吸，只作用于散板引子，节奏段恢复原速度。
        from interpret import Style, tempo_curve
        qgrid, tgrid = tempo_curve(piece, Style(**piece.style))
        count = 4 if piece.slug == 'day_afternoon' else 8
        measures = score.find('Staff').findall('Measure')
        for bi, measure in enumerate(measures[:count]):
            voice = measure.find('voice')
            beat = 0.
            for child in list(voice):
                if child.tag not in ('Chord', 'Rest'):
                    continue
                q = bi * 4 + beat
                k = min(int(round(q * 96)), len(tgrid) - 2)
                bpm = 60 / ((tgrid[k + 1] - tgrid[k]) * 96)
                tempo = ET.fromstring(f'<Tempo><visible>0</visible><tempo>{bpm / 60:.8f}</tempo><followText>0</followText><text>♩ = {bpm:.3f}</text></Tempo>')
                voice.insert(list(voice).index(child), tempo)
                dots = int(child.findtext('dots', '0'))
                beat += lengths[child.findtext('durationType')] * (2 - .5 ** dots)
        restore = piece.bars[count].tempo[0] if piece.bars[count].tempo else piece.bars[0].tempo[0]
        if not piece.bars[count].tempo:
            measures[count].find('voice').insert(0, ET.fromstring(f'<Tempo><visible>0</visible><tempo>{restore / 60:.8f}</tempo><followText>0</followText><text>♩ = {restore}</text></Tempo>'))
    return '<?xml version="1.0" encoding="UTF-8"?>\n' + ET.tostring(root, encoding='unicode')


def soundfont_input(part):
    """MuseScore 4.7 FluidResolver 的指定预设资源格式。"""
    name = os.path.splitext(os.path.basename(part.soundfont))[0]
    return {'resourceMeta': {
        'id': f'{name}\\{part.bank}\\{part.program}',
        'type': 'fluid_soundfont', 'vendor': 'Fluid', 'hasNativeEditorSupport': False,
        'attributes': {'soundFontName': name, 'playbackSetupData': 'last.last.last',
                       'presetName': name, 'presetBank': str(part.bank), 'presetProgram': str(part.program)}
    }, 'unitConfiguration': {}}


def write_mscz(piece, path):
    base = os.path.splitext(os.path.basename(path))[0]
    with open(os.path.join(HERE, 'score_style.mss'), 'rb') as f:
        style = f.read()
    with open(os.path.join(HERE, 'viewsettings.json'), 'rb') as f:
        view = f.read()
    custom_tracks = [{'partId': str(i + 1), 'instrumentId': part.template_id,
                      'in': soundfont_input(part)} for i, part in enumerate(piece.parts) if part.soundfont]
    container = ('<?xml version="1.0" encoding="UTF-8"?>\n<container>\n  <rootfiles>\n'
                 '    <rootfile full-path="score_style.mss"/>\n'
                 f'    <rootfile full-path="{base}.mscx"/>\n'
                 '    <rootfile full-path="viewsettings.json"/>\n' +
                 ('    <rootfile full-path="audiosettings.json"/>\n' if custom_tracks else '') +
                 '    </rootfiles>\n  </container>\n')
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as z:
        z.writestr(f'{base}.mscx', build_mscx(piece))
        z.writestr('score_style.mss', style)
        z.writestr('viewsettings.json', view)
        z.writestr('META-INF/container.xml', container)
        if custom_tracks:
            z.writestr('audiosettings.json', json.dumps({'tracks': custom_tracks}, ensure_ascii=False))


# ---------------------------------------------------------------- MIDI 输出
def write_midi(piece, path):
    num, den = piece.time
    dd = {4: 2, 8: 3}[den]
    conductor = [(0, 0, bytes([0xFF, 0x58, 4, num, dd, 24, 8]))]
    for bi, bpm in piece.tempo_map():
        us = int(60_000_000 / bpm)
        conductor.append((bi * piece.measure_ticks, 0, b'\xff\x51\x03' + us.to_bytes(3, 'big')))
    if piece.slug in ('day_afternoon', 'day_evening'):
        from interpret import Style, tempo_curve
        qgrid, tgrid = tempo_curve(piece, Style(**piece.style))
        count = 4 if piece.slug == 'day_afternoon' else 8
        for bi, bar in enumerate(piece.bars[:count]):
            tick = bi * piece.measure_ticks
            for _, dur, _ in parse_line(piece.line(bar, 0)):
                k = min(round(tick / 480 * 96), len(tgrid) - 2)
                bpm = 60 / ((tgrid[k + 1] - tgrid[k]) * 96)
                conductor.append((tick, 1, b'\xff\x51\x03' + int(60_000_000 / bpm).to_bytes(3, 'big')))
                tick += DUR[dur][2]
        if not piece.bars[count].tempo:
            conductor.append((count * piece.measure_ticks, 1, b'\xff\x51\x03' + int(60_000_000 / piece.bars[0].tempo[0]).to_bytes(3, 'big')))
    tracks = [_track_bytes(conductor)]
    channel = 0
    for part in piece.parts:
        ch = 9 if part.drum_map else channel
        evs = [(0, 0, bytes([0xB0 | ch, 0, part.bank >> 7])),
               (0, 0, bytes([0xB0 | ch, 32, part.bank & 127])),
               (0, 0, bytes([0xC0 | ch, part.program]))]
        name = part.name.encode('utf-8')
        evs.append((0, 0, b'\xff\x03' + _vlq(len(name)) + name))
        staff_ids = [i for i, (p, _) in enumerate(piece.staff_parts()) if p is part]
        vel = {}
        for bi, bar in enumerate(piece.bars):
            for si in staff_ids:
                if si in bar.dyn:
                    vel[si] = VELOCITY[bar.dyn[si]]
                t = bi * piece.measure_ticks
                for pitches, dur, flags in parse_line(piece.line(bar, si)):
                    ticks = DUR[dur][2]
                    if pitches:
                        length = int(ticks * (1.5 if 'fermata' in flags else 0.96))
                        legato = ((piece.slug == 'door_side' and si == 3) or
                                  (piece.slug == 'main_theme' and
                                   ((si == 3 and 4 <= bi < 12) or (si == 2 and 12 <= bi < 20))))
                        if legato and 'fermata' not in flags:
                            length = ticks - 1
                        v = min(127, vel.get(si, 64) + (14 if 'accent' in flags else 0))
                        if piece.slug == 'main_theme' and si == 3 and 34 <= bi < 36:
                            v -= min(31, int((t - 34 * piece.measure_ticks) / piece.measure_ticks * 31))
                        for p in pitches:
                            m = part.playback_pitch(midi_of(p))
                            evs.append((t, 1, bytes([0x90 | ch, m, v])))
                            evs.append((t + length, 0, bytes([0x80 | ch, m, 0])))
                    t += ticks
        tracks.append(_track_bytes(evs))
        if not part.drum_map:
            channel += 1
            if channel == 9:
                channel = 10
    with open(path, 'wb') as f:
        f.write(b'MThd' + struct.pack('>IHHH', 6, 1, len(tracks), 480) + b''.join(tracks))


def _vlq(n):
    out = [n & 0x7F]
    n >>= 7
    while n:
        out.insert(0, (n & 0x7F) | 0x80)
        n >>= 7
    return bytes(out)


def _track_bytes(events):
    events.sort(key=lambda e: (e[0], e[1]))
    data, last = b'', 0
    for t, _, msg in events:
        data += _vlq(t - last) + msg
        last = t
    data += _vlq(0) + b'\xff\x2f\x00'
    return b'MTrk' + struct.pack('>I', len(data)) + data



# ---------------------------------------------------------------- 曲谱与源数据一致性
# 曲谱（.mscz）可以在 MuseScore 里手工编辑；cues.py 必须与之同步。比对的是“音乐内容”：
# 声部（名称、模板、音色、移调）、每小节的音符与时值、力度、文字、演奏法、排练记号与可见速度。
# 排版、eid、隐形播放控制（visible=0 的 Tempo、Slur、HairPin）不比对。

def score_content(mscx):
    parts = []
    for p in re.findall(r'<Part id="\d+">(.*?)</Part>', mscx, re.S):
        def g(pat):
            m = re.search(pat, p, re.S)
            return m.group(1) if m else ''
        first = re.search(r'<Channel(?: name="[^"]*")?>(.*?)</Channel>', p, re.S)
        ch = first.group(1) if first else ''
        msb = re.search(r'ctrl="0" value="(\d+)"', ch)
        lsb = re.search(r'ctrl="32" value="(\d+)"', ch)
        prog = re.search(r'<program value="(\d+)"', ch)
        parts.append((g(r'<Instrument id="([^"]*)"'), g(r'<longName>([^<]*)'), g(r'<shortName>([^<]*)'),
                      g(r'<instrumentId>([^<]*)'),
                      (int(msb.group(1)) << 7 if msb else 0) + (int(lsb.group(1)) if lsb else 0),
                      int(prog.group(1)) if prog else 0, g(r'<transposeChromatic>(-?\d+)') or '0'))
    staves = []
    for _, body in re.findall(r'<Staff id="(\d+)">(.*?)</Staff>\s*(?=<Staff id|</Score>)', mscx, re.S):
        bars = []
        for m in re.findall(r'<Measure[^>]*>(.*?)</Measure>', body, re.S):
            voice = re.findall(r'<voice>(.*?)</voice>', m, re.S)
            events = []
            pattern = r'<(Chord|Rest|Dynamic|StaffText|PlayTechAnnotation|RehearsalMark|Tempo)>(.*?)</\1>'
            for el in re.finditer(pattern, voice[0] if voice else '', re.S):
                kind, b = el.groups()
                if kind in ('Chord', 'Rest'):
                    d = re.search(r'<durationType>(\w+)', b)
                    dur = (d.group(1) if d else 'measure') + ('.' if '<dots>' in b else '')
                    pitches = tuple(sorted(int(v) for v in re.findall(r'<pitch>(\d+)', b)))
                    events.append((kind[0], dur, pitches))
                elif kind == 'Dynamic':
                    events.append(('D', re.search(r'<subtype>(\w+)', b).group(1)))
                elif kind == 'Tempo':
                    if '<visible>0</visible>' not in b:
                        events.append(('T', round(float(re.search(r'<tempo>([\d.]+)', b).group(1)), 3)))
                elif kind == 'PlayTechAnnotation':
                    events.append(('P', re.search(r'<playTechType>(\w+)', b).group(1)))
                else:
                    t = re.search(r'<text>(.*?)</text>', b, re.S)
                    events.append((kind[0], re.sub('<[^>]+>', '', t.group(1)) if t else ''))
            bars.append(events)
        staves.append(bars)
    return parts, staves


def compare_with_score(piece, path):
    """返回曲谱与 cues.py 的差异列表（空表示一致）。"""
    with zipfile.ZipFile(path) as z:
        stored = z.read([n for n in z.namelist() if n.endswith('.mscx')][0]).decode('utf-8')
    (pa, sa), (pb, sb) = score_content(stored), score_content(build_mscx(piece))
    diffs = []
    labels = ('模板', '名称', '简称', 'MusicXML', 'bank', 'program', '移调')
    if len(pa) != len(pb):
        diffs.append(f'声部数：曲谱 {len(pa)}，源数据 {len(pb)}')
    for i, (x, y) in enumerate(zip(pa, pb)):
        for k, (u, v) in enumerate(zip(x, y)):
            if u != v:
                diffs.append(f'声部 {i + 1} {labels[k]}：曲谱 {u!r}，源数据 {v!r}')
    if len(sa) != len(sb):
        diffs.append(f'谱表数：曲谱 {len(sa)}，源数据 {len(sb)}')
    for si, (x, y) in enumerate(zip(sa, sb)):
        for bi, (u, v) in enumerate(zip(x, y)):
            if u != v:
                diffs.append(f'谱表 {si + 1} 第 {bi + 1} 小节：曲谱 {u}，源数据 {v}')
    return diffs
