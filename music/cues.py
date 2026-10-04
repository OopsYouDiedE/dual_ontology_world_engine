"""《安眠者有福》原声曲目（2026-10-04 重写）。

核心动机“门句”取自第一章的情感核心：格雷厄姆喊的是“不要开门”，苏泠只听见后半截
“开门”。一小节四拍，前两拍是两个下行的弱音（不要），后两拍是上跳五度的呼喊（开门）：

    D5 C#5 B4 | F#5> E5 F#5          b 小调；后半截停在属音 F#，像一个问句

- 只出现后半截（F#5 E5 F#5）时，是门外的“呼喊”，听者补不出前半句；
- 完整出现时，是真相；尾声里后半截第一次落回主音，问句得到回答。
- 恶魔的和声是“字面为真”的阻碍终止：旋律给出正确的落音，脚下却换成了别的和弦。

曲目：
    main_theme      主题《安眠者有福》   标题、章末
    door_side       第一章《门的这一侧》 现场调查（循环）
    confrontation   对质                 始业之答、第二章对质与名录库之议（循环）
    day             第二章·日间           白天的时段（循环）
    night           第二章·夜             夜间推演、第一章“歇息”结局（循环）
"""
from score_io import Bar, Part, Piece, octaves, shift

# ---------------------------------------------------------------- 和弦表
# 名字 -> (钢琴左手四音（低到高）, 风琴三四音, 低音)
CH = {
    'Bm': (['B1', 'F#2', 'B2', 'D3'], 'F#3+B3+D4', 'B1'),
    'Bm/A': (['A1', 'F#2', 'B2', 'D3'], 'F#3+B3+D4', 'A1'),
    'Bm/A#': (['A#1', 'F#2', 'B2', 'D3'], 'F#3+B3+D4', 'A#1'),
    'Bm9': (['B1', 'F#2', 'C#3', 'D3'], 'D3+A3+C#4', 'B1'),
    'B': (['B1', 'F#2', 'B2', 'D#3'], 'F#3+B3+D#4', 'B1'),
    'E/B': (['B1', 'E2', 'G#2', 'B2'], 'G#3+B3+E4', 'B1'),
    'Gmaj7': (['G1', 'D2', 'B2', 'F#3'], 'F#3+B3+D4', 'G1'),
    'Gmaj9': (['G1', 'D2', 'A2', 'B2'], 'F#3+A3+B3', 'G1'),
    'G': (['G1', 'D2', 'G2', 'B2'], 'G3+B3+D4', 'G1'),
    'Em': (['E2', 'B2', 'E3', 'G3'], 'G3+B3+E4', 'E2'),
    'Em7': (['E2', 'B2', 'D3', 'G3'], 'G3+B3+D4', 'E2'),
    'Em9': (['E2', 'B2', 'F#3', 'G3'], 'D3+F#3+G3', 'E2'),
    'Em/G': (['G1', 'E2', 'B2', 'E3'], 'E3+G3+B3', 'G1'),
    'E/G#': (['G#1', 'E2', 'B2', 'E3'], 'E3+G#3+B3', 'G#1'),
    'A': (['A1', 'E2', 'A2', 'C#3'], 'E3+A3+C#4', 'A1'),
    'A7': (['A1', 'E2', 'G2', 'C#3'], 'E3+G3+C#4', 'A1'),
    'A7sus4': (['A1', 'E2', 'D3', 'G3'], 'G3+A3+D4', 'A1'),
    'A6': (['A1', 'E2', 'F#2', 'C#3'], 'E3+F#3+C#4', 'A1'),
    'D': (['D2', 'A2', 'D3', 'F#3'], 'F#3+A3+D4', 'D2'),
    'D/F#': (['F#1', 'D2', 'A2', 'D3'], 'F#3+A3+D4', 'F#1'),
    'Dmaj7/F#': (['F#1', 'D2', 'A2', 'C#3'], 'A3+C#4+D4', 'F#1'),
    'F#': (['F#1', 'C#2', 'F#2', 'A#2'], 'F#3+A#3+C#4', 'F#1'),
    'F#7': (['F#1', 'C#2', 'E2', 'A#2'], 'E3+A#3+C#4', 'F#1'),
    'F#sus4': (['F#1', 'C#2', 'F#2', 'B2'], 'F#3+B3+C#4', 'F#1'),
    'F#m': (['F#1', 'C#2', 'F#2', 'A2'], 'F#3+A3+C#4', 'F#1'),
    'C#o7': (['C#2', 'G2', 'B2', 'E3'], 'G3+B3+E4', 'C#2'),
    'Cmaj7#11': (['C2', 'G2', 'B2', 'E3'], 'F#3+B3+E4', 'C2'),
    'Gm6': (['G1', 'D2', 'E2', 'Bb2'], 'E3+G3+Bb3', 'G1'),
    'Gm': (['G1', 'D2', 'G2', 'Bb2'], 'G3+Bb3+D4', 'G1'),
    'A/C#': (['C#2', 'A2', 'C#3', 'E3'], 'E3+A3+C#4', 'C#2'),
}

LULLABY = [0, 1, 2, 3, 2, 1, 2, 1]   # 摇篮式：低-五-八-十-八-五
PENDULUM = [0, 1, 2, 1, 3, 1, 2, 1]  # 钟摆：五度是支点，像配重来回


def _split(chords, beats=4):
    """每小节 1/2/4 个和弦 -> [(和弦, 拍数)]"""
    per = beats / len(chords)
    return [(c, per) for c in chords]


def _dur(beats):
    return {4: 'w', 3: 'h.', 2: 'h', 1.5: 'q.', 1: 'q', 0.5: 'e'}[beats]


def arp(chords, pattern=LULLABY, up=0):
    """左手八分音符分解和弦（4/4）。"""
    out = []
    per = 8 // len(chords)
    for c in chords:
        notes = CH[c][0]
        seq = pattern if per == 8 else ([0, 1, 2, 3] if per == 4 else [0, 2])
        for i in seq[:per]:
            out.append(notes[i])
    line = ' '.join(f'{n}:e' for n in out)
    return shift(line, up) if up else line


def pad(chords, beats=4, up=0):
    line = ' '.join(f'{CH[c][1]}:{_dur(b)}' for c, b in _split(chords, beats))
    return shift(line, up) if up else line


def bass(chords, beats=4, up=1):
    """低音弦乐：和弦根音，相同根音合并为一个长音。"""
    merged = []
    for c, b in _split(chords, beats):
        root = CH[c][2]
        if merged and merged[-1][0] == root:
            merged[-1][1] += b
        else:
            merged.append([root, b])
    return shift(' '.join(f'{r}:{_dur(b)}' for r, b in merged), up)


def block(chords, beats=4):
    """右手和弦（第二、四拍）——用于旋律交给别的乐器时。"""
    out = []
    for c, b in _split(chords, beats):
        upper = shift(CH[c][1] + ':q', 1).split(':')[0]
        if b == 4:
            out.append(f'r:q {upper}:q r:q {upper}:q')
        elif b == 2:
            out.append(f'r:q {upper}:q')
        else:
            out.append(f'{upper}:q')
    return ' '.join(out)


def tempo_text(bpm, words, unit='metNoteQuarterUp', dotted=False):
    dot = '<sym>space</sym><sym>metAugmentationDot</sym>' if dotted else ''
    return f'<b>{words}</b> <sym>{unit}</sym>{dot}<font face="Edwin"/> = {bpm}'


MOTIF = 'D5:e C#5:e B4:q F#5:q> E5:e F#5:e'   # 门句：不要 · 开门
CALL = 'r:h F#5:q> E5:e F#5:e'                 # 只听得见的后半截
ECHO = 'F#4:q E4:e F#4:e r:h'                  # 呼喊在管道里的回声

# 主题 A 段：八小节，前句半终止于 F#，后句完满终止
THEME_A = [
    (MOTIF, ['Bm', 'Gmaj7']),
    ('G5:q. F#5:e E5:q D5:q', ['Em7', 'A7sus4']),
    ('C#5:q D5:e E5:e F#5:q B4:q', ['A', 'Bm']),
    ('C#5:h A#4:q r:q', ['F#', 'F#7']),
    (MOTIF, ['Bm', 'Gmaj7']),
    ('A5:q. G5:e F#5:q E5:q', ['D', 'Em7']),
    ('D5:q C#5:e B4:e C#5:q A#4:q', ['G', 'C#o7', 'F#', 'F#7']),
    ('B4:w', ['Bm']),
]


def main_theme():
    CEL, BEL, ORG, RH, LH, STR = range(6)
    parts = [
        Part('celesta', 'Celesta', 'Cel.', 'keyboard.celesta', 8, 60, 108, pan=0.35),
        Part('bell', 'Tubular Bells', 'T.B.', 'metal.bells.tubular', 14, 60, 77, pan=-0.3, gain=0.8),
        Part('organ', 'Reed Organ', 'Org.', 'keyboard.harmonium', 20, 36, 96, pan=-0.2),
        Part('piano', 'Piano', 'Pno.', 'keyboard.piano', 0, staves=2, pan=0.1),
        Part('strings', 'Low Strings', 'Vc.', 'strings.cello', 42, 36, 76, bass_clef=True, pan=-0.1, gain=0.9),
    ]
    bars = []
    # 引子 · 门外：只有后半截与它的回声
    bars.append(Bar({CEL: CALL, BEL: 'B4:w', STR: 'B2:w'},
                    dyn={CEL: 'p', BEL: 'pp', STR: 'pp', ORG: 'pp', RH: 'p', LH: 'p'},
                    mark='引子 · 门外', text={CEL: 'lontano, come da un tubo'},
                    tempo=(63, tempo_text(63, 'Lento, cullando'))))
    bars.append(Bar({CEL: ECHO, STR: 'B2:w'}))
    bars.append(Bar({CEL: CALL, ORG: 'D4+F#4+B4:w', STR: 'G2:w'}))
    bars.append(Bar({CEL: ECHO, ORG: 'C#4+F#4+A#4:w', STR: 'F#2:w'}))
    # A · 安眠：钢琴独自说出整句
    for i, (mel, ch) in enumerate(THEME_A):
        kw = dict(dyn={RH: 'p', LH: 'pp', STR: 'pp'}, mark='A · 安眠', text={RH: 'semplice, cantabile'}) if i == 0 else {}
        bars.append(Bar({RH: mel, LH: arp(ch), STR: bass(ch)}, **kw))
    # A2 · 誓约：风琴接过旋律，钢片琴在半终止处应答那半截呼喊
    a2 = list(THEME_A)
    a2[6] = ('D5:q. C#5:e B4:q C#5:q', ['G', 'A7'])
    a2[7] = ('D5:w', ['D'])
    for i, (mel, ch) in enumerate(a2):
        lines = {ORG: mel, RH: block(ch), LH: arp(ch), STR: bass(ch)}
        if i == 3:
            lines[CEL] = 'r:h F#6:q E6:e F#6:e'
        kw = dict(dyn={ORG: 'mp', RH: 'pp', LH: 'p', STR: 'p', CEL: 'pp'}, mark='A2 · 誓约',
                  text={ORG: 'legato, come un inno'}) if i == 0 else {}
        bars.append(Bar(lines, **kw))
    # B · 旧日的灯：D 大调，钢片琴高八度叠奏
    theme_b = [
        ('F#5:q. G5:e A5:q F#5:q', ['D', 'Bm']),
        ('E5:q. F#5:e G5:h', ['A', 'Em']),
        ('F#5:q E5:e D5:e E5:q B4:q', ['Bm', 'Em']),
        ('C#5:h. r:q', ['A']),
        ('F#5:q. G5:e A5:q D6:q', ['D', 'Bm']),
        ('C#6:q. B5:e A5:q G5:q', ['F#m', 'D']),
        ('F#5:q B5:q A5:e G5:e E5:q', ['Bm', 'G', 'A', 'A7']),
        ('F#5:h E5:e D5:e C#5:q', ['F#', 'F#7']),
    ]
    for i, (mel, ch) in enumerate(theme_b):
        lines = {RH: mel, CEL: shift(mel, 1), ORG: pad(ch), LH: arp(ch), STR: bass(ch)}
        kw = {}
        if i == 0:
            lines[BEL] = 'D5:w'
            kw = dict(dyn={RH: 'mf', CEL: 'pp', ORG: 'p', LH: 'mp', STR: 'mp', BEL: 'pp'},
                      mark='B · 旧日的灯', text={RH: 'con calore'})
        bars.append(Bar(lines, **kw))
    # A' · 有福：全奏，钢琴八度，后句攀上高点再落回
    a3 = list(THEME_A)
    a3[5] = ('B5:h A5:q G5:q', ['G', 'A7'])
    a3[6] = ('F#5:q E5:e D5:e C#5:q A#4:q', ['Bm', 'C#o7', 'F#', 'F#7'])
    for i, (mel, ch) in enumerate(a3):
        lines = {RH: octaves(mel, 1) if mel != 'B4:w' else 'B4+B5:w', ORG: pad(ch), LH: arp(ch), STR: bass(ch)}
        kw = {}
        if i == 0:
            lines[BEL] = 'B4:w'
            kw = dict(dyn={RH: 'f', ORG: 'mf', LH: 'mf', STR: 'mf', BEL: 'p', CEL: 'p'},
                      mark="A' · 有福", text={RH: 'largamente'})
        if i == 3:
            lines[CEL] = 'r:h F#6:q E6:e F#6:e'
        if i == 4:
            kw = dict(dyn={RH: 'ff', ORG: 'f', STR: 'f'})
        if i == 6:
            kw = dict(dyn={RH: 'mf', ORG: 'mp', LH: 'mp', STR: 'mp'}, text={RH: 'dim.'})
        bars.append(Bar(lines, **kw))
    # 尾声 · 问句得到回答：后半截第一次落回主音（B 大调）
    bars.append(Bar({CEL: CALL, ORG: 'F#3+B3+D#4:w', STR: 'B2:w', LH: 'B1+F#2:w'},
                    dyn={CEL: 'p', ORG: 'pp', STR: 'pp', LH: 'pp'}, mark='尾声 · 回答', text={CEL: 'dolcissimo'}))
    bars.append(Bar({CEL: 'E5:h D#5:h', ORG: 'G#3+B3+E4:h F#3+B3+D#4:h', STR: 'B2:w', LH: 'B1+E2:h B1+F#2:h'}))
    bars.append(Bar({CEL: CALL, ORG: 'F#3+B3+D4:w', STR: 'G2:w', LH: 'G1+D2:w'}, dyn={CEL: 'pp'}))
    bars.append(Bar({CEL: 'D#5:h B4:h^', ORG: 'F#3+B3+D#4:w^', STR: 'B2:w^', LH: 'B1+F#2:w^', BEL: 'B4:w^'},
                    dyn={BEL: 'pp'}))
    return Piece('main_theme', '安眠者有福', 'Beati qui dormiunt — 主题', 2, (4, 4), parts, bars,
                 reverb=2.6, wet=0.3, mix={CEL: 2, BEL: 5, RH: 3, LH: -6, STR: -2})


def door_side():
    """第一章《门的这一侧》：钟摆式的左手，零星的右手，远处只剩半句呼喊。"""
    CEL, ORG, RH, LH, STR = range(5)
    parts = [
        Part('celesta', 'Celesta', 'Cel.', 'keyboard.celesta', 8, 60, 108, pan=0.45, gain=0.85),
        Part('organ', 'Reed Organ', 'Org.', 'keyboard.harmonium', 20, 36, 96, pan=-0.3, gain=0.7),
        Part('piano', 'Piano', 'Pno.', 'keyboard.piano', 0, staves=2),
        Part('strings', 'Low Strings', 'Vc.', 'strings.cello', 42, 36, 76, bass_clef=True, pan=-0.15, gain=0.75),
    ]
    cycle1 = [['Bm9'], ['Gmaj9'], ['Em9'], ['F#sus4'], ['Bm9'], ['Gmaj9'], ['Dmaj7/F#'], ['A6']]
    cycle2 = [['Em9'], ['Gmaj9'], ['Bm9'], ['F#sus4'], ['Em9'], ['Cmaj7#11'], ['Gmaj7'], ['F#sus4', 'F#']]
    sections = [
        ('一 · 晨', cycle1, ['r:w', 'r:h B4+D5:h', 'r:w', 'r:h C#5:h', 'r:w', 'r:h A4+D5:h', 'r:w', 'r:h C#5+E5:h'],
         {3: CALL}, False, False),
        ('二 · 灰迹', cycle1, ['r:q B4:q C#5:q D5:q', 'F#5:h. E5:q', 'D5:h B4:h', 'C#5:w',
                              'r:q B4:q C#5:q D5:q', 'A5:h. G5:q', 'F#5:h E5:h', 'C#5:h. r:q'],
         {7: 'r:h F#5:q> E5:e F#5:e'}, True, False),
        ('三 · 声路', cycle2, ['G5:h F#5:q E5:q', 'D5:h. r:q', 'r:q F#5:q B5:q A5:q', 'C#5:w',
                              'G5:h F#5:q E5:q', 'E5:h. r:q', 'D5:h B4:h', 'C#5:h A#4:h'],
         {3: 'r:h F#5:q> E5:e F#5:e', 7: 'r:h F#6:q> E6:e F#6:e'}, True, True),
        ('四 · 余响', cycle1, ['r:w', 'r:h F#5:h', 'r:w', 'r:h C#5:h', 'r:w', 'r:h F#5:h', 'r:w', 'r:w'],
         {7: CALL}, True, False),
    ]
    bars = []
    for si, (mark, cycle, rh, calls, with_strings, with_organ) in enumerate(sections):
        for i, ch in enumerate(cycle):
            lines = {RH: rh[i], LH: arp(ch, PENDULUM, up=1 if si != 2 else 0)}
            if with_strings:
                lines[STR] = bass(ch)
            if with_organ:
                lines[ORG] = pad(ch)
            if i in calls:
                lines[CEL] = calls[i]
            kw = {}
            if i == 0:
                kw = dict(mark=mark)
                if si == 0:
                    kw.update(dyn={RH: 'p', LH: 'pp', CEL: 'pp', STR: 'pp', ORG: 'pp'},
                              tempo=(72, tempo_text(72, 'Andante, come un pendolo')),
                              text={LH: 'sempre legato, senza ped.'})
                if si == 2:
                    kw.update(dyn={RH: 'mp', LH: 'p', STR: 'p', ORG: 'pp', CEL: 'p'})
                if si == 3:
                    kw.update(dyn={RH: 'p', LH: 'pp', STR: 'pp', CEL: 'pp'})
            bars.append(Bar(lines, **kw))
    return Piece('door_side', '门的这一侧', '第一章 · 现场（循环）', 2, (4, 4), parts, bars, reverb=1.8, wet=0.28,
                 mix={CEL: 3, RH: 3, LH: -5.5})


def confrontation():
    """对质：下行半音的哀歌低音，旋律落在“对的音”上，和弦却阻碍终止。"""
    ORG, RH, LH, STR = range(4)
    parts = [
        Part('organ', 'Reed Organ', 'Org.', 'keyboard.harmonium', 20, 36, 96, pan=-0.25),
        Part('piano', 'Piano', 'Pno.', 'keyboard.piano', 0, staves=2, pan=0.1),
        Part('strings', 'Low Strings', 'Vc.', 'strings.cello', 42, 36, 76, bass_clef=True, pan=-0.05),
    ]

    def pulse(chords, offbeat=False):
        out = []
        for c, b in _split(chords):
            root = CH[c][2]
            low = shift(root + ':e', 0).split(':')[0]
            hi = shift(root + ':e', 1).split(':')[0]
            hits = int(b * 2)
            for k in range(hits):
                if offbeat:
                    out.append('r:e' if k % 2 == 0 else f'{low}+{hi}:e')
                else:
                    out.append(f'{low}+{hi}:e' + ('>' if k == 0 else ''))
        return ' '.join(out)

    lament = [['Bm'], ['Bm/A#'], ['Bm/A'], ['E/G#'], ['Gmaj7'], ['Em/G'], ['F#sus4', 'F#'], ['G']]
    demon = [
        ('E5:q F#5:q G5:q A5:q', ['Em']), ('A#5:h. C#6:q', ['F#']), ('B5:h G5:h', ['G']),
        ('E5:q D5:q C#5:q A#4:q', ['C#o7', 'F#7']), ('B4:q C#5:q D5:q E5:q', ['Bm']),
        ('F#5:q E5:q D5:q C#5:q', ['F#7']), ('D5:h B4:h', ['G']), ('C#5:h A#4:h', ['F#']),
    ]
    truth = list(THEME_A[:7]) + [('B4:w', ['G'])]   # 最后一音“对了”，脚下却是 G
    bars = []
    for i, ch in enumerate(lament):
        kw = {}
        if i == 0:
            kw = dict(mark='一 · 指认', dyn={ORG: 'mp', LH: 'mf', STR: 'mp', RH: 'mf'},
                      tempo=(88, tempo_text(88, 'Moderato, inesorabile')), text={LH: 'marcato'})
        bars.append(Bar({ORG: pad(ch), LH: pulse(ch), STR: bass(ch)}, **kw))
    for i, (mel, ch) in enumerate(demon):
        kw = dict(mark='二 · 字面为真', text={RH: 'tenuto, con ambiguità'}) if i == 0 else {}
        bars.append(Bar({RH: octaves(mel), ORG: pad(ch), LH: pulse(ch, offbeat=True), STR: bass(ch)}, **kw))
    for i, (mel, ch) in enumerate(truth):
        kw = dict(mark='三 · 整句', dyn={ORG: 'f'}, text={ORG: 'solo, a piena voce'}) if i == 0 else {}
        if i == 7:
            kw = dict(text={ORG: 'la nota giusta, il basso sbagliato'})
        bars.append(Bar({ORG: mel, RH: block(ch), LH: pulse(ch), STR: bass(ch)}, **kw))
    return Piece('confrontation', '对质', '始业之答 · 对质（循环）', 2, (4, 4), parts, bars, reverb=1.6, wet=0.22,
                 mix={ORG: 1, RH: 3, LH: -6})


# 第二章日间用的 6/8 伴奏：低音在 1、4 拍，和弦在其余八分
DAY = {
    'D': ('D3', 'A3+D4+F#4', 'D2'), 'G': ('G2', 'G3+B3+D4', 'G2'), 'A': ('A2', 'A3+C#4+E4', 'A2'),
    'A7': ('A2', 'G3+C#4+E4', 'A2'), 'Bm': ('B2', 'B3+D4+F#4', 'B2'), 'Em': ('E3', 'G3+B3+E4', 'E2'),
    'Em7': ('E3', 'G3+B3+D4', 'E2'), 'Gm': ('G2', 'G3+Bb3+D4', 'G2'), 'Gm6': ('G2', 'G3+Bb3+E4', 'G2'),
    'D/F#': ('F#2', 'A3+D4+F#4', 'F#2'), 'F#m': ('F#2', 'A3+C#4+F#4', 'F#2'),
}


def barcarolle(chords):
    out = []
    for c in (chords if len(chords) == 2 else chords * 2):
        low, chord, _ = DAY[c]
        out.append(f'{low}:e {chord}:e {chord}:e')
    return ' '.join(out)


def pizz_line(chords):
    if len(chords) == 1:
        root = DAY[chords[0]][2]
        fifth = shift(DAY[chords[0]][0] + ':q', 0).split(':')[0]
        return f'{root}:q. {fifth}:q.'
    return ' '.join(f'{DAY[c][2]}:q.' for c in chords)


def day():
    """第二章·日间：D 大调 6/8。门句的大调形（F# E D | A G A），短缺来自不信任——借来的 g 小调。"""
    CLA, CEL, RH, LH, PIZ = range(5)
    parts = [
        Part('clarinet', 'Clarinet', 'Cl.', 'wind.reed.clarinet', 71, 49, 94, pan=-0.3),
        Part('celesta', 'Celesta', 'Cel.', 'keyboard.celesta', 8, 60, 108, pan=0.4, gain=0.7),
        Part('piano', 'Piano', 'Pno.', 'keyboard.piano', 0, staves=2, pan=0.1),
        Part('pizz', 'Pizzicato Strings', 'Pizz.', 'strings.cello', 45, 36, 76, bass_clef=True, pan=-0.1),
    ]
    a = [
        ('F#5:e E5:e D5:e A5:q G5:e', ['D']), ('A5:q. r:e F#5:e A5:e', ['D']),
        ('B5:q A5:e G5:e F#5:e E5:e', ['G']), ('E5:q. C#5:q.', ['A']),
        ('F#5:e E5:e D5:e B5:q A5:e', ['Bm']), ('G5:q. E5:q.', ['G']),
        ('G5:e F#5:e E5:e A5:q C#5:e', ['Em', 'A7']), ('D5:q. r:q.', ['D']),
    ]
    b = [
        ('D5:e G5:e B5:e A5:q G5:e', ['G']), ('Bb5:q. A5:q.', ['Gm']),
        ('A5:e F#5:e D5:e E5:q F#5:e', ['D/F#']), ('G5:q. E5:q.', ['Em7']),
        ('F#5:e B5:e D6:e C#6:q B5:e', ['Bm']), ('Bb5:q. G5:q.', ['Gm6']),
        ('E5:e F#5:e G5:e A5:e G5:e E5:e', ['Em7', 'A7']), ('C#5:q. A4:q.', ['A7']),
    ]
    bars = []
    tempo = (90, tempo_text(60, 'Allegretto, a passeggio', dotted=True))
    for i, (mel, ch) in enumerate(a):
        kw = dict(mark='A · 配给台', tempo=tempo, text={RH: 'leggiero'},
                  dyn={RH: 'mf', LH: 'p', PIZ: 'mp', CLA: 'mp', CEL: 'p'}) if i == 0 else {}
        bars.append(Bar({RH: mel, LH: barcarolle(ch), PIZ: pizz_line(ch)}, **kw))
    for i, (mel, ch) in enumerate(b):
        kw = dict(mark='B · 十六人的规矩', text={CLA: 'dolce, un poco esitante'}) if i == 0 else {}
        bars.append(Bar({CLA: mel, LH: barcarolle(ch), PIZ: pizz_line(ch)}, **kw))
    for i, (mel, ch) in enumerate(a):
        lines = {CLA: mel, LH: barcarolle(ch), PIZ: pizz_line(ch)}
        if i % 2 == 1:
            lines[CEL] = shift(mel, 1)
        kw = dict(mark="A' · 午后", dyn={CLA: 'mf'}) if i == 0 else {}
        bars.append(Bar(lines, **kw))
    for i, (mel, ch) in enumerate(b):
        lines = {RH: mel, CEL: shift(mel, 1), LH: barcarolle(ch), PIZ: pizz_line(ch)}
        kw = dict(mark="B' · 傍晚对账", dyn={RH: 'mp', CEL: 'pp'}) if i == 0 else {}
        bars.append(Bar(lines, **kw))
    return Piece('day', '十六人的日子', '第二章 · 日间（循环）', 2, (6, 8), parts, bars, reverb=1.5, wet=0.2,
                 mix={CLA: 3, CEL: 3, RH: 4, LH: -6, PIZ: -1})


def night():
    """第二章·夜：八音盒放慢的门句；发条将尽，最后只剩一个“开”。"""
    BOX, BEL, ORG, LH, STR = range(5)
    parts = [
        Part('musicbox', 'Music Box', 'M.Box', 'metal.music-box', 10, 60, 108, pan=0.25),
        Part('bell', 'Tubular Bells', 'T.B.', 'metal.bells.tubular', 14, 48, 77, pan=-0.45, gain=0.55),
        Part('organ', 'Reed Organ', 'Org.', 'keyboard.harmonium', 20, 36, 96, pan=-0.2, gain=0.6),
        Part('piano', 'Piano', 'Pno.', 'keyboard.piano', 0, bass_clef=True, pan=0.05, gain=0.8),
        Part('strings', 'Low Strings', 'Vc.', 'strings.cello', 42, 36, 76, bass_clef=True, gain=0.8),
    ]

    def rock3(c):
        n = CH[c][0]
        return shift(f'{n[0]}:e {n[1]}:e {n[2]}:e {n[3]}:e {n[2]}:e {n[1]}:e', 1)

    phrase = [
        ('D5:e C#5:e B4:h', 'Bm'), ('F#5:q E5:e F#5:e r:q', 'Gmaj7'), ('G5:h F#5:e E5:e', 'Em7'),
        ('C#5:h.', 'F#sus4'), ('D5:e C#5:e B4:h', 'Bm'), ('F#5:q E5:e F#5:e r:q', 'Gmaj7'),
        ('D5:q C#5:q A#4:q', 'F#'), ('B4:h.', 'Bm'),
    ]
    middle = [
        ('F#5:h A5:q', 'D'), ('E5:h.', 'A/C#'), ('D5:h F#5:q', 'Bm'), ('C#5:h.', 'F#m'),
        ('B4:h D5:q', 'G'), ('A4:h.', 'D/F#'), ('G4:q B4:q E5:q', 'Em7'), ('C#5:h A#4:q', 'F#'),
    ]
    winding = [(shift(m, 1), c) for m, c in phrase[:4]] + [
        ('D6:e C#6:e B5:h', 'Bm'), ('F#5:q r:h', 'Gmaj7'), ('r:h.', 'Em7'), ('r:h C#5:q', 'F#sus4')]
    bars = []
    for si, (mark, seq) in enumerate((('一 · 熄灯', phrase), ('二 · 守夜', middle), ('三 · 发条', winding))):
        for i, (mel, c) in enumerate(seq):
            lines = {BOX: mel, ORG: f'{CH[c][1]}:h.', LH: rock3(c), STR: shift(f'{CH[c][2]}:h.', 1)}
            if i == 0:
                lines[BEL] = 'B3:h.' if si != 1 else 'F#3:h.'
            kw = {}
            if i == 0:
                kw = dict(mark=mark)
                if si == 0:
                    kw.update(tempo=(54, tempo_text(54, 'Lento, carillon')),
                              dyn={BOX: 'mp', BEL: 'pp', ORG: 'ppp', LH: 'pp', STR: 'pp'})
            bars.append(Bar(lines, **kw))
    return Piece('night', '安眠', '第二章 · 夜（循环）', 2, (3, 4), parts, bars, reverb=3.0, wet=0.36,
                 mix={BEL: 5, LH: -3})


CUES = [main_theme, door_side, confrontation, day, night]
