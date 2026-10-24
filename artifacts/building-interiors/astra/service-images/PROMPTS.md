# 后勤与村落室内候选效果图

日期：2026-10-24。执行：内置 image_gen，12次独立生成，逐张实际审看；未使用平面作为先验图、未画平面、未运行游戏测试/构建。来源为主代理提供的工程边界；未读取项目设计文件。本目录不修改正式设计或游戏。

所有图均为人眼约1.6m候选透视。尺寸、设备数、结构和容量不由透视证明。住宅/村房下端垫空，连续梁与楼板，架空标高继承外部模型；图未展示基础，不能据此包围房下或填入储藏/地下住房。用色是视觉审看，未声称精确像素调色验收。

## 覆盖及复用表

|图|实例覆盖|候选外轮廓/层高|
|---|---|---|
|01|独立供应厅|38×18m / 4.2m|
|02|独立洗衣修补|30×16m / 4.2m|
|03|独立燃料设备|26×14m / 4.2m|
|04|独立冷干库|28×16m / 4.2m|
|05|杂役住宅16栋同型|11×8m / 3.2m|
|06|东西共用房各1栋|10×12m / 3.6m|
|07|杂役洗储房|18×10m / 3.6m|
|08|村住宅25栋同型|11×9m / 3.2m|
|09|村聚集房A/B|11×14m / 3.5m|
|10|渔具工坊A/B|12×12m / 3.5m|
|11|村仓A/B|9×12m / 3.5m|
|12|东坡温室/苗床候选|无已定外轮廓和高度|

## 公共提示词（每次完整前置）

Use case: historical-scene. Architectural interior concept for a fictional 1900s royal overseas academy. Photorealistic material-rich architectural visualization, realistic human scale, eye level 1.6m, wide rectilinear lens, single-story ground-level interior, no people, no floor plan, no axonometric. Practical clean stone, wood, wrought iron and restrained brass; daylight and warm lamps #F2D091. No purple, phosphorescent cold gold, pure black #0E0C10 or demonic gold #D9A13A; no modern screens, science-fiction machines, luxury lounge styling, text, logos or watermark. A candidate spatial study, not finalized architecture. 

## 每图提示词、来源与取舍

### 01-supply-hall

图片：[01-supply-hall.png](01-supply-hall.png)

原始来源：`C:\Users\iii\.codex\generated_images\01a16d06-d277-7a31-97d8-a3ce1cd5f022\exec-bf2e7f23-f83d-441a-af18-912028e3f53e.png`

实际追加提示词：

One wide eye-level view of a large institutional supply and catering hall in a candidate 38x18m envelope, 4.2m story. Capacity study supports 16 students plus 64 service workers plus 16 visitors, not just 16 diners. Show an extensive clean food preparation room with several heavy wooden worktables, period range cooking along one wall, copper pots, a clearly distinct service counter toward a separate serving room through a broad opening, rear receiving door. Wide unobstructed trolley paths. Dishwashing in a separated wet scullery visible through a side doorway. No fuel store inside the food room; no cold store substitutes.

审看：采用：大规模备料台、连续热厨、服务口、独立洗碗门洞和后端收货路径。舍弃：侧厅盛大桌饰、花束及装饰铁花不作为必要设计；图不验证96人供餐设备吞吐量。

### 02-laundry-repair

图片：[02-laundry-repair.png](02-laundry-repair.png)

原始来源：`C:\Users\iii\.codex\generated_images\01a16d06-d277-7a31-97d8-a3ce1cd5f022\exec-bdb884d8-c737-4854-b971-ab4ebc5c3483.png`

实际追加提示词：

A two-panel architectural eye-level diptych of one laundry and linen repair building, candidate 30x16m, 4.2m story. LEFT: tiled wet laundry with stone wash troughs, copper wash boiler with flue, hand wringer, drainage and ventilating windows, wide clear circulation. RIGHT: separate DRY linen repair and ironing room behind a full-height dividing wall and door, timber tables, treadle sewing machine, linen shelves and simple ironing tools. The two views depict physically separated rooms; no food, fuel or archive storage.

审看：采用：湿洗槽/排水与独立干缝补熨烫室；窗侧作业采光。舍弃：湿区远端出现的干布草架不作为长期干储，干布草统一移入干室；机器精细管路不是工程图。

### 03-fuel-equipment

图片：[03-fuel-equipment.png](03-fuel-equipment.png)

原始来源：`C:\Users\iii\.codex\generated_images\01a16d06-d277-7a31-97d8-a3ce1cd5f022\exec-38ea6712-f1a1-45b3-abb1-286c01ba2c95.png`

实际追加提示词：

A two-panel eye-level interior diptych of a practical fuel and equipment building, candidate 26x14m, 4.2m story. LEFT: ventilated masonry fuel room with orderly coal bins and timber fuel racks, clear central cart aisle, no ignition sources. RIGHT: a physically separate equipment repair room beyond a full-height stone partition with closable door, workbench, hand tools, small period forge with proper masonry flue and organized iron equipment. Each room separately ventilated, fire division visually explicit. No food, clothing, housing, archives, modern machines.

审看：采用：燃料存储与设备维修热作分室，石隔墙和宽通路。舍弃：燃料间明火式壁灯，须换安全封闭照明，禁止依图添加火源；图不证明防火构造和通风达标。

### 04-cold-dry-stores

图片：[04-cold-dry-stores.png](04-cold-dry-stores.png)

原始来源：`C:\Users\iii\.codex\generated_images\01a16d06-d277-7a31-97d8-a3ce1cd5f022\exec-0b6dee47-1f51-484d-a9ba-9d2c1c240302.png`

实际追加提示词：

Two-panel eye-level architectural interior diptych of candidate 28x16m cold and dry stores building with 4.2m story. LEFT: cool masonry food store with thick walls, ice chest, stone shelving and food jars, separate door to vestibule; no modern refrigerators. RIGHT: dry, well-ventilated pantry with raised wooden grain bins, flour sacks and shelves, clear circulation; physically divided from cool room by full-height wall and closable door. No laundry, fuel, forge or paper archives; food safety spatial distinction visible.

审看：采用：冷食品和干粮全高分室、干粮抬离地面及通路。舍弃：冷库外门台阶可能暗示下沉，后续保持单层地面标高，不建立地下室；冰槽形状不证明制冷能力。

### 05-service-residence

图片：[05-service-residence.png](05-service-residence.png)

原始来源：`C:\Users\iii\.codex\generated_images\01a16d06-d277-7a31-97d8-a3ce1cd5f022\exec-fbd65c11-6bc1-42a9-a036-31f8dc0e8405.png`

实际追加提示词：

Two-panel eye-level interior diptych of a comfortable dignified compact four-capacity service-worker home. Envelope candidate 11x8m, story 3.2m. LEFT: simple sunny shared sitting/dining room with four wooden chairs, dining table, compact sideboard, hooks, unobstructed passage to separate bedroom doors. RIGHT: one of TWO separate twin bedrooms with TWO individual beds, bedside shelves, personal storage, window, closable door, generous clear route between beds. Total candidate home has two twin rooms, not an open barracks. Raised ground-level dwelling with continuous timber floor, no basement, no space below depicted. Plain warm domestic quality, no luxury, no people or invented family.

审看：采用：共同生活区、两间双床房的候选，个人储物、真实床间通行。未画第二间卧室独立透视，允许同型映射；四人是容量，不是已定人口。墙面装饰不成为人物/剧情事实。

### 06-service-common

图片：[06-service-common.png](06-service-common.png)

原始来源：`C:\Users\iii\.codex\generated_images\01a16d06-d277-7a31-97d8-a3ce1cd5f022\exec-c9ad40c7-9e3b-4dfd-9fd7-f6d301ed875a.png`

实际追加提示词：

One eye-level view of a comfortable service-workers' common room candidate 10x12m, 3.6m story, shared building reusable for east and west instances. Plain timber dining and reading tables, chairs and benches with cushions, modest stove with flue, small tea counter and storage cabinet, large windows, clear central passage and visible entry vestibule. Dignified but modest, not luxury salon. Standalone raised ground-level building, continuous floor, no basement or lower storage depicted.

审看：采用：中央通路、桌凳两侧、窗边阅读和小茶水台。舍弃：外部柱廊、过高窗拱和庄园背景不作模型依据；高度服从3.6m候选层高。

### 07-service-wash-store

图片：[07-service-wash-store.png](07-service-wash-store.png)

原始来源：`C:\Users\iii\.codex\generated_images\01a16d06-d277-7a31-97d8-a3ce1cd5f022\exec-9961a9c4-25fa-4ece-bd57-c373e80f24cb.png`

实际追加提示词：

Two-panel eye-level diptych of a service settlement wash-and-storage building candidate 18x10m, 3.6m story. LEFT: common washroom with period ceramic washbasins, stone wet floor and private washing cubicles with full-height doors, open clear approach. RIGHT: separate dry linen/household storage room with wooden shelves, folded cloth and baskets, small sorting table, continuous timber floor. Full-height partitions, separate doors, wet and dry isolation. Standalone single raised ground floor, no basement or lower enclosed space.

审看：采用：公用洗面与私密洗浴隔间、独立干布草储藏。舍弃：不按透视直接确定洁具数、柜高和走道宽，右侧高架须实际设计可达性。卫生供排水尚未设计验证。

### 08-village-residence

图片：[08-village-residence.png](08-village-residence.png)

原始来源：`C:\Users\iii\.codex\generated_images\01a16d06-d277-7a31-97d8-a3ce1cd5f022\exec-bee7349a-94a3-4d74-b8dd-c0be5439e89a.png`

实际追加提示词：

Two-panel eye-level interior diptych of generic village dwelling, candidate 11x9m single story 3.2m, comfortable four-capacity module, not an assertion of family population. LEFT: common living/dining room with four chairs at wooden table, compact masonry cooking hearth with flue in a distinct cooking alcove, dish shelves, bright window, doorways to private rooms; clear safe circulation. RIGHT: one of two separate twin bedrooms with two single wooden beds, individual storage and sunny window, closable door. Timber floors continuous, house is raised above ground but foundation not shown, no basement, no lower store. Modest local stone and wood, no characters.

审看：采用：共同生活/炊事凹室、两间双床候选、各自储物。只显示一间双床详图，另一间同型映射；不新增家庭人物。炉灶与木地板的热隔离需在工程阶段明确。

### 09-village-gathering

图片：[09-village-gathering.png](09-village-gathering.png)

原始来源：`C:\Users\iii\.codex\generated_images\01a16d06-d277-7a31-97d8-a3ce1cd5f022\exec-805e73bc-01e0-4977-8924-40376640cf7c.png`

实际追加提示词：

Eye-level interior of a modest village public gathering hall, reusable A/B type, candidate 11x14m single story 3.5m. Simple wood trusses and whitewashed local stone, flexible arrangement of plain tables and benches to either side of a broad clear central aisle, modest cupboard with cups and pitchers, coat hooks near entrance, tall windows, no stage, no theater, no entertainment equipment, no family portraits. The floor is one continuous level above open undercroft not visible here; no basement or enclosed storage beneath. Welcoming lived-in dignity, not aristocratic salon.

审看：采用：两侧可移动桌凳、中央贯通通路、门旁衣帽和器皿柜。舍弃：图外多层石屋背景不决定村落外观，室内窗高不反推层高；不新增舞台或娱乐设施。

### 10-fishing-workshop

图片：[10-fishing-workshop.png](10-fishing-workshop.png)

原始来源：`C:\Users\iii\.codex\generated_images\01a16d06-d277-7a31-97d8-a3ce1cd5f022\exec-37f4d8da-3f91-45fc-b6b0-7d91fedcb9af.png`

实际追加提示词：

Eye-level interior of a village fishing-gear workshop, reusable A/B type, candidate 12x12m single story 3.5m. Local stone walls, timber beams, large bright windows and wide double doorway. Clear central working aisle, heavy benches for net mending, orderly rope coils and fishing nets on wall racks, wooden floats, baskets, small hand tools in cabinets. Net hanging area separated spatially from repair bench, no forge, no fires, no fish food store, no boats blocking circulation. Continuous solid timber floor over open undercroft not visible; never basement, no storage below the dwelling.

审看：采用：修网工作台、挂网杆、绳索/浮子柜和中央搬运通道。舍弃：门外临海风景不确定建筑朝向/落位；图不核验挂网和灯具安全间距。

### 11-village-store

图片：[11-village-store.png](11-village-store.png)

原始来源：`C:\Users\iii\.codex\generated_images\01a16d06-d277-7a31-97d8-a3ce1cd5f022\exec-77f971d2-5d5c-4e91-8b66-57108e92b9bd.png`

实际追加提示词：

Eye-level interior of a generic village dry store, reusable A/B type, candidate 9x12m single story 3.5m. Local stone walls, timber beams, ventilating high windows, wooden shelves and raised grain bins, orderly sacks and closed wooden crates, small weighing and receiving table near broad double doorway, wide central cart aisle. No forge, no fuel, no laundry, no beds, no modern machines. Above-ground building with continuous floor and open foundation space outside frame; no cellar, no descending stairs or lower storage. Practical clean restrained interior.

审看：采用：中央车行/人行通路、侧向粮箱货架、近门称量收货台。舍弃：过高料仓与梯子不直接照搬，按3.5m层高重新落位；明火式灯具须替换安全封闭照明。

### 12-greenhouse-candidate

图片：[12-greenhouse-candidate.png](12-greenhouse-candidate.png)

原始来源：`C:\Users\iii\.codex\generated_images\01a16d06-d277-7a31-97d8-a3ce1cd5f022\exec-75c2069f-e579-461e-a5dd-8d63a8051ae3.png`

实际追加提示词：

Eye-level interior candidate of a single-story east-slope greenhouse and seedling nursery belonging to fictional 1900s academy. FOOTPRINT AND HEIGHT NOT YET FIXED: avoid asserting exact dimensions. Period timber and wrought-iron glasshouse frame, daylight, orderly raised seedling beds and propagation benches, terracotta pots, narrow hand-watering tools, clear level central walkway, potting bench near visible door, ventilating roof panels. Ordinary botanical seedlings only, no exotic fictional magic plants, no cold food storage, no modern control screens. Human scale, no underground space. Useful nursery rather than conservatory lounge; no seating salon.

审看：采用：苗床、育苗台、近门移栽工作台、手工浇水与屋顶通风候选。舍弃：图中外部建筑不决定落位，装饰铁柱不强制保留。温室无已定外轮廓，不能用其替代冷干库。

## 未绘房型及范围

|项目|状态|
|---|---|
|上述11个已定类型|均有至少一张独立生成图；双视角按同栋分室表现|
|温室|另有1张无落位候选图|
|重复住宅、A/B、东西实例|仅同型复用，未逐实例另画，不能宣称朝向/门窗完全一致|
|住宅第二间同型双床房|在总候选中占位，未另画独立透视|
|厕所具体配置、给排水/炉灶热隔离/消防/结构施工|尚未工程设计与验证；不得以图视为已解决|
|所有未入镜角落和完整设备数量|未绘；由主Astra据选定分区完善平立体，不能从画面补猜尺寸|

本轮可采用的是空间关系、材料和人尺度家具意向。生成图中的景外建筑、装饰、机具细节不自动成为正式设计事实。

