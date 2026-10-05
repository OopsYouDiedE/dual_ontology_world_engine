# MuseScore 原生 OGG 循环边界独立核验

2026-10-05；全新独立测试，仅核验当前《The Near Side of the Door》。未读取设计文稿、历史验证报告或旧测试脚本，未修改曲谱或生产音频；未以 MP3 代替 OGG。

**结论：本机 MuseScore 4.7.5 原生导出 OGG 成功，实际编码为 Opus。当前原生文件具有约 2.37 秒低于 −60 dBFS 的尾部安静段，因此不能作为持续音乐的无缝循环交付。** 首尾采样跳变虽很小，但这不消除长尾停顿。现有生产 OGG 为 Vorbis，首尾没有同阈值的连续静音，采样跳变也低于内部差分 P99.9；这些仅是波形边界证据，本轮未进行听感或 Godot 实机循环验收。

## 原生命令与结果

```text
"C:/Program Files/MuseScore 4/bin/MuseScore4.exe" --version
退出码 0；stdout：MuseScore4 4.7.5；stderr 空。

"C:/Program Files/MuseScore 4/bin/MuseScore4.exe" -o "C:/Users/iii/Desktop/dual_ontology_world_engine/tmp/musescore-ogg-check/door_side-native-exit-captured.ogg" "C:/Users/iii/Desktop/dual_ontology_world_engine/music/Blessed Sleepers - The Near Side of the Door.mscz"
退出码 0；耗时 7.976 秒；stdout/stderr 均为空；未超时（上限 300 秒）。
```

外部进程均由 `Start-Process -WindowStyle Hidden` 启动。首次导出到 `door_side-native.ogg` 成功产出 911,740 字节；由于首次启动者已退出，未保留该次进程退出码，随后独立导出到新路径捕获完整退出码。未覆盖首次文件。命令及原始日志位于 `tmp/musescore-ogg-check/`，机器指标位于 [musescore-ogg-loop-metrics.json](musescore-ogg-loop-metrics.json)。

## 同方法完整解码比较

| 指标 | MuseScore 原生 OGG | 现有生产 door_side.ogg |
|---|---:|---:|
| ffprobe 实际容器 / 编码 | Ogg / Opus | Ogg / Vorbis |
| 采样率 / 声道 | 48,000 Hz / 2 | 44,100 Hz / 2 |
| ffprobe 时长 | 56.339500 s | 53.275215 s |
| ffmpeg 解码退出码 | 0 | 0 |
| 解码 PCM 时长 | 56.333000 s | 53.275215 s |
| −60 dBFS 首 / 尾连续静音 | 0.002188 / 2.366646 s | 0 / 0 s |
| −80 dBFS 首 / 尾连续静音 | 0.000438 / 1.556063 s | 0 / 0 s |
| 首 / 尾 10 ms RMS | −50.20 / <−130 dBFS¹ | −21.40 / −25.12 dBFS |
| 首 / 尾 100 ms RMS | −44.13 / −130.80 dBFS | −21.73 / −24.18 dBFS |
| 最大声道首尾采样跳变 | 0.0000381931（−88.36 dBFS） | 0.00395770（−48.05 dBFS） |
| 内部采样差分 P99.9 | 0.00534497（−45.44 dBFS） | 0.06316982（−23.99 dBFS） |
| 接缝跳变 / 内部 P99.9 | 0.00715 | 0.06265 |

¹ 原始浮点尾 10 ms RMS 为约 1.40×10⁻⁴⁵，属于极小的 float32 值；精确值保留在 JSON，报告按接近数字静音表述。Opus 容器时长与解码时长相差 312 / 48,000 = 0.0065 秒，对应 probe 中的初始 padding；比较使用实际解码帧数。

方法：ffprobe 查询实际流；ffmpeg 将整首解码为原采样率的 float32 PCM（不裁切、不拼接、不重采样）。静音为首尾连续帧内全部声道幅值均低于阈值；RMS 合并该窗口所有声道样本；跳变逐声道比较末帧与首帧，取最大值；内部 P99.9 为整首每声道相邻帧绝对差分分布。两文件的响度、渲染与采样率不同，因此绝对跳变不作为单独听感优劣判断。保留每首末 3 秒接首 3 秒的试听 WAV，尚未试听：`tmp/musescore-ogg-check/*-seam-3s-before-after.wav`。

## 原文件保护

导出前后 SHA-256 一致：

- 曲谱 `music/Blessed Sleepers - The Near Side of the Door.mscz`：`A8E6278466EE11B60D48C571089C51FA06E7FB1B9749A03CA8C1FA099924A200`。
- 生产 `godot_csharp/ShareAsset/Internal/Props/Audio/Music/door_side.ogg`：`61AC030C35AEBE6F23D0D0FE219BD9FFA103DBB14EB3C825748D95E247C133C1`。

本轮没有扩展第二首、没有试听结论，也没有把编码格式本身当作循环成功依据。
