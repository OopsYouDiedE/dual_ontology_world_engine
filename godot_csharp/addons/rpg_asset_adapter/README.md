# RPG Asset Adapter

将 RPG Maker / Kokoro Reflections 素材转换为 Godot 4 TileSet、SpriteFrames 和独立场景。插件属于游戏资源工具，不依赖核心 C# 引擎。

本次修正范围、限制和验证命令见 [修正说明](FIXES.md)。

## 入口与职责

- `plugin.gd` 和 `ui/`：编辑器底部的 RPG Asset Adapter 面板，选择目录、扫描、转换。
- `adapter_facade.gd`：统一调用入口，组织检测、转换与资源保存。
- `detector/`、`profiles/`：识别素材类型及来源约定。
- `converter/`、`data/`：自动图块重组、地形邻接、普通图集切片、动画和元数据。
- `run_batch_convert.gd`：项目 Victorian 素材批处理入口。转换结果含错误时以非零状态退出。
- `test_adapter.gd`、`test_visual.gd`、`test_render.gd`：已有的转换和渲染检查脚本。

## 使用

项目已经启用插件。在底部面板选择 `res://ShareAsset/victorian/48px+MVMZ`，先扫描，再转换。48px 素材使用48px格子。默认输出目录为 `res://ShareAsset/victorian`，其中 `tilesets/Victorian.tres` 是地图使用的 TileSet，`atlases/` 保存重组图集。

命令行在仓库根目录执行，`godot` 替换为本机 Godot 4.6 Mono 可执行文件：

```text
godot --headless --path godot_csharp --script res://addons/rpg_asset_adapter/run_batch_convert.gd
```

批处理会覆盖对应的已有转换产物。仅制作地图时无需重复转换。

## 实际地图验证

`res://scenes/Parlor/Parlor.tscn` 使用已保存的 Victorian TileSet，参照过往分镜 `artifacts/theme-film/scenes/shot-03.png` 制作公共娱乐室。场景包含六个 TileMapLayer，可直接打开并按 F6 运行；地图不依赖生成脚本，不需要运行时拼装。

```text
godot --headless --path godot_csharp --script res://tools/build_parlor.gd
godot --path godot_csharp --rendering-method gl_compatibility --script res://tools/build_parlor.gd -- --capture
```

第一条重建场景并验证保存后重新加载的图块和地形邻接。第二条同时通过 Godot 实际渲染生成 `artifacts/parlor-validation/parlor.png`。结果写入同目录的 `validation.txt`。

本地图验证地面和地毯自动拼接、A4墙体、B/C家具切片、图层顺序及外部资源持久化。没有覆盖水面动画、角色遮挡或游戏通行规则。TileSet 的 `suggested_layer` 等自定义数据是提示；地图作者负责实际分层。素材中的普通家具按48px格子切片，多格家具需要整块摆放。
