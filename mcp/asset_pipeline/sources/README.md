# 原始资产与制作资料

本目录承接原 `godot_csharp/AssetsSource/` 的原始文件、可编辑模型、制作参数、来源记录和许可资料。目录迁移保留这些文件；成品继续写入 `godot_csharp/ShareAsset/`。

`mcp/asset_pipeline/` 的生成器按仓库位置解析本目录，RPG 适配器使用同一来源根目录。`mcp/godot_workspace.py` 创建的工具项目把这里映射为 `res://AssetsSource/`，供原生 Godot 导入工具读取。正式游戏通过 `res://ShareAsset/` 使用发布资源。

音乐的原谱、编配源码、专用采样库和来源资料集中在 `mcp/music_pipeline/`。各美术子目录的 README 记录对应生成器、可编辑源、成品关系与来源，不在迁移过程中重写原始素材。
