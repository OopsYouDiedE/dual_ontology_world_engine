# DV surface-v2 实现与实际运行

本轮已接入真正的 DV 前端与 C# 生成链，并将 Core、正式会话、只读检查程序及 chapter1 内容改写为可执行的新语法。认知映射、门槛接纳和有限域最小代价刻画都有运行结果。完整设计尚未全部实现，边界见本文末尾及 language_core/SYNTAX.md 文首。

## 编译与核心实现

compiler/compiler.dv 中的 sf_* 负责缩进扫描、Unicode/define、表达式和声明展开；ck_* 负责基本名义类型、字面量约束与可变性检查。生成目标函数使用普通语句块，局部绑定采用唯一内部名称。编译器自身仍是 DV 启动语法，C0→C1→C2→C3 在本轮构建中完成，C2/C3 生成源码相同。前端、语义展开、搜索和证书核验均在 DV，Python 仅做本轮迁移的离线编辑以及构建/文件编排。

核心源码标识符采用英文。define 的中文函数、返回、输出别名与 `define x x` 经过实际编译执行，平方示例输出 9。非法 `Acceptance = 1.2`、`Nat = 0.5` 以及 Probability 直接作为 Confidence 返回，在编译期得到输入路径、行列和具体原因，没有生成目标代码；原始诊断见 surface-v2-diagnostics-final。

映射实际写法位于 session/20-speech.dv：

```dv
mapping dvs_listener_mapping(mind, policy, bindings):
    input Percept
    output InterpretationSet
    using dvs_speech_interpret(mind, policy, bindings)

let interpretation = dvs_listener_mapping(mind, policy, bindings) × candidate
```

`×` 和 `*` 展开到该映射实现，使用本人的记忆、指代和规则，不执行数学矩阵叉积。预测调用沿说话者的听者模型，真实接收沿实际听者大脑；二者没有互相写入。

core/55-cognition.dv 提供 acceptance 声明、程度分类与转移。degree、level、命题 confidence 分列；相等即达到门槛。正式新材料触发解释重评，采纳、撤回和未采纳内容的拒斥分别记录。旧的认知条件和政策门槛继续参与计算。

## 第一章实际会话

最终章节会话为 surface-v2-run-04；整个执行期间源码封存未变。它从正式初态开始，由人物自己的认知和共同决策选择行动，没有操作答案脚本、状态注入或 Godot 回放。新前端从原生 DV 内容生成场景，场景数据与迁移前 chapter1-substantive-loop-r6 逐字节相同，SHA-256 均为 `aa750d53b56763728e871075a10d3e22eaf0c8c0aac47647d74d5d753c7db769`。

8:20 的同一实际来源 e17 分别进入四人的映射。侦探 e20 保留解释，接纳程度 0、等级 held；霍铎 e23、沈鼎 e26、苏泠 e29 的程度为 0.6、等级 accepted。该结果体现相同话语与不同主体规则的差异，不代表四人都采纳了相反理解。

8:39，沈鼎 e162 的原接纳随本人新材料降到 0，留下 accepted→held 的撤回回执。9:51，侦探 e668 对此前只保留的解释作出 rejected 判断，不再伪记为撤回。

| 正式接受 | 游戏时刻 | 回执 |
| --- | --- | --- |
| 身份 | 10:53 | e859 |
| 声源 | 11:03 | e882 |
| 原话 | 11:13 | e905 |
| 信任 | 11:23 | e928 |

这些是侦探本人 answer_accepted 的回执，既不是候选出现，也不是同伴答题或回答动作启动。11:33 的原题片段 e951 仍因 incomplete-answer 被拒绝；结束时侦探模型额度为 0，规划额度为 7，行动额度为 14，最终状态为 no-admissible-action-or-budget。因此四问完成不能替代原题完整因果论证或整章交接。

会话进程实际耗时 150.062 秒，含完整轨迹输出。这里记录本次实际耗时，不把不同认知更新实现与多次现场运行的耗时差异当作同条件性能结论。原始轨迹为 219110170 字节，保留在本地 trace-enabled.json，并以 raw-traces.zip 原字节封存。便于阅读的事件摘录为 surface-v2-events-final.json，注明原事件身份及来源。

## 强制刻画、守恒与真实账本

imprint 声明将目标、可调变量域、守恒式、资源界限和成本函数交给 dv_imprint_solve。有限空间内选择最低成本可行方案；dv_imprint_check 独立重建所选方案并检查空间覆盖和更便宜的可行项，不能只接受求解器写出的 Optimal 标签。当前只对完整核验的 Optimal 执行认知提交。预算不足分别返回 Unresolved/Feasible；已用搜索和核验工作量照常结算。

可执行内容 cognitive-semantics.dv 声明支持量与平衡量之和为 1。为达到 0.6 的接纳门槛，实际选择 `3/5 + 2/5`，成本 3，而不是成本 6 的另一可行方案。独立核验拒绝将成本改成 0 的证书；正常证书经过 JSON 保存/恢复后仍能重新核验。核验器复用已重建的所选候选，避免在覆盖检查中重复计算该候选，实际计费对应搜索与核验各一次。受限工作预算的执行没有修改认知值，工作余额实际减少。

随后增加的 dv_imprint_prepare/apply 已把求解接到真实 Core mind、主体系字段引用和既有 action-cost ledger。surface-v2-delivery-build 同时实际编译执行 cognitive_specimen 与 cognitive_mind_specimen：

- 从真实脑字段读取初态，执行后再次 dv_read 得到 support=3/5、counterweight=2/5。
- 重算接纳得到 degree=3/5、level=accepted，认知版本与 Imprint 来源一同保留。
- 正式费用账本的 stamina 由 10 变为 7，spent.stamina=3，reserved 各维归零，返回 settled/applied 回执。
- 搜索与独立核验工作余额由 18 变为 0，和时间/体力/计算资源的定价分列。

这段运行使用 Core 的实际脑状态与正式费用结算，不是只修改示例中的 acceptance 字段。它仍是独立机制内容：第一章没有为新增刻画行动规定变化域和费用，本轮没有虚构角色刻画事件，也未改变第一章初始认知。

章节最终会话后只新增了上述独立适配入口、对应内容与文档；既有章节执行函数保持不变。新适配器单独重新编译运行并封存源码，未将未调用它的章节会话当作它的执行证据。

## 修正与工程记录

surface-v2-run-01 在生成的 C# 中发生局部变量名称冲突，未进入游戏；修正为按词法绑定生成唯一内部名。surface-v2-run-02 暴露未采纳解释被标为撤回；surface-v2-run-03 暴露撤回状态下一轮被重复改标为拒斥。最终 run-04 分别保留撤回历史与真正拒斥。失败和中间运行日志独立保留，未覆盖为最终结果。

surface-v2-final-build 和 surface-v2-mind-build 编译了迁移后的章节只读检查程序，但本轮没有运行其多场景判定套件。自动数学核验与实际章节行为是两种证据，不用检查数量作项目结论。

当前机器的已安装运行时为 .NET 6/9，net8.0 产物使用显式 DOTNET_ROLL_FORWARD=Major 执行；仍以 .NET 8 为构建目标。NuGetAudit=false 用于避开不可达的构建漏洞索引查询，不改变 DV 数学或剧情检查。

## 实现边界与未完成内容

当前表层支持普通函数和控制语句、位置参数、记录/列表、固定映射运算、acceptance 及有限域 imprint。具名实参、通用 record/enum/scalar/generic、单位推导、by 证明、完整能力/模块系统、其余领域声明与目标无关 Typed IR 尚未实现。未注解代码仍采用 Value，既有通用数值仍用有限 double；配平与显式范围构造采用精确有理数。不能将当前检查器称为 Lean 等价证明内核或整个 Core 的完全静态数值证明。

数学参考是 Lean 的“表达展开与性质检查分层”思路，实际实现范围以上述源码与执行证据为准：[Lean Subtypes](https://lean-lang.org/doc/reference/latest/Basic-Types/Subtypes/)。

本轮验收范围为隔离 DV 编译、正式主体逻辑会话和独立刻画机制运行；没有执行一级三份 UI 记录、二级画面验收或三级 Godot 输入/碰撞/显示验收。三人面对足够反证维护原解释、恶魔利用貌似相关证据造成可信错觉的完整剧情链仍未完成；原题完整论证也未完成，不能宣称整章人物与游玩验收完成。
