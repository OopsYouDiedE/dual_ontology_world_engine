# RPG Asset Adapter

将 RPG Maker / Kokoro Reflections 素材转换为 Godot 4 TileSet、SpriteFrames 和独立场景。插件属于游戏资源工具，不依赖核心 C# 引擎。

插件只负责资源转换，不承担游戏层的运行时绘制、通行规则或交互。

## 入口与职责

- [TileMap MCP 工作流](mcp/README.md)：新图绘制、显式裁切适配、47形墙顶包边、房间预览与Godot导出；[统一设计规范](../../docs/design-standard.md#第五部分32px-像素美术与地图工作流)。

- `plugin.gd`：提供 `Project > Tools > RPG Asset Adapter: Convert Saved Config`，读取已保存配置并执行一次转换。
- `adapter_facade.gd`：统一调用入口，组织检测、转换与资源保存。
- `detector/`、`profiles/`：识别素材类型及来源约定。
- `converter/`、`data/`：自动图块重组、地形邻接、普通图集切片、动画和元数据。
- `run_batch_convert.gd`：项目 Victorian 素材批处理入口。转换结果含错误时以非零状态退出。
- `test_adapter.gd`、`test_visual.gd`、`test_render.gd`：已有的转换和渲染检查脚本。

## 使用

通过 `python mcp/godot_workspace.py --editor` 打开工具工程。正式游戏工程只读取发布资产。工具工程将 `res://AssetsSource` 映射到 `mcp/asset_pipeline/sources`，将 `res://ShareAsset` 映射到游戏发布资产目录。游戏地图基础格为32px；转换器会优先使用文件名或图像尺寸识别外部素材的原生格子，因此识别出的48px素材仍按48px处理。默认输入目录为 `res://AssetsSource/External/Victorian`，输出目录为 `res://ShareAsset/External/Victorian`，其中 `tilesets/Victorian.tres` 是地图使用的 TileSet，`atlases/` 保存重组图集。

命令行在仓库根目录执行，通过 `GODOT`、`GODOT_BIN` 或 `--godot` 指定本机 Godot 4.6 可执行文件：

```text
python mcp/godot_workspace.py --headless --script res://addons/rpg_asset_adapter/run_batch_convert.gd
```

批处理会覆盖对应的已有转换产物。仅制作地图时无需重复转换。

统一 MCP 的 `godot_tools` 提供 `adapter-batch`、`adapter-conversion`、`adapter-visual`、`adapter-render`、`adapter-metadata` 和 `adapter-tagged-slice`。这些路由使用独立工具工程，不要求 C# 构建。Python 工程入口由 `adapter_checks` 提供；绘图、地图、像素交接和 UI 度量函数由 `rpg_adapter` 提供。所有入口以 `mcp/tools.json` 为同一目录。

## 转换边界

- B–E 图块按左右两块8列区域处理；A1–A4重组时记录原始图块编号，保存后可重新应用标记。
- `Tilesets.json` 只按素材基本文件名匹配；未匹配时跳过并返回警告，匹配条目只作用于对应槽位的同名图集。
- 每次转换完整写入通行和属性标记，清除旧值及旧碰撞。星标图块不生成阻挡碰撞；方向通行和星标组合由游戏层处理。
- 角色或物件未生成资源时，统一入口返回转换错误。
- A2右半区作为普通装饰图块保留；KR Non-RM图集不套用RPG Maker槽位编号。一次转换只选择一个匹配的 `Tilesets.json` 条目，需要不同规则的素材应分批转换。
- 已有产物不会自动改写；重新执行转换时对应出出文件会被覆盖。

## 地图开发

地图与物理交互使用 `python mcp/godot_workspace.py --sandbox` 打开的独立沙盒。共享游戏场景与资产从正式目录挂载，转换器、编辑器插件和采集脚本只存在于工具工程。MCP TileMap 导出生成 `mcp/sandboxes/generated/TileMapWorkflowPreview.tscn`；预览角色与工具引用不会写入正式游戏场景。

配置文件位于 `mcp/rpg_adapter/victorian_config.tres`，默认源素材保持在工具管线，输出 TileSet、图集和运行场景写到 `godot_csharp/ShareAsset`。转换边界沿用上文声明。
