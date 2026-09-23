# 公共娱乐室构图原型

在Godot中打开 `Parlor.tscn`，按F6运行。场景使用 `ShareAsset/Victorian/tilesets/Victorian.tres`，基础格32px。它验证场景构图与素材组合，尚未实现玩家交互。

生成入口为 `res://tools/build_parlor.gd`，生成记录输出到仓库 `artifacts/parlor-validation/`。重建会覆盖本场景；手工编辑后先保存需要保留的修改。旧验证截图已清理，不能据旧统计认定当前版本通过验收。

布局参照 `artifacts/theme-film/scenes/shot-03.png`，由地面、墙体、墙顶、地毯、家具和桌面物件六层组成。实际制作须按仓库 `docs/design-standard.md` 检查比例、遮挡、通路和操作。
