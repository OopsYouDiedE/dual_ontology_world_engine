"""《安眠者有福》原声的记谱语言与 MuseScore 4（.mscz）/ MIDI（.mid）编译导出。

记谱语言（支持单谱表多声部、跨小节连音线、乐句连线、三连音、细化时值、奏法与渐变力度）：
    'D5:e (C#5:e B4:q) F#5:q>~ F#5:e E5:e'
    '上声部旋律 || 下声部内声部对位'

时值记号：
    t  三十二分音符 (60 ticks)      s   十六分音符 (120 ticks)     s. 附点十六分 (180 ticks)
    e3 八分三连音   (160 ticks)     e   八分音符   (240 ticks)     e. 附点八分   (360 ticks)
    q3 四分三连音   (320 ticks)     q   四分音符   (480 ticks)     q. 附点四分   (720 ticks)
    h  二分音符     (960 ticks)     h.  附点二分   (1440 ticks)    w  全音符     (1920 ticks)

前缀与后缀标记：
    (   乐句连线（Slur）起点（前缀）
    )   乐句连线（Slur）终点（后缀）
    ~   延音线（Tie，后缀，支持小节内或跨小节同音相连）
    >   重音（Accent）
    -   保持音（Tenuto）
    '   断音/跳音（Staccato）
    ^   延长记号（Fermata）
"""
import json
import math
import os
import re
import struct
import xml.etree.ElementTree as ET
import zipfile
from dataclasses import dataclass, field
from fractions import Fraction
from xml.sax.saxutils import escape

HERE = os.path.dirname(os.path.abspath(__file__))

STEP_SEMI = {'C': 0, 'D': 2, 'E': 4, 'F': 5, 'G': 7, 'A': 9, 'B': 11}
STEP_TPC = {'F': 13, 'C': 14, 'G': 15, 'D': 16, 'A': 17, 'E': 18, 'B': 19}
SHARP_ORDER = 'FCGDAEB'

# 记号 -> (durationType, 附点数, 以 480 为四分音符的 tick, 是否三连音)
DUR = {
    't': ('32nd', 0, 60, False),
    's': ('16th', 0, 120, False),
    's.': ('16th', 1, 180, False),
    'e3': ('eighth', 0, 160, True),
    'e': ('eighth', 0, 240, False),
    'e.': ('eighth', 1, 360, False),
    'q3': ('quarter', 0, 320, True),
    'q': ('quarter', 0, 480, False),
    'q.': ('quarter', 1, 720, False),
    'h': ('half', 0, 960, False),
    'h.': ('half', 1, 1440, False),
    'w': ('whole', 0, 1920, False),
}
VELOCITY = {'ppp': 20, 'pp': 33, 'p': 49, 'mp': 64, 'mf': 80, 'f': 96, 'ff': 112}
ACC = {-1: 'accidentalFlat', 0: 'accidentalNatural', 1: 'accidentalSharp'}
FLAG = {
    '>': 'accent',
    '^': 'fermata',
    '-': 'tenuto',
    "'": 'staccato',
    '~': 'tie',
    ')': 'slur_end',
}
FLAG_REV = {v: k for k, v in FLAG.items()}


def parse_pitch(name):
    step, rest, alter = name[0], name[1:], 0
    while rest and rest[0] in '#b':
        alter += 1 if rest[0] == '#' else -1
        rest = rest[1:]
    octave = int(rest)
    return step, alter, octave, 12 * (octave + 1) + STEP_SEMI[step] + alter


def midi_of(name):
    return parse_pitch(name)[3]


def pitch_name(midi, prefer_sharp=True):
    names = ('C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B') if prefer_sharp else (
        'C', 'Db', 'D', 'Eb', 'E', 'F', 'Gb', 'G', 'Ab', 'A', 'Bb', 'B')
    return f'{names[midi % 12]}{midi // 12 - 1}'


def key_alters(fifths):
    alters = {s: 0 for s in STEP_SEMI}
    order = SHARP_ORDER if fifths > 0 else SHARP_ORDER[::-1]
    for s in order[:abs(fifths)]:
        alters[s] = 1 if fifths > 0 else -1
    return alters


def split_voices(line):
    """支持 'voice1 || voice2' 或 tuple/list 多声部写法。"""
    if isinstance(line, (tuple, list)):
        return [str(v).strip() for v in line if str(v).strip()]
    if '||' in line:
        return [v.strip() for v in line.split('||') if v.strip()]
    return [line.strip()]


def parse_line(line):
    """'(A4:q D5+F5:h)~ r:q' -> [(音高列表或 None, 时值记号, 标记集合)]"""
    if '||' in str(line) or isinstance(line, (tuple, list)):
        line = split_voices(line)[0]
    events = []
    for tok in line.split():
        flags = set()
        while tok.startswith('('):
            flags.add('slur_start')
            tok = tok[1:]
        while tok and tok[-1] in FLAG:
            flags.add(FLAG[tok[-1]])
            tok = tok[:-1]
        notes, dur = tok.split(':')
        if dur not in DUR:
            raise ValueError(f'未知时值记号 {dur!r} (token={tok!r})')
        events.append((None if notes == 'r' else notes.split('+'), dur, flags))
    return events


def format_events(events):
    out = []
    for pitches, dur, flags in events:
        pre = '(' if 'slur_start' in flags else ''
        suf = ''.join(FLAG_REV[f] for f in ('accent', 'tenuto', 'staccato', 'fermata', 'slur_end', 'tie') if f in flags)
        body = 'r' if pitches is None else '+'.join(pitches)
        out.append(f'{pre}{body}:{dur}{suf}')
    return ' '.join(out)


def line_ticks(line):
    return sum(DUR[d][2] for _, d, _ in parse_line(line))


def shift(line, octaves):
    """整行（含多声部）移八度。"""
    voices = split_voices(line)
    if len(voices) > 1:
        return ' || '.join(shift(v, octaves) for v in voices)
    moved_events = []
    for pitches, dur, flags in parse_line(voices[0]):
        if pitches is None:
            moved_events.append((None, dur, flags))
            continue
        moved = []
        for p in pitches:
            step, alter, octave, _ = parse_pitch(p)
            acc = '#' * alter if alter > 0 else 'b' * -alter
            moved.append(f'{step}{acc}{octave + octaves}')
        moved_events.append((moved, dur, flags))
    return format_events(moved_events)


def octaves(line, below=-1):
    """旋律加八度叠置（默认向下）。"""
    voices = split_voices(line)
    if len(voices) > 1:
        return ' || '.join(octaves(v, below) for v in voices)
    out_events = []
    for pitches, dur, flags in parse_line(voices[0]):
        if pitches is None:
            out_events.append((None, dur, flags))
            continue
        extra = shift('+'.join(pitches) + ':q', below).split(':')[0].split('+')
        out_events.append((extra + pitches, dur, flags))
    return format_events(out_events)


# ---------------------------------------------------------------- 乐曲结构

@dataclass
class Part:
    key: str                 # 程序内的名字，也是合成器音色
    name: str                # 谱面乐器名 (longName)
    short: str               # 谱面简称 (shortName)
    instrument_id: str       # MuseScore instrumentId
    program: int             # General MIDI 音色
    lo: int = 21
    hi: int = 108
    staves: int = 1
    bass_clef: bool = False  # 单行谱是否用低音谱号
    pan: float = 0.0         # -1 左 … 1 右
    gain: float = 1.0
    voice: str = ''          # 音色键名，默认同 key
    template_id: str = ''    # MuseScore 本机乐器模板 ID
    bank: int = 0            # SoundFont/MIDI bank
    drum_map: dict = field(default_factory=dict)
    channel_name: str = ''
    soundfont: str = ''       # music/ 下的专用采样库，相对路径
    transpose: tuple = (0, 0)  # 记谱移调（半音, 音级），如降 B 调单簧管 (-2, -1)、钢片琴 (12, 7)
    amateur: tuple = None      # 业余音域 (低, 高)；缺省同专业音域 lo–hi
    channels: tuple = ()       # 多通道模板的全部通道 ((名称, bank, program), ...)；channel_name 为当前所用
    part_id: int = 0           # MuseScore <Part id="...">，0 表示按顺序编号
    track_name: str = ''       # MuseScore <trackName>，缺省同 name

    def playback_pitch(self, pitch):
        return self.drum_map[pitch] if self.drum_map else pitch


@dataclass
class Bar:
    lines: dict              # 谱表序号 -> 一行记谱（或 '上声部 || 下声部'）；缺省为整小节休止
    dyn: dict = field(default_factory=dict)
    mark: str = ''
    text: dict = field(default_factory=dict)
    tempo: tuple = None      # (每分钟四分音符数, 谱面文字)
    harm: list = None        # 本小节和弦，供演奏层与和声分析使用
    lead: int = None         # 本小节主旋律所在谱表
    tech: dict = field(default_factory=dict)     # 谱表序号 -> 演奏法（如 'pizzicato', 'arco'）
    hairpin: dict = field(default_factory=dict)  # 谱表序号 -> ('cresc' | 'dim', 跨越小节数)


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
    mix: dict = field(default_factory=dict)  # 谱表序号 -> 增益（dB）
    style: dict = None       # 演奏诠释参数（interpret.Style）
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

    def voices(self, bar, staff):
        return split_voices(self.line(bar, staff))

    def tempo_map(self):
        """[(小节序号, 每分钟四分音符数)]"""
        return [(i, b.tempo[0]) for i, b in enumerate(self.bars) if b.tempo]

    def validate(self):
        staff_parts = self.staff_parts()
        n = len(staff_parts)
        for bi, bar in enumerate(self.bars):
            for si in bar.lines:
                if si >= n:
                    raise ValueError(f'{self.slug} 第 {bi + 1} 小节：没有第 {si} 行谱表')
            for si in range(n):
                part, _ = staff_parts[si]
                for vi, vline in enumerate(self.voices(bar, si)):
                    t = line_ticks(vline)
                    if t != self.measure_ticks:
                        raise ValueError(
                            f'{self.slug} 第 {bi + 1} 小节第 {si} 行(voice {vi + 1})：{t} != {self.measure_ticks}: {vline!r}'
                        )
                    if not part.drum_map:
                        for pitches, _, _ in parse_line(vline):
                            if pitches:
                                for p in pitches:
                                    m = midi_of(p)
                                    if not (part.lo <= m <= part.hi):
                                        raise ValueError(
                                            f'{self.slug} 第 {bi + 1} 小节 {part.name} 音高越界：{p} (MIDI {m} 不在 {part.lo}..{part.hi})'
                                        )
        if not self.bars[0].tempo:
            raise ValueError(f'{self.slug}：第一小节须有速度')


# ---------------------------------------------------------------- MSCX 输出

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


def _events_xml(line, kalt, part, state=None):
    if state is None:
        state = {}
    out = []
    parsed = parse_line(line)
    tuplet_rem = 0
    for pitches, dur, flags in parsed:
        dtype, dots, ticks, is_tuplet = DUR[dur]
        if is_tuplet and tuplet_rem == 0:
            base_type = dtype
            out.append(
                '<Tuplet>'
                '<normalNotes>2</normalNotes>'
                '<actualNotes>3</actualNotes>'
                f'<baseNote>{base_type}</baseNote>'
                '<Number><style>tuplet</style><text>3</text></Number>'
                '</Tuplet>'
            )
            tuplet_rem = 3
        dxml = (f'<dots>{dots}</dots>' if dots else '') + f'<durationType>{dtype}</durationType>'
        if pitches is None:
            out.append(f'<Rest>{dxml}</Rest>')
        else:
            arts = ''
            if 'accent' in flags:
                arts += '<Articulation><subtype>articAccentAbove</subtype></Articulation>'
            if 'tenuto' in flags:
                arts += '<Articulation><subtype>articTenutoAbove</subtype></Articulation>'
            if 'staccato' in flags:
                arts += '<Articulation><subtype>articStaccatoAbove</subtype></Articulation>'
            if 'fermata' in flags:
                out.append('<Fermata><subtype>fermataAbove</subtype></Fermata>')
            mark_attrs = ''
            if 'tie' in flags:
                mark_attrs += ' _tie="1"'
            if 'slur_start' in flags:
                mark_attrs += ' _slur_start="1"'
            if 'slur_end' in flags:
                mark_attrs += ' _slur_end="1"'
            notes_str = ''.join(_note_xml(p, state, kalt, part) for p in pitches)
            out.append(f'<Chord{mark_attrs}>{dxml}{arts}{notes_str}</Chord>')
        if is_tuplet:
            tuplet_rem -= 1
            if tuplet_rem == 0:
                out.append('<endTuplet/>')
    return ''.join(out)


def _part_xml(pid, part):
    actual_pid = part.part_id or pid
    tname = part.track_name or part.name
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
    pitch_range = ''
    if not percussion:
        pitch_range = (f'\n        <minPitchP>{part.lo}</minPitchP>'
                       f'\n        <maxPitchP>{part.hi}</maxPitchP>'
                       f'\n        <minPitchA>{a_lo}</minPitchA>'
                       f'\n        <maxPitchA>{a_hi}</maxPitchA>')
    elif part.template_id == 'tablas':
        pitch_range = (f'\n        <minPitchP>{part.lo}</minPitchP>'
                       f'\n        <maxPitchP>{part.hi}</maxPitchP>'
                       f'\n        <minPitchA>{a_lo}</minPitchA>'
                       f'\n        <maxPitchA>{a_hi}</maxPitchA>')
    return (f'    <Part id="{actual_pid}">\n' + ''.join(staves) + f'''      <trackName>{escape(tname)}</trackName>
      <Instrument id="{part.template_id}">
        <longName>{escape(part.name)}</longName>
        <shortName>{escape(part.short)}</shortName>
        <trackName>{escape(tname)}</trackName>{pitch_range}{transpose}{instrument_id}{clef}{drumset}{channels}
        </Instrument>
      </Part>
''')


def _loc_xml(dm, df):
    parts = []
    if dm != 0:
        parts.append(f'<measures>{dm}</measures>')
    if df != 0:
        parts.append(f'<fractions>{df.numerator}/{df.denominator}</fractions>')
    if not parts:
        parts.append('<fractions>0/1</fractions>')
    return '<location>' + ''.join(parts) + '</location>'


def _resolve_spanners(root, piece):
    """将记谱标记中的 _tie、_slur_start/_slur_end 及 Bar.hairpin 展开为标准 MuseScore 4 Spanner。"""
    score = root.find('Score')
    lengths = {'32nd': Fraction(1, 8), '16th': Fraction(1, 4), 'eighth': Fraction(1, 2),
               'quarter': Fraction(1, 1), 'half': Fraction(2, 1), 'whole': Fraction(4, 1)}

    for si, staff in enumerate(score.findall('Staff')):
        measures = staff.findall('Measure')
        max_voices = max(len(m.findall('voice')) for m in measures)
        for vi in range(max_voices):
            chord_seq = []  # [(chord_el, bi, whole_frac, tie_flag, slur_start, slur_end)]
            in_tuplet = False
            for bi, m in enumerate(measures):
                voices = m.findall('voice')
                if vi >= len(voices):
                    continue
                voice = voices[vi]
                pos = Fraction(0, 1)
                for child in list(voice):
                    if child.tag == 'Tuplet':
                        in_tuplet = True
                        continue
                    if child.tag == 'endTuplet':
                        in_tuplet = False
                        continue
                    if child.tag not in ('Chord', 'Rest'):
                        continue
                    dtype = child.findtext('durationType')
                    dots = int(child.findtext('dots', '0'))
                    dur_q = lengths[dtype] * (Fraction(2, 1) - Fraction(1, 2) ** dots)
                    if in_tuplet:
                        dur_q *= Fraction(2, 3)
                    whole_pos = pos / 4
                    if child.tag == 'Chord':
                        tie_f = child.attrib.pop('_tie', None) == '1'
                        ss_f = child.attrib.pop('_slur_start', None) == '1'
                        se_f = child.attrib.pop('_slur_end', None) == '1'
                        chord_seq.append((child, bi, whole_pos, tie_f, ss_f, se_f))
                    pos += dur_q

            # 1) Tie 连接
            for idx, (ch_el, bi, wpos, tie_f, _, _) in enumerate(chord_seq):
                if not tie_f or idx + 1 >= len(chord_seq):
                    continue
                next_ch, nbi, nwpos, _, _, _ = chord_seq[idx + 1]
                dm = nbi - bi
                df = nwpos - wpos
                next_notes = {n.findtext('pitch'): n for n in next_ch.findall('Note')}
                for note in ch_el.findall('Note'):
                    p = note.findtext('pitch')
                    if p in next_notes:
                        target = next_notes[p]
                        sp_next = ET.fromstring(
                            f'<Spanner type="Tie"><Tie/><next>{_loc_xml(dm, df)}</next></Spanner>'
                        )
                        sp_prev = ET.fromstring(
                            f'<Spanner type="Tie"><prev>{_loc_xml(-dm, -df)}</prev></Spanner>'
                        )
                        note.append(sp_next)
                        target.append(sp_prev)

            # 2) Slur 连接
            slur_start_item = None
            for ch_el, bi, wpos, _, ss_f, se_f in chord_seq:
                if ss_f:
                    slur_start_item = (ch_el, bi, wpos)
                if se_f and slur_start_item is not None:
                    sch, sbi, swpos = slur_start_item
                    if sch is not ch_el:
                        dm = bi - sbi
                        df = wpos - swpos
                        sch.append(ET.fromstring(
                            f'<Spanner type="Slur"><Slur/><next>{_loc_xml(dm, df)}</next></Spanner>'
                        ))
                        ch_el.append(ET.fromstring(
                            f'<Spanner type="Slur"><prev>{_loc_xml(-dm, -df)}</prev></Spanner>'
                        ))
                    slur_start_item = None

        # 3) HairPin（渐强/渐弱）
        for bi, bar in enumerate(piece.bars):
            if si not in bar.hairpin:
                continue
            kind, span_bars = bar.hairpin[si]
            subtype = 0 if kind == 'cresc' else 1
            velo_change = 18 if kind == 'cresc' else -22
            start_voice = measures[bi].find('voice')
            end_bi = min(bi + span_bars, len(measures) - 1)
            end_voice = measures[end_bi].find('voice')
            start_anchor = next((c for c in start_voice if c.tag in ('Chord', 'Rest')), None)
            end_anchor = next((c for c in end_voice if c.tag in ('Chord', 'Rest')), None)
            if start_anchor is not None and end_anchor is not None and end_bi > bi:
                dm = end_bi - bi
                props = (f'<subtype>{subtype}</subtype>'
                         f'<veloChange>{velo_change}</veloChange>'
                         '<singleNoteDynamics>0</singleNoteDynamics>'
                         '<voiceAssignment>allInStaff</voiceAssignment>')
                sp_start = ET.fromstring(
                    f'<Spanner type="HairPin"><HairPin>{props}</HairPin>'
                    f'<next><location><measures>{dm}</measures><fractions>0/1</fractions></location></next></Spanner>'
                )
                sp_end = ET.fromstring(
                    f'<Spanner type="HairPin"><prev><location><measures>{-dm}</measures><fractions>0/1</fractions></location></prev></Spanner>'
                )
                start_voice.insert(list(start_voice).index(start_anchor), sp_start)
                end_voice.insert(list(end_voice).index(end_anchor), sp_end)


def build_mscx(piece, preserved_parts_xml=None):
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
                        '        <Text><style>composer</style><text>Dual Ontology Studio</text></Text>\n'
                        '        </VBox>\n')
        for bi, bar in enumerate(piece.bars):
            voices = piece.voices(bar, si)
            v_xmls = []
            for vi, vline in enumerate(voices):
                head = ''
                if vi == 0:
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
                        head += (f'<Dynamic><subtype>{d}</subtype><velocity>{VELOCITY[d]}</velocity>'
                                 '<voiceAssignment>allInStaff</voiceAssignment></Dynamic>')
                    if si in bar.tech:
                        label = {'pizzicato': 'pizz.', 'arco': 'arco', 'tremolo': 'trem.'}[bar.tech[si]]
                        head += (f'<PlayTechAnnotation><playTechType>{bar.tech[si]}</playTechType>'
                                 f'<text>{label}</text></PlayTechAnnotation>')
                body = _events_xml(vline, kalt, part)
                tail = '<BarLine><subtype>end</subtype></BarLine>' if (vi == 0 and bi == len(piece.bars) - 1) else ''
                v_xmls.append(f'<voice>{head}{body}{tail}</voice>')
            rows.append('      <Measure>' + ''.join(v_xmls) + '</Measure>\n')
        rows.append('      </Staff>\n')
        staves_xml.append(''.join(rows))

    if preserved_parts_xml is not None:
        parts_xml = preserved_parts_xml
    else:
        parts_xml = ''.join(_part_xml(i + 1, p) for i, p in enumerate(piece.parts))

    mscx = ('<?xml version="1.0" encoding="UTF-8"?>\n'
            '<museScore version="4.70">\n'
            '  <programVersion>4.7.5</programVersion>\n'
            '  <Score>\n'
            '    <Division>480</Division>\n'
            '    <open>1</open>\n'
            f'    <metaTag name="workTitle">{escape(piece.title)}</metaTag>\n'
            f'    <metaTag name="subtitle">{escape(piece.subtitle)}</metaTag>\n'
            '    <metaTag name="composer">Dual Ontology Studio</metaTag>\n'
            '    <metaTag name="creationDate">2026-10-16</metaTag>\n'
            + parts_xml + ''.join(staves_xml) +
            '    </Score>\n'
            '  </museScore>\n')
    root = ET.fromstring(mscx)
    _resolve_spanners(root, piece)
    return apply_expression_playback(root, piece)


def apply_expression_playback(root, piece):
    """补充散板引子（拉格阿拉普）的平滑呼吸速度曲线。"""
    score = root.find('Score')
    lengths = {'32nd': .125, '16th': .25, 'eighth': .5, 'quarter': 1., 'half': 2., 'whole': 4.}
    if piece.slug in ('day_evening', 'day_afternoon'):
        from interpret import Style, tempo_curve
        qgrid, tgrid = tempo_curve(piece, Style(**(piece.style or {})))
        count = 8 if piece.slug == 'day_evening' else 4
        measures = score.find('Staff').findall('Measure')
        for bi, measure in enumerate(measures[:count]):
            voice = measure.find('voice')
            beat = 0.
            in_tuplet = False
            for child in list(voice):
                if child.tag == 'Tuplet':
                    in_tuplet = True
                    continue
                if child.tag == 'endTuplet':
                    in_tuplet = False
                    continue
                if child.tag not in ('Chord', 'Rest'):
                    continue
                q = bi * 4 + beat
                k = min(int(round(q * 96)), len(tgrid) - 2)
                bpm = 60 / ((tgrid[k + 1] - tgrid[k]) * 96)
                tempo = ET.fromstring(
                    f'<Tempo><visible>0</visible><tempo>{bpm / 60:.8f}</tempo><followText>0</followText><text>♩ = {bpm:.3f}</text></Tempo>'
                )
                voice.insert(list(voice).index(child), tempo)
                dots = int(child.findtext('dots', '0'))
                step_q = lengths[child.findtext('durationType')] * (2 - .5 ** dots)
                if in_tuplet:
                    step_q *= 2.0 / 3.0
                beat += step_q
        restore = piece.bars[count].tempo[0] if piece.bars[count].tempo else piece.bars[0].tempo[0]
        if not piece.bars[count].tempo:
            measures[count].find('voice').insert(
                0,
                ET.fromstring(
                    f'<Tempo><visible>0</visible><tempo>{restore / 60:.8f}</tempo><followText>0</followText><text>♩ = {restore}</text></Tempo>'
                ),
            )
    return '<?xml version="1.0" encoding="UTF-8"?>\n' + ET.tostring(root, encoding='unicode')


def soundfont_input(part):
    """MuseScore 4.7 FluidResolver 的指定预设资源格式。"""
    if part.soundfont:
        name = os.path.splitext(os.path.basename(part.soundfont))[0]
        return {
            'resourceMeta': {
                'id': f'{name}\\{part.bank}\\{part.program}',
                'type': 'fluid_soundfont',
                'vendor': 'Fluid',
                'hasNativeEditorSupport': False,
                'attributes': {
                    'soundFontName': name,
                    'playbackSetupData': 'last.last.last',
                    'presetName': name,
                    'presetBank': str(part.bank),
                    'presetProgram': str(part.program),
                },
            },
            'unitConfiguration': {},
        }
    return {
        'resourceMeta': {
            'id': 'MS Basic',
            'type': 'fluid_soundfont',
            'vendor': 'Fluid',
            'hasNativeEditorSupport': False,
            'attributes': {
                'playbackSetupData': 'last.last.last',
                'soundFontName': 'MS Basic',
            },
        },
        'unitConfiguration': {},
    }


def default_audiosettings(piece):
    tracks = []
    for i, part in enumerate(piece.parts):
        pid = str(part.part_id or (i + 1))
        tracks.append({
            'partId': pid,
            'instrumentId': part.template_id,
            'in': soundfont_input(part),
            'out': {
                'auxSends': [
                    {'active': True, 'signalAmount': 0.3},
                    {'active': True, 'signalAmount': 0.3},
                ],
                'balance': 0,
                'fxChain': {},
                'volumeDb': 0,
            },
            'soloMuteState': {'mute': False, 'solo': False},
        })
    tracks.append({
        'partId': '999',
        'instrumentId': 'metronome',
        'in': soundfont_input(Part('metronome', 'Metronome', 'Met.', '', 0)),
        'out': {'balance': 0, 'fxChain': {}, 'volumeDb': 0},
    })
    return {
        'activeSoundProfile': 'MuseSounds',
        'aux': [
            {
                'out': {
                    'balance': 0,
                    'fxChain': {
                        '0': {
                            'active': True,
                            'chainOrder': 0,
                            'resourceMeta': {
                                'attributes': {},
                                'hasNativeEditorSupport': True,
                                'id': 'Muse Reverb',
                                'type': 'muse_plugin',
                                'vendor': 'Muse',
                            },
                            'unitConfiguration': {},
                        }
                    },
                    'volumeDb': 0,
                }
            },
            {'out': {'balance': 0, 'fxChain': {}, 'volumeDb': 0}},
        ],
        'master': {'balance': 0, 'fxChain': {}, 'volumeDb': 0},
        'tracks': tracks,
    }


def update_audiosettings(piece, raw_bytes=None):
    """保持曲谱工程音频设置自然中立（不施加额外混音增益控制，由曲谱内强弱记号完全控制动态）。"""
    if raw_bytes is None:
        cfg = default_audiosettings(piece)
    else:
        try:
            cfg = json.loads(raw_bytes.decode('utf-8'))
        except Exception:
            cfg = default_audiosettings(piece)
    for track in cfg.get('tracks', []):
        track.setdefault('out', {})['volumeDb'] = 0.0
    cfg.setdefault('master', {})['volumeDb'] = 0.0
    return json.dumps(cfg, ensure_ascii=False, indent=2).encode('utf-8')


def _extract_existing_mscz_assets(path):
    """读取现有 .mscz 中的 <Part> 定义与 audiosettings.json 等工程设置，严格保持乐器名称与音色绑定不变。"""
    if not os.path.exists(path):
        return None, {}
    extra_files = {}
    preserved_parts = None
    with zipfile.ZipFile(path, 'r') as z:
        for name in z.namelist():
            if name.endswith('.mscx'):
                mscx_text = z.read(name).decode('utf-8')
                parts_matches = re.findall(r'(    <Part id="\d+">.*?</Part>\n)', mscx_text, re.S)
                if parts_matches:
                    preserved_parts = ''.join(parts_matches)
            elif name not in ('META-INF/container.xml',):
                extra_files[name] = z.read(name)
    return preserved_parts, extra_files


def write_mscz(piece, path):
    base = os.path.splitext(os.path.basename(path))[0]
    preserved_parts, extra_files = _extract_existing_mscz_assets(path)
    style = extra_files.get('score_style.mss')
    if style is None:
        with open(os.path.join(HERE, 'score_style.mss'), 'rb') as f:
            style = f.read()
    view = extra_files.get('viewsettings.json')
    if view is None:
        with open(os.path.join(HERE, 'viewsettings.json'), 'rb') as f:
            view = f.read()
    audiosettings_bytes = extra_files.get('audiosettings.json')
    audiosettings = update_audiosettings(piece, audiosettings_bytes)
    automation = extra_files.get('automation.json', b'[]\n')
    thumbnail = extra_files.get('Thumbnails/thumbnail.png')

    container = (
        '<?xml version="1.0" encoding="UTF-8"?>\n<container>\n  <rootfiles>\n'
        '    <rootfile full-path="score_style.mss"/>\n'
        f'    <rootfile full-path="{base}.mscx"/>\n'
        '    <rootfile full-path="viewsettings.json"/>\n'
        '    <rootfile full-path="automation.json"/>\n'
        '    <rootfile full-path="audiosettings.json"/>\n'
        '    </rootfiles>\n  </container>\n'
    )
    mscx_content = build_mscx(piece, preserved_parts_xml=preserved_parts)
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as z:
        z.writestr('score_style.mss', style)
        z.writestr(f'{base}.mscx', mscx_content)
        if thumbnail is not None:
            z.writestr('Thumbnails/thumbnail.png', thumbnail)
        z.writestr('automation.json', automation)
        z.writestr('audiosettings.json', audiosettings)
        z.writestr('viewsettings.json', view)
        z.writestr('META-INF/container.xml', container)


# ---------------------------------------------------------------- 曲谱时长计算（从 .mscz / Piece 直接求精确循环秒数）

def mscz_musical_duration(path):
    """直接从 .mscz 内的 .mscx 解析拍号与速度事件（含散板逐音隐形速度），计算精确的乐谱小节总时长（秒）。"""
    dur_q = {'32nd': 0.125, '16th': 0.25, 'eighth': 0.5, 'quarter': 1.0, 'half': 2.0, 'whole': 4.0}
    with zipfile.ZipFile(path) as z:
        mscx = z.read([n for n in z.namelist() if n.endswith('.mscx')][0]).decode('utf-8')
    root = ET.fromstring(mscx)
    staff = root.find('.//Score/Staff')
    tempo = 2.0
    sig_n, sig_d = 4, 4
    total_sec = 0.0
    has_fermata_last = False
    measures = staff.findall('Measure')
    for bi, m in enumerate(measures):
        v = m.find('voice')
        ts = v.find('TimeSig')
        if ts is not None:
            sig_n, sig_d = int(ts.findtext('sigN')), int(ts.findtext('sigD'))
        bar_q = sig_n * 4.0 / sig_d
        used_q = 0.0
        in_tuplet = False
        for ch in v:
            if ch.tag == 'Tempo':
                tempo = float(ch.findtext('tempo'))
            elif ch.tag == 'Tuplet':
                in_tuplet = True
            elif ch.tag == 'endTuplet':
                in_tuplet = False
            elif ch.tag == 'Fermata' and bi == len(measures) - 1:
                has_fermata_last = True
            elif ch.tag in ('Chord', 'Rest'):
                dt = ch.findtext('durationType')
                dots = int(ch.findtext('dots', '0'))
                q = bar_q if dt == 'measure' else dur_q[dt] * (2.0 - 0.5 ** dots)
                if in_tuplet:
                    q *= 2.0 / 3.0
                total_sec += q / tempo
                used_q += q
        if used_q < bar_q - 1e-6:
            total_sec += (bar_q - used_q) / tempo
    return total_sec, has_fermata_last


# ---------------------------------------------------------------- MIDI 备用输出

def write_midi(piece, path):
    num, den = piece.time
    dd = {4: 2, 8: 3}[den]
    conductor = [(0, 0, bytes([0xFF, 0x58, 4, num, dd, 24, 8]))]
    for bi, bpm in piece.tempo_map():
        us = int(60_000_000 / bpm)
        conductor.append((bi * piece.measure_ticks, 0, b'\xff\x51\x03' + us.to_bytes(3, 'big')))
    tracks = [_track_bytes(conductor)]
    channel = 0
    for part in piece.parts:
        ch = 9 if part.drum_map else channel
        evs = [(0, 0, bytes([0xB0 | ch, 0, part.bank >> 7])),
               (0, 0, bytes([0xB0 | ch, 32, part.bank & 127])),
               (0, 0, bytes([0xC0 | ch, part.program]))]
        name = (part.track_name or part.name).encode('utf-8')
        evs.append((0, 0, b'\xff\x03' + _vlq(len(name)) + name))
        staff_ids = [i for i, (p, _) in enumerate(piece.staff_parts()) if p is part]
        vel = {}
        for bi, bar in enumerate(piece.bars):
            for si in staff_ids:
                if si in bar.dyn:
                    vel[si] = VELOCITY[bar.dyn[si]]
                for vline in piece.voices(bar, si):
                    t = bi * piece.measure_ticks
                    for pitches, dur, flags in parse_line(vline):
                        ticks = DUR[dur][2]
                        if pitches:
                            length = int(ticks * (1.5 if 'fermata' in flags else (0.5 if 'staccato' in flags else 0.96)))
                            v = min(127, vel.get(si, 64) + (14 if 'accent' in flags else 0))
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
            voices = re.findall(r'<voice>(.*?)</voice>', m, re.S)
            bar_voices = []
            pattern = r'<(Chord|Rest|Dynamic|StaffText|PlayTechAnnotation|RehearsalMark|Tempo)>(.*?)</\1>'
            for v in voices:
                events = []
                for el in re.finditer(pattern, v, re.S):
                    kind, b = el.groups()
                    if kind in ('Chord', 'Rest'):
                        d = re.search(r'<durationType>(\w+)', b)
                        dur = (d.group(1) if d else 'measure') + ('.' if '<dots>' in b else '')
                        pitches = tuple(sorted(int(val) for val in re.findall(r'<pitch>(\d+)', b)))
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
                bar_voices.append(events)
            bars.append(bar_voices)
        staves.append(bars)
    return parts, staves


def compare_with_score(piece, path):
    """返回曲谱与 cues.py 的差异列表（空表示一致）。"""
    with zipfile.ZipFile(path) as z:
        stored = z.read([n for n in z.namelist() if n.endswith('.mscx')][0]).decode('utf-8')
    preserved_parts, _ = _extract_existing_mscz_assets(path)
    (pa, sa), (pb, sb) = score_content(stored), score_content(build_mscx(piece, preserved_parts_xml=preserved_parts))
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
        if len(x) != len(y):
            diffs.append(f'谱表 {si + 1} 小节数：曲谱 {len(x)}，源数据 {len(y)}')
        for bi, (u, v) in enumerate(zip(x, y)):
            if u != v:
                diffs.append(f'谱表 {si + 1} 第 {bi + 1} 小节：曲谱 {u}，源数据 {v}')
    return diffs
