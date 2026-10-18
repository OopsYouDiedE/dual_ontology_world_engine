# 唯一 240 秒正式一级窗口独立审查

本审查仅读取本轮 frozen/standard-yi 原始证据、源码和投影；未读取设计文档，未启动或恢复会话、未构建。formal-model 不含 Godot 几何、输入或画面。源码静态审查与本次实际证据分别判断。

## 实际范围与停止

result.json 记录新开局（resumed=false）、240 秒 logical-session 超时，实际计时 240.578 秒；构建时源码一致、运行期间源码未变。未观察到 debug_end、最终 checkpoint 或 seal。原 DAG 254,905,851 字节全部落在有效提交前缀内、没有残缺尾字节，但这不等于完整会话封存。evidence_complete=false 符合证据；五日时域没有到达，不能用超时推断以后无法到达。

独立读取 raw-event-index 的末事件为 e20554：quer 的 observation，游戏时间3309（第3日07:09）；其前实际 host_completion=e20552、action_execution=e20553 也已提交。stdout 仅投影到 e20527@3308，cursor=20528；其后27个原始事件尚未进入调试投影。玩家正文末段仅覆盖本人已flush的信息，末段为侦探第3日06:09实测；不将其他主体07:09事件冒充侦探已经看见的正文。

正文尾部实际出现180分钟等候与仪器实测交替（第3日00:06/00:07、03:07/03:08、06:08/06:09）。本轮时间仍消耗在实际等待和重复检视上；该观察不能直接解释其合理性或证明改进。人物合理性与剧情达成需主代理依据设计另判。

## 真实阶段 checkpoint

raw summary 的字段是 checkpoint_commits；正式 result 对应 stage_checkpoints。stage-day3 已提交于 frame621205、root601295，保存事件范围19907，并非缺失。

首次使用冻结版本 debug_checkpoints.checkpoint_metadata 只读验证实际存档时发生 `ValueError: No field 'pending'`。正式 Complete 后合法状态移除 pending 键；冻结工具却要求该键必须存在，因此该版本显式导出/恢复会拒绝本次合法存档。这是实际发现的工具缺陷，不是游戏会话失败。

主代理随后只修 optional pending 读取。用修正后工具再次只读检查原始 DAG，真实结果为：before_time2733 → game_time2881跨过2880边界；steps/executed1776；action1775由veronica执行，host e19904 → execution e19905，status=applied，最后事件e19906；saved event extent与有效提交范围相同。证明阶段存档来自实际执行后的状态，不能把它标为未产生。

此后修复改变了 debug_checkpoints.py 的源码指纹。原冻结证据、源码与结果保留；严格源码合同没有放宽，当前工作区不能据此声称已直接恢复旧 checkpoint。未展开完整 tagged Save、未运行恢复诊断。

## idle 守卫证据

原始事件索引没有 planning_stop_reused，原 DAG文本没有 idle_guard 键；stage-day3保存的主体也没有该字段。此次实际前缀未观察到发证或复用，不能以静态守卫或中性工程探针宣称正式窗口已获空闲跳过收益。

## 限制与结论

本轮未实现五日覆盖或完整封存；存在合法 checkpoint 被冻结验证工具拒绝的具体缺陷，事后工具读取修复已使原始阶段元信息可独立核对。目标声明复用不等于完整 Goal Refresh Guard。没有同条件对照，不能给出提速结论。剧情完成、人物合理性、Godot UI和三级游戏体验均不由本次审查证明。
