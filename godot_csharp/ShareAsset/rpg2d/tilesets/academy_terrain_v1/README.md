# 学院地形纹理 v1

按 Victorian 原生地形图的 A2 / A4 布局制作的学院主题副本，参考分镜 shot-03 与 shot-19。原始 `ShareAsset/victorian/48px+MVMZ` 保持不变。

## 可用资源

- `48px+MVMZ/Academy_Inside_A2.png`：768×576，地面自动图块；左侧五列采用重绘纹理，右侧三列保留原始素材。包含深木地板、酒红金纹地毯、蓝金地毯、石地板。
- `48px+MVMZ/Academy_Inside_A4.png`：768×720，墙顶与墙面自动图块；包含蓝灰砌石、胡桃木护墙和低饱和壁纸。
- `AcademyTerrain48.tres`：可直接赋给 Godot TileMapLayer；包含上述地形及上一轮16组家具。原 source_id、地形编号及邻接数据保持一致。
- `atlases/`：由现有 RPG Asset Adapter 的自动图块转换器生成的 Godot 图集 PNG / ImageTexture。

48px 是地图格尺寸；自动拼接使用24×24子块。A2按96×144区域排列；A4每组96×240，上144像素为墙顶，下96像素为墙面。不能把 A2 / A4 当作普通逐格图集直接切片。

## 示例

`res://scenes/AcademyTerrain/Parlor48.tscn` 使用胡桃木墙、暖灰方砖与酒红地毯。

`res://scenes/AcademyTerrain/Corridor48.tscn` 使用蓝灰砖墙、冷色石砖与蓝金地毯。冷色地面通过图层调色实现。

两张场景仍为24×16格，家具沿用上一轮的分镜扩充素材。运行时使用已保存的 TileSet 和场景，无需执行生成脚本。检查渲染见 `artifacts/academy-terrain-validation/`。

## 制作与验证

内置 imagegen 重绘原图纹理，提示词保存在 prompts.md，实际生成源图保存在 source/。A2源图1448×1086，裁去额外重复边缘后取1365×1024；A4源图1295×1214。二者以最近邻整理回原图尺寸，再逐像素恢复原始透明度。A2石地板取重绘稿中的48×48方砖，在96×144源块内按同一相位重排，修正自动拼接时出现的长条纹。

这是符合原生48px布局的**生成纹理适配版**，不宣称生成模型直接输出了原生分辨率或人工逐像素重绘。纹理仍含生成细节；未使用的全部地形组合尚未逐一视觉验收。

构建入口为 `res://tools/build_academy_terrain.gd`。脚本检查源图尺寸、逐像素透明度、替换图集尺寸及场景重载后的图块有效性。首次构建需要上一轮 `Academy48.tres` 和示例场景。执行会覆盖本目录派生资源及 AcademyTerrain 示例；手工编辑前另存副本。传入用户参数 `--capture` 可记录实际 Godot 渲染。

本素材继承原 Victorian 资源的使用条件；生成副本不改变原资源许可。
