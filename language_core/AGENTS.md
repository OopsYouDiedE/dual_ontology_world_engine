# 独立语言 Core 的维护规则

本目录继承根 AGENTS.md 的流程与验证约束。它是隔离的语言重写目录，不能引用 `csharp/`、Godot 或章节程序集。现役游戏仍使用原 Core；不得以本目录的机制输出宣称游戏接入或旧 API 等价。

语言名称固定为 DuelView（DV），源码后缀 `.dv`。目标结构与 AST 合同见 DESIGN.md；其中待实现的正式语法、编译器和后端不能当作当前能力。所有新实现仍放在本目录。

目标分层借鉴外层 elaboration 与小语义核：世界法则、主体接受的推演规则与优先策略使用不同的声明/环境合同，策略评分不得取得命题真理或行动合法性的效力。语义核与编译器最终同样用 DV 实现；依据轨迹、概率更新与行动校验分开记录，不将它们统称为形式化证明。

## 模块边界

- `core/*.dv` 是可编译的领域语义库：公理采纳、物体切片、观察、传播、价值、回应、论证代价与检查点查询均用 DV 实现。axiom 声明在 DV 前端展开；不得把领域算法移到 Runtime.cs。JSON 编解码和参数计数仅服务平台 I/O。
- `core/` 世界与检查点按返回副本维护；主体输入、客观提交和只读检查分开。动态数据函数仍属于可信调用协议，不能称为静态能力安全边界。人物参数和章节实例仍从 .story 装载。

- `compiler/compiler.dv` 是编译器启动子集：扫描、AST、名称/作用域/元数检查和 C# 后端均用 DV 实现。`compiler/seed/` 的 C# C0 只负责启动；生成 C1/C2/C3 仅使用 `compiler/Runtime.cs` 的数据与平台 ABI，不引用解释器。当前子集采用运行时值域检查，尚无静态类型/能力系统和目标无关 IR，不能与 DESIGN 的完整语言混称。原解释语法尚未统一到编译语法。

- `Kernel/Value.cs`、`Reader.cs`、`Interpreter.cs` 为启动内核：基本数据、词法/列表读入、词法作用域、函数调用和资源预算。
- `bootstrap/*.dv` 为语言自身实现的解释器、条件语义、规则评价、推理、信念更新、规划、社交与学习。新增推演能力在此实现；不得在 native table 中加入决策、推理、案例求解或概率推断算法。
- `InferenceProgram` 用语言解释器载入上述模块；正式 `WorldCore` 的推演调用经此实例执行，不调用旧 Core。
- `Terms.cs` 与 `InferenceModels.cs` 是宿主类型化数据合同与编解码边界，不负责求解。
- `WorldCore.cs` 管理宿主权威身份、观察提交、不可变主体快照、控制/可达性与事务提交。主体求值不传入权威对象。未提交的 prepared token 同时绑定世界与认知版本，提交一次后不能复用。
- 事务边界为每个实体建立唯一阶段资源，语言侧 canonical-nouns 将已绑定引用规范到该资源；主体的各观察 facet 不合并成权威知识。访问核验在对外报告真实条件/存在性失败前完成。预测未知条件不能当作 false；规划结果分列被拒预测、所选路径深度与完成比较层，状态展开预算包含初始状态。
- `Cli/` 与 `examples/` 仅提供通用语言机制证据。不得加入章节内容、具体人物、谜题或预设答案。

## 工程验证

```powershell
dotnet build language_core/LanguageCore.sln -c Release --disable-build-servers
dotnet run --project language_core/Cli/Language.Cli.csproj -c Release -- demo
python language_core/tools/record_mechanisms.py --out artifacts/partial-bootstrap-core/engineering-<new-run>
python language_core/tools/build_core.py --out artifacts/partial-bootstrap-core/domain-build-<new-run>
python language_core/tools/bootstrap_compiler.py --out artifacts/partial-bootstrap-core/compiler-<new-run>
```

记录工具只允许新增证据目录，先存源码指纹；构建失败时不继续依赖该构建的机制演示。stdout/stderr 保留原始字节，结束后再次核对源码。它不调用游戏、统一回归或 flow 系列入口。

编译器记录工具另外保存原始源码归档、工具链信息、每阶段生成源码和程序集指纹；核对 C2/C3 目标源码固定点、生成项目依赖、独立程序实际运行和编译器源码变体对新生成结果的影响。后端修改后必须从 DV 重新生成，不手改目标文件。固定点不等于编译正确性证明；完整静态类型、能力、IR 与推演库编译迁移须单独记录缺口。

每轮机制验证由全新上下文审查者核对具体输入、输出、缺陷与未覆盖边界；审查者不得读取设计文档。主代理独立对照设计规范。修复后重新冻结源码，再用新的审查上下文复评。

用户 2026-10-12 对本轮编译器自举明确要求停止独立复评，直接按自举要求自动判定。本轮以 `bootstrap_compiler.py` 的真实阶段执行、固定点、依赖、源变体和独立程序输出作判定；停止后的审查片段保留，但不作为完成审评或验收条件。该裁定不把启动子集等同于完整 DV 规格。

## 完整迁移的结束条件

用户对本轮世界语义、人物回应、合理度和检查器要求实现后先汇报，随后再提供验收代码方案。本轮仅做必要的语法/生成构建与编译器固定点排错，不启动独立复评或预先编写领域验收场景；不能依据构建宣布第一章行为或心理模型合格。

以 README 的旧 Core 能力清单逐项核对，保留独立反例与差异；全部推演能力必须有本语言实现，不能通过 native primitive、旧程序集调用或隐藏答案补齐。语言表达能力、算法行为对齐、宿主接入和正式游戏验收分别记录。有限机制示例不能代替完整迁移或形式化证明。当前原型未实现的执行语法应明确拒绝。

编译器自举亦是最终必需条件：词法/解析、名称与类型/能力检查、IR 转换和目标后端都用 DV 实现。依照 DESIGN.md 冻结各阶段源码与依赖，由启动编译器产生 C1，再由 C1/C2 编译同一份编译器源码，记录确定目标源码的固定点比较与实际语义。当前元循环解释器不能代替该条件。生成 C# 等目标代码必须由源重新生成；外部目标工具链与最小平台 ABI 依赖须列明。

架构、合同、命令或验证方式调整时同步本文件；报告持续使用 `artifacts/partial-bootstrap-core/`。仅提交本轮目录和明确的全局架构增量，保留既有未提交修改。

## 自动验证设计的当前授权

用户 2026-10-12 明确授权开始正式自动验证设计；本轮仅落盘 `verification/DESIGN.md` 和模块边界，不实现或运行验收器，不恢复独立复评。拟议主程序及最多三个补充程序全部用 DV，C# ABI 仅平台 I/O，章节实例与答案只由 `.story` 装载。`verification/` 负责 manifest、因果记录、正式会话接口、只读判定、代价审计和报告的通用合同，拟议接口不得冒称现有能力。后续实现与运行授权分别记录，交付持续使用 `artifacts/partial-bootstrap-core/`。
