# 音乐源码与导出

本目录 `mcp/music_pipeline/` 保留可编辑曲谱和音频生成源码：`cues.py`、九首 `Blessed Sleepers - *.mscz`、`score_io.py`、合成/后处理模块、SoundFont、谱面样式与视图设置。`music1.mscz` 是单独保留的原谱，不属于九首发行导出清单。目录迁移保留原谱、采样资料、来源声明和许可文件。

| 内容 | 仓库路径 |
| --- | --- |
| 曲谱、编配与音频生成源码 | `mcp/music_pipeline/` |
| 专用采样库与来源记录 | `mcp/music_pipeline/SoundFonts/` |
| 游戏音乐、音效 | `godot_csharp/ShareAsset/Internal/Props/Audio/Music/`、`Audio/Sfx/` |
| 游戏人物短音 | `godot_csharp/ShareAsset/Internal/Characters/voices/` |
| 发行导出 | `artifacts/releases/soundtrack/` |

生成器按脚本位置寻找本目录曲谱、样式、视图设置和专用采样库；仓库根目录用于定位游戏成品和发行导出。`Part.soundfont` 继续相对本目录解析，例如 `SoundFonts/Tabla.sf2`。

## 生成入口

从仓库根目录运行：

```sh
python mcp/music_pipeline/build_soundtrack.py --scores
python mcp/music_pipeline/build_soundtrack.py --export --scores
python mcp/music_pipeline/build_soundtrack.py --from-mscz
```

第一条核对曲谱与 `cues.py`，缺少曲谱时会生成。第二条通过 MuseScore 4 导出乐谱和试听文件；第三条在安装 MuseScore 4 时以已有 `.mscz` 为输入生成游戏音频。常规运行会检查已有曲谱与 `cues.py` 的差异；只有明确传入 `--rewrite-scores` 才从代码改写已有曲谱，`--from-mscz` 跳过该比对。

PDF、MusicXML、MIDI、MP3 默认写入仓库根目录的 `artifacts/releases/soundtrack/`，可通过 `--export-dir <目录>` 修改。生成器默认不会把这些发行副本重新写回 `mcp/music_pipeline/`。该默认目录已被 Git 忽略。

顶层 MCP 入口 `python mcp/server.py` 的 `music_pipeline` 工具提供 `render`、`scores`、`rewrite-scores`、`from-mscz`、`export`、`audit` 操作。未给 `operation` 时保持默认 `render`；`arguments` 接受原有命令行选项。`export` 采用 `--export --scores`，`audit` 调用 `audit_score_assets.py` 并接受其 `--out` 参数。音乐、音效和人物短音可用 `arguments=["--only","<ID>"]` 选定；对应模块在调用时加载。

需要 Python、NumPy，以及写入 OGG 时使用的 `soundfile`。安装 MuseScore 4 后，可通过 `MUSESCORE` 环境变量指定程序路径；未发现 MuseScore 时脚本会提示使用内置合成器，并跳过相应发行导出。该回退使用 `cues.py`，即使传入 `--from-mscz` 也不会改成读取原谱渲染；若要求以现有曲谱为唯一输入，须先配置 MuseScore。内置合成器与 MuseScore 的音色和渲染结果可能不同。

## 游戏使用的音频

游戏音乐与音效写入 `godot_csharp/ShareAsset/Internal/Props/Audio/Music/` 和 `Audio/Sfx/` 下的 `.ogg`；人物短音写入 `godot_csharp/ShareAsset/Internal/Characters/voices/`。首次准备工作副本时，应先生成需要的音频，再让 Godot 导入。独立发行 MP3 不替代这些运行时路径。

## 历史发行副本

2026-10-20 的仓库清理将九首曲目的 36 份 PDF、MusicXML、MIDI、MP3 从当前源码树移除，原谱和生成链保留。原始导出可从清理前提交 [`6166be88`](https://github.com/OopsYouDiedE/dual_ontology_world_engine/tree/6166be88b96864271b33a1624c0568cb728581c7/music) 取回；Git 历史未重写。重新生成的文件是否与旧导出逐字一致，取决于 MuseScore、SoundFont 和环境版本。

## 2026-10-21 配乐修订记录

本轮修正了双谱表钢琴力度作用域：生成的 MuseScore Dynamic 显式设置 `voiceAssignment=allInStaff`，右手旋律不再受左手弱奏覆盖；主主题及现场、日常变奏作了局部声部变化。《午间》后八小节改为笛子加花与二胡骨干相互衬托，不再完整复制前一段；《午后》分离框鼓不同击打 MIDI 音高，并在前奏加入呼吸式细微变速；《暮课》降低引子持续伴奏的脉冲感，后段把笛与西塔琴处理成分句接续。制作链的音频循环接缝在混音母带处理后再次对齐边界样本。

独立原谱 `music1.mscz` 仅清理原来的末尾空白小节，原始文件备份在 `artifacts/music-revision-20261021/music1-before-trim.mscz`。游戏不再对完整主题曲 `main_theme` 强制循环。对《誓约》等具有起礼/终礼结构的配乐，进一步拆分成状态驱动的循环乐段需要与实际游戏事件结合，不把波形级无缝当作音乐结构的无缝。项目曲谱及游戏 OGG 的再生入口见本文件上文。

## 当前编排与音色修订（2026-10-21，第二轮）

主题曲现为32小节：引子4、钢琴陈述8、钢琴与大提琴回应8、再现8、尾声4。保留门句与最终B大调解决，撤去连续高八度钢片琴加倍和钢琴八度齐奏；簧风琴改低音谱号与中低音区和声，低音提琴仅在三个位置补充基础。原生乐句连线和渐强渐弱已写入谱面，不依靠回退合成器诠释。

《午后》扬琴第16小节第3拍至第17小节末不再击打，第18小节恢复，按四分音符96计算共3.75秒；自然余音不强行截断。《午间》二胡的越界高音用A5替代，筝改用连贯的合法音区伴奏；《誓约》陶笛低音改写、管钟采用连续的紧凑旋律，笙重新配置在低音区。专业与业余音域限制均保持原值，低音持续声部使用低音谱号；中高音旋律保留更易读的谱号，不把所有单行谱机械改成低音谱号。

大鼓从 Standard/87 的长太鼓采样改为 Orchestra Kit（bank128/program48）/36 的管弦大鼓；框鼓不再使用通鼓45/47，改为手鼓代用采样 Low Tumba/64 与 Bongo Rim/60。D调笛子使用Flute代用音色，二胡使用Violin代用音色。乐器显示名称保留，以上音色均为现有采样库的明确代用，不宣称传统实录。原生音源绑定、各轨音量、声相与混响发送由源数据写入曲谱，避免只在回退合成器生效。

第一章常驻《门的这一侧》；《对质》只在正式答辩回执台词显示期间播放，结束回切背景，普通对话、打开笔记本或出现答案候选不会触发。局部检查脚本位于 `mcp/verification/check_music_context.gd`，入口是 `python mcp/godot_workspace.py --headless --script res://tools/check_music_context.gd`，它使用独立工具项目，不运行 DV 会话。

静态复核命令：`python mcp/music_pipeline/audit_score_assets.py --out artifacts/<新的目录>/score-audit.json`。本轮试听MP3、原生MIDI/MusicXML/WAV及PNG谱面位于 `artifacts/music-orchestration-20261021-v2/`；实际游戏仍使用 `ShareAsset/Internal/Props/Audio/Music/` 的OGG。旧 `artifacts/releases/soundtrack/` 不代表本轮试听版本。音频数值、音域和资源检查不替代人工听音。
