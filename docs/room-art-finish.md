# 室内画面完善

2026-10-25。RoomArtFinish是正式地图与全图采集共用的呈现组件，由CampusMapView._new_map_record与capture_maps调用attach。室外跳过，同一实例只接入一次。

- 局部光消费已有stage_lights；256px径向渐变纹理由代码生成。环境偏冷，灯源偏暖，窗光偏冷；静态接触影仅贴墙根与已有家具脚点。
- 正式RoomStage在组件存在时关闭重复PointLight，并按当前房间启停组件灯源；原窗束和粒子仍属Host。室内日时采用柔和混色，避免与旧全局压暗重复。
- 家具尺寸与对应碰撞由room_furnishing.py按类别统一发布：桌子1.4–1.5、床1.45、扶手椅1.6、普通椅子1.2；配套椅位按桌沿单列。RoomArtFinish不再运行时统一放大，避免二次缩放。受阻的交互站点在同语义房间内寻找有身体余量的接近点，再发布到唯一几何合同。
- 窗源裁切依据chapter2_art.rooms的window配方，保存到生成的Contracts/room_art_finish.json。连续生活翼沿build_living_floor.PARTS转换原点，起居厅544行以下的原点加192；源裁切仍保持原坐标。水平1.25、垂直1.15。
- 木质下墙采用64px组合护板和收边；绿底金边地毯按源floor_work位置单独组织双边框、细链纹、角饰与菱形花心，不统一放大地砖或重绘既有中央厅纹盘。临床和厨房瓷砖不套用木墙。
- 不放大32px素材伪装成原生64px细节。高分辨率手绘地面维持源稿精度；护板与纹样新增细节来自代码造型，食堂家具另按下述透明原稿发布。

重新生成窗裁切使用现役publish_room_terrain_skins.py入口；章节场景重新生成也刷新此合同。该合同仅为呈现元数据，不授予人物事实，不改变碰撞、门、站点或逻辑米制。

验收以artifacts/room-art-finish/index.html的目标、同取景前后图、差异及范围为准。全图采集不包含Host UI、日时、窗束及真实玩家镜头，不能替代完整实机验收。

## 家具与纹样配方

`mcp/asset_pipeline/room_furnishing.py` 是家具比例、桌椅组合和接近点调整的唯一工程配方。章节场景生成器在基础footprint解析后调用一次tune_room，Sprite.scale与CollisionShape2D使用同一比例；生成源保存原基础尺寸，重复生成不累乘。场景变化后重建living_floor并运行export_geometry同步站点/站位。不能只改运行Sprite或只扩大导航障碍。

纹样读取已有rug/runner来源范围：地毯边界约扩大15%，起居厅长毯加宽32世界像素，边框间距与纹章节距分别绘制。房间32px物理格与纹样64px组合单位分离；未用旧减色转换器，也未把原生32px素材插值当成新增绘画细节。

`godot_tools/capture-furnished-rooms` 打包正式地图的资源闭包到一次性纯GDScript工程，避免无关UI/字幕重新导入；仍使用原始场景、纹理与运行脚本。工具采集全图，再在七张室内地图上用16×12、上移6px的现役StoryActor脚部碰撞体，从首个出生点经真实test_move与move_and_collide逐段到达站点。该检查是独立几何夹具，不创建DV会话，不证明玩家认知、剧情通关或完整Host UI。路径与终点记录为带哈希的motion.dvlog。

## 参考驱动的食堂样片

比例调整不替代源稿重绘。ReferenceDining保存按04-logistics参考图生成的透明家具原稿、无烘焙光地面材质和显式cuts.json；approved-cuts发布保留源精度，运行线性采样。room_furnishing按家具名选择各自源图和世界宽度，前后凳位与碰撞同步。烟罩高位后景独立于炉体脚点；桌灯进入stage_lights。室内统一组件仅采样已发布地面材质，不从概念图硬抠家具、删背景或把整张概念图当地图。首次光斑地板试稿已弃用，最终素材不含照明明暗池。

Astra审查指出食堂桌宽已经接近参考，不应继续扩大。当前仅完成食堂家具画法、桌凳关系和地面纹理样片；墙窗、手泵、桶袋、连续厨台和周边收纳仍不一致。最新图见artifacts/reference-dining-repair/index.html。

## 餐桌正交重绘

按用户要求，餐桌、长凳、备餐台及板盘采用平行矩形桌面、正面与竖直腿，禁止透视收缩和左右侧面。ReferenceDining/orthographic-guides保存几何约束，cuts.json保存明确源哈希、裁切与真实落地脚点；两张餐桌共用同一重绘设计。不可把旧概念图中的轻微透视作为本轮目标。当前渲染、同取景前后图、验证范围及残余差异见artifacts/orthographic-furniture-redraw/index.html。厨房其他柜炉未在此轮重绘。
