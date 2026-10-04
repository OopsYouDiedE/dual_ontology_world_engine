"""《安眠者有福》原声一键生成。

    python music/build_soundtrack.py              # 曲谱、配乐、音效与对白提示音全部重建
    python music/build_soundtrack.py --only night # 只重建某一首（或某个音效 id）
    python music/build_soundtrack.py --scores     # 只写曲谱（.mscz / .mid，及下述 PDF / MusicXML）

输出：
    music/Blessed Sleepers - <曲名>.mscz / .mid        曲谱原稿（MuseScore 4 可打开编辑）
    music/Blessed Sleepers - <曲名>.pdf / .musicxml    由 MuseScore 4 导出的总谱与交换格式（装有 MuseScore 时）
    godot_csharp/ShareAsset/Internal/Props/Audio/Music/<id>.ogg   游戏内配乐（无缝循环）
    godot_csharp/ShareAsset/Internal/Props/Audio/Sfx/<id>.ogg     音效
    godot_csharp/ShareAsset/Internal/Characters/voices/voice_*.wav 对白提示音
依赖：numpy、soundfile（OGG Vorbis 编码）；可选 MuseScore 4（环境变量 MUSESCORE 或默认安装路径）。
"""
import argparse
import os
import shutil
import subprocess
import sys
import wave
import zlib

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import cues  # noqa: E402
import interpret  # noqa: E402
import sfx  # noqa: E402
import synth  # noqa: E402
from score_io import write_midi, write_mscz  # noqa: E402

ROOT = os.path.dirname(HERE)
ASSETS = os.path.join(ROOT, 'godot_csharp', 'ShareAsset', 'Internal')
MUSIC_OUT = os.path.join(ASSETS, 'Props', 'Audio', 'Music')
SFX_OUT = os.path.join(ASSETS, 'Props', 'Audio', 'Sfx')
VOICE_OUT = os.path.join(ASSETS, 'Characters', 'voices')
SCORE_NAMES = {
    'main_theme': 'Main Theme', 'door_side': 'The Near Side of the Door', 'confrontation': 'Confrontation',
    'day': 'Days of Sixteen', 'night': 'Sleep', 'day_noon': 'Noon', 'day_afternoon': 'Afternoon',
    'day_evening': 'Evening', 'rite': 'Rite',
}


def find_musescore():
    for path in (os.environ.get('MUSESCORE'), 'C:/Program Files/MuseScore 4/bin/MuseScore4.exe',
                 shutil.which('mscore'), shutil.which('MuseScore4')):
        if path and os.path.exists(path):
            return path
    return None


def export_score(musescore, mscz):
    """用 MuseScore 4 打开生成的曲谱并导出 PDF 与 MusicXML；打不开即报错。"""
    base = os.path.splitext(mscz)[0]
    for ext in ('.pdf', '.musicxml'):
        out = base + ext
        if os.path.exists(out):
            os.remove(out)
        subprocess.run([musescore, '-o', out, mscz], check=True, capture_output=True, timeout=300)
        if not os.path.exists(out) or os.path.getsize(out) == 0:
            raise RuntimeError(f'MuseScore 未能导出 {out}')


SOUNDFONT = os.environ.get('SOUNDFONT', 'C:/Program Files/MuseScore 4/sound/MS Basic.sf3')
# 音色 -> (库, 预设, 电平修正 dB)；电平修正使各采样在同一力度下大致等响
SAMPLED = {
    'piano': (0, 0, 0.0), 'organ': (0, 20, -2.0), 'celesta': (0, 8, 0.0), 'bell': (0, 14, -1.0),
    'musicbox': (0, 10, -2.0), 'clarinet': (0, 71, -4.0), 'strings': (0, 49, 1.0), 'pizz': (0, 45, 9.0),
    'flute': (0, 73, -5.0), 'fiddle': (0, 110, -3.0), 'dulcimer': (0, 15, 4.0), 'shamisen': (0, 106, 4.0),
    'koto': (0, 107, -2.0), 'woodblock': (0, 115, -3.0), 'shakuhachi': (0, 77, -6.0), 'sitar': (0, 104, -2.0),
    'ocarina': (0, 79, -9.0), 'taiko': (0, 116, 0.0),
}
# 合成音色（音色库里没有的乐器）的电平修正
SYNTH_TRIM = {'tanpura': -8.0, 'tabla': -12.0, 'framedrum': -11.0, 'bianzhong': -10.0, 'bianqing': -11.0}
# 混响发送：越“远”的乐器越湿（钟在塔上，钢片琴与八音盒在空间里回荡，拨弦最干）
SEND = {'bell': 0.6, 'celesta': 0.45, 'musicbox': 0.45, 'organ': 0.38, 'piano': 0.3,
        'strings': 0.34, 'clarinet': 0.26, 'pizz': 0.2, 'bianzhong': 0.55, 'bianqing': 0.5, 'taiko': 0.4,
        'flute': 0.28, 'fiddle': 0.26, 'dulcimer': 0.25, 'shamisen': 0.2, 'koto': 0.3, 'woodblock': 0.18,
        'shakuhachi': 0.32, 'sitar': 0.25, 'ocarina': 0.35, 'tanpura': 0.3, 'tabla': 0.15, 'framedrum': 0.18}
_sf = None


def soundfont():
    global _sf
    if _sf is None and os.path.exists(SOUNDFONT):
        import sf2
        _sf = sf2.SoundFont(SOUNDFONT)
    return _sf


def render(piece):
    """渲染为立体声：演奏诠释层（interpret.py）给出时值与力度，采样器发声（无音色库时退回 synth.py）。
    循环曲渲三遍取中间一遍：上一遍的余音与混响接进开头，下一遍提前的起音也留在结尾，接缝处没有断点。"""
    rng = np.random.default_rng(zlib.crc32(piece.slug.encode()))
    events, total = interpret.perform(piece, interpret.Style(**(piece.style or {})), rng)
    staff_parts = piece.staff_parts()
    font = soundfont()
    regions = {}
    passes = 3 if piece.loop else 1
    tail = piece.reverb * 1.5 + 8.0
    n = int((total * passes + tail) * synth.SR)
    dry = np.zeros((2, n), np.float32)
    send = np.zeros((2, n), np.float32)
    cache = {}
    for ev in events:
        part, _ = staff_parts[ev.staff]
        voice = part.voice or part.key
        key = (voice, ev.pitch, ev.velocity, round(ev.length, 2))
        if key not in cache:
            if font is not None and voice in SAMPLED:
                bank, program, trim = SAMPLED[voice]
                if voice not in regions:
                    regions[voice] = font.regions(bank, program)
                audio = font.note(regions[voice], ev.pitch, ev.velocity, ev.length) * 10 ** (trim / 20)
            else:
                fn = synth.VOICES[voice]
                mono = (fn(ev.pitch, ev.velocity, ev.length, rng, pedal=0.0) if voice == 'piano'
                        else fn(ev.pitch, ev.velocity, ev.length, rng))
                audio = np.stack([mono, mono]).astype(np.float32) * 0.7 * 10 ** (SYNTH_TRIM.get(voice, 0.0) / 20)
            cache[key] = audio
        audio = cache[key]
        pan = part.pan + ((ev.pitch - 60) / 48 * 0.25 if voice == 'piano' else 0.0)
        gl, gr = synth.pan_gains(float(np.clip(pan, -1, 1)))
        gain = part.gain * 10 ** (piece.mix.get(ev.staff, 0.0) / 20)
        left = audio[0] * gain * gl * 1.4142
        right = audio[1] * gain * gr * 1.4142
        amount = SEND.get(voice, 0.3)
        for p in range(passes):
            i = int(round((ev.start + p * total) * synth.SR))
            a, b = max(0, i), min(n, i + audio.shape[1])
            if b <= a:
                continue
            dry[0, a:b] += left[a - i:b - i]
            dry[1, a:b] += right[a - i:b - i]
            send[0, a:b] += left[a - i:b - i] * amount
            send[1, a:b] += right[a - i:b - i] * amount
    wet = synth.reverb(send.astype(np.float64), piece.reverb, 1.0)
    mix = wet * (piece.wet / 0.3)
    mix[:, :n] += dry * (1 - piece.wet * 0.5)
    if piece.loop:
        start = int(round(total * synth.SR))
        out = mix[:, start: start + int(round(total * synth.SR))]
    else:
        out = mix[:, : int((total + tail) * synth.SR)]
    return synth.master(synth.compress(out, ratio=piece.squeeze)), total


def write_ogg(path, data, channels_first=True):
    import soundfile as sf
    arr = np.ascontiguousarray((data.T if channels_first and data.ndim == 2 else data).astype(np.float32))
    channels = 1 if arr.ndim == 1 else arr.shape[1]
    # libsndfile 的 Vorbis 编码一次写入过长会在 Windows 上栈溢出，分块写入
    with sf.SoundFile(path, 'w', synth.SR, channels, format='OGG', subtype='VORBIS',
                      compression_level=0.55) as f:
        for i in range(0, len(arr), 4096):
            f.write(arr[i:i + 4096])


def write_wav(path, mono):
    pcm = (np.clip(mono, -1, 1) * 32767).astype('<i2')
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(synth.SR)
        w.writeframes(pcm.tobytes())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--only', default='')
    ap.add_argument('--scores', action='store_true')
    args = ap.parse_args()
    for d in (MUSIC_OUT, SFX_OUT, VOICE_OUT):
        os.makedirs(d, exist_ok=True)
    musescore = find_musescore()
    if not musescore:
        print('未找到 MuseScore 4：跳过 PDF / MusicXML 导出')

    for make in cues.CUES:
        piece = make()
        if args.only and args.only != piece.slug:
            continue
        base = os.path.join(HERE, f'Blessed Sleepers - {SCORE_NAMES[piece.slug]}')
        write_mscz(piece, base + '.mscz')
        write_midi(piece, base + '.mid')
        if musescore:
            export_score(musescore, base + '.mscz')
        if args.scores:
            print(f'{piece.slug}: {len(piece.bars)} 小节（只写曲谱）')
            continue
        audio, total = render(piece)
        write_ogg(os.path.join(MUSIC_OUT, piece.slug + '.ogg'), audio)
        peak = 20 * np.log10(np.max(np.abs(audio)) + 1e-12)
        print(f'{piece.slug}: {len(piece.bars)} 小节，{total:.1f} 秒，峰值 {peak:.1f} dBFS')
    if args.scores:
        return

    for name, fn in sfx.SFX.items():
        if args.only and args.only != name:
            continue
        x = fn(np.random.default_rng(zlib.crc32(name.encode()) ^ sfx.RNG_SEED))
        write_ogg(os.path.join(SFX_OUT, name + '.ogg'), x, channels_first=False)
        print(f'sfx {name}: {len(x) / synth.SR:.2f} 秒')
    for name, fn in sfx.VOICES.items():
        if args.only and args.only != name:
            continue
        x = sfx.finish_voice(fn(np.random.default_rng(zlib.crc32(name.encode()))))
        write_wav(os.path.join(VOICE_OUT, name + '.wav'), x)
        print(f'voice {name}: {len(x) / synth.SR:.3f} 秒')


if __name__ == '__main__':
    main()
