# Essence–Projection Core 迁移与验收

日期：2026-09-08。

## 本轮交付

正式核心位于 `godot_csharp/core`，命名空间为 `DualOntologyWorldEngine.Core`，独立于 Godot。
`Core.sln` 只包含核心和新验收程序；原解决方案同时构建 Godot 宿主。

迁移实现了已确认的核心边界：

- Essence 只表示客观对象身份。位置、通道、锁、开放数值、外观、流程、规则载体和侵蚀采用 Property。
- Projection 没有 Source。WorldRuntime 私有持有现实与认知绑定，允许一个 Essence 有多个现实表现。
- Projection 使用稳定对象引用，Kind 可以随认知改变；无需为改类新建整个对象。
- Soul.World 对外公开不可变集合。初始化、观察、推理、采信、侵蚀、反馈和遗忘统一经过 Observe。
- 存在判断、感知证据、认知身份独立。主观存在使用三值逻辑；未认知的真实对象不创建占位。
- 明确否定关系与未知关系分开。标签输入中的 null 表示撤销，未提供的标签表示没有新信息。
- 对象被遗忘时清理相关认知引用；重新观察建立新表征。已经提交的物理动作保留原名词引用。
- 本质关系不会自动成为表面关系。StateProperty.Observable 指定可公开的数值；SurfaceProperty 声明各侧面的标签及关系。
- 读取规则只获得语法结构，不自动采信；重复观察同一规则保留已有立场。

## 系统职责

`AstDecoder` 解析文本及 Projection 嵌入，输出类型化 AST。它不执行规则、支付资源或修改世界。
`@名称` 只在给定 World 内解析；重名必须使用 `@0` 等显式嵌入。外部名词不能退回客观世界查找。
`Tag` 和 `RelationTag` 没有字符串隐式转换；外部自定义语义由解码器显式登记。

`CTBSystem` 管理行动值、到期顺序、取消及改期，每个调度项仅激活一次。
同刻按原登记序号排序。延迟在动作激活前支付，不叠加旧恢复时间。

`ActionRuntime` 在激活时检查真实控制关系、位置、钥匙、资源和条件。
一个无等待阶段使用暂存状态依次准备效果，成功后整体提交。阶段中后续条件读取前面已准备的变化。
Wait 划分阶段，前一阶段已经完成的效果不因后续失败或取消而回滚。

`DecisionSystem` 只读取内部世界中的已采信规则。支持候选槽位、Require / Forbid / Permit / Prefer、稳定择优以及未知条件。
Forbid 命中阻止候选；其余模式按权重评分，Require 未匹配扣分。不存在的主观目标仍可形成尝试。

`SocialSystem` 根据自身交互倾向、对请求者的信任和已知交互历史，给出接受、协商或拒绝态度。
它不执行请求。Decision 可以选择被接受的请求；执行完成后通过 Observe 标记已回应。
请求跨主体传递时重映射 Projection 引用；接收者无法指称的请求保留文本，不自动生成隐藏名词。

`SimulationSession` 连接上述系统，提供 Submit、Decide、Invoke、Activate 和 Deactivate。
Invoke 直接使用脑中流程 AST，言灵没有另一套解释器。
Activate 开启自主循环：无候选时休眠，Observe 唤醒；每次选出的动作至少经过指定正延迟再激活。
嵌套等待共用 CTB，父动作在等待期间保持身体行动槽，其他身体仍可行动。

## 观察责任

WorldRuntime.Observe 是引擎侧入口，调用方提供经过筛选的感知范围。核心不实现视觉遮挡、房间内坐标或碰撞。
Inspect 根据拓扑位置及持有关系检查可达性，再将可见表现送入 Observe。
IProjectionTransform 只接收使用内部投影引用的 ObservationInput，可以扭曲、拒绝采信或返回 null 抑制感知。
传入已消失对象的现实表现表示明确的否定观察，不能把未进入观察范围解释成不存在。

行动结算结果供宿主审计；Soul 只收到通用完成/未完成反馈及实际观察内容，不收到隐藏对象的真实失败原因。
历史不是第二套 Belief 存储，它保存 Observe 的证据快照；对象本身仍由内部 Projection 表示。

## 验收入口

在仓库根目录执行：

```powershell
dotnet build godot_csharp/Core.sln -c Release
dotnet run --project godot_csharp/tests/core_acceptance/CoreAcceptance.csproj -c Release
dotnet build godot_csharp/DualOntologyWorldEngine.sln -c Release
pwsh -File godot_csharp/measure-core.ps1
```

验收程序覆盖：误认钥匙的规则决策闭环；三种存在认知；多侧面通道；侵蚀身份连续性；跨世界引用拒绝；批量感知与屏蔽；文本/嵌入一致性；三值逻辑；原子支付；嵌套等待和取消；排队期间真实状态变化；请求映射与个人历史；CTB 次序；自主休眠与唤醒；言灵共用执行；认知撤销及遗忘；规则采信；禁止静默改绑；遗忘后的事务失败。

新验收程序的错误认知和知识初始化也使用 Observe。钥匙选择与推理行动由解码的脑内规则产生，测试不代替 Decision 修改选择结果。

## 代码量口径

统计 `.cs` 物理行，排除 bin / obj、测试和文档。脚本同时提供文件数与非空行。
迁移前正式 Core 为 29 个文件、9,917 行；本轮为 10 个文件、1,346 行，减少 86.43%。
非空行为 8,714 → 1,258。后续修改后的当前结果以脚本输出为准。

这是正式 Core 的实现规模比较，不是仓库总量减少比例，也不代表旧叙事业务逐项等价迁移。
`legacy/core` 和原实验核心完整保留，旧动物园继续引用旧核心。正式 Core 不引用 legacy。

旧核心中的具体文学套路、灵异物品、情绪组合、动物园语义与旧审计数值仍作为历史基线。
它们没有作为兼容层放入新 Core，也没有计作已经由新验收覆盖。下一阶段在用户验收之后，用动物园补充缺失的通用语义并展示能力。

## 构建记录

正式 Core Release 构建零警告、零错误；18 组新验收场景全部通过。
包含 Godot 宿主的解决方案 Release 构建成功。
旧动物园 CLI 和基线项目已验证可以继续构建；宿主和 CLI 的 NuGet 漏洞信息查询因网络不可用产生 NU1900 警告，编译成功。
原实验钥匙测试在归档路径运行通过，仅作为旧基线保全检查。

用户原先对 `docs/标准状态效果与时效管理规范.md` 的修改未纳入本轮改动。
