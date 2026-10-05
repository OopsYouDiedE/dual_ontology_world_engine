# 地图绘图优化交付（2026-10-05）

本轮交付美术生产源与结构图，不改正式玩法、场景、地图碰撞或资源引用。已保留工作区既有修改及并行任务的设计规范修改。

## 图片与用途

| 文件 | 用途与实际状态 |
| --- | --- |
| `island_overview_painted_v1.png` | 全岛地貌与色彩参考；以现有 `campus-site/island_corvo.png` 为形状参考。生成绘画不是 DEM 栅格，不能据此验收等高线、坡度或真实比例。 |
| `campus_overview_painted_v1.png` | 学园建筑、材质、植被与路线视觉参考；以现有 `campus_plan.png` 和生活翼体量图为参考。总体方位可辨，但生成图的建筑外廓、路程、台阶和塌方不作精确布局依据。 |
| `living_wing_material_study_v1.png` | 生活翼高侧窗、分段基底、配重门与铜管的材质研究图；室内分间与每区六间的确定布局不一致，仅用于材质/立面参考。 |
| `living_wing_plan_native_v1.png` | 原生绘图工具绘制的确定结构平面图；保留四区、每区六间、两条露天夹道、中央竖厅与南附楼。14 px/m 是图纸表现比例，不是游戏地图的 1 m = 24 px。 |
| `detail_structures_native_v1.png` | 640×480 原生像素结构样板，六个面板：门口、石阶、雨水桶与排水沟、铜管接头、碎石夹道、绣球。仅为美术源。 |

展示副本在本目录；美术源在 `godot_csharp/AssetsSource/Internal/Props/MapOptimization/`。三张生成绘画使用内置 `image_gen__imagegen`，完整最终提示词与输入图路径保存于源目录 `imagegen_prompts_v1.json`。两张原生图使用 `mcp__rpg_tilemap__draw_asset`，保留原生指令与配方。未将未经实机验收的样板放入 ShareAsset。

## 技术验证与限制

独立上下文 Agent 不读取项目设计文稿，检查 PNG 尺寸、源/展示哈希、原生指令重放、裁切与锚点，检查运行时直接引用。机器可读证据在 `independent-verification.json`。技术通过不代表艺术验收或游戏试玩通过。

细节图共 15,557 条原生 rect/line/point 指令，无抗锯齿；独立指令重放与原图逐像素一致。保留建议脚底锚点，其中夹道面板采用裁切底边 pivot；全部碰撞 footprint 未定义、遮挡未验收。画布尺寸为 32 的倍数，不代表每个内容 crop 恰好占整数格。正式切片须按原生 crop 处理。

细节图完整重建命令：`python godot_csharp/tools/rebuild_map_detail_structures_native_v1.py`（依赖 Pillow）。JS 入口只输出配方；Python 重建 PNG 并核对内容，拒绝覆盖不同内容。

结构平面图共 731 条原生绘图指令。独立验收确认 24 个卧室（16 入住、8 空房）、24 处卧室开口、左右镜像、逐房 14 px/m 像素坐标、3 m 夹道、10×29.4 m 中央厅和 10×6 m 附楼均与输入规格一致。完整重建命令：`python godot_csharp/tools/rebuild_living_wing_plan_native_v1.py`。配方、逐房 manifest 在美术源目录。图中无文字标签，北向上，甲/丁/丙/乙依次为左上/右上/左下/右下；平面内部小件只是示意，家具最终落位沿用真实场景资源。

五张 PNG 的源/展示哈希一致，两张原生图均经过独立实际重建。全套新图直接运行时引用为零，验收报告没有待测项；运行时接入属于尚未实施的后续工作。

本轮未运行 Godot、未进行接缝/碰撞/Y 轴遮挡验收或盲玩，未声称这些通过。运行时采用现有 C# 逻辑与地图布局；生成绘画不反向修改确定地形或叙事设置。
