# DuelView DV r2 独立机制审查

日期：2026-10-12。审查者使用新的独立上下文。范围是 `language_core/` 的语言机制与 C# 宿主权威边界；本轮没有修改该目录，没有运行游戏、Godot、flow 或统一回归。没有读取设计文件正文、旧 Core、剧本、译文或历史报告。冻结目录仅用于核对本轮源码与程序集身份，旧反例工具只作为通用输入工具使用，没有读取其旧 stdout 或审查结论。

本轮指定的资源别名、显式禁止、未知预测、完整层预算、远距存在性、快照与 token 反例没有暴露实质性失败。这个结论仅覆盖下述具体输入与输出，不代表完整迁移、正式编译器、自托管编译器或形式化证明，也不能代替游戏验收。

## 冻结与执行身份

入口与结束均核对 `engineering-20261012-dv-r2/source-sha256.json` 所列源码以及 `execution.json` 所列生产 DLL；`source_differences` 和 `assembly_differences` 均为空。设计文件只计算 SHA-256，不解码或显示正文。

审查工具只引用入口核对后复制到 `review-dv-r2/frozen/Language.Core.dll` 的生产程序集，没有项目引用，也没有重建 `language_core`。该 DLL 的 SHA-256 为：

`f5d9c923b746e49895b4c471c07ee18b9e84bd03e4e9ac44ddc9b17f215ced1b`

生产 CLI DLL 的 SHA-256 为：

`b6f339bef4a84996525f458b43c480b5e981c4a6f27f2b6f6d23d70e66e5bb78`

审查工具及补充隔离观察工具的构建、执行退出码均为 0；没有发生异常退出或缺少最终记录。工程构建只说明本工具能够引用冻结程序集，不作为人物、游戏或完整语言能力的结论。原始 stdout/stderr 均通过 `write_bytes` 新增封存，局部 `.gitattributes` 关闭文本转换。

证据位于 `review-dv-r2/`：`fingerprint-before.json`、`fingerprint-after.json`、`boundary-fingerprint-before.json`、`fingerprint-final.json`、`execution.json`、`boundary-execution.json`；实际反例输出为 `harness.stdout.txt` 和 `boundary.stdout.txt`，对应 stderr 文件保留原始字节。`ReviewHarness.csproj` 明确只编译 `Program.cs`，避免随后新增的补充工具被其自动递归编译；反例输入源码未因这项工程范围约束改变。

## 指定反例的实际行为

| 输入与前提 | 实际输出 | 机制判断 |
| --- | --- | --- |
| 一个权威实体 `stock=3`，两个观察句柄 `object`/`facet`；执行 `(seq (spend object stock 2) (spend facet stock 2))` | `Succeeded=False, Failure=resource, stock=3, worldRevision=3` | 同一实体的阶段资源累计消耗；第二次消耗导致整个事务拒绝，首段消耗没有落地 |
| 相同别名结构，执行两次 `change +1` | `Succeeded=True, stock=5, worldRevision=4` | 两个别名写入累计到一个真实资源，未只保留一次写入 |
| 主体已接受显式 `object` 的 `Forbid(change stock 1)`，使用 `facet` 执行同一实体的相同写入 | `Succeeded=False, Failure=forbidden, stock=3` | 真实事务内对动作与已激活规则统一规范引用，别名不能绕过该禁止 |
| 主体未观察 `stock` 数值，偏好 `change stock 1`；请求深度 2、预算 1 | `first` 为空，`edges=()`、`depth=0`、`comparedDepth=0`、`expanded=1`、`stop=no-action`、`predictionFailures=[unknown-state]`；权威 `stock` 仍为 3 | 没有将未知数值视作 0 或使用权威数值建立后继；失败原因保留在正式结果 |
| `when(unseen>=1,set flag=1)` 后接 `change progress +1`；另一高分规则要求 `progress>=1` 且 `not(flag>=1)` | 无首步、无边，`expanded=1`，`stop=no-action`，`predictionFailures=[unknown-condition]`；快照仍有 `flag=0,progress=0,stock=3` | 未把 Unknown 条件当 False，并未基于跳过分支后的旧 flag 生成确定高分后继 |
| 两个根候选：`change stock +1` 分 1、`change progress +1` 分 2；深度 1、预算 1 | 选择 progress 首步；`depth=1,comparedDepth=1,expanded=1,stop=depth` | 共享初始状态计一次；预算计状态展开，而不是两个候选边 |
| 相同候选，深度 2 或 3，预算 1；以及深度 2、预算 2 | 保留 progress 首步，`depth=1,comparedDepth=1,expanded=1,stop=complete-layer-budget` | 剩余预算不足展开两个 frontier 状态时，整层未展开，没有按候选顺序只扩一部分 |
| 相同候选，深度 2、预算 3 | 选择 stock→progress；`score=3,depth=2,comparedDepth=2,expanded=3,stop=depth` | 初始状态 1 加下一层两个父状态 2；完成了第二层比较 |
| 预算 0 | `LanguageException: "Invalid expansion budget"` | 非正展开预算被明确拒绝 |
| 远距实体，主体存在性均为 Unknown；一个真实存在，一个真实不存在；分别 `require exists` | 两者均 `out-of-reach` | 本反例未通过存在/不存在的不同失败原因泄漏远距权威状态 |
| 远距存在实体 `secret=1`；分别要求 `secret>=0`、`secret>=2` | 两者均 `out-of-reach` | 真/假条件结果前先执行访问核验 |

这里 `depth` 是选中路径长度，`comparedDepth` 是已完成比较层，不应互相替代。Unknown 的反例没有确定可比较后继，因而没有路径或完成比较层；`expanded=1` 记录已尝试展开初始状态。

## 真实规范引用与主观观察的边界

源码中的 `WorldCore.Prepare` 为每个权威实体选一个阶段资源句柄，对真实执行动作调用 `canonical-nouns`。已接受规则先在 `view.Facts` 与主体槽位绑定上激活，再在事务内部规范其引用；权威资源演算、可达性和提交均在宿主边界执行。规则前提的激活没有改成权威事实求值。相关实现为 `WorldCore.cs:292`、`:303`、`:310`、`:311`，语言规范化位于 `20-world.dv:75`。

补充输入特意让同一个权威实体为 `stock=3`，主体 A 的 `object` 观察为 7、`facet` 观察为 2。A 对 `object stock>=7` 得到 True，对 `facet stock>=7` 得到 False，未观察的 facet 数值条件仍为 Unknown。随后真实事务经两个别名各 `change +1`，权威值成为 5；A 的旧快照和再次 Capture 的快照仍分别保留 7 与 2。真实资源统一没有合并观察 facet，也没有把权威结果自动纠正到主体认知中。

主观 `Plan` 使用显式主体快照建立私有预测；它与真实事务的规范引用、权威合法性和提交属于不同边界。不能仅因上述真实别名事务成立，就宣称所有主观规划对未知别名关系、跨 facet 信念、完整实体消歧已经完成。

## 快照、控制与 token

| 输入 | 实际输出 |
| --- | --- |
| 同时准备两个 `change stock +1` token，提交第一个 | 成功，权威 stock=4、worldRevision=4 |
| 提交竞争的第二个 token | `stale-world`，stock 保持 4 |
| 再次提交第一个 token | `already-committed`，stock 保持 4 |
| token 准备后对同一主体执行 `Conceive` | 提交拒绝 `stale-cognition`，stock 保持 4 |
| 将 token 交给另一个 WorldCore | `LanguageException: Foreign prepared action.` |
| 将快照交给另一个 WorldCore | `LanguageException: Snapshot belongs to another authority boundary.` |
| 在主体条件里使用权威实体 ID 文本 | `LanguageException: Reference is outside subject view.` |
| 权威提交后使用旧快照要求 stock>=4 | False，旧观察 stock=3 没有被权威提交改变 |

补充工具在同一世界创建两个主体和两个不同实体，两个主体均使用本地句柄 `object`。A 对 `stock>=11` 为 False，B 为 True；A 的事务不改 B 权威资源。B 更新自身观察到 15 后，B 旧快照对 `stock>=15` 仍为 False，新快照为 True，A 的结果仍为 False。A 试图用已观察到的 B 身体作为 actor，准备返回 `no-control`。快照打印文本中没有该输入的 `authority-` 实体 ID 前缀。

这些输入证实了本次观察中的世界来源、主体本地句柄、版本和身体控制隔离；没有试图把受信任的 `WorldCore` 宿主 API 当作供不可信代码任意调用的沙箱。

## 当前实现与未覆盖范围

依据允许读取的 README 与本轮源文件清单，活动语言源码均使用 `.dv`，语言名为 DuelView（DV）。当前仍是部分解释自举：语言自身写的解释器和推演模块由启动 C# 内核载入并执行，Reader、运行预算、内存及宿主权威事务边界仍有 C# 依赖。正式类型化前端、IR/后端、DV→C# 编译器与编译器自托管链是目标设计，当前不存在，不能把这些反例称为编译固定点或自托管编译器证据。

本轮只做独立机制审查，未执行一级、二级或三级验收；人物行为合理性与游玩体验未验证。没有读取设计文档，因此也没有作设计符合性或内容判断。

未覆盖完整旧 API/能力迁移、所有规则优先级与 Permit 组合、别名关系与主体知识的完整建模、所有条件/关系/层级嵌套、并发线程竞争、跨进程保存、所有概率与推导反例、正式语法与静态类型检查、目标代码生成、编译器自托管和游戏接入。没有进一步扩大实验，也没有以示例输出推断这些边界成立。
