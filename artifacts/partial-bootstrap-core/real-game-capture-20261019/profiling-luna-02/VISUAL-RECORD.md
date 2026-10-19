# Visual record — profiling-luna-02

Reviewer: gpt-6-luna. Scope: visual inspection of PNGs listed in `run/frames.jsonl`. This is sampled observation of the visible game screen only; gaps between samples are unknown and this is not a frame-by-frame review.

## Sampled observations

- **18 ms** (`frame-000000018.png`): Title screen. Dark wooden tabletop, lit lamp at left, cup, and a dark green book with a fire-at-sea illustration. Menu text visibly includes “开始阅读”, “条例”, “凡例”, and “合卷”; some small characters are not entirely sharp. No game clock, map characters, dialogue, or portrait.
- **5,068 ms** (`frame-000005068.png`): Game clock **8:01**. Top label appears “安眠者有福”. Top-down dorm room with window, wardrobe, bookcase, bedside table and lamp, bed, rug, desk and chair. Pixel figure in dark brown clothing stands by the bed. Lower dialogue panel has a large portrait of a dark-haired young man in a brown coat, dark vest and white shirt; name “侦探”. Readable line: “我记得的最后一件事，是上船。”
- **10,152 ms** (`frame-000010152.png`): Clock **8:01**. Similar room/map view and same detective portrait/dialogue remain visible; a small interaction tag near the lower center reads “bed [E]”.
- **15,230 ms** (`frame-000015230.png`): Clock **8:03**. Same dialogue panel and detective portrait; map view is horizontally shifted to show corridor openings and room number markers. No new dialogue discernible.
- **20,212 ms** (`frame-000020212.png`): Clock **8:05**. Corridor with several pixel people: one slim dark-haired figure in dark clothes, a brown-haired figure in brown clothing, a stockier bald/light-haired figure in a pale shirt and dark trousers, and a short-haired figure in orange/brown clothing. Detective portrait remains. Readable line: “沿通路走到相连的房间。”
- **25,214 ms** (`frame-000025214.png`): Clock **8:06**. Corridor shifted; brown-haired pixel figure and stockier pale-shirt figure remain in view. Detective portrait and same readable line remain.
- **29,899 ms** (`frame-000029899.png`): Clock **8:07**. Wider room with a long green-topped table, papers/items and chairs; a red upholstered chair and floor lamp are visible. A dark-clothed pixel person at left and brown-haired figure near the pale-shirt figure at right. No dialogue panel; a small prompt says “shen [E]”.
- **34,920 ms** (`frame-000034920.png`): Clock **8:08**. Same room/table area; the brown-haired figure and pale-shirt figure are at right. Detective portrait returns. Readable dialogue: “第二行：一个小小的放大” (visible text ends there).
- **39,944 ms** (`frame-000039944.png`): Clock **8:09**. Same room and figures; detective portrait. Dialogue begins “一根铜管从铜口后面出来：一路沿北墙向西，穿墙进门厅，贴着墙下到隔离门的门框；另一路向东，钻进活动厅与…”. The final part is cut off in the displayed line and is not inferred.
- **44,955 ms** (`frame-000044955.png`): Clock **8:11**. Same room view. Portrait changes to a muscular, close-cropped man wearing an orange/red work shirt, dark suspenders, and white wraps on both forearms; name “霍铎”. Readable line: “…霍铎。钳工。”
- **48,977 ms** (`frame-000048977.png`): Clock **8:13**. Portrait changes to a dark-haired, bearded man in a white Chinese-style jacket with dark blue cuffs and a brown apron; name “沈鼎”. Readable line: “哟，又醒一个。进来坐，茶刚泡上。”
- **49,977 ms** (`frame-000049977.png`): Clock **8:14**. Same room and沈鼎 portrait. Readable line: “我姓沈，沈鼎，烧菜的。”
- **55,010 ms** (`frame-000055010.png`): Clock **8:16**. Same display and沈鼎 portrait. Readable line: “你呢？穿这么一身风衣，不像跑船的。”
- **60,030 ms** (`frame-000060030.png`): Clock **8:18**. Same room and沈鼎 portrait. Readable line: “都还记得啥？我就记得上船。下了跳板在后，脑子里白茫茫一片。” The wording “在后” is what the displayed glyphs appear to show; its intended phrasing is uncertain.
- **65,073 ms** (`frame-000065073.png`): Clock **8:34**. Same room layout and detective portrait again. Readable line: “侦探。……做侦探的。”
- **70,044 ms** (`frame-000070044.png`): Clock **8:37**. Map has shifted to a gray stone-floored side area with a small low bookcase, a dark upright fixture at the left edge, and a branching coat/hat stand or similar fixture; the detective pixel figure stands beside the threshold. Long table and chairs remain visible in the adjoining room. Detective portrait and readable line: “查看门内的插销和门外的横闩。”
- **74,933 ms** (`frame-000074933.png`): Clock **8:38**. Same visible area and detective portrait. Readable line: “门上的猫眼，玻璃外侧糊满烟灰，看出去一片黑。”
- **79,988 ms** (`frame-000079988.png`): Clock **8:39**. Same room and portrait. Readable line: “未行另号：急病者可经铜口呼叫转移。” The opening glyphs are difficult to distinguish; the rest is legible as shown.
- **85,000 ms** (`frame-000085000.png`): Clock **8:39**. Same room and portrait. Readable line: “门底边有一道擦痕。”
- **89,011 ms** (`frame-000089011.png`): Clock **8:40**. Same room and detective portrait. Readable line: “鞋尖朝着门侧的插门。” Final noun/compound after “门侧的” is hard to distinguish; recorded as visible, not corrected.
- **89,808 ms** (`frame-000089808.png`, last frame): Clock **8:40**. Same room, pixel figure, detective portrait and UI. Dialogue has advanced to “鞋跟一端被什么东西压过。”

## Coverage and limits

The samples above were selected near 0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, and 85 seconds, plus frames near 89 seconds and the last frame. Changes between inspected PNGs are not known. The game clock values above are read from the visible top-right corner of each inspected screenshot. No hidden state, plot purpose, or outcome is inferred.
