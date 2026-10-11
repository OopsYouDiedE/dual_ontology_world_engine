# r17 独立机制验证

本轮为全新上下文独立代理执行的自动机制回归，不属于一级、二级或三级验收。未读取设计规范、剧本、生成剧情文件或历史游玩轨迹/报告。只审查 AGENTS、通用机制源码和中性夹具。生产代码由主代理修改，本代理只修改测试夹具。未提交或推送。

## 最终结果

- PlayerControllerCheck：controller-05，退出0，完整通过。覆盖自身结果不重开自身方法、外部依据只重开相关方法、失败经历保留、已见未做方法返查、没有当前问题时的 inspect/argument 探索、表单未知假设不替换已证明假设、普通菜单无作答奖励、纯END退出标记。
- Core SubjectiveDecisionRegression：core-decision，退出0。
- StrategyIntegrationCheck：strategy-integration，退出1，保留3项房间登记断言失败。两个 appended_room_receipt_updates_each_subject_own_room_observation 和一个 threshold_after_appended_entry_keeps_actual_room_and_own_observation；未修复或放宽断言。
- 正式宿主 Debug 构建：host-debug，退出0。
- Godot 导入：godot-import，退出0，无脚本/引擎错误。
- FullTest GDScript解析：fulltest-parse，退出0，无脚本/引擎错误。绘制等待3秒超时分支仅做源码核查及语法检查，未以实机验证该分支。
- Godot测试实例均结束；用户原有PID23084为编辑器，未关闭。主代理已获通知可以启动实机。

## 本代理的测试改动

Program.cs的U-route夹具先完成有效接触，在真实公开菜单中看见inspect并选择close，再检测返回未做方法的路线。没有注入经历或虚构完成。

RouteLearningCases.cs的更换路点检查保留同分、无新颖奖励，并要求无调查奖励、无需要目标路线标记；已经检视且没有实际未做方法的来源不再被任意第二次求知视为目标。

RepetitionResultCases.cs追加没有当前问题的inspect/argument未做方法返查、父来源多次访问不额外压低未做方法路线，以及普通菜单不获得作答奖励。均为中性机制夹具，不是玩家游玩证据。

## 保留的中间失败

controller-01：现场interact与menu混合候选被误记为来源内部未做方法，原402断言失败。主代理随后修正归属条件。本代理曾在controller-02临时以wait替代menu诊断，随后完全恢复原menu断言；最终controller-03/05以原断言通过。

controller-02：旧更换路点断言强求已检视目标扣分标记，但新机制已不将耗尽来源当作目标。按上述行为边界修正夹具，保留失败日志。

controller-04：本代理新增路线夹具错误地将未显示来源的可走路点放在来源32像素内，触发已有来源消失判定。将路点改为距当前位置32像素、距来源68像素，保持128像素回返路段；controller-05通过。未改生产逻辑。

## 未验证项

本轮不判断剧情、人物行为合理性、实际效率、游玩时长、画面或最终体验。测试通过及测试数量不能证明上述项目；这些由主代理对照设计与真实轨迹判断。一级、二级、三级均未执行。房间登记集成失败仍未解决。
