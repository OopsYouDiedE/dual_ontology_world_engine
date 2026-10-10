# Core 行动经历独立验证

最终 Core 构建、新行动经历回归与相关既有回归通过。已复现的数值边界和经历历史二次增长问题由主代理修补，追加回归确认修补有效；高频评估开销与并发读取边界仍需单独评估。

本报告对应 `SubjectiveDecision.cs` SHA256 `5159e747c05042287a9761151513ee81f85c959273d57806ac048d2342824bbe`。修补前报告保留为 `report-before-api-repair.md`，修补前哈希索引保留为 `hashes-before-api-repair.json`，不覆盖旧证据。

## 范围与隔离

验证由新独立上下文执行，仅读取 AGENTS 约束、通用 Core 源码和中性夹具。没有读取设计规范、正式剧本、历史游玩记录或历史报告。新入口 `csharp/SubjectiveDecisionRegression.csproj` 使用独立 `obj/SubjectiveDecisionRegression/` 与 `bin/SubjectiveDecisionRegression/`；测试源由主代理排除在 Core 类库之外。

本代理只新增/修订中性回归和本目录证据；生产修补、Core 项目排除及维护文档由主代理处理。没有运行 Godot、FullTest、UI 或盲玩，没有提交推送。构建与回归串行执行，追加构建开始前等待宿主验证释放资源，结束后明确通知宿主验证继续。

## 最终结果

| 项目 | 结论 | 最终证据 |
| --- | --- | --- |
| Core Release 构建 | 通过 | `13-core-build-api-repaired.log` |
| SubjectiveDecisionRegression | 通过 | `14-decision-build-api-repaired.log`、`15-decision-api-repaired.log` |
| SubjectiveReasoningRegression | 通过 | `16-reasoning-build-api-repaired.log`、`17-reasoning-api-repaired.log` |
| CognitionRegression | 通过 | `18-cognition-build-api-repaired.log`、`19-cognition-api-repaired.log` |
| 补充 API/经历增长探针 | 完成，见下方数据 | `20-api-probe-repaired.log` |

新回归使用平层和嵌套中性世界，得到以下机制结论：

- 经历保留在本人认知中，不影响另一个主体。尝试、失败、无进展、成功、中断独立计数；成功和中断不会作为失败处罚。
- 同情境失败后可以改选另一候选。未知对象、没有新增状态的观察、未观察的权威变化和其他主体的观察都不解除本人失败；本人相关目标标签更新允许新情境重试，并保留旧失败记录。
- 实际 SimulationSession 的成功、FailedEffect 和取消回执分别学习；失败阶段没有提交后续效果。几何导致的 NoProgress 仅验证其经历分类与评分，未运行真实 Godot 碰撞。
- ActionLearning 的公开 Capture 合并每个方法/情境的认知节点；反复更新同一情境不新增节点。普通认知 wire、图重绑定与运行时保存恢复均保留所有结果、评分与主体句柄，恢复不重放观察。
- 禁令与 Permit 门槛不被评分绕过，浅层和深层的角色预算差异保留，评估不回写认知。
- DTO/wire 拒绝未知版本、重复经历、负数/超限计数、未知结果、未知字段、重复 JSON 属性、缺失必需字段、BOM 和非法 UTF8；跨存储节点的重复经历键同样拒绝。
- 规则、信息、成本、重访与失败项按独立预期组合；计数饱和安全。大方向权重饱和到 int 上限，finite 极值距离保持非负结果，负数/超限信息权重拒绝，小数分数能够触发重评。

`wire-final/` 保存最终输入、认知 wire 与运行时存档；`wire/` 和 `wire-retry/` 保存之前轮次的独立输入。每次命令的 `*.manifest.json` 记录命令、退出状态、耗时、原始日志 SHA256 与 Core 根层源码/项目运行前后哈希。最终命令执行期间源码没有变化。`hashes-final.json` 是最终证据索引。

## 失败与修补证据

`01-core-build.log` 保留新增生产文件使用 `when` 作为 switch 变量的语法失败；主代理改名后 `02-core-build-retry.log` 通过。

`04-decision-regression.log` 保留夹具自身失败：错误要求 Ast.Bind 返回同一效果引用，并使用默认 JSON 编码带 Tag 键的快照。仅把测试改为 Ast.Equivalent 与正式 CognitionCodec 字节比较，`06-decision-regression-retry.log` 通过。实际结果、提交、只读和保存断言均保留。

`12-api-probe.log` 保留公开数值边界问题和历史增长：大权重及 finite 极值距离曾返回 `-2147483648`；负信息权重曾返回 `-1`；整份记忆反复写入历史导致二次增长。修补后的 `15-decision-api-repaired.log` 包含对应断言，`20-api-probe-repaired.log` 记录大权重结果 `2147483647`、finite 极值结果 `8`、负数/超限信息权重拒绝。

日志通过二进制捕获保留工具原始输出，局部 `.gitattributes` 对日志使用 `-text -diff`，对 wire 关闭换行转换。失败输出和修补前报告未覆盖。

## 性能实证与边界

每次经历观察现在只保存当前方法/情境的一条 wire。历史 Details 随新情境近似线性增长；补充探针没有测量正式场景帧率或长时游玩。

| 中性经历条目 | 修补前保留 Details 字节 | 修补后保留 Details 字节 | 修补后末份 Details 字节 |
| --- | ---: | ---: | ---: |
| 32 | 80909 | 5782 | 181 |
| 64 | 317421 | 11574 | 181 |
| 128 | 1257795 | 23186 | 182 |

| 中性经历条目 | 修补前平均 Assess 毫秒 | 修补后平均 Assess 毫秒 | 修补前平均线程分配字节 | 修补后平均线程分配字节 |
| --- | ---: | ---: | ---: | ---: |
| 32 | 1.11 | 2.05 | 389977 | 759787 |
| 64 | 1.53 | 2.93 | 651116 | 1387001 |
| 128 | 2.76 | 6.41 | 1180163 | 2673888 |

时间受本机与 JIT 影响，不构成正式游戏性能阈值。单条存储减少了历史体积，但逐节点严格 Decode 和推理图重绑定增加了评估分配；不建议仅凭本轮通过宣称可高频调用。高频/多主体/长历史负载尚未验证。

其余静态边界：Assess 分别读取原 DecisionSystem 的快照评估、live 经历、第二次快照和 live Context，尚未证明并发认知更新时整次结果同版。Context 哈希存在、父节点、标签和正关系，没有包括拒绝关系、规则或 Procedure；本轮证明目标标签变化允许重试，未证明所有影响效果的认知变化都能重试。未把这些静态风险判作已经实测的机制失败。

## 验收结论

本轮为中性 Core 自动回归，构建与所列机制通过。一级剧情/引擎/UI全程对齐、二级 UI 专项、三级游戏本体盲玩均未执行。人物行为合理性由主代理按设计核对；剧情、UI、几何、宿主接线、游玩体验和有效时长未验证。Core 回归通过不能代替这些验收。
