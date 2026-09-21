import numpy as np
import scipy.io.wavfile as wavfile
import scipy.signal as signal
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import json, xml.etree.ElementTree as ET, zipfile, os

# 1. Load Audio
sr, duet_audio = wavfile.read('scratch/duet.wav')
if duet_audio.dtype != np.float32:
    duet_audio = duet_audio.astype(np.float32) / 32768.0
if len(duet_audio.shape) > 1:
    duet_audio = duet_audio.mean(axis=1)

total_duration = len(duet_audio) / sr
print(f"Duet audio sample rate: {sr}, length: {len(duet_audio)} samples, duration: {total_duration:.3f} s")

# Section definitions (Measure ranges, seconds at 180 BPM, 4/3 s per bar)
sections = [
    ("M1-12", "日常问答 (Theme A)", 1, 12, 0.0, 16.0),
    ("M13-28", "恶魔授业 (Motif B1)", 13, 28, 16.0, 37.333),
    ("M29-48", "调式镜像 (Motif B2)", 29, 48, 37.333, 64.0),
    ("M49-64", "地下密室 (Dies irae/Dorian)", 49, 64, 64.0, 85.333),
    ("M65-68", "深层法则 (Danny Boy弧线)", 65, 68, 85.333, 90.667),
    ("M69-84", "机件/危机 (Ostinato/压缩)", 69, 84, 90.667, 112.0),
    ("M85-88", "属准备与全休止 (Pedal/Pause)", 85, 88, 112.0, 117.333),
    ("M89-106", "终极爆发 (全主题召回)", 89, 106, 117.333, 141.333),
    ("M107-110", "和解尾声 (摇篮曲抚平)", 107, 110, 141.333, 146.667),
]

# 2. Compute Spectrogram
nperseg = 2048
noverlap = 1024
frequencies, times, Sxx = signal.spectrogram(duet_audio, fs=sr, nperseg=nperseg, noverlap=noverlap)
Sxx_db = 10 * np.log10(np.maximum(Sxx, 1e-10))

# 3. Compute RMS Loudness (dB)
frame_len = int(sr * 0.1) # 100ms
hop_len = int(sr * 0.05)  # 50ms
rms_times = []
rms_db = []
for start in range(0, len(duet_audio) - frame_len, hop_len):
    segment = duet_audio[start:start + frame_len]
    rms = np.sqrt(np.mean(segment**2))
    rms_times.append(start / sr)
    rms_db.append(20 * np.log10(max(rms, 1e-5)))

rms_times = np.array(rms_times)
rms_db = np.array(rms_db)

# 4. Energy band distribution per section
print("\n=== 各乐章频谱能量分布与响度均值 ===")
stats = []
for label, name, m_s, m_e, t_s, t_e in sections:
    # Filter times
    t_mask = (times >= t_s) & (times < t_e)
    if not np.any(t_mask): continue
    sec_sxx = Sxx[:, t_mask]
    
    # Frequency bands
    low_mask = (frequencies >= 20) & (frequencies < 250)
    low_mid_mask = (frequencies >= 250) & (frequencies < 1000)
    high_mid_mask = (frequencies >= 1000) & (frequencies < 4000)
    high_mask = (frequencies >= 4000) & (frequencies < 10000)
    
    tot_energy = np.sum(sec_sxx)
    low_e = np.sum(sec_sxx[low_mask, :]) / tot_energy * 100
    lmid_e = np.sum(sec_sxx[low_mid_mask, :]) / tot_energy * 100
    hmid_e = np.sum(sec_sxx[high_mid_mask, :]) / tot_energy * 100
    high_e = np.sum(sec_sxx[high_mask, :]) / tot_energy * 100
    
    # RMS loudness
    r_mask = (rms_times >= t_s) & (rms_times < t_e)
    avg_loudness = np.mean(rms_db[r_mask]) if np.any(r_mask) else -60
    max_loudness = np.max(rms_db[r_mask]) if np.any(r_mask) else -60
    
    stat_entry = {
        "section": label,
        "name": name,
        "t_range": f"{t_s:.1f}s - {t_e:.1f}s",
        "low_pct": float(round(low_e, 1)),
        "lmid_pct": float(round(lmid_e, 1)),
        "hmid_pct": float(round(hmid_e, 1)),
        "high_pct": float(round(high_e, 1)),
        "avg_loudness_db": float(round(avg_loudness, 1)),
        "max_loudness_db": float(round(max_loudness, 1)),
    }
    stats.append(stat_entry)
    print(f"{label:8s} | {name:20s} | 低频: {low_e:4.1f}% | 中低频: {lmid_e:4.1f}% | 中高频: {hmid_e:4.1f}% | 高频: {high_e:4.1f}% | 均响度: {avg_loudness:5.1f} dB | 峰值: {max_loudness:5.1f} dB")

# 5. Extract Notes from Duet MSCZ
notes_data = []
with zipfile.ZipFile('music/The Unfallen Duet.mscz') as z:
    mscx_name = [n for n in z.namelist() if n.endswith('.mscx')][0]
    d_tree = ET.fromstring(z.read(mscx_name))
score = d_tree.find('Score') if d_tree.tag != 'Score' else d_tree
staves = score.findall('Staff')
dur_map = {'whole': 4, 'half': 2, 'quarter': 1, 'eighth': 0.5, '16th': 0.25, '32nd': 0.125}

for s_idx, s in enumerate(staves):
    s_name = "Violin" if s_idx == 0 else ("Piano Treble" if s_idx == 1 else "Piano Bass")
    for m_idx, m in enumerate(s.findall('Measure'), 1):
        m_start_time = (m_idx - 1) * (4.0 / 3.0)
        # Check voices
        for v in m.findall('voice'):
            curr_beat = 0.0
            for el in v:
                d_type = el.findtext('durationType')
                dots = int(el.findtext('dots', '0'))
                dur_beats = dur_map.get(d_type, 0)
                if dots == 1: dur_beats *= 1.5
                elif dots == 2: dur_beats *= 1.75
                
                if el.tag == 'Chord':
                    t_start = m_start_time + curr_beat * (1.0 / 3.0)
                    t_dur = dur_beats * (1.0 / 3.0)
                    for n in el.findall('Note'):
                        pitch = int(n.findtext('pitch'))
                        notes_data.append({
                            'staff': s_name,
                            'measure': m_idx,
                            'start': t_start,
                            'end': t_start + t_dur,
                            'pitch': pitch
                        })
                curr_beat += dur_beats

print(f"Total extracted notes: {len(notes_data)}")

# 6. Plot 1: Spectrogram & RMS Dynamics
fig, (ax1, ax2) = plt.subplots(2, 1, figsize=(14, 8), sharex=True, gridspec_kw={'height_ratios': [2.5, 1]})
plt.subplots_adjust(hspace=0.15)

# Spectrogram
freq_mask = frequencies <= 6000
im = ax1.pcolormesh(times, frequencies[freq_mask], Sxx_db[freq_mask, :], cmap='inferno', shading='gouraud', vmin=-70, vmax=0)
ax1.set_ylabel('Frequency (Hz)', fontsize=11)
ax1.set_title('《The Unfallen Duet》 Audio Spectrogram (STFT)', fontsize=13, fontweight='bold')
ax1.set_ylim(0, 5500)
cbar = fig.colorbar(im, ax=ax1, pad=0.01)
cbar.set_label('Magnitude (dB)')

# RMS Curve
ax2.plot(rms_times, rms_db, color='#0ea5e9', linewidth=1.5, label='RMS Loudness (dB)')
ax2.set_ylabel('Loudness (dB)', fontsize=11)
ax2.set_xlabel('Time (seconds)', fontsize=11)
ax2.set_ylim(-50, 0)
ax2.grid(True, linestyle='--', alpha=0.5)

# Add section divider lines
for label, name, m_s, m_e, t_s, t_e in sections:
    ax1.axvline(t_s, color='white', linestyle='--', alpha=0.6, linewidth=1)
    ax2.axvline(t_s, color='#64748b', linestyle='--', alpha=0.6, linewidth=1)
    # Text label on spectrogram
    ax1.text(t_s + 1.0, 5100, label, color='white', fontsize=9, fontweight='bold',
             bbox=dict(boxstyle='round,pad=0.2', facecolor='black', alpha=0.6))

# Highlight M88 General Pause
ax1.axvline(117.0, color='#f43f5e', linestyle='-', alpha=0.8, linewidth=1.5)
ax1.text(117.2, 3800, 'M88 Cut (0.33s Pause)', color='#f43f5e', fontsize=9, fontweight='bold')

plt.savefig('artifacts/theme-film/duet_spectrogram.png', dpi=200, bbox_inches='tight')
plt.close()
print("Saved duet_spectrogram.png")

# 7. Plot 2: Piano Roll Note Visualization
fig, ax = plt.subplots(figsize=(14, 6))
colors = {'Violin': '#ef4444', 'Piano Treble': '#3b82f6', 'Piano Bass': '#10b981'}
alphas = {'Violin': 0.85, 'Piano Treble': 0.65, 'Piano Bass': 0.75}
heights = {'Violin': 0.7, 'Piano Treble': 0.6, 'Piano Bass': 0.8}

for n in notes_data:
    st = n['staff']
    ax.barh(n['pitch'], n['end'] - n['start'], left=n['start'], height=heights[st],
            color=colors[st], alpha=alphas[st], edgecolor='none')

ax.set_xlabel('Time (seconds)', fontsize=11)
ax.set_ylabel('MIDI Pitch', fontsize=11)
ax.set_title('《The Unfallen Duet》 Note Roll Visualization (Multi-Track)', fontsize=13, fontweight='bold')
ax.set_ylim(20, 96)
ax.grid(True, linestyle=':', alpha=0.4)

# Legend
from matplotlib.patches import Patch
legend_elements = [
    Patch(facecolor='#ef4444', label='Violin (Staff 1)'),
    Patch(facecolor='#3b82f6', label='Piano Treble (Staff 2)'),
    Patch(facecolor='#10b981', label='Piano Bass (Staff 3)'),
]
ax.legend(handles=legend_elements, loc='upper left', framealpha=0.9)

for label, name, m_s, m_e, t_s, t_e in sections:
    ax.axvline(t_s, color='#94a3b8', linestyle='--', alpha=0.5, linewidth=1)
    ax.text(t_s + 0.8, 92, label, color='#334155', fontsize=8, fontweight='bold')

plt.savefig('artifacts/theme-film/duet_pianoroll.png', dpi=200, bbox_inches='tight')
plt.close()
print("Saved duet_pianoroll.png")

# Save stats json
with open('artifacts/theme-film/audio-analysis-stats.json', 'w', encoding='utf-8') as f:
    json.dump(stats, f, ensure_ascii=False, indent=2)
print("Saved audio-analysis-stats.json")
