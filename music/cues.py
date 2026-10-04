"""《安眠者有福》原声曲目（2026-10-04 重写）。

核心动机“门句”取自第一章的情感核心：格雷厄姆喊的是“不要开门”，苏泠只听见后半截
“开门”。一小节四拍，前两拍是两个下行的弱音（不要），后两拍是上跳五度的呼喊（开门）：

    D5 C#5 B4 | F#5> E5 F#5          b 小调；后半截停在属音 F#，像一个问句

- 只出现后半截（F#5 E5 F#5）时，是门外的“呼喊”，听者补不出前半句；
- 完整出现时，是真相；尾声里后半截第一次落回主音，问句得到回答。
- 恶魔的和声是“字面为真”的阻碍终止：旋律给出正确的落音，脚下却换成了别的和弦。

结构规则（用户裁定，2026-10-04）：背景乐每首只保留一个主题、一种织体，循环时不再分段发展；
多段与多种演奏形式只用于主题曲与日常变奏。东方与中亚、印度风格以主题变奏出现，不点明国别。
全部变奏都从主题 A 的八小节变出，全作只有一个旋律身份。

曲目：
    main_theme      主题《安眠者有福》     标题、章末（多段）
    door_side       门的这一侧             第一章现场调查（单一主题循环）
    confrontation   对质                   始业之答、第二章第 4 日对质（单一主题循环）
    night           安眠                   第二章夜间、第一章“歇息”结局（单一主题循环）
    day             十六人的日子           第二章晨与上午（日常变奏：钢琴、单簧管、钢片琴三种形式）
    day_noon        午间                   第二章午间配给（丝竹式变奏：慢板加花 → 快板）
    day_afternoon   午后                   第二章下午（木卡姆式变奏：散板 → 手鼓 3+3+2 → 扬琴式加花）
    day_evening     暮课                   第二章傍晚（拉格式变奏：阿拉普 → 塔布拉十六拍）
    rite            誓约                   第 7 日名录库之议（雅乐式变奏：金声玉振，笙、埙、琴支声）
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
    bars.append(Bar({CEL: CALL, BEL: 'B4:w', STR: 'B2:w'}, harm=['Bm'], lead=CEL,
                    dyn={CEL: 'p', BEL: 'pp', STR: 'pp', ORG: 'pp', RH: 'p', LH: 'p'},
                    mark='引子 · 门外', text={CEL: 'lontano, come da un tubo'},
                    tempo=(63, tempo_text(63, 'Lento, cullando'))))
    bars.append(Bar({CEL: ECHO, STR: 'B2:w'}, harm=['Bm'], lead=CEL))
    bars.append(Bar({CEL: CALL, ORG: 'D4+F#4+B4:w', STR: 'G2:w'}, harm=['Gmaj7'], lead=CEL))
    bars.append(Bar({CEL: ECHO, ORG: 'C#4+F#4+A#4:w', STR: 'F#2:w'}, harm=['F#'], lead=CEL))
    # A · 安眠：钢琴独自说出整句
    for i, (mel, ch) in enumerate(THEME_A):
        kw = dict(dyn={RH: 'p', LH: 'pp', STR: 'pp'}, mark='A · 安眠', text={RH: 'semplice, cantabile'}) if i == 0 else {}
        bars.append(Bar({RH: mel, LH: arp(ch), STR: bass(ch)}, harm=ch, lead=RH, **kw))
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
        bars.append(Bar(lines, harm=ch, lead=ORG, **kw))
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
        bars.append(Bar(lines, harm=ch, lead=RH, **kw))
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
        bars.append(Bar(lines, harm=ch, lead=RH, **kw))
    # 尾声 · 问句得到回答：后半截第一次落回主音（B 大调）
    bars.append(Bar({CEL: CALL, ORG: 'F#3+B3+D#4:w', STR: 'B2:w', LH: 'B1+F#2:w'}, harm=['B'], lead=CEL,
                    dyn={CEL: 'p', ORG: 'pp', STR: 'pp', LH: 'pp'}, mark='尾声 · 回答', text={CEL: 'dolcissimo'}))
    bars.append(Bar({CEL: 'E5:h D#5:h', ORG: 'G#3+B3+E4:h F#3+B3+D#4:h', STR: 'B2:w', LH: 'B1+E2:h B1+F#2:h'},
                    harm=['E/B', 'B'], lead=CEL))
    bars.append(Bar({CEL: CALL, ORG: 'F#3+B3+D4:w', STR: 'G2:w', LH: 'G1+D2:w'}, dyn={CEL: 'pp'},
                    harm=['Gmaj7'], lead=CEL))
    bars.append(Bar({CEL: 'D#5:h B4:h^', ORG: 'F#3+B3+D#4:w^', STR: 'B2:w^', LH: 'B1+F#2:w^', BEL: 'B4:w^'},
                    dyn={BEL: 'pp'}, harm=['B'], lead=CEL))
    return Piece('main_theme', '安眠者有福', 'Beati qui dormiunt — 主题', 2, (4, 4), parts, bars,
                 reverb=2.6, wet=0.3, mix={CEL: 8, BEL: 13, ORG: -2, RH: 3, LH: 2, STR: -7},
                 style=dict(rubato=1.0, swell=1.0, pedal={RH: 'full', LH: 'full'}), squeeze=1.8)


def door_side():
    """第一章《门的这一侧》：一个主题、一种织体。钟摆式左手、零星的右手旋律，远处只剩半句呼喊。"""
    CEL, ORG, RH, LH, STR = range(5)
    parts = [
        Part('celesta', 'Celesta', 'Cel.', 'keyboard.celesta', 8, 60, 108, pan=0.45, gain=0.85),
        Part('organ', 'Reed Organ', 'Org.', 'keyboard.harmonium', 20, 36, 96, pan=-0.3, gain=0.7),
        Part('piano', 'Piano', 'Pno.', 'keyboard.piano', 0, staves=2),
        Part('strings', 'Low Strings', 'Vc.', 'strings.cello', 42, 36, 76, bass_clef=True, pan=-0.15, gain=0.75),
    ]
    chords = [['Bm9'], ['Gmaj9'], ['Em9'], ['F#sus4'], ['Bm9'], ['Gmaj9'], ['Dmaj7/F#'], ['A6'],
              ['Bm9'], ['Gmaj9'], ['Em9'], ['F#sus4'], ['Bm9'], ['Gmaj9'], ['Dmaj7/F#'], ['F#sus4', 'F#']]
    melody = ['r:q B4:q C#5:q D5:q', 'F#5:h. E5:q', 'D5:h B4:h', 'C#5:w',
              'r:q B4:q C#5:q D5:q', 'A5:h. G5:q', 'F#5:h E5:h', 'C#5:h. r:q',
              'r:q B4:q C#5:q D5:q', 'F#5:h. E5:q', 'D5:h B4:h', 'C#5:w',
              'r:q D5:q E5:q F#5:q', 'B5:h. A5:q', 'F#5:h E5:h', 'C#5:h A#4:h']
    bars = []
    for i, ch in enumerate(chords):
        lines = {RH: melody[i], LH: arp(ch, PENDULUM, up=1), STR: bass(ch), ORG: pad(ch)}
        if i in (3, 11):
            lines[CEL] = CALL
        kw = {}
        if i == 0:
            kw = dict(mark='门的这一侧', dyn={RH: 'p', LH: 'pp', CEL: 'pp', STR: 'pp', ORG: 'ppp'},
                      tempo=(72, tempo_text(72, 'Andante, come un pendolo')), text={LH: 'sempre legato, senza ped.'})
        bars.append(Bar(lines, harm=ch, lead=RH, **kw))
    return Piece('door_side', '门的这一侧', '第一章 · 现场（单一主题循环）', 2, (4, 4), parts, bars, reverb=1.8, wet=0.28,
                 mix={CEL: 9, ORG: 2, RH: 3, LH: -0.5, STR: -7},
                 style=dict(rubato=0.4, swell=0.6, pedal={RH: 'half', LH: 'half'}), squeeze=2.4)


def confrontation():
    """对质：一个主题、一种织体。风琴奏整句门句，钢琴左手八分脉动；两次句末都落在“对的音”上，
    脚下却是 G 大调（阻碍终止，恶魔“字面为真”）。第二遍句尾攀到 B5 再落下。"""
    ORG, RH, LH, STR = range(4)
    parts = [
        Part('organ', 'Reed Organ', 'Org.', 'keyboard.harmonium', 20, 36, 96, pan=-0.25),
        Part('piano', 'Piano', 'Pno.', 'keyboard.piano', 0, staves=2, pan=0.1),
        Part('strings', 'Low Strings', 'Vc.', 'strings.cello', 42, 36, 76, bass_clef=True, pan=-0.05),
    ]

    def pulse(chords):
        out = []
        for c, b in _split(chords):
            root = CH[c][2]
            hi = shift(root + ':e', 1).split(':')[0]
            out += [f'{root}+{hi}:e' + ('>' if k == 0 else '') for k in range(int(b * 2))]
        return ' '.join(out)

    first = list(THEME_A[:7]) + [('B4:w', ['G'])]
    second = list(THEME_A[:5]) + [('B5:h A5:q G5:q', ['G', 'A7']),
                                  ('F#5:q E5:e D5:e C#5:q A#4:q', ['Bm', 'C#o7', 'F#', 'F#7']), ('B4:w', ['G'])]
    bars = []
    for i, (mel, ch) in enumerate(first + second):
        kw = {}
        if i == 0:
            kw = dict(mark='对质', dyn={ORG: 'mf', RH: 'p', LH: 'mf', STR: 'mp'},
                      tempo=(88, tempo_text(88, 'Moderato, inesorabile')), text={ORG: 'a piena voce', LH: 'marcato'})
        if i in (7, 15):
            kw = dict(text={ORG: 'la nota giusta, il basso sbagliato'})
        bars.append(Bar({ORG: mel, RH: block(ch), LH: pulse(ch), STR: bass(ch)}, harm=ch, lead=ORG, **kw))
    return Piece('confrontation', '对质', '始业之答 · 对质（单一主题循环）', 2, (4, 4), parts, bars, reverb=1.6, wet=0.22,
                 mix={ORG: -1, RH: 3, LH: -3, STR: -5},
                 style=dict(rubato=0.25, swell=0.5, pedal={RH: 'half', LH: 'none'}))


def night():
    """第二章·夜：一个主题、一种织体。八音盒放慢的门句，第二遍发条将尽，最后只剩一个“开”。"""
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
    winding = [(shift(m, 1), c) for m, c in phrase[:4]] + [
        ('D6:e C#6:e B5:h', 'Bm'), ('F#5:q r:h', 'Gmaj7'), ('r:h.', 'Em7'), ('r:h C#5:q', 'F#sus4')]
    bars = []
    for i, (mel, c) in enumerate(phrase + winding):
        lines = {BOX: mel, ORG: f'{CH[c][1]}:h.', LH: rock3(c), STR: shift(f'{CH[c][2]}:h.', 1)}
        if i in (0, 8):
            lines[BEL] = 'B3:h.'
        kw = {}
        if i == 0:
            kw = dict(mark='安眠', tempo=(54, tempo_text(54, 'Lento, carillon')),
                      dyn={BOX: 'mp', BEL: 'pp', ORG: 'ppp', LH: 'pp', STR: 'pp'})
        bars.append(Bar(lines, harm=[c], lead=BOX, **kw))
    return Piece('night', '安眠', '第二章 · 夜（单一主题循环）', 2, (3, 4), parts, bars, reverb=3.0, wet=0.36,
                 mix={BEL: 9, LH: 1, STR: -8},
                 style=dict(rubato=1.0, swell=0.8, pedal={LH: 'full'}), squeeze=2.0)


# ---------------------------------------------------------------- 日常变奏
# 主题 A 的大调形：整句在调号内上移三度（B 小调 → D 大调），门句成为 F# E D | A G A
THEME_MAJOR_68 = [
    ('F#5:e E5:e D5:e A5:e G5:e A5:e', ['D', 'A/C#']),
    ('B5:q. A5:e G5:e F#5:e', ['Bm', 'Em']),
    ('E5:e F#5:e G5:e A5:q D5:e', ['A', 'D']),
    ('E5:q. C#5:q.', ['A', 'A7']),
    ('F#5:e E5:e D5:e A5:e G5:e A5:e', ['D', 'A/C#']),
    ('C#6:q. B5:e A5:e G5:e', ['F#m', 'D']),
    ('F#5:e E5:e D5:e E5:q C#5:e', ['Bm', 'A7']),
    ('D5:q. r:q.', ['D']),
]
COUNTER_68 = ['D5:q. C#5:q.', 'D5:q. E5:q.', 'C#5:q. D5:q.', 'C#5:q. E5:q.',
              'D5:q. C#5:q.', 'C#5:q. A4:q.', 'B4:q. A4:q.', 'A4:h.']

# 6/8 船歌伴奏：低音在 1、4 拍，和弦在其余八分
DAY = {
    'D': ('D3', 'A3+D4+F#4', 'D2'), 'G': ('G2', 'G3+B3+D4', 'G2'), 'A': ('A2', 'A3+C#4+E4', 'A2'),
    'A7': ('A2', 'G3+C#4+E4', 'A2'), 'Bm': ('B2', 'B3+D4+F#4', 'B2'), 'Em': ('E3', 'G3+B3+E4', 'E2'),
    'F#m': ('F#2', 'A3+C#4+F#4', 'F#2'), 'A/C#': ('C#3', 'A3+C#4+E4', 'C#2'),
}


def barcarolle(chords):
    out = []
    for c in (chords if len(chords) == 2 else chords * 2):
        low, chord, _ = DAY[c]
        out.append(f'{low}:e {chord}:e {chord}:e')
    return ' '.join(out)


def pizz_line(chords):
    if len(chords) == 1:
        return f'{DAY[chords[0]][2]}:q. {DAY[chords[0]][0]}:q.'
    return ' '.join(f'{DAY[c][2]}:q.' for c in chords)


def day():
    """第二章晨与上午：主题的日常变奏，D 大调 6/8 船歌。三种形式：钢琴、单簧管（钢片琴应和）、钢片琴（单簧管对位）。"""
    CLA, CEL, RH, LH, PIZ = range(5)
    parts = [
        Part('clarinet', 'Clarinet', 'Cl.', 'wind.reed.clarinet', 71, 49, 94, pan=-0.3),
        Part('celesta', 'Celesta', 'Cel.', 'keyboard.celesta', 8, 60, 108, pan=0.4, gain=0.7),
        Part('piano', 'Piano', 'Pno.', 'keyboard.piano', 0, staves=2, pan=0.1),
        Part('pizz', 'Pizzicato Strings', 'Pizz.', 'strings.cello', 45, 36, 76, bass_clef=True, pan=-0.1),
    ]
    bars = []
    tempo = (90, tempo_text(60, 'Allegretto, a passeggio', dotted=True))
    for i, (mel, ch) in enumerate(THEME_MAJOR_68):
        kw = dict(mark='一 · 钢琴', tempo=tempo, text={RH: 'leggiero'},
                  dyn={RH: 'mf', LH: 'p', PIZ: 'mp', CLA: 'mp', CEL: 'p'}) if i == 0 else {}
        bars.append(Bar({RH: mel, LH: barcarolle(ch), PIZ: pizz_line(ch)}, harm=ch, lead=RH, **kw))
    for i, (mel, ch) in enumerate(THEME_MAJOR_68):
        lines = {CLA: mel, LH: barcarolle(ch), PIZ: pizz_line(ch)}
        if i % 2 == 1:
            lines[CEL] = shift(mel, 1)
        kw = dict(mark='二 · 单簧管', text={CLA: 'dolce'}) if i == 0 else {}
        bars.append(Bar(lines, harm=ch, lead=CLA, **kw))
    for i, (mel, ch) in enumerate(THEME_MAJOR_68):
        lines = {CEL: mel, CLA: COUNTER_68[i], LH: barcarolle(ch), PIZ: pizz_line(ch)}
        kw = dict(mark='三 · 钢片琴', dyn={CEL: 'mp', CLA: 'p'}) if i == 0 else {}
        bars.append(Bar(lines, harm=ch, lead=CEL, **kw))
    return Piece('day', '十六人的日子', '日常变奏 · 船歌（循环）', 2, (6, 8), parts, bars, reverb=1.5, wet=0.2,
                 mix={CLA: 6, CEL: 7, RH: 4, LH: 0, PIZ: -5},
                 style=dict(rubato=0.5, swell=0.8, pedal={RH: 'half', LH: 'half'}, short_chords=(LH,)))


# 4/4 的五声化大调主题（宫调式）：句尾避开导音，半终止停在羽音 B 上
THEME_GONG = ['F#5:e E5:e D5:q A5:q G5:e A5:e', 'B5:q. A5:e G5:q F#5:q', 'E5:q F#5:e G5:e A5:q D5:q',
              'E5:h D5:q B4:q', 'F#5:e E5:e D5:q A5:q G5:e A5:e', 'B5:q. A5:e F#5:q E5:q',
              'F#5:q E5:e D5:e E5:q A4:q', 'D5:w']
# 笛子加花（放慢加花）：骨干音不变，拍内填入邻音与回音
THEME_GONG_ORNATE = [
    'F#5:s A5:s F#5:s E5:s D5:e. E5:s A5:s B5:s A5:e G5:s A5:s B5:s A5:s',
    'B5:e. D6:s B5:e A5:e G5:s A5:s G5:s E5:s F#5:q',
    'E5:s F#5:s E5:s D5:s F#5:e G5:e A5:e. B5:s A5:s F#5:s E5:s D5:s',
    'E5:q. F#5:e D5:e. E5:s B4:s A4:s B4:e',
    'F#5:s A5:s F#5:s E5:s D5:e. E5:s A5:s B5:s A5:e G5:s A5:s B5:s A5:s',
    'B5:e. D6:s B5:e A5:e F#5:s A5:s F#5:s E5:s E5:q',
    'F#5:s A5:s F#5:s E5:s D5:e E5:e E5:e. D5:s A4:q',
    'D5:h. E5:s D5:s B4:s D5:s',
]
ZHENG = {'D': 'D3:e A3:e D4:e E4:e F#4:e E4:e D4:e A3:e', 'B': 'B2:e F#3:e B3:e D4:e E4:e D4:e B3:e F#3:e',
         'A': 'A2:e E3:e A3:e B3:e D4:e B3:e A3:e E3:e', 'E': 'E3:e B3:e E4:e F#4:e A4:e F#4:e E4:e B3:e'}
GONG_ROOTS = ['D', 'B', 'A', 'E', 'D', 'B', 'A', 'D']


def tremolo(line):
    """扬琴式轮音：四分音符及更长的音拆成同音八分。"""
    out = []
    for tok in line.split():
        notes, dur = tok.split(':')
        n = {'q': 2, 'q.': 3, 'h': 4, 'h.': 6, 'w': 8}.get(dur.rstrip('>^-'))
        if n and notes != 'r':
            out += [f'{notes}:e'] * n
        else:
            out.append(tok)
    return ' '.join(out)


def day_noon():
    """午间配给：丝竹式变奏。慢板里笛子加花、胡琴奏骨干、筝以五声分解托底；转快板后扬琴轮音、
    弹拨低八度与木鱼加入，第二遍笛子翻高八度。各声部同奏一条旋律的不同繁简（支声复调），不用西式和声。"""
    DIZI, ERHU, YANG, PIPA, ZHENG_, MUYU = range(6)
    parts = [
        Part('flute', 'Bamboo Flute', 'Fl.', 'wind.flutes.flute', 73, 60, 98, pan=-0.25),
        Part('fiddle', 'Two-string Fiddle', 'Fdl.', 'strings.violin', 110, 55, 96, pan=0.25),
        Part('dulcimer', 'Hammered Dulcimer', 'Dulc.', 'pitched-percussion.hammered-dulcimer', 15, 48, 96, pan=0.4),
        Part('shamisen', 'Plucked Lute', 'Lute', 'pluck.shamisen', 106, 40, 88, pan=-0.4),
        Part('koto', 'Zither', 'Zith.', 'pluck.koto', 107, 36, 91, bass_clef=True, pan=0.1),
        Part('woodblock', 'Wood Block', 'W.B.', 'percussion.woodblock', 115, 60, 84),
    ]
    bars = []
    for i in range(8):
        kw = dict(mark='一 · 慢板（放慢加花）', tempo=(60, tempo_text(60, 'Adagio, con fioriture')),
                  dyn={DIZI: 'mp', ERHU: 'p', ZHENG_: 'p', PIPA: 'p', YANG: 'p', MUYU: 'p'}) if i == 0 else {}
        lines = {DIZI: THEME_GONG_ORNATE[i], ERHU: THEME_GONG[i], ZHENG_: ZHENG[GONG_ROOTS[i]]}
        bars.append(Bar(lines, harm=[GONG_ROOTS[i]], lead=DIZI, **kw))
    for rep in range(2):
        for i in range(8):
            kw = {}
            if i == 0 and rep == 0:
                kw = dict(mark='二 · 快板', tempo=(112, tempo_text(112, 'Allegro')),
                          dyn={DIZI: 'mf', ERHU: 'mf', YANG: 'mp', PIPA: 'mp', MUYU: 'mp'})
            elif i == 0:
                kw = dict(mark="二' · 快板（笛高八度）")
            lines = {DIZI: THEME_GONG[i] if rep == 0 else shift(THEME_GONG[i], 1), ERHU: THEME_GONG[i],
                     YANG: tremolo(shift(THEME_GONG[i], -1)), PIPA: shift(THEME_GONG[i], -1),
                     MUYU: 'C5:q> C5:q C5:q C5:q'}
            bars.append(Bar(lines, harm=[GONG_ROOTS[i]], lead=DIZI, **kw))
    return Piece('day_noon', '午间', '日常变奏 · 丝竹式（循环）', 2, (4, 4), parts, bars, reverb=1.3, wet=0.2,
                 mix={ZHENG_: 4, MUYU: 8}, style=dict(rubato=0.5, swell=0.7, lead_db=2.0))


# B 和声小调的主题：希贾兹色彩落在 G–A# 的增二度上（第 6 小节由 A#5 落到 G5）
THEME_HIJAZ = [
    MOTIF, 'G5:q. F#5:e E5:e F#5:e D5:q', 'C#5:q D5:e E5:e F#5:q B4:q', 'C#5:h A#4:q r:q',
    MOTIF, 'A#5:q. G5:e F#5:q E5:q', 'D5:q C#5:e B4:e C#5:q A#4:q', 'B4:w',
]
TAQSIM = ['F#5:h. G5:e F#5:e', 'E5:q D5:q C#5:h', 'D5:e C#5:e B4:q F#5:h', 'G5:e F#5:e E5:e D5:e C#5:h']


def day_afternoon():
    """下午：木卡姆式变奏。先是芦笛在持续音上的散板，然后手鼓按 3+3+2 进入、芦笛奏主题，
    最后扬琴以轮音加花接过主题、拉弦低八度相随。和声只靠 B–F# 的持续音与旋律本身。"""
    NEY, SANTUR, DUTAR, FIDDLE, DAF, DRONE = range(6)
    parts = [
        Part('shakuhachi', 'Reed Flute', 'Ney', 'wind.flutes.shakuhachi', 77, 60, 93, pan=-0.2),
        Part('dulcimer', 'Hammered Dulcimer', 'Sant.', 'pitched-percussion.hammered-dulcimer', 15, 48, 96, pan=0.35),
        Part('shamisen', 'Long-neck Lute', 'Lute', 'pluck.shamisen', 106, 40, 84, bass_clef=True, pan=-0.35),
        Part('fiddle', 'Spike Fiddle', 'Fdl.', 'strings.violin', 110, 55, 96, pan=0.2),
        Part('framedrum', 'Frame Drum', 'F.Dr.', 'percussion.frame-drum', 115, 48, 72, pan=0.05),
        Part('strings', 'Drone', 'Dr.', 'strings.cello', 42, 36, 76, bass_clef=True, gain=0.6),
    ]
    aksak = 'C3:e> r:e C4:e C3:e> r:e C4:e C4:e> C4:e'
    ostinato = 'B2:e F#3:e B3:e B2:e F#3:e B3:e F#3:e B3:e'
    bars = []
    for i, line in enumerate(TAQSIM):
        kw = dict(mark='一 · 散板', tempo=(96, tempo_text(96, 'Moderato, rubato')),
                  dyn={NEY: 'mp', DRONE: 'pp', SANTUR: 'pp', DUTAR: 'p', FIDDLE: 'p', DAF: 'mp'}) if i == 0 else {}
        bars.append(Bar({NEY: line, DRONE: 'B2+F#3:w', SANTUR: 'B3:h r:h' if i % 2 == 0 else 'r:w'},
                        harm=['Bm'], lead=NEY, **kw))
    for i, line in enumerate(THEME_HIJAZ):
        kw = dict(mark='二 · 主题（3+3+2）') if i == 0 else {}
        bars.append(Bar({NEY: line, DUTAR: ostinato, DAF: aksak, DRONE: 'B2+F#3:w'}, harm=['Bm'], lead=NEY, **kw))
    for i, line in enumerate(THEME_HIJAZ):
        kw = dict(mark='三 · 扬琴加花', dyn={SANTUR: 'mf'}) if i == 0 else {}
        bars.append(Bar({SANTUR: tremolo(line), FIDDLE: shift(line, -1), DUTAR: ostinato, DAF: aksak,
                         DRONE: 'B2+F#3:w'}, harm=['Bm'], lead=SANTUR, **kw))
    return Piece('day_afternoon', '午后', '日常变奏 · 木卡姆式（循环）', 2, (4, 4), parts, bars, reverb=1.4, wet=0.22,
                 mix={DRONE: -6, DAF: -2}, style=dict(rubato=0.6, swell=0.7, lead_db=2.5))


# Kirwani 拉格（音阶与和声小调相同），Sa = B
ALAP = ['r:h B3:h', 'C#4:e D4:q. C#4:q B3:q', 'D4:h. E4:e F#4:e', 'F#4:w',
        'G4:q F#4:q A#4:e G4:e F#4:q', 'B4:h. A#4:q', 'D5:e C#5:e B4:q F#4:h', 'E4:e F#4:e D4:e C#4:e B3:h']
THEME_KIRWANI = [
    MOTIF, 'G5:q. F#5:e E5:q D5:q', 'C#5:q D5:e E5:e F#5:q B4:q', 'C#5:h A#4:q r:q',
    MOTIF, 'A#5:q. G5:e F#5:q E5:q', 'D5:q C#5:e B4:e C#5:q A#4:q', 'B4:w',
]
# 塔布拉十六拍（teentaal），击法以音高编码（见 synth.tabla）：
# Dha Dhin Dhin Dha | Dha Dhin Dhin Dha | Dha Tin Tin Ta | Ta Dhin Dhin Dha
TEENTAAL = ['C3+C4:q> C3+D4:q C3+D4:q C3+C4:q', 'C3+C4:q C3+D4:q C3+D4:e E4:e C3+C4:q',
            'C3+C4:q D4:q D4:q C4:q', 'C4:q C3+D4:q C3+D4:e E4:e C3+C4:q']


def day_evening():
    """傍晚：拉格式变奏。坦布拉的 Pa–Sa–Sa–Sa 持续音之上，西塔琴先以阿拉普慢慢展开音阶，
    第七小节才露出门句；随后塔布拉进入十六拍，西塔琴与竹笛先后奏主题。"""
    SITAR, BANSURI, TANPURA, TABLA = range(4)
    parts = [
        Part('sitar', 'Sitar', 'Sit.', 'pluck.sitar', 104, 48, 84, pan=-0.15),
        Part('flute', 'Bamboo Flute', 'Fl.', 'wind.flutes.flute', 73, 60, 96, pan=0.25),
        Part('tanpura', 'Tanpura', 'Tanp.', 'pluck.tanpura', 104, 40, 64, bass_clef=True, gain=0.8),
        Part('tabla', 'Tabla', 'Tab.', 'percussion.tabla', 116, 48, 72, pan=0.1),
    ]
    drone = 'F#3:q B3:q B3:q B2:q'
    bars = []
    for i, line in enumerate(ALAP):
        kw = dict(mark='一 · 阿拉普', tempo=(60, tempo_text(60, 'Lento, senza misura')),
                  dyn={SITAR: 'mp', TANPURA: 'p', BANSURI: 'mp', TABLA: 'mp'}) if i == 0 else {}
        bars.append(Bar({SITAR: line, TANPURA: drone}, harm=['Bm'], lead=SITAR, **kw))
    for rep in range(2):
        for i, line in enumerate(THEME_KIRWANI):
            kw = {}
            if i == 0 and rep == 0:
                kw = dict(mark='二 · 主题（十六拍）', tempo=(88, tempo_text(88, 'Andante')))
            elif i == 0:
                kw = dict(mark="二' · 竹笛")
            lines = {TANPURA: drone, TABLA: TEENTAAL[i % 4], SITAR: shift(line, -1)}
            if rep == 1:
                lines[BANSURI] = line
            bars.append(Bar(lines, harm=['Bm'], lead=SITAR if rep == 0 else BANSURI, **kw))
    return Piece('day_evening', '暮课', '日常变奏 · 拉格式（循环）', 2, (4, 4), parts, bars, reverb=1.8, wet=0.24,
                 mix={SITAR: 2, BANSURI: 1, TANPURA: -5, TABLA: -4}, style=dict(rubato=0.7, swell=0.7, lead_db=2.5))


# 羽调式五声（B D E F# A）的主题：门句成为 D–B | F#–E–F#
THEME_YU = ['D5:h B4:h', 'F#5:h E5:q F#5:q', 'A5:h F#5:q E5:q', 'D5:h E5:h', 'F#5:w',
            'D5:h B4:h', 'F#5:h E5:q F#5:q', 'B5:h A5:q F#5:q', 'E5:h D5:q E5:q', 'B4:w']
QIN = ['E4:e D4:q. B3:h', 'F#4:h. E4:e F#4:e', 'B4:e A4:q. F#4:q E4:q', 'D4:h E4:h', 'F#4:q. E4:e F#4:h',
       'E4:e D4:q. B3:h', 'F#4:h. E4:e F#4:e', 'B4:h A4:q F#4:q', 'E4:q. D4:e E4:h', 'B3:w']
SHENG = ['B3+F#4+B4:w', 'B3+F#4:w', 'A3+E4:w', 'D4+A4:w', 'B3+F#4:w',
         'B3+F#4:w', 'B3+F#4:w', 'E4+B4:w', 'A3+E4:w', 'B3+F#4+B4:w']


def rite():
    """誓约（第 7 日名录库之议）：雅乐式变奏。击柷起乐，编钟逐音奏主题（金声），编磬收句（玉振），
    笙以四五度持续，建鼓开合；第二遍埙奏主题、琴低八度支声，末以三通鼓止。"""
    BZ, QING, SHENG_, XUN, QIN_, DRUM, ZHU = range(7)
    parts = [
        Part('bianzhong', 'Chime Bells', 'Bells', 'metal.bells.chime', 14, 48, 96, pan=-0.3),
        Part('bianqing', 'Stone Chimes', 'Stones', 'pitched-percussion.lithophone', 13, 48, 96, pan=0.3),
        Part('organ', 'Mouth Organ', 'M.Org.', 'wind.reed.sheng', 20, 48, 88, gain=0.8),
        Part('ocarina', 'Vessel Flute', 'V.Fl.', 'wind.flutes.ocarina', 79, 60, 88, pan=-0.15),
        Part('koto', 'Seven-string Zither', 'Zith.', 'pluck.koto', 107, 36, 84, pan=0.15),
        Part('taiko', 'Great Drum', 'Dr.', 'drum.taiko', 116, 36, 60, bass_clef=True),
        Part('woodblock', 'Wooden Box', 'Box', 'percussion.woodblock', 115, 60, 84),
    ]
    bars = []
    for rep in range(2):
        for i in range(10):
            lines = {SHENG_: SHENG[i]}
            if rep == 0:
                lines[BZ] = THEME_YU[i]
            else:
                lines[XUN] = THEME_YU[i]
                lines[QIN_] = QIN[i]
                lines[BZ] = THEME_YU[i].split()[0].split(':')[0] + ':w'
            if i in (4, 9):
                lines[QING] = 'F#5:w' if i == 4 else 'B4:w'
            if i in (0, 5):
                lines[DRUM] = 'C3:q> r:q r:h'
            if rep == 1 and i == 9:
                lines[DRUM] = 'C3:q> C3:q> C3:q> r:q'
            if rep == 0 and i == 0:
                lines[ZHU] = 'C5:q C5:q C5:q r:q'
            kw = {}
            if i == 0 and rep == 0:
                kw = dict(mark='一 · 金声玉振', tempo=(50, tempo_text(50, 'Grave, rituale')),
                          dyn={BZ: 'mf', QING: 'mp', SHENG_: 'p', XUN: 'mp', QIN_: 'mp', DRUM: 'mf', ZHU: 'mp'})
            elif i == 0:
                kw = dict(mark='二 · 埙与琴')
            bars.append(Bar(lines, harm=['Bm'], lead=BZ if rep == 0 else XUN, **kw))
    return Piece('rite', '誓约', '日常变奏 · 雅乐式（名录库之议，循环）', 2, (4, 4), parts, bars, reverb=2.8, wet=0.32,
                 mix={QING: 3, QIN_: 4, DRUM: 2, ZHU: 12}, style=dict(rubato=0.15, swell=0.4, lead_db=2.0), squeeze=1.8)


CUES = [main_theme, door_side, confrontation, night, day, day_noon, day_afternoon, day_evening, rite]
