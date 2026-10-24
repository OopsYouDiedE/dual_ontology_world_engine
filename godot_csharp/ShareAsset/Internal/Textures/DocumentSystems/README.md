# 《安眠者有福》文档纹理体系

打开 [index.html](index.html) 浏览全部纹理，点击样本可打开独立 SVG。

26 个材质单元在 `materials/`，9 个透明徽记在 `motifs/`，9 个透明平铺纹样在 `repeats/`，带说明的样板在 `boards/`。

适用时代、场景与受限颜色见 [制作设计说明](DESIGN-NOTES.md)。机器可读索引为 `manifest.json`。这是风格化 SVG 色彩图资源，不是经过标定的 PBR 材质。

可编辑生成源：`mcp/asset_pipeline/build_document_textures.py`。原始设计案未被修改。
