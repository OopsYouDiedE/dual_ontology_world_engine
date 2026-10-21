# 配乐用途、音域、音色与主题曲修订

本轮按用户对《对质》常驻、《午后》扬琴连续击奏、越界音、低音谱号、大鼓及主题曲设计的意见实施。开始前完整备份见本目录 `before.zip`、`before-hashes.json`。没有修改原有乐器名称、简称、声部数、记谱移调及专业/业余音域限制；未覆盖其他任务的UI、DV和美术改动。

## 实际修改

《对质》限定在已送达的正式答辩回执台词呈现期间，第一章调查背景采用《门的这一侧》。阅读结束恢复最新场景背景；旧回调不能关闭新的交锋作用域，普通文字和候选行动不能触发对质。当前机制连接真实Sound/GameAudio/UISystem入口，但本轮没有运行整章游戏。

《午后》扬琴第16小节第3拍到第17小节末休止，第18小节恢复。实际原生MIDI中该六拍区间没有新击奏，前一音已结束；按四分音符96为3.75秒。小提琴在空档承接主题，自然释放尾音不硬切。末小节提前结束重复击奏。

主题曲从40小节重编为32小节：4小节引子、8小节钢琴陈述、8小节钢琴与大提琴回应、8小节再现、4小节尾声。移除连续高八度钢片琴加倍与钢琴八度齐奏；保留门句与B大调结束。钢片琴只奏10个音，最高B5；管钟只有2次起音；钢琴最高A5。簧风琴改为中低音区衬托和低音谱号，低音提琴只在3处加入，不再全程复制大提琴根音。原生MIDI中钢琴右手出现力度变化，左手维持独立pp；谱面包含37条乐句连线和7条渐强渐弱线。新版游戏音频约126.79秒。

音域采用具体音符/声部处理：二胡高于业余上限的B5改A5；陶笛原低B4句尾改写并保留合法B5结束；管钟为其可用音区重写连续轮廓；筝伴奏改为紧凑且连贯的合法音型。低音弦乐、低音持续音、低音三味线、簧风琴和重新配置的笙采用低音谱号；筝与其他中高音旋律保持易读的高音谱号，避免改成低音谱号后反而产生大量上加线。独立原稿music1另行检查，未再改写，未发现越界。

大鼓原先绑定Standard/87的Long Taiko Hit；改为Orchestra Kit（bank128/program48）/36的Orch Bass Drum。框鼓原45/47是通鼓采样，现使用Low Tumba/64与Bongo Rim/60的手鼓代用；笛子从Piccolo改Flute代用，二胡从Fiddle改Violin代用。以上替代均保持显示名称并明确记录，不声称传统乐器实录。真实预设和已用键位经本机MS Basic.sf3分区表核查，详见sample-map.json。

修复原生混音设置：Part.gain/pan和Piece.mix真正写入audiosettings，取消所有轨道重置0dB的做法；多谱表乐器共用同一推子，左右手用allInStaff力度独立控制。减少鼓、木鱼、钟与钢片琴的抢占，降低持续伴奏的混响发送。显式音色覆盖同时更新保存的Channel、打击乐键位和原生预设；其余声部保留自动奏法音色切换。

## 核查与保留的失败记录

九首发行曲谱均通过实际MSCZ范围、源数据一致性、原生混音及指定采样键检查；对比备份证明乐器身份、记谱移调与范围上限/下限未被放宽。改变谱号的过程不改变Note.pitch。输出见score-audit.json、final-static-audit.json、sample-map.json。九首都用MuseScore 4.7.5重新导出原生MIDI、MusicXML和WAV，并更新游戏OGG；试听MP3由同一份母带OGG转码，避免拿未经响度处理的原谱与游戏音频混比。audio-audit.json记录最终编码后RMS、峰值、接缝及文件指纹；编码后没有满幅削波，但未据此宣称听感完美或接缝绝对不可闻。

已经查看main_theme第1/3页、day_afternoon第2/3页和rite第2页PNG，确认实际谱号、扬琴休止与主题曲连线/力度标记。首次局部音频检查退出时出现4个资源未释放的警告；检查脚本由固定80ms等待改为等待实际音频混音步后退出。最终Godot资源导入与music-context检查均以0退出且没有ERROR。原始日志保留在logs/，最终记录是godot-import-final.log与music-context-final.log。MIDI检查中一次过度严格的“音符必须恰在休止边界结束”断言失败，后改为核实前一音在休止开始前结束；修正后原生MIDI验证通过，不把发音时长缩短误判成越过休止。

本轮没有进行人工听音、真实扬声器/耳机比较或整章游戏验收，采样替代和主观编排改善仍需按实际试听判断。多段日常曲的整曲循环与誓约剧情段落拆分沿用现有机制，本轮不声称已经完成新的事件分段系统。

## 产物与复核

`listening/main_theme.mp3` 是新版主题；`listening/main_theme_before.mp3` 为开始前版本，便于比较。同目录另有八首新版MP3。`native/` 保存九首原生MIDI/MusicXML/WAV；`preview/` 是真实MuseScore谱面PNG。实际游戏使用 `godot_csharp/ShareAsset/Internal/Props/Audio/Music/` 的九首OGG，已经重新导入。旧artifacts/releases/soundtrack不代表本轮试听版。

可重复静态命令：`python music/audit_score_assets.py --out artifacts/<新的目录>/score-audit.json`；曲谱一致性：`python music/build_soundtrack.py --scores`；局部音频入口：`Godot --headless --path godot_csharp --script res://tools/check_music_context.gd`。这些检查不创建或执行DV正式会话。
