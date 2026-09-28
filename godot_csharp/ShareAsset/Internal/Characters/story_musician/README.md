# 音乐家 · 第一剧情打样动作

`musician.tres`：3向idle各1帧、3向walk各4帧、front/left talk各3帧，共21帧。右向镜像左向。`special.tres`独立保存4帧非循环 `listen_left`（停步侧耳），不混入通用动作集。

64×64透明承载格，普通人形约43px高，脚底锚点(32,55)，Godot居中AnimatedSprite2D偏移(0,-23)。按同一动作/方向基准等比例整理，按鞋底中点注册，不逐帧拉成相同外框；alpha阈值192后二值化。未扩大人物地面占位。

由内置imagegen生成源稿，再用 `godot_csharp/tools/build_musician.ps1` 最近邻切片整理；不是原生逐像素手绘。原始源稿和提示词全文在 `godot_csharp/AssetsSource/Internal/Characters/StoryMusician/`。主图集256×512；特殊动作256×64；预览图为4倍显示。

重建：在仓库根目录运行 `godot_csharp/tools/build_musician.ps1`，随后在Godot重导入。运行时只引用图集及SpriteFrames，不引用预览或源稿。

本段使用待机、侧向说话与侧耳，暂无持物动作。前/背面walk的帧间步幅较弱，属于待进一步改进的扩展样张，不宣称通用行走已完成美术验收。其他职业角色尚未按本格式扩产。
