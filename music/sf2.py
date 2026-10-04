"""SoundFont 2 / 3 采样器：读取 MuseScore 4 自带的 MS Basic.sf3（MIT，FluidR3 衍生）。

按 SoundFont 2.01 规范与 FluidSynth 的处理方式实现本作用到的部分：
- 预设 → 乐器分区展开（音域、力度层、全局分区、预设层生成器相加）；
- SF3 的逐段 Ogg Vorbis 采样解码；根音、粗/细调、音阶缩放、循环；
- 调制器：默认调制器 + 乐器/预设层调制器（同一标识则覆盖），来源支持力度、音高与常用控制器默认值，
  四种变换（线性、凹、凸、开关）与单/双极性、方向；
- 体积包络（延迟/起音/保持/衰减/延音/释放，起音线性、其余按分贝线性）与按音高缩放的保持/衰减；
- 调制包络驱动随时间变化的二阶低通（短时傅里叶分块实现），初始衰减按 FluidSynth 的 0.4 系数。
不实现 LFO、合唱、混响发送与音高包络（本作的音色不依赖它们）。
"""
import io
import struct

import numpy as np

SR = 44100

GEN_NAMES = {
    0: 'startAddrsOffset', 1: 'endAddrsOffset', 2: 'startloopAddrsOffset', 3: 'endloopAddrsOffset',
    4: 'startAddrsCoarseOffset', 7: 'modEnvToPitch', 8: 'initialFilterFc', 9: 'initialFilterQ',
    11: 'modEnvToFilterFc', 12: 'endAddrsCoarseOffset', 13: 'modLfoToVolume', 17: 'pan',
    25: 'delayModEnv', 26: 'attackModEnv', 27: 'holdModEnv', 28: 'decayModEnv', 29: 'sustainModEnv',
    30: 'releaseModEnv', 31: 'keynumToModEnvHold', 32: 'keynumToModEnvDecay',
    33: 'delayVolEnv', 34: 'attackVolEnv', 35: 'holdVolEnv', 36: 'decayVolEnv', 37: 'sustainVolEnv',
    38: 'releaseVolEnv', 39: 'keynumToVolEnvHold', 40: 'keynumToVolEnvDecay', 41: 'instrument',
    43: 'keyRange', 44: 'velRange', 45: 'startloopAddrsCoarseOffset', 48: 'initialAttenuation',
    50: 'endloopAddrsCoarseOffset', 51: 'coarseTune', 52: 'fineTune', 53: 'sampleID', 54: 'sampleModes',
    56: 'scaleTuning', 58: 'overridingRootKey',
}
ENV = ['delayVolEnv', 'attackVolEnv', 'holdVolEnv', 'decayVolEnv', 'releaseVolEnv',
       'delayModEnv', 'attackModEnv', 'holdModEnv', 'decayModEnv', 'releaseModEnv']
DEFAULTS = dict({k: -12000 for k in ENV}, initialFilterFc=13500, initialFilterQ=0, modEnvToFilterFc=0,
                modEnvToPitch=0, pan=0, sustainVolEnv=0, sustainModEnv=0, keynumToModEnvHold=0,
                keynumToModEnvDecay=0, keynumToVolEnvHold=0, keynumToVolEnvDecay=0, initialAttenuation=0,
                coarseTune=0, fineTune=0, sampleModes=0, scaleTuning=100, overridingRootKey=-1,
                startAddrsOffset=0, endAddrsOffset=0, startloopAddrsOffset=0, endloopAddrsOffset=0,
                startAddrsCoarseOffset=0, endAddrsCoarseOffset=0, startloopAddrsCoarseOffset=0,
                endloopAddrsCoarseOffset=0, modLfoToVolume=0)
# 预设层只能对这些生成器做相对增量（地址、音域、采样号等不可）
PRESET_ADDITIVE = set(DEFAULTS) - {'sampleModes', 'overridingRootKey', 'startAddrsOffset', 'endAddrsOffset',
                                   'startloopAddrsOffset', 'endloopAddrsOffset', 'startAddrsCoarseOffset',
                                   'endAddrsCoarseOffset', 'startloopAddrsCoarseOffset', 'endloopAddrsCoarseOffset'}
# SoundFont 2.01 §8.4 默认调制器中影响本作的几条：(来源, 目标, 量, 量来源, 变换)
DEFAULT_MODS = [
    (0x0502, 48, 960, 0, 0),    # 力度 → 衰减（凹、反向）
    (0x0102, 8, -2400, 0, 0),   # 力度 → 截止频率（线性、反向）
    (0x0587, 48, 960, 0, 0),    # CC7 音量 → 衰减
    (0x058B, 48, 960, 0, 0),    # CC11 表情 → 衰减
]
CC_DEFAULTS = {7: 100, 10: 64, 11: 127, 91: 40, 93: 0, 1: 0, 64: 0}


def _chunks(data, start, end):
    i = start
    while i < end:
        cid = data[i:i + 4].decode('latin1')
        size = struct.unpack('<I', data[i + 4:i + 8])[0]
        yield cid, i + 8, size
        i += 8 + size + (size & 1)


def _tc(timecents):
    return 2.0 ** (timecents / 1200.0) if timecents > -12000 else 0.0


def _concave(x):
    x = np.clip(x, 0.0, 1.0)
    if x >= 1.0:
        return 1.0
    return float(np.clip(-20.0 / 96.0 * np.log10((1.0 - x) ** 2), 0.0, 1.0))


def _source_value(src, vel, key):
    if src == 0:
        return 1.0
    index, cc = src & 0x7F, bool(src & 0x80)
    direction, polar, kind = (src >> 8) & 1, (src >> 9) & 1, (src >> 10) & 0x3F
    if cc:
        # CC2（呼吸/表情）随力度：MuseScore 对“Expr.”音色按每个音的力度发送 CC2，力度到音量由它控制
        x = (vel if index == 2 else CC_DEFAULTS.get(index, 0)) / 127.0
    elif index == 2:
        x = vel / 127.0
    elif index == 3:
        x = key / 127.0
    elif index == 14:   # 弯音轮居中
        x = 0.5
    elif index == 16:   # 弯音灵敏度 2 半音
        x = 2 / 127.0
    else:               # 无控制器 / 压力等 —— 视为 0
        x = 0.0
    if direction:
        x = 1.0 - x
    if polar:
        x = 2.0 * x - 1.0
        sign = -1.0 if x < 0 else 1.0
        mag = abs(x)
        if kind == 1:
            mag = _concave(mag)
        elif kind == 2:
            mag = 1.0 - _concave(1.0 - mag)
        elif kind == 3:
            mag = 1.0 if mag >= 0.5 else 0.0
        return sign * mag
    if kind == 1:
        return _concave(x)
    if kind == 2:
        return 1.0 - _concave(1.0 - x)
    if kind == 3:
        return 1.0 if x >= 0.5 else 0.0
    return x


class SoundFont:
    def __init__(self, path):
        with open(path, 'rb') as f:
            data = f.read()
        self.data = data
        pdta = sdta = None
        for cid, off, size in _chunks(data, 12, len(data)):
            if cid == 'LIST':
                kind = data[off:off + 4].decode('latin1')
                if kind == 'pdta':
                    pdta = (off + 4, off + size)
                elif kind == 'sdta':
                    sdta = (off + 4, off + size)
        self.smpl = next((off, size) for cid, off, size in _chunks(data, *sdta) if cid == 'smpl')
        rec = {cid: data[off:off + size] for cid, off, size in _chunks(data, *pdta)}

        def table(name, fmt, size):
            raw = rec[name]
            return [struct.unpack(fmt, raw[i:i + size]) for i in range(0, len(raw), size)]
        self.presets = table('phdr', '<20sHHHIII', 38)
        self.pbag = table('pbag', '<HH', 4)
        self.pgen = table('pgen', '<Hh', 4)
        self.pmod = table('pmod', '<HHhHH', 10)
        self.insts = table('inst', '<20sH', 22)
        self.ibag = table('ibag', '<HH', 4)
        self.igen = table('igen', '<Hh', 4)
        self.imod = table('imod', '<HHhHH', 10)
        self.samples = table('shdr', '<20sIIIIIbbHH', 46)
        self._decoded = {}

    def preset_names(self):
        return [(b, p, n.split(b'\0')[0].decode('latin1')) for n, p, b, *_ in self.presets[:-1]]

    @staticmethod
    def _zone(gens, mods, bags, j):
        g0, m0 = bags[j]
        g1, m1 = bags[j + 1]
        out = {}
        for oper, amount in gens[g0:g1]:
            name = GEN_NAMES.get(oper)
            if name is None:
                continue
            if name in ('keyRange', 'velRange'):
                out[name] = (amount & 0xFF, (amount >> 8) & 0xFF)
            elif name in ('instrument', 'sampleID'):
                out[name] = amount & 0xFFFF
            else:
                out[name] = amount
        zone_mods = {}
        for src, dest, amount, amt_src, trans in mods[m0:m1]:
            zone_mods[(src, dest, amt_src)] = (amount, trans)
        return out, zone_mods

    def regions(self, bank, program):
        """预设展开为分区列表：每项含合并后的生成器与调制器。"""
        idx = next(i for i, (n, p, b, *_) in enumerate(self.presets[:-1]) if p == program and b == bank)
        pzones = [self._zone(self.pgen, self.pmod, self.pbag, j)
                  for j in range(self.presets[idx][3], self.presets[idx + 1][3])]
        pglobal, pglobal_mods = ({}, {})
        if pzones and 'instrument' not in pzones[0][0]:
            pglobal, pglobal_mods = pzones[0]
        out = []
        for pz, pz_mods in pzones:
            if 'instrument' not in pz:
                continue
            pgen = dict(pglobal, **pz)
            pmods = {**pglobal_mods, **pz_mods}
            inst = pz['instrument']
            izones = [self._zone(self.igen, self.imod, self.ibag, j)
                      for j in range(self.insts[inst][1], self.insts[inst + 1][1])]
            iglobal, iglobal_mods = ({}, {})
            if izones and 'sampleID' not in izones[0][0]:
                iglobal, iglobal_mods = izones[0]
            for iz, iz_mods in izones:
                if 'sampleID' not in iz:
                    continue
                g = dict(DEFAULTS, **iglobal)
                g.update(iz)
                for k, v in pgen.items():
                    if k in PRESET_ADDITIVE:
                        g[k] = g[k] + v
                key = _intersect(dict(iglobal, **iz).get('keyRange', (0, 127)), pgen.get('keyRange', (0, 127)))
                vel = _intersect(dict(iglobal, **iz).get('velRange', (0, 127)), pgen.get('velRange', (0, 127)))
                if not (key and vel):
                    continue
                mods = {(s, d, a): (amt, t) for s, d, amt, a, t in DEFAULT_MODS}
                mods.update(iglobal_mods)
                mods.update(iz_mods)
                g['keyRange'], g['velRange'] = key, vel
                g['mods'] = mods
                g['pmods'] = pmods
                out.append(g)
        return out

    def sample(self, sid):
        if sid in self._decoded:
            return self._decoded[sid]
        name, start, end, ls, le, rate, root, corr, link, stype = self.samples[sid]
        off = self.smpl[0]
        if stype & 0x10:  # SF3：每个采样是一段 Ogg Vorbis，循环点相对采样起点
            import soundfile as sf
            audio, rate = sf.read(io.BytesIO(self.data[off + start: off + end]), dtype='float32')
            if audio.ndim == 2:
                audio = audio.mean(1)
        else:
            audio = np.frombuffer(self.data[off + start * 2: off + end * 2], '<i2').astype(np.float32) / 32768.0
            ls, le = ls - start, le - start
        self._decoded[sid] = (audio, rate, root, corr, ls, le)
        return self._decoded[sid]

    def note(self, regions, pitch, vel, length, release_scale=1.0, sr=SR):
        """渲染一个音，返回立体声 (2, n)。length 为按键时长（秒），之后进入释放段。"""
        hits = [g for g in regions if g['keyRange'][0] <= pitch <= g['keyRange'][1]
                and g['velRange'][0] <= vel <= g['velRange'][1]]
        if not hits:
            near = min(regions, key=lambda g: min(abs(pitch - g['keyRange'][0]), abs(pitch - g['keyRange'][1])))
            hits = [g for g in regions if g['keyRange'] == near['keyRange']
                    and g['velRange'][0] <= vel <= g['velRange'][1]] or [near]
        parts = [self._voice(g, pitch, vel, length, release_scale, sr) for g in hits]
        n = max(p.shape[1] for p in parts)
        out = np.zeros((2, n), np.float32)
        for p in parts:
            out[:, :p.shape[1]] += p
        return out

    def _params(self, g, pitch, vel):
        p = {k: v for k, v in g.items() if k not in ('mods', 'pmods')}
        for (src, dest, amt_src), (amount, trans) in g['mods'].items():
            name = GEN_NAMES.get(dest)
            if name in p:
                p[name] += amount * _source_value(src, vel, pitch) * _source_value(amt_src, vel, pitch)
        for (src, dest, amt_src), (amount, trans) in g['pmods'].items():
            name = GEN_NAMES.get(dest)
            if name in p:
                p[name] += amount * _source_value(src, vel, pitch) * _source_value(amt_src, vel, pitch)
        return p

    def _voice(self, g, pitch, vel, length, release_scale, sr):
        audio, rate, root, corr, ls, le = self.sample(g['sampleID'])
        p = self._params(g, pitch, vel)
        # 力度与默认调制器的衰减已算入调制；初始衰减生成器部分按 FluidSynth 乘 0.4
        mod_atten = p['initialAttenuation'] - g['initialAttenuation']
        atten_cb = g['initialAttenuation'] * 0.4 + mod_atten
        start = int(p['startAddrsOffset'] + 32768 * p['startAddrsCoarseOffset'])
        end = len(audio) + int(p['endAddrsOffset'] + 32768 * p['endAddrsCoarseOffset'])
        ls = ls + int(p['startloopAddrsOffset'] + 32768 * p['startloopAddrsCoarseOffset'])
        le = le + int(p['endloopAddrsOffset'] + 32768 * p['endloopAddrsCoarseOffset'])
        start = int(np.clip(start, 0, max(0, end - 4)))
        root_key = p['overridingRootKey'] if p['overridingRootKey'] >= 0 else root
        cents = (pitch - root_key) * p['scaleTuning'] + p['coarseTune'] * 100 + p['fineTune'] + corr
        ratio = 2.0 ** (cents / 1200.0) * rate / sr
        key_off = 60 - pitch
        delay, attack = _tc(p['delayVolEnv']), _tc(p['attackVolEnv'])
        hold = _tc(p['holdVolEnv'] + p['keynumToVolEnvHold'] * key_off)
        decay = _tc(p['decayVolEnv'] + p['keynumToVolEnvDecay'] * key_off)
        sustain_db = float(np.clip(p['sustainVolEnv'], 0, 1440)) / 10.0
        release = max(0.02, _tc(p['releaseVolEnv']) * release_scale)
        looping = int(p['sampleModes']) in (1, 3) and 0 <= ls < le <= len(audio) and le - ls > 8
        total = length + min(release, 8.0) * 1.15
        if not looping:
            total = min(total, (end - start) / (ratio * sr) + 0.005)
        n = max(32, int(total * sr))
        pos = start + np.arange(n) * ratio
        if looping:
            over = pos >= le
            pos[over] = ls + np.mod(pos[over] - ls, le - ls)
        pos = np.clip(pos, 0, len(audio) - 2)
        i0 = pos.astype(np.int64)
        frac = (pos - i0).astype(np.float32)
        wave = audio[i0] * (1 - frac) + audio[i0 + 1] * frac
        t = np.arange(n) / sr
        env_db = _vol_env(t, delay, attack, hold, decay, sustain_db, length, release)
        wave = wave * np.power(10.0, (env_db - atten_cb / 10.0) / 20.0).astype(np.float32)
        # 调制包络 → 截止频率
        fc_cents = p['initialFilterFc']
        depth = p['modEnvToFilterFc']
        if depth:
            menv = _mod_env(t, _tc(p['delayModEnv']), _tc(p['attackModEnv']),
                            _tc(p['holdModEnv'] + p['keynumToModEnvHold'] * key_off),
                            _tc(p['decayModEnv'] + p['keynumToModEnvDecay'] * key_off),
                            1.0 - float(np.clip(p['sustainModEnv'], 0, 1000)) / 1000.0, length,
                            max(0.02, _tc(p['releaseModEnv'])))
            fc = fc_cents + depth * menv
        else:
            fc = np.full(n, fc_cents, dtype=np.float64)
        wave = _tv_lowpass(wave, 8.176 * 2.0 ** (np.clip(fc, 1500, 13500) / 1200.0), sr)
        pan = float(np.clip(p['pan'] / 500.0, -1, 1))
        angle = (pan + 1) * np.pi / 4
        return np.stack([wave * np.cos(angle), wave * np.sin(angle)]).astype(np.float32)


def _vol_env(t, delay, attack, hold, decay, sustain_db, length, release):
    a0, a1 = delay, delay + attack
    h1 = a1 + hold
    env = np.full(len(t), -100.0)
    rising = (t >= a0) & (t < a1)
    env[rising] = 20 * np.log10(np.maximum((t[rising] - a0) / max(attack, 1e-5), 1e-5))
    env[(t >= a1) & (t < h1)] = 0.0
    dec = t >= h1
    env[dec] = np.maximum(-sustain_db, -100.0 * (t[dec] - h1) / decay) if decay > 0 else -sustain_db
    rel = t >= length
    if rel.any():
        i = min(int(length * SR), len(t) - 1)
        env[rel] = env[i] - 100.0 * (t[rel] - length) / release
    return np.maximum(env, -100.0)


def _mod_env(t, delay, attack, hold, decay, sustain, length, release):
    a0, a1 = delay, delay + attack
    h1 = a1 + hold
    env = np.zeros(len(t))
    rising = (t >= a0) & (t < a1)
    env[rising] = (t[rising] - a0) / max(attack, 1e-5)
    env[(t >= a1) & (t < h1)] = 1.0
    dec = t >= h1
    env[dec] = np.maximum(sustain, 1.0 - (1.0 - sustain) * (t[dec] - h1) / max(decay, 1e-5))
    rel = t >= length
    if rel.any():
        i = min(int(length * SR), len(t) - 1)
        env[rel] = np.maximum(0.0, env[i] * (1.0 - (t[rel] - length) / release))
    return env


def _tv_lowpass(x, fc, sr, block=1024):
    """随时间变化的二阶低通：50% 重叠的汉宁窗分块，各块按自己的截止频率滤波后叠加。"""
    if np.min(fc) >= sr * 0.45:
        return x
    n = len(x)
    hop = block // 2
    pad = np.concatenate([np.zeros(hop, np.float32), x, np.zeros(block + hop, np.float32)])
    count = (n + hop) // hop + 1
    idx = np.arange(block)[None, :] + hop * np.arange(count)[:, None]
    win = np.hanning(block + 1)[:-1].astype(np.float32)
    frames = pad[idx] * win
    size = block * 2
    spec = np.fft.rfft(frames, size, axis=1)
    f = np.fft.rfftfreq(size, 1 / sr)
    centers = np.clip(hop * np.arange(count) - hop + block // 2 - hop, 0, n - 1)
    fcs = fc[np.clip(centers, 0, len(fc) - 1)][:, None]
    spec *= 1.0 / np.sqrt(1.0 + (f[None, :] / fcs) ** 4)
    out_frames = np.fft.irfft(spec, size, axis=1)
    out = np.zeros(len(pad) + block, np.float64)
    for k in range(count):
        out[k * hop: k * hop + size] += out_frames[k]
    return out[hop: hop + n].astype(np.float32)


def _intersect(a, b):
    lo, hi = max(a[0], b[0]), min(a[1], b[1])
    return (lo, hi) if lo <= hi else None
