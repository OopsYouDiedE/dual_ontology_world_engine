# 未发生之事：脑内推演

网页入口为 `dist/mind.html`，本地预览地址为 `http://127.0.0.1:4173/mind.html`。只新增了 `game_resource` 内的场景与素材，书本和雪地场景保留。

## 操作

右侧字典默认闭合。悬停时轻微移动，点击打开。封面图案为方框内菱形。点击 `sina`、`o` 和一个效果词，组成言灵；点击已填的槽位可以移除词语。点击“施放言灵”，或选词后按 Enter 施放。

左侧点击三个固定记忆碎片，依次组成“薇洛处于潮湿／接触火焰言灵／火转蒸汽，不点燃”，再点击“写入推演”。规则写入后可以暂时移除，以对比同一句话的结果。

建议先施放火焰，再写入规则，依次施放水和火。观察薇洛的状态、人物效果和下方推演回响。还原心象会保留当前规则与词语组合；重新推演清空整个实验。

## 固定规则与言灵

规则只匹配“当前状态为潮湿，随后收到火焰言灵”。匹配时消耗潮湿、生成蒸汽，目标回到平静且不会燃烧。不满足前提时，火焰直接生效。

本演示将状态简化为平静、潮湿、燃烧、寒冷。水和冷各自覆盖之前的状态。未写入规则时，火也直接覆盖潮湿；这用于表现威尔逊亲自设定脑内规律的效果，不是现实水火物理模拟。

言灵采用道本语的游戏简式：`sina o seli` 为火，`sina o telo` 为水，`sina o lete` 为冷。词汇采用基础词义，具体作用、状态覆盖和施法模板是本场景约定。词汇来源参见 [Toki Pona 官方站点](https://www.tokipona.org/index.html)与 [Toki Pona 词表及原始词典出处](https://en.wiktionary.org/wiki/Appendix:Toki_Pona)。

## 素材与生成

复用 `dist/assets/wilson-sprite.png`。薇洛是此次新绘的角色，并非上一场景的温蒂。

新增背景 `dist/assets/mind-ground.png`：1672 × 941，RGB。新增 `dist/assets/willow-mind-sprite.png`：2172 × 724，RGBA，四列一行，透明底，分别是平静、惊讶、潮湿、寒冷。网页直接按格裁切，并按透明区域校正脚底位置。

两张素材使用内置 image_gen 生成，每张一次。背景保留了地毯、营地、装置和草地，只加入弯月、悬浮小石块与边缘雾气。程序绘制言灵轨迹、状态粒子、蒸汽和微弱的梦境光点。

背景最终提示词：

```text
Use case: stylized-concept
Asset type: 16:9 wide game background, mind-ground.png.
Primary request: A fixed elevated top-down nighttime camp in the hand-drawn ink-line paper-cut art style of Don't Starve, recalling a camp inside Wilson's memories.
Scene/backdrop: Dark moss-green grass, wooden plank flooring around the edges, a low ruined stone wall at upper left, an eerie crooked wooden hut silhouette at upper right, a few old-fashioned alchemy devices in the lower-left and lower-right margins. A large open playable clearing dominates the center with one worn dark-gray square rug.
Dream details: Only two small floating stone fragments near the top and one subtly impossible bent crescent moon; faint mist around edges. The dream stays recognizably a grounded camp.
Style/medium: Scratchy black hand-drawn outlines, cross-hatched shading, weathered paper-cut game scenery, coordinated with Don't Starve Wilson.
Composition/framing: Wide 16:9, fixed elevated top-down game camera, objects small and confined mainly to edges, central area spacious and empty for gameplay.
Lighting/mood: Night, center visibly brighter and readable while edges fall into dark fog; eerie quiet memory.
Color palette: Muted gray-green, moss green, subdued teal, sparse aged gold accents.
Constraints: No people, no characters, no creatures, no text, no UI, no watermark, no decorative border. No outer space, no cosmic background, no neon, no magic circles.
```

薇洛最终提示词：

```text
Use case: stylized-concept
Asset type: willow-mind-sprite.png, game character sprite strip.
Primary request: One horizontal four-column, one-row sprite strip with exactly four full-body poses of Willow from Don't Starve. GENUINELY TRANSPARENT BACKGROUND with alpha, clean cutout art, no colored backdrop and no fake checkerboard.
Subject: Willow, pale face, black twin ponytails, red blouse, black skirt and black boots. Match the scratchy hand-drawn black ink-line, paper-cut visual style and proportions of Don't Starve's Wilson game sprites.
Composition/framing: Four equal-width invisible cells across a wide canvas. Each pose fully contained in its own quarter; all characters the same scale and height with feet aligned to exactly the same baseline; all face diagonally toward the viewer's left. Generous transparent padding between characters and around canvas edges, no body clipping or overlapping cells.
Poses left to right: 1 calm neutral standing, 2 surprised with both hands raised, 3 soaked with damp drooping hair, shoulders hunched and head lowered, 4 cold with arms crossed tightly hugging herself.
Constraints: Exactly four Willow figures, no other characters. Consistent clothing and identity in every frame. No props, no text, no frame lines, no grid lines, no labels, no UI, no watermark, no visible ground or cast ground shadows. Real transparent alpha background is essential.
```

## 验证

已检查 JavaScript 语法、页面素材引用、薇洛图片的透明通道和本地 HTTP 入口。独立状态检查覆盖普通火焰、无规则时潮湿遇火、规则命中、前提不成立、寒霜、移除规则及无效输入。未进行浏览器截图测试。
