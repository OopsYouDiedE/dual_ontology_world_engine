# 独立假设表单 UI 实际输入核查

使用冻结整合副本 `tmp/detective-rpg-integrated-20261021/godot_csharp` 的真实 InvestigationUI.tscn/.gd；外部 capture_replay_ui.gd 仅实例化该组件及 FrameArchive，以明确标注的通用 fixture 提供 V1–V6、A/B及fixture.named.*选项。没有启动Main/GameEngine/DV会话、没有注入游戏状态或谜题答案，也没有修改冻结runtime源码。Godot进程均已退出并释放。

## 已实际核查

run-03实际鼠标打开OptionButton并点击弹出菜单项，以真实滚轮滚动到V1–V6和发送按钮。初始命名假设及六维度全部selected=0，两个发送按钮禁用；逐项选择B时前五项不启用组合发送，第六项组成fixture声明的精确组合后才启用。实际点击发送仅由UI收集器接收 `{action_id:fixture.replay,hypothesis_id:fixture.case.all-B}`。重新打开新表单仍全部未选，实际选择命名Beta并发送，仅收集到fixture.named.beta。

临时TranslationServer切换en、ja、zh_CN并真实绘制，六项B均保留，组合发送仍启用；没有调用持久化语言setter。结束恢复原locale，language.cfg/settings.cfg前后SHA256一致。原始输入、控件状态、发送payload及FrameArchive完成回执均在run-03/ui-input.dvlog；14张PNG全部完成原子发布。已实际查看初始顶部、初始V6、选中组合、英日切换及命名Beta截图，文字/选项可见。

本轮只证明组件对这份通用fixture的选择与payload呈现；没有证明正式候选开放条件、真实付费结算、回执、知识、谜题正确性或游戏进展。

## 前两次采集器失败

run及run-02都确认了初始未选状态并保存2张PNG，但采集器经子Viewport/弹出菜单发送End/Enter的键盘路由未激活预期菜单项，故如实封存失败。run-02日志显示PopupMenu仍visible、selected仍0。它们是UI采集输入路由未完成，不能写成玩法失败、表单错误或通过。第三次改用原生鼠标命中弹出菜单真实最后一行，没有调用select或emit_signal伪造选择。

run-02前未制作额外副本，原run始终原样保留；收到逐轮复制要求后，run-03前将run和run-02的全部已封存文件独占复制到carry-forward-before-03，并逐文件SHA256校验。失败脚本版本保留为capture_replay_ui-attempt01.gd及-attempt02.gd；无原始证据覆盖。

## 证据链

- 原主任务全部历史通过外部只读 `../carry-forward-03/carry-forward.dvlog` 继续引用。
- 最新性能比较单独引用 `../perf-integrated-90s-x4-03/`，不把本UI运行混入性能结论。
- `carry-forward-before-03/copied-evidence.dvlog`记录前两次UI全量复制与校验。
- `evidence-index.dvlog`记录本UI三轮PNG/二进制文件指纹与进程退出状态。

本次没有第二个正式会话，也未执行流程回归。
