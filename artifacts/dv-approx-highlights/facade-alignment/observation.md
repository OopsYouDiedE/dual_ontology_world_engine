# 静态地图实机渲染观察

观察日期：2026-10-25 UTC。采集使用 Godot 4.6.1、`gl_compatibility` / ANGLE、Dummy 音频、窗口化普通渲染。通过 `godot_csharp/tools/capture_scene.gd` 的静态镜头参数取帧；未启动高光舞台、正式会话或注入世界状态。以下固定镜头只用于材质与画面核对，不代表正式会话中的玩家镜头。

## 讲堂外立面

目标图：[12_settlement_material_refinement.png](../../campus-architecture-review/astra/12_settlement_material_refinement.png)。当前画面：[scene.png](scene.png)，场景 `res://scenes/Chapter2/Maps/campus_nw.tscn`，相机位置 `(2352, 2020)`、zoom `1.6`。捕获参数、时间与 SHA-256 见 [capture-provenance.json](capture-provenance.json)。

保留的旧大屋顶和主体布局在画面上方占据了立面主体的大部分；新增墙面仅在屋檐下露出一条横向带。新增区域的暖白墙、窄木窗、深色瓦檐和石基在颜色上彼此可辨，六扇窗的竖向比例清楚，瓦檐连续跨过正面，左右端以卷翘檐角收尾。与目标图相比，当前游戏画面是明显的像素块材质：墙面细碎横向砌纹重复感强，石基块纹颗粒密且对比偏高，瓦檐纹样也呈规则重复；目标图的墙面更平整、开窗更宽，石材与瓦面具有更连续的明暗层次。两者的材质语言和建筑尺度尚未对齐。

比例上，当前石基约占新增浅墙高度的三成，视觉重量较重；目标图里基座相对整面建筑高度更收敛。当前六扇窄窗分布均匀、墙间距较大，读作狭长廊房；目标主体是多层建筑，窗户分布更密且上下层关系明确。当前檐口厚度和暗度突出，形成一道强横线，压住了墙窗带。整体轮廓整齐，但墙面贴图的水平行列和石基水平灰缝都能直接读出重复周期；画面未见贴图撕裂或明显错位。檐口下沿与墙顶相接连续，没有可见漏底；主立面右侧与相邻结构的连接处被不同深度的屋面/墙体遮挡，镜头里无法确认接缝背后的真实几何连续性。目标图不含该游戏场景的邻接结构，故此处不能据目标图判断。

## 中央交通厅参考对照

目标图：[12-central-hall.png](../../chapter12-room-design/12-central-hall.png)。当前画面：[scene.png](central-hall/scene.png)，场景 `res://scenes/Chapter2/Maps/central_hall.tscn`，相机位置 `(416, 416)`、zoom `1.0`。该固定观察镜头并非正式会话镜头。采集参数见 [capture-provenance.json](central-hall/capture-provenance.json)。

当前地面保留了暖灰拼石、中央金色圆盘和左右对称布置，主要空间关系能与目标图对应。当前厅室轮廓和外沿更像矩形台阶切角，外侧是无细节的深色空场；目标图有完整深木框架、墙板、灯具、长椅、告示与盆栽，厅室边界也由连续室内构件封闭。游戏画面中的石砖尺寸与重复灰缝更规则，金色照明落成几条窄亮带；目标图砖面阴影更柔和，圆盘周边的金属细节更丰富。当前图未见明显地面贴图断裂，外围缺少目标图中的墙体细节与装饰层次。

## 粮仓参考对照

目标图：[06-granary.png](../../chapter12-room-design/06-granary.png)。当前画面：[scene.png](granary/scene.png)，场景 `res://scenes/Chapter2/Maps/granary.tscn`，相机位置 `(160, 144)`、zoom `2.5`。该固定观察镜头并非正式会话镜头。采集参数见 [capture-provenance.json](granary/capture-provenance.json)。

当前室内使用棕色木地板、灰砖后墙与木箱/桶，材质色调与目标图相近。当前房间构图明显更空：两只小袋、一个桶和两个箱子周围留有大块空地；目标图的墙边有双粮槽、堆叠麻袋、多个木桶、散装粮食与门口木构件，灯光也更温暖。当前地板是规则横向长板重复，木纹对比与亮度变化较均匀；目标图的木材有更深的边框、立体压边和道具投影。当前外轮廓显示为紫黑色空场边框，目标图则以厚重石木围边完整收口。画面内未见地板明显裂缝或物件互相穿插；当前道具密度、围边和光影层次与目标仍有可见差距。

## 采集清单

| 场景 | 固定镜头 | 结果 |
| --- | --- | --- |
| `campus_nw.tscn` | `(2352, 2020)`, zoom `1.6` | 成功，1280×992；ANGLE GL Compatibility |
| `central_hall.tscn` | `(416, 416)`, zoom `1.0` | 成功，1280×992；ANGLE GL Compatibility |
| `granary.tscn` | `(160, 144)`, zoom `2.5` | 成功，1280×992；ANGLE GL Compatibility |

Godot 资产导入在采集前完成；三次捕获均以默认 `scene` 步骤等待 0.5 秒并保存 PNG。画面比较只记录上述固定视野内可见内容，不推断未入镜区域或正式游玩效果。
