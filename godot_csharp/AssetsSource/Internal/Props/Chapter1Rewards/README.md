# 第一章附加家具生产源

原生整数像素配方为本目录 `build.py`，复用 `tools/first_awakening_art/` 的胡桃木、黄铜色板、左上方光照和轮廓处理。透明 PNG 保存在本目录，逐字节发布到 `ShareAsset/Internal/Props/Chapter1Rewards/`；不缩放、不抗锯齿、不量化。地图格为 32×32，物件可以横跨多格。

| 文件 | 原生尺寸 | 脚底锚点 | 用途 |
| --- | --- | --- | --- |
| `reward_locker.png` | 80×32 | (40,32) | 第一章活动厅北墙的矮柜，四扇独立小门和纸签 |
| `cylinder_player.png` | 32×40 | (16,40) | 矮柜右侧小支架上的手摇装置 |

从仓库根目录执行 `python godot_csharp/AssetsSource/Internal/Props/Chapter1Rewards/build.py` 生成并发布该资源族。现役地图由 `tools/chapter2_art/rooms.py` 与 `tools/build_chapter2_scenes.py` 维护；该资源族的存在不自动构成生产接线。脚底碰撞由现役家具表或原生DV的几何声明提供，图像不代替互动资格与内容来源。
