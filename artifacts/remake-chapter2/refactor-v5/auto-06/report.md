# auto-06 独立定向 UI 回归

本轮只运行现有 Godot/C# 编译产物与当前 GDScript；没有重建 C#、运行全量回归/Core/bots，也没有接触独立 CLI 盲玩。只读当前源码，不读取设计或历史报告。末两处新增 C# 反馈修复不属于本轮编译版本。

笔记、无图查中央厅公告、帮助均通过实际 RichTextLabel 原文逐行相等、非空文字实际 size 大于零检查。真实中央公告板交互取得原场景 Sprite2D 素材，图片卡及事实正文通过相同检查。四张截图已用 view_image 检查。初始对白没有被此自动脚本推进完，其 layer20 覆盖面板底部，因此截图仅证明正文恢复和素材显示，不能作为正常交互全部布局通过的证据。

首次失败停止于第3日上午真实宿主重演：“没有第 1 项。只写‘重演’看列表。” 当前路径 replay_options 为空，测试脚本调用 _choose(0,1) 选择了不存在的选项。此为测试前提错误，不能据此判定 accept_replay 有产品问题。重演结果去重、证物卡完整原文、修改后过期均未得到验证，需要全新 Agent 在已有合法选项的真实路径上复测。

已保留 commands.txt、visible_output.txt、checks.json、四张 PNG 与 cli_raw.txt。独立 CLI 用同一命令文件执行；去掉 CLI 输入回显后，宿主126行与 CLI 前126行逐行一致，CLI另有EOF结束尾行。未为通过测试修改生产源码。

启动命令：Godot_v4.6.1-stable_mono_win64_console.exe --path godot_csharp --script res://scenes/Chapter2/Game/check/regression_auto06.gd --rendering-method gl_compatibility

独立 CLI：dotnet.exe godot_csharp/scenes/Chapter2/Prototype/bin/Debug/net8.0/Chapter2Prototype.dll play artifacts/remake-chapter2/refactor-v5/auto-06/commands.txt
