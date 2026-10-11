# DV 世界语义、人物回应与检查器实现汇报

本轮依据用户连续补充完成代码，随后等待用户提出验收代码方案。实现位于 `language_core/core/`，编译器与 ABI 扩展位于 `language_core/compiler/`。未接入现役第一章，未改动其剧情或既有 Core 代码。

设计核对来源：当前设计规范第四部分与第六部分；第一章 `.story` 的声路与真相、策略轨道及人物策略声明。只提取通用机制，不将章节身份、对白或答案复制到引擎。新增人物心理机制及成本判断规则已写入设计规范“DV 世界语义与人物回应补充（2026-10-12）”，其他既有修改保留。

## 交付内容

| 模块 | 实现 |
| --- | --- |
| 00-data.dv | 世界、物体、门、通道、本地引用、字段与带版本切片；复制式更新 |
| 10-laws.dv | 公理实例、分域采纳、修订/暂停、条件、阶段变更、真实执行、主观预测/推知与规范条件 |
| 20-observation.dv | 获准字段及实际变化的映射、来源与相关证据身份、去重、旧材料处理、信念冲突及认知版本 |
| 30-transport.dv | 指定路径的连通/介质/门控检查、片段衰减过滤；采纳检查与真实/预测传播分离 |
| 40-value.dv | 按显式准则分别计算主体价值、客观价值和状态变化的价值，保留未知项 |
| 50-response.dv | 请求评价、价值策略采纳、候选意图选择、独立表达轴、情感变化提案及依据 |
| 60-object-laws.dv | 通用物体运行公理声明，包含门与通道 |
| 70-reasoning-cost.dv | 推导依赖核对、已知前提/假设/推知规则、共享节点成本、合理度与置信/假说标志分列 |
| 80-inspection.dv | 世界会话检查点、按时刻查询本人世界观/价值策略/状态/切片；只读命令入口 |

编译器新增 `(axiom name version (parameters...) premise effects)`，由 DV 前端展开并检查。JSON 编解码与命令行参数计数是 C# 平台 ABI，未加入领域求解器。所有上述领域算法仍由 DV 编写并编译为 C#。

人物回应采用心理评价启发的分层设计：价值、关系、当下情感和应对资源分别输入，行为意图与表达态度分别输出。研究仅为维度划分提供启发，线性权重及动态公式是尚未标定的项目选择，不以构建成功证明心理真实性。

合理度先受推导有效性约束，再计算步骤、证据不确定性、假设和冲突的代价。无效推导报错；低成本不使它有效。带假设的结果仍标记为假说，操作效果不能冒充已经发生的事实。当前证明检查范围有限，不称形式化验证。

## 命令入口

最终构建输出的检查器：

```powershell
dotnet artifacts/partial-bootstrap-core/domain-build-r5/inspector/bin/Release/net8.0/Program.dll session.json <subject> <time> worldview
dotnet artifacts/partial-bootstrap-core/domain-build-r5/inspector/bin/Release/net8.0/Program.dll session.json <subject> <time> state
dotnet artifacts/partial-bootstrap-core/domain-build-r5/inspector/bin/Release/net8.0/Program.dll session.json <subject> <time> slice <object> <facet>
```

session.json 由 DV 会话保存，命令不读取现役游戏存档，也不自行推进第一章。输出标明请求时刻、实际检查点时刻、时间单位和认知版本。历史缺失、未到达未来时刻不能由当前真相补造。输入数据不会作为程序执行。

## 工程事实及失败记录

最终源码冻结与构建记录为 `domain-build-r5/`：`sources.zip` 保存源字节，`sources.json` 为指纹，`commands.json` 及 stdout/stderr 保存实际命令结果。先从 C0 生成 C1，再由 C1/C2 编译同一编译器源码，构建 C2/C3，比较目标源码固定点。C2/C3 目标源码 SHA-256 相同：

```text
e299eabb100983ac163489a06a09bba3f8f0673971e36104c265e4d46a5bdf42
```

随后使用 C3 编译领域库和检查器，并完成 C# 构建。原始记录明确 `domain_program_executed: false`：本轮没有执行领域样例、心理行为验证或用户尚未提供的验收程序。

`domain-build-r1/` 保留了初次失败：DV 生成器报告 offset 45014 的 invalid form arity，原因是论证遍历函数的括号使 while/if 结构错位。修正后另建证据目录；r2/r3/r4 是后续实现过程的构建记录，不覆盖失败。最终构建期间源指纹保持一致。

## 尚未完成或未验证

- 现役 `.story` 装载到 DV、第一章事件调度与时钟、Godot 输入/UI、旧 Core 等价尚未接线。
- 动态值 ABI 仍非静态类型/能力安全边界，低层数据接口按可信调用约定使用。
- 未实现完整声学模型、自动路径搜索、一般概率模型或完整证明搜索；具体物理参数、心理权重、成本尺度等待验收与调试。
- 领域行为尚未运行验证；用户后续验收方案到达后再实施，不另开独立复评。
- 本轮仅工程生成构建和编译器固定点检查；一级、二级、三级游戏验收均未执行，不能据此评价人物、剧情或游玩改善。

完整接口与数据约定见 `language_core/core/README.md`。
