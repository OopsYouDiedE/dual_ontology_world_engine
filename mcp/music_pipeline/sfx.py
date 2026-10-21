"""《安眠者有福》音效与对白提示音（2026-10-04 重写）。

全部由 synth.py 的同一套模型合成：纸、木、铁、黄铜、钟与音叉，都是 1890—1910 年代
学园里会有的材质。每个函数返回单声道 numpy 数组（44.1 kHz，-1..1）。
"""
import numpy as np

import synth
from synth import SR, TAU, _noise_burst

RNG_SEED = 1904


def _t(seconds):
    return np.arange(int(seconds * SR)) / SR


def _band(x, lo, hi):
    """频域带通（lo..hi Hz）。"""
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    spec *= 1.0 / (1.0 + (f / hi) ** 4)
    spec *= (f / lo) ** 2 / (1.0 + (f / lo) ** 2)
    return np.fft.irfft(spec, len(x))


def _room(x, seconds=0.6, wet=0.18, seed=11):
    ir = synth.impulse_response(seconds, seed, damping=5000)
    out = np.zeros(len(x) + len(ir) - 1)
    out[: len(x)] = x * (1 - wet)
    out += synth.convolve(x, ir) * wet * 0.6
    return _trim(out)


def _trim(x, floor_db=-62):
    level = 10 ** (floor_db / 20) * np.max(np.abs(x))
    idx = np.nonzero(np.abs(x) > level)[0]
    end = idx[-1] + int(0.02 * SR) if len(idx) else len(x)
    x = x[:end].copy()
    fade = min(len(x), int(0.015 * SR))
    x[-fade:] *= np.linspace(1, 0, fade)
    return x


def _norm(x, peak_db=-3.0):
    return x / (np.max(np.abs(x)) + 1e-12) * 10 ** (peak_db / 20)


def _place(dst, src, at):
    i = int(at * SR)
    end = min(len(dst), i + len(src))
    dst[i:end] += src[: end - i]


# ---------------------------------------------------------------- 界面

def ui_click(rng):
    """纸面按键：一下干净的纸木轻叩。"""
    t = _t(0.06)
    tap = _band(rng.standard_normal(len(t)), 1800, 7000) * np.exp(-t / 0.004)
    tone = np.sin(TAU * 1650 * t) * np.exp(-t / 0.012) * 0.35
    return _norm(tap * 0.7 + tone, -8)


def page(rng):
    """翻页：一阵纸的摩擦声，前轻后重，尾巴落下。"""
    t = _t(0.42)
    env = np.minimum(1, t / 0.11) * np.exp(-np.maximum(0, t - 0.11) / 0.07)
    flutter = 1 + 0.35 * np.sin(TAU * 31 * t + rng.uniform(0, TAU))
    swish = _band(rng.standard_normal(len(t)), 700, 6000) * env * flutter
    snap = _band(rng.standard_normal(len(t)), 1500, 5000) * np.exp(-np.maximum(0, t - 0.17) / 0.01) * (t > 0.17) * 0.5
    return _norm(_room(swish + snap, 0.4, 0.12), -9)


def evidence(rng):
    """记下证物：钢笔在纸上划两笔，钢片琴轻轻一响。"""
    out = np.zeros(int(1.6 * SR))
    for at, dur in ((0.0, 0.11), (0.15, 0.08)):
        t = _t(dur)
        grain = np.abs(rng.standard_normal(len(t))) ** 2
        grain = np.convolve(grain, np.ones(60) / 60, mode='same')
        stroke = _band(rng.standard_normal(len(t)), 2200, 7500) * grain * np.sin(np.pi * t / dur)
        _place(out, stroke * 0.6, at)
    _place(out, synth.celesta(83, 70, 0.4, rng), 0.3)    # B5
    _place(out, synth.celesta(90, 52, 0.4, rng) * 0.6, 0.38)  # F#6
    return _norm(_room(out, 1.2, 0.22), -6)


def replay(rng):
    """进入重演：一串倒放的钢片琴，像时间往回卷。"""
    n = int(1.6 * SR)
    out = np.zeros(n)
    for k, p in enumerate((71, 74, 78, 83, 86)):
        _place(out, synth.celesta(p, 70, 0.3, rng), 0.12 * k)
    out = _room(out, 1.4, 0.3)[::-1].copy()
    t = np.arange(len(out)) / SR
    whoosh = _band(rng.standard_normal(len(out)), 400, 3000) * (t / t[-1]) ** 3 * 0.25
    out = out + whoosh
    fade = int(0.08 * SR)
    out[-fade:] *= np.linspace(1, 0, fade)
    return _norm(out, -6)


def answer_ok(rng):
    """答对：两声小钟，属音落到主音。"""
    out = np.zeros(int(3.0 * SR))
    _place(out, synth.bell(90, 80, 1, rng, stretch=0.45), 0.0)   # F#6
    _place(out, synth.bell(95, 90, 1, rng, stretch=0.55), 0.2)   # B6
    return _norm(_room(out, 1.6, 0.25), -6)


def answer_wrong(rng):
    """答错：低音区一记闷住的和弦，木头般短促。"""
    out = np.zeros(int(1.1 * SR))
    for p in (35, 36, 42):
        _place(out, synth.piano(p, 70, 0.18, rng, pedal=0.0), 0.0)
    t = _t(0.3)
    thump = np.sin(TAU * (70 - 30 * t) * t) * np.exp(-t / 0.06)
    _place(out, thump * 0.6, 0.0)
    return _norm(_room(out, 0.7, 0.15), -6)


def tuning_fork(rng):
    """音叉：A4 敲击，初始的金属“叮”很快散去，只留纯音。"""
    t = _t(3.6)
    tone = np.sin(TAU * 440 * t) * np.exp(-t / 1.8)
    clang = np.sin(TAU * 440 * 6.27 * t) * np.exp(-t / 0.05) * 0.4
    hit = _band(rng.standard_normal(len(t)), 2000, 8000) * np.exp(-t / 0.002) * 0.3
    x = (tone + clang + hit) * np.minimum(1, t / 0.001)
    return _norm(_room(x, 0.9, 0.2), -6)


# ---------------------------------------------------------------- 门与钟

def door_unbar(rng):
    """隔离门：外闩抬起——木铁刮擦、一声闷响，门板上的铁件余振。"""
    out = np.zeros(int(2.2 * SR))
    t = _t(0.45)
    scrape_env = np.minimum(1, t / 0.3) * np.exp(-np.maximum(0, t - 0.32) / 0.04)
    rough = 1 + 0.6 * np.sign(np.sin(TAU * (18 + 30 * t) * t))
    _place(out, _band(rng.standard_normal(len(t)), 300, 1600) * scrape_env * rough * 0.5, 0.0)
    t2 = _t(0.5)
    clunk = np.sin(TAU * (95 - 25 * t2) * t2) * np.exp(-t2 / 0.09)
    clunk += _band(rng.standard_normal(len(t2)), 200, 2500) * np.exp(-t2 / 0.012) * 0.8
    _place(out, clunk, 0.42)
    _place(out, synth.bell(78, 40, 1, rng, stretch=0.18) * 0.35, 0.44)
    return _norm(_room(out, 1.2, 0.25), -4)


def door_open(rng):
    """寝区门转开：合页吱的一声（粘滑摩擦的脉冲串）。"""
    t = _t(0.55)
    rate = 28 + 50 * t / t[-1]
    phase = TAU * np.cumsum(rate) / SR
    pulses = np.maximum(0, np.sin(phase)) ** 12
    body = np.sin(TAU * np.cumsum(300 + 120 * t / t[-1]) / SR) * pulses
    env = np.sin(np.pi * t / t[-1]) ** 0.6
    creak = _band(body + 0.3 * pulses * rng.standard_normal(len(t)), 250, 3000) * env
    return _norm(_room(creak, 0.5, 0.15), -12)


def door_close(rng):
    """寝区门合上：木门撞框的一声闷响。"""
    t = _t(0.35)
    thud = np.sin(TAU * (120 - 50 * t) * t) * np.exp(-t / 0.05)
    knock = _band(rng.standard_normal(len(t)), 300, 2000) * np.exp(-t / 0.008) * 0.6
    return _norm(_room(thud + knock, 0.5, 0.15), -10)


def bell_slot(rng):
    """学园的时钟：每个时段开始敲一下。"""
    out = synth.bell(71, 90, 1, rng, stretch=0.8)
    return _norm(_room(out, 2.4, 0.3), -5)


def tube(rng):
    """铜口：盖片一响，管道里共鸣的嘶声。"""
    t = _t(0.9)
    hiss = rng.standard_normal(len(t))
    delay = int(SR / 340)  # 管长对应的梳状共振
    y = hiss.copy()
    for _ in range(3):
        y[delay:] += 0.6 * y[:-delay]
    y = _band(y, 250, 2400) * np.minimum(1, t / 0.05) * np.exp(-t / 0.35) * 0.15
    clack = _band(rng.standard_normal(len(t)), 800, 5000) * np.exp(-t / 0.006)
    clack += np.sin(TAU * 1240 * t) * np.exp(-t / 0.03) * 0.4
    hum = np.sin(TAU * 117 * t) * np.minimum(1, t / 0.1) * np.exp(-t / 0.4) * 0.15
    return _norm(_room(clack + y + hum, 0.8, 0.2), -6)


SFX = {
    'ui_click': ui_click, 'page': page, 'evidence': evidence, 'replay': replay,
    'answer_ok': answer_ok, 'answer_wrong': answer_wrong, 'tuning_fork': tuning_fork,
    'door_unbar': door_unbar, 'door_open': door_open, 'door_close': door_close,
    'bell_slot': bell_slot, 'tube': tube,
}


# ---------------------------------------------------------------- 对白提示音
# 基频沿用 StoryDialogue.gd 中响度补偿所依据的音高：
# 主厨约 141 Hz、侦探约 200 Hz、工程师约 315 Hz、音乐家约 489 Hz、铜口约 468 Hz。

def _blip(t, f0, partials, tau, attack):
    x = np.zeros_like(t)
    for ratio, amp, tdiv in partials:
        x += amp * np.sin(TAU * f0 * ratio * t) * np.exp(-t / (tau / tdiv))
    return x * np.minimum(1, t / attack)


def voice_detective(rng):
    """侦探：木质的“嗒”，像铅笔头点在笔记本上。"""
    t = _t(0.11)
    return _blip(t, 200, [(1, 1, 1), (2, 0.35, 1.5), (3.98, 0.25, 3)], 0.04, 0.002)


def voice_engineer(rng):
    """霍铎：黄铜般的短促，带一点金属的棱。"""
    t = _t(0.1)
    parts = [(k, 1.0 / k, 1 + 0.3 * k) for k in (1, 3, 5, 7)] + [(6.1, 0.12, 6)]
    return _blip(t, 315, parts, 0.035, 0.0015)


def voice_chef(rng):
    """沈鼎：圆而暖，像指节敲在厚陶罐上。"""
    t = _t(0.14)
    return _blip(t, 141, [(1, 1, 1), (2, 0.45, 1.3), (3, 0.12, 2)], 0.055, 0.004)


def voice_musician(rng):
    """苏泠：清亮如音叉，尾音稍长。"""
    t = _t(0.14)
    return _blip(t, 489, [(1, 1, 1), (2, 0.08, 1.4), (5.4, 0.05, 5)], 0.06, 0.002)


def voice_tube(rng):
    """铜口：隔着管道，闷而带金属回声。"""
    t = _t(0.16)
    x = _blip(t, 468, [(1, 1, 1), (0.5, 0.5, 0.8), (2, 0.3, 1.5), (2.9, 0.15, 2)], 0.045, 0.003)
    echo = int(0.028 * SR)
    x[echo:] += 0.35 * x[:-echo]
    return _band(x, 300, 2200)


VOICES = {'voice_detective': voice_detective, 'voice_engineer': voice_engineer, 'voice_chef': voice_chef,
          'voice_musician': voice_musician, 'voice_tube': voice_tube}


def finish_voice(x):
    """提示音统一 RMS，首尾无爆音。"""
    fade = int(0.01 * SR)
    x = x.copy()
    x[-fade:] *= np.linspace(1, 0, fade)
    rms = np.sqrt(np.mean(x ** 2)) + 1e-12
    x *= 10 ** (-17 / 20) / rms
    return np.clip(x, -0.98, 0.98)
