# 学院与火山同框：实际 3D 镜头与细化报告

2026-10-05。使用当前 `artifacts/remake-chapter2/campus-site/build_3d.py` 与 `scene.json` 重建的科尔沃岛模型，真实地形水平与高程缩放 0.5。旧 `island-3d-structure` 为旧地貌原案，本轮没有使用。

## 实际结果

测试六个机位并保留图、世界坐标与焦距：门外人眼、誓约庭人眼、誓约庭 6 m、生活翼东门人眼、南侧 25 m、东南 45 m。低视角能够同时保住建筑和背景山缘。院内机位最适合表现身处学院的感觉；25 m 机位展示更多建筑。门外和生活翼旁存在真实近墙遮挡；六图比较在 `six_camera_contact_sheet.jpg`。

最终推荐机位是 `02_court_eye`：移到誓约庭北部，局部 u=0、v=-88 m，双线性地表上约 1.7 m、35 mm、16:9，前景矮墙已避开。保留原机位 `02_court_eye_original`。第二张为 `05_south_25m`：u=0、v=-210 m，地表上约 25 m、35 mm。低视角能看到火山外坡与山缘，不能看到火口内部和湖；没有拉高或放大山体。

## 3D 改动

生产源：`godot_csharp/AssetsSource/Internal/Props/MapOptimization/3D/campus_camera_refined_v1.blend`。可重复生成脚本：`godot_csharp/tools/refine_campus_camera_3d.py`。

保留原 141 个网格的顶点、位置、变换与世界包围盒；追加真实门窗、门框石柱、拱券与敞开门叶、入口台阶、穹顶 24 根铜肋、檐沟排水管和少量植物。新增 1,464 个对象，完整场景 1,545 个 mesh。增加体量的比例与学园总体布局未变。

第二轮细化道路铺石、横排石墙、白灰墙、瓦面、铜和草岩材质，程序纹理显式使用 `Geometry.Position` 世界米制坐标。第三轮针对屋面纹理误铺垂直山墙作修正：按法线区分瓦坡和垂直封口，坡面保留瓦排，山墙使用白灰/横排石墙。对照图为 `refined/gable_cap_comparison.jpg`；前版保留 `*_structure_pass.png`、`*_material_pass_first.png`。

最终真实渲染：`refined/02_court_eye_final.png` 与 `refined/05_south_25m_final.png`，1920×1080、Blender 4.2、Cycles 64 samples。相机参数和源哈希记录在 `refined/delivery_manifest.json`。模型仍是结构与材质制作源，植物和远山细节有继续深化空间。

## 精绘派生图

`refined/academy_volcano_eye_level_painted_v1.png` 使用内置 imagegen 从真实院内渲染精绘；美术源及完整提示词保存在 `AssetsSource/Internal/Props/MapOptimization/`。输入渲染已归档为 `3D/hero_render_reference_v1.png`。

精绘图保留主要建筑与山缘构图，但台阶和细部纹理存在生成变化，只作美术展示；几何验收以真实模型和渲染为准。

精绘输入来自屋面封口修正前的材质版，已原样归档；它不作为最新山墙材质验收的证据。采用的镜头与制作规约统一记录在 `docs/design-standard.md` 第五部分，本文只保存本轮制作和测试结果。

## 独立验证

机位、结构细化与山墙修正分别由全新独立上下文 Agent 验证，不读取设计文稿。证据见 `independent-camera-verification.json`、`refined/independent-refined-verification.json` 和 `refined/independent-gable-verification.json`。执行实际源加载、几何对比、材质节点追踪与独立重渲染，不是剧情盲玩或 Godot 游戏验证。

双线性地形插值与 Blender 三角网格的高度存在厘米以内至约 1.5 cm 的差异：院内实际 mesh 射线眼高约 1.7046 m，南侧约 24.9853 m。因此机位高度按约数描述。没有修改运行时场景、C# 机制或碰撞。

重建（仓库根目录）：

```powershell
& 'C:/Program Files/Blender Foundation/Blender 4.2/blender.exe' -b -P godot_csharp/tools/refine_campus_camera_3d.py
```

首次准备需先执行本目录 `rebuild_terrain.py` 和 `render_camera_tests.py`；生产脚本依赖此任务保留的 `camera_tests.blend` 与 `terrain.npz`。原文件和各阶段模型/图保留，没有覆盖原学院源码或原模型。
