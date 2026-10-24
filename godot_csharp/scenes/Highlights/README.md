# 高光舞台

独立纯 GDScript 场景以六个局部舞台展示条件节选。入口：

```powershell
& $env:GODOT --path godot_csharp res://scenes/Highlights/Highlights.tscn -- --highlight=chess_god
```

`--highlight=` 可取 `shura_poet`、`scholar_relic`、`chess_god`、`demon_miracle`、`kitchen_dumplings`、`last_transfer`，省略时从第一段开始。顶部六个按钮及数字键切换舞台，空格或按钮前进对白，WASD/方向键控制现场侦探。切换/重置只重新装配该舞台。

舞台读取 `Contracts/highlight_stages.json` 的 `title`、`condition`、`location`、`lines`。每行包含 `speaker`、`text`，可选 `actor` 和 `cue`；cue 支持 approach、gesture、black_move、white_move、miracle、dumpling、steam、transfer、tide。所有人物对白、场景条件由主代理维护的内容合同提供。

逻辑画布为 512×384，地图由 16×12 个 32 px 格组成，人物脚点和物件脚点共用 Y 排序。画布与镜头整体近看，人物保持32×48与32格的原比例。角色使用 `CharacterBody2D`，房间、物件和身体通过真实局部碰撞阻挡移动。新四人 atlas 每姿态 32×48，既有侦探、厨师、苏泠、雀儿复用 PixelCast，雀儿资源ID为thief。新稿包含三个方向静姿；移动时使用身体行走和轻微步态起伏。

`HighlightsTerrain.tres` 为三个 terrain sets：floor=0（47角边）、wall=1（16四边）、water=2（47岸缘）；material indices 以 `Terrain/asset-manifest.json` 为准。遮罩保留中心16×16，四边使用16宽连接臂，47格式的每个8×8角由对应角位独立决定。场景使用 `set_cells_terrain_connect`；地面下有完整底色。图像原稿保存在 `mcp/asset_pipeline/sources/Internal/`，道具分别发布到 Props/Highlights/Architecture、Furniture、Kitchen、Logistics、Mechanisms、Relics、Handheld，人物发布到 Characters/Highlights。

诗人幕沿用选定原图 `sources/Internal/Props/Highlights/Backgrounds/shura_gallery.png` 的木墙、风雨拱洞、窗和铜管关系，场内收为横向悬廊；南侧栏杆与北侧墙脚实际限定通道，门洞保留通行缺口。拱洞后的天气纹理从该原图原区域提取，发布记录在 Architecture/gallery_weather.publication.json。厨房按灶台、工作台、后墙窗构成操作三角。抢运接收台具有连续台面、南侧台缘与楼梯入口，人物从实际楼梯缺口上行。

approach 与 transfer cue 等待 `route_finished` 后才启用下一句；在途输入不重复调度。搬运依次等待领取、实际上行、交接和让出路线；物件保持原尺寸，粮袋复用单袋sack，苗床复用独立托盘。受阻时停止本段后续旁白并保留当前物件。切换/重置仅清理舞台。棋局近看按钮改变相机倍率，仍消费原棋盘。

围棋盘由实际十九条横线和十九条纵线绘制，只演示四个依次合法、无提子的开局落点；不计算整盘胜负。厨房角色在场动作与蒸汽独立运行，恶魔仅在窗外以单个半透明实体显示。抢运每件物件仅领取和交接一次，碰撞受阻保留现状，接收台保留实际交接物件；低沿的源质是舞台示意，没有米制绝对液面。镜头不包含全岛模拟。

舞台不创建 GameEngine/DvSession，不触发 Core Prepare/Complete，不保存正式世界、知识或通关结果。所见内容属于条件节选；正式会话与游戏验收另有证据边界。该 README 说明实施合同，实际显示和交互结论由本轮人工画面观察记录。
