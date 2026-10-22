# 独立激进重写开关接线

基线7703d634；分支codex/aggressive-flags。仅工程实现与有限工具检查，未读取设计文件，未启动正式/全局会话、Godot或Core AI。

新增默认开启的独立`--opt-taskgraph on|off`、`--opt-storyflow on|off`、`--opt-stateindex on|off`。统一FLAGS现在含21项，run_debug参数、完整运行配置、配置恢复核对、全关基线和独立消融共用该名单。60保存、布尔校验并将三个同名字段传入全部actor.policy。原生DvCli同样接收三项；新开局配置传入正式HostOpen；回放显式给这些参数会拒绝，恢复显式覆盖taskgraph/storyflow也拒绝，不静默忽略。stateindex恢复可设置环境，但须匹配保存配置。

stateindex由astral分支实现Runtime/20真实消费者；本分支负责DV_OPT_STATEINDEX环境、65 fresh/resume实际值核对、原生Host会话构造器实际值核对及运行时来源记录。独立ValueContext创建时采样。复用程序v2合同现在要求runtimespeed/stateindex两项独立布尔捕获配置，源/生成物/程序集完整指纹不变；两项均可在同一编译二进制上用不同配置启动。旧v2缺项会拒绝而不是补写历史证明。run_debug使用恢复后的run_config设置实际环境，顺带消除了恢复时runtimespeed仍误用默认参数的分歧。

SessionEvidence保留三个开关与taskgraph策略近似/storyflow内容重写的来源标识。工程文档区分taskgraph本人策略近似、storyflow内容重写和stateindex存储优化；相似效果不冒称轨迹等价，也不允许预置答案、伤病、死亡或他人同意。该分支不修改30/50消费者。

有限验证：`python -m unittest discover -s mcp/verification -p test_tool_hardening.py`完成，既有来源复用/复制后校验/参数名单机制检查未报错；更新双运行时开关捕获夹具。`run_debug.py --help`实际列出三项；`--opt-taskgraph invalid`在参数阶段明确拒绝，未启动会话。`git diff --check`未报空白错误。未执行Release或DV全闭包检查：本分支65/Host新增消费者依赖并行astral的`rt_stateindex_enabled`/`R.stateindex_enabled`，应在主线合入消费者后统一工程排错；本报告不声称C#已构建或人物行为已验收。

合并依赖：taskgraph由30实现`actor.policy.opt_taskgraph`，storyflow由50实现`actor.policy.opt_storyflow`；stateindex由astral的10/Runtime/20实现。本提交仅完成入口、配置、证据与合同接线。
