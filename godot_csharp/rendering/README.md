# 通用着色器库

独立 RPG 渲染组件位于 [rpg/](rpg/README.md)。环境、相机与校准地图分别维护。植被使用 VisualShader 节点图，建筑地形适配及几何装饰位于 `geometry/`。该入口尚未接入正式剧情场景。

常规三维表面以程序化 `gdshader` 为主要路线，木、布、石、金属和涂饰共用包含物体及父节点缩放的米制材质坐标，并对微噪声进行屏幕导数衰减；但是坐标仍附着于物体、保持局部纹理方向，不会因为人物移动而按世界位置滑动。该补偿以常规旋转与缩放为前提；但是带剪切的变换仍须另行核对。

跨区域精细度评估见 [Astra 审查](../../docs/跨区域精细度评估.md)，制作标准同时约束形体、材质频率、轮廓、接触和光照；但是程序化表面本身不代表旧贴片、建筑实心盒、地毯拉伸或人物家具比例已修复。

新增 `AtmosphereRig.tscn` 提供稀疏空气微粒，室内默认 24 粒、室外 48 粒，三维分镜中可关闭；但是粒子仅承担视觉氛围，不表示世界天气、毒雾或可见性事实。环境资源按场景实例复制，室内外分别调整接触阴影与日光阴影距离；但是当前没有 GPU 性能测量，也未启用体积雾来掩盖场景细节。

本库集中维护空间着色器与公共噪声包含文件，服务于 E 三维美术库与室外精细化；但是游戏规则、人物认知和世界内容继续由 DV 与 C# 正式宿主负责。

|目录|着色器|用途|
|---|---|---|
|surfaces|painted_surface、wood、fabric、aged_metal、stone|颜料、木材、织物、金属、石材|
|surfaces|parquet、tile_floor、roof_tiles|拼木地板、地砖、屋瓦|
|surfaces|water、foliage|水面与轻风植被|
|surfaces|community_grass、outdoor_ground、outdoor_paving|真实草叶风动、土草过渡与米制铺石|
|surfaces|exterior_plaster、exterior_roof_tiles|室外抹灰与米制屋瓦|
|effects|fire、explored_fog、memory_dim|火焰、探索遮罩、记忆显示|
|post|contour|深度细描边|
|common|surface_noise.gdshaderinc|共享噪声与导数过滤函数|
|common|surface_metrics.gdshaderinc|米制表面层次、屏幕覆盖过滤与观察尺度适配|

`MaterialLibrary.surface(pigment, kind, roughness, metallic)` 提供常用材质，kind 为 0 颜料、1 木材、2 石材、3 织物，metallic 大于 0.4 时选用金属材质；但是参数表示视觉表面，不承担世界规则分支。

木材与织物使用模型坐标和导数过滤，`grain_density`、`weave_density` 控制细节频率，`pigment` 与 `surface_roughness` 控制底色和反射；但是远距离效果仍受屏幕分辨率与模型 UV、尺度影响，应以最终镜头检查。

植被材质仅应用于叶片表面，树干使用木材材质，默认风幅度为 0.012 米；但是该视觉形变没有改变碰撞。导入模型保持等比缩放，茶桌使用原始米制尺寸。

`ContourEffect.gd` 将深度轮廓叠加于相机，默认宽度 1 像素、强度 0.38，界面支持关闭；但是透明表面、共面接缝及面部细线不属于当前检测范围。室外新材质已通过 Godot 4.6.1 Compatibility 的真实单场景显示检查；但是没有 GPU 性能测量，也不据此推定其他渲染器的效果。

材质接入通过 `HallCraft.gd` 和 `ImportedAssets.gd` 复用，屋瓦已从内联代码提取；但是有独立用途且仍在使用的界面着色器继续保留在原目录。

`community_grass.gdshader` 改编自 Malido 的 [Stylized Multimesh Grass Shader](https://godotshaders.com/shader/stylized-multimesh-grass-shader/)，原作者页面将代码标为 CC0，来源、校验与使用位置见 `community_sources.json`；但是页面的示例图片和模型没有导入，项目草叶是自行构造的立体渐尖网格。

草叶根部按地形高程放置，风只作用于上部，并通过实例颜色和空间风场打散重复；但是草叶属于装饰，不生成实体碰撞，也不代表 Core 中的天气或人物交互事实。

`PersistentMultiMesh.gd` 把实例变换与颜色保存为场景属性，并在真实渲染器中恢复 MultiMesh，支持不保留 GPU 缓冲的无界面作者导出；但是它只恢复几何数据，不加载游戏状态或制造动作回执。

2026-10-27 的统一标准由 Astra 结合公共厅与室外入口的同尺度图提出，表面按 65 厘米宽变化、6 厘米细节和 1.2 厘米微纹分层，木板、砖块与独特纹样保留各自结构尺寸；但是这些数字是程序化外观参数，不是新的世界法则或材质实测报告。

`surface_metrics.gdshaderinc` 按像素覆盖范围过滤周期细节，低于约 2 像素时归向平均值、2～4 像素过渡，低对比细节随观察尺度可放大至约 2 倍，兼容正交缩放和透视距离；但是它不放大门窗、板块或物体的物理尺寸，也不取代独特图案设计。

`RenderDetailController.gd` 按曲面径向投影直径选择原始／中／低三档细分，共享材质与网格变体，并使用迟滞抑制阈值来回切换，碰撞仍取作者几何；但是本轮只控制静态 SphereMesh、CylinderMesh、CapsuleMesh 和 TorusMesh，蒙皮人物、动画对象与导入 ArrayMesh 不属于该控制器的验收范围。

美术库提供 6／12／24 米横向跨度检查，并以屏幕投影校准宽度，导出地图也接入相同曲面分级控制器，导出前恢复作者最高细分；但是屏幕上的分档数量只表示已登记静态曲面的当前状态，不是可见物体数或性能提升数据。
