# 第一章迁移来源

本目录保留旧剧本与翻译等迁移来源；独立Awakening场景已经退役。正式入口为 `scenes/Host/Main.tscn`，使用 `scenes/Chapter2/Field/CampusMapView.tscn` 和原生DV生产会话。

地图配方由 `tools/chapter2_art/rooms.py` 维护，现役地图生成入口为 `tools/build_chapter2_scenes.py`。共享家具由 `tools/first_awakening_art/` 维护；资源前缀中的Awakening不表示仍有独立旧游戏场景。

旧场景重写脚本已清理；旧流程回归、旧菜单机制及旧截图脚本不再作为执行要求。当前输入、现场回执和验收要求见 [Godot维护说明](../../AGENTS.md) 及 [仓库README](../../../README.md)。历史实现与报告从Git及原有artifacts查阅。
