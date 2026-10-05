from pathlib import Path
import json, hashlib, sys
import numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parent))
from validate_loop_seams import decode, ROOT
out=Path(__file__).resolve().parent
m=json.loads((out/'loop-seam-metrics.json').read_text(encoding='utf-8'))
r=json.loads((out/'loop-seam-door-side-rerender.json').read_text(encoding='utf-8'))
unchanged=all(hashlib.sha256((ROOT/v[t]['path']).read_bytes()).hexdigest()==v[t]['sha256'] for v in m.values() for t in ['ogg','mp3'])
a=decode(ROOT/m['door_side']['ogg']['path']);b=decode(ROOT/'tmp/audio-seam-validation/door_side.ogg')
error=float(np.max(np.abs(a-b)))
lines=['# 循环接缝独立量化验证（2026-10-05）','',
'结论：自建导出为游戏循环服务的理由基本成立。九首 OGG 均按诠释周期保留跨周期余音，没有首尾静音，单采样接缝跳变未超过全曲内部差分 P99.9；对应 MuseScore MP3 含起音前留白与较长结尾静音，不能直接作为同等循环版本。但现有链路不能承诺数学严格无缝：压缩器在裁切后重置包络，时间采样四舍五入也能产生一采样不对齐。本次没有实际听音，不能声称听感验收合格。','',
'独立 Agent 未读取 docs/ 与既有验收报告。检查 build_soundtrack.py、interpret.py、synth.py、cues.py 的真实链路及 sf2.py 释放段实现；未运行 main、未修改曲谱或正式音频。', '',
'## 方法与定义','',
'使用 ffmpeg 解码全部九首 OGG 与对应九首 MP3 为 44100 Hz 双声道 float32；numpy 计算。时长取实际解码样本数。RMS 按两个声道全部样本计算，dBFS 以振幅 1 为参考。跳变 J=max_channel(abs(first-last))；内部差分为每声道相邻采样绝对差，P99.9 在全部声道差分上计算。局部比较另取末10ms+首10ms（以及100ms）邻域。静音定义为两声道绝对振幅均低于阈值的首尾连续样本长度，而非感知静音。完整数值、SHA256、最早演奏事件见 loop-seam-metrics.json；PCM链路见 loop-seam-door-side-rerender.json。','',
'非零首尾差并不自动代表点击：正常音波逐采样变化，曲首计划起音也会提高局部能量。interpret 为旋律提前12ms并加约3ms抖动，三个周期复用同一事件/波形；负起音会进入前周期结尾。RMS首尾不同既包含正常起音/终止，也可能包含增益包络变化，不能单凭窗口RMS定罪。','',
'## 现有18个文件','',
'| 曲目/格式 | 秒 | 首10ms dBFS | 尾10ms | 首100ms | 尾100ms | J | 内部P99.9 | 内部max | J/P99.9 | −60dB首/尾静音秒 | −80dB首/尾静音秒 |',
'|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|']
for slug,v in m.items():
    for kind in ['ogg','mp3']:
        q=v[kind]
        lines.append(f"| {slug}/{kind} | {q['duration_s']:.6f} | {q['head_10ms_rms_dbfs']:.2f} | {q['tail_10ms_rms_dbfs']:.2f} | {q['head_100ms_rms_dbfs']:.2f} | {q['tail_100ms_rms_dbfs']:.2f} | {q['seam_jump_max']:.6f} | {q['internal_diff_p999']:.6f} | {q['internal_diff_max']:.6f} | {q['jump_over_internal_p999']:.3f} | {q['head_below_-60db_s']:.6f}/{q['tail_below_-60db_s']:.6f} | {q['head_below_-80db_s']:.6f}/{q['tail_below_-80db_s']:.6f} |")
lines += ['',
'day 的 J/全曲P99.9=0.975，J/接缝局部10msP99.9约1.12，是本组最接近局部异常阈值的曲目，仍不能由此认定听得到点击。其余八首局部J/P99.9均低于1。rite首尾10ms能量差约8dB，但首小节有管钟、建鼓、击柷等计划起音，不能把乐器起音误判为额外点击。','',
'## 三遍取中间的真实保障与缺口','',
'render 将同一组事件渲染三遍，先对完整send做有限长IR卷积，再裁第二遍。前遍尾音和混响确实能进入第二遍开头，第三遍提前起音能进入第二遍尾部。IR长度=1.3×reverb+18ms，本组约1.708–3.918秒，周期均43.6秒以上；采样器释放最多8秒×1.15，当前常见合成尾音也远短于周期。因此本组前周期提供了充足的尾音铺垫。一般化仍需检查声源尾音+IR支持长度是否超过一周期，三遍本身不是任意曲目/任意尾音的证明。','',
'事件位置使用round((event.start+p×total)×SR)，裁切使用round(total×SR)起点和长度，两种舍入并不保证整数平移。door_side的精确周期=2349437.48517采样，裁2349437采样，但第3起点−第2起点=2349438；循环比连续下一周期少1采样（22.68微秒），可导致细小相位差。这不是首尾必须相等的问题，而是循环网格并非严格周期。','',
'compress 在裁出的单周期上执行：g=0重新初始化；512采样RMS分块、末块补零，插值曲线未设周期边界。door_side 实测首端增益+0.0317dB，末端+1.7633dB，循环时增益回跳−1.7316dB。首尾包络确实不周期，三遍并未保障此后置阶段。master 是整曲固定增益加无状态soft clip，不引入独立时间状态，但会缩放已有不连续；Vorbis分别编码端点也不能保证严格周期波形。','',
'## door_side受控重渲染','',
'仅调用render与write_ogg，输出到 tmp/audio-seam-validation/door_side.ogg；临时包装compress捕获输入与输出，未改变原算法。', '',
'| 阶段 | J | 内部P99.9 | J/P99.9 | 首10msRMS | 尾10msRMS |',
'|---|---:|---:|---:|---:|---:|']
for stage in ['pcm_precompress','pcm_compressed','pcm_mastered','encoded_ogg']:
    q=r[stage]
    lines.append(f"| {stage} | {q['seam_jump_max']:.8f} | {q['internal_diff_p999']:.8f} | {q['jump_over_internal_p999']:.4f} | {q['head_10ms_rms_dbfs']:.3f} | {q['tail_10ms_rms_dbfs']:.3f} |")
lines += ['',f'现有与重渲染OGG解码样本最大绝对差={error:.9g}；OGG文件字节哈希可因容器随机serial等信息不同而不同，比较解码波形更合适。原18个生产文件再次核对SHA256均未变：{unchanged}。', '',
'编码后door_side J=0.003958（−48.05dBFS），低于接缝局部10ms最大内部差分0.01697，未出现突出单采样尖峰。包络缺口是已证实的逻辑/量化事实，是否可闻仍需试听与真实Godot循环播放验证。','',
'## 试听与局限','',
'loop-seam-door_side.wav、loop-seam-day_noon.wav、loop-seam-rite.wav各12秒，顺序为末3秒→首3秒→末3秒→首3秒，真实循环接缝在3秒与9秒；6秒处是人为跳至末段，不应当作游戏循环接缝。不施加淡化、不响度重调，24bit PCM。','',
'本次没有声卡实听、Godot宿主播放、解码器反复seek性能或主观音乐终止感评价；量化只排查明显静音/采样级异常并指出包络缺口。单采样差分阈值不是通用听觉验收线；不同采样率、引擎解码与设备可能影响接缝。MP3为另一渲染器和演奏时序，较长时长并不单由静音导致。复现：python artifacts/music-rewrite/validate_loop_seams.py；受控单曲复现加--rerender。']
(out/'loop-seam-validation.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('production hashes unchanged:',unchanged,'decoded rerender max error:',error)
