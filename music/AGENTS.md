# 音乐资产维护

本目录遵循根目录 AGENTS。剧情用途以用户指令和 docs/design-standard.md 为准。

- 音符按实际发声音高编写；改谱号只改变显示。不可通过扩大 MuseScore 保存的专业/业余音域或关闭警告处理越界。替代音、整声部移调、音区重写必须保留可读的旋律走向，并核对实际 MSCZ 与原生 MIDI。
- 保留乐器名称、简称、声部数与记谱移调。用户授权的音色修订用 Part.explicit_sound 显式声明，同时更新保存的 Channel、打击乐键位和 audiosettings 音源预设；其余声部保留自动奏法音色切换。代用采样须明示，不冒称传统乐器实录。
- Piece.mix、Part.gain/pan 必须同步到 MuseScore 原生混音。多谱表乐器同用一个混音推子，左右手用 allInStaff 力度控制，不为同一原生通道设置冲突增益。新曲谱原子替换，保存前完成音域和结构检查。
- 修改 cues.py 后重新生成对应 MSCZ，再用 MuseScore 原生导出 MIDI/音频；检查 python music/audit_score_assets.py --out <新的报告路径> 与 python music/build_soundtrack.py --scores。采样检查读取本机 SoundFont，不将第三方音色库复制进报告或重新分发。
- 只更新音乐时按 --only <曲目ID> 生成，避免顺带覆盖音效/人物语音。Godot 音频导入与局部 check_music_context.gd 检查可用于资源/作用域排错，不替代剧情游玩或人工听音验收。
- 音乐背景仅依据可见的章节/时段投影；对质只在已送达的正式答辩回执台词期间获得作用域令牌，结束恢复背景。候选答案、恶魔存在或普通文本不触发对质。不得修改世界状态以通过音乐检查。
- 本轮备份、原生导出、谱面预览和报告放独立 artifacts 任务目录；保留失败日志，明确区分谱面检查、音频数值、资源导入、作用域检查与实际听感。
