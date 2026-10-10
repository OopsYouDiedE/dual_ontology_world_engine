# UIPaper

界面纸面的像素纸纹（设计规范第六部分 §4）。由 `tools/build_ui_paper.py` 生成：128×128 原生像素可平铺，按最近邻放大 2 倍，种子固定，可重建。

- `paper_pixel.png`：纸面（弹窗、抬头、对白条、书签贴片）。
- `paper_ruled.png`：册子横格页，同一纸纹每 32 px（一个地图格）一道淡横格。
- `paper_plain.png`：设置里关掉“纸面纹理”时替换用的纯纸色。

用于 `scenes/UI/Styles/*.tres` 里的纸面样式盒（StyleBoxTexture 平铺）。
