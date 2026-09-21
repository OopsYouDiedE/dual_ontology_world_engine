# 修正说明

本次修正集中在资源元数据转换，保留现有图集布局和游戏层职责。

- B–E 图块编号改为左右两块 8 列区域；A1–A4 在重组时记录原始图块编号，保存后仍可重新应用标记。
- `Tilesets.json` 仅按素材基本文件名匹配；未匹配时跳过并返回警告。已匹配条目只作用于对应槽位的同名图集。
- 每次完整写入通行和属性标记，清除旧值及旧碰撞。星标图块不生成阻挡碰撞；方向通行和星标的组合规则由游戏层处理。
- 角色或物件未生成资源时，统一入口返回转换错误。

现有 A2 右半区作为普通装饰图块保留，不套用标准自动图块标记。KR Non-RM 图集同样不套用 RPG Maker 的槽位编号。一次转换仍只选择一个匹配的 Tilesets.json 条目，需要不同规则的素材应分批转换。

已有产物不会自动改写。需要新行为时重新执行转换；对应输出文件会被覆盖。

## 验证

在仓库根目录运行，`godot` 替换为本机实际 Godot 可执行文件：

```text
godot --headless --path godot_csharp --script res://addons/rpg_asset_adapter/test_metadata.gd
```

检查涵盖编号映射、匹配隔离、属性重置、碰撞更新及资源保存后重新加载。测试在项目目录生成临时资源并在正常结束时删除，不依赖外部素材。

编号依据：[RPG Maker MZ Tilemap 实现](https://developer.rpgmakerweb.com/rpg-maker-mz/Tilemap.js.html)。
