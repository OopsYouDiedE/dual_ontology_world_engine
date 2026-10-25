# E 场景维护

继承 Godot 与根 AGENTS。E 以 D 的真实三维结构为主体，同时参考 A 的布局、家具造型与手绘细节，不把融合缩减成调色。禁止用 SVG 绘制纹理，也不将 SVG 转为 PNG 规避；装饰纹理使用有来源的位图原稿，程序 Shader 可表现木纹、水面与光照。工程细节见 README.md。

房间由 GridMap 与 MeshLibrary 装配。每室拥有完整墙体边界，相邻室墙面使用相同坐标；墙面只朝室内，不绘外侧。区域作者暂不制作门；后续门模型仅显示观察者当前房间所属的一套。

用户已将单房间显示修订为同地图探索记忆：当前房间正常显示，已探索的非当前房间压暗并覆地面半透明雾，未探索房间隐藏。离开的房间仅保留最后所见的静态家具，不显示人物和实时变化。RoomKnowledgeView只消费宿主交付的本人认知与静态快照，注册几何不授予探索；不得读取隐藏房间实时状态冒充记忆。当前演示为独立美术夹具，尚未接入正式宿主观察流。

16名学员共用HallCraft身体尺寸，CastE只负责发型、服饰与配件。区域作者在areas独立模块制作，不修改身体或剧情。LibraryE按区域路由并按需装配，录制布景静态资源与完整动作接线分开报告。

WorldEnvironment、Camera3D 和日光是 scene.tscn 内显式节点，环境与相机属性保存在 tres。E 用 Forward+ 采集，记录实际渲染方法、SSAO/SSIL/Glow、相机和房间可见性。根节点保留 configure_lighting(normal/off/shift)。固定 runtime/E 输出在旧采集退出后整体替换，主代理逐图对照 A 与 D，报告位于 artifacts/style-e/index.html。不得以开关启用、格数或几何数量代替画面判断。

乙区参考图重设计由areas/YiRegion、YiArchitecture、YiFurniture负责。米制美术布局与正式导航几何分开，未经宿主接线不得宣称游戏地图已替换。每室独立四面墙仅去除外侧背面，保留截面；总览为作者视图，不能作为玩家探索认知来源。--zone-yi采集只写artifacts/style-e/zone-yi-redesign/render，报告注明目标图、推定尺寸、整体与单室取景及未完成范围。当前乙区不使用此前UV图集草稿。
