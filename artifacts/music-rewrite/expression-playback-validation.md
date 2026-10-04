# 隐形演奏控制独立验收

日期：2026-10-05。验收 Agent 使用新独立上下文；未读取项目设计文件或历史验收报告。读取当前音乐源码、指定 before 原稿备份、applied.json，以及本轮清理清单。

## 结论

通过。四份原稿只加入隐形播放控制；原音高、时值、休止、乐器、谱号、文字、排版参数和音色配置均保留。正式同名 MP3、MusicXML、PDF 已实际使用 MuseScore 4 单文件、单输出导出；四份正式 MIDI 经逐事件比对后同步。

## 原稿与原生往返

- 最终 applied.json 的 before/after SHA-256 均与实际文件匹配。ZIP 成员名称一致，所有非 MSCX 成员内容逐字节一致。删除新增 Slur/HairPin 与 visible=0 的 Tempo 后，XML 全树（元素顺序、属性、文本）完全一致，因此既有用户改动未被重建覆盖。
- Main Theme：4 条隐形 Slur；m35→m36 一条 HairPin（渐弱，veloChange=-31，singleNoteDynamics=0，voiceAssignment=allInStaff）。Door：1 条隐形 Slur。休止处分断逻辑与当前源码一致。
- Afternoon：16 项新增隐形 Tempo；Evening：26 项新增隐形 Tempo。原有可见速度仍保留。
- 实际原生另存四份副本至 tmp，Slur/HairPin/Tempo 数量、visible=0、allInStaff 全部保留。正式 MSCZ 未通过另存重写。
- Evening 逐节点保留用户的西塔琴、长笛中文名，谱号、单线打击乐谱表及 Tablas/Tabla 鼓映射；audiosettings.json 等音色配置逐字节保留。

## PDF 与导出

- 12 次正式导出均退出 0，每次通过 Start-Process -WindowStyle Hidden，单首单输出，未关闭用户应用。
- 遵循 PDF 技能标记、Poppler 渲染及图像查看流程。主旋律 5 页、门的这一侧 2 页、午后 3 页、暮课 2 页，共 12 页已查看。
- 独立从 before 原稿再次原生导出基准 PDF，与正式新 PDF 的所有页面在 scale-to=1000 渲染下逐像素完全相同（全部 changed_pixels=0），页面尺寸与页数一致。隐形播放控制未改变谱面。
- 旧正式 PDF 曾存在与实际原稿不匹配的问题，午后旧 2 页而原稿原生导出已为 3 页；该差异属旧输出过时，不是本轮控制导致。

## MIDI 与音频

- 四份 MIDI 先在 tmp 用当前生成器产生，再逐轨比较 note-on 的时间、通道、音高及完整 conductor 事件，全部相同，才同步至正式同名 MID；m35 渐弱整数取值依本轮授权匹配原生 HairPin，其他原力度保留。连奏段 note-off 延长至下一时值前 1 tick，其他原音符保持。
- Main Theme 钢琴右手 m35→m36 各起音力度 80→73→69→65→57→49，左手保留原力度。
- Afternoon 引子 BPM 95.1396–97.1449，m5 恢复 96；Evening 引子 BPM 59.3726–60.8348，m9 恢复 88。实际读取正式 MIDI 速度事件证明有效。
- 以下音频均用 FFmpeg 全长解码为 44.1 kHz 双声道浮点 PCM，退出 0、无解码报错、全部有限且非静音。

| 文件 | 完整解码秒数 | RMS | 峰值 |
|---|---:|---:|---:|
| Blessed Sleepers - Main Theme.mp3 | 159.242 | 0.059447 | 0.839968 |
| Blessed Sleepers - The Near Side of the Door.mp3 | 56.398 | 0.010669 | 0.085669 |
| Blessed Sleepers - Afternoon.mp3 | 53.029 | 0.059910 | 0.532958 |
| Blessed Sleepers - Evening.mp3 | 78.524 | 0.033797 | 0.308742 |
| main_theme.ogg | 153.194 | 0.112037 | 0.946805 |
| door_side.ogg | 53.275 | 0.112442 | 0.821761 |

## 清理与独立生成

- 在不含任何旧 Duet 文件和旧脚本的隔离目录复制现行生成器与样式资源，实际写出四首 MSCZ/MID，ZIP 自检与非空检查通过；最终检查使用清除旧 Note dataclass/performance 函数后的当前源码。
- 正式清理清单 18 个文件：原路径全部不存在，tmp/obsolete-music-backup 对应备份的 SHA-256 全部匹配；未删除备份。
- 七个现役音乐模块的检索不存在旧系列名或旧生成脚本引用。

## 证据位置与范围

机器记录与原生输出副本：tmp/expression-playback/independent/ 下 structural.json、native-roundtrip.json、exports.json、pdf-baseline-compare.json、midi.json、audio.json、generator.json、cleanup.json。

本轮没有运行其余五首全量回归，没有读取谜底或设计文件；这里是结构与原生导出/播放事件验收，音频验收为完整解码与数值检查，并非主观听感评审。

