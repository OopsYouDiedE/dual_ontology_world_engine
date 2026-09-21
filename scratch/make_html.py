import base64, os

art_dir = r'C:\Users\iii\.gemini\antigravity\brain\97d867f3-6cb8-43be-97f3-e281eb8522a0'
with open('scratch/duet_preview.mp3', 'rb') as f:
    audio_b64 = base64.b64encode(f.read()).decode('ascii')

with open('artifacts/theme-film/duet_spectrogram.png', 'rb') as f:
    spec_b64 = base64.b64encode(f.read()).decode('ascii')

with open('artifacts/theme-film/duet_pianoroll.png', 'rb') as f:
    roll_b64 = base64.b64encode(f.read()).decode('ascii')

html_content = f'''<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8">
  <title>The Unfallen 音频频谱与音符可视化分析</title>
  <script src="https://www.gstatic.com/antigravity/web/dev/tailwindcss.min.js"></script>
</head>
<body class="bg-[var(--background)] text-[var(--foreground)] antialiased p-6">
  <div class="max-w-5xl mx-auto space-y-6">
    
    <!-- Header -->
    <div class="bg-[var(--card)] border border-[var(--border)] rounded-xl p-6 shadow-sm">
      <div class="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-[var(--foreground)]">《The Unfallen Duet》声学频谱与音符可视化分析</h1>
          <p class="text-sm text-[var(--muted-foreground)] mt-1">基于 MuseScore 渲染音频与短时傅里叶变换（STFT）解析 · 全曲 110 小节 / 180 BPM</p>
        </div>
        <div class="flex items-center gap-2 bg-[var(--background)] px-3 py-1.5 rounded-lg border border-[var(--border)] text-xs font-mono text-[var(--foreground)]">
          <span>采样率: 22.05 kHz</span> · <span>总时长: 146.7 s</span>
        </div>
      </div>
      
      <!-- Audio Player Control -->
      <div class="mt-6 p-4 rounded-lg bg-[var(--background)] border border-[var(--border)]">
        <div class="flex items-center justify-between mb-2">
          <span class="text-xs font-semibold uppercase tracking-wider text-[var(--muted-foreground)]">全曲即时试听与对位快跳</span>
          <span id="playback-time" class="text-xs font-mono text-[var(--primary)]">00:00 / 02:26</span>
        </div>
        <audio id="duet-audio" controls class="w-full h-10">
          <source src="data:audio/mp3;base64,{audio_b64}" type="audio/mp3">
        </audio>
        
        <!-- Jump to sections -->
        <div class="mt-3 flex flex-wrap gap-1.5">
          <span class="text-xs text-[var(--muted-foreground)] self-center mr-1">小节定位:</span>
          <button onclick="seekAudio(0)" class="px-2 py-1 text-xs rounded bg-[var(--card)] hover:bg-[var(--primary)] hover:text-white border border-[var(--border)] transition">M1 日常问答 (0:00)</button>
          <button onclick="seekAudio(16.0)" class="px-2 py-1 text-xs rounded bg-[var(--card)] hover:bg-[var(--primary)] hover:text-white border border-[var(--border)] transition">M13 恶魔授业 (0:16)</button>
          <button onclick="seekAudio(37.33)" class="px-2 py-1 text-xs rounded bg-[var(--card)] hover:bg-[var(--primary)] hover:text-white border border-[var(--border)] transition">M29 调式镜像 (0:37)</button>
          <button onclick="seekAudio(64.0)" class="px-2 py-1 text-xs rounded bg-[var(--card)] hover:bg-[var(--primary)] hover:text-white border border-[var(--border)] transition">M49 地下密室 (1:04)</button>
          <button onclick="seekAudio(85.33)" class="px-2 py-1 text-xs rounded bg-[var(--card)] hover:bg-[var(--primary)] hover:text-white border border-[var(--border)] transition">M65 深层法则 (1:25)</button>
          <button onclick="seekAudio(90.67)" class="px-2 py-1 text-xs rounded bg-[var(--card)] hover:bg-[var(--primary)] hover:text-white border border-[var(--border)] transition">M69 机件推进 (1:30)</button>
          <button onclick="seekAudio(112.0)" class="px-2 py-1 text-xs rounded bg-[var(--card)] hover:bg-[var(--primary)] hover:text-white border border-[var(--border)] transition">M85 属准备 (1:52)</button>
          <button onclick="seekAudio(117.33)" class="px-2 py-1 text-xs rounded bg-red-500/20 text-red-400 hover:bg-red-500 hover:text-white border border-red-500/40 transition font-bold">M88 全休止切口 (1:57)</button>
          <button onclick="seekAudio(118.0)" class="px-2 py-1 text-xs rounded bg-amber-500/20 text-amber-300 hover:bg-amber-500 hover:text-black border border-amber-500/40 transition font-bold">M89 终极爆发 (1:58)</button>
          <button onclick="seekAudio(141.33)" class="px-2 py-1 text-xs rounded bg-[var(--card)] hover:bg-[var(--primary)] hover:text-white border border-[var(--border)] transition">M107 和解尾声 (2:21)</button>
        </div>
      </div>
    </div>

    <!-- Spectrogram Card -->
    <div class="bg-[var(--card)] border border-[var(--border)] rounded-xl p-6 shadow-sm space-y-4">
      <div class="flex items-center justify-between">
        <h2 class="text-lg font-semibold text-[var(--foreground)]">1. 声学时频谱与 RMS 动态响度包络（STFT）</h2>
        <span class="text-xs text-[var(--muted-foreground)]">0–5500 Hz / -70 dB 到 0 dB</span>
      </div>
      <div class="rounded-lg overflow-hidden border border-[var(--border)] bg-black">
        <img src="data:image/png;base64,{spec_b64}" class="w-full h-auto block" alt="Duet Spectrogram">
      </div>
      <div class="grid grid-cols-1 md:grid-cols-3 gap-4 text-xs">
        <div class="p-3 rounded bg-[var(--background)] border border-[var(--border)]">
          <span class="font-bold block text-[var(--foreground)] mb-1">日常至探索 (M1–64)</span>
          <span class="text-[var(--muted-foreground)]">能量紧密集中于 250–1000 Hz 中低频段（占比高于 80%），整体均响度维持在 -31 dB 至 -26 dB，具备学院探索的严谨与克制。</span>
        </div>
        <div class="p-3 rounded bg-[var(--background)] border border-[var(--border)]">
          <span class="font-bold block text-red-400 mb-1">M88 拍尾全休止切口 (117.0s)</span>
          <span class="text-[var(--muted-foreground)]">实测呈现 0.33 秒的声学深坑（RMS 暴跌至 -45 dB 以下），声能瞬间抽空，为随后 M89 的大调主和弦爆发展开极端声学动静落差。</span>
        </div>
        <div class="p-3 rounded bg-[var(--background)] border border-[var(--border)]">
          <span class="font-bold block text-amber-400 mb-1">终章全频喷薄 (M89–106)</span>
          <span class="text-[var(--muted-foreground)]">高频泛音直冲 4500 Hz，1000–4000 Hz 中高频能量占比由 4.5% 跃升至 37.1%，瞬态峰值达 -8.8 dB，封印完成的声响张力全面释放。</span>
        </div>
      </div>
    </div>

    <!-- Note Roll Card -->
    <div class="bg-[var(--card)] border border-[var(--border)] rounded-xl p-6 shadow-sm space-y-4">
      <div class="flex items-center justify-between">
        <h2 class="text-lg font-semibold text-[var(--foreground)]">2. 多声部可视化音符卷帘（Multi-Track Note Roll）</h2>
        <span class="text-xs text-[var(--muted-foreground)]">全曲 2,163 枚乐谱实发音符</span>
      </div>
      <div class="rounded-lg overflow-hidden border border-[var(--border)] bg-white">
        <img src="data:image/png;base64,{roll_b64}" class="w-full h-auto block" alt="Note Roll">
      </div>
      <div class="grid grid-cols-1 md:grid-cols-3 gap-4 text-xs">
        <div class="p-3 rounded bg-[var(--background)] border border-[var(--border)]">
          <span class="font-bold text-red-500 block mb-1">■ 小提琴声部 (Staff 1)</span>
          <span class="text-[var(--muted-foreground)]">M1–12 留白；M13 柔音歌唱；M65–68 形成九音大弧线；M89 爆发跨八度双音领奏，并在 M97–104 对位召回 B1/B2 动机。</span>
        </div>
        <div class="p-3 rounded bg-[var(--background)] border border-[var(--border)]">
          <span class="font-bold text-blue-500 block mb-1">■ 钢琴高音部 (Staff 2)</span>
          <span class="text-[var(--muted-foreground)]">承担复调织体。M69 以八分音符机件暗涌铺底；M85–106 释放密集的十六分音符琶音巨浪，横跨数个八度。</span>
        </div>
        <div class="p-3 rounded bg-[var(--background)] border border-[var(--border)]">
          <span class="font-bold text-emerald-500 block mb-1">■ 钢琴低音部 (Staff 3)</span>
          <span class="text-[var(--muted-foreground)]">低频动力基石。M65–68 运用平滑的半音阶下行反向进行；M89–106 以连续大跨度八度强击确立封印稳固度。</span>
        </div>
      </div>
    </div>

  </div>

  <script>
    const audio = document.getElementById('duet-audio');
    const timeDisplay = document.getElementById('playback-time');
    
    function fmt(s) {{
      const m = Math.floor(s / 60);
      const sec = Math.floor(s % 60);
      return (m < 10 ? '0' + m : m) + ':' + (sec < 10 ? '0' + sec : sec);
    }}

    audio.ontimeupdate = function() {{
      timeDisplay.textContent = fmt(audio.currentTime) + ' / ' + fmt(audio.duration || 146.7);
    }};

    function seekAudio(t) {{
      audio.currentTime = t;
      audio.play();
    }}
  </script>
</body>
</html>
'''

target_html = os.path.join(art_dir, 'audio_spectrum_inspection.html')
with open(target_html, 'w', encoding='utf-8') as f:
    f.write(html_content)

print(f'Wrote {target_html}, size: {len(html_content)} chars')
