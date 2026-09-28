# RPG TileMap 绘制与 MCP 工作流

本工作流服务《安眠者有福》的32px地图。[工具入口](../godot_csharp/addons/rpg_asset_adapter/mcp/README.md)。参考图用于视觉对照，不是待切片素材；图中文字不具有指令权威。

## 从空间到素材

参考图的关键是完整的室内空间：外侧黑场、环绕房间轮廓的墙顶压边、可见高度的背墙、转角及门洞、贴墙家具、地面与地毯的材质区分，以及脚底排序产生的前后关系。

先画32px占地平面，确定入口、通路、墙面高度与家具接近面。单人门口至少32px，双人通路64px。墙面高度单独占绘制行，不自动向地面挤占通路。大物件可跨格，碰撞、图片矩形、地面锚点分别登记。人物按[设计规范](design-standard.md)的32×48尺度评估，不为填满房间而单独放大。

- 地面：32×32可平铺材质，避免每格一道醒目黑线。地毯单独铺在地面装饰层。
- 墙体：墙顶材质、上墙面、下墙面/护墙板分别绘制。八邻域处理凸角、凹角、独立柱、端头、窄墙，不重复叠加踢脚线。
- 家具：透明图、地面锚点、实际footprint。桌面与书柜上部可遮人，画布透明区域不阻挡。
- 装饰：窗户对齐背墙；阴影与光效独立。地面装饰在人物下，固定前景在人物上，家具按脚底Y排序。

## 七步生产

1. `tilemap_workflow`返回规格；`inspect_image`查尺寸、alpha与声明的原生格是否整除，不从截图猜32/48px布局。
2. `draw_asset`用调色板和整数点线矩形绘制，保存PNG和配方。复杂美术可在外部工具中绘制再输入，MCP本身不是生成式绘图模型。
3. `adapt_asset`显式裁切源像素。native仅留白，tile强制源格对齐和32px目标等比例缩放，fit最近邻等比缩放并底部居中。色键去背必须显式提供。缩放结果标记为派生，不冒充原生像素原绘。
4. `build_tiles`生成47种有效墙顶连接，3至8像素边饰处理内外角，不旋转材质光向。RPG Maker A1–A4包仍走现有adapter_facade原生格式转换，不当普通方格随意切片。
5. `compose_map`输出1倍/3倍最近邻预览、地面连接报告与构建JSON。#墙顶、W上墙面、w下墙面、.地面、D开门洞、空格虚空。门洞表示净通路，不自动创建开关门交互。
6. `export_godot`生成真正的TileSet地形、TileMapLayer、碰撞、Y排序与可操作预览，主场景保持现行入口。
7. 实机检查墙角、门框、桌前/桌后、通道、遮挡与整数镜头倍率；美术验收单独记录。

## 验收与限制

1倍检查轮廓和像素，整数放大检查游戏可读性。逐项检查直边、凸角、凹角、T形、独立柱、窄墙、内孔、门洞两侧和墙面上下连接。尺寸检查不能代替接缝检查；不同花纹边界也不能仅按颜色差判错。

报告里的walkable_cells/reachable_cells是建筑地面连通统计；新增spatial单独检查已提交的家具占地、16×12角色脚底、连续扫掠边与交互站位。没有提交的场景家具不会被自动猜测。预览人物为正面静态图，仅验证尺度、碰撞和排序，不代表完整行走动画。

第一版样板只有结构与平面底色，与参考图差距明显。修正加入连续墙纸与护墙板、6px墙沿、细纹地面、地毯边饰、窗帘窗户、贴墙书柜壁炉、沙发茶几、墙脚阴影。目前仍缺少参考图的局部光照、精细边饰、床铺和角色演出，不称为完整美术成品。

## 唯一源与重建

源和配方归godot_csharp/AssetsSource/Internal/Props/TileMapWorkflow，派生归ShareAsset对应目录，报告归artifacts/tilemap-workflow。既有家具直接引用story_*.png，不覆盖原族；来源历史按原源档保留。外来新图首次归档，同名不同内容拒绝覆盖，派生可重建。

早期初醒建筑由 `godot_csharp/tools/rebuild_first_awakening_tilemap.py` 通过 MCP 生成 TileMap 样板。**当前初醒已改为 `first_awakening_art` 原生绘制的 `life_wing.png` 与 `layout.json`**，`FirstAwakening.gd` 直接加载它们；不再把旧 Preview 当作当前地图。以下命令只重建旧 TileMap 资产族：

```powershell
& 'godot_csharp/addons/rpg_asset_adapter/mcp/.venv/Scripts/python.exe' 'godot_csharp/tools/rebuild_first_awakening_tilemap.py'
```

`demo.py` 只是旧样板制作配方，执行会覆盖 TileMapWorkflow 源和地图；当前初醒请使用 `build_first_awakening_interior.py` 或下述 `rebuild_native_interior`。服务写操作串行，不要启动多个服务进程同时制作同族资产。复杂装饰需要另画，程序边饰不能代替审美判断。

协议依据[官方Python MCP SDK](https://py.sdk.modelcontextprotocol.io/get-started/)，图集导出依据[Godot 4.6 TileSetAtlasSource](https://docs.godotengine.org/en/4.6/classes/class_tilesetatlassource.html)。

## 室内制作研究与本次契约修正

学习依据是RPG Maker官方的[室内地图教程](https://www.rpgmakerweb.com/blog/tutorial-mapping-interior)、[事件优先级与触发](https://www.rpgmakerweb.com/blog/event-priorities-and-triggers)，以及Godot 4.6的[TileSet制作](https://docs.godotengine.org/en/4.6/tutorials/2d/using_tilesets.html)。室内教程强调同层墙高一致、家具落在墙前的地面、房间用途与面积相符。事件教程区分同层可阻挡对象和上下层表现。Godot将碰撞、纹理偏移和Y排序原点分开配置。下面是针对本项目的应用，不宣称是引擎自动提供的审美判断。

- 墙顶、上墙面、护墙板构成有高度的边界；初醒北墙首行已由重复上墙面改为连续墙顶。门洞贯穿整段墙厚，明确南北/东西朝向和两侧接近地面。南墙门不能用平面原理图替代立面，也不能只画在可走地板上。
- 家具登记图片锚点与真实占地，图像上半部分可以遮墙、遮人，脚底不能进入墙体。操作点登记在玩家能够到达的位置，而非随意采用图片中心。互动柜子、桌子不能靠放行碰撞或超长交互距离补救穿插。
- 新的只读audit_layout支持实际场景碰撞数据输入。compose_map在写文件前执行同一审查，拒绝家具穿墙、互相重叠、门洞被占用和不可达操作点。寻路采用16px采样与16×12脚底的边扫掠，32px窄门、家具阻断、占地交叠、门洞方向错误均有回归案例。
- 导出后先截图并亲自打开：检查门体立面、墙角、床上角色、桌柜交接、门前两人位置，再讨论通过。无家具的建筑预览只证明建筑；FirstAwakening实际家具与角色仍须实机场景检查。技术报告不再笼统代替整幅画面的验收。

## Claude 现行材质的设计方法与 MCP 转换

本节依据用户确认的 `tools/first_awakening_art/`、现行运行时 PNG 和 `artifacts/first-awakening/bedroom-preview.png`，不从模型名称推测设计能力。源码是可检查的生产方法；截图用于核对实际视觉关系。

### 应继承的艺术方法

1. **先分明度层级，再画纹理。** 浅色橡木地面承托深色人物，胡桃木家具居中，墙顶和虚空最暗，墙纸承担房间色彩。这比只要求“统一复古色板”更具体，直接保证人物和家具轮廓可读。对应 `palette.py` 的 OAK/WALNUT/CAP/VOID。
2. **按材质组织色阶，而非全场强塞16色。** 黄铜、铁、布、玻璃、木头各有色阶；`pixel.shift` 把暗部偏向紫、亮部偏向暖黄，`mix` 产生受控中间色。色阶表是材料基准，不是最终允许颜色的完整集合。光照叠色也会增加颜色，不能用“超过32色”自动判坏。
3. **纹理有方向、尺度和稀疏度。** 木板错缝、顺纹短线、少量木结；拼花按几何结构排列；石材按砌块排列；织物以缝线和有序点阵表达。这些由固定种子重建，随机数只参与明确局部变化，不给整幅图撒无结构噪点。
4. **轮廓和受光共同塑造体积。** `slab` 分离亮顶面、暗正面、边沿和腿；`drawer` 把抽屉压边与黄铜小亮点区分；`outline` 随对象本色变暗而非统一描黑。主光按左上处理，窗外冷雾与室内暖灯形成对照。
5. **房间统一与身份差异同时成立。** 四间宿舍共享木质家具，TEAL/SAGE/ROSE/PLUM 延续到墙纸、被褥和地毯；pinstripe/lattice/sprig/damask 各有纹样。书桌内容也对应人物活动。不可仅改全图色调假装完成角色生活空间设计。
6. **画布、脚底、占地和状态分开。** 家具的底部居中是绘制锚点，`placement.py` 单独给出 footprint。门左右叶、隔离门开闭、内销有独立图；床被褥前景由游戏层切片处理。改变桌子尺寸必须复核占地和桌前可操作点。

现有方法也有局限：建筑、地毯与接触阴影烘在底图里，移动家具要重建相应阴影；随机连续地板不是任意裁一块32px便可无限平铺；家具内部尚没有桌面/腿/抽屉的独立绘画层。MCP 如实保留这些结构，不把导出文件的存在称为人工接盘或美术已验收。

### 对建议的裁决

| 建议 | 本项目处理 |
| --- | --- |
| 锁PICO-8/DB32再缩图量化 | 采用材质色阶与局部密度约束；量化是显式可选派生步骤，默认不处理 Claude 原生图。缩图不能补救造型和动作，也不能叫原生像素制作。 |
| 角色32×32、地图16×16 | 不采用。保持设计规范的地图32×32、角色基础32×48；64×64动作格只是透明承载。 |
| Aseprite脚本和工程交接 | 原配方保留，增加真实 `.aseprite` RGBA层源、静态图集/JSON、可选固定CLI导出。房间按建筑与家具分层，单家具内部仍为一层。 |
| 9-slice/Wang解决拼接 | 九宫格拉伸和邻域地形连接不是同一问题。保留旧47形墙顶工具；连续材质裁片必须另验周期与转角，不能直接套用47形宣称无缝。 |
| 每次3行、每行24汉字 | 作为可调写作预算，不改成全项目硬规则。现行紧凑对白区是1088×110、正文23px；实际行宽取决于字体与排版，应实机验收。 |
| 禁词、潜台词、利益冲突 | 卡片记录目的、隐瞒、声口、知情；工具检查预算和显式知识声明，套话仅提示。文学质量与潜台词由作者审读；禁止把隐藏真相泄进失败提示。 |
| 完成后给2–3选项等待选择 | 常规已授权工作直接接下一就绪任务；只有真正路线冲突才问。完成证据失效会阻止下游，不能用“已完成”布尔值糊弄。 |
| 调高effort即可主动且有创造力 | 不作此保证。思考预算可帮助复杂推理，但目标边界、可用工具、反馈和验收证据仍须实际建立。 |

把不足直接归因于“企业合规训练”或宣称某模型天生被动，无法从这段建议或项目资料证实。MCP 是工具通信协议，不是绘画模型、文学评审或后台执行器。[官方 MCP 工具说明](https://modelcontextprotocol.io/specification/2025-11-25/server/tools)；[OpenAI 工具接入说明](https://developers.openai.com/plugins/build/mcp-server)；[推理预算说明](https://developers.openai.com/api/docs/guides/reasoning)。

### 统一生产闭环

在现有 `rpg-tilemap` 服务中调用 `native_art_catalog` 查看37个物件配方、五种地面材质与实际色阶；`read_native_recipe` 读取七个源模块。`render_native_prop` 运行选定配方，`render_native_material` 从原生连续材料场裁片，两者记录参数、源哈希和归属，拒绝覆盖历史原稿或未经登记的人工改图。

`rebuild_native_interior(mode="verify")` 用同一个 builder 在内存重建56件物件及建筑，逐像素和布局比对；`mode="rebuild"` 才覆盖该入口拥有的运行时文件。CLI 同样调用 `render()`，没有新建第二套绘制逻辑。修改 Python 配方后需重启 MCP 进程，避免常驻模块使用旧代码。

`export_native_handoff` 导出56件家具单帧源、建筑单层源、R-05分层编辑参考，共58份Aseprite文件；静态图集保留每件物品实际尺寸、帧区域与锚点。`collision=null` 表示不是按画布推断占地，真实摆放数据另在交接清单里。房间工程不含人物、运行时灯光或交互状态，不是场景替代品。`layered_sprite` 可把后续明确提供的同画布PNG图层打包，不能从扁平图片猜隐蔽图层。

`export_aseprite` 在安装Aseprite并设置 `ASEPRITE_BIN` 后，用固定CLI将源工程导出PNG/JSON，保留图层、tag和slice元数据，不缩放、不trim。手工修图后须明确选择以工程为源的导出与场景引用，再更新族清单；不要重新跑Python配方覆盖画师修改。当前仅生成并检查了文件结构，未做Aseprite应用内兼容验收。[文件格式](https://github.com/aseprite/aseprite/blob/main/docs/ase-file-specs.md)；[CLI](https://www.aseprite.org/docs/cli/)。

`review_dialogue` 接收卡片和turns；默认3行×24字可调整，像素预算只给估算，不冒充字体测量。例：音乐家表面想拿回缺口杯，隐瞒昨夜经过门厅，台词“杯子给我。\n别碰缺口。”配“把杯柄转向自己”的动作；工具不会自动证明这句比其他写法更好，也不会自行写进正式剧情。

`create_plan(goal,tasks)` 保存本轮有边界的任务图；每项有目的、依赖、验收与准备信息。`next_task` 返回可继续执行的工作，`update_task` 的完成声明需要仓库证据文件及SHA256，`read_plan` 重查证据。机械检查只证明文件与声明未变，不证明艺术或游戏已通过；代理仍须真实执行、观察、修复。MCP不自行唤醒聊天或改变模型参数，已授权范围内的继续推进由调用它的代理负责。

当前接入验证见 `artifacts/tilemap-workflow/native-mcp-check.json`，物件对照图见 `native-props-review.png`。这些是协议与生产可复现性证据，不替代门洞、遮挡、连续移动或整案实机验收。

## Claude 剧情修订方法：把信息获取写进规则

依据用户确认的现行 `first_awakening.dialogue`，以及 `artifacts/first-awakening/observation-cards.md`、`questioning-review.md`、`reasoning-design-review.md`。修订记录属于设计依据，不是现行实现已经符合的证明；旧盲测记录也不能作为本轮通过证明。

要继承的核心方法有六点：

1. **观察不能冒充历史。** 钟在走只证明当前走时；补纸分层是可见结构，不能独自证明人物长期居住；灰色相似不等于同源鉴定。角色和玩家都可以提出更强解释，但必须标明推断。
2. **信息依赖具体操作。** 展开纸才见木楔，拿杯片与杯比对才得断口吻合，拿小盒到桌边才得磨痕配对。避免台词提前把远处两物自动配齐。
3. **当前状态与历史重建分开。** 玩家现在拔销造成新擦痕，不能倒写为原有证据；脑内重演产生的是假设预测，带回现场核对后才成为已验证关系。
4. **角色观点可以互相冲突，作者不能借角色认证答案。** 工程师的牢房猜测、厨师的灾厄猜测、音乐家的防御猜测应由各自掌握的局部信息与利益驱动。want/secret/voice不是要求所有角色凭空多一个秘密，更不能靠口癖替代人物判断。
5. **承认解释的限度。** 内销在房内足以削弱“外侧始终完全控制”的版本，不能逻辑上排除所有监禁可能。反驳应限定具体命题；保留等价解释和未知姓名、日期。
6. **允许玩家真实追问和退出。** 重复呼叫、取物比较、主动验证都应有反馈。失败提示只能使用已公开提交的观察；未实现的破墙实验要说范围不足，不能伪造“试过且失败”。

现行脚本仍有需要作者处理的偏差：

- `engineer_0`、`chef_0`的“入学前”与观察卡的记忆边界冲突。MCP验收报告给出一条工程师台词的精确替换预览，未覆盖正式剧情。
- `_npc_stage()`仅按广播/推举阶段切换。`musician_1`让侦探直接说盒底与桌痕吻合，并没有对应“带盒比对”条件；缩短这句话不会修复知识门槛。
- 广播的“暂避风暴的温床”暗示已知外部灾难，“盲目的臆测比沉默更为下劣”又把主持人变成训诫者。观察卡的中性任务说明更符合当前信息边界。
- `meeting`的“门缝又有灼热气流”与观察卡所述当前无明显灼热感不一致，应区分历史假设与当下观察后再决定台词。
- `challenge_prison`把一种受控版本的漏洞扩大为“牢房说无法解释”；应保留反驳的适用范围。`*_2`在推举代表后才触发，不能仅因它们总结已讨论内容就判为提前泄露。提交反馈也受`captain`及证据条件约束，必须沿实际调用链审查。

`story_design_contract`返回上述方法、四份来源的SHA256与摘录；`inspect_story`读真实行号、标题、跳转和控制结构指纹。`review_story_beats`检查显式声明的角色知识顺序及observation/hypothesis/rule_result，假设不能直接增加已知事实。它不从自然语言猜测隐藏真相，也不判断推理逻辑是否充分。

台词修订采用 `inspect_story → review_story_revision → apply_story_revision`：每行同时提交原文、替换文与整个源文件SHA256。只改现存人物台词；保留说话者、缩进、换行、标签、插值和内联跳转，选项/条件/do语句不允许经此入口修改。未知语法、重复标题或未解析跳转阻止写入。写入使用同目录临时文件与原子替换，并在替换前复查哈希；同服务串行，多进程或外部编辑器最终检查后的极短竞态不能由文件哈希完全消除。

“3行×24字”只负责编辑节奏。`measure_dialogue_text`使用真正的Godot TextParagraph和当前对白字体候选测量换行。本轮64条现行台词在1088×110、23px参数下均未测出宽高溢出，不能据此说台词没有说教或泄题。系统字体会随机器变化，正式目标平台仍须实机检查。

## Gemini UI 建议：核实后进入同一 MCP

采用石质推演板与档案卷宗的材质隐喻、冷灰青与暗金、整数像素浮雕按钮。当前页面是脑内推演，用道具外观组织功能；尚未实现角色真实拿取卷宗，因此不把美术隐喻当成已经完成的世界内交互。

Godot规范按4.6官方文档核实：[StyleBoxTexture](https://docs.godotengine.org/en/4.6/classes/class_styleboxtexture.html)、[Button](https://docs.godotengine.org/en/4.6/classes/class_button.html)、[StyleBoxFlat](https://docs.godotengine.org/en/4.6/classes/class_styleboxflat.html)、[Theme](https://docs.godotengine.org/en/4.6/classes/class_theme.html)。具体裁决如下。

| 原建议 | 采用及修正 |
| --- | --- |
| 所有背景禁止ColorRect | 面板背景统一资源管理；ColorRect用于真正的遮罩/叠层本身没有引擎错误，不作全局禁令。 |
| 9-slice两轴一律Tile | 本族切片按周期边带制作，两轴Tile=1；Tile不会修复非周期纹理，Tile Fit会调整片段尺度，不用于这里的原生像素边带。 |
| pressed上边距加1 | 上+1、下−1，总和不变，同时设align_to_largest_stylebox=0。单纯增加会改最小高度；最大样式对齐也可能抵消位移。 |
| focus可用StyleBoxEmpty | 保留透明底像素角框，键盘/手柄仍能知道焦点位置；focus是叠加态，不用不透明背景遮住按钮。 |
| 只配置normal/hover/pressed/focus | 补齐disabled、hover_pressed，防止回退到默认外观。 |
| 米黄必须低对比 | 采用#C4BC9F，但保持文字可读性。低饱和不等于低可读性；错误提示也改为较亮暖色。 |
| 临时StyleBoxFlat | 零圆角、关闭AA，边框1–2整数像素。真实生产面板使用StyleBoxTexture，临时框体不能宣称正式美术交付。 |

`build_diegetic_ui`输出同一FirstAwakeningInterior族的8张原生切片、8份单层可编辑Aseprite、Theme和含显式sub_resource的`scenes/FirstAwakening/MindThemePreview.tscn`。本族逐点绘制，不通过缩图量化。已有文件必须与所有权清单哈希相符才允许replace，画师修改会阻止覆盖。首次使用需Godot导入PNG，再运行实际加载检查。

原子资产规格（margin顺序左/上/右/下）：

| 名称 | 原生尺寸 | texture_margin | content_margin |
| --- | --- | --- | --- |
| dossier卷宗 | 24×24 | 8/8/8/8 | 24/24/24/24 |
| frame石框 | 24×24 | 8/8/8/8 | 12/12/12/12 |
| editor凹槽 | 24×24 | 4/4/4/4 | 6/6/6/6 |
| button normal/hover/disabled | 16×16 | 4/4/4/4 | 14/7/14/7 |
| button pressed（兼hover_pressed） | 16×16 | 4/4/4/4 | 14/8/14/6 |
| focus透明角框 | 24×24 | 8/8/8/8 | 6/6/6/6 |

切线与颜色清单位于源族`mind-ui.json`；人工修改保持边条可周期重复和中心区域正尺寸。Aseprite文件尚未在Aseprite应用内打开验收。

当前`MindPresentation.gd`、`MindBackdrop.gd`已经使用同一Theme，移除了推演页的浅色纸面和旧按钮背景覆盖。主剧情、证据与Core规则未因换肤改写。`inspect_godot_ui`真实加载固定样板并检查继承后样式、Button六态、content/slice margins、按下边距、控件边界；它不能静态识别任意游戏脚本动态生成的整个UI。当前游戏页另用既有`playtest_ui.gd`验证真实输入、中文编辑、视角、音乐、错误反馈与旧重演状态，截图已人工打开检查。

验证记录：`artifacts/tilemap-workflow/ui-story-mcp-check.json`。真实stdio发现33个工具，样板1280×992与960×640均通过；当前游戏推演页仍按项目既有1280×992逻辑尺寸运行，不能把样板的小尺寸通过当作游戏全界面响应式通过。玩法回归见`ui-game-regression.log`，渲染输入检查见`ui-playtest.log`和`artifacts/first-awakening/playtest-ui-observations.json`。环境仍有证书存储/着色器缓存写入诊断，离线加载和玩法检查完成；不宣称日志完全无错误。
