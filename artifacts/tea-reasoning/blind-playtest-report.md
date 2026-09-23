# 当前版盲试玩记录

日期：2026-09-23  
入口：`scenes/Reasoning/Reasoning.tscn`  
试玩者：AI 辅助测试，不代表真实陌生人试玩。

## 复现步骤

1. 在项目根目录启动：
   `Godot_v4.6.1-stable_mono_win64_console.exe --path godot_csharp --rendering-method gl_compatibility --log-file artifacts/tea-reasoning/blind.log scenes/Reasoning/Reasoning.tscn`
2. 尝试绑定运行中的 Godot 窗口并读取可见控件。
3. 计划中的现场操作：逐个 NPC 选择每个对话分支；不点击主厨“我该先从哪里查”；检查现场、札记和人物陈述；进入脑内重演并验证一套规则。

## 实际结果

Godot 进程可启动并写出日志，但桌面自动化接口返回 `apps=[]`，没有可绑定的 Godot 窗口，因此没有执行鼠标/键盘输入，也没有把静态文档或源码答案冒充玩家观察。当前不能判断对话按钮的无回应、意外关闭、证据缺失或整案是否可完成。

可见信息边界（来自场景操作说明，未读取案件实现）：玩家应能看到现场、只读札记、人物陈述和脑内重演界面；开场不应泄露全部终态；主厨提示属于可选引导。

## 独立驱动

`check/blind_playtest.gd` 只遍历运行场景中可见的 UI 文本并写入 `user://blind_playtest_visible.txt`，不修改调查状态、不注入证据、不读取案件实现。待桌面窗口可绑定后，再用真实输入补充 NPC 分支和无引导整案记录。

## 结论

本轮被桌面窗口不可见/不可绑定阻塞，尚未形成“能完成整案”或“不能完成整案”的玩法结论。需要在 Godot 窗口能被自动化接口发现后重测；若主代理修复 click/对话，也应从入口重新执行上述步骤。
