# 五日历史耗时只读复核（2026-10-18）

结论：此前确有在180现实秒内推进到五日时域终点的一级 formal-model 记录。`verify-5days-180s-r13/result.json`记载76.07799999999406秒、退出码0、逻辑循环结束、原始trace写完。`events.jsonl`中的实际debug_end为game_time=7200、horizon=7200、active=false、stop_evidence.reasons=[world-horizon]，action_bound_reached=false；stdout的对应记录一致。不是把180秒预算当成实测耗时。旧result没有单列编译、后处理及总交付耗时，不据此承诺整体工作76秒完成。

五日时域与剧情完成须分开。原始末段的session_stopped虽然写budget-exhausted，debug_end的具体停止证据是world-horizon，角色仍有行动额度。结尾只读状态为council_closed=true（6330）、underground_tremor=true（6330），council_ended=false；侦探signed/voted均false。这些具体值不能证明预期剧情、人物行动及闭幕完整达成。本次不读设计文档，不对其设计合格性作结论。

## 历史与当前条件

- 旧封存bundle `program/session.dv:13959`在扩章策略中强制plan_depth=1、first_night_plan_depth=1、positive_move_root_stop=true、daytime_wait_chunk_limit=120。当前`chapter2/content.dv`相应行为为深度2、positive_move_root_stop=false，等待块仍120。旧移动首层正收益可提前停止的算法分支当前仍有，但配置已经关闭。旧76秒不能直接作为当前深度与行为条件下的性能基线。
- 旧`dvs_schedule`还有last_decision_stopped缓存：非玩家主体在mind修订、mind时间、章节及待收闻条件不变时被排除重新决策；当前已移除该排除，因为世界时刻、现场到达及内容目标可产生新机会。这会改变实际参与的决策与工作量。
- 当前`dvp_plan`的预测后继资格使用node_actor，旧版使用原actor。决策语义亦有变化。
- 不能笼统说旧76秒来自删除所有证据快照。旧`dvs_event`仍对整条事件rt_snapshot，旧trace.json实际为4,579,254,874字节。当前同样冻结事件，并新增rt_raw_event(frozen_event)原始事件输出；当前证据输出模式也增加DAG与分阶段计时。未整体读取4.5GB trace，也未还原旧工具，不能量化不同序列化方案对耗时的贡献。
- 在本次定点函数比对中，没有发现足以据此宣称“旧版所有预算耗尽自动等待、当前取消”的差异。保留未知；旧debug_end中侦探model_budget=0，但这不是全体主体无额度，也不是world-horizon的替代停止原因。等待、预算与全部新内容的净耗时影响未测量。

## 来源与限制

只读检查了历史result、stdout、逐行解析的events、source-hashes和封存session.dv，并与当前相关函数比较；没有启动任何会话、构建或旧版本。source-hashes记录的session、章节、入口、编译器Runtime及run_debug等多处指纹与当前不一致，旧源码文件重组也存在。因此本轮窗口与旧76秒不是同条件比较，不能据此归因当前性能退化或声称性能无退化。历史result自报build_source_consistent/source_unchanged；本次未重编译验证这两个历史标志。

本复核确认的是已有一级逻辑执行证据的事实与限制；不涉及Godot几何、碰撞、输入、画面或三级验收。
