# 高光舞台

## Astra 素材接入（2026-10-25）

餐厅和护碑两幕采用 Astra 独立生成并经用户认可的素材板。`modern-stage` 路由调用 `mcp/asset_pipeline/publish_astra_stage.py`：只裁切、透明分离、镜像和尺寸对齐，不重新绘制或改色。源板与记录归档于 `sources/Internal/ModernStage/Astra/`，每个矩形或轮廓裁切及头/身体分带缩放写入 `Props/ModernStage/publication.json`。其他人物、灶台与母碑沿用已有素材，不宣称全部角色美术已更新。

地面为128px宽的 TileMap 模块，纵向等比保留源纹理比例，物理装配仍为32px尺度。A/B/C按钮在同一镜头、身体尺寸和路线下比较旧32px重复、Astra原稿直接重复、Astra原稿整块镜像接缝。墙面直接采样源板正面，与矩形桌面共同保持RPG平行斜俯视，仅露顶面和前侧。没有重新绘制地毯、灯光或装饰。

默认采用Astra在实机比较后选择的B（原稿直接重复），C作为可见的备选保留；镜像可能使石纹形成过大的连片，不能只凭接缝连续宣称更优。护碑幕把同源石材切片按128px宽、24px高分层装配前墙，并用源板边缘裁切条标明墙顶和墙脚，避免直接把整块地板放大成墙。

餐厅的“绕桌落座”按钮或R键执行完整桌周路径，按真实碰撞到达座位后切换已有PixelCast背向坐姿。桌椅使用独立碰撞和脚点排序，受阻停止。护碑的 `cane_draw/cane_raise/cane_strike/cane_stop` 来自内容合同，依次抽杖、抬杖、敲地、左手伸出；右手持续持杖。两幅可用短身稿与两幅旧姿态按共同解剖头高进行分带尺寸对齐，未使用双杖错误帧。它们是四个关键姿态加时序，不是新增完整逐帧手臂动画，也没有新方向步态。

这两幕使用1024×768渲染视口及2倍相机，保持原512×384世界取景；几何和素材方案比较不以镜头变化代替改进。有限采集：`python mcp/verification/run_modern_stage.py --godot <Godot程序> --out artifacts/tilemap-modernization/runtime`。原始输入、身体采样与结果写DVTILE01校验二进制，实际PNG来自游戏SubViewport。该工具不启动Core/AI，也不代表正式章节验收。

采集使用临时纯GDScript工程，仅复用生产`.godot/imported`的资源缓存；源哈希失效仍交给Godot导入，不复用会话、存档或编辑器状态。`modern-stage-report`只生成固定目录的HTML，核对本轮PNG时戳、源码指纹和二进制完整性，不启动游戏。

以下32×48、三方向静姿与旧地形说明继续适用于未更新的四幕；不覆盖上述两幕的Astra接入。

独立纯 GDScript 场景以六个局部舞台展示条件节选。入口：

```powershell
& $env:GODOT --path godot_csharp res://scenes/Highlights/Highlights.tscn -- --highlight=chess_god
```

`--highlight=` 可取 `shura_poet`、`scholar_relic`、`chess_god`、`demon_miracle`、`kitchen_dumplings`、`last_transfer`，省略时从第一段开始。顶部六个按钮及数字键切换舞台，空格或按钮前进对白，WASD/方向键控制现场侦探。切换/重置只重新装配该舞台。

舞台读取 `Contracts/highlight_stages.json` 的 `title`、`condition`、`location`、`lines`。每行包含 `speaker`、`text`，可选 `actor` 和 `cue`；cue 支持 approach、gesture、black_move、white_move、miracle、dumpling、steam、transfer、tide。所有人物对白、场景条件由主代理维护的内容合同提供。

逻辑画布为 512×384，地图由 16×12 个 32 px 格组成，人物脚点和物件脚点共用 Y 排序。画布与镜头整体近看，人物保持32×48与32格的原比例。角色使用 `CharacterBody2D`，房间、物件和身体通过真实局部碰撞阻挡移动。新四人 atlas 每姿态 32×48，既有侦探、厨师、苏泠、雀儿复用 PixelCast，雀儿资源ID为thief。新稿只有三个方向静姿，只注册idle；实际碰撞后的位移决定朝向。完整SpriteFrames已有walk时才播放步态，没有帧的角色移动时保持静姿，不用整身起伏或旋转代替动作。

诗人地面直接取自 `shura_gallery.png` 的菱形地纹，青灰/赭红双线边饰与角饰独立装配。`AuthoredSurface.gd` 以7/24固定比例采样完整裁切图，地砖不进入47角边遮罩；32px格仅确定覆盖范围，相位跨格连续。诗人幕使用2048×1024渲染视口及4倍相机，保留原512×256世界取景；120px包边格对应128输出像素，底面源像素对应约1.17输出像素。不能把逻辑缩放直接等同于最终像素损失，须同时报告相机与视口。源坐标、实际原图尺寸、SHA-256、缩放和非无缝限制记录在 `Props/AuthoredRooms/publication.json`。现有三层为底面、包边与中心纹样，缺少作者法线/AO/发光遮罩，不宣称完整材质分层。

`GalleryFloorOrnament.gd` 将原图中心大菱形单独放在(256,237)，只出现一次；地面相位与源图的(767,623)纹样中心对应。包边使用 `AuthoredTerrainBorder.gd` 和 `Props/AstraTerrain/GalleryBorder.tres`，覆盖(48,204)–(464,268)的13×2格可见地面。32px逻辑格对应120px原稿组合格；底面继续按7/24连续采样。包边源像素只裁切、旋转和拼接，复用适配器的47种唯一角边邻接，空邻居参与匹配。地面本体不挖角，中心纹样不重复，人物和碰撞保持原场景定义。源图烘焙光照和线宽不均仍存在；有限渲染证据见 `../../../artifacts/astra-autoterrain/`。

其余五幕和水岸仍用旧 `HighlightsTerrain.tres`：floor=0（47角边）、wall=1（16四边）、water=2（47岸缘）；material indices 以 `Terrain/asset-manifest.json` 为准。它们尚未有对应原稿的一致性结论。图像原稿保存在 `mcp/asset_pipeline/sources/Internal/`，道具分别发布到 Props/Highlights/Architecture、Furniture、Kitchen、Logistics、Mechanisms、Relics、Handheld，人物发布到 Characters/Highlights。

诗人幕沿用选定原图 `sources/Internal/Props/Highlights/Backgrounds/shura_gallery.png` 的木墙、风雨拱洞、窗和铜管关系，场内收为横向悬廊；南侧栏杆与北侧墙脚实际限定通道，门洞保留通行缺口。拱洞后的天气纹理从该原图原区域提取，发布记录在 Architecture/gallery_weather.publication.json。厨房按灶台、工作台、后墙窗构成操作三角。抢运接收台具有连续台面、南侧台缘与楼梯入口，人物从实际楼梯缺口上行。

approach 与 transfer cue 等待 `route_finished` 后才启用下一句；在途输入不重复调度。搬运依次等待领取、实际上行、交接和让出路线；物件保持原尺寸，粮袋复用单袋sack，苗床复用独立托盘。受阻时停止本段后续旁白并保留当前物件。切换/重置仅清理舞台。棋局近看按钮改变相机倍率，仍消费原棋盘。

围棋盘由实际十九条横线和十九条纵线绘制，只演示四个依次合法、无提子的开局落点；不计算整盘胜负。厨房角色在场动作与蒸汽独立运行，恶魔仅在窗外以单个半透明实体显示。抢运每件物件仅领取和交接一次，碰撞受阻保留现状，接收台保留实际交接物件；低沿的源质是舞台示意，没有米制绝对液面。镜头不包含全岛模拟。

舞台不创建 GameEngine/DvSession，不触发 Core Prepare/Complete，不保存正式世界、知识或通关结果。所见内容属于条件节选；正式会话与游戏验收另有证据边界。该 README 说明实施合同，实际显示和交互结论由本轮人工画面观察记录。

舞台保留键在 `_input` 消费按下与抬起，空格推进不受当前按钮焦点影响。SceneTree 的 `highlight_launch_consumed` 标记使启动参数仅被消费一次；返回标题不再次启动节选。

诗人幕使用 512×256 视口，镜头中心为原32px世界的(256,208)，人物保持原尺寸。Architecture/gallery_facade.png 是原稿北立面(0,0,1536,320)区域的448×96发布图，包含成对门窗、中央破拱和铜管，来源与重采样记录见同名publication.json；地板、门口碰撞、南栏仍为独立运行对象。

高处台面由现有地形图集的木面与石基组合，StageRailing以脚点加入Y排序。两袋粮食与苗床有独立固定落点；搬运者先走到落点附近，再卸下原物件，接触阴影随放下恢复。源质岸缘共三次朝楼梯推进，使用water47和随层移动的透明雾，最后一次先等待高处站位；最终侦探停止局部走位，防止段落结束后走回危险面。该像素范围只表示局部演出，没有绝对米制液面结论。
