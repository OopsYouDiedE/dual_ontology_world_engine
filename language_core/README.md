# DuelView（DV）：独立语言 Core

语言名称为 **DuelView**，简称 **DV**，源码后缀为 **`.dv`**。新目录中的可运行原型位于分支 `codex/partial-bootstrap-core`，不引用旧 Core。最终目标是**编译器与全部引擎推演核心均以 DV 编写，并由 DV 编译器编译成 C#；其他目标语言通过后端扩展**。

[DESIGN.md](DESIGN.md) 给出语言结构、类型化 AST、解析树、后端与编译器自举链。该文件中的正式类型化语法是待实现设计；当前已新增可自编译的通用计算启动子集，见 [compiler/README.md](compiler/README.md)。完整静态类型、能力系统、目标无关 IR 和推演库编译迁移仍待实现；不是旧 Core 的完整替换。

2026-10-13 的[易读表层语法设计](DESIGN.md#12-易读表层语法2026-10-13)采用 `defn name(x: Type) -> Type { ... }`、普通调用和中缀运算，公理以 `when:` / `effects:` 具名段落书写；AST 保留在编译器内部。本轮只完成语法设计和迁移合同，现有可执行 `.dv` 仍采用 S 表达式，新示例尚不能编译。

同日加入文件头 `define 别名 原名` 设计，例如 `define 返回 return`、`define 输出 rt_print`，以单 token 映射支持中文编程；仅当前文件生效，保留字符串/注释和语义，首版无递归宏。完整规则与中文函数示例见 DESIGN 12.8，当前尚未实现。

## 可编译的世界语义库

新增 [core/README.md](core/README.md)：公理分域采纳、物体/门/通道与切片、观察更新、主客观价值、心理评价式回应、论证代价和历史检查器。实现采用 DV 编译子集；尚未接入现役第一章。当前已获准在隔离 DV 会话中实现并运行自动验证，具体证据范围见 verification README。

## 自举的实际含义

正式会话自动验证的目标合同见 [verification/DESIGN.md](verification/DESIGN.md)，当前实现与运行方式见 [verification/README.md](verification/README.md)。用户已授权实际实现：会话和四个只读入口使用编译 DV，Python 只编排冻结、编译与文件/进程 I/O。具体执行证据和限制保存在 `artifacts/partial-bootstrap-core/`；不能宣称 Godot 接入、旧 Core 等价或游戏验收完成。

用户随后明确本轮人物、世界、话语、规则、参数及 oracle 均为真实 DV 声明/函数，JSON 仅由独立 DV 程序导出作运行交换。oracle 与主体包分离；旧 `.story` 只读作迁移溯源。此裁定覆盖本目录旧内容装载规定，未修改现役宿主的剧本链路。

启动 C# 内核读取 S 表达式，载入 `10-evaluator.dv`。这个语言写的解释器再解释全部 `bootstrap/*.dv`，其中包括解释器自己的源码。所得函数在新环境中运行；`InferenceProgram` 和正式 `WorldCore` 调用这些自解释载入的函数。演示还取出新载入的解释器函数并实际调用它。

没有提供 native `eval`、案例求解、规则打分、信念更新或规划 primitive。语言代码实际执行这些算法。基本数值、列表、函数调用、不透明函数包装、局部 cell 和预算仍由 .NET 提供。Reader、内存管理和宿主执行边界也保留在 C# 中，这描述的是原解释链路。新增编译链路的扫描、解析、名称/调用检查和 C# 生成已全部放入 `compiler/compiler.dv`，能够编译自身；其数据与平台 ABI 仍由 C# 提供。完整语言自托管目标尚未完成。

成熟参考采用 [MAL 的 C# 与自身语言实现](https://github.com/kanaka/mal/tree/2bbfaa54cca4908efc90b4173b1406e260788e8a/impls)：词法环境、quote、闭包、尾调用与自解释载入。源码是本目录的新实现，并未复制或引入整套 MAL；不宣称完整 MAL/Scheme 兼容。参考与语义依据见 [REFERENCES.md](REFERENCES.md)。

## 推演实现在哪里

| 文件 | 语言自身实现 |
| --- | --- |
| `00-base.dv` | 列表、关联表、三值逻辑与基础工具 |
| `10-evaluator.dv` | 词法环境、定义、绑定、条件、闭包、调用与模块求值 |
| `20-world.dv` | 类型形状校验、主体条件求值、规则模式、禁止/许可、推知建议与纯阶段准备 |
| `30-logic.dv` | 带 occurs check 的一阶合一和有界 ground Horn 推导 |
| `40-belief.dv` | 候选过滤、最少冲突保留、来源/信任折扣、归一化与边缘置信度；显式似然更新；本人持有的他人心智模型 |
| `45-deduction.dv` | 已接受 Infer 规则的有界条件推导、支持/否认冲突、显式假设、来源和停止原因 |
| `50-planning.dv` | 共同有界行动搜索、完整层预算、私有预测、奖励身份、循环截断、社交与尝试记忆 |

`WorldCore` 提供类型化的 `Evaluate`、`Assess`、`Infer`、`Prove`、`Reason`、`Plan` 与 `Prepare/Commit`。前述推演函数在自解释载入的语言模块中执行；C# 只组织和验证输入输出。`ReadAuthority`、`Define`、`Observe` 等是受信任宿主 API，不能把整个 WorldCore 对象当作不可信主体的沙箱接口。

## 认识与概率的边界

求值固定一个不可变主体快照与版本。未知观察保留 `unknown`，否定未知仍未知。权威实体 ID 不出现在主体快照；普通执行不会把权威状态自动纠正到主体认知中。`mental-evaluate` 遍历的是本人显式持有、注明来源或假设性质的模型，不能获取别人的真实心智。这个功能是心智建模；解释器/推断器由自身语言实现是自举，两者分开验证。

错误信念用信念模型理解，不强行采用有事实性公理的知识算子。本目录没有宣称已经实现完整 Kripke/S5 语义、Jøsang 意见演算、Dempster–Shafer 融合或概率半环证明。

现有 `Reasoner` 的兼容候选与证词因子属于启发式证据权重，本实现保留其计算方式；Defaults 是尚未想到问题时的默认答案，并非一般先验分布。`bayes-update` 则要求调用者显式提供分布和似然；相关性与依赖假设仍须由模型定义。规则偏好分、证据权重与概率分别使用，不把一个 Rule.Weight 同时解释为三者。

## 运行

需要 .NET 8；记录证据另外需要 Python 3。没有第三方包依赖。

```powershell
dotnet build language_core/LanguageCore.sln -c Release --disable-build-servers
dotnet run --project language_core/Cli/Language.Cli.csproj -c Release -- demo
dotnet run --project language_core/Cli/Language.Cli.csproj -c Release -- eval "(+ 20 22)"
dotnet run --project language_core/Cli/Language.Cli.csproj -c Release -- self "(+ 20 22)"
dotnet run --project language_core/Cli/Language.Cli.csproj -c Release -- run language_core/examples/subjective-inference.dv
```

支持 `def!`、`let*`、`if`、`do`、`fn*`、`quote` 和 `'`；参数用圆括号，不支持 MAL 的 vector、宏、quasiquote 或异常捕获。函数类型与身份在 seed 和自解释环境中均不透明。

基础 seed 默认限制 2,000,000 求值步骤与 256 嵌套层；自解释正式调用声明 100,000,000 步骤上限。Reader 限制源大小 1 MiB、结构深度 128。推导深度与展开预算是另外的语义参数，不用宿主指令数量冒充搜索深度。自解释有明显开销；没有游戏性能证据。

规划的 expansions 计展开的父状态：共享初始状态计一次，每个后续 frontier 状态亦计一次；一个状态下生成的候选边数另受程序指令上限限制，不是 expansions 的计数口径。后续层预算不足时整层停止。Depth 为选中路径长度，ComparedDepth 为已完成比较层；PredictionFailures 单独保留未知状态/条件、资源与循环等未展开原因。未知 when 不当作 false，不凭该分支保留值推导确定后继。

真实事务将多个观察别名映射到同一资源状态后进行阶段演算，主体快照仍保留各自观察。不可达对象的 exists guard 在读取结果前按访问边界拒绝；同一不可达位置的存在/不存在对象不通过不同失败原因泄漏。

## 与旧 Core 的完整迁移清单

| 能力 | 本轮实际边界 / 还需完成 |
| --- | --- |
| Condition 与 Rule | 条件、三值与五种模式已有语言实现；当前按调用者固定的槽位上下文求值。旧 Core 跨全部已知目标/工具的候选枚举和社交请求接线尚未迁入 |
| CaseModel / Reasoner | 候选过滤、默认答案、最少冲突、来源因子与边缘置信度已有语言实现和类型化入口；还需覆盖所有真实模型与空/冲突输入的完整行为差异 |
| SubjectiveReasoner | 有界推导、显式假设、矛盾与来源已有语言实现；旧能力的信息价值、假说组合、限定证据集与完整 derivation 格式尚未对齐 |
| SubjectiveActionPlanner | 语言实现包含多步预测、奖励回传、完整层与循环处理；当前奖励身份是动作结构。旧目标/信息独立收益账本、代价、上下文转移与完整停止细节尚未对齐 |
| SubjectiveDecision / ActionLearning | 语言已有尝试记忆与按语义上下文的重试成本；旧学习器的全套调整、存档 wire 与决策适配尚未迁入。演示常数不是新游戏设计裁定 |
| SocialSystem / Speech | 通用社交分数可用本语言表达；请求实例、传播、接受、信任经历和言语事件全合同尚未迁入 |
| 行动执行 | 仅 set/change/spend/relate/require/when/seq；两阶段提交检查权威资源、初始可达性、主体规则与版本。wait、临时租约、各物理 act、say、think、CTB、活动取消和保存明确未实现 |
| 认知与跨进程保存 | 新实现有主体本地身份、闭合观察批次、不可变版本快照；旧审计历史、遗忘、词项消歧、所有事件/能力版本与保存合同尚未迁入 |
| 正式游戏 | 未接入 Godot、剧本编译器与现役章节；一级、二级、三级验收均未执行 |

因此“旧 Core 已完整重写”目前仍不能成立。最终完成须逐项补齐语言实现与反例对齐，确认所有推演核心不依赖隐藏 C# 算法，再做宿主接入和被明确要求的正式验收。不能以解释器能运行一个程序或构建成功替代这个结束条件。

本轮证据和独立审查保存在 [artifacts/partial-bootstrap-core](../artifacts/partial-bootstrap-core/)。
