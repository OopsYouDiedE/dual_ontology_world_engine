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
import sfx  # noqa: E402
import synth  # noqa: E402
from score_io import performance, write_midi, write_mscz  # noqa: E402

ROOT = os.path.dirname(HERE)
ASSETS = os.path.join(ROOT, 'godot_csharp', 'ShareAsset', 'Internal')
MUSIC_OUT = os.path.join(ASSETS, 'Props', 'Audio', 'Music')
SFX_OUT = os.path.join(ASSETS, 'Props', 'Audio', 'Sfx')
VOICE_OUT = os.path.join(ASSETS, 'Characters', 'voices')
SCORE_NAMES = {
    'main_theme': 'Main Theme', 'door_side': 'The Near Side of the Door', 'confrontation': 'Confrontation',
    'day': 'Days of Sixteen', 'night': 'Sleep',
}
# 钢琴延音踏板（秒）：钟摆曲不踩踏板，其余适度
PEDAL = {'door_side': 0.12, 'confrontation': 0.2, 'day': 0.15}


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


def render(piece):
    """渲染为立体声；循环曲渲三遍取中间一遍：上一遍的余音与混响接进开头，
    下一遍提前的起音（演奏时值的微小抖动）也留在结尾，接缝处没有断点。"""
    notes, total = performance(piece)
    staff_parts = piece.staff_parts()
    rng = np.random.default_rng(zlib.crc32(piece.slug.encode()))
    passes = 3 if piece.loop else 1
    tail = piece.reverb * 1.5 + 6.0
    buf = np.zeros((2, int((total * passes + tail) * synth.SR)))
    for note in notes:
        part, _ = staff_parts[note.staff]
        voice = part.voice or part.key
        fn = synth.VOICES[voice]
        vel = int(np.clip(note.velocity + rng.integers(-3, 4), 1, 127))
        length = note.length * (2.2 if note.fermata else (0.97 if voice in ('organ', 'clarinet') else 1.0))
        if voice == 'piano':
            sample = fn(note.pitch, vel, length, rng, pedal=PEDAL.get(piece.slug, 0.45))
        else:
            sample = fn(note.pitch, vel, length, rng)
        pan = part.pan
        if voice == 'piano':
            pan = float(np.clip(pan + (note.pitch - 60) / 48 * 0.35, -1, 1))
        gl, gr = synth.pan_gains(pan)
        jitter = rng.normal(0, 0.004) if voice != 'bell' else 0.0
        for p in range(passes):
            i = max(0, int((note.start + p * total + jitter) * synth.SR))
            end = min(buf.shape[1], i + len(sample))
            seg = sample[: end - i] * part.gain * 10 ** (piece.mix.get(note.staff, 0.0) / 20)
            buf[0, i:end] += seg * gl
            buf[1, i:end] += seg * gr
    wet = synth.reverb(buf, piece.reverb, piece.wet)
    if piece.loop:
        start = int(total * synth.SR)
        out = wet[:, start: start + int(total * synth.SR)]
    else:
        out = wet[:, : int((total + tail) * synth.SR)]
    return synth.master(out), total


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
