# 初醒 Demo：当前宿主技术入口

界面与操作优化（2026-10-03）：修复三处使假说手稿（F）每帧报脚本错误的回归（重演舞台缺 `known_dorms`、走带缺 `state.event_mode`、结论标签类型不符）；对白框改为带内嵌立绘的浮动卡片并支持按住 Ctrl 快进；顶栏键帽与推演材料进度、近身提示圆角条与 Tab 换目标；R 重启先确认；领取柜可一次请四人依次核验交付；证物页翻页与目录；假说选项横排；答辩标签显示填写进度。规约见[主控规范 §4.19](../../../docs/design-standard.md#419-现场界面与减少重复操作2026-10-03)。自动回归：`python tools/verify.py`（`story_flow_check.gd` 与截图集 `check/state_gallery.gd`）；导航网格与碰撞一致性检查为 `check/navigation_check.gd`；原文提及的 `story_module_check.gd` 已不在仓库中。本轮做了两次独立盲玩，均未到达结局：[第一轮](../../../artifacts/first-awakening/blind-ui-pass-20261003-01/report.md)发现宿舍门卡死、面板溢出等问题，已修复；[复测](../../../artifacts/first-awakening/blind-ui-pass-20261003-02/report.md)卡在“交接因果推演”入口不可见，结论为不合格，待改项列于报告。复测还查出盲玩驱动的按键从未进入 `Input` 状态，已修正。

剧情重整（2026-10-02）：当前红筹案以 `RedTokenDemo` 协调 Content、Session、Hypothesis、Simulation 与独立答辩 View；观察文本通过共享 `Story/ObservationReader` 读取。新增剧情的模块职责与任务顺序统一维护在 [主控规范 §4.15](../../../docs/design-standard.md#415-剧情模块与新增任务边界2026-10-02)。旧场景/输入入口保持，历史检查与试玩记录不自动等同本轮验证。 本轮独立自动回归：模块86/86、红筹425/425、开场调查40/40、Core作用域21/21通过；构建通过。记录见[重整验收](../../../artifacts/story-modularization/final-verification/report.md)，不替代盲玩。模块检查入口为 `check/story_module_check.gd`，使用 Godot .NET 的 `--path godot_csharp --script res://scenes/FirstAwakening/check/story_module_check.gd` 执行。


当前版本已接入《未归的红筹》的场景、调查卡、手稿共享舞台与原生新道具。已按实际截图盲玩修复候选反馈、长页操作、鼠标改点与转角、规则参照隐式改写、初识与本人认签顺序；最后修复定向425项通过。全新独立会话 `mouse-rendered-20261002-07` 首输入至正常终局30分33.615秒，231输入、246PNG、实际查看49张，无求助、错误、超时或移动停滞。各轮归纳见 `artifacts/first-awakening/consolidated-audit-report.md`（原始轨迹目录已精简删除）；游戏帧2次超过50ms与截图开销分列。历次失败与体验评价保留，纯思考、真人净30分钟及发布体验仍未认证。下方旧案历史记录不代表本版验收结果。

2026-10-02：剧本 S03—S26 经剧情队列接入（触发表见主控规范 §4.11 前的“运行时接线”）；侦探醒来时外衣挂在衣架上，检视外衣后换装；S28 在霍铎拔销后开门。跳转状态回归：`--headless --path godot_csharp --script res://scenes/FirstAwakening/check/story_flow_check.gd`。盲玩驱动 `check/playtest_session.gd` 已按 `tools/blind_playtest.py` 协议重建（有窗口运行，末尾加 `-- --session=<新ID> --agent=<名称>`）。

默认 `Main2D.tscn` → `FirstAwakening.tscn` → `RedTokenDemo.gd`。`RedTokenBridge.cs` 接入宿主 C# 会话；原 `FirstAwakening.gd` / Hall 系列保留兼容，旧六轮与七槽不控制新默认入口。

本轮修复涉及场景、UI、物证细节及正文交互，技术回归与独立盲玩分开验收，构建和可视操作通过不能据此称为可发布剧情 Demo。新案正文目前使用中文；语言设置与姓名控件保留，选择英日时会明确显示覆盖范围。

- `RedTokenDemo.gd`：近身调查、道具近景、姓名/跟随输入、手稿入口；重复的开发说明不在玩家界面常驻。
- `MindNotebook.tscn` / `RedTokenReplayStage.gd`：既有原生纸面、字体与左画面/右草稿结构。回放消费桥的提交快照。
- `tools/first_awakening_art/red_token.py`：使用既有材质色阶的原生像素道具配方，生成源码 PNG、运行 PNG、底脚锚点/占地与组合部件关系。
- `red_mechanisms.tres`：运行时装配、调查点和新增碰撞的共同数据；附属纸带、封条、夹布等跟随所附家具排序，管路使用独立底层。

从仓库根重建新增道具：`$env:PYTHONPATH='godot_csharp/tools'; python -m first_awakening_art.red_token`。全生活翼重建入口 `python godot_csharp/tools/build_first_awakening_interior.py` 同样发布新配方并保持新默认场景脚本。美术源在 `AssetsSource/Internal/Props/FirstAwakeningInterior/`，运行资产在 `ShareAsset/Internal/Props/FirstAwakeningInterior/`；生成的 native/2x 联系表与独立验收归 `artifacts/first-awakening/`。

以下保留原宿主的技术记录，仅适用于对应兼容代码和旧检查入口，不代表当前默认流程。
# 初醒 Demo

默认入口 `res://scenes/Main2D.tscn` 加载 `FirstAwakening.tscn`。这是独立可玩的生活翼调查片段；宣传前三场仍可分别打开 `res://scenes/Story/CentralHall.tscn`、`Breakfast.tscn` 和 `ParlorGathering.tscn`，茶案继续在 `scenes/Reasoning/`。

## 操作

初醒当前主入口为事件纸面；事件选项和纸面说明已加入中英日资源，完整英文/日文剧情适配仍未验收。原行动编辑器、地点观察文案及姓名登记也有中文、英文、日文资源；旧编辑器仅作兼容路径，不是本案玩家输入。场景 HUD 的语言选择保存在 `user://language.cfg`。Dialogue Manager 使用带稳定 ID 的 `first_awakening.dialogue` 与 `i18n/en.po`、`i18n/ja.po` 翻译对白。可编辑译文置于 `i18n/`；纸面译文从 `mind_ui.tsv` 生成 PO，回放译文在 `replay_ui.csv`。打包游戏时须包含 `i18n/replay_ui.csv` 原文件，桥接层用它把 Core 的规则文案对应到编号。

脑内推理页的中文手写区使用 `ShareAsset/External/Yozai/Yozai-Regular.ttf`，中文印刷区与回放字幕使用 `ShareAsset/External/ZhaohuaTypeWriter/ZhaohuaTypeWriter.ttf`。英文手写区使用 `ShareAsset/External/playwriteustrad/PlaywriteUSTrad[wght].ttf`，印刷区使用 New Tegomin。日文手写区使用 `ShareAsset/External/ZenKurenaido/ZenKurenaido-Regular.ttf`，印刷区与字幕使用 `ShareAsset/External/newtegomin/NewTegomin-Regular.ttf`。字体只由本场景的 `MindNotebook.tscn`、`MindPresentation.gd`、`ReplayStage.gd` 和专用 `mind_theme.tres` 引用；外部字体目录保留各自授权文件。
固定纸面按钮的外观集中在可读的 `paper_button_theme.tres`，并由 `MindNotebook.tscn` 引用；运行时仅按语言更新字体。
脑内推理页的操作符号使用 Google Material Symbols Outlined；来源、字体文件与 Apache 2.0 许可保存在 `ShareAsset/External/MaterialSymbols/`。

- WASD / 方向键移动，或左键点击可行走的地面自动寻路；接近点击位置 8px 时停下，近身点击不会使角色抖动。左键点击微光物件或人物会自动走到交互范围并触发调查、交谈或操作；靠近后仍可按 E / Enter 交互。手动方向输入会取消点击路线。
- J 打开物证簿；圆桌讨论开始后，F 打开脑内推演。此前按 F 只显示当前流程限制。浮窗中可用滚轮阅读，检视卡可按任意键或点击外侧关闭。
- 开局三位同伴在长桌周围轮流交谈，北侧缺口留给从门口进入的侦探。跨过活动厅门槛后，主厨、工程师、音乐家间隔0.3秒转向来人，侦探自动走到独立站位。问到称呼时打开上半屏纸面登记卡；姓名去首尾空白后须为1–16字，不能与三位同伴、侦探或铜口重名，可回车确认或使用建议名。命名期间锁住移动及游戏快捷键，姓名接入发言、此前回看、物证簿来源和结局。对白默认旁听者看说话人，也支持指定谁看谁及恢复跟随说话人；广播时看向铜口，内部质询时看侦探。随后回自己的房间调查窗、床、抽屉、废纸篓，再到长桌首次会合。
- 首次会合选择工程师（门厅）、主厨（备餐间）或音乐家（研习室）为搭档，同行检视六处物件；另一组的勘验在长桌转述，物证簿注明来源。其他三间宿舍从开局起隐藏内部且保持实碰撞，仅当前搭档在身边时可查看其房间。宿舍门和生成脚本使用相同权限配置。
- 六处同行勘验后回长桌，交换见闻并开始圆桌讨论，此时开放脑内推演。脑内三项假说检验由 Core 反例执行产生反馈，不改变会议认可状态。把已解锁的世界事件按相对先后放上纸面，看完当前草稿的重演后，可在会议面板选择说法、出示相关证物；错证保留争议，三项正确回应后同伴授权答辩。正式提交仍要求完整经过与已调查物证相容。
- F 首次先显示脑内页，再在该页上播放侦探心声，结束后恢复输入。玩家在外界广播器逐行输入事件词汇，或点击下方助手插入；行序决定相对先后，直接编辑文字即可改序或删除。异响、热烟、铃止、巡查、外闩、主厨今晨插销、工程师试门各自作为事件；窄缝逃离、门未再开、侦探搬走行李、工程师先插销和第五人始终在外清理室内是可放入回放的竞争假说。助手列表支持滚动。未知、重复或尚未由调查开启的词汇会显示错误并阻止重演。音乐家、侦探、工程师、主厨依固定本能响应，物理后果由 Core 结算。侦探的入约台词改为只记得登船，本能响应保持可用；巡查者身份无须作答，院方进出与外闩责任须有证据。事件稿离开纸面后保留。
- 回放可点“跟随／门厅／宿舍／研习室／备餐间”切换摄影机，也可直接拖动画面；视角操作不更改现实或推演状态。推理页低音量循环播放项目已有的钢琴独奏，可关闭，退出淡出。
- 从头重演后，可回看、暂停／继续或进入下一幕。关键痕迹出现时自动暂停，左侧一次只展示一处预测；错误设想与已调查物证的具体冲突按处呈现。未调查位点先记下位置，再回现场按 E 印证。核对不限制次数、不推进现实时间，也不生成已证实动机。草稿修改后旧结果失效；提交必须使用当前草稿完整播放后的结果，且与已调查物证相容。
- 铜口答辩支持文本直接输入与下方助手词汇点选。原三项第一阶段陈述之外，新增夜里拔销、擦地后的巡查、今晨重插内销及院方外闩四项，七槽共用证据可用性与提交校验，统一合格结局 `ending_passed`。R2 的初步三项陈述可以提交，但铜口只返回未核对形迹的具体位置；正式通过须站在铜口附近、接通拉柄、获得同伴认可，并使用当前完整回放结果。

## 本轮验证

独立盲玩使用 `check/playtest_session.gd`，由每轮全新 Agent 从空白开局通过通用键鼠客户端 `godot_csharp/tools/blind_playtest.py` 操作。启动末尾加 `-- --session=<全新ID> --agent=<Agent名称>`；不要预先创建会话目录。本轮使用有窗口的 `--rendering-method gl_compatibility`，不加 `--headless` 或固定帧加速；人物移动使用鼠标。驱动将下拉菜单嵌入截图视口，保存可见选项和按钮选中状态。输入、玩家可见文本、截图、版本及帧指标保存在 `artifacts/first-awakening/blind-<ID>/`。测试 Agent 不读设计、源码、谜底或历史轨迹，主代理仅负责设定对照与技术修复。关键画面实际查看本轮PNG；文字快照不包含纹理像素，不能以缺少图片条目判定空白。技术回归、预写答案输入回归和独立盲玩分别验收；每轮盲玩必须同时交游玩轨迹与评价，工具耗时不能冒充真人等效时长。详细验收要求统一维护于主控规范第二部分 §4.14。

2026-10-02 新案反馈专项由全新独立技术 Agent 验证：Godot 工作区构建与导入成功，真实 OpenGL 渲染下 137 项检查、133 条输入、0 失败；验证手动候选与答辩可达、成功试验反馈、具体差异、修改/重置失效、答辩草稿保留、已读材料回看及三语释放选项布局。通用驱动另以两个全新技术烟雾会话核验下拉菜单截图与选项快照，修复内部菜单漏报后通过。报告位于 `artifacts/first-awakening/feedback-repair/independent-technical-report.md`。上述准备包含直接建立已知材料，属于自动技术回归，不能作为独立游玩通关证明；截图盲玩结果另按对应会话报告记录。

2026-10-01 后续体验修复：主厨的针对性问题由玩家已识别的证据缺口解锁，正确提出巡查经过也可继续，不再强制先输入一个固定错误说法。答辩助手与缺项提示隐藏尚无依据的后续陈述；巡查事件助手先称“来者”，身份另由规程与证据核验。圆桌当下交流说明四人的职业习惯，推演不再暗示侦探保留在校人际记忆。已看过的相同预测不反复强制暂停；“重演改动”复用已看过的共同前段，仍保留从头播放与回看。物证核对记录预测签名，变化后须重新核对。本轮独立技术 Agent 的抽象因果检查为33/33，门案专项与回放检查为0失败；独立盲玩结果以各会话报告为准。

后续独立盲玩定位了会议证物受理和答辩提示故障：同一说法接受有依据的多种证物，缺观察时注明地点，无关证物指向已取得的相关材料，失败不扣世界时间；文字齐备、重演核对和同伴认可分别显示。已调查物证从推演检视后直接回到原播放位置。开场进一步减少27行重复对白。指针交互与同行多候选选路在一次调用内共用网格，下次重新读取障碍物；遇阻重算等待改为0.15秒，碰撞规则保留。独立validation_09已验证三搭档分支、门案与回放0失败，最新完整版本的盲玩评价另行记录。

以下为先前固定路径验收历史。`full_input_playthrough.gd` 中的六轮假说与解法预先写在脚本里，其输入通关不构成独立盲玩。退出 ObjectDB 警告在独立复验中间歇出现，不能报告为已经完全消除；正常游玩采样与退出诊断分别记录。

2026-10-01 重门案三阶段实现验收：`demo_door_case_check.gd` 验证 R1–R6、复查与提问解锁、主厨证言不自动暴露、旧三槽初答辩只能收到 `reject_incomplete`，四个新增槽缺一不能通过；`event_replay_check.gd`、`i18n_integration_check.gd` 和行为编辑器兼容检查通过。`check_first_awakening.gd -- --all-branches` 三位搭档均 PASS；`full_input_playthrough.gd` 使用实际键鼠事件，完成六轮自然假设、针对性询问和七项答辩，headless 与 OpenGL 均走到 `ending_passed`，最终完整重演为45个行动。OpenGL 使用 `--rendering-method gl_compatibility --fixed-fps 60 -- --door-case-screenshots`，本轮画面位于 `artifacts/first-awakening/demo-door-*.png`，重点包括铜牌、R2 缺口、侧光鞋印、主楼侧扫弧、主厨问答、七项答辩及开门结局。实际输入验收修复了新增触发区的玩家层掩码和七选项门菜单的可点击布局。最终资源重新导入后，专项、事件与三语整合检查再次通过；`demo_door_case_check.gd -- --capture-answer` 以 OpenGL 专项捕获更新了 `demo-door-seven-part-answer.png` 的七项状态文案，该更新图属于界面专项捕获，完整流程证据保留在 `demo-door-opengl.log`。自动检查耗时不能用作游玩时长；12–18分钟仍是按推演轮次估算，尚无未读谜底真人试玩验证。部分 headless 检查退出仍可能提示对象或资源未释放。

2026-09-30 铜口答辩与外界广播器输入重构：铜口实体答辩界面彻底移除 MenuButton 下拉菜单，改为 TextEdit 直接书写陈述词汇或点击下方分类助手插入；输入支持任意行序、中英日词汇与标点容错，状态栏实时提示论证缺口与解锁状态。`check_first_awakening.gd`、`event_replay_check.gd`、`i18n_integration_check.gd` 均 PASS；OpenGL 完整键鼠通关录制通过并更新 `full-playthrough-11-answer.png` 实机截图。

2026-09-30：`check/event_replay_check.gd` 通过，覆盖事件按钮、改序、四人本能、Core 痕迹、失败假说、提交限制与英文纸面标题。`check/full_input_playthrough.gd -- --no-screenshots` 从键鼠调查、放置三件事件、18段回放、三组证物回应走到 `ending_passed`，PASS。旧动作稿的 `check/check_first_awakening.gd` 以兼容模式检查三位搭档，三条分支和宿舍门口、长廊移动均 PASS；该检查现在默认只跑工程师分支，传 `-- --all-branches` 才跑三条搭档分支。同伴在同行阶段以135px/s追上玩家，交换见闻后恢复90px/s。脚本调度的集合、铜管前站位、答辩站位和另一组调查改为同时出发，全员停下后再继续对话；这段走位按150px/s，结束后恢复各人原速。以上走位与单分支默认修改尚未重新运行检查。

旧动作纸面 `paper_notebook_check.gd` 以兼容模式运行39项、0失败。Godot C# 构建0错误、现有 Dialogue Manager 插件23条告警；`i18n_integration_check.gd` 0失败。自动检查退出仍可能提示对象或资源未释放。未读谜底玩家测试、本能的当下小演出与完整英日剧情验收尚未完成。

## 制作结构

地图为 32×32 基础格，四宿舍、主走廊、研习室、备餐间和门厅在一个连通平面内。`FirstAwakening.tscn` 保存建筑精灵、墙与家具碰撞、家具、四人、隔离门、房门、灯光、尘粒、相机和 HUD 节点；`RoomDetails.gd` 只负责灯火微动与门厅痕迹绘制。建筑和地毯由32px整数绘制配方产出。家具和四名角色按脚底 Y 排序。人物使用现有 `animation_v2` 64×64 透明承载格，人物本身没有放大；世界相机统一2倍最近邻显示。

当前家具、道具及生活翼建筑来自 `ShareAsset/Internal/Props/FirstAwakeningInterior/`，由 `tools/build_first_awakening_interior.py` 调用 `tools/first_awakening_art/` 的原生像素配方直接绘制；不是从 image_gen 源图裁切、缩放或量化得到。早期 image_gen 源图和提示记录保留在 `AssetsSource/Internal/Props/FirstAwakeningInterior/` 作为历史来源，不是现行重建输入。各资产实际尺寸见运行时 `manifest.json`。物证检视卡仍是独立的 22 张 image_gen 图，源 PNG 位于 `AssetsSource/Internal/Props/FirstAwakeningEvidence/`，来源说明与原样发布入口见 `ShareAsset/Internal/Props/FirstAwakeningEvidence/README.md`；推理页 `mind_seal.png` 则是独立的 24×24 原生逐点配方 `tools/first_awakening_art/mind_ui.py`。这些生产来源说明不代表全部艺术已验收通过。原 External/Victorian 素材保留，本场景不再用其旧家具裁片混搭。

床体在人物之后、前层被褥在人物之前，起床落点为床前；四间宿舍采用相同墙面、寝具、地毯、家具和相对摆放位置，不按职业布置工具、乐谱或生活杂物。三张可检查书桌均已清空，房间保留编号差异。侦探外衣的袖口灰痕与纸篓包楔属于保留的案件线索；其他纸篓为空。共同生活痕迹被恶魔清理是作者侧设定，现场文案只描述缺失的东西，不直接揭示清理者。公共区域的案件物证仍保留。`RoomDoor.gd` 先核对房主陪同权限，再按人物距离开启门扇并切换真实门叶碰撞；未授权房间覆盖内部。隔离门底图不含内销，独立销体根据现场操作或 Core 回放结果显示；配重、绳索、检视盖与止口保持可见。现场和脑内各自持有图像节点，脑内复用相同的地毯与陈设，不能改变现实状态。

宿舍下侧为连续的墙顶、灰泥与木护墙板，四扇房门嵌入立面，门洞贯穿3行墙厚；走廊净地面64px。门扇锚点y=384，调查点、工程师站位及地毯随之调整。公共房间南侧同样有向外立面，并保留两处门厅通道。门框与门扇分绘，开门时不使用遮住人物的实心黑底；脑内复用相同门框宽度。

宿舍门牌从西至东为R-05至R-08，门框统一深胡桃木材质，与灰泥及木护墙板衔接。主角步行动画在本场景独立的SpriteFrames副本中由10fps调为7.5fps（右向镜像同用左向），移动速度仍为90px/s；其他动作与共享角色资源不受影响。

艺术调整保留32px规格：冷灰蓝灰泥墙面与暖灰木地板分层，稀疏纹样和细织纹代替密集菱形地毯。现场采用冷色环境光、窗边漫射冷光、暖色油灯；墙体按行合并为遮光轮廓，家具按实际占地遮光，人物脚底遮光轮廓跟随移动。它们是独立的 `LightOccluder2D`，不改物理碰撞或寻路；灯光采用轻度PCF5过滤。当前是俯视2D足迹投影，并非完整三维高度阴影；脑内维持较中性的独立展示光线。

当前室内资源重建入口为 `python godot_csharp/tools/build_first_awakening_interior.py`。该入口从 `tools/first_awakening_art/` 配方生成 PNG，并同步生成可读的 `FirstAwakening.tscn` 与 `ShareAsset/Internal/Props/FirstAwakeningInterior/layout.tres`；`layout.json` 仅留给现有空间审查工具，运行时读取 `.tres`。只更新场景和布局资源、不改 PNG 时使用 `--scene-only`。调查点名称、翻译编号、位置、检视文字与插图键在 `sites.tres`；生成器按该资源为43个调查点写入 `Area2D` 和范围形状，演员的触发区随人物移动。起床落点、集会站位、浮窗边界与底板也在场景中。答辩选项及其证据要求在 `answers.tres`，菜单和提交共用 `AnswerRules.gd` 校验；物证簿分类、会议所需物证和回放线索在 `investigation_content.tres`。现场点击命中与寻路由 `FieldNavigation.gd` 计算，调查浮窗的排版由 `InvestigationPanel` 上的 `FieldModal.gd` 管理；`FirstAwakening.gd` 保留玩家输入、案件进度及流程衔接。对话在 `first_awakening.dialogue`；推演纸面在 `MindNotebook.tscn`，回放舞台在 `ReplayStage.tscn`，固定证据卡在 `EvidenceDetail.tscn`，回放中的关门门叶在 `ReplayDormDoor.tscn`。场景专用主题和回放着色材质均为可读 `.tres`。重建会按配方覆盖 `FirstAwakening.tscn`，因此家具或建筑的永久改动须同步修改配方；`sites.tres`、`answers.tres` 与 `investigation_content.tres` 独立编辑。`rebuild_first_awakening_tilemap.py` 与 TileMapWorkflow 是旧样板路径，不用于当前初醒场景重建。

当前 `MindEventPresentation.gd` 只编辑世界事件；`HallReplayBridge.cs` 的 `CompileEvents` 将本案锁定的四人本能转成行动，并在玩家放置热烟事件时调用 Core。`csharp/HallCausalityRules.cs` 计算门、木楔、灰迹、包纸、双锁和擦拭状态，撤楔后的配重合门也在 Core 结算。桥接层返回初态及每个事件后的状态、行动成功标记和字幕；失败不绘成成功。旧 `HallReplayProgram` 与动作稿编译仍为兼容路径，不是本案默认输入。现场状态与推演状态分开。

人物交谈、四人会议、铜口提问与结局均由现有 Dialogue Manager 插件读取 `first_awakening.dialogue`。场景复用 `StoryDialogue.gd` 作为气泡外观；物证卡、推演盘与答辩词槽是独立操作界面，不解析或推进对话。

## 历史验证与交接（仅证明记录当时的版本）

以下按日期保留旧操作、字体、玩法和通过记录。现行事件输入、答辩及验收状态以上方操作和本轮验证为准；旧动作稿、双结局和系统楷体不作为恢复旧设计的指令。

2026-09-29 取名、讨论朝向与宿舍修订（含已有未提交修改的工作区）：姓名窗口在工程师询问称呼时实际等待输入，确认后对白插入姓名；语言切换及回看保持所填姓名。四间宿舍的配方、场景、碰撞与调查点同步为近乎相同的制式陈设，清空职业桌面；旧寝具检视图与现行状态不符，四处引用已解除，历史图与源稿仍保留。恶魔清理共同生活痕迹仅记录为作者侧事实；玩家对白没有直接指认清理者。

`check_first_awakening.gd` 三搭档分支 PASS，覆盖中文姓名输入、空名禁用、Escape不能跳过、移动不穿透窗口、初见朝向及调查/答辩流程。`full_input_playthrough.gd` 在 OpenGL 实际渲染中以键鼠输入取名并完整通关 PASS，现场截图已更新；新增 `full-playthrough-name-entry.png` 与 `full-playthrough-arrival-discussion.png` 位于 `artifacts/first-awakening/`。已查看这两张截图；技术操作通过，人物演技与审美仍待用户审看。构建0警告0错误，三语界面/当前开场首句、语言切换、行动语言与回放翻译检查0失败。旧语言检查的条件观察者与行动者不同，已按现行同主体约束更新检查输入，未修改规则实现；完整英日文长篇开场仍未适配。资产检查584资源、618依赖路径、0失败。部分 headless 检查退出仍报对象/资源未释放，OpenGL可视通关没有此退出提示；不作为已修复项。命令使用 Godot 4.6.1 .NET、`--fixed-fps 60`；headless 三分支与 OpenGL 全程历史日志已清理，不把自动检查耗时当作人类游玩时长。

2026-09-29 场景流程修订验收（含既有未提交修改的工作区）：`check_first_awakening.gd` 三搭档均 PASS；`full_input_playthrough.gd` 完整键鼠输入 PASS。后者从真实移动进入广播、各自查房、工程师同行六点、长桌讨论，再通过 F 输入五页规则并播放23个事件，出示三组证物，点击三个新答辩菜单并提交至 `ending_passed`；未直接写进度或人物位置。命令采用 `--headless --fixed-fps 60 --log-file <仓内绝对路径>`，全程脚本另传 `-- --no-screenshots`；固定帧步60fps用于加快自动检查，不作为人类游玩时长。对应历史运行日志已清理。行为规则运行检查0失败；Core Release和Godot工作区增量构建均0警告0错误，原插件完整编译的可空性告警记录仍保留。最终资产检查582资源、615依赖路径、0失败。

本轮同时修正连续坐标转寻路格时强行放行撞椅子的终点，以及搭档卡在旧目标后不重新规划的问题；实际跟随与六点检视通过。现实房间的可见性由房主陪同控制，脑内复制只展示实际见过的宿舍，不借现实同伴占位规划模拟路径。运行仍有系统根证书读取提示；最终两项交互检查没有脚本错误或退出警告。可视运行权限申请被拒绝，本轮没有更新旧截图，画面与新增微小痕迹仍待可视验收；新增开场及流程文案为中文，完整英文/日文剧情尚未适配。历史截图和旧双解结局通过记录不代表本次画面验收。

2026-09-29 行为规则修订：驱动人物动作的规则就是动机，复用原纸面。持续条件、优先、禁止和跨页人物组合已接入 `HallReplayBridge.cs`；物理结果仍由原 Core 结算。语言检查3163项、规则运行检查、原纸面操作检查与更新后的主场景检查通过；.NET构建0错误，现有Dialogue Manager插件有23项可空性警告。实际键鼠事件走完调查、会议、五人规则输入、完整回放和铜口提交，通过现有结局。该路径选择有效会议证据；未覆盖的旧弱证据分支仍有缺失方法问题，不计为已通过。运行有系统根证书读取告警，部分检查退出有对象未释放告警。原纸面可视运行截图为 `artifacts/first-awakening/behavior-rules-paper.png`。

当时的兼容路径仍是24类动作、13类条件的有限解释器，未接通通用规划、任意自然语言、任意工具替代、持物占用或新消息传递。持续规则的新句式目前以中文实现。新版 `activity_hall_opening.dialogue` 保留为创作依据；其相识、广播、质询、分组开场已接入 `first_awakening.dialogue`，原稿后半的自动解说改由检视、脑内推演和会议证物操作承担。下方旧日期的通过记录不代表本轮重新验证。

后续视觉调整：按用户要求暂时恢复彩色推演，`ReplayInk.gdshader`饱和度为0.8；共享该显示处理的证据缩图同步恢复彩色。纸面保持原样。更新实机截图，纸面检查36项通过；下方黑白描述为本日先前修订记录。

2026-09-27 组合推演与初见修订：回放取消灰绿洗色，采用中性黑白。角色位置由执行结果中的人物与目标驱动；水流随状态出现，回看撤去后发效果，逆操作后不能把历史锁止痕迹当作当前双锁核对。原场景全套检查及真实键鼠通关到结局PASS；纸面渲染检查36项通过，组合语言3132项通过。截图 `paper-composed.png` 展示跨人物组合；首次能力引导见 `playtest-mind-introduction.png`。本记录不代表任意自然语言或最终艺术验收。

2026-09-27 纸面版本：`MindNotebook.tscn`固定1280×992双页布局；纸底、折缝、墨线及按钮切片由 `tools/first_awakening_art/mind_ui.py` 的原生点线配方制作，通过既有MCP `ui_workflow.build_diegetic_ui(replace=True)`发布到同一资产族。生产清单为 `AssetsSource/Internal/Props/FirstAwakeningInterior/mind-ui.json`，包含源码及产物哈希；长期MCP进程检测到配方修改时要求重启，防止用旧模块覆盖新资源。文字仍是可编辑控件。下文深绿背景记录为历史版本，当前以整幅浅纸面为准。

本轮渲染检查 `paper_notebook_check.gd` 25项通过；原 `check_first_awakening.gd` 全套通过；`playtest_ui.gd`真实键鼠事件验证中文输入、插词、保留草稿、错误定位和回放通过。语言独立检查479项通过。截图见 `artifacts/first-awakening/paper-empty.png`、`paper-words.png`、`paper-result.png`、`paper-error.png`、`paper-blocked.png`。导入仍出现现有Dialogue Manager编辑器配置写入失败；运行有系统证书及着色器缓存写入警告。当前未验证窄屏、200%字级或最终玩家审美验收。

详细美术规格、全套物料清单与重构指南见：[统一设计与验收规范](../../../docs/design-standard.md#4-初醒案件现场与因果链)。

```powershell
dotnet build godot_csharp/DualOntologyWorldEngine.sln
& 'C:\Users\iii\Desktop\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe' --headless --path godot_csharp --script res://scenes/FirstAwakening/check/check_first_awakening.gd
& 'C:\Users\iii\Desktop\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe' --headless --path godot_csharp --script res://tools/check_asset_layout.gd
```

2026-09-26 门厅与交互清理：已修正门厅隔离门阈值交互坐标与未检视物证优先判定，消除实体碰撞线与内销调查点的错位，连续实机走位与调查已畅通。临时运行日志与冗余抽样帧已清理。
2026-09-26 界面复修：真实按钮回调检查覆盖人物选择、条件时机、改句／撤句、从头重演、回看、旧结果核对禁用、返回现场与检视卡恢复；原有 Core 正反例、三次位点检验、会议辩驳和两种通过结局回归通过。此前资源布局检查为 500 个资源、511 条依赖路径、0 失败。自动检查不代替玩家实机推理与最终美术验收。

脑内工作区恢复 Reasoning 的深绿色背景、米色书写纸，正文当时优先使用简体中文系统楷体；清松手写体5保留为候选。移除数字输入、人物下拉框、排序箭头、删除叉及诊断日志堆叠。截图检查覆盖空白案卷、已写假设、改写状态和错误因果结果；发现并修复过窄标题、底部溢出与共享字体重复变更引发的排版错误。回放字幕不显示内部行动码。现实位置与脑内场景继续独立。

2026-09-26 推理页迭代：先用 `artifacts/first-awakening/reasoning-design-review.md` 列出玩家输入、规则反馈和验收点，再以实际 Godot 截图检查版式。页眉印记为 `tools/first_awakening_art/mind_ui.py` 在原生 24×24 像素上逐点绘制；此图与既有家具生成稿的制作方式不同。背景乐运行时副本为 `ShareAsset/Internal/Props/first_awakening_reasoning_piano.mp3`，源曲保留在 `music/The Unfallen Piano Solo.mp3`。

按用户纠正，已删除本轮擅加的谜底暗示、引导问题、调查建议和说明性副标题。空白案卷不预填事件结论；推演痕迹正文直接取当前回放位置的 Core 结果，不额外编写解释或提示。

门厅删除原有地面机关剖面，隔离门接入南墙立面并具备建筑占地碰撞。地图 MCP 审查门洞方向／净空、家具占地与交互站位，实际家具由直接捕获脚本导出，再经 `check/audit_runtime.py` 送入真实 MCP 会话；报告固定为 `artifacts/first-awakening/runtime-spatial-audit.json`。静态报告不能替代动态移动：本轮真实18秒集会回归发现厨师被立灯阻挡，根因为寻路只扩张8px，而人物脚底为向前12px的16×12盒。现在寻路使用相应非对称净空，不删除家具碰撞来掩盖问题。

证物卡继续使用先前内置 image_gen 生成的 22 张检视图，本轮未更换检视卡；本轮新增的室内游戏精灵属于上文 FirstAwakeningInterior 资产族。检视卡源图与提示记录位于 `AssetsSource/Internal/Props/FirstAwakeningEvidence/`，运行时副本位于 `ShareAsset/Internal/Props/FirstAwakeningEvidence/`，通过 `res://tools/build_first_awakening_evidence.gd` 原样发布。

验收截图和日志固定归仓库 `artifacts/first-awakening/`，新增 `replay-empty-preview.png`、`replay-edit-preview.png`、`replay-conflict-preview.png`，并更新 `replay-preview.png` 与门厅截图。场景为包含既有未提交修改的工作区；本轮技术检查及截图不能替代用户审美验收。受当前执行环境限制，检查命令须增加 `--log-file` 指向此目录的绝对路径；系统证书读取警告不影响离线演示。

2026-09-26 对话与推理界面调整：初醒采用独立启用的简洁对话布局，保留说话人、正文、继续/选项及回看，隐藏重复章节名和按键提示；答辩按三个问题逐项展开已解锁词块，未取得依据的选项不展示。行为草稿采用TextEdit直接输入受限行动句，编译后仍交由现有Core执行，并非任意自然语言判案。新增回归覆盖光标插词、非法行定位、修正恢复提交、旧结果核对禁用及离开后草稿保留。


## 交互流程修订（2026-09-27）

电话接通与初次广播、广播前后相识、大门反复操作、物证回应、共同离场确认均由 `first_awakening.dialogue` 编排。场景方法只提交相应状态与界面切换。大门可拔/插销、推/拉、查看检视孔与机构后继续操作；看过外闩前，推门失败只反馈另一侧的阻力。人物从互相提醒和分工开始，对未证实解释保留异议。

组合动作规格见 [动作表](../../../docs/design-standard.md#1-现场勘查与因果桥梁-24-项动作规范)，涵盖24动作、13条件及当前实现缺口。英文/日文 Dialogue 翻译由 `python godot_csharp/tools/update_first_awakening_po.py --check` 校验；中文以 Dialogue 为唯一源，源文变化不得沿用旧译文。

`check/interaction_flow_check.gd` 从实际可见按钮验证一次接通、晚初见、门操作循环、脑内说明层级、正确/错误物证及取消/确认离场。截图固定为 `artifacts/first-awakening/interaction-*.png`。这不代替未读谜底的玩家试玩。

本轮验证：Godot完整资产重建与导入完成；.NET构建0警告0错误；交互流程检查与三语集成均0失败；全程键鼠通关及原主检查均PASS（含两种答辩结局与重启）。对应历史运行日志已清理。已查看电话、大门和脑内说明截图；尚未进行未读谜底玩家的节奏与推理体验验收。
