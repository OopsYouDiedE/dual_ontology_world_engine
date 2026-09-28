# 初醒 Demo

默认入口 `res://scenes/Main2D.tscn` 加载 `FirstAwakening.tscn`。这是独立可玩的生活翼调查片段；宣传前三场仍可分别打开 `res://scenes/Story/CentralHall.tscn`、`Breakfast.tscn` 和 `ParlorGathering.tscn`，茶案继续在 `scenes/Reasoning/`。

## 操作

初醒场景支持中文、英文、日文。场景 HUD 与脑内推演页都有语言选择器；选择会保存在 `user://language.cfg`，下次启动自动恢复。Godot `TranslationServer` 负责界面和场景文字，Dialogue Manager 使用带稳定 ID 的 `first_awakening.dialogue` 与 `i18n/en.po`、`i18n/ja.po` 翻译对白。脑内行动编辑器按当前语言显示词汇和语法，并保留旧中文草稿的解析能力。可编辑译文统一置于 `i18n/`：场景界面 `first_awakening_ui.csv`、人物名 `dialogue_names.csv`、行动语言 `mind_language.csv`、回放 `replay_ui.csv`、纸面界面 `mind_ui.tsv`；后者用 `tools/build_mind_ui_translations.py` 生成三语 PO。翻译引用分别使用 `FA_UI_###`、`FA_NAME_###`、`FA_LANG_###`、`FA_REPLAY_###`、`FA_MIND_###`，对白用 `FA_###`。新增文案须分配新编号、填写三语并运行 `check/i18n_integration_check.gd`、`check/mind_i18n_language_check.gd`、`check/replay_i18n_check.gd`、`check/locale_switch_check.gd`。打包游戏时须包含 `i18n/replay_ui.csv` 原文件，桥接层用它把 Core 的规则文案对应到编号。

脑内推理页的中文手写区使用 `ShareAsset/External/Yozai/Yozai-Regular.ttf`，中文印刷区与回放字幕使用 `ShareAsset/External/ZhaohuaTypeWriter/ZhaohuaTypeWriter.ttf`。英文手写区使用 `ShareAsset/External/playwriteustrad/PlaywriteUSTrad[wght].ttf`，印刷区使用 New Tegomin。日文手写区使用 `ShareAsset/External/ZenKurenaido/ZenKurenaido-Regular.ttf`，印刷区与字幕使用 `ShareAsset/External/newtegomin/NewTegomin-Regular.ttf`。字体只由本场景的 `MindNotebook.tscn`、`MindPresentation.gd`、`ReplayStage.gd` 和专用 `mind_theme.tres` 引用；外部字体目录保留各自授权文件。
固定纸面按钮的外观集中在可读的 `paper_button_theme.tres`，并由 `MindNotebook.tscn` 引用；运行时仅按语言更新字体。
脑内推理页的操作符号使用 Google Material Symbols Outlined；来源、字体文件与 Apache 2.0 许可保存在 `ShareAsset/External/MaterialSymbols/`。

- WASD / 方向键移动，或左键点击可行走的地面自动寻路；接近点击位置 8px 时停下，近身点击不会使角色抖动。左键点击微光物件或人物会自动走到交互范围并触发调查、交谈或操作；靠近后仍可按 E / Enter 交互。手动方向输入会取消点击路线。
- J 打开物证簿；F 打开脑内推演。浮窗中可用滚轮阅读，检视卡可按任意键或点击外侧关闭。
- 在研习室拉下铜口拉柄：首次点击连续呈现拉柄说明、接通、广播与询问选项，无需退出后再点。松开拉柄即断开；正式提交须站在铜口附近且重新接通。
- 广播后到研习桌召开四人会议。选择一个待讨论说法后，在Dialogue中出示已取得的物证；不相关物证得到具体回应并可重选，不会记为通过。三项讨论完成后，同伴请侦探代为陈述，会议面板提供“打开脑内推演”。铜口答辩页同时写明下一步需要分别编写人物草稿、共同重演并现场核对三处痕迹；会议已反驳的说法不再列为可选答辩词块。
- F首次先显示脑内页，再在该页上播放侦探心声，结束后恢复输入。五个人分别有自己的草稿页，切换人物会保留各页内容；每页只能写本人观察和行动。句号、分号、换行或“然后”分隔该人物的行动。24类动作、38个现场对象、6个部位与13类条件在词汇页公开，不按职业限制动作。词条插入当前光标。语法非法整稿不运行；物理上做不到的行动记录明确失败。支持改写、删除、粘贴及离开后保留草稿。
- 五页从同一历史初态重演。桥接层按固定人物次序轮询各页下一项，未发生的条件等待其他人物改变状态；所有人都无法推进时显示条件失败。推演支持移动、检视、已知谈话、会议与传声回路，以及推拉门、撑撤包拆楔、插拔内销、落抬外闩、移回止挡、擦地、开关水阀及擦窗。烟条件在门撑开后等待环境事件。跨门侧须先打开未锁门。观察和谈话不能凭空给出未调查记录；持续时间、任意自然语言、传递新消息和未建模动作仍未实现。
- 回放可点“跟随／门厅／宿舍／研习室／备餐间”切换摄影机，也可直接拖动画面；视角操作不更改现实或推演状态。推理页低音量循环播放项目已有的钢琴独奏，可关闭，退出淡出。
- 从头重演后，可回看、暂停／继续或进入下一幕。关键痕迹出现时自动暂停，左侧一次只展示一处预测；错误设想与已调查物证的具体冲突按处呈现。未调查位点先记下位置，再回现场按 E 印证。每轮最多检验三处，每处推进发条时间十分钟。草稿修改后明确标记旧重演，禁止拿旧结果核对新假设；已验证猜想保留。
- 传声口支持历史解与在无独立日期基准下仍相容的新生解。错误论证有具体反例，可继续调查、重编推演再提交；成功论证结束后隔离门切换为开启画面。三次拒绝离场后只能留下继续调查。

## 制作结构

地图为 32×32 基础格，四宿舍、主走廊、研习室、备餐间和门厅在一个连通平面内。`FirstAwakening.tscn` 保存建筑精灵、墙与家具碰撞、家具、四人、隔离门、房门、灯光、尘粒、相机和 HUD 节点；`RoomDetails.gd` 只负责灯火微动与门厅痕迹绘制。建筑和地毯由32px整数绘制配方产出。家具和四名角色按脚底 Y 排序。人物使用现有 `animation_v2` 64×64 透明承载格，人物本身没有放大；世界相机统一2倍最近邻显示。

当前家具、道具及生活翼建筑来自 `ShareAsset/Internal/Props/FirstAwakeningInterior/`，由 `tools/build_first_awakening_interior.py` 调用 `tools/first_awakening_art/` 的原生像素配方直接绘制；不是从 image_gen 源图裁切、缩放或量化得到。早期 image_gen 源图和提示记录保留在 `AssetsSource/Internal/Props/FirstAwakeningInterior/` 作为历史来源，不是现行重建输入。各资产实际尺寸见运行时 `manifest.json`。物证检视卡仍是独立的 22 张 image_gen 图，源 PNG 位于 `AssetsSource/Internal/Props/FirstAwakeningEvidence/`，来源说明与原样发布入口见 `ShareAsset/Internal/Props/FirstAwakeningEvidence/README.md`；推理页 `mind_seal.png` 则是独立的 24×24 原生逐点配方 `tools/first_awakening_art/mind_ui.py`。这些生产来源说明不代表全部艺术已验收通过。原 External/Victorian 素材保留，本场景不再用其旧家具裁片混搭。

床体在人物之后、前层被褥在人物之前，起床落点为床前；四间宿舍保留统一房壳，工程师桌边留出拆检位置，厨师将桌与收纳集中一侧，音乐家桌椅错开留出练习空地；新增三张可检查书桌，窗、寝具、门牌各自记录可观察差异。`RoomDoor.gd` 按人物距离开启门扇，同时切换真实门叶碰撞。隔离门底图不含内销，独立销体根据现场操作或 Core 回放结果显示；配重、绳索、检视盖与止口保持可见。现场和脑内各自持有图像节点，脑内复用相同的地毯与陈设，不能改变现实状态。

宿舍下侧为连续的墙顶、灰泥与木护墙板，四扇房门嵌入立面，门洞贯穿3行墙厚；走廊净地面64px。门扇锚点y=384，调查点、工程师站位及地毯随之调整。公共房间南侧同样有向外立面，并保留两处门厅通道。门框与门扇分绘，开门时不使用遮住人物的实心黑底；脑内复用相同门框宽度。

宿舍门牌从西至东为R-05至R-08，门框统一深胡桃木材质，与灰泥及木护墙板衔接。主角步行动画在本场景独立的SpriteFrames副本中由10fps调为7.5fps（右向镜像同用左向），移动速度仍为90px/s；其他动作与共享角色资源不受影响。

艺术调整保留32px规格：冷灰蓝灰泥墙面与暖灰木地板分层，稀疏纹样和细织纹代替密集菱形地毯。现场采用冷色环境光、窗边漫射冷光、暖色油灯；墙体按行合并为遮光轮廓，家具按实际占地遮光，人物脚底遮光轮廓跟随移动。它们是独立的 `LightOccluder2D`，不改物理碰撞或寻路；灯光采用轻度PCF5过滤。当前是俯视2D足迹投影，并非完整三维高度阴影；脑内维持较中性的独立展示光线。

当前室内资源重建入口为 `python godot_csharp/tools/build_first_awakening_interior.py`。该入口从 `tools/first_awakening_art/` 配方生成 PNG，并同步生成可读的 `FirstAwakening.tscn` 与 `ShareAsset/Internal/Props/FirstAwakeningInterior/layout.tres`；`layout.json` 仅留给现有空间审查工具，运行时读取 `.tres`。只更新场景和布局资源、不改 PNG 时使用 `--scene-only`。调查点名称、翻译编号、位置、检视文字与插图键在 `sites.tres`；生成器按该资源为38个调查点写入 `Area2D` 和范围形状，演员的触发区随人物移动。起床落点、集会站位、浮窗边界与底板也在场景中。答辩选项及其证据要求在 `answers.tres`，菜单和提交共用 `AnswerRules.gd` 校验；物证簿分类、会议所需物证和回放线索在 `investigation_content.tres`。现场点击命中与寻路由 `FieldNavigation.gd` 计算，调查浮窗的排版由 `InvestigationPanel` 上的 `FieldModal.gd` 管理；`FirstAwakening.gd` 保留玩家输入、案件进度及流程衔接。对话在 `first_awakening.dialogue`；推演纸面在 `MindNotebook.tscn`，回放舞台在 `ReplayStage.tscn`，固定证据卡在 `EvidenceDetail.tscn`，回放中的关门门叶在 `ReplayDormDoor.tscn`。场景专用主题和回放着色材质均为可读 `.tres`。重建会按配方覆盖 `FirstAwakening.tscn`，因此家具或建筑的永久改动须同步修改配方；`sites.tres`、`answers.tres` 与 `investigation_content.tres` 独立编辑。`rebuild_first_awakening_tilemap.py` 与 TileMapWorkflow 是旧样板路径，不用于当前初醒场景重建。

`csharp/HallReplayProgram.cs` 解释 STEP/WAIT_SMOKE，并兼容旧IF/GOTO，限定人物只有到门厅后才能据热烟行动；整稿预验证拒绝非法或数字枚举行动。`csharp/HallCausalityRules.cs` 计算门、木楔、灰迹、包纸、双锁和擦拭状态，撤楔后的配重合门也在Core结算。`HallReplayBridge.cs` 返回初态及每个事件后的真实状态、行动成功标记和可读字幕；失败不绘成成功，游戏层不再重算状态。现场状态与推演状态分开。

人物交谈、四人会议、铜口提问与结局均由现有 Dialogue Manager 插件读取 `first_awakening.dialogue`。场景复用 `StoryDialogue.gd` 作为气泡外观；物证卡、推演盘与答辩词槽是独立操作界面，不解析或推进对话。

## 验证与交接

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
