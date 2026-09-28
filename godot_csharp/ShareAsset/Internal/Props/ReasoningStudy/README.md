# 研究室：自动铺装素材

用于 `res://scenes/Reasoning/StudyRoom.tscn`。32×32 为地图格，16×16 为自动图块的四分之一块。原 Victorian 资源未覆盖。

## 文件与格式

| 文件 | 尺寸 | 内容和使用方式 |
|---|---|---|
| Study_A2.png | 512×384 | 首行两个64×96自动块：石板地面、蓝金地毯；其他槽位透明预留 |
| Study_A4.png | 512×480 | 首组两列，每列64×160：上64×96墙顶、下64×64墙面；石质和木质各一组 |
| Study_A5.png | 256×512 | 普通32px格：左上4×4石板、右上4×4石墙、左侧第4–5行木板、右侧第4–7行地毯；其余透明 |
| Study_B.png | 512×512 | 透明家具图集：工作桌、书柜、工作台、拱窗；跨格区域及脚底锚点见manifest.json |
| Study.tres | — | 已配置的Godot TileSet，实际场景使用此资源 |
| TerrainPreview.tscn | — | 自动连接的单格、矩形、L形、内孔、十字预览 |

A2采用当前项目转换器支持的布局，只填左侧自动块区域。未新增动画水面，不生成空的A1；当前家具一张B装得下，不制造重复C。B的透明像素不添加自动地形语义；多格家具在游戏中作为独立Y排序节点使用，不能把整张家具画布当作碰撞。

A2和A4墙顶由现有 `autotile_converter.gd` 的48形重组；A4墙面由16形重组。源布局保留48形需要的四分之一块，但Godot自动连接候选排除与46形连接掩码重复的47形，避免孤立块出现两套候选。TerrainSet 0匹配边与角，TerrainSet 1仅匹配边。

`.res` 为同名PNG的可直接加载派生Texture，避免重建依赖编辑器导入顺序。`*_derived.png/.res` 为展开后的运行时自动图块图集，不应手改；PNG供检视，场景经TileSet引用Texture资源。

## 来源与处理

`AssetsSource/Internal/Props/ReasoningStudy/source/materials.png` 与 `furniture.png` 是内置image_gen生成的1254×1254源图，使用 `Images/illustrations/shot-02.png` 作为风格参考。[完整提示词](../../../../AssetsSource/Internal/Props/ReasoningStudy/source/prompts.md)保留。源图不修改。

这些素材经过降采样、透明边界裁切、四分之一块排布与连接边饰加工，属于32px游戏派生资源，**不是原生32px逐像素原绘**。生成源本身没有提供正确的自动图块结构：边缘、内角、外角由构建脚本明确装配，不能把四个原始材质方格直接当作A2/A4。

## 重建与铺装

从Godot工程目录运行（使用Godot .NET 4.6.1可执行程序）：

```text
godot --headless --path . --script res://tools/build_study_tiles.gd
godot --headless --path . --script res://tools/build_study.gd
godot --path . --script res://tools/check_reasoning_study_tiles.gd
```

Windows环境如用户日志目录不可写，使用 `--log-file` 指定项目内日志。构建入口只读source并重建本目录派生资源；第二条只重建StudyRoom。

编辑器中将 `Study.tres` 指定给TileMapLayer，选择Terrains中的中文材质名称，以Connect方式绘制。代码参照 `godot_csharp/tools/build_study.gd` 按名称查询terrain索引，调用 `set_cells_terrain_connect(cells, terrain_set, terrain, false)`。显式考虑空邻接才能稳定生成边界。

地面、墙面、墙顶、护墙、地毯分别放在独立TileMapLayer。每种terrain支持与空白连接；本批没有定义石材直接渐变成地毯的混合地形，地毯在上层覆盖地面。A5/B普通瓦片放在各自图层，避免自动重连改动其邻域。

## 验证与边界

`godot_csharp/tools/check_reasoning_study_tiles.gd` 实际调用Godot连接算法，检查六种terrain共360个已铺单元的边和角邻接；同时检查A5/B没有错误注册terrain。可视预览展示四种代表性材质。研究室实机脚本检查移动碰撞、调查、回放及现实/脑内分离。

本批完成格式、自动连接和场景引用验证。图案重复感、装饰丰富度与最终概念图一致性仍需继续视觉打磨，不将自动铺装通过等同于整案或最终美术验收。
