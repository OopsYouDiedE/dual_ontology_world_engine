# 初醒室内运行时物件

当前运行时家具与生活翼建筑由 `godot_csharp/tools/first_awakening_art/` 的原生像素配方绘制；入口为 `godot_csharp/tools/build_first_awakening_interior.py`。早期 image_gen 源稿及提示仍保存在 `godot_csharp/AssetsSource/Internal/Props/FirstAwakeningInterior/` 供来源追溯，当前重建不读取这些图像。

重建：在仓库根目录执行 `godot_csharp/addons/rpg_asset_adapter/mcp/.venv/Scripts/python.exe godot_csharp/tools/build_first_awakening_interior.py`，然后由 Godot 导入资源。单一输出目录，直接更新现行资源，不建立按轮次分裂的副本。

`manifest.json` 记录生产入口、原生配方过程、实际像素尺寸及脚底锚点。PNG由配方直接逐像素绘制，没有源图缩放或量化步骤。所有家具与道具由 FirstAwakening 场景装配；碰撞由场景脚底占地决定，不能把整张精灵透明画布当作碰撞盒。技术生产路径不等于全部艺术验收通过。

四间宿舍共用 `bed_0`、`desk_0`、`bookcase_empty`、`linen_chest_0` 和相同地毯、墙面、灯光配方，家具相对位置一致。书架、衣帽架与桌面为空，梳妆台统一放白瓷小碟；抽屉里的空白纸由检视文案呈现。侦探椅背外衣的袖口灰痕和包楔纸篓保留案件线索，其他房间使用普通椅子与 `paper_basket_empty`。修改须更新配方并重建场景/布局，避免旧职业摆景复生。

床被褥、自动宿舍门、隔离门内销、脑内回放的门缝与物件遮挡需要运行时验收。固定截图、实机移动和空间审查位于 `artifacts/first-awakening/`，截图不代替最终艺术质量判定。

`mind_seal.png` 为原生 24×24 像素绘制，2倍最近邻显示于推理页；其独立配方为 `godot_csharp/tools/first_awakening_art/mind_ui.py`，来源及入口见 AssetsSource 同族 README。它不由家具重建入口生成。

`mind-ui-*.png`与`mind_theme.tres`由既有MCP工具`build_diegetic_ui`统一生成，源与规格清单在AssetsSource同族。`MindPresentation.gd`、`MindBackdrop.gd`和`MindThemePreview.tscn`共用这套切片。首次生成后需Godot导入PNG，再调用`inspect_godot_ui`验收；该工具只检查样板，当前动态推演页另运行`check/playtest_ui.gd`。家具builder不重建这些UI资源。
