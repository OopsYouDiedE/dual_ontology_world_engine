# 安眠者有福

Blessed Are Those Who Sleep。当前主线是孤岛学园游戏，目标为可游玩、可实际绘制并可录制进宣传片的内容片段。

地图基础格为 **32×32 像素**，角色与大型物件可跨格。制作与完成判定遵循 [设计与验收规范](docs/design-standard.md)。

## 当前入口与状态

- [宣传前三场人物动作图集](godot_csharp/ShareAsset/Images/characters/animation_v2/README.md)：每人111帧位，含Godot资源和[可操作验收室](godot_csharp/scenes/CharacterAnimationPreview/README.md)。

- `godot_csharp/project.godot`：Godot C# 游戏工程。默认 `scenes/Main2D.tscn` 加载[宣传前三场剧情打样](godot_csharp/scenes/Story/README.md)：Dialogue对白、选择、角色演出、房间交互与回看。F6/F7/F8切换中央厅、早餐厅、娱乐室；仍待审美审看，旧Shot01/02摆景保留。
- [场景审查与扩产缺口](docs/illustrations-audit.md)：分镜年代、时间线、剧情/机制分类，以及角色动作与TileMap资源盘点。
- [家具喝茶谜原型](godot_csharp/scenes/Reasoning/README.md)：公共研究室调查与家具喝茶重演，当前仍是未完成的可玩原型。
- [宣传片制作要求](docs/游戏和核心宣传视频和图片录制/宣传片指导意见.md)：从可玩片段安排录制。
- [学园设定](docs/academy_setting/academy-game-design-zh.md)：内容参考，长期剧情不等于本轮实现范围。
- [21人32×48正面站立帧](godot_csharp/ShareAsset/Images/characters/README-zh.md)：原生像素角色资源与日常携带规范。

## 构建与验证

使用 .NET 8 和 Godot 4.6.1 .NET：

```powershell
dotnet build csharp/Core.sln -c Release
dotnet build godot_csharp/DualOntologyWorldEngine.sln
dotnet run --project godot_csharp/scenes/Reasoning/check/Reasoning.Check.csproj
```

Core 使用 Essence–Projection 模型及嵌套行动值调度，独立于 Godot。房间内空间、碰撞、显示与交互由游戏层负责。维护约束见 [csharp/AGENTS.md](csharp/AGENTS.md)，原理见 [Core说明](docs/双重引擎运作原理.md)。

## 资源与旧线

[Godot素材插件](godot_csharp/addons/rpg_asset_adapter/README.md) 提供统一转换入口。外部素材按原生尺寸读取，32px地图规格不用于盲改源图切片参数。

`music/` 保存配乐源文件；`artifacts/theme-film/` 与宣传片美术目录保留概念源稿，它们不属于已验收的运行时资产。

动物园工作线归入 `archive/zoo/`，不作为本游戏设计依据。
