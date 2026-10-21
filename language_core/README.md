# DuelView 独立语言 Core

DuelView 简称 DV，源码后缀为 `.dv`。领域算法、主体推演和原生章节内容由 DV 编译为 C#；Godot 通过 `DvSession` 使用同一正式动作链。这里的源码与工程说明不代替实际游戏验收。

## 作者从这里开始

剧情可以直接写成段落，保留人的标点与换行；条件和效果使用确定的领域句式：

```dv
称谓 木箱 为「crate」。
称谓 剩余件数 为「remaining」。

叙述 箱边见闻：
    她放下提灯，伸手拂去箱盖上的灰。
    “这里还有东西。”

法则 取一件：
    当 木箱 的 剩余件数 至少 1。
    则 木箱 的 剩余件数 增加 -1。
```

这是中性语法示例。`叙述` 返回文字，`台词` 返回逐行文字列表；它们不要求正文使用引号、转义或 `return`。`法则` 和 `行动` 保持既有条件、来源、观察和正式执行合同。新写章节优先用这些形式，算法和复杂数据组合仍可使用普通函数。完整写法、缩进、中文引号、文件内称谓、本人/现场边界及当前限制见 [SYNTAX.md 的自然书写部分](SYNTAX.md#当前执行子集按段落与句子书写)。

## 当前文件与依赖

| 文件或目录 | 职责 |
| --- | --- |
| [10-compiler.dv](10-compiler.dv) | 编译器前端、名称/基本名义类型检查与直接 C# 后端；自身仍使用 S 表达式启动语法 |
| [20-core.dv](20-core.dv) | 数据、世界、观察、传播、价值、费用、论证与有限域刻画 |
| [30-session.dv](30-session.dv) | 主体认知、规划、话语、付费重演、调度和 Prepare/Complete |
| [40-chapter1.dv](40-chapter1.dv) | 第一章原生内容与认知配置 |
| [50-chapter2.dv](50-chapter2.dv) | 第二章原生内容、目标、对话与空间声明 |
| [60-game.dv](60-game.dv) | include 20/30/40/50，组合生产、宿主呈现与 `dvg_debug_run` |
| [70-checks.dv](70-checks.dv) | include 60 的独立只读因果检查、oracle、夹具与报告；仍有启动语法报告函数及旧 JSON 输入接口 |
| [compiler/](compiler/README.md) | `seed/Program.cs`、`Seed.csproj` 与基础值/容器/文件 ABI `Runtime.cs` |
| [Session/](Session/README.md) | C# 宿主与序列化边界，构建时生成 `obj/dv/DVProgram.cs` |
| [tools/](tools/) | 共用构建、编辑器诊断、正式逻辑运行及二进制证据工具 |
| [editors/](editors/AGENTS.md) | VS Code 与 Visual Studio 2022 客户端；Godot 客户端在宿主插件目录 |

`tools/dv_toolchain.py` 解析行首的相对 `#include`，保留各源文件的 define/称谓作用域与位置边界；缩进正文中的引用样式文字原样保留。它以 `compiler/seed/Seed.csproj` 为 C0，依次用 `10-compiler.dv` 生成 C1、C2、C3，并比较 C2/C3 的 C# 源码。`tools/build_session.py` 默认将 `60-game.dv` 的闭包交给 C3，生成由 Session 项目引用的类库。`70-checks.dv` 不被生产入口反向包含；seed、Runtime、检查源码与诊断恢复工具都具有独立用途。

## 构建与工程检查

在仓库根目录运行：

```powershell
dotnet build language_core/LanguageCore.sln -c Release --disable-build-servers -p:NuGetAudit=false
python language_core/tools/build_session.py --out language_core/Session/obj/dv
python language_core/tools/check_dv.py --format json language_core/30-session.dv
```

首条命令构建 Seed 与 Session；第二条只生成可检查的生产 C# 源码，并在需要时构建自举编译器。Session 项目本身也会调用生成器。生成物存于 `obj`，不手工修改；可用 `-p:DvPython=<Python路径>` 为 MSBuild 指定 Python。环境要求 .NET 8 SDK 与 Python 3，宿主要求见仓库根 README。

[编译组配置](dv-check.json) 依次声明编译器、生产入口和独立检查入口。编辑 20–60 时选择生产闭包，编辑 70 时才加入检查/oracle；编辑 10 时单独检查启动源。配置只决定静态诊断的源集合，不执行世界或会话。组内其他文件从磁盘读取，当前文件可以来自未保存缓冲区。

[VS Code 客户端](editors/vscode/README.md)、[Visual Studio 2022 客户端](editors/visualstudio/README.md) 和 [Godot 插件](../godot_csharp/addons/dv_checker/README.md) 复用 `tools/check_dv.py` / `tools/dv_lsp.py`。当前提供编译诊断，尚无完整证明状态、自动补全或跨文件定义导航；协议检查、插件安装与实际编辑器呈现须分别验证。

## 正式逻辑与证据

只有任务要求实际逻辑会话时，使用：

```powershell
python language_core/tools/run_debug.py --out artifacts/partial-bootstrap-core/<新目录> --timeout <本轮授权秒数>
```

该入口调用 `60-game.dv` 的 `dvg_debug_run`，以 `formal-model` 执行同一 Prepare/Complete 和连续章节扩展。它不包含 Godot 的几何、碰撞、输入与画面；达到时域、实际剧情达成和完整封存分别报告。测试授权与上限以当前用户指令及 [AGENTS.md](AGENTS.md) 为准，不沿用历史任务窗口自动启动会话。

新原始事件、状态、存档和投影使用 `.dvdag` / `.dvsave` / `.dvlog`。`tools/binary_evidence.py`、`tools/raw_dag.py` 校验二进制记录；`tools/debug_checkpoints.py` 与 `tools/DebugCheckpointResume.cs` 保留实际阶段来源的恢复能力。旧 JSON 记录只作只读兼容，JSON 仍用于进程内接口与编译配置。人工游玩的精简审计策略和完整逻辑证据分别说明，不能据精简输出宣称完整审计。

## 实现范围与文档

现场失败与成功限制：一次性及默认不重复行动以成功 Complete 写入的 `completed_actions` 限制次数，`used` 仍记录全部尝试，费用与行动配额照常结算。人工实际失去热点接触，或在完整运动 job 证明下现场受阻时，正式拒绝回执可生成 `manual_contact_retries` 控制凭据；仅临时人工输入视图允许再次显式选择，仍重核资格与实际执行。凭据随主体保存、恢复与扩章，不授予认识；自主拒绝签名及资源、绑定、身份、站点证据和权威前提保护继续有效。

规划声明：Prepare 使用内容的规划层数、后继限制与近似设置，按原 `planning_limit` 提供本轮预算，保留独立 idle 固定点证明；不把所有自主主体统一强制为一层近似，也不另设更高预算。此修正恢复内容声明的认知差异，属于控制与规划条件变化；此前强制近似结果需单列，不能据此声称同条件提速。

行动规划在评分前排除本预测路径 `node.tasks` 中已有的非 transit 任务，避免同一尚未知观察机会重复领取预测价值并成为 best；后继入队的原检查保留。这只修正路径内重复计分，保留真实可重复行动、原预算、层数、顺序、费用与个人价值，不为人物指定路线或提前写入观察。效果需在独立冻结的后续窗口核对。

重演准入：有假设目录时，replay 需至少一个候选满足本人的话题、来源与开放条件，人工选中项另按原人工条件核对；正式 Complete 复用同一候选条件并重新核对。纯资格扫描不运行模型，按完整本人输入保守复用；无假设目录的 factor_constraints 专用重演保留原路径。此机制避免空候选仍支付重演费用，不授予问题或线索，不提高预算，也不把未启用候选当作已求解。

连续过门按键释放的重试采用严格源侧取消证明：宿主独立核对 Prepared 身份与源/目标绑定，DV 核对 `source-cancel-v1` 及原始回执；当前只支持无跨图、执行开始与停止坐标精确相同且均为有限二维坐标的 `released` 取消。本人仍在已核准的原源室或源侧门道；原失败与费用保留，仅允许新的人工输入重试，不计完成、不发目标观察、不自动重派。请求发出时的位置与真正开始执行的位置分列；中途移动后返回及缺证明的旧记录尚不覆盖。

20–60 使用 surface-v2 的可执行表层，40/50 的纯法则、独立叙述和部分行动已采用自然书写，10 保留启动语法，70 同时含表层与启动语法。因此不能声称所有内容或七个文件都已彻底转换。已接入自然段落、领域句式、公式函数、MATLAB 风格具名输出函数、缩进、普通调用、记录/列表、中缀运算、单绑定列表推导、mapping、acceptance 和有限域 imprint；任意自然语言理解、完整泛型、单位推导、能力类型、闭包及目标无关 Typed IR 仍属未实现范围。

`20-core.dv` 的 `dv_bind/dv_view` 一次建立显式主客观对象对应，具体事实仍走观察链。`dv_evaluate` 以 verify/run 共用一个法则求值器，前提、原子效果和可选后置条件保持同义；验证候选带快照范围，不提交事件。`dv_imprint_solve_with` 提供参考枚举和精确线性区间分支剪枝，自动选择只影响算法，不扩大预算前缀；独立 checker 核验最优性与覆盖。`dv_analyze_law`、`dv_imprint_analyze`、`dv_analyze_time` 分别报告快照依赖、有限模型适用性和时间重评边界。Session 使用同一个 Core 时间分析核，保留逐分钟回退与正式动作链。可执行示例、结果资格和未实现边界集中在 [SYNTAX.md](SYNTAX.md)。

[docs/syntax.md](../docs/syntax.md) 是现行语法入口，[SYNTAX.md](SYNTAX.md) 明确区分执行子集与完整语法方案，[DESIGN.md](DESIGN.md) 维护工程架构与不变量，[REFERENCES.md](REFERENCES.md) 保留研究来源。剧情、人物和玩法只依据 [设计规范](../docs/design-standard.md)。已移除目录及历史任务记录从 Git 历史追溯，不作为当前入口或本轮验收证据。


## 人工重演与观察对照

手稿提供未预选的具名假设与组合变量。控制链仍传原动作 ID，附加所选 hypothesis_id；DV 负责选择、完成时重核和付费，只执行该候选。不同候选的尝试签名隔离，认识变化后允许重新核对；AI 仍用原批量重演。V6 开放前按明确标注的“彼此陌生，尚未查明”初始假设处理，开放后由玩家选择两种关系。

第一章七项痕迹有内容声明的公开标签。dvg_replay 只显示本人已取得且仍有效的直接感知与已付费重演之间的相容、矛盾或不足，保留旧结果与未完成提示。comparisons 元数据记录模型、来源与真实观察关联的动作 ID，不读取世界真值，也不补跑模型。当前地点名称只来自观察者本人的当前地点。上述为实现合同，实际可操作性与剧情达成另依本轮运行记录。
