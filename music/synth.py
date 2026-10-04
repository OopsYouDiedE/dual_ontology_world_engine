"""《安眠者有福》原声合成器：只用 numpy，把曲谱渲染成立体声音频。

音色都按 1890—1910 年代学园里可能有的乐器建模：钢琴、簧风琴、钢片琴、八音盒、
管钟、低音弦乐（拉奏与拨奏）与单簧管。全部为加法合成，不依赖外部录音与音色库，
同一输入得到逐位相同的波形（随机数固定种子）。
"""
import numpy as np

SR = 44100
A4 = 440.0
TAU = 2 * np.pi


def hz(pitch, cents=0.0):
    return A4 * 2.0 ** ((pitch - 69 + cents / 100.0) / 12.0)


def _env_ar(n, attack, release_start, release, sr=SR):
    """线性起音 + 指数释放的包络（样本数 n）。"""
    t = np.arange(n) / sr
    env = np.minimum(1.0, t / max(attack, 1e-4))
    rel = t > release_start
    env[rel] *= np.exp(-(t[rel] - release_start) / max(release, 1e-4))
    return env


def _partials(t, freqs, amps, taus, phases=None):
    out = np.zeros_like(t)
    for i, (f, a, tau) in enumerate(zip(freqs, amps, taus)):
        if f >= SR * 0.45 or a <= 0:
            continue
        ph = 0.0 if phases is None else phases[i]
        out += a * np.exp(-t / tau) * np.sin(TAU * f * t + ph)
    return out


def _noise_burst(n, rng, cutoff, sr=SR):
    """一段经频域低通的噪声。"""
    noise = rng.standard_normal(n)
    spec = np.fft.rfft(noise)
    f = np.fft.rfftfreq(n, 1 / sr)
    spec *= 1.0 / (1.0 + (f / cutoff) ** 2)
    return np.fft.irfft(spec, n)


# ---------------------------------------------------------------- 乐器

def piano(pitch, vel, length, rng, pedal=0.5):
    f0 = hz(pitch)
    v = vel / 127.0
    ring = float(np.clip(9.0 * 2 ** (-(pitch - 36) / 18.0), 0.5, 11.0))
    n = int((length + pedal + 0.6) * SR)
    n = min(n, int((length + min(ring, 4.0)) * SR) + int(0.5 * SR))
    t = np.arange(n) / SR
    count = int(min(26, 15000 / f0))
    bright = 0.2 - 0.15 * v
    B = 1.2e-4 * 2 ** ((pitch - 60) / 30.0)
    out = np.zeros(n)
    for k in range(1, count + 1):
        fk = f0 * k * np.sqrt(1 + B * k * k)
        ak = k ** -1.05 * np.exp(-(k - 1) * bright)
        if k == 1:
            ak *= 0.9
        slow = ring / (1 + 0.22 * (k - 1))
        fast = slow * 0.16
        env = 0.55 * np.exp(-t / fast) + 0.45 * np.exp(-t / slow)
        for det in (-0.35, 0.35):  # 两根弦的细微失谐产生拍音
            out += 0.5 * ak * env * np.sin(TAU * fk * (1 + det * 1e-3) * t + rng.uniform(0, TAU))
    hammer = _noise_burst(n, rng, 2500 + 3000 * v) * np.exp(-t / 0.006) * 0.25 * v
    out += hammer
    damper = np.ones(n)
    off = t > length + pedal
    damper[off] = np.exp(-(t[off] - length - pedal) / 0.14)
    out *= damper * np.minimum(1.0, t / 0.002)
    return out * (0.35 + 0.65 * v) * 0.5


def celesta(pitch, vel, length, rng):
    f0 = hz(pitch)
    v = vel / 127.0
    scale = 2 ** (-(pitch - 72) / 24.0)
    n = int((2.6 * scale + 0.3) * SR)
    t = np.arange(n) / SR
    ratios = [1.0, 2.0, 3.0, 4.16, 9.3]
    amps = [1.0, 0.16, 0.05, 0.11 + 0.08 * v, 0.04 * v]
    taus = [2.0 * scale, 0.8 * scale, 0.45 * scale, 0.2 * scale, 0.05]
    out = _partials(t, [f0 * r for r in ratios], amps, taus, rng.uniform(0, TAU, 5))
    out += _noise_burst(n, rng, 6000) * np.exp(-t / 0.003) * 0.15 * v
    out *= np.minimum(1.0, t / 0.0015)
    return out * (0.3 + 0.7 * v) * 0.45


def musicbox(pitch, vel, length, rng):
    """八音盒：簧片的高次泛音不成整数倍；每个音略有走音，像发条将尽。"""
    f0 = hz(pitch, rng.uniform(-7, 7))
    v = vel / 127.0
    scale = 2 ** (-(pitch - 76) / 30.0)
    n = int((2.8 * scale + 0.2) * SR)
    t = np.arange(n) / SR
    ratios = [1.0, 2.0, 5.93, 16.2]
    amps = [1.0, 0.08, 0.22, 0.06]
    taus = [2.4 * scale, 1.0 * scale, 0.35 * scale, 0.08]
    out = _partials(t, [f0 * r for r in ratios], amps, taus, rng.uniform(0, TAU, 4))
    out += _noise_burst(n, rng, 7000) * np.exp(-t / 0.002) * 0.25
    return out * (0.4 + 0.6 * v) * 0.42


def _sustained(t, f0, harmonics, vib_rate, vib_depth, vib_delay, rng, detunes=(0.0,)):
    """持续音：带颤音的谐波叠加；detunes 以音分表示多个声部的齐奏。"""
    out = np.zeros_like(t)
    for d in detunes:
        rate = vib_rate * rng.uniform(0.93, 1.07)
        depth = vib_depth * np.minimum(1.0, np.maximum(0.0, (t - vib_delay) / 0.4))
        inst = f0 * 2 ** (d / 1200.0) * (1 + depth * np.sin(TAU * rate * t + rng.uniform(0, TAU)))
        phase = TAU * np.cumsum(inst) / SR
        for k, a in harmonics:
            if f0 * k >= SR * 0.42:
                break
            out += a * np.sin(k * phase + rng.uniform(0, TAU))
    return out / len(detunes)


def organ(pitch, vel, length, rng):
    """簧风琴：偶次谐波较弱，两排簧片微差产生合唱感，带一点风箱起伏。"""
    f0 = hz(pitch)
    v = vel / 127.0
    rel = 0.16
    n = int((length + rel * 5) * SR)
    t = np.arange(n) / SR
    harm = []
    for k in range(1, 24):
        a = k ** -1.05 * (0.55 if k % 2 == 0 else 1.0) / (1 + (f0 * k / 3400.0) ** 2)
        harm.append((k, a))
    out = _sustained(t, f0, harm, 0.0, 0.0, 0.0, rng, detunes=(-2.5, 2.5))
    bellows = 1 + 0.025 * np.sin(TAU * 0.7 * t + rng.uniform(0, TAU))
    out *= bellows * _env_ar(n, 0.05, length, rel)
    return out * (0.25 + 0.75 * v) * 0.24


def strings(pitch, vel, length, rng):
    """低音弦乐齐奏（大提琴与低音提琴）：锯齿波谐波，三人齐奏，慢起音。"""
    f0 = hz(pitch)
    v = vel / 127.0
    rel = 0.45
    n = int((length + rel * 4) * SR)
    t = np.arange(n) / SR
    harm = [(k, (1.0 / k) / (1 + (f0 * k / 2100.0) ** 2) * (1.25 if 180 < f0 * k < 420 else 1.0))
            for k in range(1, 30)]
    out = _sustained(t, f0, harm, 5.3, 0.0022, 0.25, rng, detunes=(-6.0, 0.0, 6.5))
    out *= _env_ar(n, min(0.32, length * 0.5), length, rel)
    return out * (0.25 + 0.75 * v) * 0.30


def pizz(pitch, vel, length, rng):
    f0 = hz(pitch)
    v = vel / 127.0
    scale = 2 ** (-(pitch - 48) / 24.0)
    n = int((0.9 * scale + 0.2) * SR)
    t = np.arange(n) / SR
    count = int(min(16, 6000 / f0))
    freqs = [f0 * k for k in range(1, count + 1)]
    amps = [k ** -1.5 for k in range(1, count + 1)]
    taus = [0.42 * scale / (1 + 0.35 * (k - 1)) for k in range(1, count + 1)]
    out = _partials(t, freqs, amps, taus, rng.uniform(0, TAU, count))
    out *= np.minimum(1.0, t / 0.003)
    return out * (0.3 + 0.7 * v) * 0.5


def clarinet(pitch, vel, length, rng):
    f0 = hz(pitch)
    v = vel / 127.0
    rel = 0.09
    n = int((length + rel * 5) * SR)
    t = np.arange(n) / SR
    harm = []
    for k in range(1, 18):
        a = (1.0 / k if k % 2 else 0.12 / k) / (1 + (f0 * k / (1800 + 1600 * v)) ** 2)
        harm.append((k, a))
    out = _sustained(t, f0, harm, 4.8, 0.0018, 0.35, rng)
    breath = _noise_burst(n, rng, 3000) * 0.02
    out = (out + breath) * _env_ar(n, 0.045, length, rel)
    return out * (0.3 + 0.7 * v) * 0.32


BELL_PARTIALS = [  # (相对击音的频率比, 振幅, 衰减秒)
    (0.25, 0.30, 7.0), (0.5, 0.55, 4.0), (0.597, 0.45, 3.4), (0.75, 0.25, 2.6),
    (1.0, 1.0, 3.0), (1.26, 0.18, 1.6), (1.5, 0.22, 1.2), (2.0, 0.12, 0.7), (2.64, 0.06, 0.4)]


def bell(pitch, vel, length, rng, stretch=1.0):
    f0 = hz(pitch)
    v = vel / 127.0
    n = int(7.5 * stretch * SR)
    t = np.arange(n) / SR
    freqs = [f0 * r for r, _, _ in BELL_PARTIALS]
    amps = [a for _, a, _ in BELL_PARTIALS]
    taus = [d * stretch for _, _, d in BELL_PARTIALS]
    out = _partials(t, freqs, amps, taus, rng.uniform(0, TAU, len(freqs)))
    out += _noise_burst(n, rng, 5000) * np.exp(-t / 0.004) * 0.2 * v
    out *= np.minimum(1.0, t / 0.001)
    return out * (0.3 + 0.7 * v) * 0.28


VOICES = {'piano': piano, 'celesta': celesta, 'musicbox': musicbox, 'organ': organ,
          'strings': strings, 'pizz': pizz, 'clarinet': clarinet, 'bell': bell}


# ---------------------------------------------------------------- 空间

def impulse_response(seconds, seed, sr=SR, predelay=0.018, damping=6000.0):
    """合成的房间冲激响应：早期反射 + 指数衰减、逐渐变暗的尾音。"""
    rng = np.random.default_rng(seed)
    n = int((seconds * 1.3 + predelay) * sr)
    t = np.arange(n) / sr
    tail = rng.standard_normal(n) * np.exp(-6.9 * t / seconds)
    # 尾音越晚越暗：把早段与经低通的晚段交叉混合
    dark = _noise_burst(n, rng, damping * 0.35) * np.exp(-6.9 * t / seconds)
    mix = np.clip(t / seconds, 0, 1)
    tail = tail * (1 - mix) * 0.6 + dark * mix * 1.6
    tail[: int(predelay * sr)] = 0
    for delay, gain in ((0.011, 0.5), (0.019, 0.38), (0.027, 0.3), (0.041, 0.24), (0.053, 0.2), (0.067, 0.16)):
        i = int((delay + rng.uniform(-0.002, 0.002)) * sr)
        tail[i] += gain * rng.choice((-1, 1)) * 8
    spec = np.fft.rfft(tail)
    f = np.fft.rfftfreq(n, 1 / sr)
    spec *= 1.0 / np.sqrt(1.0 + (f / damping) ** 2)
    ir = np.fft.irfft(spec, n)
    return ir / np.sqrt(np.sum(ir ** 2))


def convolve(signal, ir):
    n = len(signal) + len(ir) - 1
    size = 1 << (n - 1).bit_length()
    out = np.fft.irfft(np.fft.rfft(signal, size) * np.fft.rfft(ir, size), size)
    return out[:n]


def reverb(stereo, seconds, wet, seed=7):
    left = convolve(stereo[0], impulse_response(seconds, seed))
    right = convolve(stereo[1], impulse_response(seconds, seed + 1))
    out = np.zeros((2, len(left)))
    out[0, : stereo.shape[1]] += stereo[0] * (1 - wet)
    out[1, : stereo.shape[1]] += stereo[1] * (1 - wet)
    out[0] += left * wet * 0.55
    out[1] += right * wet * 0.55
    return out


def pan_gains(pan):
    angle = (pan + 1) * np.pi / 4
    return np.cos(angle), np.sin(angle)


def compress(stereo, ratio=1.6, below_rms_db=4.0, attack=0.03, release=0.35, sr=SR):
    """总线压缩：门限取整曲平均响度以下 below_rms_db，按 RMS 包络柔和地收拢动态，
    让安静段与全奏段的落差适合作为游戏背景。ratio 为压缩比，ratio < 1 时为扩展（不用）。"""
    hop = 512
    threshold_db = 20 * np.log10(np.sqrt(np.mean(stereo ** 2)) + 1e-12) - below_rms_db
    mono = np.sqrt(np.mean(stereo ** 2, axis=0))
    frames = len(mono) // hop + 1
    padded = np.pad(mono, (0, frames * hop - len(mono)))
    level = 20 * np.log10(np.sqrt(np.mean(padded.reshape(frames, hop) ** 2, axis=1)) + 1e-9)
    over = np.maximum(0.0, level - threshold_db)
    target = -over * (1 - 1 / ratio)
    # 向上压缩：比门限低 12 dB 以内的安静段按同一比率抬起，再往下的（静音、尾音）不动
    under = np.clip(threshold_db - level, 0.0, 12.0) * (level > threshold_db - 30)
    target += under * (1 - 1 / ratio) * 0.6
    gain = np.zeros(frames)
    g = 0.0
    a_coef = np.exp(-hop / (attack * sr))
    r_coef = np.exp(-hop / (release * sr))
    for k in range(frames):
        coef = a_coef if target[k] < g else r_coef
        g = coef * g + (1 - coef) * target[k]
        gain[k] = g
    curve = np.interp(np.arange(stereo.shape[1]), np.arange(frames) * hop + hop / 2, gain)
    return stereo * 10 ** (curve / 20)


def master(stereo, target_rms_db=-19.0, ceiling_db=-1.2):
    """响度对齐到目标 RMS，并以柔和削波限制峰值。"""
    rms = np.sqrt(np.mean(stereo ** 2)) + 1e-12
    gain = 10 ** (target_rms_db / 20) / rms
    out = stereo * gain
    ceiling = 10 ** (ceiling_db / 20)
    knee = ceiling * 0.8
    mag = np.abs(out)
    over = mag > knee
    out[over] = np.sign(out[over]) * (knee + (ceiling - knee) * np.tanh((mag[over] - knee) / (ceiling - knee)))
    return out
