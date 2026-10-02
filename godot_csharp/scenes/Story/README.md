# 宣传分镜前三场 · 剧情打样

本页记录现有三场的操作与验收，场景顺序沿用旧宣传分镜编号。[首支宣传片主线大纲](../../../docs/design-standard.md#第六部分宣传视频与成果验收规范)从可玩日常中选取需要的片段；茶案是独立机制原型，不在这三场的人物或美术扩产中。

`CentralHall.tscn` 可直接打开进入这组剧情打样；默认 `Main2D.tscn` 当前进入初醒 Demo。打样内部可用 F6/F7/F8 或对白区上方按钮切换三场；中央厅相应门廊也可进入早餐厅/娱乐室，二者南侧入口返回中央厅。兼容入口 `Shot01.tscn`、`Shot02.tscn` 和 `Parlor.tscn` 分别实例化 Story 的中央厅、早餐厅和围谈场景，不再维护平行摆景。

## 操作与录制

当前完整日常片见[15.10秒视频与验收](../../../artifacts/first-three-scenes/daily-montage.md)：早餐递碗、教师示意、娱乐室邀座。每段仅一至两名主行动者，其余静止，无字幕或对白框。娱乐室家具已统一正向平行斜俯视，修正切片与座垫接触。

窗口1280×992，上方1280×768为画面。早餐演出4倍整数镜头，授课和娱乐室3倍；不单独缩放人物。早餐厅14×9格、娱乐室14×10格，中央厅维持原布局。

`tools/record_breakfast.ps1 -Montage`录制完整三幕；不带参数只录早餐。自动创建并移除viewport覆盖。`check_montage.gd`验证各两次真实M播放、静止旁观者、无文字、结果状态及控制区恢复。早先的`breakfast-action.mp4`为6倍近景动作参考，当前完整构图以daily-montage为准。下方历次记录仅供追溯。
- WASD/方向键移动，靠近人物或家具按 E/Enter。
- 空格/Enter/点击推进对白；选项可用鼠标或键盘选择。
- R 开始或重演本场剧情；对白中不能重启或切场。
- M 播放本场无对白日常片段；片段结束恢复移动与切场。
- H 回看对话，回看时暂停移动和切场。
- F3 显示地图格、动作画布、脚底与碰撞盒。

每场按 R 后从开场对白录制至结束，保留选择画面。中央厅保留音乐家侧耳与钟声表演；早餐厅围绕饭前交谈，娱乐室围绕诗人讲述，盗贼/律师采用坐姿。后二者目前是可操作对白打样，尚未补端盘、取食等专门演技。三场均不是茶案或言灵机制展示，不把对白选择宣称为 Core 规则结算。

2026-09-25 日常片段技术检查：`check_montage.gd` 对三段各按一次 M，检查初次授课的人物、字幕和定时结束，以及后两段碗和画家的位置变化及操作恢复；这些时序检查通过，不涵盖已被否定的第一幕美术和麦斯威尔专用动作。旧中央厅片段的画面帧仍存于 `artifacts/first-three-scenes/daily-CentralHall-*.png`，不属于预定顺序。中央厅原有对白回归 `check_story.gd` 通过。`check_social.gd` 仍以早餐厅调整前的地图宽度、边界碰撞和旧玩家位置作断言，运行时在这些旧断言上失败；它不能作为本次日常片段的通过依据。

## 结构与资源

`StoryDialogue.gd` 共用 Dialogue Manager 的呈现、选择、回看与恢复；`CentralHall.gd` 管理首场走位和钟声，`SocialRoom.gd` 管理早餐/围谈的布局及交互，台词分别在三个 `.dialogue` 文件。`DemoNavigation.gd` 提供样片切换。

`StoryActor.gd` 以脚底为原点，64×64承载格偏移(0,-23)，正常人形约43px，碰撞16×12；角色与家具按脚底Y排序。前三场九名角色均引用 `animation_v2`：侦探、音乐家、自然学家、工程师、主厨、诗人、盗贼、律师、画家。音乐家侧耳仍使用 `story_musician/special.tres`。基础动作的生成源与重建入口见[图集说明](../../ShareAsset/Internal/Characters/animation_v2/README.md)。

`RoomBinding.cs` 将房间绑定为 Core Space，并在游戏层属性里保存实际 TileMapLayer 引用；`RoomItem` 直接持有 Core 物件，以 `SpatialProperty.Space` 归属房间。不向 Core 引入坐标、Godot依赖或实时循环。

装修共用 ReasoningStudy 的32px石地面、木护墙与蓝金地毯，消除娱乐室高对比拼花和不同墙面混搭。大型原家具用 `story_*` 紧凑派生；围谈的石壁炉、暗红沙发、扶手椅、茶桌采用新的一组源稿。餐椅使用已有内部木椅，钢琴仍使用 Victorian。地面/墙面用 TileMapLayer，家具用有脚底锚点的 Sprite2D，碰撞单独按占地设置。早餐厅保留餐桌两端绕行和灶台前通道；娱乐室坐姿朝向围谈中心，坐垫高度用人物显示偏移表现，地面占位仍留在角色根节点。源稿和角色源都有派生缩放，不能称为全套原生像素重绘或最终年代考证通过。

2026-09-25 早餐厅取景调整：以旧 `artifacts/theme-film/scenes/shot-02.png` 为构图参照，将高窗、双灶、备餐台、木门和三人围桌收进同一画面；主厨站在桌后端碗。早餐厅家具的运行时透明画布分别为餐桌224×64、备餐台192×64、木门64×96、边桌64×64、汤碗32×32，均为32px整格倍数。家具从现有 Props 源图派生，汤碗源图与提示词记录在资产说明中。早餐场景可通过 `res://tools/build_story_tiles.gd -- --breakfast-only` 定向重建这些物件。侦探、自然学家和工程师的就座位置为静态演出位；玩家移动时起身，R重演时回到座位。当前画面见 `artifacts/first-three-scenes/Breakfast-review.png`；这次仅做了画面捕获，交互路径仍需实机验收。

## 2026-09-24 比例校正

对照 `artifacts/theme-film/scenes/shot-01.png`（1625×968）、`shot-02.png`（1642×958）、`shot-03.png`（1672×941）。只比较场景画面，不把对白区算入分母；概念图有不一致的透视与像素密度，下面的概念人物高度为目测包围盒估算，不能当作原生帧规格。

| 比较项 | 修改前 | 参考图估算 | 本轮实机 |
| --- | --- | --- | --- |
| 人物高度 / 场景高度 | 43/576 ≈ 7.5% | 中央厅约12–14%，早餐主厨约17%，围谈人物约14–16%（坐姿不可直接等同站姿） | 43/384 ≈ 11.2%，相对提高50%；人物未缩放，镜头整数放大 |
| 世界取景与房间宽度 | 1024×576，32格宽 | 以人物高度折算，约500–650px级横向取景，随构图变化 | 640×384，20格宽；真实可走区域同步收紧，非裁掉周边通路 |
| 中央钟画布高 / 人物高 | 224/43 ≈ 5.2 | 约2.5–3 | 96px图像内容 / 43 ≈ 2.2，放入96×128透明承载格 |
| 早餐桌宽 / 人物高 | 448/43 ≈ 10.4 | 约4–6 | 256/43 ≈ 6.0 |

本轮优先统一可玩房间的空间尺度，不强行把三张不同构图的概念图完全套入同一个镜头。早餐主厨的画面占比仍小于原概念特写；角色衣服、专门盛餐演技、钢琴和部分配件的风格仍与宣传图有差距。画面收紧、素材协调和完整艺术验收分别判断。

家具统一通过 `res://tools/build_story_tiles.gd -- --compact-only` 重建。12组既有道具按比例派生；4件围谈家具当前来自 `AssetsSource/Internal/Props/story_parlor_projection.png`，`story_parlor_furniture.png` 保留为早期源稿。当前转换以脚本中的裁切范围、锚点和输出尺寸为准。运行时仅读取 `ShareAsset/Internal/Props/story_*.res`。alpha包围盒以0.5阈值识别实体，保留裁取后的原始alpha；输出缩放不是原生像素重绘。旧 Shot01/02 纹理源与切片入口保留在 `AssetsSource/Internal/Props/` 和 `tools/shots/`，但其脚本只重建旧源派生贴图，不再写场景；Story 使用 `story_*` 紧凑资源。

Dialogue Manager 已实质接入3.10.4：台词与选项留在三个 `.dialogue` 文件，`do` 调用场景演出方法；`StoryDialogue.gd` 仅负责显示、选择、回看和恢复。本轮调整对白区域与点击边界，没有另外实现对白解析器。

## 验证与限制

2026-09-24 本轮实机：C#构建通过；三场截图和宣传参考并排保存在 `artifacts/first-three-scenes/comparison.html`。中央厅与社交回归均通过，结果为同目录 `story-validation.txt`、`social-validation.txt`，最新日志为 `check-story-presentation-final.log`、`check-social-presentation-final.log`。检查包含对白两分支、真实移动/交互/出入口/切场、回看输入锁，以及本轮新增的交谈朝向、侧耳听钟方向、围谈坐姿末帧与座垫偏移。

人物补了脚底接触阴影、对话目标转向和各房间休息朝向，坐姿人物不再因通用对白动作离开座垫。音乐家侧耳按钟的位置镜像。画家源图的行间残留曾混入侧面帧，导致头顶悬空黑线并影响缩放；已修复导入边界并重建111帧，运行时截图确认残留消失。衣服与发型沿用原稿，未把这些修复描述为整套人物重绘。

Shot01/Shot02/Parlor兼容入口均通过Godot直接加载；旧工具已取消布局生成，仅保留来源可追溯的素材制作。收拢前的相关未提交差异保存在 `artifacts/first-three-scenes/retired-shot-worktree.patch`。旧源裁切脚本通过解析检查，未执行会覆盖现有上游贴图的整批重建；Story紧凑家具和画家图集已实际重建。编辑器导入受到AppData设置写权限限制，离线运行检查通过；退出验收进程仍有对象释放警告。

```powershell
dotnet build godot_csharp/DualOntologyWorldEngine.sln
Godot --path godot_csharp --rendering-method gl_compatibility --log-file artifacts/story-check.log --script res://scenes/Story/check_story.gd -- --capture
Godot --path godot_csharp --rendering-method gl_compatibility --log-file artifacts/scene-capture.log --script res://scenes/Story/capture_samples.gd
```

`check_story.gd` 用真实输入验证中央厅移动、墙碰撞、接近钟、两条对白分支、重演、回看与恢复；启用 `--capture` 时的截图也输出到 `artifacts/first-three-scenes/`。`capture_samples.gd` 输出三场实机截图到同一目录，截图本身不证明交互通过。`artifacts/story-sample/` 保留2026-09-23及更早的历史截图、日志和验证记录，不再作为当前生成目标。当前审美与专门表演仍待验收。

本机Godot会报告系统证书库读取失败；离线场景不依赖网络。显式 `--log-file` 用于避开本机 user:// 日志目录权限问题。构建、帧数和自动交互检查都不替代审美审看。

2026-09-23 当前工作区验证：Core Release 与 Godot C# 增量构建均通过。中央厅 `check_story.gd` 通过墙碰撞、钟交互、键盘/鼠标两分支、音乐家侧耳、重演、回看输入锁和北侧绕行；`check_social.gd` 通过早餐/围谈两分支、人物与家具 E 交互、R复位、回看移动锁、F6/F7/F8真实切场与对白/回看切场锁，以及南门 E 返回中央厅。结果分别在 `artifacts/story-sample/validation.txt` 与 `artifacts/first-three-scenes/social-validation.txt`。

新增社交回归命令：

```powershell
Godot --headless --path godot_csharp --log-file artifacts/social-check.log --script res://scenes/Story/check_social.gd
```

本轮检查修复了家具被送入人物类型数组的错误、选项回调绑定不稳定及切场后访问旧视口的问题；回看打开时选项也不能推进。实机截图核对了木地板、护墙、桌椅与坐姿遮挡。基础动作不能替代端盘/取食等专门表演，装修风格与整体艺术质量仍需审看。编辑器导入在受限环境中会额外报用户配置写入失败；运行时回归没有这一编辑器错误。

