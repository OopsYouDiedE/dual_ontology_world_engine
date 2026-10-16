"""《安眠者有福》原声编译管线（MuseScore 4 直接导出 + 音频后处理版）。

    python music/build_soundtrack.py                  # 编译曲谱，由 MuseScore 4 导出并后处理生成游戏配乐、音效与提示音
    python music/build_soundtrack.py --only night     # 只编译某一首曲目（或某个音效 id）
    python music/build_soundtrack.py --scores         # 只核对/更新曲谱，不渲染游戏 OGG
    python music/build_soundtrack.py --export         # 另用 MuseScore 4 导出 PDF / MusicXML / MIDI / MP3
    python music/build_soundtrack.py --rewrite-scores # 从 cues.py 更新 .mscz（严格保留原谱乐器名称与音色设置）并导出全部产物
    python music/build_soundtrack.py --from-mscz      # 直接以现有 .mscz 为唯一源，经 MuseScore 4 编译进游戏（跳过 cues.py 比对）

编译流程说明：
1. 乐器名称保护：更新或导出 `.mscz` 时，严格保留 `.mscz` 中既有的 `<Part>` 乐器名称、简称、通道、移调与 `audiosettings.json` 音色设置。
2. MuseScore 4 原生渲染：直接调用 `MuseScore4.exe` 将 `.mscz` 渲染为无损立体声波形，并导出 `.pdf`、`.musicxml`、`.mid`、`.mp3`。
3. 游戏音频后处理（切空白、尾音回叠无缝循环、动态收拢与响度统一）：
   - 自动检测并裁除首部空白前导；
   - 按曲谱精确小节/速度时值计算循环点 `t_score`：对循环配乐，将末尾余音与混响尾段（Tail）平滑包络回叠至循环开头，
     并在接缝处施加微交叉淡化，实现零静音空隙、零爆音的无缝循环；对含延长记号收束的主题曲，自动裁除尾部静音段并平滑淡出；
   - 经总线动态压缩与响度对齐（目标 RMS -19.0 dBFS，峰值上限 -1.2 dBFS）编码写入游戏 OGG。
"""
import argparse
import concurrent.futures
import os
import shutil
import subprocess
import sys
import tempfile
import wave
import zlib

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import cues  # noqa: E402
import interpret  # noqa: E402
import sfx  # noqa: E402
import synth  # noqa: E402
from score_io import compare_with_score, mscz_musical_duration, write_midi, write_mscz  # noqa: E402

ROOT = os.path.dirname(HERE)
ASSETS = os.path.join(ROOT, 'godot_csharp', 'ShareAsset', 'Internal')
MUSIC_OUT = os.path.join(ASSETS, 'Props', 'Audio', 'Music')
SFX_OUT = os.path.join(ASSETS, 'Props', 'Audio', 'Sfx')
VOICE_OUT = os.path.join(ASSETS, 'Characters', 'voices')
SCORE_NAMES = {
    'main_theme': 'Main Theme',
    'door_side': 'The Near Side of the Door',
    'confrontation': 'Confrontation',
    'day': 'Days of Sixteen',
    'night': 'Sleep',
    'day_noon': 'Noon',
    'day_afternoon': 'Afternoon',
    'day_evening': 'Evening',
    'rite': 'Rite',
}


def find_musescore():
    for path in (
        os.environ.get('MUSESCORE'),
        'C:/Program Files/MuseScore 4/bin/MuseScore4.exe',
        shutil.which('mscore'),
        shutil.which('MuseScore4'),
    ):
        if path and os.path.exists(path):
            return path
    return None


def _run_musescore_export(musescore, mscz, out_path, extra=()):
    if os.path.exists(out_path):
        os.remove(out_path)
    subprocess.run([musescore, *extra, '-o', out_path, mscz], check=True, capture_output=True, timeout=300)
    if not os.path.exists(out_path) or os.path.getsize(out_path) == 0:
        raise RuntimeError(f'MuseScore 4 未能导出 {out_path}')


def export_score(musescore, mscz):
    """用 MuseScore 4 打开曲谱并导出 PDF、MusicXML、MIDI 与 MP3。"""
    base = os.path.splitext(mscz)[0]
    tasks = [
        (base + '.pdf', ()),
        (base + '.musicxml', ()),
        (base + '.mid', ()),
        (base + '.mp3', ('-b', '320')),
    ]
    for out_path, extra in tasks:
        _run_musescore_export(musescore, mscz, out_path, extra)


def postprocess_musescore_audio(stereo_lr, sr, mscz_path, piece):
    """对 MuseScore 4 导出的立体声波形（shape=(2, N)）执行切空白、余音回叠无缝循环、总线压缩与响度对齐。"""
    t_score, has_fermata_last = mscz_musical_duration(mscz_path)

    # 1) 检测并裁除首部编码/渲染前导静音（上限 50ms，保留乐谱正常的休止拍时值）
    peak_env = np.max(np.abs(stereo_lr), axis=0)
    nz = np.nonzero(peak_env > 1e-4)[0]
    max_preroll = int(0.035 * sr)
    if len(nz) > 0 and 0 < nz[0] <= max_preroll:
        stereo_lr = stereo_lr[:, nz[0]:]

    loop_samples = int(round(t_score * sr))

    # 2) 循环与尾部空白处理
    if piece.loop and not has_fermata_last and stereo_lr.shape[1] >= loop_samples:
        body = stereo_lr[:, :loop_samples].copy()
        tail = stereo_lr[:, loop_samples:].copy()

        # 裁掉尾音末端低于 -72 dBFS 的纯静音段
        tail_mag = np.max(np.abs(tail), axis=0)
        active_tail = np.nonzero(tail_mag > 2.5e-4)[0]
        if len(active_tail) > 0:
            tail = tail[:, : min(tail.shape[1], active_tail[-1] + int(0.05 * sr))]
            tail_len = min(tail.shape[1], body.shape[1] // 2)
            if tail_len > 64:
                # 将最末小节的自然混响与释放余音平滑衰减并回叠进第一小节开头，消除循环接缝断音
                fade_curve = np.maximum(0.0, np.cos(np.linspace(0.0, np.pi / 2, tail_len, dtype=np.float32))) ** 1.5
                body[:, :tail_len] += tail[:, :tail_len] * fade_curve[None, :]

        # 在循环边界施加 15ms 微平滑过渡，确保首尾采样连续无爆音
        xfade = min(int(0.015 * sr), body.shape[1] // 8)
        if xfade > 8:
            w = np.linspace(0.0, 1.0, xfade, dtype=np.float32)[None, :]
            seam_start = body[:, :xfade].copy()
            seam_end = body[:, -xfade:].copy()
            body[:, :xfade] = seam_start * w + seam_end * (1.0 - w) * 0.15
            body[:, -xfade:] = seam_end * (1.0 - w * 0.5) + seam_start * (w * 0.5)
        out = body
        final_dur = loop_samples / sr
    else:
        # 含延长记号收束（如主题曲尾声）：保留延长拍与自然混响衰减，裁除末尾多余空白静音
        mag = np.max(np.abs(stereo_lr), axis=0)
        peak = float(np.max(mag) + 1e-12)
        floor = peak * (10.0 ** (-58.0 / 20.0))
        active = np.nonzero(mag > floor)[0]
        min_keep = min(stereo_lr.shape[1], int((t_score + 1.2) * sr))
        end_idx = max(min_keep, int(active[-1] + 0.25 * sr)) if len(active) else stereo_lr.shape[1]
        end_idx = min(stereo_lr.shape[1], end_idx)
        out = stereo_lr[:, :end_idx].copy()

        # 尾部柔和淡出与首部 8ms 微淡入，防止任何截断伪影
        fade_out = min(int(0.45 * sr), out.shape[1] // 6)
        if fade_out > 16:
            out[:, -fade_out:] *= (np.maximum(0.0, np.cos(np.linspace(0.0, np.pi / 2, fade_out, dtype=np.float32))) ** 2)[None, :]
        fade_in = min(int(0.008 * sr), out.shape[1] // 16)
        if fade_in > 4:
            out[:, :fade_in] *= np.linspace(0.0, 1.0, fade_in, dtype=np.float32)[None, :]
        final_dur = out.shape[1] / sr

    # 3) 总线动态收拢与响度统一（-19.0 dBFS RMS，-1.2 dBFS 峰值上限）
    processed = synth.master(synth.compress(out.astype(np.float64), ratio=piece.squeeze)).astype(np.float32)
    return processed, final_dur


def render_via_musescore(musescore, mscz_path, piece):
    """直接调用 MuseScore 4 将 .mscz 导出为无损音频并执行游戏级后处理。"""
    import soundfile as sf

    with tempfile.NamedTemporaryFile(suffix='.wav', delete=False) as tmp:
        tmp_wav = tmp.name
    try:
        _run_musescore_export(musescore, mscz_path, tmp_wav)
        data, sr = sf.read(tmp_wav, dtype='float32', always_2d=True)
    finally:
        if os.path.exists(tmp_wav):
            os.remove(tmp_wav)

    stereo_lr = np.ascontiguousarray(data.T)
    return postprocess_musescore_audio(stereo_lr, sr, mscz_path, piece)


# ---------------------------------------------------------------- 无 MuseScore 4 时的后备采样/合成器渲染
SOUNDFONT = os.environ.get('SOUNDFONT', 'C:/Program Files/MuseScore 4/sound/MS Basic.sf3')
SAMPLED = {
    'piano': (0, 0, 0.0), 'organ': (0, 20, -2.0), 'celesta': (0, 8, 0.0), 'bell': (0, 14, -1.0),
    'musicbox': (0, 10, -2.0), 'clarinet': (0, 71, -4.0), 'strings': (0, 48, -2.5), 'pizz': (0, 45, 9.0),
    'flute': (0, 73, -5.0), 'fiddle': (0, 110, -3.0), 'dulcimer': (0, 15, 4.0), 'shamisen': (0, 106, 4.0),
    'koto': (0, 107, -2.0), 'woodblock': (128, 48, -3.0), 'shakuhachi': (0, 77, -6.0), 'sitar': (0, 104, -2.0),
    'ocarina': (0, 79, -9.0), 'taiko': (0, 116, 0.0),
    'violin': (0, 40, -3.0), 'xylophone': (0, 13, -11.0),
    'framedrum': (128, 0, -11.0), 'tabla': (0, 0, -12.0), 'bassdrum': (128, 48, 0.0),
    'cellos': (41, 48, -4.0), 'basses': (51, 48, 2.0), 'cellos_pizz': (40, 45, 9.0), 'basses_pizz': (50, 45, 7.0),
    'templeblocks': (1, 115, -3.0), 'dagu': (128, 0, -4.0),
}
SYNTH_TRIM = {'tanpura': -8.0, 'tabla': -12.0, 'framedrum': -11.0, 'bianzhong': -10.0, 'bianqing': -11.0}
SEND = {
    'bell': 0.6, 'celesta': 0.45, 'musicbox': 0.45, 'organ': 0.38, 'piano': 0.3,
    'strings': 0.34, 'clarinet': 0.26, 'pizz': 0.2, 'bianzhong': 0.55, 'bianqing': 0.5, 'taiko': 0.4,
    'flute': 0.28, 'fiddle': 0.26, 'dulcimer': 0.25, 'shamisen': 0.2, 'koto': 0.3, 'woodblock': 0.18,
    'violin': 0.26, 'xylophone': 0.5, 'bassdrum': 0.4, 'cellos': 0.34, 'basses': 0.34, 'cellos_pizz': 0.2,
    'basses_pizz': 0.2, 'templeblocks': 0.18, 'dagu': 0.4,
    'shakuhachi': 0.32, 'sitar': 0.25, 'ocarina': 0.35, 'tanpura': 0.3, 'tabla': 0.15, 'framedrum': 0.18,
}
_sf = None
_custom_fonts = {}


def soundfont():
    global _sf
    if _sf is None and os.path.exists(SOUNDFONT):
        import sf2
        _sf = sf2.SoundFont(SOUNDFONT)
    return _sf


def render_fallback(piece):
    """当系统未安装 MuseScore 4 时的后备渲染器。"""
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
        note_font = font
        if part.soundfont:
            if part.soundfont not in _custom_fonts:
                import sf2
                _custom_fonts[part.soundfont] = sf2.SoundFont(os.path.join(HERE, part.soundfont))
            note_font = _custom_fonts[part.soundfont]
        voice = part.voice or part.key
        pitch = part.playback_pitch(ev.pitch)
        preset = (part.bank, part.program)
        key = (part.soundfont, voice, *preset, pitch, ev.velocity, round(ev.length, 2))
        if key not in cache:
            if note_font is not None and voice in SAMPLED:
                trim = SAMPLED[voice][2]
                region_key = (part.soundfont, *preset)
                if region_key not in regions:
                    regions[region_key] = note_font.regions(*preset)
                audio = note_font.note(regions[region_key], pitch, ev.velocity, ev.length) * 10 ** (trim / 20)
            else:
                fn = synth.VOICES[voice]
                mono = (fn(pitch, ev.velocity, ev.length, rng, pedal=0.0) if voice == 'piano'
                        else fn(pitch, ev.velocity, ev.length, rng))
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


def _compile_cue(make, args, musescore):
    piece = make()
    base = os.path.join(HERE, f'Blessed Sleepers - {SCORE_NAMES[piece.slug]}')
    mscz_path = base + '.mscz'

    rewrite = args.rewrite_scores or not os.path.exists(mscz_path)
    if rewrite:
        write_mscz(piece, mscz_path)
    elif not args.from_mscz:
        diffs = compare_with_score(piece, mscz_path)
        if diffs:
            msg = [f'{piece.slug}：曲谱与 cues.py 不一致（共 {len(diffs)} 处），请传 --rewrite-scores 或 --from-mscz：']
            msg.extend(f'    {d}' for d in diffs[:10])
            raise RuntimeError('\n'.join(msg))

    if rewrite and not musescore:
        write_midi(piece, base + '.mid')
    if musescore and (rewrite or args.export):
        export_score(musescore, mscz_path)
    if args.scores:
        return f'{piece.slug}: {len(piece.bars)} 小节（曲谱已就绪）'

    if musescore:
        audio, total = render_via_musescore(musescore, mscz_path, piece)
        engine_tag = 'MuseScore 4'
    else:
        audio, total = render_fallback(piece)
        engine_tag = 'fallback-synth'

    write_ogg(os.path.join(MUSIC_OUT, piece.slug + '.ogg'), audio)
    peak = 20 * np.log10(np.max(np.abs(audio)) + 1e-12)
    rms = 20 * np.log10(np.sqrt(np.mean(audio ** 2)) + 1e-12)
    return f'{piece.slug} [{engine_tag}]: {len(piece.bars)} 小节，{total:.2f} 秒，RMS {rms:.1f} dBFS，峰值 {peak:.1f} dBFS'


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--only', default='')
    ap.add_argument('--scores', action='store_true')
    ap.add_argument('--rewrite-scores', action='store_true',
                    help='从 cues.py 更新 .mscz（保留原谱乐器名称与音色设置）并导出')
    ap.add_argument('--from-mscz', action='store_true',
                    help='直接将现有 .mscz 经 MuseScore 4 编译导出为游戏音频（不核对 cues.py）')
    ap.add_argument('--export', action='store_true',
                    help='重新用 MuseScore 4 导出 PDF / MusicXML / MIDI / MP3')
    ap.add_argument('--jobs', type=int, default=4,
                    help='并行调用 MuseScore 4 的工作线程数（默认 4）')
    args = ap.parse_args()

    for d in (MUSIC_OUT, SFX_OUT, VOICE_OUT):
        os.makedirs(d, exist_ok=True)
    musescore = find_musescore()
    if not musescore:
        print('未找到 MuseScore 4：退回内置合成器渲染，跳过 PDF / MusicXML 导出')

    selected_cues = [make for make in cues.CUES if not args.only or args.only == make().slug]
    if selected_cues:
        workers = max(1, min(args.jobs, len(selected_cues)))
        if workers > 1 and musescore:
            with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as ex:
                futures = [ex.submit(_compile_cue, make, args, musescore) for make in selected_cues]
                for fut in futures:
                    print(fut.result())
        else:
            for make in selected_cues:
                print(_compile_cue(make, args, musescore))

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
