# 卷首书桌 3D 资产来源与重建

本目录保存《安眠者有福》标题书桌的可编辑几何源。几何由 `godot_csharp/tools/build_painterly_desk.py` 在 Blender 4.2 中程序生成；木纹、布面、纸面与金属底色图由 AI 图像生成工具生成，不能表述为人工手绘原画。Godot 在运行时负责相机、纸页、火芯、灯光与阴影，`desk.glb` 只包含桌面及道具几何。

## 文件关系

| 文件 | 用途 |
| --- | --- |
| `godot_csharp/tools/build_painterly_desk.py` | 几何生成器，依据自身位置定位仓库；固定随机种子 `73417`。 |
| `godot_csharp/tools/build_desk_surface_maps.py` | 独立微表面数据烘焙器，使用 Blender 原生节点与 CPU Cycles，不修改底色图或道具几何。 |
| `desk_physical_spec.json` | 实物尺寸、来源、统一毫米尺度和道具布局的唯一输入。 |
| `godot_csharp/ShareAsset/Internal/Props/UIDesk/Painterly3D/desk-physical-layout.json` | 从实际网格导出的尺寸、包围范围与灯芯顶点；Godot 火焰和光源读取此配置。 |
| `godot_csharp/AssetsSource/Internal/Props/UIDesk/Painterly3D/painterly_desk.blend` | Blender 可编辑几何源；可直接查看桌板、油灯、放大镜、墨水瓶、钢笔及书签。 |
| `godot_csharp/AssetsSource/Internal/Props/UIDesk/Painterly3D/desk_surface_maps.blend` | 数据烘焙器生成的可复建节点源，包含四种材质图、UV 样片、打包的参考底色和生成脚本。 |
| `godot_csharp/ShareAsset/Internal/Props/UIDesk/Painterly3D/desk.glb` | Godot 使用的几何导出文件；不含摄像机或灯光。 |
| `godot_csharp/scenes/UI/DeskStage3D.gd` | 场景装配、透视相机、视口适配与统一风场驱动的灯光。 |
| `godot_csharp/scenes/UI/painterly_surface.gdshader` | 材质底色与笔触细节的运行时着色。 |
| `godot_csharp/scenes/UI/desk_glass.gdshader` | 透明玻璃、Fresnel 轮廓响应及受光反射。 |
| `godot_csharp/scenes/UI/magnifier_lens.gdshader` | 仅用于 `LensGlass`：深度约束的屏幕空间放大、折射及 Fresnel 响应。 |
| `godot_csharp/scenes/UI/desk_flame.gdshader` | 油灯火焰形体、暖色发光及风向偏转。 |
| `godot_csharp/scenes/UI/DeskAtmosphere3D.gd` | 标题书桌空间中的少量 GPU 微尘；支持减少动态时隐藏和暂停。 |

## 重建几何

在仓库根目录使用 Blender 4.2 的干净初始配置运行，避免个人启动场景与界面语言影响生成器：

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.2\blender.exe' --factory-startup --background --python 'godot_csharp/tools/build_painterly_desk.py'
```

该命令重新生成上述 `.blend` 和 `.glb`。以脚本为持续维护的几何来源；直接编辑 `.blend` 后再运行生成器会覆盖该文件中的手工修改。生成器使用 Godot 坐标约定书写形体，再转换到 Blender 坐标并按 glTF 的 Y 向上约定导出。书页与原生 UI 由标题代码单独装配，不在此模型中。

## 当前道具几何

- 油灯采用层叠黄铜灯脚、细同心刻圈、梨形透明储油体和双层玻璃烟囱；燃烧器有实际贯通孔，调节轮有细齿。
- 放大镜采用双层金属镜框、凸面玻璃、分段金属套环和成形木柄。运行时仅 `LensGlass` 使用 `magnifier_lens.gdshader`，结合实际镜片位置与凸面法线实现深度约束的屏幕空间放大和折射。
- 墨水瓶采用倒角钴蓝玻璃外壳、深色墨水内芯、瓶口螺纹和瓶盖滚花，不附大面积白色标签。
- 钢笔采用渐变木制笔杆、金属套环、细笔夹与薄弧面笔尖；笔尖的中央开缝和通气孔由几何形成。

生成器保留对象前缀与材质标签，供标题场景覆盖运行时材质：`LampGlass`、`LensGlass`、`InkGlass` 对应三种玻璃，`PolishedWood` 对应木笔杆和镜柄；`Pewter`、`PewterEdge`、`PewterDark` 是兼容旧接口的金属标签，当前标题中表现为做旧黄铜。生成器在导出前检查三角形数量，达到 100,000 即中止。

## 物理尺寸标定

书封长 6.96 场景单位，对应本场景设定的 220 mm，因此一单位为 `220 / 6.96 = 31.6091954 mm`，封面宽 4.96 单位约为 156.78 mm。这是明确的美术尺度基准，不是从参考图推断出的实物测量。相机在约 60°俯视角下保持近景，俯角与取景距离独立维护。物件尺寸增大时通过摆放与自然裁切组织画面；不能缩小物体，或拉远相机把所有道具塞进画面。

当前相机俯角固定为 60°、注视距离 12 单位、注视平面垂直视野 7.2 单位；开书只跟随阅读页平移，镜头俯角、距离和视野保持一致。采用 `KEEP_HEIGHT`，窗口宽度改变侧边可见范围；鼠标视差只施加克制的方向变化。翻动书皮可沿实体轨迹出框，相机不追着书皮后退。

| 道具 | 采用的尺寸基准 | 规格来源与边界 |
| --- | --- | --- |
| 油灯 | 足径 114.3 mm，总高 355.6 mm | [Vermont Mansfield 14 英寸桌灯](https://www.vermontlanterns.com/content/oil-lantern-brass-table-14)的 4.5 × 4.5 × 14 英寸量级；调整灯体纵向比例，保持调节轮为圆形。 |
| 放大镜 | 实际镜片直径 80 mm | [Eschenbach 官方目录](https://www.eschenbach-optik.co.jp/main/wp-content/uploads/2022/12/8ffa81b928e8080d2015bc76cb7b33a2.pdf)列出的 80 mm 手持镜片；现有黄铜框与木柄保留自身形状，整体长度从网格测得。 |
| 墨水瓶 | 瓶身宽 53 mm、深 44 mm、总高 70 mm | [Sailor 50 ml 墨水瓶](https://sailor.co.jp/product/13-1007/)的外形尺寸量级，分别校正三个轴。 |
| 钢笔 | 首尾长 141 mm，笔杆主径 13.1 mm | [Pelikan 官方尺寸表](https://www.pelikan-passion.com/images/international/assets/magazine/passion-magazin-01-2018_en.pdf?download=)及同级 M800 的约 14.1–14.2 cm 长度；模型独立校正轴向和横截面，不宣称复制品牌型号或开盖尺寸。 |

生成器以实际网格顶点测量，标定后将各道具最低点放在桌面上方 0.005 单位。它导出的 `lamp_wick_tip_world` 是已经完成所有变换的灯芯顶端；火焰高度设定为 28 mm、半径 3.5 mm，火焰下端随生长保持在灯芯处。反射体积、布置净空和开书镜头同步调整。上述实物资料仅用于确定可信大小，未复制其外观、商标或光学性能。

## 当前材质图来源

当前标题使用 **`reference-material-atlas.webp`**（1254 × 1254），以及从对应象限裁出的 **`reference-cloth.webp`**、**`reference-paper.webp`**（各 627 × 627）。这些底色图由 imagegen 参考用户上传的书桌图片生成；Pillow 仅用于象限裁切及 **WebP quality 85** 转码，没有人工重绘或人工手绘原画的来源声明。文件位于 `godot_csharp/ShareAsset/Internal/Props/UIDesk/Painterly3D/`。

| atlas 象限 | 内容与色调 |
| --- | --- |
| 左上 | 深暖胡桃木，细木纹及克制的磨损。 |
| 右上 | 磨损的橄榄墨绿书封布面，配细金边。 |
| 左下 | 暖象牙色旧纸，保留纸纤维和自然旧化。 |
| 右下 | 旧黄铜，细划痕、氧化斑和低饱和明暗变化。 |

本次生成源 PNG 为 `generated_images/exec-95d3fa32-3eb0-4349-9fe2-448869010f3c.png`，尺寸 1254 × 1254；该路径相对本次生成会话的工作目录。它是底色图，不应解释为法线、粗糙度或烘焙照明数据。重新执行几何生成器不会重新生成这些图像。

以下 SHA256 来自生成源和派生文件；派生文件与本次传输清单 `reference_finish_assets/upload.json` 一致：

| 文件 | SHA256 |
| --- | --- |
| `exec-95d3fa32-3eb0-4349-9fe2-448869010f3c.png` | `9f75f7618959a89047f4f5e85d37deb9f8e7030a1aa26aa31b813902e69819eb` |
| `reference-material-atlas.webp` | `599707811ba7f1ad4875f50b21c8c37664b778900dd14456222453d0586a1e63` |
| `reference-cloth.webp` | `200d1ac000f34ca3c404253206eaaee3e63d401ed04a7697254e1bc9bec0d27d` |
| `reference-paper.webp` | `2f8c15508f7600b26510a60b3149389cad24cc8cf7894383759def3e2012b392` |

生成提示词摘要（非逐字记录）：依据用户的暖色书桌参考图，为标题画面制作四格平面底色材质；四格分别表现深暖胡桃木、带细金边的磨损橄榄墨绿布面、暖象牙旧纸及旧黄铜，保留细微材质变化与绘画质感，同时避免把完整道具、文字或强烈方向阴影直接画进材质。

### 早期材质历史

早期冷灰方案的文件名为 `material_atlas.webp`、`cloth.webp`、`paper.webp`，同样来自 1254 × 1254 的 AI 生成四格底色图，并以 WebP quality 85 派生。其四格依次为冷灰褐木材、深绿灰布面、浅色旧纸和低饱和做旧金属。本段保留来源及命名历史；当前标题画面采用上述 `reference-*` 版本。早期提示词摘要为近代侦探书桌的暗木、深布、旧纸与做旧金属，偏冷褐、灰绿和低饱和色调。

用户最新要求以参考图为准，因此本次**标题场景**使用暖金色灯光及旧黄铜色调；这不是对全游戏场景配色的修改。

## 独立法线与粗糙度数据烘焙

在当前 `reference-material-atlas.webp` 已就位后，使用 Blender 4.2 执行：

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.2\blender.exe' --factory-startup --background --python 'godot_csharp/tools/build_desk_surface_maps.py'
```

该脚本使用原生程序材质节点，分别指定木材方向性细槽和磨损、布面经纬、纸纤维以及黄铜浅划痕与氧化粗糙度。节点的凹凸距离分别为木材 0.001 m、布面 0.0005 m、纸面 0.00015 m、黄铜 0.0002 m；四种样片宽度分别为 1.0 m、0.45 m、0.45 m、0.35 m。这些是作者设置的微表面尺度，并非从参考照片测量得到；最终视觉尺度还取决于运行时 UV 缩放和法线强度。

参考底色只提供空间配准：局部高通残差最多影响归一化高度的 ±0.03，宽范围色彩和照明不会直接变成形体。主高度与粗糙度由独立的程序节点计算。CPU Cycles 使用固定种子 `73417`、8 samples，分别执行 `NORMAL` 和 `ROUGHNESS` 数据烘焙；这些图不是 imagegen 生成的法线外观图片，也不改变原有 albedo 美术。

| 生成文件 | 尺寸与用途 |
| --- | --- |
| `reference-normal.png` | 1024 × 1024；OpenGL 切线空间 +Y 法线，线性 RGB 数据。 |
| `reference-roughness.png` | 1024 × 1024；线性单通道粗糙度。 |
| `reference-cloth-normal.png` / `reference-cloth-roughness.png` | 各 512 × 512；直接裁取烘焙 atlas 右上象限。 |
| `reference-paper-normal.png` / `reference-paper-roughness.png` | 各 512 × 512；直接裁取烘焙 atlas 左下象限。 |

PNG 使用 8 bit 数据编码，不经过 AgX、显示变换或 gamma 转换；裁切与 atlas 使用同一烘焙像素缓冲区。Godot 中法线应按法线数据采样，粗糙度按线性数据采样，均不使用底色的 `source_color` 转换。图集布局与原始底色一致：左上木材、右上布面、左下纸面、右下黄铜。

脚本把上述 PNG 写入运行时 `Painterly3D` 目录，把节点源与 `reference-surface-maps-manifest.json` 写入 `AssetsSource` 的同名目录。manifest 记录实际 Blender 版本、输入底色 SHA256、参数、各象限法线/粗糙度统计及输出 SHA256。运行时检查空白或非法法线及恒定粗糙度；这些数据检查不代替 Godot 实机视觉验收。脚本仅依赖 Blender 与 Python 标准库，不要求安装 NumPy 或 Pillow。

## 共享书本的运行时装配

卷首与游戏内案卷共用 `TitleUI.tscn` 的实体书：`CasebookUI.gd` 只以 `mode = "casebook"` 提供入口与当前 viewer 的纯 UI 案卷数据。桌面几何、真实光照、书体尺寸、60°相机契约、翻页网格及 `BookProjection3D.gd` 的页面命中路径保持共用。设置、目录、条目、批注和书签属于原生 Control 内容，由 `BookSettingsPage.gd` / `BookArchivePage.gd` 等页面装配进 SubViewport；新增这些页面无需重建 `desk.glb`，也不将文字或批注烘焙进底色、法线或粗糙度。

`BookLeafTurn3D.gd` 的正面与背面使用向外的绕序、各自法线和对应 UV 切线，厚度边缘闭合；`book_leaf_mesh_3d.gdshader` 使用背面剔除，并依据独立表面标识选择正背纹理。背面文字 UV 与法线贴图基向量同步，切线框架随叶片形变；不能在片元阶段再次翻转几何法线来补偿错误绕序。微表面写入 `NORMAL_MAP` / `NORMAL_MAP_DEPTH`，几何法线保留给真实受光与阴影。这两个运行时文件作为同一网格/材质接口维护；修改它们不会自动更新 Blender 道具模型，也不能以压平光照或重绘背面底色代替几何修复。

页面和独立现场界面的版式由 `tools/build_ui_theme.py` 统一生成三份主题：`TitleTheme.tres` 用于实体书，`DialogueTheme.tres` 用于对白，`OverlayTheme.tres` 用于现场 HUD 与独立推演纸页。三者共用语义纸色、墨色、笔迹色与字体角色；Overlay 的纸面直接引用现役 `reference-paper.webp`，不增加第二套素材生成配方。

三语 UI 偏好保存在独立的宿主设置文件，原始正文与玩家批注保留原文；它们不是本目录的素材生成输入。案卷逐行来源、个人批注与独立推演、绑定 viewer 和存档哈希的二进制侧车合同见 `godot_csharp/AGENTS.md`。共享书本的观察工具为 `tools/capture_casebook_ui.gd`，命令与输出见 `tools/README.md`：它使用有限可见内容夹具，不启动 Main/DV，画面不能作为实际取得证物、完整书签恢复或玩法验收依据。本节只记录装配与资源边界，构建和视觉效果由后续实际证据报告。

## 实现边界

灯光闪烁来自同一组连续时间函数，驱动火芯形变、光源位置与能量，物体阴影由 Godot 实时计算。其他玻璃仍采用透明混合、Fresnel 轮廓和受光反射；仅 `LensGlass` 的 `magnifier_lens.gdshader` 实现深度约束的屏幕空间放大、折射与 Fresnel 响应。当前默认放大参数为 `magnification = 1.32`、折射率为 `refractive_index = 1.52`，以实际着色器为准。屏幕拷贝来自不透明绘制阶段，采样图像不含后绘的透明火焰和玻璃；这是屏幕空间近似，不是真实光线追踪，也不代表尺寸参考品牌的光学性能。火焰由独立发光着色器表现，不是流体燃烧模拟。本说明记录资产来源、生成方式和实现边界，不构成运行效果或交互验收记录；实际表现以 Godot 实机截图及相应交互记录为准。
