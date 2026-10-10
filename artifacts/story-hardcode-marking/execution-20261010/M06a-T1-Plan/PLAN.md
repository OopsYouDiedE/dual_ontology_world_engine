# M06a / C08 T1 精确实施计划

状态为 `PROPOSED_NOT_A_LEASE`，环境为 `DETECTION_ONLY`。本目录仅收敛计划与只读证据，没有生产写、生成器执行、构建、测试、Godot 或 Git 操作。主代理提供的生产基线为 `232bb99aab6d0caac9dcd14edbead0f27347aefc`；本代理未自主用 Git 验证。根/模块 AGENTS 只作流程依据，未沿链接读取设计规范。本计划沿已批准 `AR-CORE-01` 与 `control/contracts-phase02.md` 的 T1 边界；前两章约 3–5 游戏日，40 日仅留未来命名空间。

**先签 T1a，T1b 另签独立租约。** 最小落地不修改 compiler、Core、运行时 C#/GDScript、音频、DSL 或公共资源格式。所有迁入文字逐字来自现有 Python 源字面量或已声明的作者表；本目录用源行号与 UTF-8 文本 hash 标识原值，不另存第二份剧情文案。精确路径在 [file-plan.json](file-plan.json)，69 条关联在 [bindings.json](bindings.json)，当前字节/结构证据在 [baseline-evidence.json](baseline-evidence.json)。这三份文件是待审计划，不能用其中状态字段代替租约或人类授权。

## 正式解析与序列化已经闭合

| 资源 | 正式入口与实际字段 | 本轮迁移影响 |
| --- | --- | --- |
| 第一章房间名 | `compile_story.load_source`（59–61）调用 `story_chapter1.load`；`TABLES`（116）映射 `ROOM_NAMES`，1331–1364 读取/合并，1376–1379 形成章节 DTO；`content_data`（266–278）将内容表写到 Content JSON 根 | 六个 room label 复用 `generated_awakening_content.json:ROOM_NAMES`；不新增或修改房间名表的既有值 |
| 第一章家具/地图旁白 | `TABLES`（117）映射独立属性 `FURNITURE_ASIDES`，不在 `CONTENT_KEYS`（125–128）；`compile_story.layout_data`（345）显式输出到 layout 的 `furniture_asides` | 新九键只改变 `generated_awakening_layout.json:furniture_asides`，不能误从 Content 根查找 |
| 第二章资源文字 | `compile_story.generated_files`（470–473）调用正式 `story_script.compile_story(..., section="chapter2")`；`source_lines`（157–248）选择同名续篇并保留物理来源；Translator（404–405、421）收集设计块并调用 `compile_design`；后者（316–320、333–334）平铺 `文本` 到 `design.text`；正式 `ws.Compiler.parse`（965）后在 978–983 将设计挂到对应 chapter | 新 54 字符串键仅改变 `generated_chapter_02.json:chapters[id=chapter2].design.text` |
| 翻译台词 CSV | `line_table`（400–427）仅收集 `op=line` 的 `line_id/text` 与 `label_id/label`；输入来自对白/续篇/话题（476–477） | 普通房间名、家具旁白与 `design.text` 资源键不触发 `note_line` 或新台词行；`generated_lines_zh.csv` 预期原字节 |

compiler 的全部七个正式输出已由 487–495 行确认：Awakening Script 下的 `generated_awakening_dialogue.json`、`generated_awakening_content.json`、`generated_awakening_layout.json`、`generated_chapter_01_world.json`、`Chapter1CompiledDialogue.cs`、`generated_lines_zh.csv`，及 Chapter2 Script 下的 `generated_chapter_02.json`。现 CLI 515 行会逐文件写出；因此租约需要逐项列出这七条路径，并将本批之外的六份限定为原字节。不得在独立 clone 中签广义目录通配写权限，也不为减少输出触碰而改 compiler。

**来源元数据的实证限制：** `story_base` 21–36 行及 `story_script` 107–154、963–978 行支持 include 的源文件/物理行诊断与 world source map；但 `read_text_map`、TABLES/map 与 `compile_text` 的返回值只有键/文字，没有资源键到源行的 DTO。T1 不新增索引/来源格式。九键和 54 键的迁移源定位由本轮绑定证据记录；不能声称新 `design.text` 键会自动拥有独立 `source_id/line_id`。

## T1a：九旁白与六房间标签

生产源仅允许三条：`story/第一章/热点与布局.story`、`tools/build_awakening_map.py`、`godot_csharp/AGENTS.md`。现 `提示与站位.story` 的 `家具旁白` 表只读，不改。新增块追加在 `热点与布局.story` 现 EOF（222 行）之后，沿既有 `家具旁白` 语法续表；`merge_table`（1141–1150）负责跨块重复拒绝，所签九键必须不同，不能依赖当前同块字典推导去发现已覆盖的重复。

固定锚点与源键采用 `map.awakening_yi.<slot>`。它标识本批既有地图资源槽，不冒充从路径猜出的章节 ID。槽身份由审阅后固定的关联明确给出，文本由源键查表，坐标只负责几何。现输出 `[rect, text]` 的九个槽顺序固定为：

| 现输出序号 | 稳定槽/source key 后缀 | 原文字源行 | 几何矩形（仅几何） |
| --- | --- | --- | --- |
| 0 | `window.EN` | `build_awakening_map.py:196` 的既有 body 字面量 | `[1138,38,28,36]` |
| 1 | `window.R05` | 同上 | `[914,38,28,36]` |
| 2 | `window.R08` | 同上 | `[690,38,28,36]` |
| 3 | `window.hall` | 196 行的既有 orelse 字面量 | `[431,38,34,36]` |
| 4 | `rug.R08` | 199 行的既有 orelse 字面量 | `[680,136,56,72]` |
| 5 | `rug.R05` | 199 行的既有 body 字面量 | `[904,136,56,72]` |
| 6 | `rug.R07` | 199 行的既有 orelse 字面量 | `[680,552,56,72]` |
| 7 | `rug.R06` | 199 行的既有 body 字面量 | `[904,552,56,72]` |
| 8 | `slippers.R05` | 200 行的既有字面量 | `[968,168,9,8]` |

这些序号只是在未携带 anchor 字段的现有配置中定位已签槽；anchor/source key 不由枚举次数产生。后续重排、增删或几何配方变更必须重新审阅关联。正常生成分支的四个 window 槽和四个 rug 槽使用显式固定的源槽列表取文字，删除 `cx > 600` 与 `fw[1] == ...` 选句。几何仍按原 decor/floor 配方求出，不改变装饰 tuple、像素算法或场景节点；不为 T1 改 `chapter2_art/rooms.py`。

六个标签关联固定为 `R08→ROOM_NAMES.R08`、`R05→ROOM_NAMES.R05`、`EN→ROOM_NAMES.empty`、`R07→ROOM_NAMES.R07`、`R06→ROOM_NAMES.R06`、`ES→ROOM_NAMES.empty`。原表源为 `热点与布局.story:197–200,206`。ROOMS 四元组第四项改存作者表键，所有 owner/left/north 原值保留；生成 layout 时才取实际显示文字。地图 recipe 的未输出 title、owner、纹理/演员专用绑定不在本批，不将其算成完成归一。

新增最小 `build_awakening_map.py --text-only` 入口：读取正式 compiler 已写出的 layout/content JSON，只接受 `furniture_asides` 九个已签字符串键与上述房间名键；读取现 `map_layout.tres`，按固定槽替换文字与按 room id 替换标签。先检查完整槽数、room 集合、值类型、缺键与当前基线，再生成候选；几何只作不变校验，不能作为选句条件。只允许写 `map_layout.tres`，相同字节不必写；此模式不得调用 `plan/paint/rewrite_scene`、打开图片或写场景/PNG。正常全量模式也改为按键读文字，但本批禁止执行全量模式。

预期实际变化只有编译 layout 新九键；`map_layout.tres` 文字逐字相同，整个文件应字节不变；Awakening 场景及全部资产仅 hash，不在输出写租约内。现布局虽无 anchor 字段，仍保持原 `[rect,text]` shape，不新增 v1 index。

## T1b：十四室内标题与四十场点标签

本批另签。生产源为 `story/续篇/07_家具与文本.story`、`tools/chapter2_art/rooms.py`、`tools/build_chapter2_scenes.py` 与同一 `godot_csharp/AGENTS.md`。它不扩大 T1a；签约时必须对 T1a 合入后的实际基线重新冻结 compiler 与配置hash，不能把本目录初始 layout hash当成后续未变标准。十四地图精确为 `living_hall`、`zone_jia`、`zone_yi`、`zone_bing`、`zone_ding`、`central_hall`、`lecture`、`archive`、`counterweight`、`resonance`、`clinic`、`logistics`、`granary`、`greenhouse`。每个场点由现 site id 区分，重复的 `watch_point` 以 map key 命名空间区分；40 条具体源行见 bindings。

新增文本块追加到该文件现 EOF（418 行）之后，保留 `续篇 chapter2` 内缩进。沿既有 `文本` 分组语法声明 14 个 `资源.地图.<map_key>.标题` 与 40 个 `资源.场点.<map_key>.<site_id>.标签`；所有值从 `rooms.py` 对应现字面量逐字迁入。四个 zone 的现 title f-string 展开只使用已声明 `ZONE_KEY` 对应的原 label；这属于现有文字迁移，不创作新标题。`zone_yi/bed` 的原标签也必须逐字保留，不能顺手翻译或润色。

`rooms.py` 的内部 recipe 改用 `title_key` 与 `label_key`，保留 key/site id、point、rows、regions、floor、wall、furniture、exits、spawns 及所有绘制参数。生成器从正式 Chapter2 `chapters[id=chapter2].design.text` 解析并校验全部54键，投影为旧 room `title` 与旧 site `{id,point,label}` shape；不得把临时 `*_key` 泄漏进 manifest。现模式的 metadata/title（144）、metadata/label（176）以及 manifest（235–243）仍接收原形字段。

新增最小 `build_chapter2_scenes.py --text-only <map_key>...` 入口，严格只接已签十四室内键。读正式 compiler JSON、现指定 scene 与 manifest，按明确根节点/`Sites/<site_id>` 定位 metadata/title、metadata/label；完整校验每个目标恰好一个匹配及 site 集合后再发布候选。保持所有其他 scene 原始文本字节和 manifest 数据/序列化字节；相同候选不必写。此模式不构建/绘制 rooms、不执行 outdoor/sprite/painter，不重写 `Sites` 到尾部或重算站位；它不需读室外 recipe。现 `--sites-only` 只更新标签，且会重写 Sites 到 EOF，不能代替本批 title+label 入口；它改为消费同一个正式文本解析结果，仍不由本计划解锁实际执行。

只允许十四条 `Maps/<key>.tscn` 与 manifest 作为此入口的输出路径，全部预期字节不变。另六条室外场景（`campus_n/ne/nw/s/sw`、`harbor`）不可写；图片、props、导入文件不可写。compiler 仅 Chapter2 `design.text` 新54键允许变，其他6输出原字节。

## 已发现的全量重生边界

当前布局的前三个 window x 是 `1152/928/704`；静态按 `rooms.py:122,125,165–177` 的36格镜像再按 `build_awakening_map.py:117–118` 加128，可得 `1184/960/736`，相差32；第四窗均为448。源 hash 与 C08 证据一致。本代理未执行生成器，故只登记**静态来源推导与现配置不一致**，不声称已测得生成 diff 或其成因。T1a text-only 保留现几何，因此不会将该差异纳入文案迁移。完整重生的几何/像素一致性仍待单独核对；不能据本计划声称正常全量模式已经字节等价。

所有现 `.tscn`、`.tres`、manifest 全字节 hash 及非文字结构 hash 均已记录。691 个当前场景直接引用的资源（包括脚本、SpriteFrames 与纹理）只做路径/字节 hash，没有看图或沿代码读取；它不是世界所有资源的完备索引。全资产“不变”的执行规则由禁止资产写路径实现，不能把这份直接引用清单冒充 T2 完整资产目录。

## Godot AGENTS 必须同步的管线条目

实施批在 `godot_csharp/AGENTS.md` 第四节加入实际命令与行为（只写流程/字段，不抄剧情）：

1. 先运行正式 `python godot_csharp/tools/compile_story.py`，随后 `python godot_csharp/tools/build_awakening_map.py --text-only` 从编译 layout `furniture_asides` 和 Content `ROOM_NAMES` 更新既有地图文字；该模式不写图片或场景几何。
2. T1b 实施后登记 `python godot_csharp/tools/build_chapter2_scenes.py --text-only <室内地图键>...`，从正式 Chapter2 `design.text` 更新既有 title/site label；明确许可地图、manifest输出、完整校验与几何/资产不变规则。
3. 修正现 `--sites-only` 的“从 rooms.py 更新”描述为“几何来自 rooms.py，文字来自正式 compiler design.text”，并保留其现用途与尾段重写限制；title 用新 text-only 入口。
4. 通常全量资产重生与 Godot import 仍按现管线执行；本次迁移的租约不包含它们。纯本模块管线同步不需修改根 AGENTS。

以上是待实施内容；本次没有修改 AGENTS。不得将九句、标题、人物名或剧情结论写进 AGENTS。

## 独立验证与压力要求

实施者只生成待审变更，验收交由全新独立上下文 Agent，禁止读设计文件。按本批源码/编译输出比对，精确允许的新增键以外必须原字节；新入口输出所有地图/config字节应原样，所有几何/场景节点/资源hash不变。正式编译 `--check`、相关构建、自动回归与 UI 所需独立检查另由主代理下发，失败原始日志保留；本计划不运行或宣称任何级别验收通过。

T1 针对资源查找/发布的独立压力验证应记录环境、种子、规模、操作数、耗时、内存、异常与原始日志，覆盖重复调用、缺/重复键、未知map/site、错误字符串类型、全批校验失败不得半写，以及在隔离 fixture 内修改几何参数时同source key仍取同文字。中性 fixture 结果不能冒充正式3–5日游戏stress；正式千种子目标由批准的虚拟侦探链单独执行，不在T1静态计划中虚构可达或PASS。资源T1不包含音频竞争/cancel消费验证；该项仍属于未来T3或其他已获批租约。

构建：未验证。机制执行：未验证。人物行为合理性：未评价。游玩体验：未评价。一级、二级、三级：均未执行。该结论与静态绑定/来源闭合相互独立。

## 本轮实际读取范围

源内容为明确授权的两个地图生成器、`rooms.py` 的资源绑定/几何相关段；`compile_story.py` 1–65、266–350、400–497；`story_base.py` 1–97；`story_chapter1.py` 1–36、70–155、400–418、1141–1199、1207–1218、1330–1380；`story_design.py` 1–24、205–237、281–337。四份许可 `.story` 只研究既有表/资源绑定，未读其他剧情片段。

主代理额外窄读授权的 `story_script.py`：107–165、165–190、202–205、227–249、390–408、421–423、931–984，限 include/source、section选择、设计块调用与挂载桥；没有执行/import该模块或阅读其他剧情分支。读取其390–408段时仅用途是定位 DESIGN_BLOCKS 调用，其中 schedule匹配语句没有作为内容依据。配置仅结构解析；compiler七输出只hash未浏览内容。历史试玩、黄金/oracle、S18、设计规范均未读。
