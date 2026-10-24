# Godot 标题返回与退出观察

本轮仅观察采集工具退出路径，不构成正式游戏会话或通关验收。按 `godot_csharp/tools/README.md` 的普通窗口采集协议启动 `res://scenes/Highlights/Highlights.tscn`，使用 GL compatibility / OpenGL3、Dummy 音频、1280×992 窗口及普通鼠标输入。输入为 `[{"label":"exit-stage","wait":2},{"click":[1200,920],"label":"exit-title","wait":2}]`。

进程在 2026-10-24 17:15:12.149 UTC 启动，于 17:15:23.435 UTC 结束，运行 11.286 秒，退出码为 0。工具保存两张本轮截图，PNG 保存返回码均为 0；标题图记录时间为 17:15:20 UTC，尺寸 1280×992，SHA-256 为 `b5ecf27f767d71fdc11bee35c643ba51a7b1b833aeaabbc6691de248ada03b2f`。文件修改时间与 manifest 中保存时间相符。检查图像确认显示正式标题菜单。

stdout/stderr 完整输出中未见 `ObjectDB instances leaked` 或资源仍在使用的提示。stderr 仅有显卡不支持所需 OpenGL 3.3、Godot 切换到 ANGLE 的警告；启动日志显示 OpenGL ES 3.0 Compatibility，通过 ANGLE / Microsoft Basic Render Driver。

本轮来源 manifest 已复制到 `runtime/exit-provenance.json`。截图保存在 `runtime/screenshots/exit-stage.png` 与 `runtime/screenshots/exit-title.png`；原始运行日志只在本次观察中读取，未另存。