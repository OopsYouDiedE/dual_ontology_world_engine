# RPG 渲染组件

本组件使用 Godot 4.6 Forward+。项目由 Godot 选择可用的 RenderingDevice 驱动。启动器不传入渲染方法或驱动覆盖参数。组件在不支持的后端报错并停止照明。组件不修改全局项目设置、宿主 viewport 画质或已有世界环境。

画面目标是高度风格化的像素化 3D RPG。低分辨率呈现保留真实三维几何、透视、光照、阴影与反射。风格化材质负责控制色彩和明暗层次。

## 职责

| 文件 | 职责 |
| --- | --- |
| `RpgRenderer.tscn` / `.gd` | 独占一个 World3D 的环境与太阳。检查现有环境冲突。支持运行时启停。 |
| `RpgRenderProfile.gd` | 可编辑的太阳、天空、曝光、雾密度、阴影距离和 GI 网格参数。 |
| `Daylight.tres` / `BlueHour.tres` | 两种校准用光照配置。实际天气由宿主提供。 |
| `RpgPixelView.tscn` / `.gd` | 独立低分辨率三维视口，整数倍最近邻放大，HUD 保持原生分辨率。 |
| `StylizedSurface.gdshader` | 风格化三维表面材质。 |
| `RpgCameraRig.tscn` / `.gd` | 透视相机、指数跟随、环绕、缩放和球形 SpringArm 碰撞。 |
| `FoliageWind.tres` | 可编辑 VisualShader 节点图。连接时间、位置、UV、风强和叶色，计算叶片弯曲、粗糙度和背光。 |

地图、渲染器与相机作为同一 World3D 下的独立节点装配。地图可以保有局部灯和 ReflectionProbe。渲染器不读取地图脚本，不生成地图，不提交游戏状态。人物网格、骨骼与动作播放器属于独立角色组件；本轮没有替换或接入正式人物动作。

高画质启用 SDFGI、SSAO、SSIL、SSR、体积雾和受阈值控制的辉光。均衡档保留 SDFGI 与 SSAO，关闭 SSIL、SSR 和体积雾。固定曝光与 Filmic 色调映射用于校准。景深和自动曝光默认关闭。屏幕空间反射的画外部分依赖地图探针与天空反射。

静态地图网格需使用 `GI_MODE_STATIC`。移动人物不得登记成静态 GI 几何。SDFGI 不适合细薄墙体，建筑需要厚度和封闭体积。动态发光物体的照明需要真实灯光。更换静态布局或光照后应更新地图的反射探针。

## 独立入口

在 Godot .NET 中运行 `res://scenes/Rendering/RendererShowcase.tscn`，或通过正式 `dual-ontology-input.on` 传入同一路径。场景由真实宿主渲染，使用正式 `step(click)` 与 `observe`。退出时调用 `close`。

示例使用自己的 SubViewport 与 World3D。三维源图默认 320×248，使用 MSAA 4×，不使用 TAA。画面按整数倍最近邻放大，HUD 使用原生分辨率。示例的阴影 atlas 与去色带参数保存在独立视口，不覆盖宿主 viewport。UI 提供光照、画质、相机环绕、缩放、复位与渲染灯光开关。FPS 是即时读数，不代表基准测试结果。

`CalibrationCourt.tscn` 是原创的非剧情校准地图，包含粗糙度样球、水池、木座椅、局部灯、真实拱廊、坡地及植物。地图没有 WorldEnvironment、太阳或相机。没有导入外部模型或图像生成素材。

## 程序化几何与节点着色器

`geometry/TerrainArcade.gd` 是可复用的 CPU 几何节点。它从每根柱脚四角向实际物理地面发射采样射线，通过独立地形层读取高程。它生成不同高度的实体基础，上部柱列、拱券和屋面保持水平。拱券、外圈线脚和重复檐口齿饰由网格生成，有实际厚度和碰撞。缺失地面或地形高差超过限制时保留上一版有效输出并报告原因。当前适用于米制、竖直、无非均匀缩放的线性拱廊；任意建筑拓扑、切坡和地下洞穴未实现。

节点参数包括跨数、跨距、起拱高度、拱厚、分段数、地形碰撞层及基础高差上限。运行时等待地形进入物理服务器后调用 `rebuild()`。编辑器可使用 `generate_in_editor` 重建预览。生成结果为派生缓存，参数与输入地形为维护来源。

`geometry/FoliageCluster.gd` 按固定种子生成真实叶片网格，UV.y 表示叶根到叶尖。`FoliageWind.tres` 的 GPU 节点图计算弯曲，叶根保持固定。ShaderMaterial 由一个簇内的叶片共用。该组件不生成碰撞，不以风动修改世界天气。建筑结构及浮雕不由 shader 位移代替。

## 接口

- 渲染器：`apply_profile(resource)`、`set_quality(0/1)`、`enabled`。
- 相机：设置 `target`、`target_offset`、`collision_mask`；调用 `orbit(yaw_delta, pitch_delta)`、`zoom(distance_delta)`。角度单位为度，距离单位为米。动态更改目标碰撞子树后调用 `refresh_exclusions()`。
- 地形：采样层默认第 2 层；生成建筑碰撞使用第 1 层；示例相机检测两层。
- 叶片：实例化 `FoliageCluster.gd`，添加前配置 `leaf_count`、`radius`、`random_seed`、`wind_strength`、`leaf_color`。运行时改参数后调用 `rebuild()`。

## 依据与范围

技术依据为 [Godot 4.6 Environment](https://docs.godotengine.org/en/4.6/classes/class_environment.html)、[环境与后处理](https://docs.godotengine.org/en/stable/tutorials/3d/environment_and_post_processing.html)及 [VisualShader](https://docs.godotengine.org/en/4.6/classes/class_visualshader.html)。这些 API 提供渲染功能，成品美术质量仍需模型、灯光制作及实际场景验收。

目标是建立风格化像素 3D RPG 的可复用渲染基础。本轮不宣称达到某款商业作品的最终美术质量。正式剧情场景接入、完整角色动画、长距离流送、多硬件性能基准及全章节通关不在本轮验收范围。
