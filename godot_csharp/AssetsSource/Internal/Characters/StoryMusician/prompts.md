# 音乐家动作源稿记录

参考图：`godot_csharp/ShareAsset/Internal/Characters/front-standing-preview.png` 第 08 人（Musician），仅用于身份与像素风格对照。最终取向为银灰齐颌短发、黑灰高领短上衣、窄裤；不加耳机和常驻器具。使用内置 image_gen 生成源稿，随后以最近邻缩放、透明裁切与规则分格整理；结果是生成后整理的游戏资源，不是原生逐像素手绘。

源图对应：

- `godot_csharp/AssetsSource/Internal/Characters/StoryMusician/idle-talk-source.png`：待机的前/后/左向和不同人物视图。提示词要求透明背景、统一角色、约 1900 年、银灰短发和黑色高领衣裤。
- `godot_csharp/AssetsSource/Internal/Characters/StoryMusician/walk-source.png`：前、后、左向各四帧步行。提示词要求同一角色、各帧交替迈步、统一比例与脚底。
- `godot_csharp/AssetsSource/Internal/Characters/StoryMusician/talk-source.png`：前向与左向各三帧克制说话表情/手势。
- `godot_csharp/AssetsSource/Internal/Characters/StoryMusician/listen-source.png`：左向停步侧耳倾听四帧，最后阶段抬手靠耳。

生成器会调整请求的图格排布，故 `godot_csharp/tools/build_musician.ps1` 对源稿按实际可见行列裁切并整理。所有目标帧归一为 64×64，角色可见高度 43px，脚底落在 y=54，中心 x=32；采用最近邻采样。重建命令：在 PowerShell 运行 `godot_csharp/tools/build_musician.ps1`。

## 完整生成提示词

**idle/talk/姿态基稿**

```text
Pixel art animation sprite sheet, transparent background, exact same musician as character 08 in reference image, chin-length silver gray hair, black high-neck top and narrow pants, circa 1900, no headphones or props. 9 rows of frames: idle front/back/left one each; walk front/back/left four each; talk front/left three each; listen left four frames stopped cupping ear. Crisp consistent hard-edged pixels; each logical cell 64x64 with figure 43px high, centered and baseline y55, no text labels.
```

**listen与其余方向姿态基稿**

```text
Using the generated musician sheet just shown as exact character identity reference, create an animation-only sprite sheet with transparent background. 4 rows x 4 columns, exactly four frames in each row: row 1 front-facing walk cycle, row 2 back-facing walk cycle, row 3 left-facing walk cycle, row 4 left-facing attentive listening gesture cycle. Same silver chin-length hair and dark high-neck outfit, 1900 period, no headphones, no instrument/prop. Distinct readable alternating leg and arm poses across walking frames. Listening frames stop walking and progressively tilt head toward viewer's left and cup one ear with hand. All frames same scale and baseline, character height around 43px in logical 64x64 cell with feet at y=55, center x=32. Hard-edged retro pixel art with consistent shading, transparent background, no captions, no grid lines, no cell borders. The sheet should contain precisely 16 isolated sprite poses arranged in four equal rows and columns, with clear uniform gutter spacing.
```

**独立步行动作稿**

```text
Use the just-shown musician sprites as the exact character design reference. Generate a transparent pixel-art sprite sheet containing exactly 12 isolated sprites in a 3-column by 4-row grid. All 12 are walking poses, no speaking or listening. Row 1: front-facing 4-frame walk cycle. Row 2: back-facing 4-frame walk cycle. Row 3: left-facing profile 4-frame walk cycle. (If 4 rows are required for layout, keep only 3 rows with generous empty transparent lower margin; do not duplicate poses.) Silver-gray chin-length hair, black/charcoal high-neck cropped jacket and narrow trousers, no headphones, no accessories, no instrument, no text. Show clear leg alternation and subtle counter-swinging arms, with frame 1 and 3 opposite and frames 2 and 4 opposite. Match reference's restrained soft retro pixel shading and proportions. Characters about 43 pixels tall in logical 64x64, same baseline and scale.
```

**对话动作稿**

```text
Using the musician sprites just shown as exact identity reference, generate a transparent pixel-art sprite sheet exactly 6 sprites in 3 columns x 2 rows. First row contains three front-facing talking animation poses, second row contains three left-facing talking poses. Each pose differs subtly in mouth shape, eye focus or a small restrained hand gesture, suitable for narrative dialogue. Silver-gray chin-length hair, dark charcoal high-neck jacket and narrow trousers, no headphones or props. Same standing body silhouette, same scale, same baseline and 43px height inside 64x64 logical frames. Crisp soft retro pixel art, transparent background, no labels or borders. If the generator chooses another ratio, still clearly separate all 6 poses.
```
