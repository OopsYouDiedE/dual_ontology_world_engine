# 本机 MuseScore 原生导出独立验证

验证时间：2026-10-04 至 2026-10-05（Asia/Shanghai）。验证由全新独立上下文执行，未读取项目设计文件或历史验收报告，未修改曲谱、乐器代码或 OGG。

## 结果

九份原始 MSCZ 均成功直接导出同名 `music/*.mp3` 与临时原生 PDF，18 项退出码全部为 0。两份显式 MuseSounds 探测亦退出 0。所有单项均在 11 秒以内完成，无超时。九份 PDF 共 24 页均可完整渲染；十一份 MP3 均完整解码成功、非静音，编码均为 320000 bit/s、44100 Hz、双声道。

验收限制：导出成功不代表民族乐器音色正确。两份显式 MuseSounds 探测的实际全部声部均分配 `MS Basic` / `fluid_soundfont`，未证实民族乐器专用音源。PDF 存在一个明确排版缺陷：《暮课》第一页第二谱系统的 Andante 速度标记右端超出页面边界并被裁切，故本轮排版不作全部合格结论。

## 执行方法与保护

本机执行文件为 `C:/Program Files/MuseScore 4/bin/MuseScore4.exe`，`--long-version` 返回 MuseScore 4.7.5，Build 3654226。每份曲谱分别启动 CLI 单项导出，均使用 PowerShell `Start-Process -Wait -PassThru -WindowStyle Hidden`，没有 batch job。

- 默认音频参数为 `-b 320 -o "music/<原文件名>.mp3" "music/<原文件名>.mscz"`。
- PDF 参数为 `-o "tmp/claude-native-music/<原文件名>.pdf" "music/<原文件名>.mscz"`，没有覆盖 music 中的 PDF。
- 默认导出没有强制 Basic、移除设置、factory settings、重置用户配置或复制后删减音源设置。
- Rite、Evening 的独立探测参数为 `-b 320 --sound-profile "MuseSounds" --tracks-diff "tmp/claude-native-music/<原文件名>-tracks.json" -o "tmp/claude-native-music/<原文件名>-probe.mp3" "music/<原文件名>.mscz"`。
- PDF 技能 marker 在首次导出前成功执行一次：`--operation-kind create --expected-output-count 9 --output-format pdf`。
- 导出后 `git status --short -- music` 仅显示九份新 MP3，未显示 MSCZ、OGG 或原 PDF 的修改。未关闭用户应用。

## 默认 MP3 与 PDF 逐份结果

时长按 ffprobe 容器 duration；RMS、峰值为 FFmpeg 全长解码 astats 的 Overall 统计。全部 MP3 解码退出 0，无解码错误；全部 PDF 的 pdfinfo 与 pdftoppm 成功。

| 文件名后缀 | PDF 页数 | MP3 时长（秒） | RMS（dB） | 峰值（dB） |
|---|---:|---:|---:|---:|
| Afternoon | 3 | 53.054650 | -26.667125 | -11.173428 |
| Confrontation | 2 | 46.706900 | -21.033808 | -5.786291 |
| Days of Sixteen | 2 | 51.069350 | -29.610090 | -14.611703 |
| Evening | 2 | 78.706900 | -29.382781 | -8.392837 |
| Main Theme | 5 | 159.242425 | -24.304399 | -0.637165 |
| Noon | 4 | 69.355075 | -22.737057 | -6.825683 |
| Rite | 2 | 99.056300 | -25.424353 | -7.106073 |
| Sleep | 2 | 56.398325 | -36.220451 | -18.730056 |
| The Near Side of the Door | 2 | 56.398325 | -38.984239 | -21.135548 |

九份 PDF 的所有 24 页均按 90 DPI 渲染并通过全页联系表人工查看。《暮课》第一页另查看全页 PNG 核实右边界裁切；正式证据为 `native-evening-page-1.png`。其余 23 页在本轮总览检查中未见明显缺字、页面内容裁切或大范围重叠。此处仅为原生排版检查，不评价旋律、配器准确性或设计符合度；没有进行听觉音色鉴别。

## MuseSounds 实际分配

两份探测的 stderr 为空，没有可用于证明 MuseSounds 正确加载的日志。`--tracks-diff` 返回 `oldTracks: []`，因此不能从该空列表推断原工程所有已保存音色；但 `newTracks` 明确记录此次 profile 应用后的实际分配：

| 曲谱 | 声部 instrumentId（排除节拍器） | 实际 name / type |
|---|---|---|
| Rite | bianzhong, bianqing, organ, ocarina, koto, taiko, woodblock | 全部 MS Basic / fluid_soundfont |
| Evening | sitar, flute, tanpura, tabla | 全部 MS Basic / fluid_soundfont |

两份探测的 metronome 亦为 MS Basic / fluid_soundfont。Rite probe 为 99.056300 秒、RMS -25.424353 dB、峰值 -7.106073 dB；Evening probe 为 78.706900 秒、RMS -29.382781 dB、峰值 -8.392837 dB，完整解码退出 0。

因此，显式要求 MuseSounds 的 CLI 本轮能够生成可解码音频，但实际分配证据不足以通过专用音源验收。不得将 `bianzhong`、`bianqing`、`tanpura` 或 `tabla` 等乐器标识本身当作正确音色的证据。

## 证据位置

- `artifacts/music-rewrite/native-musescore-exports.json`：逐项参数、退出码、耗时、文件大小。
- `artifacts/music-rewrite/native-musescore-metrics.json`：PDF 页数、渲染退出码、MP3 编码与解码统计。
- `artifacts/music-rewrite/Blessed Sleepers - Rite-tracks.json` 与 `Blessed Sleepers - Evening-tracks.json`：原始音源分配证据。
- `artifacts/music-rewrite/native-evening-page-1.png`：右边界裁切证据。
- `tmp/claude-native-music/`：本机 PDF、24 页渲染、联系表、MuseSounds probe MP3、CLI stdout/stderr、pdfinfo 与全长 decode 日志。

## 源码与本机音源核对（主代理整合）

`music/build_soundtrack.py` 的 `SAMPLED` 所用 18 个 bank 0 预设，在本机 `C:/Program Files/MuseScore 4/sound/MS Basic.sf3` 实际预设表中均存在。下列五种乐器由 `music/synth.py` 合成，未绑定到 MuseScore 的该合成器；曲谱 / MIDI 使用的 GM 预设为替代音色：

| 项目声部 | 曲谱 GM 程序号（零基） | 本机 MS Basic 实际预设 |
|---|---:|---|
| framedrum（框鼓） | 115 | Woodblock |
| tabla（塔布拉） | 116 | Taiko Drum |
| tanpura（坦布拉） | 104 | Sitar |
| bianzhong（编钟） | 14 | Tubular Bells |
| bianqing（编磬） | 13 | Xylophone |

此外 `strings` 的独立游戏渲染映射为 program 49（Strings Slow），而 `cues.py` 中 Low Strings / Drone 谱面声部及 MIDI 使用 program 42（Cello），两条播放链的音源不同。它们不能因同用 MS Basic 就被视作一致。

本机 MuseSampler 配置指向 `C:/ProgramData/MuseHub/Downloads/Muse Hub Instruments`。其 `.instruments` 数据库的 packages / instruments 表没有安装记录；Muse Keys、Muse Percussion、Muse Woodwinds 所检查文件，以及 Cellos / Cello Solo 的 SPX 均为零字节。部分其他乐器 SPX 非零，但目录或文件存在不足以证明所需专用乐器可用；本轮 Evening / Rite 的原生 tracks-diff 已给出实际落到 MS Basic 的证据。

本地原有曲谱修改及旧导出已在拉取前保留于命名 stash `codex preserve local score edits and exports before Claude music pull`，另有 `tmp/music-pre-Claude/` 文件备份。拉取合并提交为 `78d0395e`，保留原共享 UI，并接入远端音乐切换改动。此次核对没有修改配器、源曲谱或游戏音频。
