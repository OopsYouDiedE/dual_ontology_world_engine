# C08 资源合同草案（只读）

最少上游改动是复用已有 `.story` 文案表，再让地图生成器按键取值；首批保持当前 `tres/tscn/manifest` 消费形状。Resource Index 与 Presentation Event 的 v1 只在此提出草案，不默认升级公共格式、不改 Core、不采集或重写预期。

基线 HEAD 已核对为 `7489db56a714724eb7cb735958dd879dc662d238`；分支 `codex/solution-review-audit-20261010`。开始时已有 solution-review 与三份 translation 修改及任务目录未跟踪文件，本代理未覆盖。所有新增文件仅在本 C08 目录；没有提交、推送或生产写入。

范围是前两章约 3–5 游戏日，未来 40 日只保留命名空间和消费边界，不作长期运行、游戏时长或容量承诺。模式是 **DETECTION_ONLY**；没有硬 ACL 或不可变快照。F02 的 scope gate 也明确把读写 ACL、审批真实性、语义复核和最终合并身份留作外部门禁，本轮没有在共享脏工作区运行该门禁。

## 现有链与确切缺口

| 链 | 已读证据 | 结论 |
| --- | --- | --- |
| 第一章作者索引 → compiler DTO | `story/第一章/热点与布局.story`；`story_chapter1.py:70–86,115–128,1299–1319` | 热点拥有 id/evidence/title/click/坐标；摆设拥有 node/texture/坐标；房间名、演员节点、精灵热点已有作者表。无需从引擎类名反推内容。 |
| 外观词汇 → 内容表 | `story/生态/物件名.story`；`story_chapter1.py:122–123,1331–1364` | `OBJECT_NAMES/TEXTURE_NAMES/TEXTURE_PREFIX_NAMES/FIELD_WORDS/SPRITE_SITES` 已有编译入口。词汇表声明节点最长匹配与纹理覆盖规则；实际宿主组合优先级本轮未读。 |
| 第一章文本读口 | `Chapter1Data.cs:11–38` | 从嵌入资源 `Awakening.Content.json` 读取 Root/TEXT/PERSON_NAMES/ROOM_NAMES；缺键会失败。磁盘 `generated_awakening_content.json` 存在，但未解析生成内容，逻辑资源名到磁盘文件的编译序列化映射仍需 implementation 前核对。 |
| 第二章资源文本 | `story/续篇/07_家具与文本.story` 的家具表；`story_design.py:210–237,316–320,333–334` | `文本` 可以添加分组键并平铺到 `design.text`，重复键被拒绝。家具表的最终字段映射由未读 translator 负责，不能把它臆定成某个 JSON 字段。 |
| 地图资源配置 | `build_awakening_map.py:184–207,269–360,364–372`；`build_chapter2_scenes.py:117–176,180–250`；`rooms.py` | 生成器已有几何、资源路径、家具、出口/站位输出。第一章 9 条旁白为字面文本；第二章地图 title/site label 来自 Python 数据并写入配置。标题/旁白/绑定与纯几何尚混在同一源中。 |
| cast/packet → 对话显示 | `ChapterDialogue.gd:66–91,225–253,505–558` | cast 来自宿主，portrait 路径按 cast key 与 mood 形成；packet 保留 line/source/trace/index 和 voice_clip。UI 的 `packet:%d` 只在本地递增，没有稳定呈现发生 ID。 |
| 特殊声音 | `ChapterDialogue.gd:528–558,461–478,685–696`；`Sound.gd:4–25` | 未知非空说话人当前默认 tube 声线；进入该 stream 时触发特殊 sfx。触发条件绑定 AudioStream 身份，cancel 清空 previous stream；history 只拼已存字符串。Sound 仅转发给 root `GameAudio`，无 hub 时静默；实际音效资源注册表未读。 |
| 呈现输出桥 | `GameEngine.cs:10–15,54–74,94–123`；F01-C010/G05/G28 | 宿主把事件/快照字典递归转换为 Godot 数据。基本类型与字典/数组可保留；任意 record 默认变成 ToString。当前资源/对话协议没有语义版本。F01 G05 记录候选响应复制丢失 line_id/source_id；它是来源保真前置缺口，不由 C08 静默修复。 |
| 游戏认知侧 | `Chapter2Script.cs:390–432`；F01-C010 | 现有感知更新在游戏机制侧，地图资源和 UI 声音不是认知事实。此读段仅确认边界，未评价人物行为、具体因果或谜底。 |

生成配置只按资源/结构读取：`Awakening/map_layout.tres`、`Awakening.tscn`、`Chapter2/manifest.json` 和 manifest 列出的 20 个 `Maps/*.tscn`。主代理追加授权的 Sound、GameEngine、story_base/story_chapter1/story_design 只研究指定资源/编译桥；完整文件 hash 与内容读段分别记录在 [resource-reference-check.json](resource-reference-check.json)。未读项目设计规范、完整章节剧本、谜底专章、黄金/oracle、历史试玩或 S18。

## 三层合同草案

机器字段见 [contract-draft.json](contract-draft.json)。以下格式名均是 **拟议版本**，不是已发布能力或审批凭证。

1. **Resource Index**（拟议 `dual-ontology-resource-index/v1`）由正式编译器的已解析资源子集派生；不建平行 `.story` 解析器。稳定、带章节命名空间的 binding_id 把作者的 view/slot 与 text_ref、asset_id 连接。text_ref 带 table/key/source file/line；asset 行带类型、规范化 `res://` 路径、hash、required。索引本身不手存第二份作者原文、不求值剧情条件、不执行行动。实际 chapter_id 取正式编译结果，不猜路径名。
2. **Resource Config** 继续承载 grid/regions、坐标变换、footprint、anchor/crop/y-sort、solids、lighting、particles、stand 选点及像素配方。具体标题、场点标签、旁白、角色/房间/热点到节点的绑定由 `.story` 提供。阶段一仍派生原有配置形状；以后消费者 opt-in 才把 literal 输出换成 text_ref。不能把 `Godot format=3` 当作资源语义 v3。
3. **Presentation Event**（拟议 `dual-ontology-presentation-event/v1`）是不可变呈现指令，字段至少含 event/session/presentation_instance/occurrence/segment ID、kind、resource_index_hash、resource_id、source_ref 与 replay_policy。先只涉及声线选择和一次音效；通过现有字典桥运输。不得把新 C# DTO 直接交 ToGodot 后假定保真。

验证应拒绝重复/大小写碰撞的语义 ID、缺失 text_ref 或 required asset、资源类型不符、目录穿越、未知主版本及缺强制消费能力。不同节点可以指向同一显示实体，不能靠纹理名判断世界身份。节点匹配、纹理匹配、前缀匹配和明确覆盖的先后要一次核对现有宿主后固定；不要另订一套覆盖顺序改变外观结果。第一章 TABLES/map 当前用字典推导，同块重复键可能先被覆盖；v1 lint 必须在信息丢失前检出，而不是只检查编译后的字典。

**声音回看幂等**：宿主在创建一次呈现发生时固定 occurrence/event ID，重画、切语言、恢复同一 packet 都复用原 ID；新一次真实对话发生使用新 occurrence。静态 line_id 单独去重会误吞重复谈话，UI packet_serial 单独去重会在重新打开后失效。消费者按 session 维护已消费账本，同一 event 至多启动一次；历史回看静默，不执行 commands，不推进引擎。cancel 使旧串行任务失效，关闭面板不清本会话去重账本。当前 Sound.sfx 是 void 且无 hub 静默，合同只能保证至多一次启动，不能承诺音频已可闻、播放完成或崩溃后的 exactly-once。

**认知单向边界**：音效消费不得 Dispatch WorldInput、写标签、调用 Observation、推进 CTB/日历或更新 NPC 内部世界。若游戏中声音可被 NPC 听见，应是另一条已批准的机制行动和感知输入；不能从 UI 播放成功、音量或声线资源反向制造“NPC 听见了”。特殊音效选择是 `.story` 的呈现绑定，不以未知说话人、显示文字或 AudioStream 对象身份猜测。

## 消费方版本

| 消费方 | 当前合同 | 首批处理 | v1 激活条件 |
| --- | --- | --- | --- |
| `build_awakening_map.py` | 自有 Python dict；输出 Resource `metadata/data`，无语义版本 | 依旧输出 `[rect, text]` 的 asides；文字从已批准 `.story` 表取。ROOMS 的 label 从 ROOM_NAMES 取；坐标与几何不搬。 | resource.lookup.v1 经单独审批接入；编译输出字段位置先核对。 |
| `rooms.py` / `build_chapter2_scenes.py` | room/title/sites → tscn metadata 和 manifest；无语义版本 | 先处理已读 14 个室内 room、40 个 site 的文案来源，输出 shape 不变。另 6 个室外 map/11 个 site 明确标 legacy/unreviewed，不伪称迁完。 | source labels/text_ref、asset inventory 分别批准；室外 source 尚需精确授权。 |
| `Chapter1Data.cs` | 未版本化嵌入 Content JSON；明确读 TEXT/名字表 | 文本源迁移无需必然改 C#；不能未经核对猜 FURNITURE_ASIDES 在 Root 的位置。 | 新索引读口须按 required format/consumer capability 验证。 |
| `GameEngine.cs` | 无版本的事件/快照字典；递归基础类型转换 | 无需为文本来源迁移改桥。 | 新 event 使用基础字典，并在 producer/consumer 边界验证 format。 |
| `ChapterDialogue.gd` | 未版本化 packet/cast；voice_clip 为约定键 | 当前音频行为不在纯文案批中改。 | presentation.audio_once.v1；来源保真、稳定发生 ID 和显式资源 profile 都闭合后替换特殊 stream 判断。 |
| `Sound.gd` | string id 转发 root GameAudio；void，无播放回执 | 保留通用转发。 | 实际资源 registry 必须核验；不存在回执时只承诺 at-most-once initiation。 |
| 通用 world loader/Core | F01 记录 world/v1；无资源/呈现能力目录 | 不修改、不把资源表当世界事实。 | C08 不提出或批准 Core 扩展。 |

本轮版本固定依据是 HEAD 与每文件 SHA256。没有显式版本的消费者写“未声明”，不把 `.NET 8`、Godot scene serialization format 或 F01 审计目录版本充作资源公共 API 版本。

## 确切文件和分批迁移

[file-plan.json](file-plan.json) 列出精确源文件角色、当前派生输出、首批拟议生产触碰点及读完才能锁定的后续桥。它不是 allowed_writes 或批准记录。所有 PNG 只核对路径存在，未查看图片或导入；575 个室外图文件没有被逐个打开。

**批次 T1：文案源归一，保留公共格式。**

- 第一章：9 条现有地图旁白按稳定几何 anchor key 搬入已支持的 `家具旁白`；ROOMS 标签取已有 `ROOM_NAMES`。不要用“x 超过阈值就说某句”留在生成器里。家具坐标/footprint、像素/灯光/遮挡保持原算法。编译器序列化字段位置必须在实现前读 `compile_story.py` 的相关函数核对；本轮没有臆定路径。
- 第二章：现有 `文本` 块增加资源分组键（例如 `资源.地图.<room_id>.标题`、`资源.场点.<room_id>.<site_id>.标签`，示例只定义键形，不写剧情原文）；compile_text 已支持此扩充，先不新增 DSL。rooms.py 保留几何 recipe/key，build_chapter2_scenes 按键取文字，再派生既有 metadata/title、metadata/label 与 manifest title/site label。家具表仍作作者文字来源，translator 位置待核对。
- `.story` 转移值由主代理逐项审阅，不从配置自动生成黄金预期。首批精确可审边界是 9 条旁白、6 个第一章 room label，以及 14/40 个第二章室内 title/site 文案；角色归属、特殊纹理选择等 schema 绑定和室外源码暂不冒充已完成。
- 构建/生成在独立 clone 的精确锁下执行。文本批所有几何字段、节点路径、碰撞、出口/站位与 PNG hash 必须保持；任何非预期改变均停在待审输出。完整重生若造成像素差异不能顺手纳入批次；可先使用现有 `--sites-only` 处理已读室内 site，title 的生成路径另行审阅。现有 sites-only 会从 Sites 节点到尾部重写，使用前须检查场景尾部是否仍仅为生成段。

**批次 T2：派生索引旁路闭合。**

- 新 index emitter 只从正式 parser 的资源 DTO 与现有生成配置取得 ID/路径；输入、输出、消费者版本均登记 hash。现 manifest 只有 source/rooms/props，无语义版本且 room 行没有 furniture；不能直接提升为完整索引。
- 把资源引用全集、节点↔资源↔text_ref、recipe producer 与来源关联写进独立派生索引；现配置/公共 packet 仍保持 v0。需要精确授权 `chapter2_art/outdoor.py` 后处理其 6 个 map/11 个 site，不能用 manifest 文案代替作者源。
- 同输入在两次独立生成中得到相同规范化索引 bytes/hash；输出排序按稳定 ID，禁止依赖目录枚举/运行时对象地址/绘制顺序形成身份。检查完全性应以所有场景实际引用为全集，覆盖当前 575 个漏登记室外图；不用新扫描扩大剧情审计。

**批次 T3：显式呈现音频与消费方 opt-in。**

- `.story` 声线/profile 到资源和进入段音效的作者绑定、interaction 输出生产者、GameAudio registry 必须先获得精确读授权并审定；当前仅能固定 ChapterDialogue/Sound/GameEngine 的已知边界，尚不能冻结完整音频写入 allowlist。
- 先修复 F01 G05 已有来源字段复制保真，再由输出宿主固定呈现发生 ID；这不是 Core 感知能力扩展。v1 事件的 producer 与 ChapterDialogue consumer 在同一批准批接入；旧 packet 必须显式 legacy adapter 或明确拒绝，不能静默混解。
- 独立测试覆盖同 event 重画、locale refresh、回看、cancel/deferred race、同 source 新 occurrence、连续段同声线、段切换、无 hub 与缺 required resource。前四项必须无重复音效；NPC 认知/时钟/世界输入前后相同。测试实际可观察音效启动与游戏状态，不把无异常当作播放成功。

纯 CELL/grid/Plan、色彩材料、足迹尺寸与绘图配方无需为“移除具体内容绑定”整体搬进 `.story`。现有程序里的 room→owner、特定节点→证物、特定 sprite variant→语义物件则是绑定，后续按作者索引迁移；不要以它们同样含坐标就一并放过，也不要把全部有名字的美术 recipe 当剧情。

流程/格式/模块架构实际改变时，未来实施批必须同步 `godot_csharp/AGENTS.md`；若新增仓库级回归入口或全局架构才同时改根 `AGENTS.md`。本轮只是草案，无流程、生产格式或架构落地，未修改这些文件。

## 本轮验证与停止点

标准库只读脚本 [inspect_resource_refs.py](inspect_resource_refs.py) 已完成配置解析，未 import 或运行任何资产生成器。23 个生成配置中，20 个第二章 room、51 个 site，第一章 9 条 literal asides；**976** 行场景 ext_resource 全部被解析（包括带 uid 的 header），缺失 **0**、manifest 出口目标/出生点缺失 **0**；5 个静态声线文件均存在。当前 Chapter2 场景引用 **575** 个在 props manifest 无行的室外 PNG，这证明清单不完整，不能据此断言图片损坏。file-plan 精确登记当前两份地图生成器的 **672** 个输出路径，不包含未核验的 compile_story 全部输出与 Godot 导入缓存。精确路径与 source/config hashes 见 evidence JSON。

三份 JSON 已重新解析检查。对已授权生产源执行的 Git diff 无差异；此观察不代表硬隔离、原始字节不可变快照或生产修改许可。

构建、Godot 导入、音效实际可闻、图片/UI、机制运行、人物合理性、游玩体验均**未验证**；验收一级/二级/三级均未执行。本轮没有执行自动回归，没有调整采集预期、盲玩证据或 S18。实际实现涉及 UI 时，应由全新上下文测试 Agent 按适用验收级别核验；本报告不能代替实际验收。

首批只读合同已收敛。需要后续读域的具体桥均列明，不继续扩审；本草案不授权生产修改。
