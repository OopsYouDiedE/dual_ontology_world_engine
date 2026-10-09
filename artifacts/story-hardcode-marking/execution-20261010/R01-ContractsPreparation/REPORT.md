# R01 公共合同准备审查

状态：`PRE_IMPLEMENTATION_REVIEW / INCOMPLETE_FOR_G_CONTRACT`。A–D 与 C08 均为未批准、未实现候选。本报告可以帮助主代理收敛审批文本和未来任务卡，不能完成逐提交 R01，不能代替 R02 行为/多周目评审、用户裁决、消费方确认、G-CONTRACT 版本锁或实施租约。

审查基线由主代理提供为 `7489db56a714724eb7cb735958dd879dc662d238`。本代理未操作 Git，未独立核对当前 HEAD；输入以本报告附带 SHA256 固定。本轮范围仅前两章约 3–5 游戏日，40 日是未来任务。隔离是共享环境中的 **DETECTION_ONLY**，没有硬 ACL、独立 clone 或不可变输入快照的保证。

## 可审性结论

AR-CORE-01 已清楚写出主体信息边界、已提交身体动作的连续性、禁止全真推演伪装为主体结果、禁止重跑初态恢复、禁止任意脚本/反射/万能效果及扩大章节范围。这些边界适合提交给用户作语义裁决；目前材料仍不足以冻结公共合同和签生产实施租约。

F01 证明的是当前实现的静态支持度，D01–D05 是已封存中性诊断，不能把它们的成功断言或实验 DTO 认作新能力已批准。A–D 提供/消费方写到了模块级，但缺少具体字段、类型、错误分类、必需能力、版本兼容和旧入口适配矩阵；“保存/恢复”和“完整不可变差异”还没有可执行的范围定义。

明确的材料冲突有两项，必须在锁定合同前消除：

1. D01A `Referent` 表示作者词项提及，含 RawLexeme/CanonicalLexeme/SourceSpan；D01B `ReferentId` 表示一个主体会话中的 Projection。两者不能共用同名公共 ID 后再靠上下文猜含义。作者词项 ID、主体投影句柄、解释结果 ID 应分别命名，或由主代理选定另一种无歧义词汇；本审查不代选名称。
2. D01B `history_and_persistence` 和 P04 明确不纳入首次最小实现，先返回 Unsupported；AR-CORE-01 B 则明确把前两章版本化保存/恢复纳入本批。后续任务应以最终获批范围为准，不能继续把 P04 当未来项同时宣称 B 已交付。

另有已被审批申请收敛的旧候选分歧：D01A A05、D01B P03 曾提出“提交后遗忘是否撤销”的待定选择，AR 已明确保持既有提交绑定。未来合同必须标注旧候选已被该 proposal 的边界取代；不得复用“每阶段检查主观成员”实现隐式取消身体动作。

## A–D 的最小复用和语义边界

| Proposal | 可以复用的既有路径 | 真正的新公共合同/语义边界 | 目前不满足项 |
| --- | --- | --- | --- |
| A 身份/切面/主观引用/中文词项 | Essence、Projection、SurfaceProperty、Project/Observe、嵌入 Noun.Reference、当前绑定拒绝静默换绑 | 持久身份；多切面作者 IR 和宿主桥；主体词项候选集；所选切面 Inspect；统一类型化解析结果 | ID 域冲突；别名获知来源、限定词 Unknown、归一化/作用域策略、切面失效、旧入口含义和错误映射未冻结 |
| B 版本、快照、历史、保存 | 部分不可变 ObservationEvent 字段；WorldSession 的局部 gate；现有连续会话活引用 | 覆盖所有认知写入的批提交版本；原子捕获；完整历史差异；稳定引用恢复；旧档迁移/拒绝 | 批边界及公开 Core 写入口覆盖不明；规则/Procedure/关系/遗忘日志不全；保存切点、codec、调度和租约恢复无合同；与 D01B 首批范围冲突 |
| C 主体推演/权威审计 | 既有 Reasoner、DecisionSystem、规划副本与世界归属校验；显式假设数据 | 输入来源约束；主体专用 immutable 图；引用深隔离；结果与停止原因分开；完整缓存键 | 模型生产来源、分支回调和 AST/关系重绑定未闭合；HypothesisDelta 字段权限、预算消费方、随机过程恢复目标未定 |
| D 能力、事件、内容迁移 | 当前 parser/IR/loader 白名单；阶段 Prepare/Commit；Say/Observe/SocialSystem；现有来源字段；条件 Wait/TemporarySetUntilEnd | 正式能力 ID 和版本协商；统一类型化事件来源与消费方适配；作者声明仅组合获批能力 | F01 审计 manifest 不是运行时目录；无精确新字段/unknown-field/required-capability 策略；事件类型/权限/幂等/错误边界不全；多个未版本化协议尚无迁移矩阵 |

D02 已证明规则适用性与目标完成守卫可以用 Exists/Tagged/At/Not/All 和原 Rule 组合；没有依据再建“新规则永远优先”或“承载投影不存在则自动注销规则”的机制。D05 已证明当前租约、条件等待和提交检查可完成瞬间共同条件的一次效果；全组原子资源提交、连续共同保持一段时间是不同要求，必须另有明确语义和能力裁决。D03 的现有物理组合也不授权自动遮挡、一般材料传播或声学模型。

## 实现前必须具体化的选择

| ID | 决策点 | 最低可锁内容 | 缺口的中性反例 |
| --- | --- | --- | --- |
| CP-01 | 身份域与生命周期 | objective Entity/Facet、author term、subject mental handle 的类型与不互换规则；会话/主体/命名空间；重命名、Forget、重新观察、销毁和恢复的生命周期 | 一个实体两切面与两个实体同词都各有两候选；相同显示词不能说明同一身份；Forget 后重新观察不能按名称把旧句柄换绑 |
| CP-02 | 词汇与解释 | NFC 是否采用；ID 比较规则；跨章裸 ID；静态引用唯一与运行时多义分开；别名来源；限定词 Unknown 的处理；候选排序只供展示 | 同词两项，其中限定条件一项 True、一项 Unknown，不能把 Unknown 排除为 False 后假装唯一；NFC/NFD 碰撞必须诊断 |
| CP-03 | 解释/执行分界 | 解析错误、命题三值、能力失败和客观提交失败的不同结果类型；提交前 stale 凭证检查；提交后保持原客观绑定；指定切面观察策略 | 同一已提交动作后仅 Forget 仍按既有绑定执行；客观 Remove 则 MissingObject；旧词项重解析不应改绑到同名新实体 |
| CP-04 | 认知提交与一致读 | 一次 revision 的原子写集合、失败是否生成版本、规则/别名/Forget 等是否都更新；Ast/Decision/Reasoner 的读覆盖；公开写入口参与方式；捕获重入与并发策略 | 旧完整态和新完整态都 False，逐叶混读却 True（D01A P05）；只在静止时复制或外填 revision 不能解决此反例 |
| CP-05 | 历史与保存范围 | checkpoint+delta 或等价合同；全部可查询字段及稳定引用；词汇/规则/模型/能力版本；遗忘审计可见权；保存于静止切点或支持活动执行；缺失/旧/未来版本拒绝 | 两段过去 Rule 权重 1/5，后来都为9，现有 History 相同而过去分数不同；只保当前字段无法恢复过去；存有未清租约或待唤醒动作时重跑初态会错误重置 |
| CP-06 | 保存兼容与原子恢复 | 独立格式版本；旧档是否迁移、哪些迁移器和不支持版本；解析/校验完成后发布恢复状态；恢复 AST、关系、调度、租约和消费账本；失败不破坏当前会话 | 改名和 Forget 后跨进程恢复不应按名字重新绑定；坏引用或不支持能力必须在覆盖当前状态前拒绝；重复载入不能重发已提交效果 |
| CP-07 | 推演来源和假设权限 | 仅主体获知图与公开/获准模型；模型生产来源；假设字段白名单；认知/处境假设区别；权威审计的不同 API、日志、缓存和输出类型 | 可见历史和模型相同、隐藏现实不同应返回相同主体结果；用隐藏值先筛模型同样泄漏，即使 Reasoner 参数没有 Essence（D04） |
| CP-08 | 隔离、缓存与停止结果 | AST/Parent/关系/Rules/Procedure/回调的分支引用映射；禁止持源可变引用；cache key 至少覆盖主体/会话/认知/规则/词汇/模型/假设/预算策略；结论与 StopReason 分离 | 已知集合不变但值/来源被纠正，旧缓存必须失效；深度0与无行动不得同一 null 合同；同分支改值99不能改变源图7 |
| CP-09 | 类型化事实与消息 | Event/Observation/Claim/Broadcast/Presentation 的独立类型；actor/subject/target/location/time/阶段/结果/来源/因果父；实际对象与主体所指区分；接收资格和接受回执 | 声称通路开放、收到证词、相信、实际开放、到场五种结果不可互换；翻译文本变化不应改变事实；权威 actor/object 字段不能直接泄漏给主体 |
| CP-10 | 幂等与失败边界 | 唯一 occurrence/intent/phase/commit 标识；重复提交/送达/加载的规则；准备失败、部分提交、取消、commit 后 callback 异常；租约取得对象的清理 | A/B/C 先后占岗且从未重叠，历史完成集合不能当共同成功；瞬间交集不能证明连续保持；两个消费方收到同一效果不得各提交一次 |
| CP-11 | 正式能力/IR 协商 | 能力名、版本、类型、执行侧、副作用、批准引用、必需消费方；未知主版本/必需能力/语义字段拒绝；兼容 adapter 清单；加载前无副作用校验 | 添加未登记 effect/handler、未识别 facet 字段不能被忽略后继续；“declare generic relation/rule”不能展开任意 callback；Core 可构造不等于作者端已支持 |
| CP-12 | 表现单向边界 | C02 世界/消息事件与 C08 呈现事件的关联方式；发生 ID、字典运输、来源保真；legacy packet 适配和 required resource 错误；显示端只消费成立结果 | 重画/切语言/历史回看同 occurrence 不新播放；同 source 新 occurrence 可播放；声音启动不能制造 NPC 已听闻、Observation 或时钟变化 |

上述是待主代理具体化的选择和负面用例，不是本轮新增测试、黄金预期或设定判断。

## 提供方、消费方和版本接口

下表的源码定位来自允许读取的 F01/C08/诊断材料，本代理没有打开这些生产文件，也未验证其当前字节。完整提供/消费方签认仍未取得。

| 公共边界 | 已知提供/消费方 | 当前已知版本 | 锁定前尚需给出的适配接口 |
| --- | --- | --- | --- |
| 实体/切面/主观引用 | Model/WorldRuntime → WorldSession/Attach/WorldBehavior/CoreScriptWorld；AstDecoder、ActionContext、Inspect、Reasoner | Core API 能力版本未声明；world/v1 | 注册与查询 DTO、stable handle、主体视图权限；多切面入口；旧单表现入口的保留规则；originating Facet 丢失时明确失败/legacy 行为 |
| 中文 AST | story_script/world_script/AST 绑定 → WorldSession/WorldBehavior、两章适配器、Decision/Reasoner | world `dual-ontology-world-script/v1`；公共词汇版本未声明 | 作者 term→主体解释→现有 Noun.Reference 的三段协议；Context、版本凭证、类型化解析状态；拒绝未消歧词进入首项匹配 Reasoner |
| 一致快照/历史 | Model/Cognition/WorldRuntime → Ast.Evaluate、DecisionSystem、Reasoner、词典、连续会话和保存 | 正式认知 revision/history codec 未声明 | 所有写入口及读消费方覆盖清单；immutable Rules/Procedure/关系；版本失效和历史读取权限；菜单 choice Version 不替代认知版本 |
| 保存/恢复 | 世界/认知/调度/临时状态 → GameSession、两章会话、词项句柄/AST、planner continuation | 完整持久 codec/loader 未核验；专章快照不代表 save | 明确持久 provider/loader 的精确文件后才能锁；迁移/拒绝矩阵、无原态重播、恢复原子发布、活动执行支持/拒绝、随机过程是否精确续演 |
| 主体推演/权威审计 | Reasoner/规划与模型生成 → 两章推演适配器、自动玩家窄接口 | 公共用途/结果格式未声明 | SubjectiveSnapshot/Hypothesis/Model/Budget/Result 类型与版本；模型来源和 oracle 入口权限；分支映射、停止原因、缓存失效；未读深层回调不得宣称隔离通过 |
| 世界/消息事件 | SimulationSession/ActionRuntime、Observation、Say/Social、广播/通行 → ChapterProtocol、社会/议事/传播/通行、UI、audit | F01 记录数种独立协议，无统一因果格式；章字典和对话 packet 未版本化 | 每事件 kind 的必需字段/单位/来源合法性；世界→主观→消息→呈现的有向投影；老消费者适配顺序及必需能力；不直接导出权威全真字段 |
| 能力/IR | 正式编译器/loader/获批 Core 能力 → .story 绑定、WorldSession、两章 adapter | F01 `static-capability-audit/F01/v1` 是审计格式；不是运行时能力版本 | provider/runtime capability 发布、compiler 要求和 loader 拒绝共同锁；未知字段严格性；没有批准编号不能把审计 SUPPORTED 当授权 |
| 资源/呈现 | 正式 parser DTO、资源生成器、宿主 packet producer → Chapter1Data/GameEngine/ChapterDialogue/Sound | 当前资源/packet 无语义版本；C08 两个 v1 仅草案 | 保持 C08 T1 当前 shape、T2 旁路、T3 opt-in 边界；GameEngine record→ToString 不能充字段保真；F01 G05 的 line_id/source_id 先补既有复制 |

C08 的非因果边界、正式 parser 单来源、基础字典运输、at-most-once initiation 和首批不升级公共资源格式均清楚。C08 自己也没有闭合完整音频 producer/registry 和编译序列化字段，不能将其候选 file-plan 直接签为完整租约。C02 与 C08 应统一 `source_ref` 和世界事件到呈现 occurrence 的关联字段，但不得把两种事件混成一个权威事实通道。

旧 world/v1 中 aliases/profile/source_id 可能被通用 DTO 忽略（F01 G03），这说明新 IR 不能沿用“未知字段默认忽略”的行为伪称兼容。InteractionPacket 的一般 object truthiness（F01 G23、D01B）也不能接收新解析结果对象后把它当 True；应显式检查成功解析和 `Truth.True`。

## 准确依赖与未来任务卡建议

以下只是分派建议，没有审批、文件租约或读域扩展效力。

1. **身份/词汇词典统一稿（C01/C03）**：F01 + D01A/D01B；依赖最终 A 语义裁决。先解决 CP-01/02/03 和两候选的 Referent 含义冲突；为编译器、WorldSession、AST、Inspect 和 Reasoner 消费方给出同一版本矩阵。不得让直接 Core API 实验代替 H01 作者闭环。
2. **认知提交/历史/保存分卡（B）**：F01 + D01 + D04；分别固定 CP-04 的一致读、CP-05/06 的历史/codec/恢复。保存卡必须明确 AR 的本批范围，不能沿用 D01B 的 future-only 范围。没有旧档/完整引用/调度合同前不承诺保存交付；如只支持静止切点，必须由最终批准范围明确允许，不能把活动状态默认清零。
3. **事件/能力合同（C02/H01）**：F01 + D05 + A/B 的身份/版本合同；固定 CP-09/10/11 和社会/广播/通行消费方。H01 同时检查 compiler Parse→IR→Bind→Execute，不新增章专用 Handler 或平行事实执行器。
4. **现有组合修正（C04/C05/C06/H02）**：D02/D03/D05 + 已锁定 C01/C02/C03；作者适用范围、邀请槽、来源复制等现有机制修正可分开形成精确任务。连续共同保持或全组原子提交不得潜入这张卡。C07 的探索失败不应把已可复用的基础组合整体绑死。
5. **主体推演（C07/V01C）**：D04 + C01/C02/C03/B；先锁 CP-07/08 和用途类型，再处理两章主体 adapter；模型来源、深层回调、预算执行位置未确认就保持 NOT_VERIFIED。主体自动玩家只接窄玩家接口；R-ORACLE 不分发给实现者。控制器追加的测试要求是使用虚拟侦探在正式 story 声明处顶替玩家位置；V00/V02 必须包含正式声明的主角与玩家可见候选→决策→真实提交闭环，离线 DTO fixture 不能据此宣布完成。
6. **资源/呈现快速线（C08/M06）**：F01/F02 + C08 草案；T1/T2 可保持与决策无因果交叉的独立线，T3 依赖来源字段保真及实际 producer/registry 精确读审。若改变 UI，按适用级别另测；S18 仍 LOCK。

用户批准 A–D 后，仍需每一张卡的 provider/consumer 复核、确切文件、版本锁和控制层有效租约；本报告不把模块表变成 `allowed_writes`。评审稿 §8.4 R01 依赖每个 C/H/M 任务的实际提交，本次没有 diff/提交/负面实现案例，因此只完成其准备资料。

## 必须阻断的实现形态

- 在词项解释、评分、模型生成或缓存中查询隐藏客观真值；用 UI 遮盖全真推演结果；把权威 audit 结果自动变成主体知识。
- 依据 Forget、revision 更新、词典变化自动撤销已经提交的身体动作，或把已绑定目标按同名重新解析；这超出 AR 已明确保持的连续性。
- 保存载入重跑内容初态、默填丢失的规则/调度/临时状态、重新感知全真后覆盖保存的主体图，或在完整校验前破坏当前会话。
- 万能 opcode、自定义 effect→callback、反射/脚本、任意 AST 子类、章/角色/纹理特判；以“声明关系/规则”为名绕开能力枚举和执行侧权限。
- 把发言/接受/资格名单/历史完成集合当世界提交、共同保持或实际到场；把普通 Set 的取消残留用隐藏清理特判补掉；再造现有事务/信任公式。
- 通过 unknown-field 忽略、默认 object truthiness、未锁版本的老消费者继续处理新合同；用更新黄金预期消除失败；触碰 S18 的采集/回放。

## 原始失败、访问和验收披露

D01A 首个 alias probe 缺必需 label；D03 初次输出目录导致重复程序集特性及夹具警告；D05 初次误用 Projection.Source；D04 和 D01B 的早期构建/环境记录均维持在各自目录。本代理没有重跑、清除或覆盖任何失败。D01B 的证书环境副作用、D05 的相邻源码额外输出是已读诊断中的披露，不由本次审查消除。AR 提及 flow/flow2 27 项失败，本代理没有读取/重跑其原始回归，仍标二手未验证，不能在本轮宣布解决。

本次最初被误派为 R02，主代理随后更正为 R01-ContractsPreparation；更正前未写文件，R02 写授权已撤销。首轮仅按指定文件名发现证据路径，输出也列出了未读取的其他报告路径；没有沿路径读入它们。solution-review 只向审查上下文显示标题索引和附表列出的合同/权限/任务/审批段，读取实现曾把整份文件装入 PowerShell 内存以切片；已如实登记，不声称技术上实现局部文件读 ACL。三次聚合结果发生输出截断，后续按具体文件/字段补读关键依据；这些截断是本次取证输出的限制，不是产品测试失败。

封存前控制器另告知：当前评审稿相对旧 pack 仅在开头增补虚拟侦探的正式玩家位置测试要求，当前原始 SHA256 为 `3e53904168303c99b8d9cef5c109bfd971b51c7455ca067a391b0df57c0bce66`，旧 pack/base hash 继续保留；两个生产补丁已经集成，当前 HEAD 为 `5c678a85`。本代理没有读新版生产源、核对补丁或独立验证此 HEAD；以上是控制器提供的状态，不能当作本次逐提交 R01 通过或 A–D 批准。完整评审稿 hash 以本目录实际扫描值为准。

没有读取 docs/、任何项目设计文件、.story、黄金/oracle、历史盲玩或生产源码；C08/F01 的报告内出现的文件名、资源键形和源码定位只作为接口证据，不据此作剧情结论。没有新测试、编译、构建、游戏、资源导入、图像/UI 查看、盲玩、Git 操作、生产修改、规范修改或审批写入。所有本代理写入只在本目录。

| 类别 | 本轮结论 |
| --- | --- |
| 构建 | 未执行；不继承诊断子任务的通过作为本轮构建结论 |
| 机制 | 仅静态准备审查；A–D 尚未批准/实现，G-CONTRACT 尚不满足 |
| 人物行为合理性 | 未验证；未读取设计或剧本，不作内容判断 |
| 游玩体验 | 未验证 |
| 一级/二级/三级验收 | 均未执行 |
| R01/R02 完成性 | 只完成 R01 预实现准备资料；逐提交 R01、R02 均未执行 |

证据文件：[contract-review.json](contract-review.json)、[read-scope.json](read-scope.json)、[input-hashes.json](input-hashes.json)、[delivery-hashes.json](delivery-hashes.json)。输入哈希只固定审查读取时字节，不证明 Git 归属、审批真实性或之后未被其他共享任务改动。仅对交付 JSON 做了静态解析检查，未运行产品测试。
