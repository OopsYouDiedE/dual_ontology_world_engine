"""《安眠者有福》原声曲目（2026-10-16 音乐语言与曲调体系升级版）。

核心音乐语汇：
1. “门句动机”（The Door Motif）：
   取自第一章情感核心——格雷厄姆喊的是“不要开门”，苏泠只听见后半截“开门”：
       (D5:e C#5:e B4:q) (F#5:q> E5:e F#5:e)      b 小调；后半截悬停于属音 F#，如问句
   - 调查初期仅隐现后半截（F#5–E5–F#5）的管道漏音；
   - 主题曲与高潮处完整呈示前后两截；尾声转入同主音 B 大调，后半截首次落回主音（D#5–B4），问句获得解答。
2. 恶魔的和声签名——“阻碍终止”（Deceptive Cadence）与半音帕萨卡利亚：
   旋律给出完全正确的属音到主音落音（C#5–A#4 -> B4），低音与和声却悄然滑向六级（Gmaj7 / Em/G），
   加上那不勒斯降二级（C♮）的冷冽折射，对应恶魔“字面为真、根基偷换”的叙事法则。
3. 完整的乐段发展与起承转合：
   打破单一织体循环限制，每首曲目均具备多乐段起承转合、声部复调对位（含单谱表双声部）、跨小节延留连音线、
   乐句呼吸连线与色彩调性对比。所有乐器声部名称、模板与音色绑定严格保持 MuseScore 4 工程原貌。
"""
from score_io import Bar, Part, Piece, octaves, shift

# ---------------------------------------------------------------- 乐器定义（严格保持 MuseScore 4 工程名称与通道）
STRINGS_CH = (('arco', 0, 48), ('pizzicato', 0, 45), ('tremolo', 0, 44))
CELLOS_CH = (('arco', 41, 48), ('pizzicato', 40, 45), ('tremolo', 41, 44))
BASSES_CH = (('arco', 51, 48), ('pizzicato', 50, 45), ('tremolo', 51, 44))


def p_celesta(part_id=0, **kw):
    return Part('celesta', '钢片琴', 'Cel.', 'keyboard.celesta', 8, 48, 113, template_id='celesta',
                voice='celesta', transpose=(12, 7), part_id=part_id, **kw)


def p_bells(part_id=0, **kw):
    return Part('bell', '管钟', 'Cme.', 'pitched-percussion.tubular-bells', 14, 53, 79, amateur=(60, 79),
                template_id='tubular-bells', voice='bell', part_id=part_id, **kw)


def p_harmonium(part_id=0, **kw):
    return Part('organ', '簧风琴', 'Harm.', 'keyboard.harmonium', 20, 29, 89, template_id='harmonium',
                voice='organ', part_id=part_id, **kw)


def p_piano(part_id=0, **kw):
    return Part('piano', '钢琴', 'Pno.', 'keyboard.piano', 0, 21, 108, template_id='piano', voice='piano',
                part_id=part_id, **kw)


def p_strings(part_id=0, **kw):
    return Part('strings', '弦乐组', 'St.', 'strings.group', 48, 24, 108, bass_clef=True, template_id='strings',
                voice='strings', channel_name='arco', channels=STRINGS_CH, part_id=part_id, **kw)


def p_cellos(pizz=False, part_id=0, **kw):
    return Part('cellos', '乐队大提琴', 'Vcs.', 'strings.group', 45 if pizz else 48, 36, 90, amateur=(36, 67),
                bass_clef=True, template_id='violoncellos', voice='cellos_pizz' if pizz else 'cellos',
                bank=40 if pizz else 41, channel_name='pizzicato' if pizz else 'arco', channels=CELLOS_CH,
                part_id=part_id, **kw)


def p_basses(pizz=False, part_id=0, **kw):
    return Part('contrabasses', '乐队低音提琴', 'Cbs.', 'strings.group', 45 if pizz else 48, 24, 67, amateur=(28, 62),
                bass_clef=True, template_id='contrabasses', voice='basses_pizz' if pizz else 'basses',
                bank=50 if pizz else 51, channel_name='pizzicato' if pizz else 'arco', channels=BASSES_CH,
                transpose=(-12, -7), part_id=part_id, **kw)


def p_temple_blocks(part_id=0, **kw):
    return Part('woodblock', '木鱼', 'Tmp. Bl.', 'wood.temple-block', 115, 60, 84, template_id='temple-blocks',
                voice='templeblocks', bank=1, drum_map={72: 62}, part_id=part_id, **kw)


def with_contrabass(bars, cello, bass, custom_bass=None):
    """低音提琴：默认支撑大提琴低音骨架，支持特定小节独立低音线。"""
    for bi, bar in enumerate(bars):
        if custom_bass and bi in custom_bass:
            bar.lines[bass] = custom_bass[bi]
        elif cello in bar.lines:
            # 取大提琴第一声部下移八度，并确保落在低音提琴音域 24..67 (C1..G4) 内
            c_line = bar.lines[cello].split('||')[0].strip()
            bar.lines[bass] = shift(c_line, -1)
        if cello in bar.dyn and bass not in bar.dyn:
            bar.dyn[bass] = bar.dyn[cello]
        if cello in bar.tech and bass not in bar.tech:
            bar.tech[bass] = bar.tech[cello]
    return bars


def tempo_text(bpm, words, unit='metNoteQuarterUp', dotted=False):
    dot = '<sym>space</sym><sym>metAugmentationDot</sym>' if dotted else ''
    return f'<b>{words}</b> <sym>{unit}</sym>{dot}<font face="Edwin"/> = {bpm}'


# 核心动机与变体
MOTIF = '(D5:e C#5:e B4:q) (F#5:q> E5:e F#5:e)'
CALL = 'r:h (F#5:q> E5:e F#5:e~)'
ECHO = '(F#5:q E4:e F#4:e) r:h'


# ================================================================
# 1. 主题曲：《安眠者有福》（Main Theme）— 40 小节六段叙事曲
# ================================================================
def main_theme():
    CEL, BEL, ORG, RH, LH, STR, CB = range(7)
    parts = [
        p_celesta(part_id=1, pan=0.35),
        p_bells(part_id=2, pan=-0.3, gain=0.8),
        p_harmonium(part_id=3, pan=-0.2),
        p_piano(part_id=4, staves=2, pan=0.1),
        p_cellos(part_id=6, pan=-0.1, gain=0.9),
        p_basses(part_id=7, pan=-0.15, gain=0.9),
    ]
    bars = []

    # --- 引子 · 门外（第 1–4 小节）：管道漏音、回声与半音叹息内声部 ---
    bars.append(Bar(
        {
            CEL: 'r:h (F#5:q> E5:e F#5:e~)',
            BEL: 'B4:w~',
            ORG: 'r:w',
            LH: 'B1+F#2:h. (A1+F#2:q)',
            STR: 'B2:w~',
        },
        harm=['Bm', 'Bm/A'], lead=CEL,
        dyn={CEL: 'p', BEL: 'pp', STR: 'pp', ORG: 'pp', RH: 'p', LH: 'pp'},
        mark='引子 · 门外', text={CEL: 'lontano, come da un tubo'},
        tempo=(63, tempo_text(63, 'Lento, cullando')),
    ))
    bars.append(Bar(
        {
            CEL: 'F#5:q (F#4:q E4:e F#4:e) r:q',
            BEL: 'B4:h r:h',
            ORG: '(D4+F#4:h C#4+E4:h)',
            LH: 'G1+D2:h. F#1+C#2:q',
            STR: 'B2:h (E2:q F#2:q)',
        },
        harm=['Gmaj7', 'F#7'], lead=CEL,
    ))
    bars.append(Bar(
        {
            CEL: 'r:h (G5:q> F#5:e G5:e~)',
            ORG: '(B3+D4+G4:h. B3+E4+G4:q)',
            RH: 'r:q (D4:q B3:h)',
            LH: '(G1:e D2:e B2:e D3:e) (E1:e B1:e G2:e B2:e)',
            STR: '(G2:h. E2:q)',
        },
        harm=['Gmaj7', 'Em9'], lead=CEL, hairpin={ORG: ('cresc', 2)},
    ))
    bars.append(Bar(
        {
            CEL: 'G5:q (F#4:q E4:e F#4:e) r:q',
            ORG: '(C#4+F#4+B4:h C#4+F#4+A#4:h)',
            RH: 'r:q (C#4:q A#3:h)',
            LH: '(F#1:e C#2:e F#2:e A#2:e) F#1+C#2:h',
            STR: '(F#2:h F#2:h)',
        },
        harm=['F#sus4', 'F#7'], lead=CEL,
    ))

    # --- A · 安眠（第 5–12 小节）：钢琴双声部歌唱完整“门句”，大提琴抒情对位 ---
    a_bars = [
        # m5
        (
            f'{MOTIF} || D4:h B3:h',
            '(B1:e F#2:e B2:e D3:e) (G1:e D2:e B2:e F#3:e)',
            '(B2:h G2:h)',
            ['Bm', 'Gmaj7'],
        ),
        # m6
        (
            '(G5:q. F#5:e E5:q D5:q) || B4:h A4:q G4:q',
            '(E2:e B2:e G3:e B3:e) (A1:e E2:e C#3:e G3:e)',
            '(E3:q. D3:e C#3:q B2:q)',
            ['Em7', 'A7'],
        ),
        # m7
        (
            '(C#5:q D5:e E5:e F#5:q B4:q) || A4:h F#4:q F#4:q',
            '(D2:e A2:e F#3:e A3:e) (B1:e F#2:e D3:e F#3:e)',
            '(A2:h B2:q D3:q)',
            ['D', 'Bm'],
        ),
        # m8
        (
            '(C#5:h A#4:q.) (B4:e) || F#4:h E4:q. r:e',
            '(G1:e D2:e B2:eE3:e) (F#1:e C#2:e A#2:e C#3:e)'.replace('B2:eE3:e', 'B2:e E3:e'),
            '(E3:q D3:q C#3:h)',
            ['Em7', 'F#7'],
        ),
        # m9
        (
            f'{MOTIF} || D4:h D4:q C#4:q',
            '(B1:e F#2:e B2:e D3:e) (G1:e D2:e B2:e F#3:e)',
            '(B2:q. A2:e G2:h)',
            ['Bm', 'Gmaj7'],
        ),
        # m10
        (
            '(A5:q. G5:e F#5:q E5:q) || C#5:h B4:q B4:q',
            '(F#1:e D2:e A2:e D3:e) (E2:e B2:e G3:e B3:e)',
            '(F#2:h G2:q E2:q)',
            ['D/F#', 'Em7'],
        ),
        # m11
        (
            '(D5:q C#5:e B4:e C#5:q A#4:q) || G4:h F#4:q E4:q',
            '(G1:e D2:e B2:e D3:e) (F#1:e C#2:e A#2:e C#3:e)',
            '(B2:q G2:q F#2:h)',
            ['G', 'F#7'],
        ),
        # m12
        (
            'B4:h. (C#5:e D5:e) || D4:h. r:q',
            '(B1:e F#2:e B2:e D3:e) (A1:e F#2:e B2:e D3:e)',
            '(B2:h. A2:q)',
            ['Bm', 'Bm/A'],
        ),
    ]
    for i, (rh, lh, vc, ch) in enumerate(a_bars):
        kw = (dict(dyn={RH: 'p', LH: 'pp', STR: 'p'}, mark='A · 安眠', text={RH: 'semplice, cantabile'},
                   hairpin={RH: ('cresc', 4)}) if i == 0 else {})
        bars.append(Bar({RH: rh, LH: lh, STR: vc}, harm=ch, lead=RH, **kw))

    # --- A2 · 誓约（第 13–20 小节）：簧风琴圣咏主奏，钢琴高音流水琶音与钢片琴卡农应答 ---
    a2_bars = [
        # m13
        (
            MOTIF,
            'r:q (F#5:e B5:e D6:e B5:e F#5:e D5:e)',
            '(B1:e F#2:e D3:e F#3:e) (G1:e D2:e B2:e D3:e)',
            '(B2:h G2:h)',
            None,
            ['Bm', 'Gmaj7'],
        ),
        # m14
        (
            '(G5:q. F#5:e E5:q D5:q)',
            'r:q (G5:e B5:e E6:e C#6:e A5:e G5:e)',
            '(E2:e B2:e G3:e B3:e) (A1:e E2:e C#3:e G3:e)',
            '(E3:h A2:h)',
            'r:h (B5:q A5:q)',
            ['Em7', 'A7'],
        ),
        # m15
        (
            '(C#5:q D5:e E5:e F#5:q B4:q)',
            'r:q (A5:e D6:e F#6:e D6:e B5:e F#5:e)',
            '(D2:e A2:e F#3:e A3:e) (B1:e F#2:e D3:e F#3:e)',
            '(F#3:q D3:q B2:h)',
            None,
            ['D', 'Bm'],
        ),
        # m16
        (
            '(C#5:h A#4:q) r:q',
            '(G5:e B5:e E6:e B5:e) (F#5:e A#5:e C#6:e F#5:e)',
            '(E2:e B2:e G3:e B3:e) (F#1:e C#2:e A#2:e C#3:e)',
            '(G2:h F#2:h)',
            'r:h (F#6:q> E6:e F#6:e)',
            ['Em7', 'F#7'],
        ),
        # m17
        (
            MOTIF,
            'r:q (F#5:e B5:e D6:e B5:e G5:e D5:e)',
            '(B1:e F#2:e D3:e F#3:e) (G1:e D2:e B2:e D3:e)',
            '(B2:h G2:h)',
            None,
            ['Bm', 'Gmaj7'],
        ),
        # m18
        (
            '(A5:q. G5:e F#5:q E5:q)',
            'r:q (A5:e D6:e F#6:e E6:e B5:e G5:e)',
            '(D2:e A2:e F#3:e A3:e) (E2:e B2:e G3:e B3:e)',
            '(D3:q C#3:q B2:q G2:q)',
            'r:h (D6:q B5:q)',
            ['D', 'Em7'],
        ),
        # m19
        (
            '(D5:q. C#5:e B4:q C#5:q)',
            '(G5:e B5:e D6:e B5:e) (E5:e A5:e C#6:e A5:e)',
            '(G1:e D2:e B2:e D3:e) (A1:e E2:e C#3:e G3:e)',
            '(G2:h A2:h)',
            None,
            ['G', 'A7'],
        ),
        # m20
        (
            'D5:h. r:q',
            '(F#5:e A5:e D6:e F#6:e A6:q) r:q',
            '(D2:e A2:e D3:e F#3:e) D2+A2:h',
            '(D3:h A2:q F#2:q)',
            '(A5:h D6:h)',
            ['D'],
        ),
    ]
    for i, (org, rh, lh, vc, cel, ch) in enumerate(a2_bars):
        lines = {ORG: org, RH: rh, LH: lh, STR: vc}
        if cel:
            lines[CEL] = cel
        kw = (dict(dyn={ORG: 'mp', RH: 'p', LH: 'p', STR: 'p', CEL: 'p'}, mark='A2 · 誓约',
                   text={ORG: 'legato, come un inno'}, hairpin={ORG: ('cresc', 4)}) if i == 0 else {})
        bars.append(Bar(lines, harm=ch, lead=ORG, **kw))

    # --- B · 旧日的灯（第 21–28 小节）：D 大调展开段，引入三连音、跨拍延音线与大提琴高音独奏对位 ---
    b_bars = [
        # m21
        (
            '(F#5:q. G5:e A5:q F#5:q~)',
            '(D4:h F#4:h)',
            'F#3+A3+D4:h F#3+B3+D4:h',
            '(D2:e A2:e D3:e F#3:e) (B1:e F#2:e B2:e D3:e)',
            ['D', 'Bm'],
        ),
        # m22
        (
            'F#5:q (E5:e3 F#5:e3 G5:e3 F#5:q E5:q)',
            '(A4:q. G4:e F#4:h)',
            'E3+A3+C#4:h G3+B3+E4:h',
            '(C#2:e A2:e E3:e A3:e) (E2:e B2:e G3:e B3:e)',
            ['A/C#', 'Em'],
        ),
        # m23
        (
            '(D5:q E5:e F#5:e G5:q B4:q)',
            '(F#4:h E4:q D4:q)',
            'F#3+B3+D4:h G3+B3+E4:h',
            '(B1:e F#2:e D3:e F#3:e) (E2:e B2:e G3:e B3:e)',
            ['Bm', 'Em'],
        ),
        # m24
        (
            '(C#5:h. A4:q)',
            '(E4:q F#4:q G4:q E4:q)',
            'E3+A3+C#4:h E3+G3+C#4:h',
            '(A1:e E2:e C#3:e E3:e) (A1:e E2:e G2:e C#3:e)',
            ['A', 'A7'],
        ),
        # m25
        (
            '(F#5:q. G5:e A5:q D6:q~)',
            '(D4:q F#4:q B4:h)',
            'F#3+A3+D4:h F#3+B3+D4:h',
            '(D2:e A2:e D3:e F#3:e) (B1:e F#2:e B2:e D3:e)',
            ['D', 'Bm'],
        ),
        # m26
        (
            'D6:q (C#6:e3 B5:e3 A5:e3 B5:q G5:q)',
            '(A4:h G4:q F#4:q)',
            'F#3+A3+C#4:h G3+B3+D4:h',
            '(F#1:e C#2:e A2:e C#3:e) (G1:e D2:e B2:e D3:e)',
            ['F#m', 'G'],
        ),
        # m27
        (
            '(F#5:q B5:q A5:e G5:e E5:q)',
            '(D4:q E4:q C#4:h)',
            'F#3+B3+D4:q G3+B3+E4:q E3+A3+C#4:h',
            '(B1:e F#2:e D3:e F#3:e) (A1:e E2:e C#3:e G3:e)',
            ['Bm', 'Em', 'A7'],
        ),
        # m28
        (
            '(F#5:h E5:e D5:e C#5:q)',
            '(C#4:h F#3:h)',
            'F#3+B3+C#4:h E3+A#3+C#4:h',
            '(F#1:e C#2:e F#2:e B2:e) (F#1:e C#2:e E2:e A#2:e)',
            ['F#sus4', 'F#7'],
        ),
    ]
    for i, (mel, vc_mel, org_pad, lh, ch) in enumerate(b_bars):
        lines = {RH: mel, CEL: shift(mel, 1), ORG: org_pad, LH: lh, STR: vc_mel}
        kw = {}
        if i == 0:
            lines[BEL] = 'D5:w'
            kw = dict(
                dyn={RH: 'mf', CEL: 'p', ORG: 'p', LH: 'mp', STR: 'mf', BEL: 'p'},
                mark='B · 旧日的灯', text={RH: 'con calore, rubato', STR: 'espressivo'},
                hairpin={RH: ('cresc', 5), STR: ('cresc', 5)},
            )
        bars.append(Bar(lines, harm=ch, lead=RH, **kw))

    # --- A' · 有福（第 29–36 小节）：全奏交响化高潮与半音下行收拢 ---
    a3_bars = [
        # m29
        (
            octaves(MOTIF, 1),
            'F#3+B3+D4:h G3+B3+D4:h',
            '(B1:e F#2:e B2:e D3:e) (G1:e D2:e G2:e B2:e)',
            '(B2:q. A2:e G2:h)',
            'B4:w',
            None,
            ['Bm', 'Gmaj7'],
        ),
        # m30
        (
            octaves('(G5:q. F#5:e E5:q D5:q)', 1),
            'G3+B3+E4:h G3+A3+C#4:h',
            '(E1:e B1:e E2:e G2:e) (A1:e E2:e A2:e C#3:e)',
            '(E3:h A2:q G2:q)',
            None,
            None,
            ['Em7', 'A7'],
        ),
        # m31
        (
            octaves('(C#5:q D5:e E5:e F#5:q B4:q)', 1),
            'F#3+A3+D4:h F#3+B3+D4:h',
            '(D2:e A2:e D3:e F#3:e) (B1:e F#2:e B2:e D3:e)',
            '(F#2:h B2:q D3:q)',
            None,
            None,
            ['D', 'Bm'],
        ),
        # m32
        (
            octaves('(C#5:h A#4:q) r:q', 1),
            'G3+B3+E4:h F#3+A#3+C#4:h',
            '(C#2:e G2:e B2:e E3:e) (F#1:e C#2:e F#2:e A#2:e)',
            '(C#3:h F#2:h)',
            None,
            'r:h (F#6:q> E6:e F#6:e)',
            ['C#o7', 'F#7'],
        ),
        # m33 (高潮顶点)
        (
            octaves(MOTIF, 1),
            'F#3+B3+D4:h G3+B3+D4:h',
            '(B1:e F#2:e B2:e D3:e) (A1:e F#2:e B2:e D3:e)',
            '(B2:h A2:h)',
            'F#4:w',
            '(D6:e C#6:e B5:q) (F#6:q> E6:e F#6:e)',
            ['Bm', 'Bm/A'],
        ),
        # m34
        (
            octaves('(B5:h A5:q G5:q)', 1),
            'G3+B3+E4:h G3+A3+C#4:h',
            '(G1:e D2:e G2:e B2:e) (E1:e B1:e E2:e G2:e)',
            '(G2:h E2:h)',
            None,
            '(B5:h A5:q G5:q)',
            ['G', 'Em'],
        ),
        # m35
        (
            octaves('(F#5:q E5:e D5:e C#5:q A#4:q)', 1),
            'F#3+B3+D4:q G3+B3+E4:q F#3+A#3+C#4:h',
            '(F#1:e C#2:e F#2:e B2:e) (F#1:e C#2:e E2:e A#2:e)',
            '(F#2:q G2:q F#2:h)',
            None,
            None,
            ['Bm/F#', 'C#o7', 'F#7'],
        ),
        # m36
        (
            'B4+B5:w~ || D4+F#4:h. r:q',
            'F#3+B3+D4:w',
            '(B1:e F#2:e B2:e D3:e F#3:h)',
            'B2:w',
            None,
            None,
            ['Bm'],
        ),
    ]
    for i, (rh, org_pad, lh, vc, bel, cel, ch) in enumerate(a3_bars):
        lines = {RH: rh, ORG: org_pad, LH: lh, STR: vc}
        if bel:
            lines[BEL] = bel
        if cel:
            lines[CEL] = cel
        kw = {}
        if i == 0:
            kw = dict(dyn={RH: 'f', ORG: 'mf', LH: 'mf', STR: 'f', BEL: 'mp', CEL: 'mf'},
                      mark="A' · 有福", text={RH: 'largamente, appassionato'})
        elif i == 4:
            kw = dict(dyn={RH: 'ff', ORG: 'f', STR: 'ff', CEL: 'f'})
        elif i == 6:
            kw = dict(dyn={RH: 'mf', ORG: 'mp', LH: 'mp', STR: 'mp'}, text={RH: 'dim. e rall.'},
                      hairpin={RH: ('dim', 2), ORG: ('dim', 2), STR: ('dim', 2)})
        bars.append(Bar(lines, harm=ch, lead=RH, **kw))

    # --- 尾声 · 回答（第 37–40 小节）：同主音 B 大调皮卡第解决，问句终于落回主音 ---
    bars.append(Bar(
        {
            CEL: 'r:h (F#5:q> E5:e F#5:e)',
            RH: 'B4+B5:h (D#4+F#4+B4:h)',
            ORG: 'F#3+B3+D#4:w~',
            LH: '(B1:e F#2:e B2:e D#3:e F#3:h)',
            STR: 'B2:w~',
        },
        harm=['B'], lead=CEL,
        dyn={CEL: 'p', RH: 'pp', ORG: 'pp', STR: 'pp', LH: 'pp'},
        mark='尾声 · 回答', text={CEL: 'dolcissimo, luminoso'},
    ))
    bars.append(Bar(
        {
            CEL: '(E5:h D#5:q B4:q)',
            RH: '(E4+G#4+B4:h D#4+F#4+B4:h)',
            ORG: 'F#3+B3+D#4:h (E3+G#3+B3:h)',
            LH: '(B1:e E2:e G#2:e B2:e) (B1:e F#2:e B2:e D#3:e)',
            STR: 'B2:h (G#2:h)',
        },
        harm=['E/B', 'B'], lead=CEL,
    ))
    bars.append(Bar(
        {
            CEL: 'r:h (F#5:q E5:e D#5:e)',
            RH: '(D4+G4+B4:h C#4+F#4+A#4:h)',
            ORG: '(D3+G3+B3:h C#3+F#3+A#3:h)',
            LH: '(G1:e D2:e G2:e B2:e) (F#1:e C#2:e F#2:e A#2:e)',
            STR: '(G2:h F#2:h)',
        },
        dyn={CEL: 'pp'}, harm=['Gmaj7', 'F#7'], lead=CEL,
        hairpin={RH: ('dim', 2), ORG: ('dim', 2)},
    ))
    bars.append(Bar(
        {
            CEL: '(D#5:h B4:h^)',
            BEL: 'B4:w^',
            RH: 'D#4+F#4+B4:w^',
            ORG: 'F#3+B3+D#4:w^',
            LH: 'B1+F#2+B2:w^',
            STR: 'B2:w^',
        },
        dyn={BEL: 'pp', RH: 'ppp', CEL: 'pp'}, harm=['B'], lead=CEL,
    ))

    # 低音提琴在 B 段大提琴高音独奏时保持稳健低音根音，避免音区过高
    custom_cb = {
        20: 'D2:h B1:h',
        21: 'C#2:h E2:h',
        22: 'B1:h E2:h',
        23: 'A1:h A1:h',
        24: 'D2:h B1:h',
        25: 'F#1:h G1:h',
        26: 'B1:q E2:q A1:h',
        27: 'F#1:h F#1:h',
    }
    bars = with_contrabass(bars, STR, CB, custom_bass=custom_cb)
    return Piece(
        'main_theme', '安眠者有福', 'Beati qui dormiunt — 主题', 2, (4, 4), parts, bars,
        reverb=2.6, wet=0.3, mix={CEL: 8, BEL: 13, ORG: -2, RH: 3, LH: 2, STR: -4, CB: -7},
        style=dict(rubato=1.0, swell=1.0, pedal={RH: 'full', LH: 'full'}), squeeze=1.8,
    )


# ================================================================
# 2. 第一章现场勘查：《门的这一侧》（The Near Side of the Door）— 32 小节四段叙事发展
# ================================================================
def door_side():
    """四段式悬疑室内乐：一·晨雾与配重 → 二·灰迹勘查 → 三·声路与门隙（复调卡农追问）→ 四·门内余响。"""
    CEL, ORG, RH, LH, STR = range(5)
    parts = [
        p_celesta(part_id=1, pan=0.45, gain=0.85),
        p_harmonium(part_id=2, pan=-0.3, gain=0.7),
        p_piano(part_id=3, staves=2),
        p_strings(part_id=4, pan=-0.15, gain=0.75),
    ]

    # 钟摆音型（带五度支点与跨小节微变）
    pend_bm = 'B2:e F#3:e D4:e F#3:e C#4:e F#3:e B3:e F#3:e'
    pend_gm = 'G2:e D3:e B3:e D3:e A3:e D3:e F#3:e D3:e'
    pend_em = 'E2:e B2:e G3:e B2:e F#3:e B2:e E3:e B2:e'
    pend_fs = 'F#2:e C#3:e B3:e C#3:e A#3:e C#3:e F#3:e C#3:e'
    pend_df = 'F#2:e D3:e A3:e D3:e C#4:e D3:e A3:e D3:e'
    pend_a6 = 'A2:e E3:e C#4:e E3:e F#3:e E3:e C#4:e E3:e'
    pend_cm = 'C2:e G2:e E3:e G2:e F#3:e G2:e B2:e G2:e'

    bars = []

    # --- 一 · 晨雾与配重（第 1–8 小节）：冷寂的钟摆伴奏与零星泛音，管道远端隐现呼喊 ---
    sec1 = [
        ('r:h (B4+F#5:h~)', pend_bm, 'B1:w~', None, ['Bm9']),
        ('B4+F#5:q r:q (A4+D5:h)', pend_gm, 'B1:h G1:h', None, ['Gmaj9']),
        ('r:q (G4:q F#4:h)', pend_em, 'E2:w', None, ['Em9']),
        ('(C#5:h. A#4:q)', pend_fs, 'F#1:w', CALL, ['F#sus4', 'F#7']),
        ('r:h (D5+F#5:h~)', pend_bm, 'B1:w', None, ['Bm9']),
        ('D5+F#5:q (E5:q D5:h)', pend_gm, 'G1:w', None, ['Gmaj9']),
        ('r:q (C#5:q B4:q A4:q)', pend_df, 'F#1:w', None, ['Dmaj7/F#']),
        ('(B4:h C#5:h)', pend_a6, 'A1:w', 'r:h (F#5:q> E5:e F#5:e)', ['A6']),
    ]
    for i, (rh, lh, st, cel, ch) in enumerate(sec1):
        lines = {RH: rh, LH: lh, STR: st}
        if cel:
            lines[CEL] = cel
        kw = {}
        if i == 0:
            kw = dict(
                mark='一 · 晨雾与配重',
                dyn={RH: 'p', LH: 'pp', CEL: 'pp', STR: 'pp', ORG: 'ppp'},
                tempo=(72, tempo_text(72, 'Andante, come un pendolo')),
                text={LH: 'sempre legato, come un contrappeso'},
            )
        bars.append(Bar(lines, harm=ch, lead=RH, **kw))

    # --- 二 · 灰迹勘查（第 9–16 小节）：右手浮现探索旋律（含内声部对位），弦乐步进低音 ---
    sec2 = [
        ('r:q (B4:q C#5:q D5:q) || r:h F#4:h', pend_bm, '(B1:h F#2:h)', 'F#3+B3+D4:w', None, ['Bm9']),
        ('(F#5:h. E5:q) || B4:h. G4:q', pend_gm, '(G1:h D2:h)', 'F#3+B3+D4:w', None, ['Gmaj9']),
        ('(D5:h B4:q. G4:e) || G4:h E4:h', pend_em, '(E2:h B1:h)', 'G3+B3+D4:w', None, ['Em9']),
        ('(C#5:h. A#4:q) || F#4:h E4:h', pend_fs, '(F#1:h C#2:h)', 'F#3+B3+C#4:h F#3+A#3+C#4:h', CALL, ['F#sus4', 'F#7']),
        ('r:q (B4:q C#5:q D5:q) || r:h F#4:h', pend_bm, '(B1:h A1:h)', 'F#3+B3+D4:w', None, ['Bm9']),
        ('(A5:h. G5:q) || D5:h B4:h', pend_gm, '(G1:h B1:h)', 'F#3+A3+D4:w', None, ['Gmaj9']),
        ('(F#5:h E5:q D5:q) || A4:h G4:h', pend_df, '(F#1:h A1:h)', 'A3+C#4+D4:w', None, ['Dmaj7/F#']),
        ('(C#5:h. B4:q) || E4:h. r:q', pend_a6, '(A1:h E2:h)', 'E3+F#3+C#4:w', None, ['A6']),
    ]
    for i, (rh, lh, st, org, cel, ch) in enumerate(sec2):
        lines = {RH: rh, LH: lh, STR: st, ORG: org}
        if cel:
            lines[CEL] = cel
        kw = dict(mark='二 · 灰迹勘查', dyn={RH: 'mp', STR: 'p', ORG: 'pp'},
                  text={RH: 'indagando, espressivo'}, hairpin={RH: ('cresc', 4)}) if i == 0 else {}
        bars.append(Bar(lines, harm=ch, lead=RH, **kw))

    # --- 三 · 声路与门隙（第 17–24 小节）：转入 e 小调与 Cmaj7#11 色彩，钢琴与钢片琴卡农式追问 ---
    sec3 = [
        ('(G5:h F#5:q E5:q) || B4:w', pend_em, '(E2:h G2:h)', 'G3+B3+E4:w', 'r:h (E6:q D6:e E6:e)', ['Em9']),
        ('(D5:h. B4:q) ||G4:h F#4:h'.replace('||G4', '|| G4'), pend_gm, '(G1:h B1:h)', 'F#3+B3+D4:w', None, ['Gmaj9']),
        ('r:q (F#5:q B5:q A5:q) || B4:h D5:h', pend_bm, '(B1:h D2:h)', 'F#3+B3+D4:w', None, ['Bm9']),
        ('(G#5:h C#5:h) || B4:h A#4:h', pend_fs, '(C#2:h F#1:h)', 'G#3+B3+E4:h F#3+A#3+C#4:h', CALL, ['E/G#', 'F#7']),
        ('(G5:q. F#5:e E5:q D5:q) || B4:h B4:h', pend_em, '(E2:h D2:h)', 'G3+B3+E4:w', None, ['Em9']),
        ('(F#5:h. E5:q) || C5:h B4:h', pend_cm, '(C2:h G1:h)', 'F#3+B3+E4:w', 'r:h (F#6:q> E6:e F#6:e)', ['Cmaj7#11']),
        ('(D5:h B4:q G4:q) || G4:h D4:h', pend_gm, '(G1:hE1:h)'.replace('hE1', 'h E1'), 'F#3+B3+D4:w', None, ['Gmaj7']),
        ('(C#5:h A#4:h) || F#4:h E4:h', pend_fs, '(F#1:h C#2:h)', 'F#3+B3+C#4:h F#3+A#3+C#4:h', None, ['F#sus4', 'F#7']),
    ]
    for i, (rh, lh, st, org, cel, ch) in enumerate(sec3):
        lines = {RH: rh, LH: lh, STR: st, ORG: org}
        if cel:
            lines[CEL] = cel
        kw = dict(mark='三 · 声路与门隙', dyn={RH: 'mf', ORG: 'p', STR: 'mp', CEL: 'mp'},
                  text={CEL: 'eco ravvicinato'}, hairpin={RH: ('cresc', 4)}) if i == 0 else {}
        bars.append(Bar(lines, harm=ch, lead=RH, **kw))

    # --- 四 · 门内余响（第 25–32 小节）：拼合门句轮廓，渐弱收束回冷寂钟摆 ---
    sec4 = [
        (MOTIF, pend_bm, 'B1:w', 'F#3+B3+D4:w', None, ['Bm9']),
        ('(G5:h F#5:q D5:q)', pend_gm, 'G1:w', 'F#3+B3+D4:w', None, ['Gmaj9']),
        ('(E5:h B4:h)', pend_em, 'E2:w', 'G3+B3+D4:w', None, ['Em9']),
        ('C#5:w~', pend_fs, 'F#1:w', 'F#3+B3+C#4:h F#3+A#3+C#4:h', CALL, ['F#sus4', 'F#7']),
        ('C#5:q (D5:e C#5:e B4:h)', pend_bm, 'B1:w', 'F#3+B3+D4:w', None, ['Bm9']),
        ('r:h (F#5:q E5:q)', pend_gm, 'G1:w', 'F#3+B3+D4:w', None, ['Gmaj9']),
        ('(D5:h C#5:h)', pend_df, 'F#1:w', 'A3+C#4+D4:w', None, ['Dmaj7/F#']),
        ('A#4:h r:h', pend_fs, 'F#1:h C#2:h', 'F#3+A#3+C#4:w', 'r:h (F#5:q E5:e F#5:e)', ['F#7']),
    ]
    for i, (rh, lh, st, org, cel, ch) in enumerate(sec4):
        lines = {RH: rh, LH: lh, STR: st, ORG: org}
        if cel:
            lines[CEL] = cel
        kw = {}
        if i == 0:
            kw = dict(mark='四 · 门内余响', dyn={RH: 'p', ORG: 'pp', STR: 'p', CEL: 'pp'},
                      hairpin={RH: ('dim', 4), ORG: ('dim', 4)})
        bars.append(Bar(lines, harm=ch, lead=RH, **kw))

    return Piece(
        'door_side', '门的这一侧', '第一章 · 现场勘查（四段叙事循环）', 2, (4, 4), parts, bars,
        reverb=1.8, wet=0.28, mix={CEL: 9, ORG: 2, RH: 3, LH: -0.5, STR: -7},
        style=dict(rubato=0.4, swell=0.6, pedal={RH: 'half', LH: 'none'}), squeeze=2.2,
    )


# ================================================================
# 3. 思辨博弈：《对质》（Confrontation）— 24 小节三段式交锋
# ================================================================
def confrontation():
    """三段式博弈：一·盘诘（帕萨卡利亚半音下行与卡农）→ 二·字面为真（恶魔诡辩与那不勒斯变化音）→ 三·破绽与阻碍终止。"""
    ORG, RH, LH, STR = range(4)
    parts = [
        p_harmonium(part_id=1, pan=-0.25),
        p_piano(part_id=2, staves=2, pan=0.1),
        p_strings(part_id=3, pan=-0.05),
    ]

    def drive_lh(root_lo, root_hi, fifth, syncopated=False):
        if syncopated:
            return f"{root_lo}:e' ({fifth}:q {root_hi}:e) {root_lo}:e' ({fifth}:q {root_hi}:e)"
        return f"{root_lo}+{root_hi}:e> {fifth}:e' {root_hi}:e' {fifth}:e' {root_lo}+{root_hi}:e> {fifth}:e' {root_hi}:e' {fifth}:e'"

    bars = []

    # --- 一 · 盘诘（第 1–8 小节）：帕萨卡利亚半音下行低音（B-A#-A-G#-G-F#-E-F#），钢琴与风琴卡农追问 ---
    sec1 = [
        # m1: B
        (
            '(D5:e C#5:e B4:q) r:h',
            'r:h (F#4+B4+D5:q> E4+B4:e F#4+B4:e)',
            drive_lh('B1', 'B2', 'F#2'),
            'B2:h. B2:q',
            ['Bm'],
        ),
        # m2: A#
        (
            'r:h (C#5:e B4:e A#4:q)',
            '(F#4+C#5:q. E5:e D5:q C#5:q)',
            drive_lh('A#1', 'A#2', 'F#2'),
            'A#2:h. A#2:q',
            ['F#/A#'],
        ),
        # m3: A
        (
            '(F#5:q> E5:e D5:e C#5:h)',
            'r:q (D4+F#4+B4:q C#4+F#4+A4:h)',
            drive_lh('A1', 'A2', 'F#2'),
            'A2:h. A2:q',
            ['Bm/A'],
        ),
        # m4: G#
        (
            '(B4:q E5:q D5:q B4:q)',
            '(E4+G#4+B4:h E4+G#4+D5:h)',
            drive_lh('G#1', 'G#2', 'E2'),
            'G#2:h. G#2:q',
            ['E/G#'],
        ),
        # m5: G
        (
            '(B4:e C#5:e D5:q G5:q F#5:q)',
            'r:q (D4+G4+B4:q D4+F#4+B4:h)',
            drive_lh('G1', 'G2', 'D2'),
            'G2:h. G2:q',
            ['Gmaj7'],
        ),
        # m6: F#
        (
            '(E5:q D5:e C#5:e D5:h)',
            '(D4+F#4+A4:h D4+F#4+B4:h)',
            drive_lh('F#1', 'F#2', 'D2'),
            'F#2:h. F#2:q',
            ['D/F#'],
        ),
        # m7: E
        (
            '(G5:q F#5:e E5:e D5:q B4:q)',
            '(E4+G4+B4:h E4+G4+C#5:h)',
            drive_lh('E1', 'E2', 'B1'),
            'E2:h. E2:q',
            ['Em7'],
        ),
        # m8: F#
        (
            '(C#5:h A#4:h)',
            '(F#4+B4+C#5:h F#4+A#4+C#5:h)',
            drive_lh('F#1', 'F#2', 'C#2'),
            '(F#2:h C#2:h)',
            ['F#sus4', 'F#7'],
        ),
    ]
    for i, (org, rh, lh, st, ch) in enumerate(sec1):
        kw = {}
        if i == 0:
            kw = dict(
                mark='一 · 盘诘（帕萨卡利亚）',
                dyn={ORG: 'mp', RH: 'mf', LH: 'mf', STR: 'mp'},
                tempo=(88, tempo_text(88, 'Moderato, inesorabile')),
                text={LH: 'marcato, ostinato cromatico'},
                hairpin={ORG: ('cresc', 4), STR: ('cresc', 4)},
            )
        bars.append(Bar({ORG: org, RH: rh, LH: lh, STR: st}, harm=ch, lead=ORG, **kw))

    # --- 二 · 字面为真（第 9–16 小节）：恶魔的诡辩变奏，切分低音、那不勒斯 C♮ 折射与双声部反行 ---
    sec2 = [
        # m9
        (
            '(G4+B4:h F#4+B4:h)',
            octaves('(E5:q F#5:q G5:q. A5:e)'),
            drive_lh('E2', 'E3', 'B2', syncopated=True),
            '(E2:q. F#2:e G2:h)',
            ['Em'],
        ),
        # m10
        (
            '(F#4+A#4:h E4+A#4:h)',
            octaves('(A#5:h. C#6:q)'),
            drive_lh('F#1', 'F#2', 'C#2', syncopated=True),
            '(F#2:h E2:h)',
            ['F#7'],
        ),
        # m11
        (
            '(D4+G4+B4:h E4+G4+C5:h)',
            octaves('(B5:h G5:q E5:q)'),
            drive_lh('G1', 'G2', 'D2', syncopated=True),
            '(G2:h C2:h)',
            ['G', 'Cmaj7'],
        ),
        # m12 (那不勒斯 Cmaj7#11 -> F#7)
        (
            '(E4+F#4+B4:h E4+F#4+A#4:h)',
            octaves('(F#5:q E5:e D5:e C#5:q A#4:q)'),
            drive_lh('C2', 'C3', 'G2', syncopated=True),
            '(C2:h F#2:h)',
            ['Cmaj7#11', 'F#7'],
        ),
        # m13
        (
            '(D4+F#4+B4:h D4+F#4+A4:h)',
            octaves('(B4:q C#5:q D5:q. E5:e)'),
            drive_lh('B1', 'B2', 'F#2', syncopated=True),
            '(B2:h A2:h)',
            ['Bm', 'Bm/A'],
        ),
        # m14
        (
            '(E4+G4+B4:h E4+G4+C#5:h)',
            octaves('(F#5:q G5:e F#5:e E5:q D5:q)'),
            drive_lh('G1', 'G2', 'D2', syncopated=True),
            '(G2:h E2:h)',
            ['Gmaj7', 'Em7'],
        ),
        # m15
        (
            '(E4+G4+C5:h D4+G4+B4:h)',
            octaves('(E5:q C5:q B4:q G4:q)'),
            drive_lh('C2', 'C3', 'G2', syncopated=True),
            '(C2:h G2:h)',
            ['C', 'G'],
        ),
        # m16
        (
            '(C#4+F#4+B4:h C#4+F#4+A#4:h)',
            octaves('(C#5:h A#4:h)'),
            drive_lh('F#1', 'F#2', 'C#2', syncopated=True),
            '(F#2:q E2:q D2:q C#2:q)',
            ['F#sus4', 'F#7'],
        ),
    ]
    for i, (org, rh, lh, st, ch) in enumerate(sec2):
        kw = {}
        if i == 0:
            kw = dict(mark='二 · 字面为真', dyn={RH: 'f', ORG: 'mp', LH: 'mp', STR: 'mf'},
                      text={RH: 'tenuto, con ambiguità'})
        bars.append(Bar({ORG: org, RH: rh, LH: lh, STR: st}, harm=ch, lead=RH, **kw))

    # --- 三 · 破绽与阻碍终止（第 17–24 小节）：全奏正面交锋，句末连续阻碍终止（高音 B4 落 G/Em）---
    sec3 = [
        # m17
        (
            f'{MOTIF} || F#4:h G4:h',
            "r:q D4+F#4+B4:q' r:q D4+G4+B4:q'",
            drive_lh('B1', 'B2', 'F#2'),
            '(B2:q B2:q G2:q G2:q)',
            ['Bm', 'Gmaj7'],
        ),
        # m18
        (
            '(G5:q. F#5:e E5:q D5:q) || B4:h A4:h',
            "r:q E4+G4+B4:q' r:q E4+G4+C#5:q'",
            drive_lh('E2', 'E3', 'B2'),
            '(E2:q G2:q A2:q A1:q)',
            ['Em7', 'A7'],
        ),
        # m19
        (
            '(C#5:q D5:e E5:e F#5:q B5:q) || A4:h F#4:h',
            "r:q D4+F#4+A4:q' r:q D4+F#4+B4:q'",
            drive_lh('D2', 'D3', 'A2'),
            '(D2:q F#2:q B2:q A2:q)',
            ['D', 'Bm'],
        ),
        # m20 (第一次阻碍折射)
        (
            '(A#5:h F#5:q E5:q) || C#5:h A#4:h',
            '(G4+B4+E5:h F#4+A#4+C#5:h)',
            drive_lh('F#1', 'F#2', 'C#2'),
            '(G2:h F#2:h)',
            ['C#o7/F#', 'F#7'],
        ),
        # m21
        (
            f'{MOTIF} || F#4:h B4:h',
            '(D5+F#5+B5:h D5+G5+B5:h)',
            drive_lh('B1', 'B2', 'F#2'),
            '(B2:q A2:q G2:q F#2:q)',
            ['Bm', 'Gmaj7'],
        ),
        # m22
        (
            '(B5:h A5:q G5:q) || D5:h C#5:h',
            '(E5+G5+B5:h E5+A5+C#6:h)',
            drive_lh('E2', 'E3', 'B2'),
            '(E2:q G2:q A2:q C#3:q)',
            ['Em7', 'A7'],
        ),
        # m23
        (
            '(F#5:q E5:e D5:e C#5:q A#4:q) || B4:h A#4:h',
            '(D5+F#5+B5:q C#5+G5+B5:q C#5+F#5+A#5:h)',
            drive_lh('F#1', 'F#2', 'C#2'),
            '(D3:q E3:q F#3:q F#2:q)',
            ['Bm/F#', 'C#o7', 'F#7'],
        ),
        # m24 (恶魔的阻碍终止：旋律落在正确的 B4，低音与和声却滑向 Gmaj7 -> C#o7 -> F#7)
        (
            'B4:h.~ (A#4:q) || D4:h E4:q C#4:q',
            '(D4+F#4+B4:h E4+G4+B4:q C#4+F#4+A#4:q)',
            '(G1:e D2:e G2:e B2:e) (E1:e B1:e F#1:e C#2:e)',
            '(G2:h E2:q F#2:q)',
            ['Gmaj7', 'Em', 'F#7'],
        ),
    ]
    for i, (org, rh, lh, st, ch) in enumerate(sec3):
        kw = {}
        if i == 0:
            kw = dict(mark='三 · 破绽与阻碍终止', dyn={ORG: 'f', RH: 'mf', LH: 'f', STR: 'f'},
                      text={ORG: 'a piena voce, serrato'}, hairpin={ORG: ('cresc', 5)})
        elif i == 7:
            kw = dict(text={ORG: 'la nota giusta, il basso sbagliato (cadenza d\'inganno)'})
        bars.append(Bar({ORG: org, RH: rh, LH: lh, STR: st}, harm=ch, lead=ORG, **kw))

    return Piece(
        'confrontation', '对质', '始业之答 · 对质（三段思辨循环）', 2, (4, 4), parts, bars,
        reverb=1.6, wet=0.22, mix={ORG: -1, RH: 3, LH: -3, STR: -5},
        style=dict(rubato=0.25, swell=0.5, pedal={RH: 'half', LH: 'none'}),
    )


# ================================================================
# 4. 夜间与守夜：《安眠》（Sleep）— 24 小节三段式夜曲
# ================================================================
def night():
    """三段式夜曲：一·熄灯（b 小调八音盒摇篮曲）→ 二·守夜人的微光（D/G 大调温暖对位）→ 三·发条将尽。"""
    BOX, BEL, ORG, LH, STR = range(5)
    parts = [
        p_celesta(part_id=1, pan=0.25),
        p_bells(part_id=2, pan=-0.45, gain=0.55),
        p_harmonium(part_id=3, pan=-0.2, gain=0.6),
        p_piano(part_id=4, bass_clef=True, pan=0.05, gain=0.8),
        p_strings(part_id=5, gain=0.8),
    ]

    # 夜曲式三拍子左手分解和弦
    arp_bm = 'B2:e F#3:e D4:e F#4:e D4:e F#3:e'
    arp_gm = 'G2:e D3:e B3:e F#4:e B3:e D3:e'
    arp_em = 'E2:e B2:e G3:e D4:e G3:e B2:e'
    arp_fs = 'F#2:e C#3:e A#3:e E4:e A#3:e C#3:e'
    arp_d = 'D2:e A2:e F#3:e D4:e F#3:e A2:e'
    arp_ac = 'C#2:e A2:e E3:e C#4:e E3:e A2:e'
    arp_fm = 'F#2:e C#3:e A3:e F#4:e A3:e C#3:e'
    arp_df = 'F#2:e D3:e A3:e D4:e A3:e D3:e'

    bars = []

    # --- 一 · 熄灯（第 1–8 小节）：三拍子放慢的门句摇篮曲 ---
    sec1 = [
        ('(D5:e C#5:e B4:h)', 'F#3+B3+D4:h.', arp_bm, 'B2:h.', 'B3:h.', ['Bm']),
        ('(F#5:q> E5:e F#5:e~ F#5:q)', 'F#3+B3+D4:h.', arp_gm, 'G2:h.', None, ['Gmaj7']),
        ('(G5:h F#5:e E5:e)', 'G3+B3+D4:h.', arp_em, '(E2:h G2:q)', None, ['Em7']),
        ('C#5:h.', 'F#3+B3+C#4:h F#3+A#3+C#4:q', arp_fs, 'F#2:h.', None, ['F#sus4', 'F#7']),
        ('(D5:e C#5:e B4:h)', 'F#3+B3+D4:h.', arp_bm, '(B2:h A2:q)', None, ['Bm']),
        ('(A5:q G5:e F#5:e E5:q)', 'F#3+B3+D4:h.', arp_gm, '(G2:h E2:q)', None, ['Gmaj7']),
        ('(D5:q C#5:q A#4:q)', 'G3+B3+E4:q F#3+A#3+C#4:h', arp_fs, '(E2:q F#2:h)', None, ['Em', 'F#7']),
        ('B4:h.', 'F#3+B3+D4:h.', arp_bm, 'B2:h.', None, ['Bm']),
    ]
    for i, (box, org, lh, st, bel, ch) in enumerate(sec1):
        lines = {BOX: box, ORG: org, LH: lh, STR: st}
        if bel:
            lines[BEL] = bel
        kw = {}
        if i == 0:
            kw = dict(
                mark='一 · 熄灯',
                tempo=(54, tempo_text(54, 'Lento, carillon')),
                dyn={BOX: 'mp', BEL: 'pp', ORG: 'ppp', LH: 'pp', STR: 'pp'},
                text={BOX: 'dolce, come un carillon'},
            )
        bars.append(Bar(lines, harm=ch, lead=BOX, **kw))

    # --- 二 · 守夜人的微光（第 9–16 小节）：转入 D 大调，风琴与弦乐奏出温暖副歌，与八音盒二重对位 ---
    sec2 = [
        ('(F#5:h A5:q)', '(D4:q F#4:q A4:q)', arp_d, '(D3:h C#3:q)', 'D4:h.', ['D']),
        ('(G5:h F#5:q)', '(E4:h C#4:q)', arp_ac, 'A2:h.', None, ['A/C#']),
        ('(F#5:q E5:e D5:e F#5:q)', '(D4:h B3:q)', arp_bm, '(B2:h A2:q)', None, ['Bm']),
        ('(E5:h C#5:q)', '(C#4:h A3:q)', arp_fm, 'F#2:h.', None, ['F#m']),
        ('(D5:h G5:q)', '(B3:q D4:q G4:q)', arp_gm, '(G2:h B2:q)', None, ['G']),
        ('(F#5:h D5:q)', '(A3:h D4:q)', arp_df, '(D3:h F#2:q)', None, ['D/F#']),
        ('(E5:q F#5:q G5:q)', '(G3+B3:h E4:q)', arp_em, '(E2:h G2:q)', None, ['Em7']),
        ('(F#5:h E5:q)', '(F#3+B3+C#4:h F#3+A#3+C#4:q)', arp_fs, 'F#2:h.', None, ['F#sus4', 'F#7']),
    ]
    for i, (box, org, lh, st, bel, ch) in enumerate(sec2):
        lines = {BOX: box, ORG: org, LH: lh, STR: st}
        if bel:
            lines[BEL] = bel
        kw = {}
        if i == 0:
            kw = dict(
                mark='二 · 守夜人的微光',
                dyn={BOX: 'mp', ORG: 'p', STR: 'p', LH: 'p', BEL: 'pp'},
                text={ORG: 'calmato, cantabile'},
                hairpin={ORG: ('cresc', 4), STR: ('cresc', 4)},
            )
        bars.append(Bar(lines, harm=ch, lead=BOX, **kw))

    # --- 三 · 发条将尽（第 17–24 小节）：高八度八音盒渐次稀疏，句尾只余悬空的“开” ---
    sec3 = [
        ('(D6:e C#6:e B5:h)', 'F#3+B3+D4:h.', arp_bm, 'B2:h.', 'B3:h.', ['Bm']),
        ('(F#6:q E6:e F#6:e) r:q', 'F#3+B3+D4:h.', arp_gm, 'G2:h.', None, ['Gmaj7']),
        ('(G6:h F#6:e E6:e)', 'G3+B3+D4:h.', arp_em, 'E2:h.', None, ['Em7']),
        ('C#6:h.', 'F#3+B3+C#4:h.', arp_fs, 'F#2:h.', None, ['F#sus4']),
        ('(D6:e C#6:e B5:h)', 'F#3+B3+D4:h.', 'B2:q F#3:q D4:q', 'B2:h.', None, ['Bm']),
        ('F#6:q r:h', 'D3+G3+B3:h.', 'G2:q D3:q B3:q', 'G2:h.', None, ['Gmaj7']),
        ('r:h (E5:q)', 'E3+G3+B3:h.', 'E2:h B2:q', 'E2:h.', None, ['Em7']),
        ('r:q C#5:h', 'F#3+A#3+C#4:h.', 'F#2:h. ', 'F#2:h.', None, ['F#7']),
    ]
    for i, (box, org, lh, st, bel, ch) in enumerate(sec3):
        lines = {BOX: box, ORG: org, LH: lh.strip(), STR: st}
        if bel:
            lines[BEL] = bel
        kw = {}
        if i == 0:
            kw = dict(mark='三 · 发条将尽', dyn={BOX: 'p', ORG: 'pp', STR: 'pp'},
                      hairpin={BOX: ('dim', 7), LH: ('dim', 7)})
        elif i == 4:
            kw = dict(text={BOX: 'morendo, come una molla che si scarica'})
        bars.append(Bar(lines, harm=ch, lead=BOX, **kw))

    return Piece(
        'night', '安眠', '第二章 · 夜与守夜（三段式夜曲循环）', 2, (3, 4), parts, bars,
        reverb=3.0, wet=0.36, mix={BEL: 9, LH: 1, STR: -8},
        style=dict(rubato=1.0, swell=0.8, pedal={LH: 'full'}), squeeze=2.0,
    )


# ================================================================
# 5. 清晨与上午：《十六人的日子》（Days of Sixteen）— 32 小节四段式室内乐船歌
# ================================================================
def day():
    """四段式 6/8 船歌：一·晨光初醒（钢琴）→ 二·走廊偶遇（单簧管与琶音）→ 三·旧怨与分歧（弦乐转 arco 展开）→ 四·同堂日常（合奏再现）。"""
    CLA, CEL, RH, LH, PIZ, CB = range(6)
    parts = [
        Part('clarinet', '单簧管', 'Cl.', 'wind.reed.clarinet.bflat', 71, 50, 94, amateur=(50, 89), pan=-0.3,
             template_id='bb-clarinet', voice='clarinet', transpose=(-2, -1), part_id=1, track_name='B♭ 单簧管'),
        p_celesta(part_id=2, pan=0.4, gain=0.7),
        p_piano(part_id=3, staves=2, pan=0.1),
        p_cellos(pizz=True, part_id=5, pan=-0.1),
        p_basses(pizz=True, part_id=6, pan=-0.15),
    ]

    DAY_CH = {
        'D': ('D3', 'A3+D4+F#4', 'D3:e A3:e D4:e F#4:e D4:e A3:e', 'D3'),
        'A/C#': ('C#3', 'A3+C#4+E4', 'C#3:e A3:e C#4:e E4:e C#4:e A3:e', 'C#3'),
        'Bm': ('B2', 'B3+D4+F#4', 'B2:e F#3:e B3:e D4:e B3:e F#3:e', 'B2'),
        'F#m': ('F#2', 'A3+C#4+F#4', 'F#2:e C#3:e F#3:e A3:e F#3:e C#3:e', 'F#3'),
        'G': ('G2', 'G3+B3+D4', 'G2:e D3:e G3:e B3:e G3:e D3:e', 'G3'),
        'Gm': ('G2', 'G3+Bb3+D4', 'G2:e D3:e G3:e Bb3:e G3:e D3:e', 'G3'),
        'D/F#': ('F#2', 'A3+D4+F#4', 'F#2:e D3:e F#3:e A3:e F#3:e D3:e', 'F#3'),
        'Em': ('E2', 'G3+B3+E4', 'E2:e B2:e E3:e G3:e E3:e B2:e', 'E3'),
        'Em7': ('E2', 'G3+B3+D4', 'E2:e B2:e E3:e G3:e D4:e B3:e', 'E3'),
        'A': ('A2', 'A3+C#4+E4', 'A2:e E3:e A3:e C#4:e A3:e E3:e', 'A2'),
        'A7': ('A2', 'G3+C#4+E4', 'A2:e E3:e G3:e C#4:e G3:e E3:e', 'A2'),
    }

    def barc(chords, flowing=False):
        if flowing and len(chords) == 1:
            return DAY_CH[chords[0]][2]
        out = []
        for c in (chords if len(chords) == 2 else chords * 2):
            low, chord, _, _ = DAY_CH[c]
            out.append(f"{low}:e {chord}:e' {chord}:e'")
        return ' '.join(out)

    def pizz_bass(chords):
        if len(chords) == 1:
            r = DAY_CH[chords[0]][3]
            return f"{r}:q. r:q."
        return f"{DAY_CH[chords[0]][3]}:q. {DAY_CH[chords[1]][3]}:q."

    bars = []
    tempo = (90, tempo_text(60, 'Allegretto, a passeggio', dotted=True))

    # --- 一 · 晨光初醒（第 1–8 小节）：钢琴奏出明朗的 D 大调 6/8 船歌，弦乐轻巧拨奏 ---
    sec1 = [
        ('(F#5:e E5:e D5:e) (A5:e G5:e A5:e) || D4:q. C#4:q.', ['D', 'A/C#']),
        ('(B5:q. A5:e G5:e F#5:e) || D4:q. B3:q.', ['Bm', 'Em']),
        ('(E5:e F#5:e G5:e A5:q D5:e) || C#4:q. D4:q.', ['A', 'D']),
        ('(E5:q.~ E5:e D5:e C#5:e) || B3:q. A3:q.', ['Em', 'A7']),
        ('(F#5:e E5:e D5:e) (A5:e G5:e A5:e) || D4:q. C#4:q.', ['D', 'A/C#']),
        ('(C#6:q. B5:e A5:e G5:e) || F#4:q. D4:q.', ['F#m', 'G']),
        ('(F#5:e E5:e D5:e E5:q C#5:e) || D4:q. G3+C#4:q.', ['Bm', 'A7']),
        ('D5:q. (A4:e B4:e C#5:e) || F#4:q. r:q.', ['D']),
    ]
    for i, (rh, ch) in enumerate(sec1):
        kw = (dict(mark='一 · 晨光初醒（钢琴）', tempo=tempo, text={RH: 'leggiero, grazioso'},
                   dyn={RH: 'mf', LH: 'p', PIZ: 'mp', CLA: 'mp', CEL: 'p'}, tech={PIZ: 'pizzicato'})
              if i == 0 else {})
        bars.append(Bar({RH: rh, LH: barc(ch), PIZ: pizz_bass(ch)}, harm=ch, lead=RH, **kw))

    # --- 二 · 走廊偶遇（第 9–16 小节）：单簧管接过旋律并加花，钢琴以流动琶音与钢片琴对答 ---
    sec2 = [
        ('(F#5:e E5:e D5:e) (A5:q G5:e)', 'r:q. (F#4+A4:q.)', ['D', 'A/C#'], None),
        ('(B5:q A5:e G5:e F#5:e E5:e)', '(D4:e F#4:e B4:e G4:q.)', ['Bm', 'Em'], 'r:q. (D6:e B5:e G5:e)'),
        ('(E5:e F#5:e G5:e A5:q F#5:e)', '(C#4:e E4:e A4:e F#4:q.)', ['A', 'D'], None),
        ('(E5:q. C#5:q.)', '(B3:e E4:e G4:e A4:e G4:e E4:e)', ['Em', 'A7'], 'r:q. (A5:e G5:e E5:e)'),
        ('(F#5:e E5:e D5:e) (A5:e B5:e C#6:e)', 'r:q. (F#4+A4:q.)', ['D', 'A/C#'], None),
        ('(D6:q C#6:e B5:e A5:e G5:e)', '(F#4:e A4:e C#5:e B4:q.)', ['F#m', 'G'], 'r:q. (F#6:e D6:e B5:e)'),
        ('(F#5:e G5:e A5:e E5:q C#5:e)', '(D4:q. G4+C#5:q.)', ['Bm', 'A7'], None),
        ('D5:h.', '(F#4:e A4:e D5:e F#5:q.)', ['D'], '(D6:q. A5:q.)'),
    ]
    for i, (cla, rh, ch, cel) in enumerate(sec2):
        lines = {CLA: cla, RH: rh, LH: barc(ch, flowing=(len(ch) == 1)), PIZ: pizz_bass(ch)}
        if cel:
            lines[CEL] = cel
        kw = dict(mark='二 · 走廊偶遇（单簧管）', text={CLA: 'dolce, conversando'},
                  dyn={CLA: 'mf', RH: 'p'}) if i == 0 else {}
        bars.append(Bar(lines, harm=ch, lead=CLA, **kw))

    # --- 三 · 旧怨与分歧（第 17–24 小节）：B 段展开，大提琴转连弓 arco 歌唱，引入 g 小调借用和弦色彩 ---
    sec3 = [
        # m17
        ('(D5:e G5:e B5:e A5:q G5:e)', '(B4:q.D5:q.)'.replace('.D5', '. D5'), '(G3:q. B3:q.)', ['G']),
        # m18 (借用 Gm 色彩)
        ('(Bb5:q. A5:e G5:e F5:e)', '(D5:q. Bb4:q.)', '(G3:q. D3:q.)', ['Gm']),
        # m19
        ('(A5:e F#5:e D5:e E5:q F#5:e)', '(D5:q. A4:q.)', '(F#3:q. D3:q.)', ['D/F#']),
        # m20
        ('(G5:q. E5:q.)', '(B4:e E5:e G5:e B4:q.)', '(E3:q. G3:q.)', ['Em7']),
        # m21
        ('(F#5:e B5:e D6:e C#6:q B5:e)', '(D5:q. F#5:q.)', '(B2:q. D3:q.)', ['Bm']),
        # m22 (再次掠过 Gm)
        ('(Bb5:q. G5:e F5:e E5:e)', '(D5:q. Bb4:q.)', '(G3:q. Bb3:q.)', ['Gm']),
        # m23
        ('(E5:e F#5:e G5:e A5:e G5:e F#5:e)', '(B4:q. C#5:q.)', '(E3:q. A2:q.)', ['Em7', 'A7']),
        # m24
        ('(E5:q. C#5:q.)', '(A4:e C#5:e E5:e G5:e E5:e C#5:e)', '(A2:q. G3:q.)', ['A7']),
    ]
    for i, (cla, rh, vc_arco, ch) in enumerate(sec3):
        lines = {CLA: cla, RH: rh, LH: barc(ch, flowing=True), PIZ: vc_arco}
        kw = {}
        if i == 0:
            kw = dict(
                mark='三 · 旧怨与分歧（展开）',
                dyn={CLA: 'mf', RH: 'mp', PIZ: 'mf'},
                tech={PIZ: 'arco'},
                text={PIZ: 'arco, espressivo'},
                hairpin={CLA: ('cresc', 4), PIZ: ('cresc', 4)},
            )
        bars.append(Bar(lines, harm=ch, lead=CLA, **kw))

    # --- 四 · 同堂日常（第 25–32 小节）：合奏再现，钢片琴与钢琴领奏，单簧管对位，弦乐恢复拨奏 ---
    sec4 = [
        (
            '(F#5:e E5:e D5:e) (A5:e G5:e A5:e)',
            '(D5:q. E5:q.)',
            '(F#4+D5:e E5:e D5:e) (A4+A5:e G5:e A5:e)',
            ['D', 'A/C#'],
        ),
        (
            '(B5:q. A5:e G5:e F#5:e)',
            '(D5:q. E5:e F#5:e G5:e)',
            '(D5+B5:q. A5:e G5:e F#5:e)',
            ['Bm', 'Em'],
        ),
        (
            '(E5:e F#5:e G5:e A5:q D5:e)',
            '(C#5:q. D5:q.)',
            '(C#5+E5:e F#5:e G5:e F#4+A4+A5:q D5:e)',
            ['A', 'D'],
        ),
        (
            '(E5:q. C#5:q.)',
            '(B4:e C#5:e D5:e E5:q.)',
            '(G4+E5:q. G4+C#5:q.)',
            ['Em', 'A7'],
        ),
        (
            '(F#5:e E5:e D5:e) (A5:e G5:e A5:e)',
            '(D5:q. C#5:q.)',
            '(F#4+D5:e E5:e D5:e) (A4+A5:e G5:e A5:e)',
            ['D', 'A/C#'],
        ),
        (
            '(C#6:q. B5:e A5:e G5:e)',
            '(F#5:q. G5:e F#5:e E5:e)',
            '(F#5+C#6:q. B5:e A5:e G5:e)',
            ['F#m', 'G'],
        ),
        (
            '(F#5:e E5:e D5:e E5:q C#5:e)',
            '(D5:q. C#5:e B4:e A4:e)',
            '(D5+F#5:e E5:e D5:e G4+E5:q C#5:e)',
            ['Bm', 'A7'],
        ),
        (
            'D5:q. r:q.',
            'F#4:q. r:q.',
            'F#4+A4+D5:q. r:q.',
            ['D'],
        ),
    ]
    for i, (cel, cla, rh, ch) in enumerate(sec4):
        lines = {CEL: cel, CLA: cla, RH: rh, LH: barc(ch), PIZ: pizz_bass(ch)}
        kw = (dict(mark='四 · 同堂日常（合奏）', dyn={CEL: 'mf', RH: 'mf', CLA: 'mp', PIZ: 'mp'},
                   tech={PIZ: 'pizzicato'}) if i == 0 else {})
        bars.append(Bar(lines, harm=ch, lead=CEL, **kw))

    bars = with_contrabass(bars, PIZ, CB)
    return Piece(
        'day', '十六人的日子', '日常变奏 · 室内乐船歌（四段循环）', 2, (6, 8), parts, bars,
        reverb=1.5, wet=0.2, mix={CLA: 6, CEL: 7, RH: 4, LH: 0, PIZ: -5, CB: -7},
        style=dict(rubato=0.5, swell=0.8, pedal={RH: 'half', LH: 'half'}, short_chords=(LH,)),
    )


# ================================================================
# 6. 午间配给：《午间》（Noon）— 24 小节江南丝竹三段式变奏
# ================================================================
def day_noon():
    """江南丝竹三段体：一·慢板（放慢加花、嵌空让路）→ 二·中板（丝竹问答与游移对位）→ 三·快板（合奏加花）。
    严格遵守 D 调笛子音域 69..100 (A4..E7)、二胡 62..93 (D4..A6)、扬琴 48..86、三味线 48..84、日本筝 36..83。"""
    DIZI, ERHU, YANG, PIPA, ZHENG_, MUYU = range(6)
    parts = [
        Part('flute', 'D调笛子', 'D Di.', 'wind.flutes.di-zi', 72, 69, 100, amateur=(69, 95), pan=-0.25,
             template_id='d-dizi', voice='flute', part_id=1),
        Part('fiddle', '二胡', 'Eh.', 'strings.erhu', 110, 62, 93, amateur=(62, 81), pan=0.25, template_id='erhu',
             voice='fiddle', channel_name='arco',
             channels=(('arco', 0, 110), ('pizzicato', 0, 45), ('tremolo', 0, 44)), part_id=2),
        Part('dulcimer', '扬琴', 'Dlc.', 'pitched-percussion.hammer-dulcimer', 15, 48, 86, pan=0.4,
             template_id='dulcimer', voice='dulcimer', part_id=3),
        Part('shamisen', '三味线', 'Sh.', 'pluck.shamisen', 106, 48, 84, amateur=(48, 79), pan=-0.4,
             template_id='shamisen', voice='shamisen', part_id=4),
        Part('koto', '日本筝', 'Ko.', 'pluck.koto', 107, 36, 83, amateur=(57, 83), bass_clef=True, pan=0.1,
             template_id='koto', voice='koto', part_id=5),
        p_temple_blocks(part_id=6),
    ]

    ZHENG_ARP = {
        'D': 'D3:e A3:e D4:e E4:e F#4:e A4:e F#4:e D4:e',
        'B': 'B2:e F#3:e B3:e D4:e E4:e F#4:e D4:e B3:e',
        'A': 'A2:e E3:e A3:e B3:e D4:e E4:e D4:e A3:e',
        'E': 'E2:e B2:e E3:e F#3:e A3:e B3:e A3:e E3:e',
        'G': 'G2:e D3:e G3:e A3:e B3:e D4:e B3:e G3:e',
    }

    bars = []

    # --- 一 · 慢板 · 放慢加花（第 1–8 小节，♩=60）：你繁我简、嵌空让路的丝竹支声复调 ---
    # 二胡拉连绵主干，笛子在二胡长音处加花，扬琴轻点五声和音与过门
    sec1 = [
        # m1
        (
            '(F#5:s A5:s F#5:s E5:s D5:e. E5:s) (A5:q F#5:s A5:s B5:s A5:s)',
            '(F#5:e E5:e D5:h) (A4:e D5:e)',
            'r:h (D4+A4:q F#4+D5:q)',
            'D',
        ),
        # m2
        (
            '(B5:e. D6:s B5:q) (F#5:s A5:s F#5:s E5:s F#5:q)',
            '(B4:q. D5:e F#5:h)',
            '(B3+F#4:h) r:q (D4:e E4:e)',
            'B',
        ),
        # m3
        (
            '(E5:q F#5:s A5:s F#5:s E5:s) (A5:e. B5:s D5:q)',
            '(E5:s F#5:s E5:s D5:s F#5:q) (A4:q D5:q)',
            'r:q (A3+E4:h D4+F#4:q)',
            'A',
        ),
        # m4
        (
            '(E5:q. F#5:e D5:e B4:s A4:s B4:q)',
            '(E5:h D5:q B4:q)',
            '(E3+B3:h) (B3:e D4:e F#4:q)',
            'E',
        ),
        # m5
        (
            '(F#5:s A5:s F#5:s E5:s D5:q) (A5:s B5:s A5:s F#5:s A5:q)',
            '(F#5:e E5:e D5:q A4:h)',
            '(D4+A4:h F#4+D5:h)',
            'D',
        ),
        # m6
        (
            '(B5:e. D6:s B5:e A5:e) (F#5:q E5:s F#5:s E5:e)',
            '(B4:q D5:q F#5:e A5:e E5:q)',
            'r:h (B3+F#4:q E4+B4:q)',
            'B',
        ),
        # m7
        (
            '(F#5:s A5:s F#5:s E5:s D5:e E5:e) (F#5:e. D5:s A4:q)',
            '(D5:q B4:e D5:e E5:q A4:q)',
            '(A3+E4:h E3+A3:h)',
            'A',
        ),
        # m8
        (
            'D5:h. (E5:s F#5:s A5:s B5:s)',
            '(D5:h A4:e B4:e D5:q)',
            '(D3:e A3:e D4:e F#4:e A4:h)',
            'D',
        ),
    ]
    for i, (dizi, erhu, yang, root) in enumerate(sec1):
        kw = (dict(mark='一 · 慢板（放慢加花）', tempo=(60, tempo_text(60, 'Adagio, con fioriture')),
                   dyn={DIZI: 'mp', ERHU: 'mp', YANG: 'p', ZHENG_: 'p', PIPA: 'mp', MUYU: 'p'},
                   text={DIZI: '嵌空加花，清越'}) if i == 0 else {})
        bars.append(Bar({DIZI: dizi, ERHU: erhu, YANG: yang, ZHENG_: ZHENG_ARP[root]},
                        harm=[root], lead=DIZI, **kw))

    # --- 二 · 中板 · 丝竹相和（第 9–16 小节，♩=92）：全新 B 段旋律，三味线与扬琴、二胡问答游移 ---
    sec2 = [
        # m9
        (
            'r:h (B5:e A5:e F#5:q)',
            '(B4:q D5:e E5:e F#5:q D5:q)',
            '(B3:e F#4:e B4:e D5:e F#4:e D4:e B3:q)',
            "B3:q F#3:e' B3:e' D4:q B3:q",
            'C5:q> r:q C5:q r:q',
            'B',
        ),
        # m10
        (
            '(A5:q. B5:e A5:q F#5:q)',
            'r:q (F#5:e E5:e D5:q A4:q)',
            '(F#4:e A4:e D5:e A4:e F#4:q D4:q)',
            "D4:q A3:e' D4:e' F#4:q D4:q",
            'r:q C5:q r:q C5:q',
            'D',
        ),
        # m11
        (
            'r:h (A5:e F#5:e E5:q)',
            '(E4:q G4:e A4:e B4:q D5:q)',
            '(G3:e D4:e G4:e B4:e A4:e G4:e E4:q)',
            "G3:q D3:e' G3:e' B3:q A3:q",
            'C5:q> r:q C5:q r:q',
            'G',
        ),
        # m12
        (
            '(F#5:q E5:e D5:e E5:h)',
            '(A4:q B4:q A4:e F#4:e E4:q)',
            '(A3:e E4:e A4:e B4:e A4:e F#4:e E4:q)',
            "A3:q E3:q A3:e' B3:e' E3:q",
            'r:q C5:q C5:e C5:e r:q',
            'A',
        ),
        # m13
        (
            '(D5:q E5:e F#5:e A5:q B5:q)',
            '(F#4:h D5:q F#5:q)',
            '(D4:e A4:e D5:e A4:e F#4:e A4:e D5:q)',
            "D4:q A3:e' D4:e' F#4:q A4:q",
            'C5:q> r:q C5:q r:q',
            'D',
        ),
        # m14
        (
            '(D6:q. B5:e A5:q F#5:q)',
            '(B5:q F#5:e E5:e D5:q B4:q)',
            '(B3:e F#4:e B4:e D5:e B4:e A4:e F#4:q)',
            "B3:q F#3:q B3:e' D4:e' F#4:q",
            'r:q C5:q r:q C5:q',
            'B',
        ),
        # m15
        (
            '(E5:q F#5:e A5:e B5:q A5:q)',
            '(G4:q A4:q B4:e D5:e E5:q)',
            '(G3:e D4:e G4:e A4:e B4:e A4:e E4:q)',
            "G3:q D3:q A3:e' B3:e' A3:q",
            'C5:q> r:e C5:e r:q C5:q',
            'G',
        ),
        # m16
        (
            'A5:h (F#5:e E5:e D5:q)',
            '(F#5:q E5:e D5:e A4:h)',
            '(A3:e E4:e A4:e C#5:e D5:q A4:q)',
            'A3:q E3:q D4:q A3:q',
            'C5:q> C5:q C5:q r:q',
            'A',
        ),
    ]
    for i, (dizi, erhu, yang, pipa, muyu, root) in enumerate(sec2):
        kw = (dict(mark='二 · 中板（丝竹相和）', tempo=(92, tempo_text(92, 'Moderato, giocoso')),
                   dyn={DIZI: 'mf', ERHU: 'mf', YANG: 'mp', PIPA: 'mp', ZHENG_: 'mp', MUYU: 'p'},
                   hairpin={ERHU: ('cresc', 4)}) if i == 0 else {})
        bars.append(Bar(
            {DIZI: dizi, ERHU: erhu, YANG: yang, PIPA: pipa, ZHENG_: ZHENG_ARP[root], MUYU: muyu},
            harm=[root], lead=ERHU if i % 2 == 0 else DIZI, **kw,
        ))

    # --- 三 · 快板 · 欢聚同堂（第 17–24 小节，♩=116）：笛子高音领奏，二胡支声对位，扬琴花奏 ---
    sec3 = [
        # m17
        (
            '(F#6:e E6:e D6:q) (A5:q F#5:e A5:e)',
            '(D5:q F#5:e E5:e D5:q A4:q)',
            'F#4:e A4:e D5:e F#4:e A4:e D5:e F#4:e A4:e',
            "D4:q F#4:e' E4:e' D4:q A3:q",
            'C5:q> r:e C5:e r:q C5:q',
            'D',
        ),
        # m18
        (
            '(B5:q. D6:e A5:q F#5:q)',
            '(B4:e D5:e F#5:q E5:e D5:e B4:q)',
            'B3:e D4:e F#4:e B4:e A4:e F#4:e D4:e B3:e',
            "B3:q D4:q F#4:e' E4:e' D4:q",
            'r:q C5:q r:q C5:q',
            'B',
        ),
        # m19
        (
            '(E5:q F#5:e A5:e B5:q D6:q)',
            '(E5:e D5:e B4:q A4:q F#4:q)',
            'E4:e F#4:e A4:e E4:e A4:e B4:e D5:e A4:e',
            "E4:q F#4:q A4:e' B4:e' D4:q",
            'C5:q> r:e C5:e r:q C5:q',
            'A',
        ),
        # m20
        (
            '(E6:h D6:q B5:q)',
            '(E5:q F#5:e A5:e B5:q F#5:q)',
            'E4:e B4:e E5:e B4:e D5:e B4:e F#4:e B4:e',
            "E4:q B3:q D4:q B3:e' A3:e'",
            'C5:q> C5:q r:e C5:e C5:q',
            'E',
        ),
        # m21
        (
            '(F#6:e E6:e D6:q) (A5:e B5:e D6:e A5:e)',
            '(D5:q F#5:q A5:q F#5:q)',
            'D4:e F#4:e A4:e D5:e A4:e F#4:e D5:e A4:e',
            "D4:q F#4:q A4:e' F#4:e' D4:q",
            'C5:q> r:e C5:e r:q C5:q',
            'D',
        ),
        # m22
        (
            '(B5:q. D6:e A5:q E6:q)',
            '(B4:e D5:e F#5:q A5:q E5:q)',
            'B3:e F#4:e B4:e D5:e A4:e F#4:e E4:e A4:e',
            "B3:q F#4:q A4:q E4:e' D4:e'",
            'r:q C5:q r:q C5:q',
            'B',
        ),
        # m23
        (
            '(F#6:q E6:e D6:e E6:q A5:q)',
            '(D5:q B4:e D5:e E5:q C#5:q)',
            'F#4:e A4:e D5:e B4:e E4:e A4:e C#5:e A4:e',
            "D4:q B3:q E4:q A3:e' B3:e'",
            'C5:q> C5:e C5:e C5:q C5:q',
            'A',
        ),
        # m24
        (
            'D6:h. r:q',
            'D5:h. r:q',
            '(D4:e A4:e D5:e F#4:e D5:q) r:q',
            'D4:h A3:q r:q',
            'C5:q> r:q r:h',
            'D',
        ),
    ]
    for i, (dizi, erhu, yang, pipa, muyu, root) in enumerate(sec3):
        kw = (dict(mark='三 · 快板（欢聚同堂）', tempo=(116, tempo_text(116, 'Allegro vivace')),
                   dyn={DIZI: 'f', ERHU: 'mf', YANG: 'mf', PIPA: 'mf', ZHENG_: 'mp', MUYU: 'mp'},
                   hairpin={DIZI: ('cresc', 6)}) if i == 0 else {})
        bars.append(Bar(
            {DIZI: dizi, ERHU: erhu, YANG: yang, PIPA: pipa, ZHENG_: ZHENG_ARP[root], MUYU: muyu},
            harm=[root], lead=DIZI, **kw,
        ))

    return Piece(
        'day_noon', '午间', '日常变奏 · 江南丝竹三段体（循环）', 2, (4, 4), parts, bars,
        reverb=1.3, wet=0.2, mix={ZHENG_: 4, MUYU: 8},
        style=dict(rubato=0.5, swell=0.7, lead_db=2.0),
    )


# ================================================================
# 7. 下午勘查：《午后》（Afternoon）— 24 小节三段式木卡姆变奏
# ================================================================
def day_afternoon():
    """木卡姆三段式：一·散板引子（4小节）→ 二·主题呈示（8小节，尺八与小提琴对答，3+3+2手鼓）→ 三·展开与扬琴竞奏（12小节）。"""
    NEY, SANTUR, DUTAR, FIDDLE, DAF, DRONE = range(6)
    parts = [
        Part('shakuhachi', '尺八', 'Shak.', 'wind.flutes.shakuhachi', 77, 60, 100, amateur=(62, 88), pan=-0.2,
             template_id='shakuhachi', voice='shakuhachi', part_id=1),
        Part('dulcimer', '扬琴', 'Dlc.', 'pitched-percussion.hammer-dulcimer', 15, 48, 86, pan=0.35,
             template_id='dulcimer', voice='dulcimer', part_id=2),
        Part('shamisen', '三味线', 'Sh.', 'pluck.shamisen', 106, 48, 84, amateur=(48, 79), bass_clef=True,
             pan=-0.35, template_id='shamisen', voice='shamisen', part_id=3),
        Part('fiddle', '小提琴', 'Vln.', 'strings.violin', 40, 55, 103, amateur=(55, 88), pan=0.2,
             template_id='violin', voice='violin', channel_name='arco',
             channels=(('arco', 0, 40), ('pizzicato', 0, 45), ('tremolo', 0, 44)), part_id=4),
        Part('framedrum', '框鼓', 'Fr. Dr.', 'drum.frame-drum', 0, 48, 72, pan=0.05, template_id='frame-drum',
             voice='framedrum', bank=128, drum_map={48: 45, 60: 45}, part_id=5),
        p_strings(part_id=6, gain=0.6),
    ]

    # 多样化的手鼓 Aksak (3+3+2 与切分变体)
    daf_a = 'C3:e> r:e C4:e C3:e> r:e C4:e C4:e> C4:e'
    daf_b = 'C3:e> C4:e C4:e C3:e> r:e C4:e C3:e> C4:e'
    daf_fill = 'C3:e> C4:e C3:e C4:e> C4:e C3:e C4:e> C4:e>'

    # 随调式走向变化的弹拨律动 (Bm -> Em/G -> C/G -> F#7)
    dutar_bm = 'B3:e> F#3:e B3:e B3:e> F#3:e B3:e F#3:e> B3:e'
    dutar_em = 'G3:e> E3:e B3:e G3:e> E3:e B3:e G3:e> B3:e'
    dutar_c = 'G3:e> C3:e G3:e G3:e> C3:e G3:e E3:e> G3:e'
    dutar_fs = 'F#3:e> C#3:e A#3:e F#3:e> C#3:e A#3:e F#3:e> C#4:e'

    bars = []

    # --- 一 · 散板引子（第 1–4 小节）：尺八自由散板（含三连音回音），扬琴与小提琴泛音应答 ---
    taqsim = [
        ('(F#5:h~ F#5:e3 G5:e3 E5:e3 F#5:q)', 'B3+F#4:h r:h', 'r:w', 'B2+F#3:w~'),
        ('(G5:q A#5:q F#5:e E5:e D5:q)', 'r:h (D4+F#4:h)', 'r:h (F#4:q E4:q)', 'B2+F#3:w'),
        ('(D5:e C#5:e B4:q) (F#5:h~)', 'B3+F#4:q r:q (G3+E4:h)', 'r:w', 'B2+F#3:h G2+E3:h'),
        ('F#5:q (G5:e3 F#5:e3 E5:e3 D5:q C#5:q)', 'r:h (F#3+C#4:h)', '(D4:h C#4:h)', 'F#2+C#3:w'),
    ]
    for i, (ney, sant, vln, dr) in enumerate(taqsim):
        kw = (dict(mark='一 · 散板（塔克西姆）', tempo=(92, tempo_text(92, 'Moderato, rubato')),
                   dyn={NEY: 'mp', DRONE: 'pp', SANTUR: 'p', DUTAR: 'p', FIDDLE: 'p', DAF: 'mp'},
                   text={NEY: 'libero, espressivo'}) if i == 0 else {})
        bars.append(Bar({NEY: ney, SANTUR: sant, FIDDLE: vln, DRONE: dr}, harm=['Bm'], lead=NEY, **kw))

    # --- 二 · 主题呈示（第 5–12 小节）：3+3+2 律动，尺八与小提琴对答演绎希贾兹门句 ---
    sec2 = [
        (MOTIF, 'r:h (B4:q D5:q)', dutar_bm, daf_a, 'B2+F#3:w', ['Bm']),
        ('(G5:q. F#5:e E5:e F#5:e D5:q)', '(E5:h B4:q G4:q)', dutar_em, daf_a, 'G2+E3:w', ['Em']),
        ('(C#5:q D5:e E5:e F#5:q B4:q)', '(A#4:q B4:q C#5:q D5:q)', dutar_bm, daf_b, 'B2+F#3:w', ['Bm']),
        ('(C#5:h A#4:q) r:q', '(E5:q D5:e C#5:e F#4:h)', dutar_fs, daf_fill, 'F#2+C#3:w', ['F#7']),
        ('r:h (F#5:q> E5:e F#5:e)', MOTIF, dutar_bm, daf_a, 'B2+F#3:w', ['Bm']),
        ('(F#5:q. E5:e D5:q C#5:q)', '(A#5:q. G5:e F#5:q E5:q)', dutar_c, daf_b, 'G2+E3:w', ['Em', 'C']),
        ('(B4:q G5:q F#5:q E5:q)', '(D5:q C#5:e B4:e C#5:q A#4:q)', dutar_fs, daf_a, 'F#2+C#3:w', ['F#7']),
        ('D5:h B4:h', 'B4:w', dutar_bm, daf_fill, 'B2+F#3:w', ['Bm']),
    ]
    for i, (ney, vln, dut, daf, dr, ch) in enumerate(sec2):
        kw = (dict(mark='二 · 主题（3+3+2 对答）', tempo=(98, tempo_text(98, 'Allegretto, ritmico')),
                   dyn={NEY: 'mf', FIDDLE: 'mp', DUTAR: 'mp', DAF: 'mp'},
                   hairpin={FIDDLE: ('cresc', 4)}) if i == 0 else {})
        bars.append(Bar({NEY: ney, FIDDLE: vln, DUTAR: dut, DAF: daf, DRONE: dr},
                        harm=ch, lead=NEY if i < 4 else FIDDLE, **kw))

    # --- 三 · 展开与扬琴竞奏（第 13–24 小节）：4 小节希贾兹高音展开 + 8 小节扬琴轮音加花与多声部竞奏 ---
    sec3 = [
        # m13–16: 展开段（小提琴与尺八激昂攀升，扬琴切分击弦）
        (
            '(F#5:h. G5:q)',
            'B3:e F#4:e B4:q G3:e E4:e B4:q',
            '(B4:e C#5:e D5:e E5:e F#5:q B5:q)',
            dutar_bm, daf_b, 'B2+F#3:w', ['Bm'],
        ),
        (
            '(A#5:h G5:q F#5:q)',
            'F#3:e C#4:e A#4:q G3:e D4:e B4:q',
            '(C#6:q. A#5:e G5:q F#5:q)',
            dutar_fs, daf_a, 'F#2+C#3:w', ['F#7', 'G'],
        ),
        (
            '(G5:q F#5:e E5:e D5:q G5:q)',
            'E3:e B3:e G4:q C3:e G3:e E4:q',
            '(E5:s F#5:s G5:s A#5:s B5:q G5:q E5:q)',
            dutar_c, daf_b, 'E2+B2:h C2+G2:h', ['Em', 'C'],
        ),
        (
            '(F#5:h C#5:h)',
            'F#3:e C#4:e F#4:e G4:e A#4:q F#4:q',
            '(F#5:q E5:e D5:e C#5:q A#4:q)',
            dutar_fs, daf_fill, 'F#2+C#3:w', ['F#7'],
        ),
        # m17–24: 扬琴十六分/八分加花主奏，小提琴与尺八支声交织
        (
            '(D6:h F#6:q E6:e F#6:e)',
            'D5:e C#5:e B4:e D5:e F#5:e> A#5:e E5:e F#5:e',
            '(B4:q F#4:q D5:q C#5:q)',
            dutar_bm, daf_a, 'B2+F#3:w', ['Bm'],
        ),
        (
            '(G6:q. F#6:e E6:q D6:q)',
            'G5:e A#5:e F#5:e E5:eE5:e F#5:e D5:e B4:e'.replace('eE5:e', 'e E5:e'),
            '(E5:q. D5:e C#5:q B4:q)',
            dutar_em, daf_b, 'G2+E3:w', ['Em'],
        ),
        (
            '(C#6:q D6:e E6:e F#6:q B5:q)',
            'C#5:e E5:e D5:e E5:e F#5:e D5:e B4:e D5:e',
            '(A#4:q B4:q D5:q F#5:q)',
            dutar_bm, daf_a, 'B2+F#3:w', ['Bm'],
        ),
        (
            '(C#6:h A#5:q) r:q',
            'C#5:e F#5:e E5:e C#5:e A#4:e C#5:e F#4:q',
            '(G5:q F#5:e E5:e C#5:h)',
            dutar_fs, daf_fill, 'F#2+C#3:w', ['F#7'],
        ),
        (
            MOTIF,
            'D5:e C#5:e B4:e D5:e F#5:e> G5:e E5:e F#5:e',
            '(B4:h D5:q C#5:q)',
            dutar_bm, daf_a, 'B2+F#3:w', ['Bm'],
        ),
        (
            '(A#5:q. G5:e F#5:q E5:q)',
            'A#5:e B5:e G5:e E5:e F#5:e G5:e E5:e D5:e',
            '(C#5:q. B4:e A#4:q G4:q)',
            dutar_c, daf_b, 'G2+D3:h E2+B2:h', ['G', 'Em'],
        ),
        (
            '(D5:q C#5:e B4:e C#5:q A#4:q)',
            'D5:e F#5:e C#5:e B4:e C#5:e E5:e A#4:e C#5:e',
            '(F#4:q G4:qF#4:q E4:q)'.replace('qF#4', 'q F#4'),
            dutar_fs, daf_a, 'F#2+C#3:w', ['F#7'],
        ),
        (
            'B4:h. r:q',
            '(B4:e F#4:e B4:e D5:e B4:h)',
            '(D4+B4:h. ) r:q'.replace('. )', '.)'),
            dutar_bm, daf_fill, 'B2+F#3:w', ['Bm'],
        ),
    ]
    for i, (ney, sant, vln, dut, daf, dr, ch) in enumerate(sec3):
        kw = {}
        if i == 0:
            kw = dict(mark='三 · 展开（锁芯寻踪）', dyn={FIDDLE: 'f', NEY: 'mf', SANTUR: 'mp'},
                      hairpin={FIDDLE: ('cresc', 4)})
        elif i == 4:
            kw = dict(mark='四 · 扬琴加花竞奏', tempo=(106, tempo_text(106, 'Più mosso, brillante')),
                      dyn={SANTUR: 'f', FIDDLE: 'mf', NEY: 'mf', DAF: 'f'})
        bars.append(Bar(
            {NEY: ney, SANTUR: sant, FIDDLE: vln, DUTAR: dut, DAF: daf, DRONE: dr},
            harm=ch, lead=FIDDLE if i < 4 else SANTUR, **kw,
        ))

    return Piece(
        'day_afternoon', '午后', '日常变奏 · 木卡姆四段体（循环）', 2, (4, 4), parts, bars,
        reverb=1.4, wet=0.22, mix={DRONE: -6, DAF: 6},
        style=dict(rubato=0.6, swell=0.7, lead_db=2.5),
    )


# ================================================================
# 8. 傍晚讲堂：《暮课》（Evening）— 24 小节三段式拉格（Alap → Gat → Jhala & Tihai）
# ================================================================
def day_evening():
    """印度拉格三段体（Raga Kirwani, Sa=B）：
    一·阿拉普（8小节散序）→ 二·伽特（8小节，塔布拉十六拍与西塔琴/竹笛问答）→ 三·贾拉与三叠收束（8小节密集交织与 Tihai）。
    注意 Evening.mscz 的 Part id 为 1(西塔琴), 3(西塔琴), 2(长笛), 4(Tablas)。"""
    SITAR, TANPURA, BANSURI, TABLA = range(4)
    parts = [
        Part('sitar', '西塔琴', 'Si.', 'pluck.sitar', 104, 36, 84, pan=-0.15, template_id='sitar', voice='sitar',
             part_id=1),
        Part('tanpura', '西塔琴', 'Si.', 'pluck.sitar', 104, 36, 84, bass_clef=True, gain=0.8, template_id='sitar',
             voice='sitar', part_id=3),
        Part('flute', '长笛', 'Fl.', 'wind.flutes.flute', 73, 59, 98, amateur=(60, 93), pan=0.25,
             template_id='flute', voice='flute', part_id=2),
        Part('tabla', 'Tablas', 'Tbs.', 'drum.tabla', 0, 48, 72, pan=0.1, template_id='tablas', voice='tabla',
             bank=0, drum_map={48: 73, 60: 71, 62: 87, 64: 84}, soundfont='SoundFonts/Tabla.sf2', part_id=4),
    ]

    # 富有呼吸起伏的坦布拉 Pa–Sa–Sa–sa 持续音织体
    drone_a = 'F#3:q. B3:e B3:h~'
    drone_b = 'B3:q F#3:q B2:h'
    drone_c = 'F#3:q B3:q F#4:e B3:e B2:q'

    # 塔布拉 Teentaal 16拍（4小节一周期）与句尾 Tihai（三叠击法）
    teentaal = [
        'C3+C4:q> C3+D4:q C3+D4:e E4:e C3+C4:q',
        'C3+C4:q C3+D4:e C4:e C3+D4:q C3+C4:q',
        'C4:q> D4:q D4:e E4:e C4:q',
        'C4:e E4:e C3+D4:q C3+D4:e E4:e C3+C4:q',
    ]
    tihai_bar1 = 'C3+D4:e E4:e C3+C4:q> r:q C3+D4:e E4:e'
    tihai_bar2 = 'C3+C4:q> r:q C3+D4:e E4:e C3+D4:e C3+C4:e>'

    bars = []

    # --- 一 · 阿拉普（第 1–8 小节，♩=60，senza misura）：逐级唤醒音阶，长笛远景轻叹，第7小节凝成门句 ---
    alap = [
        ('r:h (F#3:q B3:q~)', drone_a, 'r:w'),
        ('(B3:q C#4:e D4:e C#4:q B3:q)', drone_b, 'r:w'),
        ('(D4:h E4:s F#4:s E4:e F#4:q~)', drone_a, 'r:h (F#5:h)'),
        ('F#4:h (G4:e F#4:e E4:q)', drone_b, 'r:w'),
        ('(F#4:q G4:e A#4:e G4:q F#4:q)', drone_c, 'r:h (B5:q A#5:q)'),
        ('(A#4:e B4:q. D5:e C#5:e B4:q)', drone_b, 'r:w'),
        ('(D5:e C#5:e B4:q) (F#4:q> E4:e F#4:e)', drone_c, 'r:h (F#5:q E5:e F#5:e)'),
        ('(G4:e F#4:e E4:e D4:e C#4:q B3:q)', drone_b, '(F#5:h B4:h)'),
    ]
    for i, (sit, tan, fl) in enumerate(alap):
        lines = {SITAR: sit, TANPURA: tan}
        if fl != 'r:w':
            lines[BANSURI] = fl
        kw = (dict(mark='一 · 阿拉普（散序）', tempo=(60, tempo_text(60, 'Lento, senza misura')),
                   dyn={SITAR: 'mp', TANPURA: 'p', BANSURI: 'pp', TABLA: 'mp'},
                   text={SITAR: 'alap, meend espressivo'}) if i == 0 else {})
        bars.append(Bar(lines, harm=['Bm'], lead=SITAR, **kw))

    # --- 二 · 伽特 · 十六拍问答（第 9–16 小节，♩=86）：西塔琴呈示主题，长笛在句逗间作 Sawaal-Jawaab 对答 ---
    sec2 = [
        # m9
        (shift(MOTIF, -1), 'r:h (D5:e C#5:e B4:q)', drone_c, teentaal[0]),
        # m10
        ('(G4:q. F#4:e E4:q D4:q)', '(B5:q. A#5:e G5:q F#5:q)', drone_b, teentaal[1]),
        # m11
        ('(C#4:q D4:e E4:e F#4:q B3:q)', 'r:q (F#5:q D5:e E5:e F#5:q)', drone_c, teentaal[2]),
        # m12
        ('(C#4:h A#3:q) (B3:e C#4:e)', '(G5:e F#5:e E5:q C#5:h)', drone_b, teentaal[3]),
        # m13
        (shift(MOTIF, -1), '(D5:e C#5:e B4:q) r:h', drone_c, teentaal[0]),
        # m14
        ('(A#4:q. G4:e F#4:q E4:q)', 'r:q (G5:e F#5:e E5:q G5:q)', drone_b, teentaal[1]),
        # m15
        ('(D4:q C#4:e B3:e C#4:q A#3:q)', '(F#5:q E5:e D5:e E5:q C#5:q)', drone_c, teentaal[2]),
        # m16
        ('(B3:h F#4:e G4:e F#4:e E4:e)', 'B4:h. r:q', drone_b, teentaal[3]),
    ]
    for i, (sit, fl, tan, tab) in enumerate(sec2):
        kw = (dict(mark='二 · 伽特（十六拍问答）', tempo=(86, tempo_text(86, 'Andante, in Teentaal')),
                   dyn={SITAR: 'mf', BANSURI: 'mp', TABLA: 'mp'},
                   text={BANSURI: 'sawaal-jawaab (dialogando)'}) if i == 0 else {})
        bars.append(Bar({SITAR: sit, BANSURI: fl, TANPURA: tan, TABLA: tab},
                        harm=['Bm'], lead=SITAR, **kw))

    # --- 三 · 贾拉与三叠收束（第 17–24 小节，♩=96）：长笛高音歌唱，西塔琴密集 Chikari/Jhala 交织，末两小节 Tihai ---
    sec3 = [
        # m17
        ('D4:e B4:e C#4:e B4:e B3:e B4:e F#4:e B4:e', MOTIF, drone_c, teentaal[0]),
        # m18
        ('G4:e B4:e F#4:e B4:e E4:e B4:e D4:e B4:e', '(G5:q. F#5:e E5:e F#5:e D5:q)', drone_b, teentaal[1]),
        # m19
        ('C#4:e B4:e D4:e B4:e E4:e B4:e F#4:e B4:e', '(C#5:q D5:e E5:e F#5:q B5:q)', drone_c, teentaal[2]),
        # m20
        ('G4:e F#4:e E4:e D4:e C#4:q A#3:q', '(A#5:h F#5:q.) (G5:e)', drone_b, teentaal[3]),
        # m21
        ('D4:e B4:e C#4:e B4:e B3:e B4:e F#4:e B4:e', '(F#5:e E5:e D5:q) (B5:q> A#5:e B5:e)', drone_c, teentaal[0]),
        # m22
        ('A#4:e B4:e G4:e B4:e F#4:e B4:e E4:e B4:e', '(A#5:q. G5:e F#5:e G5:e E5:q)', drone_b, teentaal[1]),
        # m23 (Tihai 起点: 4+2+2)
        ('(D4:e E4:e F#4:q>) r:q (D4:e E4:e)', '(D5:e E5:e F#5:q>) r:q (D5:e E5:e)', drone_c, tihai_bar1),
        # m24 (Tihai 落回主音: 2+2+4)
        ('F#4:q> r:q (D4:e E4:e D4:e B3:e>)', 'F#5:q> r:q (D5:e E5:e D5:e B4:e>)', drone_b, tihai_bar2),
    ]
    for i, (sit, fl, tan, tab) in enumerate(sec3):
        kw = {}
        if i == 0:
            kw = dict(mark='三 · 贾拉与三叠（Jhala & Tihai）', tempo=(96, tempo_text(96, 'Mosso, con slancio')),
                      dyn={BANSURI: 'f', SITAR: 'mf', TABLA: 'f'},
                      hairpin={BANSURI: ('cresc', 6)})
        elif i == 6:
            kw = dict(text={TABLA: 'tihai (三叠收束)'})
        bars.append(Bar({SITAR: sit, BANSURI: fl, TANPURA: tan, TABLA: tab},
                        harm=['Bm'], lead=BANSURI, **kw))

    return Piece(
        'day_evening', '暮课', '日常变奏 · 拉格三段体（循环）', 2, (4, 4), parts, bars,
        reverb=1.8, wet=0.24, mix={SITAR: 2, BANSURI: 1, TANPURA: -5, TABLA: 4},
        style=dict(rubato=0.7, swell=0.7, lead_db=2.5),
    )


# ================================================================
# 9. 誓约庭议事：《誓约》（Rite）— 24 小节三段式雅乐礼乐变奏
# ================================================================
def rite():
    """雅乐三段体（B 羽五声调式：B D E F# A）：
    一·初献·金声玉振（8小节）→ 二·亚献·笙埙唱和（8小节）→ 三·终献·誓约同盟（8小节）。
    严格遵守管钟 53..79 (F3..G5)、木琴 60..108、中音笙 48..83、C中音陶笛 69..89 (A4..F6)、日本筝 36..83。"""
    BZ, QING, SHENG_, XUN, QIN_, DRUM, ZHU = range(7)
    parts = [
        p_bells(part_id=1, pan=-0.3),
        Part('bianqing', '木琴', 'Xyl.', 'pitched-percussion.xylophone', 13, 60, 108, amateur=(65, 96), pan=0.3,
             template_id='xylophone', voice='xylophone', transpose=(12, 7), part_id=2),
        Part('organ', '中音笙', 'A. She.', 'wind.reed.sheng', 20, 48, 83, gain=0.8, template_id='alto-sheng',
             voice='organ', part_id=3),
        Part('ocarina', '中音陶笛', 'A. Oc.', 'wind.flutes.ocarina', 79, 69, 89, amateur=(72, 89), pan=-0.15,
             template_id='c-alto-ocarina', voice='ocarina', part_id=4, track_name='C 中音陶笛'),
        Part('koto', '日本筝', 'Ko.', 'pluck.koto', 107, 36, 83, amateur=(57, 83), pan=0.15, template_id='koto',
             voice='koto', part_id=5),
        Part('taiko', '大鼓', 'Dagu', '', 0, 36, 60, template_id='dagu', voice='dagu', bank=128,
             drum_map={48: 87}, part_id=6),
        p_temple_blocks(part_id=7),
    ]

    bars = []

    # --- 一 · 初献 · 金声玉振（第 1–8 小节，♩=52）：柷起乐，管钟奏羽调门句，笙四五度呼吸和音，木琴玉振收逗 ---
    sec1 = [
        # m1
        (
            '(D5:h B4:h)',
            'r:w',
            'B3+F#4+B4:h. (A3+E4+A4:q)',
            'B2:q F#3:q B3:h',
            'C3:q> r:q r:h',
            'C5:q C5:q C5:q r:q',
        ),
        # m2
        (
            '(F#5:h E5:q F#5:q)',
            'r:h (F#5:q E5:q)',
            'B3+F#4:h E3+B3+E4:h',
            'F#3:h E3:q F#3:q',
            'r:w',
            'r:w',
        ),
        # m3
        (
            '(G5:h F#5:q E5:q)',
            'r:w',
            'D4+A4:h A3+E4:h',
            'D3:q A3:q F#3:q E3:q',
            'r:w',
            'r:w',
        ),
        # m4
        (
            '(D5:h E5:h)',
            'r:h (A5:q F#5:q)',
            'G3+D4:h A3+E4:h',
            'B2:q D3:q E3:h',
            'C3:q> r:q r:h',
            'r:w',
        ),
        # m5
        (
            'F#5:h (D5:q B4:q)',
            'F#5:h r:h',
            'B3+F#4+B4:h F#3+C#4+F#4:h',
            'F#3:q E3:q D3:q B2:q',
            'r:w',
            'r:w',
        ),
        # m6
        (
            '(F#5:h E5:q F#5:q)',
            'r:w',
            'B3+F#4:h A3+E4:h',
            'B2:q F#3:q A3:q F#3:q',
            'r:w',
            'r:w',
        ),
        # m7
        (
            '(E5:h D5:q E5:q)',
            'r:h (D5:q E5:q)',
            'G3+D4:h A3+E4:h',
            'E3:q B3:q D3:q E3:q',
            'r:w',
            'r:w',
        ),
        # m8
        (
            'B4:w',
            '(F#5:q D5:q B4:h)',
            'B3+F#4+B4:w',
            'B2:q F#3:q B3:h',
            'C3:q> r:q C3:q r:q',
            'r:w',
        ),
    ]
    for i, (bz, qing, sheng, qin, drum, zhu) in enumerate(sec1):
        lines = {BZ: bz, SHENG_: sheng, QIN_: qin}
        if qing != 'r:w':
            lines[QING] = qing
        if drum != 'r:w':
            lines[DRUM] = drum
        if zhu != 'r:w':
            lines[ZHU] = zhu
        kw = (dict(mark='一 · 初献（金声玉振）', tempo=(52, tempo_text(52, 'Grave, rituale')),
                   dyn={BZ: 'mf', QING: 'mp', SHENG_: 'p', XUN: 'mp', QIN_: 'mp', DRUM: 'mf', ZHU: 'mf'},
                   text={BZ: 'solenne, bronzeo'}) if i == 0 else {})
        bars.append(Bar(lines, harm=['Bm'], lead=BZ, **kw))

    # --- 二 · 亚献 · 笙埙唱和（第 9–16 小节）：中音陶笛（埙，69..89）苍古展开，古琴式筝滚拂支声 ---
    sec2 = [
        # m9
        (
            '(F#5:h A5:q F#5:q)',
            'D3:e A3:e D4:q F#4:q D4:q',
            'D4+A4:h B3+F#4:h',
            'D5:w',
            'r:w',
        ),
        # m10
        (
            '(E5:h. D5:q)',
            'A2:e E3:e A3:q B3:q D4:q',
            'A3+E4:h. (G3+D4:q)',
            'r:w',
            'r:h (E5:q D5:q)',
        ),
        # m11
        (
            '(B4:q D5:q F#5:h)',
            'B2:e F#3:e B3:q D4:e E4:e F#4:q',
            'B3+F#4:h D4+A4:h',
            'B4:w',
            'r:w',
        ),
        # m12
        (
            '(A5:h F#5:q E5:q)',
            'F#2:e C#3:e F#3:q A3:q E3:q',
            'F#3+C#4:h A3+E4:h',
            'r:w',
            'F#5:h r:h',
        ),
        # m13
        (
            '(D5:q E5:q A5:q B5:q)',
            'G2:e D3:e G3:q A3:q B3:q',
            'G3+D4:h D4+A4:h',
            'D5:w',
            'r:w',
        ),
        # m14
        (
            '(A5:h F#5:q D5:q)',
            'D3:e A3:e D4:q F#4:e E4:e D4:q',
            'D4+A4:h B3+F#4:h',
            'r:w',
            'r:h (A5:q F#5:q)',
        ),
        # m15
        (
            '(E5:q F#5:e E5:e D5:q B4:q)',
            'E3:e B3:e E4:q D4:q B3:q',
            'E3+B3:h A3+E4:h',
            'E5:w',
            'r:w',
        ),
        # m16
        (
            'F#5:w',
            'F#2:e C#3:e F#3:q A3:e F#3:e E3:q',
            'F#3+C#4+F#4:w',
            'F#4:w',
            '(F#5:h E5:h)',
        ),
    ]
    for i, (xun, qin, sheng, bz, qing) in enumerate(sec2):
        lines = {XUN: xun, QIN_: qin, SHENG_: sheng}
        if bz != 'r:w':
            lines[BZ] = bz
        if qing != 'r:w':
            lines[QING] = qing
        if i in (0, 4):
            lines[DRUM] = 'C3:q> r:q r:h'
        kw = (dict(mark='二 · 亚献（笙埙唱和）', dyn={XUN: 'mf', QIN_: 'mp', SHENG_: 'mp', BZ: 'p'},
                   text={XUN: 'antico, espressivo'}, hairpin={XUN: ('cresc', 4)}) if i == 0 else {})
        bars.append(Bar(lines, harm=['Bm'], lead=XUN, **kw))

    # --- 三 · 终献 · 誓约同盟（第 17–24 小节）：全编制庄严支声合奏，三通大鼓收束 ---
    sec3 = [
        # m17
        (
            '(D5:h B4:h)',
            '(D5:q E5:e D5:e B4:h)',
            'r:h (D5:q B4:q)',
            'B3+F#4+B4:h F#3+C#4+F#4:h',
            'B2:e F#3:e B3:q D4:e B3:e F#3:q',
            'C3:q> r:q r:h',
            'C5:q r:q C5:q r:q',
        ),
        # m18
        (
            '(F#5:h E5:q F#5:q)',
            '(F#5:q. A5:e E5:q F#5:q)',
            'r:h (F#5:q E5:q)',
            'B3+F#4:h A3+E4:h',
            'F#3:e B3:e D4:q E4:q F#4:q',
            'r:w',
            'r:w',
        ),
        # m19
        (
            '(G5:h F#5:q E5:q)',
            '(B5:h A5:q F#5:q)',
            '(B5:q A5:q F#5:q E5:q)',
            'G3+D4+G4:h D4+A4:h',
            'G2:e D3:e G3:q A3:q F#3:q',
            'r:w',
            'r:w',
        ),
        # m20
        (
            '(D5:h F#5:h)',
            '(E5:h F#5:h)',
            'D5:h F#5:h',
            'G3+D4:h B3+F#4:h',
            'B2:e F#3:e B3:qF#4:h'.replace('qF#4', 'q F#4'),
            'C3:q> r:q C3:q r:q',
            'C5:q r:q r:h',
        ),
        # m21
        (
            '(D5:h B4:h)',
            '(D5:e E5:e D5:q B4:h)',
            'r:h (D5:q B4:q)',
            'B3+F#4+B4:h G3+D4+G4:h',
            'B2:e F#3:e B3:q G3:e D4:e B3:q',
            'C3:q> r:q r:h',
            'C5:q r:q C5:q r:q',
        ),
        # m22
        (
            '(F#5:h E5:q D5:q)',
            '(F#5:q A5:q E5:q D5:q)',
            '(F#5:h E5:q D5:q)',
            'D4+A4:h A3+E4:h',
            'D3:e A3:e D4:q A3:e E4:e D4:q',
            'r:w',
            'r:w',
        ),
        # m23
        (
            '(E5:h D5:q E5:q)',
            '(E5:q. D5:e B4:q E5:q)',
            'r:h (D5:q E5:q)',
            'E3+B3+E4:h A3+E4+A4:h',
            'E3:e B3:e E4:q D4:q A3:q',
            'C3:q> r:q C3:q> r:q',
            'C5:q C5:q r:h',
        ),
        # m24 (三通大鼓收束)
        (
            'B4:w',
            'B4:w',
            'B4:w',
            'B3+F#4+B4:w',
            'B2+F#3+B3:w',
            'C3:q> C3:q> C3:q> r:q',
            'C5:q> r:q r:h',
        ),
    ]
    for i, (bz, xun, qing, sheng, qin, drum, zhu) in enumerate(sec3):
        lines = {BZ: bz, XUN: xun, QING: qing, SHENG_: sheng, QIN_: qin}
        if drum != 'r:w':
            lines[DRUM] = drum
        if zhu != 'r:w':
            lines[ZHU] = zhu
        kw = (dict(mark='三 · 终献（誓约同盟）',
                   dyn={BZ: 'f', XUN: 'f', QING: 'mf', SHENG_: 'mf', QIN_: 'mf', DRUM: 'f', ZHU: 'mf'},
                   hairpin={SHENG_: ('cresc', 4)}) if i == 0 else {})
        bars.append(Bar(lines, harm=['Bm'], lead=XUN, **kw))

    return Piece(
        'rite', '誓约', '日常变奏 · 雅乐三段体（誓约庭之议，循环）', 2, (4, 4), parts, bars,
        reverb=2.8, wet=0.32, mix={QING: 16, QIN_: 4, DRUM: -4, ZHU: 12, BZ: 3},
        style=dict(rubato=0.15, swell=0.4, lead_db=2.0), squeeze=1.8,
    )


CUES = [main_theme, door_side, confrontation, night, day, day_noon, day_afternoon, day_evening, rite]
