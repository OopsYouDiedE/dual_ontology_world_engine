# E 室外保留数据提取

`extract.py` 是 E 区域资产构建的内部步骤，不启动游戏、AI 或旧美术生成器。它读取正式地图 TSCN 的 `Objects` 和 `Solids`、原纹理实际尺寸及已有建筑高度参数，生成 E/areas/outdoors-data.json 和静态资源闭包清单。

使用 `python mcp/art_style_trials/e_outdoors/extract.py`。输出仅覆盖这两个本工具拥有的文件。碰撞脚线没有明确对应的建筑保留原图，不猜测三维足迹。运行集成与未恢复源文件的限制见 `godot_csharp/scenes/ArtStyleTrials/E/areas/outdoors-README.md`。
