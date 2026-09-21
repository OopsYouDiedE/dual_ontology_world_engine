# 厨房局部缩图试样

按用户最新要求，允许将之前绘制的原图缩小到 48px 整数倍的素材框。本试样从 shot02/source/kitchen_generated.png 提取，等比例 Lanczos 缩小一次，透明留白补齐尺寸，不重新绘制造型。

- 壁炉：576×288，12×6 图块。
- 料理台：480×192，10×4 图块。
- 储物柜：144×288，3×6 图块。

KitchenDetail48.tres 使用 48×48 切片。场景 res://scenes/Resized48Detail.tscn 使用单位缩放的 TileMapLayer。实际渲染见 artifacts/resized48-detail/detail.png。

这是缩图适配素材，不是原生 48px 绘制素材。当前仅用于局部质量检查，没有替换完整镜头。重建脚本：res://tools/build_resized48_detail.gd。
