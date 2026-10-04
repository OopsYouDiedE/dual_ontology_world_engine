# Evening Tabla 本轮独立验证

日期：2026-10-05。独立 Agent 从新上下文执行，仅读当前源码、当前产物和本轮用户编辑版备份，未读设计文件、历史报告或旧 trial 结果。本报告属于自动回归和产物检查，不属于盲玩或听感验收。

## 结论

本轮小增量检查通过，未发现阻断问题。生产 Evening.mscz 没有被本验证重写。MusicXML、320 kb/s MP3 已通过本机 MuseScore 4.7.5 原生导出覆盖同名产物。本阶段只渲染了当时已有的 PDF，没有重导；最终 PDF 的独立原生重导见 tabla-pdf-export-validation.md。

## 用户编辑版保护

对照 tmp/tabla-soundfont/Evening-user-before.mscz：ZIP 成员清单相同。score_style.mss、Thumbnails/thumbnail.png、automation.json、viewsettings.json 和 META-INF/container.xml 均逐字节一致。XML 中其余三个 Part、Staff 逐序列化一致；第四 Staff 除 Note/pitch 外保持一致。移除第四声部 Drum 定义并归一 bank 字段后，整个 XML 结构相同。排版、中文自定义、其他轨道、outMix、音量及其他 audiosettings 字段均保持。

第四声部共 120 个击打音：open Ge 73 为 48 次、Na 71 为 32 次、Tin 87 为 32 次、Te 84 为 8 次。实际生产音符按顺序与当前源码生成结果完全对应。旧编辑版的低鼓 64 对应新 Ge 73，旧高鼓 62 按来源击法恢复 Na/Tin/Te；所有时值、休止、重音和其他记谱字段保留。

## 指定采样与生成器

四声部预设为 Sitar bank 0/program 104、Flute 0/73、Sitar Drone 0/104、Tablas 0/0。当前生产 MIDI 与独立生成的临时 MIDI 均验证 controller 0、32 为 0，program 分别为 104、73、104、0；Tabla 为 MIDI 第 10 通道，音符集合为 71、73、84、87。

生产 MSCZ 第四轨 resourceMeta.id 为 `Tabla\0\0`，显式 attributes 为 soundFontName Tabla、presetName Tabla、presetBank 0、presetProgram 0。与本机 4.7.5 FluidResolver 的 makeId 和 preset 属性格式一致。

独立调用 score_io.write_mscz(cues.day_evening()) 生成 tmp/tabla-soundfont/final-validation/Evening-generated.mscz。该文件包含 audiosettings.json 与 container 引用。MuseScore 原生另存为 Evening-native-resaved.mscz 返回 0，第四轨仍为 fluid_soundfont `Tabla\0\0`，未回退 MS Basic。

源码 Part.soundfont 为 SoundFonts/Tabla.sf2；render 对该轨加载指定采样库，preset 为 bank/program 0/0，note cache 与 region cache 均包含 soundfont 字段。项目库与 Documents/MuseScore4/SoundFonts/Tabla.sf2 SHA-256 一致。SF2 INFO 中 INAM=Tabla，IENG=Rogério Dec (http://mp3.com/dec)，ICRD=May 07, 2002；ICOP 与 ICMT 为空。本检查不作商用授权结论。

## 导出与解码

MusicXML 与 MP3 均使用单进程、单次单输出 Start-Process -WindowStyle Hidden -Wait -PassThru；两次返回 0。MP3 参数为 -b 320。PDF 经 Poppler 渲染得到 2 页，contact 已查看：标题、中文区段、四声部、谱表和页码清晰，无裁切、黑块或新遮挡；第二页仅末组谱表，为保留的现有两页版式。

| 文件 | 完整解码 | 时长 | 平均电平 | 峰值 |
| --- | --- | --- | --- | --- |
| Evening.mp3 | FFmpeg 返回 0，6,941,952 双声道采样值 | 约 78.707 s | -29.0 dB | -9.5 dB |
| day_evening.ogg | FFmpeg 返回 0，6,665,412 双声道采样值 | 75.572 s | -19.0 dB | -1.3 dB |

两者均为 44.1 kHz 立体声、非静音。MP3 包含原生导出的尾部释放，游戏 OGG 为当前循环渲染长度。本检查没有要求二者尾时长或混音响度相同，没有新增听感或无缝循环验收结论。

验证脚本保存在 tmp/tabla-soundfont/final_check.py、final_midi_check.py、final_decode_meta.py。PDF contact 为 tmp/tabla-soundfont/final-validation/Evening-contact.png。


## 最终 PDF 补交付

最终 PDF 已由另一全新上下文 Agent 用本机 MuseScore 4.7.5 确实重新导出，退出码 0；当前 MSCZ 的字节和修改时间均保持。两页重新渲染后由主代理视觉复核通过，包含用户保存的中文乐器名称和谱号。此前本阶段渲染的是旧 PDF，最终产物以此次重导为准。独立证据见 [tabla-pdf-export-validation.md](tabla-pdf-export-validation.md)，contact 位于 tmp/tabla-soundfont/pdf-final/contact.png。
