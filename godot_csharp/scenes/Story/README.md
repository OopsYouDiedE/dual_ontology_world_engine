# 宣传分镜前三场 · 剧情打样

当前范围按宣传分镜排序：中央厅清晨、后勤院早餐、公共娱乐室围谈。茶案是独立机制原型，不在这三场的人物或美术扩产中。

默认 `Main2D.tscn` 加载 `CentralHall.tscn`。F6/F7/F8 或对白区上方按钮切换三场；中央厅相应门廊也可进入早餐厅/娱乐室，二者南侧入口返回中央厅。旧 Shot01/Shot02/Parlor 保留为旧摆景，不是当前入口。

## 操作与录制

窗口1024×800，上方1024×576为32px地图，下方为独立对白区。人物1倍原图、最近邻显示。

- WASD/方向键移动，靠近人物或家具按 E/Enter。
- 空格/Enter/点击推进对白；选项可用鼠标或键盘选择。
- R 开始或重演本场剧情；对白中不能重启或切场。
- H 回看对话，回看时暂停移动和切场。
- F3 显示地图格、动作画布、脚底与碰撞盒。

每场按 R 后从开场对白录制至结束，保留选择画面。中央厅保留音乐家侧耳与钟声表演；早餐厅围绕饭前交谈，娱乐室围绕诗人讲述，盗贼/律师采用坐姿。后二者目前是可操作对白打样，尚未补端盘、取食等专门演技。三场均不是茶案或言灵机制展示，不把对白选择宣称为 Core 规则结算。

## 结构与资源

`StoryDialogue.gd` 共用 Dialogue Manager 的呈现、选择、回看与恢复；`CentralHall.gd` 管理首场走位和钟声，`SocialRoom.gd` 管理早餐/围谈的布局及交互，台词分别在三个 `.dialogue` 文件。`DemoNavigation.gd` 提供样片切换。

`StoryActor.gd` 以脚底为原点，64×64承载格偏移(0,-23)，正常人形约43px，碰撞16×12；角色与家具按脚底Y排序。前三场九名角色均引用 `animation_v2`：侦探、音乐家、自然学家、工程师、主厨、诗人、盗贼、律师、画家。音乐家侧耳仍使用 `story_musician/special.tres`。基础动作的生成源与重建入口见[图集说明](../../ShareAsset/Images/characters/animation_v2/README.md)。

`RoomBinding.cs` 将房间绑定为 Core Space，并在游戏层属性里保存实际 TileMapLayer 引用；`RoomItem` 直接持有 Core 物件，以 `SpatialProperty.Space` 归属房间。不向 Core 引入坐标、Godot依赖或实时循环。

装修采用 Victorian 地面、木护墙、壁炉、烛台、座椅与钢琴；中央厅保留石厅建筑和石砖补件。地面/墙面用 TileMapLayer，家具用有脚底锚点的 Sprite2D，碰撞单独按占地设置。早餐厅保留餐桌两端绕行和灶台前通道；娱乐室围坐区与南侧入口分开。家具源与角色源都有派生缩放，不能称为全套原生像素重绘或最终年代考证通过。

## 验证与限制

```powershell
dotnet build godot_csharp/DualOntologyWorldEngine.sln
Godot --path godot_csharp --rendering-method gl_compatibility --log-file artifacts/story-check.log --script res://scenes/Story/check_story.gd -- --capture
Godot --path godot_csharp --rendering-method gl_compatibility --log-file artifacts/scene-capture.log --script res://scenes/Story/capture_samples.gd
```

`check_story.gd` 用真实输入验证中央厅移动、墙碰撞、接近钟、两条对白分支、重演、回看与恢复。`capture_samples.gd` 输出三场实机截图到 `artifacts/first-three-scenes/`，截图本身不证明交互通过。当前审美与专门表演仍待验收。

本机Godot会报告系统证书库读取失败；离线场景不依赖网络。显式 `--log-file` 用于避开本机 user:// 日志目录权限问题。构建、帧数和自动交互检查都不替代审美审看。

2026-09-23 当前工作区验证：Core Release 与 Godot C# 增量构建均通过。中央厅 `check_story.gd` 通过墙碰撞、钟交互、键盘/鼠标两分支、音乐家侧耳、重演、回看输入锁和北侧绕行；`check_social.gd` 通过早餐/围谈两分支、人物与家具 E 交互、R复位、回看移动锁、F6/F7/F8真实切场与对白/回看切场锁，以及南门 E 返回中央厅。结果分别在 `artifacts/story-sample/validation.txt` 与 `artifacts/first-three-scenes/social-validation.txt`。

新增社交回归命令：

```powershell
Godot --headless --path godot_csharp --log-file artifacts/social-check.log --script res://scenes/Story/check_social.gd
```

本轮检查修复了家具被送入人物类型数组的错误、选项回调绑定不稳定及切场后访问旧视口的问题；回看打开时选项也不能推进。实机截图核对了木地板、护墙、桌椅与坐姿遮挡。基础动作不能替代端盘/取食等专门表演，装修风格与整体艺术质量仍需审看。编辑器导入在受限环境中会额外报用户配置写入失败；运行时回归没有这一编辑器错误。
