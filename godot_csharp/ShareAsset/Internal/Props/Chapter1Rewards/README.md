# 第一章附加家具运行时资产

生产源与重建入口：`AssetsSource/Internal/Props/Chapter1Rewards/build.py`；规格及来源见同族生产源 README。`manifest.json` 记录尺寸、脚底锚点、源路径与 SHA-256，运行时 PNG 与源图逐字节一致。

`reward_locker.png` 为 80×32，`cylinder_player.png` 为 32×40，均按 1×原生像素渲染，使用 32×32 地图网格。第一章地图构建器将二者装配在 `(500,120)`、`(560,122)`；精灵在 `ObjectsAndPeople` 中按脚底 Y 排序，碰撞分别为 `(462,102,76,16)`、`(548,108,24,12)`，导航使用相同矩形。柜前 `(500,132)` 留作可行走位置。柜顶位于 Y=88，与铜口底部 Y=84 留有间隙。

玩家可见名称与心声读取剧本词汇表，不在资产代码中编写。重建后通过 Godot 导入 PNG。
