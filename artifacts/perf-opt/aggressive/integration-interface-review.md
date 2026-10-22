# 分支接口独立静态审查

2026-10-23。主仓基点 `9a60609975bad19bade29a9aec348ac484a691de`。仅阅读工程源码及 Git 差异；未读设计文件、未读大型 DAG、未运行构建或正式会话。各工作树可能继续修改；以下只约束记录的快照，不构成行为验收。

## 快照

SHA-256 按 20-core / 30-session / 40-chapter1 / 50-chapter2 顺序列出：

- survival HEAD `73abc65d82d3af64f1f888af9bcb2210f7b3512b`
  - `254E7A689017CEDB2F370EC133277C83DB156840AF6453D3433B0A5D8B31214D`
  - `9C93A320A90B18A78CA8DCD04E7C5EAE228D3FF4268EC519D3D9084F42D53CBD`
  - `07A6CDE54D3BED2B691D77146B9A782DAE506F77D018CF0EFAB2871408078BAA`
  - `86C68D2DA2E0D4F119B1C3F1264C8124A0EFDDAAC044A39AC9D0D8B2E70CC288`
- NPC HEAD `e5660d9ec9544982260c1ec251705c5d2fcb7ca3`
  - `D043DF1A577176122FA483233AF0355A2F86059BC1943996D173B94F7B9D6E9D`
  - `321C812196BE15EFC48801DD032CD79025CA83E0A81AE331DB4DD26CBDBD3D3A`
  - `6EAC5B17D042B0A6F011CF6934FEF3EA21B32B319159105F56470E0C84C25072`
  - `E20B26A06C1F2A438D19CE642751800CB2C3EA92329095AE7F0F80A0785CD68C`
- foolish HEAD `6594a1376afbaa09996afd1747d9f81e89f4d7b8`；30/40 存在未提交导航协调修改，以上文件内容一并审查。
  - `D043DF1A577176122FA483233AF0355A2F86059BC1943996D173B94F7B9D6E9D`
  - `83F12A3424CAACB82B046FBC13B05C11035B31FA5CDCCADD51EDB4D7C39608BA`
  - `6E18C7714CB2AD2ED14BE37EE8DB525FAF3BA2876D6E40070658F5355EC4992B`
  - `E20B26A06C1F2A438D19CE642751800CB2C3EA92329095AE7F0F80A0785CD68C`

## 结论与整合要求

已读路径中未发现依据 detective profile 强制 NPC 结果、直接注入答案或按目标伤亡数结算的实现。没有足够依据将以下内容变化称为作弊：伤病法则、同伴实际问答、实际听到拒绝后的反应、开门金属声和本人导航目标都是明确的内容声明；其设计授权和数值适当性须由主代理核对用户指令，独立工程审查不代替该判断。

1. held_for 的当前环境进程路径已补齐关键依赖。20-core:400 提供目标 ref 与 clock；30-session:143/4499 的 dirty 与 sim refs 保留两者；dirty inputs 保存完整 cell（包括 time）；8193 起将 held_for 保留为动态 aged 条件，8268 起每次用当前世界时间戳展开边界，静态计划不缓存 onset 数值。因此此次所读版本没有发现“静态 clock plan 冻住发病起点”的缺陷。合并必须同时保留这些接点，不能仅取 20/50 内容。

2. **尚未覆盖的通用 held_for 推理缓存合同**：survival 30-session:75 `dvs_reason_cell` 去掉 time；83–97 `dvs_reason_premises` 对原子条件只读取 proposition.ref，没有 held_for 的 clock；99 起的法则签名因此既漏 clock，又忽略起点时间。696 起 reason_input 虽通过 sim refs 收到 clock，仍对各 cell 删除 time。若 held_for 被用于 infer，条件因时间戳变化重新变为真时，旧 signature 可抑制重新推理。当前 50 中所读 held_for 用于 enforce 病理过程，不能据此断言现有伤病结算失败；这是新增通用运算符接口的明确缺口。整合前应补完整签名或明确限制 held_for 仅用于已覆盖环境法则，并记录未覆盖范围。

3. NPC 30-session:672–724 的 current_appraisals 写入 actor.mind 的显式 subject/appraisal 引用，要求个人 basis、自身输入、不自依赖；签名包含完整输入 cell、声明和本人选中 hypothesis id；写入后递增 mind.revision。已读路径未向 physical 写入 angry，也未把评价当公共事实授予他人。未知输入产生 unknown，输入或假设改变时可替换旧评价。853/879 保留普通与人工认知入口接线，合并时两处都必须保留。30 的 cognition signature 包含整个 mind、policy 及 appraisal input signature，故此次实现的知识/声明变化能够使外层认知缓存失效。

4. NPC 40:3334–3439 的冲突条件使用本人已获得材料和本人假设，后续请求、帮助回忆、答复均声明真实 request/Complete 条件；它改变角色内容和机会链，不是 profile 分支控制。初始 false 材料槽、预设偏见与反应门槛属于内容建模，主代理仍须核对来源授权。不可把 private_appraisal_updated 事件的存在当成实际发怒、提出请求或居民完成调查。

5. foolish 30:2285–2312 的 owned_task_ready 根据已有 personal goal、basis、available_when、本人 pending 状态、eligible action 和预测 achievement 工作量判断；未匹配 clue ID、答案或人物名单。40:5337 的早退用于避免临时调查导航覆盖当前可执行本人任务。已读实现不构成线索优先白名单。`navigation_commitment` 当前只标记 40:4122/4127–4129 的门厅复查与离开目标，`practical-access` 只标记检视门闩，属于有意声明的实用目标分类；后续若将其用于指定线索顺序，应重新审查。

6. profile 差异当前只配置 detective 自身的价值敏感、实证/反事实主动性及既有深度/阻力，不直接修改 NPC；empirical_initiative 对 informational benefit 的缩放不是禁止执行。实用门闩观察免于该缩放，应作为明确的策略/内容变化报告，不能称作纯速度优化或同条件结果。拒绝合作和同伴反应是新增内容，不能用源码注释中的 user provenance 自证授权。

## 整合冲突边界

30-session 三分支都修改认知、价值和辅助条件区域，40 同时修改居民与离开/导航内容；整文件覆盖会丢失其他分支。应按接口合并：survival 的 held_for 依赖与动态时界、NPC 的 scoped appraisal 及双入口调用、foolish 的 owned task helper/导航调用/目标标记都需分别保留。survival 所读版本尚无 owned_task_ready，NPC 所读版本无 held_for；不能将任一工作树作为另外两者已完整包含的证明。

未作行为、物理、UI、封存、性能或通关结论；未检验整合后的编译产物。上述快照之后的改动不在本审查结论内。
