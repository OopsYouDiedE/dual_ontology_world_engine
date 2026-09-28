# 已采用的动作图集母版

`thief.png` 是《安眠者有福》当前采用的盗贼运行时图集母版。它从迁移前的 `ShareAsset/Images/characters/animation_v2/thief.png` 原样收录，尺寸为 512×1536，每格 64×64；母版文件与当前运行时图集逐字节一致。

`../thief.png` 是更早的原始生图草稿。它与已采用图集存在 65,502 个像素差异，不能声称重建结果与该草稿一致，也不能用该草稿覆盖已采用母版。

`godot_csharp/tools/build_demo_animation_atlas.ps1 -Actor thief` 会优先读取本目录中的 `thief.png`，原样写回运行时 PNG，并重新生成标准 SpriteFrames 资源。更新母版前应先确认新的美术结果并同步审查重建产物。
