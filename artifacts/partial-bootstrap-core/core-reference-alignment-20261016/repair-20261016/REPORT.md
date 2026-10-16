# 统一修订与唯一一级测试结果（2026-10-16）

本轮按用户要求先完成整批源码、声明、文档修改和静态审读，再统一构建、执行一次一级测试。宿主解决方案与正式逻辑程序均完成编译；唯一会话随后因 `Expected Text` 异常退出，没有形成游戏行动或玩家正文。本轮未完成一级剧情与机制验收，不能认定全部问题已修复。测试后只封存证据、审查和报告，未修改源码、未重跑。

## 修改范围与设计边界

设计规范首部明确：关键节点是自然模拟的设计目标，达成须来自主体实际信息、动机、选择、执行与后果；不预置答案、代写他人选择或强制写成果。根 AGENTS 增加整批修改后统一测试及因果链核验要求。Core README 与模块 AGENTS/DESIGN 同步实际资格、规范收据和成功执行账本合同。

Sol 6.1 子代理分工完成记录形状、广播与跨区接线、本人资格/共鸣评分、第二章议事与旧疤归属、规范实际消费链及独立静态审读；主代理负责设计对照、集成和测试。Gemini 所称部分门槛“未接入”不符合当前源码：主动共鸣、夜呼与矛盾门槛已经存在，本轮补充评分、实际来源与依赖签名，而非重复创建另一条决策路径。

- 初始化递归先判记录形状，保留键并递归值，普通列表逐元素处理；同时修复恶魔政策遗漏的旧地点前提。广播首段等真实茶叙收闻，后续广播等前段成功Complete；跨区由仍在乙区的三人真实交流，不借缺席侦探的动作解锁。原四杯送茶只改变杯中内容，不替缺席人物移动或饮茶。
- 自主答辩统一公开声明；矛盾反应核对实际来源。主动共鸣效用进入候选评分与依赖，夜呼沿本人实际资格执行。后述数值ABI错误使这一新增依赖路径尚未跑通。
- 规范板读/听闻先形成内容收据，独立个人价值选择及成功Complete后才更新本人采纳。观察、理解与真实执行事件分别保存；共鸣和玩家影响后果也使用真实执行事件编号。
- 左臂旧疤动作及其生成目标、法则归属迁至维罗妮卡。议事赴会不要求预先站队，实际获知提案后可赴会；选票与签名逐人执行，不以全员同时在场限制每张票；章末仍要求全十六人真实签署，死亡不当作签名。
- 清理本轮现役DV与上一轮相关行尾格式；历史日志和快照保留原字节。新增只读debug停止事实，区分配额、工程窗口与时域；本轮异常发生于debug_end之前，未产生该末态输出。

预算扩额建议未直接采用。源码明确侦探48动作来自原内容约束，其余人物配额含工程限制；全局上限由各人配额求和，不能把附件估算当作当前值。保持行动/认知资源及逐次搜索上限，不重置余额或靠扩额保证五日成功；其对完整五日的实际限制尚无本轮行为证据。详见 [源码审读](source-review.md)、[静态审计](static-audit.md)与[测试前关键节点](key-nodes.md)。

## 唯一一级会话与错误

命令：

```powershell
dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers -p:NuGetAudit=false
python language_core/tools/run_debug.py --out artifacts/partial-bootstrap-core/core-reference-alignment-20261016/repair-20261016/level1-once --timeout 600
```

[宿主构建日志](build.log)与[逻辑程序构建日志](level1-once/build-1.txt)记录编译完成；它们不是游戏验收证据。formal-model 运行约2.657现实秒，以exit_code=1退出，未到600秒工程超时。

原始 [stderr](level1-once/stderr.txt) 为 `System.Exception: Expected Text`：`R.Text → R.is_number → dvp_owner_requirement_refs → dvp_dependency_methods → dvp_cached_dependencies → dvp_plan → dvs_choose → dvs_session_prepare`。

定位：`verification/session/16-personal-choice.dv:276` 的新增依赖收集用 `rt_is_number(quantity)` 判定运行时值类型；`compiler/Runtime.cs:19` 的该ABI实际先调用 Text，再判断字符串能否解析为数。它不是通用数值类型谓词，传入声明记录等非文本会抛错。原有及本轮数量读取中的同类调用也需下一轮一起审查；当前栈只证明依赖收集处实际触发，不宣称其他调用已执行。本轮新增代码引入了这一可达错误，静态审读和编译没有识别其ABI语义错配。

[stdout](level1-once/stdout.jsonl) 已输出 debug_start，记录08:00（480分）的恶魔、侦探认知/选择阶段，以及霍铎choice-start；此后异常。上次初始化错误位置已经越过，但这些内部阶段不等于实际行动或剧情进展。

没有 player_presentation、debug_events批次、debug_end或完整trace.json。events.jsonl只有调试开始和内部阶段数据；玩家可见文本仅导出说明行，调试轨迹正文为空。result.json中的output_well_formed/text_exported只说明已输出包可解析、导出文件已写出，不能代表游戏输出完整。

## 关键节点与未验证范围

本轮没有输出可核验的行动Complete、检视回执、答辩接受、广播正文、共鸣后果或章节交接。测试前关键节点表中的节点均未在本次输出窗口出现；完整内部轨迹因异常未保存，对未导出的内部计算保留记录不足，不补猜结果、时刻或动机。没有证据评价本轮重复劳动、停滞改善、人物合理性或五日可达性，也不推断整局必定失败。

一级已实际启动但未完成。二级、三级未执行；跨区侦探配置和Foolish_Detective未运行。Godot画面、碰撞、输入与几何均未验证。本轮唯一会话未证明关键节点自然达成。

## 证据与工作区

[运行结果](level1-once/result.json)、[源指纹](level1-once/source-hashes.json)、[独立审查](independent-review.md)、[冻结文件核对](post-run-integrity.json)与原始输出均新增保存。source-snapshot保留本次运行输入的原始字节，未覆盖上轮证据。静态git diff --check未报告格式错误，日志见static-diff-check.log，不能据此宣称行为正确。

起点为e0d9e143。用户已有music/Blessed Sleepers - Sleep.mscz修改保持原样，不纳入本轮提交。
