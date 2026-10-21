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

## 当前模块与入口

| 路径 | 职责 |
| --- | --- |
| [compiler/](compiler/README.md) | 10-compiler.dv、seed 与 Runtime |
| [modules/](modules/README.md) | 20/30 通用机制，35 公共结构，40/50 章节，60 生产入口 |
| [base/](base/DuelView.Core.csproj) | 20/30 与 Runtime 的独立程序集 |
| [host_bridge/](host_bridge/README.md) | 内容程序集、原生值图、StateDelta、存档、提前准备及回放 |
| [cli/](cli/DvCli.csproj) | 文本、JSON-Lines、TUI 与去停滞回放 |
| [checks/](checks/) | 65 逻辑运行器、70 因果检查与 oracle |
| [../mcp/build/](../mcp/build/) | 自举构建、解析缓存与分区代码生成 |
| [../mcp/editor_lsp/](../mcp/editor_lsp/) | 共用诊断和编辑器客户端 |
| [../mcp/verification/](../mcp/verification/) | 二进制证据、逻辑运行和恢复工具 |

40 与 50 分别 include 35，35 include 30，30 include 20。60 组合两章；65 include 60，70 include 65。生产程序集只包含 20–60。公共人物、时钟、位置、目标窗口与构造器使用 campaign_ 名称，章节之间没有反向函数依赖。

在仓库根目录运行：

```powershell
dotnet build dv_core/LanguageCore.sln -c Release --disable-build-servers -p:NuGetAudit=false
dotnet run --project dv_core/cli/DvCli.csproj -- --tui
python mcp/build/build_session.py --partition --out dv_core/host_bridge/obj/dv
python mcp/editor_lsp/check_dv.py --format json dv_core/modules/30-session.dv
```

需要 .NET 8 SDK 与 Python 3。MSBuild 可用 `-p:DvPython=<路径>` 指定 Python。20/30 分模块生成到 base 的 DVKernel；35–60 分模块生成到 host_bridge 的 DVProgram。各模块以本地 UTF-16 位置解析，只导入依赖的函数和名义类型接口；正文变化按模块使缓存失效，接口变化影响消费者。生成文件位于各项目 `obj/dv/modules/`，内容不变时保留时间戳。MCP/LSP 初始化在后台预热共享编译器缓存；编辑器通过 [dv-check.json](dv-check.json) 选择诊断闭包。

Godot 热路径使用只读 .NET 值图和按观察者版本的 StateDelta。文本客户端继续使用 JSON 接口。保存来自最近已提交状态；Trace 只读，Close 幂等。提前准备保存独立候选，正式选择消费版本匹配的结果；后台 NPC 继续经过现场 Execute 和 Complete。细节见 [宿主接口](host_bridge/README.md)。

Core 的宿主投影只发布 actor、逻辑地图/房间/站点身份、已获得的名称、位置标题和逻辑时刻。场景路径、人物 sprite、动画帧、站点资源映射和 HUD 布局由 Godot 自己的呈现契约绑定，CLI 消费相同中立字段。

## 正式逻辑与记录

`python mcp/verification/run_debug.py --out <新目录> --timeout <秒数>` 从 checks/65-debug-runner.dv 运行正式逻辑。执行器明确标为 formal-model，几何、碰撞与画面由 Godot 现场提供。存档、轨迹与宿主记录分别使用 .dvsave、.dvdag、.dvlog。JSON-Lines 只用于文本客户端 stdin/stdout 通信。当前任务的执行阶段、范围与时限按用户指令安排。

CLI 的 status/options/act/next 使用同一 DvSession；--dev 开启 inspect/tp/time，单次 --command 的错误返回退出码 1。SmartReplay 供 CLI 与游戏控制台共用，`dv-replay-v2` 帧保留检查点、原始设备输入、身体/UI 上下文、玩家呈现和语义事件，按动作和对白类型设定节拍，忽略原始现实等待，支持 Play/Pause/Step 与 1/2/4/8 倍速。回放使用日志已记录内容，不修改正式会话；旧记录缺少实际坐标时按逻辑位置重建。

70 的 `domain_evaluation_complete` 表示实际完成了领域分析，`domain_evaluation_reason` 说明未进入的具体原因；完整性失败、初始状态缺失、无效轨迹或配置不符不会被记为已完成。领域分析得出负面结论仍属于完成，`main_causal_supported` 单独报告主因果结论。

## 实现范围与文档

现场失败与成功限制：一次性及默认不重复行动以成功 Complete 写入的 `completed_actions` 限制次数，`used` 仍记录全部尝试，费用与行动配额照常结算。人工实际失去热点接触，或在完整运动 job 证明下现场受阻时，正式拒绝回执可生成 `manual_contact_retries` 控制凭据；仅临时人工输入视图允许再次显式选择，仍重核资格与实际执行。凭据随主体保存、恢复与扩章，不授予认识；自主拒绝签名及资源、绑定、身份、站点证据和权威前提保护继续有效。

规划声明：Prepare 使用内容的规划层数、后继限制与近似设置，按原 `planning_limit` 提供本轮预算，保留独立 idle 固定点证明；不把所有自主主体统一强制为一层近似，也不另设更高预算。此修正恢复内容声明的认知差异，属于控制与规划条件变化；此前强制近似结果需单列，不能据此声称同条件提速。

行动规划在评分前排除本预测路径 `node.tasks` 中已有的非 transit 任务，避免同一尚未知观察机会重复领取预测价值并成为 best；后继入队的原检查保留。这只修正路径内重复计分，保留真实可重复行动、原预算、层数、顺序、费用与个人价值，不为人物指定路线或提前写入观察。效果需在独立冻结的后续窗口核对。

重演准入：有假设目录时，replay 需至少一个候选满足本人的话题、来源与开放条件，人工选中项另按原人工条件核对；正式 Complete 复用同一候选条件并重新核对。纯资格扫描不运行模型，按完整本人输入保守复用；无假设目录的 factor_constraints 专用重演保留原路径。此机制避免空候选仍支付重演费用，不授予问题或线索，不提高预算，也不把未启用候选当作已求解。

连续过门按键释放的重试采用严格源侧取消证明：宿主独立核对 Prepared 身份与源/目标绑定，DV 核对 `source-cancel-v1` 及原始回执；当前只支持无跨图、执行开始与停止坐标精确相同且均为有限二维坐标的 `released` 取消。本人仍在已核准的原源室或源侧门道；原失败与费用保留，仅允许新的人工输入重试，不计完成、不发目标观察、不自动重派。请求发出时的位置与真正开始执行的位置分列；中途移动后返回及缺证明的旧记录尚不覆盖。

20–60 使用 surface-v2 的可执行表层，40/50 的纯法则、独立叙述和部分行动已采用自然书写，10 保留启动语法，70 同时含表层与启动语法。因此不能声称所有内容或各模块都已彻底转换。已接入自然段落、领域句式、公式函数、MATLAB 风格具名输出函数、缩进、普通调用、记录/列表、中缀运算、单绑定列表推导、mapping、acceptance 和有限域 imprint；任意自然语言理解、完整泛型、单位推导、能力类型、闭包及目标无关 Typed IR 仍属未实现范围。

`20-core.dv` 的 `dv_bind/dv_view` 一次建立显式主客观对象对应，具体事实仍走观察链。`dv_evaluate` 以 verify/run 共用一个法则求值器，前提、原子效果和可选后置条件保持同义；验证候选带快照范围，不提交事件。`dv_imprint_solve_with` 提供参考枚举和精确线性区间分支剪枝，自动选择只影响算法，不扩大预算前缀；独立 checker 核验最优性与覆盖。`dv_analyze_law`、`dv_imprint_analyze`、`dv_analyze_time` 分别报告快照依赖、有限模型适用性和时间重评边界。Session 使用同一个 Core 时间分析核，保留逐分钟回退与正式动作链。可执行示例、结果资格和未实现边界集中在 [SYNTAX.md](SYNTAX.md)。

[docs/syntax.md](../docs/syntax.md) 是现行语法入口，[SYNTAX.md](SYNTAX.md) 明确区分执行子集与完整语法方案，[DESIGN.md](DESIGN.md) 维护工程架构与不变量，[REFERENCES.md](REFERENCES.md) 保留研究来源。剧情、人物和玩法只依据 [设计规范](../docs/design-standard.md)。已移除目录及历史任务记录从 Git 历史追溯，不作为当前入口或本轮验收证据。


## 人工重演与观察对照

手稿提供未预选的具名假设与组合变量。控制链仍传原动作 ID，附加所选 hypothesis_id；DV 负责选择、完成时重核和付费，只执行该候选。不同候选的尝试签名隔离，认识变化后允许重新核对；AI 仍用原批量重演。V6 开放前按明确标注的“彼此陌生，尚未查明”初始假设处理，开放后由玩家选择两种关系。

第一章七项痕迹有内容声明的公开标签。dvg_replay 只显示本人已取得且仍有效的直接感知与已付费重演之间的相容、矛盾或不足，保留旧结果与未完成提示。comparisons 元数据记录模型、来源与真实观察关联的动作 ID，不读取世界真值，也不补跑模型。当前地点名称只来自观察者本人的当前地点。上述为实现合同，实际可操作性与剧情达成另依本轮运行记录。
