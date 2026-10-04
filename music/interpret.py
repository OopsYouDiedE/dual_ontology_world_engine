"""演奏诠释层：把曲谱变成“有人在弹”的演奏事件。

曲谱（cues.py）只写音符与力度记号；这里按一组确定的规则加上演奏处理，规则取自 KTH 的
Director Musices 演奏规则体系与钢琴/合奏的常规做法，全部可重现、不掷骰（只有毫秒级的
固定种子抖动）：

速度
- 乐句弧线：四小节一句，句中略向前推，句尾放慢；段落（排练记号）之末与全曲之末放得更多。
- 延长记号：所在拍明显放慢。
力度
- 乐句弧线：句中渐强、句尾回落。
- 拍位强弱：强拍略重、弱拍略轻（4/4 一、三拍；3/4 一拍；6/8 一、四拍）。
- 旋律突出：旋律声部整体加重，越高越亮（high-loud）；伴奏声部让开。
- 分解和弦：每组的低音略重，内声部略轻。
- 紧张和弦（减七、挂留、属七、增四）出现时略加重。
时值与时机
- 旋律提前（melody lead）：旋律比伴奏早约 12 毫秒出来。
- 钢琴和弦自下而上轻微琶开（每音 7 毫秒）。
- 连奏：持续音乐器的旋律与下一音重叠；钢琴旋律轻微重叠（指连奏）。
- 踏板：钢琴按和声换踏板，音延续到下一个和弦处（切分踏板，略晚于新和弦）；可设半踏板或不踩。
- 句尾呼吸：管风琴、单簧管的句末音略短。
- 船歌伴奏的和弦音短促，低音保持。
"""
import math
from dataclasses import dataclass

import numpy as np

from score_io import DUR, VELOCITY, midi_of, parse_line

SUSTAINING = {'organ', 'clarinet', 'strings'}
TENSION = ('o7', 'sus4', '#11', '7', 'A#')
GRID = 96  # 每个四分音符的速度采样点


@dataclass
class Event:
    staff: int
    start: float      # 秒
    length: float     # 按键时长（秒），之后进入释放段
    pitch: int
    velocity: int


@dataclass
class Style:
    rubato: float = 1.0          # 速度弧线深度
    swell: float = 1.0           # 力度弧线深度（dB 量级）
    pedal: dict = None           # 谱表序号 -> 'full' | 'half' | 'none'
    lead_db: float = 4.0
    short_chords: tuple = ()     # 这些谱表上的和弦音（非最低音）短促


def _beats_per_bar(time):
    num, den = time
    return num * 4 / den


def _metric_db(time, beat):
    num, den = time
    pos = round(beat * 2) / 2
    if (num, den) == (4, 4):
        return {0: 1.5, 2: 0.7}.get(pos, -0.6 if pos % 1 else 0.0)
    if (num, den) == (3, 4):
        return {0: 1.5}.get(pos, -0.6 if pos % 1 else 0.0)
    if (num, den) == (6, 8):
        return {0: 1.5, 1.5: 0.8}.get(pos, -0.7)
    return 0.0


def phrases(piece):
    """[(起始小节, 结束小节（不含）, 是否段落末)]：段落按排练记号划分，段内每四小节一句。"""
    starts = [i for i, b in enumerate(piece.bars) if b.mark] or [0]
    if starts[0] != 0:
        starts.insert(0, 0)
    out = []
    bounds = starts + [len(piece.bars)]
    for s, e in zip(bounds, bounds[1:]):
        k = s
        while k < e:
            n = 4 if e - k >= 4 else e - k
            if e - (k + n) < 2:   # 不留一两小节的零句
                n = e - k
            out.append((k, k + n, k + n == e))
            k += n
    return out


def tempo_curve(piece, style):
    """每个四分音符细分点上的“瞬时速度倍率”，以及实际时间表 t(q)。"""
    bpb = _beats_per_bar(piece.time)
    total_q = bpb * len(piece.bars)
    q = np.arange(int(total_q * GRID) + 1) / GRID
    bpm = np.zeros_like(q)
    current = piece.bars[0].tempo[0]
    for i, b in enumerate(piece.bars):
        if b.tempo:
            current = b.tempo[0]
        bpm[(q >= i * bpb) & (q < (i + 1) * bpb + 1e-9)] = current
    mult = np.ones_like(q)
    for s, e, section_end in phrases(piece):
        q0, q1 = s * bpb, e * bpb
        sel = (q >= q0) & (q < q1)
        x = (q[sel] - q0) / (q1 - q0)
        push = 0.02 * np.sin(np.pi * np.minimum(x / 0.7, 1.0))
        depth = 0.16 if section_end else 0.08
        if section_end and e == len(piece.bars) and not piece.loop:
            depth = 0.3
        rit = depth * np.clip((x - 0.82) / 0.18, 0, 1) ** 2
        mult[sel] *= 1 + style.rubato * (push - rit)
    for i, b in enumerate(piece.bars):          # 延长记号所在小节的最后一拍
        if any('^' in line for line in b.lines.values()):
            sel = (q >= (i + 1) * bpb - 1) & (q < (i + 1) * bpb)
            mult[sel] *= 0.55
    sec_per_q = 60.0 / (bpm * mult)
    t = np.concatenate([[0.0], np.cumsum(sec_per_q[:-1] / GRID)])
    return q, t


def _time_at(qgrid, tgrid, qv):
    return float(np.interp(qv, qgrid, tgrid))


def perform(piece, style, rng):
    """返回 (演奏事件列表, 总时长秒)。"""
    piece.validate()
    bpb = _beats_per_bar(piece.time)
    qgrid, tgrid = tempo_curve(piece, style)
    staff_parts = piece.staff_parts()
    phrase_of = {}
    for s, e, section_end in phrases(piece):
        for i in range(s, e):
            phrase_of[i] = (s, e)
    # 和声切换点（四分音符位置）与紧张和弦
    changes, tension_at = [], {}
    for i, b in enumerate(piece.bars):
        harm = b.harm or ['?']
        for k, name in enumerate(harm):
            qpos = i * bpb + k * bpb / len(harm)
            changes.append(qpos)
            tension_at[round(qpos, 3)] = any(tag in name for tag in TENSION)
    changes = np.array(sorted(set(changes)) + [len(piece.bars) * bpb])

    raw = []      # (staff, bar, q, dur_q, pitches, vel, flags)
    vel = {}
    for bi, bar in enumerate(piece.bars):
        for si in range(len(staff_parts)):
            if si in bar.dyn:
                vel[si] = VELOCITY[bar.dyn[si]]
            qpos = bi * bpb
            for pitches, dur, flags in parse_line(piece.line(bar, si)):
                dq = DUR[dur][2] / 480
                if pitches:
                    raw.append((si, bi, qpos, dq, [midi_of(p) for p in pitches], vel.get(si, 64), flags))
                qpos += dq
    lead_next = {}  # (staff) -> 排好序的起点，用于连奏判断
    for si in range(len(staff_parts)):
        lead_next[si] = sorted({r[2] for r in raw if r[0] == si})

    events = []
    for si, bi, q0, dq, pitches, base_vel, flags in raw:
        part, inner = staff_parts[si]
        voice = part.voice or part.key
        bar = piece.bars[bi]
        is_lead = bar.lead == si
        s, e = phrase_of[bi]
        x = (q0 - s * bpb) / ((e - s) * bpb)
        db = style.swell * (3.0 * math.sin(math.pi * min(x / 0.75, 1.0)) - 1.2 - 2.0 * max(0.0, (x - 0.85) / 0.15))
        db += _metric_db(piece.time, q0 - bi * bpb)
        if 'accent' in flags:
            db += 3.0
        if tension_at.get(round(q0, 3)):
            db += 1.5
        if bar.lead is not None:
            db += style.lead_db if is_lead else -1.0
        t_on = _time_at(qgrid, tgrid, q0)
        t_off = _time_at(qgrid, tgrid, q0 + dq)
        length = t_off - t_on
        if 'fermata' in flags:
            length *= 1.8
        lowest = min(pitches)
        order = sorted(pitches)
        for idx, pitch in enumerate(order):
            pdb = db
            if is_lead:
                pdb += 0.12 * (pitch - 72)
            elif len(pitches) > 1 and pitch != lowest:
                pdb -= 1.0
            # 分解和弦（钢琴左手单音流）：一拍内的最低音略重
            if voice == 'piano' and inner == 1 and len(pitches) == 1:
                pdb += 1.5 if abs((q0 % 1)) < 1e-6 else -1.5
            start = t_on + rng.normal(0, 0.003)
            if is_lead:
                start -= 0.012
            if voice == 'piano' and len(pitches) > 1:
                start += 0.007 * idx
            note_len = length
            if si in style.short_chords and pitch != lowest:
                note_len = min(note_len, 0.55 * length)
            if is_lead and voice in SUSTAINING:
                nxt = [qq for qq in lead_next[si] if qq > q0 + 1e-6]
                if nxt and abs(nxt[0] - (q0 + dq)) < 1e-6:
                    note_len += 0.035
                elif q0 + dq >= e * bpb - 1e-6:
                    note_len *= 0.92
            if voice == 'piano':
                mode = (style.pedal or {}).get(si, 'full')
                if is_lead:
                    nxt = [qq for qq in lead_next[si] if qq > q0 + 1e-6]
                    if nxt and abs(nxt[0] - (q0 + dq)) < 1e-6:
                        note_len += 0.015
                if mode != 'none':
                    k = np.searchsorted(changes, q0 + 1e-6)
                    change_t = _time_at(qgrid, tgrid, changes[min(k, len(changes) - 1)]) + 0.04
                    pedal_len = change_t - start
                    if mode == 'half':
                        pedal_len = min(pedal_len, note_len + 0.25)
                    note_len = max(note_len, pedal_len)
            v = int(np.clip(round(base_vel + pdb * 2.2), 8, 127))
            events.append(Event(si, start, max(0.03, note_len), pitch, v))
    total = _time_at(qgrid, tgrid, len(piece.bars) * bpb)
    return events, total
