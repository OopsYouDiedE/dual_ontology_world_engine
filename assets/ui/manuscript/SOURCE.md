# 平面手稿资产来源

生成工具：内置 image_gen。用途：现有真实三维书页上的独立无字图案，不作为整幅书案背景。
生成日期：2026-11-01。输入为文字提示，未使用外部图片。

提示词共同约束：flat orthographic front view; no text, no page, no desk, no book, no curvature or perspective distortion; no cast shadows or baked highlights. Transparent background for border/emblem/manicule.

miniature：火山、学园主楼、跨谷廊桥、河流与小帆船的细密画。生成结果自带细框，采用其边框限定金箔区域。
border：L形常春藤与莨苕叶、群青朱砂花叶、少量金叶、两只鸟和小兔，中央透明。
emblem：蓝底方框、双环浑天仪、星形、衔钥匙渡鸦，无文字。
manicule：右指手印、群青袖口、少量金色点饰；显示尺寸64×32，作为独立动态节点。

素材用作装饰，不声明新的世界规则或建筑事实。颜色和材质覆盖分别渲染；特殊手绘图案使用生图是为了提供现有程序材质不能替代的细密画和笔触。

SHA-256：
miniature.png 48416a65be97a94618bf6462e55ae1a6da674ee936a7309287695df3469e5da9
border.png 1ab999f8fb7819b7df41d253a7c56945286e5c7eba8bc27b95997ce8d1d4e768
emblem.png 894ee843aa35e20d81e4593640e6bfb95e7e62155ff5c726b060953bda25b173
manicule.png f59059423a0edcf705fb8bc49c74ff68ddcc7ea80d5ab493325ef76340188eb3

## 书法花体 B 修订

initial-b-script.png 使用内置 image_gen 绘制，2026-11-01。此前概念图 exec-741c03f8-a38a-475e-8eb7-d48614148155.png 中的蓝底装饰衬线 B 已查到；按用户纠正，提取稿 exec-3bca244e-4127-4c97-9159-d92adb1adc40.png 未采用。最终采用实际书法花体结构的新绘稿，未复制网上字形或导入外部字体。搜索参考关键词 ornamental penmanship capital B script flourishing；参考的是斜向连续主干、双碗和回旋笔迹的字形类别。

最终提示约束：standalone genuinely flourished English calligraphic script capital B; Spencerian ornamental penmanship / English roundhand script morphology; diagonal flowing curved main stem, shaded downstroke, hairline ascender oval, two asymmetrical flowing bowls, long entry and exit swashes; gold ochre painted leaf and ultramarine accents; no Roman serif, no blackletter, no rectangular border, no flowers hiding a conventional letter; flat orthographic transparent decal, no cast shadow or baked directional lighting.

特殊图像用于手绘花体轮廓与笔触，曲面仍由真实网格负责。独立材料覆盖通道区分金箔和颜料：沿用 manuscript_mask.gdshader 输出金箔 R 与颜料 B；manuscript_surface.gdshaderinc 计算凸边与锤纹法向，实体纸页着色器根据覆盖计算金箔粗糙度 0.22±0.035、颜料 0.78±0.04、金属度最高0.88。它们为实时程序化 PBR 输出，不是声称图像包含实测法向贴图。

SHA-256 initial-b-script.png e5f466915ec971018ff55788d94da9b9aaeb4b16e11659a36f3846e18af951b2
