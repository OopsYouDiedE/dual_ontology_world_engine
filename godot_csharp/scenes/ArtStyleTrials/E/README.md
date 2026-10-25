# E：三维区域美术库与公共大厅

2026-10-26。当前扩展入口为 `library.tscn`：64个室内空间、16人统一身体、六幕静态录制布景与第二章六区室外。按区域并行模块制作，详细接口和未接入正式宿主的边界见 [工程说明](../../../../docs/style-e-library.md)。同地图探索记忆规则已替代单房间唯一可见规则，`knowledge-demo`展示当前/记忆/未知三态。以下 `scene.tscn` 内容保留为原公共大厅样片的说明。

依据公共厅修订：D 为三维主体，A 的布局、手绘纹样和造型精度也必须参考；公共大厅应体现学院公共生活空间；采用3D TileMap式装配、逐房显示、共墙重合、提高墙体。随后补充 WorldEnvironment/相机参数，并明确禁止 SVG 纹理。

入口 `res://scenes/ArtStyleTrials/E/scene.tscn`。Godot 4.6.1 Forward+。直接运行场景时顶部按钮切换大厅、甲乙丙丁翼寝室样片、盥洗室。大厅取景不再同时包含盥洗室。四翼各提供一个独立寝室美术样片，不是完整四翼或16间正式寝室的迁移；房间选择不执行Core行动、碰撞移动或剧情。

## 空间和工艺

- `RoomGrid.gd` 建立1单位GridMap与多表面MeshLibrary：地板、完整3.8单位高护墙、面向镜头的南墙切口。墙根、凹框、压条与檐线是网格；地面人字拼木相位使用世界坐标，跨格连续。南墙仅为取景切口，不能据此认定物理墙高降低。
- 公共厅10×14单位，四翼样片各6×8；西墙与甲/丙翼东墙共用x=-5，东墙与丁/乙翼西墙共用x=5，厅南墙与盥洗北墙共用z=7。非当前房间不绘制也不点亮，避免重合墙的深度竞争和双倍墙厚。该美术排布不改正式几何合同。
- 参考A的阅读/会面/音乐棋桌分区：软包扶手椅、簇绒扣、茶几杯碟、炉台时钟与栏栅、书脊烫金、车木桌腿、长桌烛台和花瓶、钢琴琴键与谱架、窗帘褶皱及流苏。五人保留D的相似三头身；仅静姿。
- 新地毯与墙纸为内置imagegen生成的原生位图，不是SVG转换。源稿与显式全图裁切清单位于 `mcp/asset_pipeline/sources/Internal/ArtStyleE`；approved-cuts按scale=1发布到ShareAsset。运行生成mipmap并用各向异性过滤，保留原稿精度，不从底色推算法线。地毯按原图纵横比摆放，桌旗只取中央窄条UV，避免把完整纹样拉扁。
- 地板选用人字拼木及周边镶条，结合架空1900年代和A目标。1900年拼木地板专利提供当时采用拼木技术的直接历史依据，不把专利当作普及率统计；木种和配色依据项目美术案。盥洗区为浅色地砖配深绿角点。

## 世界环境与相机

`SalonEnvironment.tres`：固定色环境光0.55，Filmic，SSAO半径0.55/强度1.25/幂1.25，SSIL半径2/强度0.30，Glow强度0.30/阈值1.8/无全屏Bloom。SSIL是屏幕空间补光，未声称完整GI；当前没有LightmapGI/SDFGI。阴影使用日光0度角宽、bias 0.08/normal bias 0.8和局部灯0.12尺寸；日光软阴影在小模型上出现散点，当前采用稳定方向阴影并保留实际投影；采集工程使用4096阴影贴图与软阴影过滤。无需通过浓雾或景深掩盖细节。

`SalonCamera.tres`：固定曝光1.0、自动曝光关闭、近远景深均关闭。Camera3D正交投影，厅size=18.2、其他房size=12.8，near=0.1/far=70；相机在各室中心上方(0,22,25)，看向该室(0,0.8,-0.65)。视口4×MSAA。细节图使用8.4正交范围，不能当作同取景对比。

## 有限验证和边界

`run_style_routes.py --routes E --godot <程序>` 在独立纯GDScript工程运行Forward+；采集正常、动态间隔、关局部灯、换日光、关闭SSAO/SSIL/Glow对照、六房切换及阅读/会面细节。DVSTYLE1记录实际房间可见性、灯光房间身份、共墙坐标、环境开关与相机变换。源指纹和原始日志同目录。每轮固定 `artifacts/style-route-comparison/runtime/E` 整体替换。

当前画面观察与运行结论以 [报告](../../../../artifacts/style-e/index.html) 为准。尚缺A原图的完整雕花雕塑、细致人物表演和全部家具曲面；扩展库已按现有陈设配方覆盖四翼全部空间，并以材质和用途区分家具。尚未接入正式会话、移动碰撞和门禁，不能将美术切换当作游戏过门验收。墙纸重复仍需随最终房间逐面审查。

资料：[Godot GridMap](https://docs.godotengine.org/en/4.6/tutorials/3d/using_gridmaps.html)、[Environment](https://docs.godotengine.org/en/4.6/classes/class_environment.html)、[相机属性](https://docs.godotengine.org/en/4.6/classes/class_cameraattributespractical.html)、[1900拼木地板专利](https://patents.google.com/patent/US657960A/en)。
