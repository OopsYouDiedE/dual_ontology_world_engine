# Core 行动经历独立验证：修补前封存

本报告对应 `SubjectiveDecision.cs` SHA256 `38644c404e7bd6c7f8487765a2628cef00e7905d7179d4dc3556043ce5be7d10`。主回归通过；公开数值边界和经历历史增长存在已复现缺口，等待主代理修补。本报告封存后不覆盖。

验证上下文独立于主代理。只读取 AGENTS 约束、通用 Core 源码和中性夹具；没有读取设计规范、正式剧本、历史游玩记录或历史报告。新增独立 `SubjectiveDecisionRegression.csproj` 与中性测试源，测试输出和中间产物各自隔离，测试源由主代理排除在 Core 类库之外。本代理未修改生产文件、未提交推送，未运行 Godot 或 FullTest。

## 构建和机制结果

| 项目 | 结论 | 本轮证据 |
| --- | --- | --- |
| Core Release 构建 | 通过 | `02-core-build-retry.log` |
| 新 SubjectiveDecisionRegression | 通过 | `06-decision-regression-retry.log` |
| 既有 SubjectiveReasoningRegression | 通过 | `08-reasoning-regression.log` |
| 既有 CognitionRegression | 通过 | `10-cognition-regression.log` |

新回归在平层、嵌套空间的中性世界中验证：本人经历不传播到其他主体；尝试、失败、无进展、成功与中断分开；同情境失败后替代动作可以胜出；未知对象、无新增状态的观察、未观察的权威变化、其他主体的观察均不解除本人失败；本人相关目标状态更新允许新情境重试，同时保留旧失败。真实 SimulationSession 的成功、实际 FailedEffect 和取消回执分别记录，失败阶段没有提交后续效果。

普通认知 codec、图重绑定与运行时保存恢复均保持经历、动作评分和主体句柄；恢复没有重放观察。既有禁令、Permit 的权重门槛以及浅层/深层角色预算差异保持。新增 DTO 与 wire 拒绝未知版本、重复经历、负数或超限计数、未知结果、未知字段、重复 JSON 属性、缺失必需字段、BOM 和非法 UTF8。评分组合与计数饱和也已验证。

`wire/` 保存初跑输入，`wire-retry/` 保存通过轮次输入和存档。每次命令的 `*.manifest.json` 记录命令、退出状态、耗时、原始日志哈希和全部根层 Core 源码/项目文件运行前后的哈希。运行过程中源码哈希均保持不变。

## 保留的失败

- `01-core-build.log`：生产文件 switch 分支使用 `when` 作为变量名，引起语法错误。主代理改名后 `02-core-build-retry.log` 通过。
- `04-decision-regression.log`：新夹具要求绑定后的效果与原效果引用相同，并使用默认 JSON 序列化带 Tag 键的快照。既有 Ast.Bind 会构造等价效果副本，默认 JSON 不支持该字典键。仅修正测试为 Ast.Equivalent 与 CognitionCodec 字节比较后，`06-decision-regression-retry.log` 通过。没有放宽动作结果、提交、认知只读或保存断言。

失败原始日志不覆盖。日志通过二进制捕获保留工具输出字节；局部 `.gitattributes` 关闭日志换行转换和文本 diff。

## API 与性能边界

`12-api-probe.log` 是单独补充探针，不是完整游戏性能结论：

- `DirectionValue(0, 0, 10, int.MaxValue)` 返回 `-2147483648`，非负权重产生负分。
- `DirectionValue(-double.MaxValue, -double.MaxValue, double.MaxValue, 4)` 的参数全部 finite，但差值溢出后同样返回 `-2147483648`。
- `InformationValue(Unknown, false, -1)` 返回 `-1`，没有拒绝负权重。
- 每次 Observe 把整份经历 JSON 写成新的可见历史 Details，导致 retained Details 总量随新情境经历二次增长。

| 中性经历条目 | 末份 Details 字节 | 保留 Details 总字节 | 平均 Assess 毫秒 | 平均 Assess 线程分配字节 |
| --- | ---: | ---: | ---: | ---: |
| 32 | 4883 | 80909 | 1.11 | 389977 |
| 64 | 9747 | 317421 | 1.53 | 651116 |
| 128 | 19503 | 1257795 | 2.76 | 1180163 |

数值测量依赖本机、JIT 和夹具，不能推断正式场景帧率。历史字节随条目倍增接近四倍，是原始数据的直接结论。

静态边界尚未实测：ActionLearning.Assess 先取原 DecisionSystem 的快照评估，再读取 live 经历、另一份快照和 live Context，不能保证并发认知更新时整次结果都来自同一版本；没有把这一并发风险判作已复现的当前机制失败。Context 只哈希存在、父节点、标签和正关系，没有包括拒绝关系、规则或 Procedure；本轮仅验证目标标签变化重试，未证明所有可能影响实际效果的认知变化都能重试。每次 Assess 会重绑定主体推理图、解析全部经历，未覆盖大规模完整游戏长时负载。

## 验收边界

本轮是中性 Core 自动回归，不属于一级三份剧情/UI全程对齐、二级 UI 专项或三级独立盲玩。构建通过，所列机制通过；人物行为合理性需主代理按设计核对，剧情、UI、碰撞、宿主接线、游玩体验和有效时长均未验证。公开数值边界和历史增长缺口尚未修补，不以主回归通过替代这些结论。
