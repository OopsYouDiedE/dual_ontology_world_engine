# 乐器映射独立验证

验证日期：2026-10-05。使用全新独立上下文，仅检查 music 源码、曲谱归档、本机 MuseScore instruments.xml 与 MS Basic.sf3，未读取项目设计文件或历史报告。

## 源映射与保留范围

9 首 CUES 共 44 声部，全部 Instrument 模板 ID 存在于本机定义，musicXMLid 与真实模板一致，bank/program 与 CUES 一致，所用 bank/program 预设存在于 MS Basic.sf3。CSV 包含文件名、逐声部前后名称/ID/musicXMLid/bank/program、本机 channel 选项与采样预设名。

注意 Low Strings 明确使用 bank0/program49（String Ensemble 2），本机 strings 模板默认 arco 为48，属显式音色选择而非模板缺失；Pizzicato Strings 使用其真实 pizzicato channel45。所有非打击声部原音符音高、时值、节奏、表情与排版结构未变。击打声部仅进行指定pitch映射、TPC/临时记号归一与打击谱号/单线谱表配置；duration、rests、chord事件个数、强弱、重音、文字及其它结构均保持。

frame-drum、tablas、bass-drum、wood-blocks 的 Drum 子树逐字段与本机 instruments.xml 完全一致。打击源pitch映射见CSV。9曲实际MIDI逐track检查全部通过；打击声部用零起算channel9、bank128。Rite Concert Bass Drum 和 Wood Blocks 共用channel9/program48且分别使用36/76，为同一预设中的两个有效击打音高，没有program冲突。

build_mscx(piece) 的44个Instrument及Part Staff metadata与就地修正MSCZ一致；Door已有用户eid被保留，比较生成器时只忽略该生成器不产生的标识。

## 原生导出与可见结果

MuseScore4.exe 单个原生进程、一次一个输出、Start-Process Hidden/Wait/PassThru：9PDF、9MusicXML、9MP3 共27次均exit0，覆盖music中原有同名衍生文件。没有重写原MSCZ。MP3使用-b320。未调用batch。3次临时MSCZ另存（Door/Evening/Rite）也均exit0，仅保存于tmp目录。

PDF全部由Poppler渲染，共22页。contact-1.jpg与contact-2.jpg缩略检视未见页边剪裁，最终版面取舍由主代理复核。原生导出与采样级解码是自动回归证据，没有将其写作人工试听或盲玩验证。

| 文件 | PDF页数 | MP3秒 | 平均dBFS | 峰值dBFS |
|---|---:|---:|---:|---:|
| Blessed Sleepers - Afternoon.mp3 | 2 | 53.055 | -24.0 | -4.9 |
| Blessed Sleepers - Confrontation.mp3 | 2 | 46.707 | -21.2 | -5.9 |
| Blessed Sleepers - Days of Sixteen.mp3 | 2 | 51.069 | -27.3 | -12.3 |
| Blessed Sleepers - Evening.mp3 | 2 | 78.707 | -30.3 | -7.8 |
| Blessed Sleepers - Main Theme.mp3 | 5 | 159.242 | -24.7 | -2.0 |
| Blessed Sleepers - Noon.mp3 | 3 | 69.355 | -23.3 | -7.7 |
| Blessed Sleepers - Rite.mp3 | 2 | 99.056 | -25.2 | -5.6 |
| Blessed Sleepers - Sleep.mp3 | 2 | 56.398 | -36.3 | -19.6 |
| Blessed Sleepers - The Near Side of the Door.mp3 | 2 | 56.398 | -39.0 | -20.9 |

所有MP3为320000bps、44100Hz、双声道，ffmpeg完整解码exit0且非静音。ffprobe与volumedetect完整日志随验证输出保存。非静音与完整解码仅证明媒体有效，不独立证明每个声部的听觉音色正确。

## 临时roundtrip音源元数据的限制

最终复测Door CLI另存的四音乐声部与metronome均保留resourceMeta id=MS Basic、type=fluid_soundfont、playbackSetupData=last.last.last；Evening/Rite的CLI另存audiosettings tracks为空。last.last.last为库资源通用metadata，合法generic不等同具体音色。不能仅凭MS Basic库名称或该generic串判错，也不能把其当成具体乐器自动选择证据。44声部验证以真实Instrument模板、channel bank/program、真实Drum定义、原生导出为依据；没有宣称GUI音源面板已测试。

主代理依据MuseScore源码确认GENERIC_SETUP_DATA由Last/Last/Last构成。最终Door音频设置与用户备份精确比较，仅track instrumentId从organ变为reed-organ，其余所有内容保持，包括原有四声部in、out、aux、master、soloMuteState及音效发送。Door归档两轮都是234个Note，所有音符/节奏/用户排版、缩略图、automation.json、score_style.mss、viewsettings.json、container.xml精确保留。最终Door PDF/MusicXML/MP3与临时roundtrip各补导一次，4次均exit0；其余8曲未重复导出。

## 验证产物

- artifacts/music-rewrite/instrument-mapping-fix.csv：44声部前后明细。
- tmp/music-mapping-fix/validation/native-exports.csv：全部原生导出exit/格式/字节数。
- tmp/music-mapping-fix/validation/source-checks.json：归档资源与曲谱结构保留检查。
- tmp/music-mapping-fix/validation/generator-midi-checks.json：生成器及实际MIDI逐track检查。
- tmp/music-mapping-fix/validation/native-playback-selection.json：CLI另存实际resourceMeta，未知项如实保留。
- tmp/music-mapping-fix/validation/contact-1.jpg、contact-2.jpg、pdf-render/：22页Poppler图。
- tmp/music-mapping-fix/validation/*.ffprobe.json、*.decode.txt：音频探测与完整解码。

## 游戏OGG独立补验

9首游戏OGG全部ffmpeg完整解码exit0且非静音，ffprobe实际时长与本轮渲染输出四舍五入时长最大误差0.004898秒。平均音量均为-19.0dBFS；各曲峰值为-4.0至约0.0dBFS（volumedetect一位小数，Rite读数-0.0）。此项未启动Godot，没有替代实机试听。

| slug | 实际秒 | 预期秒 | 峰值dBFS |
|---|---:|---:|---:|
| main_theme | 153.193583 | 153.19 | -0.5 |
| door_side | 53.275215 | 53.28 | -1.7 |
| confrontation | 43.605737 | 43.61 | -4.0 |
| night | 53.206395 | 53.21 | -1.0 |
| day | 47.966508 | 47.97 | -3.1 |
| day_noon | 66.239932 | 66.24 | -1.4 |
| day_afternoon | 49.976553 | 49.98 | -1.3 |
| day_evening | 75.571565 | 75.57 | -1.3 |
| rite | 95.955102 | 95.96 | -0.0 |

补验记录：tmp/music-mapping-fix/validation/door-restored-checks.json、door-restored-exports.csv、ogg-checks.json以及各OGG ffprobe/decode日志。最终两张contact sheets由主代理逐页视觉复核通过，全部名称/布局/鼓谱无剪裁。
