"""《安眠者有福》原声的记谱与导出：MuseScore 4 曲谱（.mscz）与 MIDI（.mid）。

记谱写法（一个小节一行，空格分隔）：
    'D5:e C#5:e B4:q F#5:q> E5:e F#5:e'   音名:时值，'+' 连成和弦，'r' 为休止
时值：s 十六分、e 八分、e. 附点八分、q 四分、q. 附点四分、h 二分、h. 附点二分、w 全音符。
后缀：> 重音，- 保持音，^ 延长记号。不用连音线与三连音，长音都写在小节之内。
"""
import os
import struct
import zipfile
from dataclasses import dataclass, field
from xml.sax.saxutils import escape

HERE = os.path.dirname(os.path.abspath(__file__))
TEMPLATE = os.path.join(HERE, 'The Unfallen Duet.mscz')

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


@dataclass
class Bar:
    lines: dict              # 谱表序号 -> 一行记谱；缺省为整小节休止
    dyn: dict = field(default_factory=dict)
    mark: str = ''
    text: dict = field(default_factory=dict)
    tempo: tuple = None      # (每分钟四分音符数, 谱面文字)
    harm: list = None        # 本小节和弦（等分），供演奏层换踏板与强调紧张和弦；不写入谱面
    lead: int = None         # 本小节旋律所在谱表，供演奏层突出旋律


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

def _note_xml(name, state, kalt):
    step, alter, octave, midi = parse_pitch(name)
    tpc = STEP_TPC[step] + 7 * alter
    expected = state.get((step, octave), kalt[step])
    acc = ''
    if alter != expected:
        acc = f'<Accidental><subtype>{ACC[alter]}</subtype></Accidental>'
        state[(step, octave)] = alter
    return f'<Note><pitch>{midi}</pitch><tpc>{tpc}</tpc>{acc}</Note>'


def _events_xml(line, kalt):
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
        out.append(f'<Chord>{dxml}{arts}' + ''.join(_note_xml(p, state, kalt) for p in pitches) + '</Chord>')
    return ''.join(out)


def _part_xml(pid, part):
    staves = []
    for i in range(part.staves):
        extra = ''
        if part.staves == 2 and i == 0:
            extra = '\n        <bracket type="1" span="2" col="0" visible="1" />\n        <barLineSpan>1</barLineSpan>'
        if (part.staves == 2 and i == 1) or (part.staves == 1 and part.bass_clef):
            extra += '\n        <defaultClef>F</defaultClef>'
        staves.append(f'''      <Staff>
        <StaffType group="pitched">
          <name>stdNormal</name>
          </StaffType>{extra}
        </Staff>
''')
    clef = ''
    if part.staves == 2:
        clef = '\n        <clef staff="2">F</clef>'
    elif part.bass_clef:
        clef = '\n        <clef>F</clef>'
    return (f'    <Part id="{pid}">\n' + ''.join(staves) + f'''      <trackName>{part.name}</trackName>
      <Instrument id="{part.key}">
        <longName>{part.name}</longName>
        <shortName>{part.short}</shortName>
        <trackName>{part.name}</trackName>
        <minPitchP>{part.lo}</minPitchP>
        <maxPitchP>{part.hi}</maxPitchP>
        <minPitchA>{part.lo}</minPitchA>
        <maxPitchA>{part.hi}</maxPitchA>
        <instrumentId>{part.instrument_id}</instrumentId>{clef}
        <Channel>
          <program value="{part.program}" />
          <synti>Fluid</synti>
          </Channel>
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
                head += (f'<KeySig><concertKey>{piece.key}</concertKey></KeySig>'
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
            body = _events_xml(piece.line(bar, si), kalt)
            tail = '<BarLine><subtype>end</subtype></BarLine>' if bi == len(piece.bars) - 1 else ''
            rows.append(f'      <Measure><voice>{head}{body}{tail}</voice></Measure>\n')
        rows.append('      </Staff>\n')
        staves_xml.append(''.join(rows))
    parts_xml = ''.join(_part_xml(i + 1, p) for i, p in enumerate(piece.parts))
    return ('<?xml version="1.0" encoding="UTF-8"?>\n'
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


def write_mscz(piece, path):
    base = os.path.splitext(os.path.basename(path))[0]
    with zipfile.ZipFile(TEMPLATE) as z:
        style = z.read('score_style.mss')
        view = z.read('viewsettings.json')
    container = ('<?xml version="1.0" encoding="UTF-8"?>\n<container>\n  <rootfiles>\n'
                 '    <rootfile full-path="score_style.mss"/>\n'
                 f'    <rootfile full-path="{base}.mscx"/>\n'
                 '    <rootfile full-path="viewsettings.json"/>\n'
                 '    </rootfiles>\n  </container>\n')
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as z:
        z.writestr(f'{base}.mscx', build_mscx(piece))
        z.writestr('score_style.mss', style)
        z.writestr('viewsettings.json', view)
        z.writestr('META-INF/container.xml', container)


# ---------------------------------------------------------------- 演奏事件

@dataclass
class Note:
    staff: int
    start: float     # 秒
    length: float    # 秒（记谱时值）
    pitch: int
    velocity: int
    accent: bool
    fermata: bool


def performance(piece):
    """展开为 [Note] 与总时长（秒）；速度可在小节处变化。"""
    piece.validate()
    tempo = {i: bpm for i, bpm in piece.tempo_map()}
    notes, t0, bpm = [], 0.0, piece.bars[0].tempo[0]
    vel = {}
    for bi, bar in enumerate(piece.bars):
        bpm = tempo.get(bi, bpm)
        sec_per_tick = 60.0 / bpm / 480
        for si in range(len(piece.staff_parts())):
            if si in bar.dyn:
                vel[si] = VELOCITY[bar.dyn[si]]
            t = t0
            for pitches, dur, flags in parse_line(piece.line(bar, si)):
                length = DUR[dur][2] * sec_per_tick
                if pitches:
                    v = min(127, vel.get(si, 64) + (14 if 'accent' in flags else 0))
                    for p in pitches:
                        notes.append(Note(si, t, length, midi_of(p), v, 'accent' in flags, 'fermata' in flags))
                t += length
        t0 += piece.measure_ticks * sec_per_tick
    return notes, t0


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
        ch = channel
        evs = [(0, 0, bytes([0xC0 | ch, part.program]))]
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
                        v = min(127, vel.get(si, 64) + (14 if 'accent' in flags else 0))
                        for p in pitches:
                            m = midi_of(p)
                            evs.append((t, 1, bytes([0x90 | ch, m, v])))
                            evs.append((t + length, 0, bytes([0x80 | ch, m, 0])))
                    t += ticks
        tracks.append(_track_bytes(evs))
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
