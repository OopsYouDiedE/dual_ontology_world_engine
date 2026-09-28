# 安眠者有福

Blessed Are Those Who Sleep。当前主线是孤岛学园游戏，目标为可游玩、可实际绘制并可录制进宣传片的内容片段。

地图基础格为 **32×32 像素**，角色与大型物件可跨格。制作与完成判定遵循 [设计与验收规范](docs/design-standard.md)。

## 当前入口与状态

- [RPG美术与MCP工作流](docs/tilemap-workflow.md)：Claude现行初醒配方、材质色阶、全量重建比对、Aseprite交接、剧情知识边界与受保护修订、像素Theme及真实字体测量、制作任务闭环；旧47形TileMap样板仍位于 `godot_csharp/scenes/TileMapWorkflow/Preview.tscn`。技术通过与艺术验收分开记录。
- [剧情总脉络与呈现边界](docs/academy_setting/story-spine.md)：区分作者侧历史、玩家首章实际经历和后续设定，列出首章接学园日常的连续性缺口。

- [无文字日常三幕](artifacts/first-three-scenes/daily-montage.md)：15.10秒实机录制，早餐递碗、教师示意、娱乐室依次邀座；旁观者静止，三幕重播与结束恢复已验证，画面待用户审看。

- [宣传前三场人物动作图集](godot_csharp/ShareAsset/Internal/Characters/animation_v2/README.md)：每人111帧位，含Godot资源和[可操作验收室](godot_csharp/scenes/CharacterAnimationPreview/README.md)。

- `godot_csharp/project.godot`：Godot C# 游戏工程。默认 `scenes/Main2D.tscn` 加载[初醒 Demo](godot_csharp/scenes/FirstAwakening/README.md)：四人生活翼调查、门厅因果推演、会议辩驳与双解答辩。[宣传前三场剧情打样](godot_csharp/scenes/Story/README.md)仍可直接打开各场景；旧Shot01/02和Parlor路径保留兼容入口。
- [场景审查与扩产缺口](docs/illustrations-audit.md)：分镜年代、时间线、剧情/机制分类，以及角色动作与TileMap资源盘点。
- [家具喝茶谜原型](godot_csharp/scenes/Reasoning/README.md)：公共研究室调查与家具喝茶重演，当前仍是未完成的可玩原型。
- [首支宣传片现行大纲](docs/游戏和核心宣传视频和图片录制/机制主线宣传片大纲.md)：75–80秒，日常→脑内推演→言灵课堂→真名救场→决战一瞥；设计必须能落实到游戏，现有剧情打样与机制原型尚未组成成片。制作方法见[宣传片指导](docs/游戏和核心宣传视频和图片录制/宣传片指导意见.md)。
- [学园设定](docs/academy_setting/academy-game-design-zh.md)：内容参考，长期剧情不等于本轮实现范围。
- [21人32×48正面站立帧](godot_csharp/ShareAsset/Internal/Characters/README-zh.md)：原生像素角色资源与日常携带规范。

## 构建与验证

使用 .NET 8 和 Godot 4.6.1 .NET：

```powershell
dotnet build csharp/Core.sln -c Release
dotnet build godot_csharp/DualOntologyWorldEngine.sln
dotnet run --project godot_csharp/scenes/Reasoning/check/Reasoning.Check.csproj
```

素材迁移或重建后，用 Godot .NET 先执行 `--headless --editor --path godot_csharp --import`，再执行 `--headless --path godot_csharp --script res://tools/check_asset_layout.gd` 检查运行时依赖和资源加载。输出在 `artifacts/asset-consolidation/resource-validation.json`；同时检查引擎日志，因为资源非空不保证 TileSet 内部配置没有错误。实际交互回归入口见各场景 README。

Core 使用 Essence–Projection 模型及嵌套行动值调度，独立于 Godot。房间内空间、碰撞、显示与交互由游戏层负责。维护约束见 [csharp/AGENTS.md](csharp/AGENTS.md)，原理见 [Core说明](docs/双重引擎运作原理.md)。

## 架构与文件归属

`csharp/` 是无 Godot 依赖的世界引擎，独立构建与本地打包方式见 [Core README](csharp/README.md)。`godot_csharp/` 是游戏宿主，通过 `ProjectReference` 单向引用 Core；房内几何、碰撞、实时表现、UI 与具体游戏内容留在宿主。目录整理不改变现有规则结算与对象绑定方式。

游戏美术生产源统一归 `godot_csharp/AssetsSource/`（由 `.gdignore` 排除导入），游戏直接消费的资产统一归 `godot_csharp/ShareAsset/`。两者都分为 `External/<SourcePackage>/` 和 `Internal/{Characters,Identity,Props}/`：外部保留来源与授权，内部角色、身份物分别归类，其余家具、道具与地形集中归 Props。运行时不得引用源区；未经加工即使用的源纹理也应由生产链路复制到运行时目录。Godot 自身 `.godot/` 导入缓存不是正式资产或原创源。

`docs/` 保存长期设计与设定；`music/` 保存音乐工程与原声；`artifacts/<task-name>/` 保存非运行时概念与验证交付物。本地发行包分别归 `artifacts/releases/core/` 和 `artifacts/releases/game/`，打包不等于发布。仓库级通用工具归 `tools/`，游戏资产生产与 Godot 工具归 `godot_csharp/tools/`。同任务复用既有目录，不创建轮次副本。

## 素材入口与旧线

[Godot素材插件](godot_csharp/addons/rpg_asset_adapter/README.md) 提供统一转换入口。外部素材按原生尺寸读取，32px地图规格不用于盲改源图切片参数。

`music/` 保存配乐源文件；宣传片与美术概念源稿归档于 `artifacts/theme-film/`，`docs/` 中保留长期视觉规范插图。它们不属于已验收的运行时资产。

动物园工作线归入 `archive/zoo/`，不作为本游戏设计依据。
