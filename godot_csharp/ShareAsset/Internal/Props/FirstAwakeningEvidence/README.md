# 初醒证物检视图片

22张内置 image_gen 生成的物件特写，保留生成分辨率，仅用于UI检视和答辩物证。不是原生地图像素素材。

源图及逐图提示词：`AssetsSource/Internal/Props/FirstAwakeningEvidence/README.md`。运行时由 `scenes/FirstAwakening/EvidenceDiagram.gd` 按kind读取同名PNG，居中等比显示，下方描述由场景剧情维护。

重建：Godot `--headless --path godot_csharp --script res://tools/build_first_awakening_evidence.gd`，再执行编辑器导入。生产链仅原样复制已入库源图，不访问生成工具缓存，不重绘或缩放。
