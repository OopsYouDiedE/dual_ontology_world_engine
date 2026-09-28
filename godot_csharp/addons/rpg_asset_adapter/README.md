# RPG Asset Adapter

将 RPG Maker / Kokoro Reflections 素材转换为 Godot 4 TileSet、SpriteFrames 和独立场景。插件属于游戏资源工具，不依赖核心 C# 引擎。

插件只负责资源转换，不承担游戏层的运行时绘制、通行规则或交互。

## 入口与职责

- [TileMap MCP 工作流](mcp/README.md)：新图绘制、显式裁切适配、47形墙顶包边、房间预览与Godot导出；[设计规范](../../../docs/tilemap-workflow.md)。

- `plugin.gd`：提供 `Project > Tools > RPG Asset Adapter: Convert Saved Config`，读取已保存配置并执行一次转换。
- `adapter_facade.gd`：统一调用入口，组织检测、转换与资源保存。
- `detector/`、`profiles/`：识别素材类型及来源约定。
- `converter/`、`data/`：自动图块重组、地形邻接、普通图集切片、动画和元数据。
- `run_batch_convert.gd`：项目 Victorian 素材批处理入口。转换结果含错误时以非零状态退出。
- `test_adapter.gd`、`test_visual.gd`、`test_render.gd`：已有的转换和渲染检查脚本。

## 使用

项目已经启用插件。游戏地图基础格为32px；转换器会优先使用文件名或图像尺寸识别外部素材的原生格子，因此识别出的48px素材仍按48px处理。默认输入目录为 `res://AssetsSource/External/Victorian`，输出目录为 `res://ShareAsset/External/Victorian`，其中 `tilesets/Victorian.tres` 是地图使用的 TileSet，`atlases/` 保存重组图集。

命令行在仓库根目录执行，`godot` 替换为本机 Godot 4.6 Mono 可执行文件：

```text
godot --headless --path godot_csharp --script res://addons/rpg_asset_adapter/run_batch_convert.gd
```

批处理会覆盖对应的已有转换产物。仅制作地图时无需重复转换。

## 转换边界

- B–E 图块按左右两块8列区域处理；A1–A4重组时记录原始图块编号，保存后可重新应用标记。
- `Tilesets.json` 只按素材基本文件名匹配；未匹配时跳过并返回警告，匹配条目只作用于对应槽位的同名图集。
- 每次转换完整写入通行和属性标记，清除旧值及旧碰撞。星标图块不生成阻挡碰撞；方向通行和星标组合由游戏层处理。
- 角色或物件未生成资源时，统一入口返回转换错误。
- A2右半区作为普通装饰图块保留；KR Non-RM图集不套用RPG Maker槽位编号。一次转换只选择一个匹配的 `Tilesets.json` 条目，需要不同规则的素材应分批转换。
- 已有产物不会自动改写；重新执行转换时对应出出文件会被覆盖。

## 实际地图验证

当前公共娱乐室由 `res://scenes/Story/ParlorGathering.tscn` 维护。旧路径 `res://scenes/Parlor/Parlor.tscn` 仅作为兼容入口实例化该 Story 场景。

```text
godot --headless --path godot_csharp --script res://tools/build_parlor.gd
```

该兼容命令只报告当前Story场景路径并退出，不会生成或覆盖平行场景。实际操作与验证方式见 `res://scenes/Story/README.md`。

Story 房间使用项目当前的32px布局资源；Victorian TileSet仍供兼容资源与Reasoning研究室使用。TileSet 的 `suggested_layer` 等自定义数据是提示；地图作者负责实际分层。多格家具保留原生尺寸，并按基础格对齐。

元数据转换验证可运行：

```text
godot --headless --path godot_csharp --script res://addons/rpg_asset_adapter/test_metadata.gd
```
