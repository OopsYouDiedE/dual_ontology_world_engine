# 第一章附加家具生产源

原生整数像素配方为本目录 `build.py`，复用 `tools/first_awakening_art/` 的胡桃木、黄铜色板、左上方光照和轮廓处理。透明 PNG 保存在本目录，逐字节发布到 `ShareAsset/Internal/Props/Chapter1Rewards/`；不缩放、不抗锯齿、不量化。地图格为 32×32，物件可以横跨多格。

| 文件 | 原生尺寸 | 脚底锚点 | 用途 |
| --- | --- | --- | --- |
| `reward_locker.png` | 80×32 | (40,32) | 第一章活动厅北墙的矮柜，四扇独立小门和纸签 |
| `cylinder_player.png` | 32×40 | (16,40) | 矮柜右侧小支架上的手摇装置 |

从仓库根目录执行 `python godot_csharp/AssetsSource/Internal/Props/Chapter1Rewards/build.py`，再执行 `python godot_csharp/tools/build_awakening_map.py` 和 Godot 资源导入。构建地图的家具表统一生成精灵、脚底碰撞和导航足迹，不使用整张画布作碰撞。名签只画抽象墨痕；悬停名与点击心声来自 `.story`。
