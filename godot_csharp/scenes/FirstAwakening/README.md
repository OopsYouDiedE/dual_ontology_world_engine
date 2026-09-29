# 初醒 Demo

默认入口 `res://scenes/Main2D.tscn` 加载 `FirstAwakening.tscn`。这是独立可玩的生活翼调查片段；宣传前三场仍可分别打开 `res://scenes/Story/CentralHall.tscn`、`Breakfast.tscn` 和 `ParlorGathering.tscn`，茶案继续在 `scenes/Reasoning/`。

## 操作

初醒界面与原有行动编辑器、地点观察文案及姓名登记支持中文、英文、日文；活动厅开场和同行流程仍有未编号的中文对白，完整英文/日文剧情适配尚未验收。场景 HUD 与脑内推演页都有语言选择器；选择会保存在 `user://language.cfg`，下次启动自动恢复。Godot `TranslationServer` 负责界面和场景文字，Dialogue Manager 使用带稳定 ID 的 `first_awakening.dialogue` 与 `i18n/en.po`、`i18n/ja.po` 翻译对白。脑内行动编辑器按当前语言显示词汇和语法，并保留旧中文草稿的解析能力。可编辑译文统一置于 `i18n/`：场景界面 `first_awakening_ui.csv`、人物名 `dialogue_names.csv`、行动语言 `mind_language.csv`、回放 `replay_ui.csv`、纸面界面 `mind_ui.tsv`；后者用 `tools/build_mind_ui_translations.py` 生成三语 PO。翻译引用分别使用 `FA_UI_###`、`FA_NAME_###`、`FA_LANG_###`、`FA_REPLAY_###`、`FA_MIND_###`，对白用 `FA_###`。新增文案须分配新编号、填写三语并运行 `check/i18n_integration_check.gd`、`check/mind_i18n_language_check.gd`、`check/replay_i18n_check.gd`、`check/locale_switch_check.gd`。打包游戏时须包含 `i18n/replay_ui.csv` 原文件，桥接层用它把 Core 的规则文案对应到编号。

脑内推理页的中文手写区使用 `ShareAsset/External/Yozai/Yozai-Regular.ttf`，中文印刷区与回放字幕使用 `ShareAsset/External/ZhaohuaTypeWriter/ZhaohuaTypeWriter.ttf`。英文手写区使用 `ShareAsset/External/playwriteustrad/PlaywriteUSTrad[wght].ttf`，印刷区使用 New Tegomin。日文手写区使用 `ShareAsset/External/ZenKurenaido/ZenKurenaido-Regular.ttf`，印刷区与字幕使用 `ShareAsset/External/newtegomin/NewTegomin-Regular.ttf`。字体只由本场景的 `MindNotebook.tscn`、`MindPresentation.gd`、`ReplayStage.gd` 和专用 `mind_theme.tres` 引用；外部字体目录保留各自授权文件。
固定纸面按钮的外观集中在可读的 `paper_button_theme.tres`，并由 `MindNotebook.tscn` 引用；运行时仅按语言更新字体。
脑内推理页的操作符号使用 Google Material Symbols Outlined；来源、字体文件与 Apache 2.0 许可保存在 `ShareAsset/External/MaterialSymbols/`。

- WASD / 方向键移动，或左键点击可行走的地面自动寻路；接近点击位置 8px 时停下，近身点击不会使角色抖动。左键点击微光物件或人物会自动走到交互范围并触发调查、交谈或操作；靠近后仍可按 E / Enter 交互。手动方向输入会取消点击路线。
- J 打开物证簿；圆桌讨论开始后，F 打开脑内推演。此前按 F 只显示当前流程限制。浮窗中可用滚轮阅读，检视卡可按任意键或点击外侧关闭。
- 开局三位同伴在长桌周围轮流交谈，北侧缺口留给从门口进入的侦探。跨过活动厅门槛后，主厨、工程师、音乐家间隔0.3秒转向来人，侦探自动走到独立站位。问到称呼时打开上半屏纸面登记卡；姓名去首尾空白后须为1–16字，不能与三位同伴、侦探或铜口重名，可回车确认或使用建议名。命名期间锁住移动及游戏快捷键，姓名接入发言、此前回看、物证簿来源和结局。对白默认旁听者看说话人，也支持指定谁看谁及恢复跟随说话人；广播时看向铜口，内部质询时看侦探。随后回自己的房间调查窗、床、抽屉、废纸篓，再到长桌首次会合。
- 首次会合选择工程师（门厅）、主厨（备餐间）或音乐家（研习室）为搭档，同行检视六处物件；另一组的勘验在长桌转述，物证簿注明来源。其他三间宿舍从开局起隐藏内部且保持实碰撞，仅当前搭档在身边时可查看其房间。宿舍门和生成脚本使用相同权限配置。
- 六处同行勘验后回长桌，交换见闻并开始圆桌讨论，此时开放脑内推演。脑内三项假说检验由实际 Core 反例执行产生反馈，不改变会议认可状态。完整自编规则重演与物证相容后，在会议面板选择说法、出示相关证物；错证保留争议，三项正确回应后同伴授权答辩。
- F首次先显示脑内页，再在该页上播放侦探心声，结束后恢复输入。五个人分别有自己的草稿页，切换人物会保留各页内容；页只负责整理，可在任一页写任一人物；执行按句中行动者归组，观察条件必须来自行动者自身。句号、分号、换行或“然后”分隔该人物的行动。24类动作、43个现场对象、6个部位与13类条件在词汇页公开，不按职业限制动作。词条插入当前光标。语法非法整稿不运行；物理上做不到的行动记录明确失败。支持改写、删除、粘贴及离开后保留草稿。
- 五页从同一历史初态重演。`每当`声明持续条件规则；每次轮到人物决策，都重新检查其当前位置可见的条件，先选`优先`规则，再选普通规则；`禁止`约束同一人物对同一对象的动作。条件一次成立触发一次，条件失效后可重新触发。旧顺序动作仍可使用，持续规则不会被等待中的行号堵住。推演支持移动、检视、已知谈话、会议与传声回路，以及推拉门、撑撤包拆楔、插拔内销、落抬外闩、移回止挡、擦地、开关水阀及擦窗。烟条件在门撑开后等待环境事件。跨门侧须先打开未锁门。观察和谈话不能凭空给出未调查记录；持续时间、任意自然语言、传递新消息和未建模动作仍未实现。
- 回放可点“跟随／门厅／宿舍／研习室／备餐间”切换摄影机，也可直接拖动画面；视角操作不更改现实或推演状态。推理页低音量循环播放项目已有的钢琴独奏，可关闭，退出淡出。
- 从头重演后，可回看、暂停／继续或进入下一幕。关键痕迹出现时自动暂停，左侧一次只展示一处预测；错误设想与已调查物证的具体冲突按处呈现。未调查位点先记下位置，再回现场按 E 印证。核对不限制次数、不推进现实时间，也不生成已证实动机。草稿修改后旧结果失效；提交必须使用当前草稿完整播放后的结果，且与已调查物证相容。
- 铜口答辩三槽为开门之因、关门之因、清理与内外锁止。三槽共用证据可用性与提交校验，统一合格结局 `ending_passed`；不再要求判断学校/牢房或旧居历史。正式提交须站在铜口附近、接通拉柄、获得同伴认可，并使用当前完整回放结果。

## 制作结构

地图为 32×32 基础格，四宿舍、主走廊、研习室、备餐间和门厅在一个连通平面内。`FirstAwakening.tscn` 保存建筑精灵、墙与家具碰撞、家具、四人、隔离门、房门、灯光、尘粒、相机和 HUD 节点；`RoomDetails.gd` 只负责灯火微动与门厅痕迹绘制。建筑和地毯由32px整数绘制配方产出。家具和四名角色按脚底 Y 排序。人物使用现有 `animation_v2` 64×64 透明承载格，人物本身没有放大；世界相机统一2倍最近邻显示。

当前家具、道具及生活翼建筑来自 `ShareAsset/Internal/Props/FirstAwakeningInterior/`，由 `tools/build_first_awakening_interior.py` 调用 `tools/first_awakening_art/` 的原生像素配方直接绘制；不是从 image_gen 源图裁切、缩放或量化得到。早期 image_gen 源图和提示记录保留在 `AssetsSource/Internal/Props/FirstAwakeningInterior/` 作为历史来源，不是现行重建输入。各资产实际尺寸见运行时 `manifest.json`。物证检视卡仍是独立的 22 张 image_gen 图，源 PNG 位于 `AssetsSource/Internal/Props/FirstAwakeningEvidence/`，来源说明与原样发布入口见 `ShareAsset/Internal/Props/FirstAwakeningEvidence/README.md`；推理页 `mind_seal.png` 则是独立的 24×24 原生逐点配方 `tools/first_awakening_art/mind_ui.py`。这些生产来源说明不代表全部艺术已验收通过。原 External/Victorian 素材保留，本场景不再用其旧家具裁片混搭。

床体在人物之后、前层被褥在人物之前，起床落点为床前；四间宿舍采用相同墙面、寝具、地毯、家具和相对摆放位置，不按职业布置工具、乐谱或生活杂物。三张可检查书桌均已清空，房间保留编号差异。侦探外衣的袖口灰痕与纸篓包楔属于保留的案件线索；其他纸篓为空。共同生活痕迹被恶魔清理是作者侧设定，现场文案只描述缺失的东西，不直接揭示清理者。公共区域的案件物证仍保留。`RoomDoor.gd` 先核对房主陪同权限，再按人物距离开启门扇并切换真实门叶碰撞；未授权房间覆盖内部。隔离门底图不含内销，独立销体根据现场操作或 Core 回放结果显示；配重、绳索、检视盖与止口保持可见。现场和脑内各自持有图像节点，脑内复用相同的地毯与陈设，不能改变现实状态。

宿舍下侧为连续的墙顶、灰泥与木护墙板，四扇房门嵌入立面，门洞贯穿3行墙厚；走廊净地面64px。门扇锚点y=384，调查点、工程师站位及地毯随之调整。公共房间南侧同样有向外立面，并保留两处门厅通道。门框与门扇分绘，开门时不使用遮住人物的实心黑底；脑内复用相同门框宽度。

宿舍门牌从西至东为R-05至R-08，门框统一深胡桃木材质，与灰泥及木护墙板衔接。主角步行动画在本场景独立的SpriteFrames副本中由10fps调为7.5fps（右向镜像同用左向），移动速度仍为90px/s；其他动作与共享角色资源不受影响。

艺术调整保留32px规格：冷灰蓝灰泥墙面与暖灰木地板分层，稀疏纹样和细织纹代替密集菱形地毯。现场采用冷色环境光、窗边漫射冷光、暖色油灯；墙体按行合并为遮光轮廓，家具按实际占地遮光，人物脚底遮光轮廓跟随移动。它们是独立的 `LightOccluder2D`，不改物理碰撞或寻路；灯光采用轻度PCF5过滤。当前是俯视2D足迹投影，并非完整三维高度阴影；脑内维持较中性的独立展示光线。

当前室内资源重建入口为 `python godot_csharp/tools/build_first_awakening_interior.py`。该入口从 `tools/first_awakening_art/` 配方生成 PNG，并同步生成可读的 `FirstAwakening.tscn` 与 `ShareAsset/Internal/Props/FirstAwakeningInterior/layout.tres`；`layout.json` 仅留给现有空间审查工具，运行时读取 `.tres`。只更新场景和布局资源、不改 PNG 时使用 `--scene-only`。调查点名称、翻译编号、位置、检视文字与插图键在 `sites.tres`；生成器按该资源为43个调查点写入 `Area2D` 和范围形状，演员的触发区随人物移动。起床落点、集会站位、浮窗边界与底板也在场景中。答辩选项及其证据要求在 `answers.tres`，菜单和提交共用 `AnswerRules.gd` 校验；物证簿分类、会议所需物证和回放线索在 `investigation_content.tres`。现场点击命中与寻路由 `FieldNavigation.gd` 计算，调查浮窗的排版由 `InvestigationPanel` 上的 `FieldModal.gd` 管理；`FirstAwakening.gd` 保留玩家输入、案件进度及流程衔接。对话在 `first_awakening.dialogue`；推演纸面在 `MindNotebook.tscn`，回放舞台在 `ReplayStage.tscn`，固定证据卡在 `EvidenceDetail.tscn`，回放中的关门门叶在 `ReplayDormDoor.tscn`。场景专用主题和回放着色材质均为可读 `.tres`。重建会按配方覆盖 `FirstAwakening.tscn`，因此家具或建筑的永久改动须同步修改配方；`sites.tres`、`answers.tres` 与 `investigation_content.tres` 独立编辑。`rebuild_first_awakening_tilemap.py` 与 TileMapWorkflow 是旧样板路径，不用于当前初醒场景重建。

`csharp/HallReplayProgram.cs` 解释 STEP/WAIT_SMOKE，并兼容旧IF/GOTO，限定人物只有到门厅后才能据热烟行动；整稿预验证拒绝非法或数字枚举行动。`csharp/HallCausalityRules.cs` 计算门、木楔、灰迹、包纸、双锁和擦拭状态，撤楔后的配重合门也在Core结算。`HallReplayBridge.cs` 返回初态及每个事件后的真实状态、行动成功标记和可读字幕；失败不绘成成功，游戏层不再重算状态。现场状态与推演状态分开。

人物交谈、四人会议、铜口提问与结局均由现有 Dialogue Manager 插件读取 `first_awakening.dialogue`。场景复用 `StoryDialogue.gd` 作为气泡外观；物证卡、推演盘与答辩词槽是独立操作界面，不解析或推进对话。

## 验证与交接

2026-09-29 取名、讨论朝向与宿舍修订（含已有未提交修改的工作区）：姓名窗口在工程师询问称呼时实际等待输入，确认后对白插入姓名；语言切换及回看保持所填姓名。四间宿舍的配方、场景、碰撞与调查点同步为近乎相同的制式陈设，清空职业桌面；旧寝具检视图与现行状态不符，四处引用已解除，历史图与源稿仍保留。恶魔清理共同生活痕迹仅记录为作者侧事实；玩家对白没有直接指认清理者。

`check_first_awakening.gd` 三搭档分支 PASS，覆盖中文姓名输入、空名禁用、Escape不能跳过、移动不穿透窗口、初见朝向及调查/答辩流程。`full_input_playthrough.gd` 在 OpenGL 实际渲染中以键鼠输入取名并完整通关 PASS，现场截图已更新；新增 `full-playthrough-name-entry.png` 与 `full-playthrough-arrival-discussion.png` 位于 `artifacts/first-awakening/`。已查看这两张截图；技术操作通过，人物演技与审美仍待用户审看。构建0警告0错误，三语界面/当前开场首句、语言切换、行动语言与回放翻译检查0失败。旧语言检查的条件观察者与行动者不同，已按现行同主体约束更新检查输入，未修改规则实现；完整英日文长篇开场仍未适配。资产检查584资源、618依赖路径、0失败。部分 headless 检查退出仍报对象/资源未释放，OpenGL可视通关没有此退出提示；不作为已修复项。命令使用 Godot 4.6.1 .NET、`--fixed-fps 60`，headless 三分支与 OpenGL 全程日志分别复用 `check-first-awakening-run.log`、`visual-playthrough-run.log`，不把自动检查耗时当作人类游玩时长。

2026-09-29 场景流程修订验收（含既有未提交修改的工作区）：`check_first_awakening.gd` 三搭档均 PASS；`full_input_playthrough.gd` 完整键鼠输入 PASS。后者从真实移动进入广播、各自查房、工程师同行六点、长桌讨论，再通过 F 输入五页规则并播放23个事件，出示三组证物，点击三个新答辩菜单并提交至 `ending_passed`；未直接写进度或人物位置。命令采用 `--headless --fixed-fps 60 --log-file <仓内绝对路径>`，全程脚本另传 `-- --no-screenshots`；固定帧步60fps用于加快自动检查，不作为人类游玩时长。日志复用 `check-first-awakening-run.log` 与 `full-playthrough-run.log`。行为规则运行检查0失败；Core Release和Godot工作区增量构建均0警告0错误，原插件完整编译的可空性告警记录仍保留。最终资产检查582资源、615依赖路径、0失败。

本轮同时修正连续坐标转寻路格时强行放行撞椅子的终点，以及搭档卡在旧目标后不重新规划的问题；实际跟随与六点检视通过。现实房间的可见性由房主陪同控制，脑内复制只展示实际见过的宿舍，不借现实同伴占位规划模拟路径。运行仍有系统根证书读取提示；最终两项交互检查没有脚本错误或退出警告。可视运行权限申请被拒绝，本轮没有更新旧截图，画面与新增微小痕迹仍待可视验收；新增开场及流程文案为中文，完整英文/日文剧情尚未适配。历史截图和旧双解结局通过记录不代表本次画面验收。

2026-09-29 行为规则修订：驱动人物动作的规则就是动机，复用原纸面。持续条件、优先、禁止和跨页人物组合已接入 `HallReplayBridge.cs`；物理结果仍由原 Core 结算。语言检查3163项、规则运行检查、原纸面操作检查与更新后的主场景检查通过；.NET构建0错误，现有Dialogue Manager插件有23项可空性警告。实际键鼠事件走完调查、会议、五人规则输入、完整回放和铜口提交，通过现有结局，日志为 `artifacts/first-awakening/full-playthrough-run.log`。该路径选择有效会议证据；未覆盖的旧弱证据分支仍有缺失方法问题，不计为已通过。运行有系统根证书读取告警，部分检查退出有对象未释放告警。原纸面可视运行截图为 `artifacts/first-awakening/behavior-rules-paper.png`。

当前仍是24类动作、13类条件的有限解释器，未接通通用规划、任意自然语言、任意工具替代、持物占用或新消息传递。持续规则的新句式目前以中文实现。新版 `activity_hall_opening.dialogue` 保留为创作依据；其相识、广播、质询、分组开场已接入 `first_awakening.dialogue`，原稿后半的自动解说改由检视、脑内推演和会议证物操作承担。下方旧日期的通过记录不代表本轮重新验证。

后续视觉调整：按用户要求暂时恢复彩色推演，`ReplayInk.gdshader`饱和度为0.8；共享该显示处理的证据缩图同步恢复彩色。纸面保持原样。更新实机截图，纸面检查36项通过；下方黑白描述为本日先前修订记录。

2026-09-27 组合推演与初见修订：回放取消灰绿洗色，采用中性黑白。角色位置由执行结果中的人物与目标驱动；水流随状态出现，回看撤去后发效果，逆操作后不能把历史锁止痕迹当作当前双锁核对。原场景全套检查及真实键鼠通关到结局PASS；纸面渲染检查36项通过，组合语言3132项通过。截图 `paper-composed.png` 展示跨人物组合；首次能力引导见 `playtest-mind-introduction.png`。本记录不代表任意自然语言或最终艺术验收。

2026-09-27 纸面版本：`MindNotebook.tscn`固定1280×992双页布局；纸底、折缝、墨线及按钮切片由 `tools/first_awakening_art/mind_ui.py` 的原生点线配方制作，通过既有MCP `ui_workflow.build_diegetic_ui(replace=True)`发布到同一资产族。生产清单为 `AssetsSource/Internal/Props/FirstAwakeningInterior/mind-ui.json`，包含源码及产物哈希；长期MCP进程检测到配方修改时要求重启，防止用旧模块覆盖新资源。文字仍是可编辑控件。下文深绿背景记录为历史版本，当前以整幅浅纸面为准。

本轮渲染检查 `paper_notebook_check.gd` 25项通过；原 `check_first_awakening.gd` 全套通过；`playtest_ui.gd`真实键鼠事件验证中文输入、插词、保留草稿、错误定位和回放通过。语言独立检查479项通过。截图见 `artifacts/first-awakening/paper-empty.png`、`paper-words.png`、`paper-result.png`、`paper-error.png`、`paper-blocked.png`。导入仍出现现有Dialogue Manager编辑器配置写入失败；运行有系统证书及着色器缓存写入警告。当前未验证窄屏、200%字级或最终玩家审美验收。

详细美术规格、全套物料清单与重构指南见：[初醒 Demo 美术交接指南](../../../docs/academy_setting/first-awakening-art-handoff.md)。

```powershell
dotnet build godot_csharp/DualOntologyWorldEngine.sln
& 'C:\Users\iii\Desktop\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe' --headless --path godot_csharp --script res://scenes/FirstAwakening/check/check_first_awakening.gd
& 'C:\Users\iii\Desktop\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe' --headless --path godot_csharp --script res://tools/check_asset_layout.gd
```

2026-09-26 门厅与交互清理：已修正门厅隔离门阈值交互坐标与未检视物证优先判定，消除实体碰撞线与内销调查点的错位，连续实机走位与调查已畅通。临时运行日志与冗余抽样帧已清理。
2026-09-26 界面复修：真实按钮回调检查覆盖人物选择、条件时机、改句／撤句、从头重演、回看、旧结果核对禁用、返回现场与检视卡恢复；原有 Core 正反例、三次位点检验、会议辩驳和两种通过结局回归通过。此前资源布局检查为 500 个资源、511 条依赖路径、0 失败。自动检查不代替玩家实机推理与最终美术验收。

脑内工作区恢复 Reasoning 的深绿色背景、米色书写纸，正文当前优先使用简体中文系统楷体；清松手写体5保留为候选。移除数字输入、人物下拉框、排序箭头、删除叉及诊断日志堆叠。截图检查覆盖空白案卷、已写假设、改写状态和错误因果结果；发现并修复过窄标题、底部溢出与共享字体重复变更引发的排版错误。回放字幕不显示内部行动码。现实位置与脑内场景继续独立。

2026-09-26 推理页迭代：先用 `artifacts/first-awakening/reasoning-design-review.md` 列出玩家输入、规则反馈和验收点，再以实际 Godot 截图检查版式。页眉印记为 `tools/first_awakening_art/mind_ui.py` 在原生 24×24 像素上逐点绘制；此图与既有家具生成稿的制作方式不同。背景乐运行时副本为 `ShareAsset/Internal/Props/first_awakening_reasoning_piano.mp3`，源曲保留在 `music/The Unfallen Piano Solo.mp3`。

按用户纠正，已删除本轮擅加的谜底暗示、引导问题、调查建议和说明性副标题。空白案卷不预填事件结论；推演痕迹正文直接取当前回放位置的 Core 结果，不额外编写解释或提示。

门厅删除原有地面机关剖面，隔离门接入南墙立面并具备建筑占地碰撞。地图 MCP 审查门洞方向／净空、家具占地与交互站位，实际家具由直接捕获脚本导出，再经 `check/audit_runtime.py` 送入真实 MCP 会话；报告固定为 `artifacts/first-awakening/runtime-spatial-audit.json`。静态报告不能替代动态移动：本轮真实18秒集会回归发现厨师被立灯阻挡，根因为寻路只扩张8px，而人物脚底为向前12px的16×12盒。现在寻路使用相应非对称净空，不删除家具碰撞来掩盖问题。

证物卡继续使用先前内置 image_gen 生成的 22 张检视图，本轮未更换检视卡；本轮新增的室内游戏精灵属于上文 FirstAwakeningInterior 资产族。检视卡源图与提示记录位于 `AssetsSource/Internal/Props/FirstAwakeningEvidence/`，运行时副本位于 `ShareAsset/Internal/Props/FirstAwakeningEvidence/`，通过 `res://tools/build_first_awakening_evidence.gd` 原样发布。

验收截图和日志固定归仓库 `artifacts/first-awakening/`，新增 `replay-empty-preview.png`、`replay-edit-preview.png`、`replay-conflict-preview.png`，并更新 `replay-preview.png` 与门厅截图。场景为包含既有未提交修改的工作区；本轮技术检查及截图不能替代用户审美验收。受当前执行环境限制，检查命令须增加 `--log-file` 指向此目录的绝对路径；系统证书读取警告不影响离线演示。

2026-09-26 对话与推理界面调整：初醒采用独立启用的简洁对话布局，保留说话人、正文、继续/选项及回看，隐藏重复章节名和按键提示；答辩按三个问题逐项展开已解锁词块，未取得依据的选项不展示。行为草稿采用TextEdit直接输入受限行动句，编译后仍交由现有Core执行，并非任意自然语言判案。新增回归覆盖光标插词、非法行定位、修正恢复提交、旧结果核对禁用及离开后草稿保留。


## 交互流程修订（2026-09-27）

电话接通与初次广播、广播前后相识、大门反复操作、物证回应、共同离场确认均由 `first_awakening.dialogue` 编排。场景方法只提交相应状态与界面切换。大门可拔/插销、推/拉、查看检视孔与机构后继续操作；看过外闩前，推门失败只反馈另一侧的阻力。人物从互相提醒和分工开始，对未证实解释保留异议。

组合动作规格见 [动作表](../../../docs/detective-reasoning-interface.md#初醒组合动作表与执行边界)，涵盖24动作、13条件及当前实现缺口。英文/日文 Dialogue 翻译由 `python godot_csharp/tools/update_first_awakening_po.py --check` 校验；中文以 Dialogue 为唯一源，源文变化不得沿用旧译文。

`check/interaction_flow_check.gd` 从实际可见按钮验证一次接通、晚初见、门操作循环、脑内说明层级、正确/错误物证及取消/确认离场。截图固定为 `artifacts/first-awakening/interaction-*.png`。这不代替未读谜底的玩家试玩。

本轮验证：Godot完整资产重建与导入完成；.NET构建0警告0错误；交互流程检查与三语集成均0失败；全程键鼠通关及原主检查均PASS（含两种答辩结局与重启）。日志为 `artifacts/first-awakening/interaction-flow.log`、`interaction-i18n.log`、`full-playthrough-run.log`、`check-first-awakening-run.log`。已查看电话、大门和脑内说明截图；尚未进行未读谜底玩家的节奏与推理体验验收。
