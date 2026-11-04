# 放大镜 shader 来源

采用日期：2026-11-01。
作者：Serj。许可：MIT，完整许可见 LICENSE。
源码：https://github.com/gamedevserj/Godot-Shaders/blob/master/MagnifyingGlass/magnifying_glass.gdshader
许可：https://github.com/gamedevserj/Godot-Shaders/blob/master/LICENSE
下载源码 SHA-256：f1e4fa1ac7875e92835a6f70927b3143a9113e6dd01effa75af93cbf1900ccc6。

original.gdshader 保存采用时的原始代码，仅作来源存档。
正式材质位于 scenes/rendering/magnifier_lens.gdshader，由 scenes/book/DeskStage3D.gd 使用。
复用原作内外 smoothstep 圆环相乘的镜缘畸变算法。二维坐标改为真实镜片平面的归一化半径，采样中心投影自三维镜心。保留宿主已有的深度遮挡处理和单次透射合成。中央放大倍率为 1.65，镜缘畸变独立控制。降低镜面反射，避免反光遮住透射图像。
本实现属于三维适配，不是上游原样直接运行；屏幕采样仍不包含透明物体及画面外物体。
