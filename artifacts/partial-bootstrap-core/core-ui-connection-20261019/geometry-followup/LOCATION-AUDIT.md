# Native位置与现役地图静态审计（2026-10-19）

仅只读核查生产DV、地图适配器、地图生成器、现有tscn与资源manifest；未读取设计文档，未构建、启动生成器、游戏、正式会话或探针，未改生产源码。本文件给主代理审定映射依据，不把名字相近视为同一地点。

## 结论与待审定缺口

现有dvg_place显式声明的room均可由CampusMapView的room_of/entered_room命名逻辑产生；未发现空格或拼写导致的不存在room。此为命名与资源结构核对，不能证明每个目标点可寻路、碰撞可达或初次物化正确。

生产动作中outdoor_meter、outdoor_high、outdoor_alley没有dvg_place绑定。前两者缺少可直接识别的现役地图站点；alley有可信几何候选，但尚须主代理确认内容身份及逻辑行进边与地图路径相符。不能把未映射位置回退到其他房间，不能把泛指室外整图到达当作到达特定仪器或高地。

| 逻辑位置 | 本人实际动作/权威使用 | 当前缺口 | 可直接证实的几何与建议 |
| --- | --- | --- | --- |
| outdoor_meter | c2_edges central_hall↔meter↔high；c2.meter/sample.meter，sample_location必须为outdoor_meter | 无map/room/anchor；未找到户外仪器阵或源质计的生成物/marker身份 | 暂保持未映射。greenhouse的gauge是室内阀表，不能替代户外仪器。须审定现有具体实物身份，或另有明确资产授权后建立站点 |
| outdoor_high | c2_edges meter↔high↔alley；moran.c2.sketch.day在高地；c2.ascension对象location | 无map/room/anchor；没有high或高地的Sites | outdoor.py有地形高度G.Z渲染，但阈值绘色不是逻辑站点。不能选任意高像素或将campus_n全图视为高地。保持未知 |
| outdoor_alley | 急性倒下law、实际救起与逐段搬运、晚间写生；native源码多处明确夹道 | 无dvg_place；render标签也有“户外小径”，需核对身份 | 可审定候选 `{map:campus_sw, room:campus_sw:campus_sw, anchor:site:lane}`。其位置及夹道标签在生成器、manifest、tscn互证；仅给候选，未实施 |

outdoor_alley候选证据：outdoor.py:47–50为生活翼夹道西/东通往living_hall；893的lane坐标为px(-47.8,-56.7)。全局像素换算后减campus_sw的outdoor_origin(0,2640)，落在约(1924,736)；campus_sw.tscn:2248–2250实际Sites/lane即(1924,736)、标签“夹道”。manifest.json:1074–1117也记录夹道西/东两门及相同lane点。地图核心为Rect2(0,2928,2688,2928)，该点世界坐标约(1924,3376)，属于campus_sw核心，不需要借重叠区另一地图制造身份。若实施，anchor应保留，因为campus_sw:campus_sw覆盖整张室外分区，room本身不能限定夹道。lane在DECOR_MARKERS中，普通site_geometry({})会略过；现有anchor('site:lane')直接读取Marker2D，不受该过滤，不能混淆这两个接口。

生成器outdoor.py声明读取`artifacts/remake-chapter2/campus-site`，当前该路径缺失；本次rg返回具体“系统找不到指定的路径”，不能据此声称全部历史场地源已恢复。现有tscn与manifest仍可独立核对已生成的坐标。

## dvg_place显式绑定逐项表

room_of由map key与sight_region组成；entered_room另要求terrain cell为`.`或`S`。下表“名称相符”只指结构规则存在，未替代实际可达验证。

| 逻辑位置 | map | execution room | 原生区域/锚点依据 |
| --- | --- | --- | --- |
| living_hall | living_hall | living_hall:hall | sight_region y<32；spawn east_south为现存Spawns |
| central_hall | central_hall | central_hall:central_hall | 默认return key；现存central_hall.tscn |
| lecture | lecture | lecture:lecture | 默认return key；现存lecture.tscn |
| logistics | logistics | logistics:dining | x>=13；rooms.py配给台/餐厅区 |
| clinic | clinic | clinic:ward | x<20；rooms.py病房区 |
| greenhouse | greenhouse | greenhouse:greenhouse | 默认return key；现存greenhouse.tscn |
| document_workshop | archive | archive:works | x>=16；rooms.py工房区 |
| resonance_hall | resonance | resonance:hall | x<22；rooms.py共鸣厅 |
| quiet_room | resonance | resonance:silent | x>=22；rooms.py沉静室 |
| white_tower_safe | clinic | clinic:safety | x>=20；rooms.py安全端/试剂区；只能证明当前既有映射，不新裁定建筑身份 |
| washroom | living_hall | living_hall:bath | y>=32；rooms.py南端盥洗区 |
| resonance | resonance | resonance:hall | 既有别名，与resonance_hall同呈现，不改变DV位置 |
| oath_court | campus_s | campus_s:campus_s | 默认return key；Sites/oath_court=(672,1776)，label誓约庭 |
| threshold.contract | living_hall | living_hall:hall | anchor west_north是现存Spawns，y约8格，仍hall |
| threshold.astral | living_hall | living_hall:hall | anchor west_south是现存Spawns，y约24格，仍hall |
| threshold.logistics | living_hall | living_hall:hall | anchor east_north是现存Spawns，仍hall |
| dorm.contract | zone_jia | zone_jia:hall | 寝区x>=22,y<14；mirror=false |
| dorm.astral | zone_bing | zone_bing:hall | 同寝区规则；mirror=false |
| dorm.logistics | zone_ding | zone_ding:hall | 先镜像x=35-cell.x，再用同寝区规则 |
| dorm.yi | zone_yi | zone_yi:hall | 同镜像规则；现有呈现别名，不新增行动路径 |
| foyer | zone_yi | zone_yi:foyer | 镜像后x>=30,7<=y<14；真实spawn from_hall=(64,400)落此区 |
| hall | zone_yi | zone_yi:hall | 镜像后22<=x<30,y<14 |
| R05 | zone_yi | zone_yi:north_1 | 乙区房号序列+镜像，anchor site:bed=(858,196)独立互证 |
| R06 | zone_yi | zone_yi:south_1 | 同房号序列与mirror，无专门native锚点 |
| R07 | zone_yi | zone_yi:south_2 | 同房号序列与mirror，无专门native锚点 |
| R08 | zone_yi | zone_yi:north_2 | 同房号序列与mirror，无专门native锚点 |
| corridor | zone_yi | zone_yi:corridor | 镜像后x<22,9<=y<14 |
| pantry | zone_yi | zone_yi:prep | 镜像后22<=x<30,y>=14；生成器备餐间 |

房号证据：rooms.py:83的乙区numbers=[05,06,08,07]，zone()按x0=8、15依次北房/南房取号，最后mirror。CampusMapView按镜像后的列命名north_1/south_1/north_2/south_2。native c1_edges将各卧室连corridor、corridor连hall、hall连pantry/foyer，与生成器门布置相符。旧zone_yi:R05/R06/R07/R08及zone_yi:pantry不再是当前dvg_place值。

c2_locations/c2_edges使用的生产位置，除上表与缺口表之外未发现另一项需凭猜测新增映射。counterweight、granary等为现有地图资源，不能仅因存在地图就把native logistics/granary对象改成新逻辑位置；本轮没有授权这种空间/动作条件变化。

## 显式travel.station核查

在现役scenarios与game源码中，显式travel.station来自chapter1/content.dv的E1/E2/E11，以及deep-resonance的E7/E10/E6。chapter2身体字段station和工作岗位字段是DV状态，不等同travel.station，不因名字相同当作已有几何站点。没有发现本表之外已声明的非E显式travel.station。E资产由另一代理负责，本报告仅登记静态接口依赖，不重复修改。

| 目标station | 动作/构造 | 逻辑room→现执行room | 本次读取到的tscn状态 |
| --- | --- | --- | --- |
| site:E1 | detective.touch_latch | foyer→zone_yi:foyer | zone_yi当前Sites中未列E1 |
| site:E2 | actor.compare.E2 | foyer→zone_yi:foyer | 未列E2 |
| site:E11 | actor.compare.E11 | R05→zone_yi:north_1 | 未列E11 |
| site:E7 | huo.deep-resonance | hall→zone_yi:hall | 未列E7 |
| site:E10 | shen.deep-resonance | pantry→zone_yi:prep | 未列E10 |
| site:E6 | su.deep-resonance | R08→zone_yi:north_2 | 未列E6 |

读取时zone_yi.tscn的Sites仍只有transmitter、zone_rules、isolation_gate、bed。E代理后续生成物应以其新指纹/实际资源为准；本表不声称其工作已失败或已完成。四寝区的Spawns只有from_hall，其他逻辑房间首次物化缺少专门native spawn/anchor的范围沿NATIVE-UI-CONTRACT.md保留；Stands不自动等于对应逻辑房间。

## 来源与限制

- native：language_core/game/entry.dv:119起dvg_place；chapter1/content.dv的c1_edges与站点构造；chapter2/content.dv的c2_edges/c2_locations/户外law与采样动作。
- 几何：CampusMapView.gd的sight_region、room_of、entered_room、anchor、site_geometry、execute_destination。
- 生成器：godot_csharp/tools/chapter2_art/rooms.py、outdoor.py；未执行。
- 生成物：godot_csharp/scenes/Chapter2/Maps/*.tscn与ShareAsset/Internal/Props/Chapter2/manifest.json；仅文本读取，未看游戏画面。

读到的entry.dv SHA-256：767A84D898D274C73716ABA751378D1F173CB2AB7118D310F403ED4A34643174；CampusMapView.gd：2E055D07861CD55D295A6D53F7BF15DD849E18D95A7612BB57DEEC44A433D7C2。工作区有其他代理的并行改动，此身份限定读取时版本；未做运行或验收结论。建议主代理先审定户外地点身份和具体锚点，再授权实施；meter/high证据缺失不能由本报告填补。

## 用户授权户外位置后的静态矩形复核

本节覆盖前文“未授权/未映射”的阶段身份：用户已允许必要时补定meter/high位置，主代理明确指定中央厅北门至名录库通路的坐标。本代理没有读取设计，也没有新增地图；只读解析tscn的RectangleShape2D/CollisionShape2D、Sprite2D纹理PNG尺寸头、出口Area及adapter栅格公式，计算下列静态结果。没有启动Godot、生成器或正式会话。

### meter/high及表具

campus_n户外坐标以local为准，origin=(2400,0)。当前nav为16px格、中心offset=(8,8)、不可斜走。每个Solid矩形膨胀left/right=10、top=1、bottom=14；填格边界按ceil/floor包含精确边界上的格中心。

| 项目 | local坐标 / cell中心 | 既有Solids及sprite情况 | 门区域距离 |
| --- | --- | --- | --- |
| meter anchor | (672,1416)，cell(42,88)中心(680,1416) | 点、目标nav中心、相同脚底小矩形均未碰旧Solid；无既有Objects sprite外框覆盖 | 至中央厅北Area矩形最短164px；至名录库Area237px |
| high anchor | (672,1272)，cell(42,79)中心(680,1272) | 同上 | 至名录库Area93px；至中央厅北Area308px |
| 初提表具foot | (700,1416)，footprint=[690,1408]..[710,1416] | footprint不碰旧Solids；foot点不在旧sprite外框内 | 至中央厅北Area约164.30px；至名录库Area约237.21px |

中央厅北门Area=[654,1580]..[690,1616]，Spawns/hall_north=(672,1545)；名录库Area=[654,1143]..[690,1179]，Spawns/archive_front=(672,1214)。meter与high处于两门外间隔内，不在两门Area里。名录库主要Solid s2=[480,672]..[864,1152]，膨胀底边1166；high y1272离该膨胀矩形106px。中央厅北门廊s13=[588,1608]..[756,1728]，膨胀上边1607；meter y1416离其191px。既有Objects PNG矩形外框核算没有覆盖这些指定点；这仍不是实际渲染遮挡审查。

**初提表具会堵meter自己的nav格。** 加入footprint后，新的膨胀矩形=[680,1407]..[720,1430]，meter目标cell中心(680,1416)恰好包含在左边界。基资源无碰撞不能据此宣称最终布局可达。最小整数修正可将footpoint向右1px至(701,1416)，使膨胀左边界681；或anchor向左1px至(671,1416)换入前一目标格。主代理已将表具footpoint取更规整的**(704,1416)**，保留meter anchor(672,1416)。此时实际footprint=[694,1408]..[714,1416]，膨胀=[684,1407]..[724,1430]，meter目标中心x680不被堵。32×48 SVG底中定位后的显示外框=[688,1368]..[720,1416]，与meter anchor及high都分开；没有同既有建筑sprite外框相交的静态迹象。该修正是物件几何位置，不改DV行动或采样条件。

### 旧lane marker实际错误与现存lane_east替代

**前文site:lane候选仅按marker存在与身份互证；碰撞核查推翻了其可达性。** campus_sw.tscn现有Sites/lane=(1924,736)落入Solid s36本体。s36中心=(1925,737)，size=(240,706)，本体矩形=[1805,384]..[2045,1090]，膨胀后=[1795,383]..[2055,1104]。lane目标cell(120,46)中心=(1928,744)同样solid；还位于“生活翼·起居厅”sprite外框[1794,166]..[2056,1090]内。不能再以marker存在或campus_sw整图room相符声称夹道anchor可达。

只看最近合法格，东侧(2056,728)/(2056,744)离旧marker约132.24px，但位于夹道东门Area，不能直接拿来作免换图风险的目标。主代理提出改用既有Spawns/lane_east，核对如下：

| 现有spawn | 坐标、nav目标 | Solid与门检查 | 生成器来源 |
| --- | --- | --- | --- |
| lane_east | (2107,744)，cell(131,46)中心(2104,744) | 点和目标格均不在任何膨胀Solid；真实角色box16×12，脚底上方12px，与东门Area不重叠，水平净距27px | outdoor.py DOORS夹道东(u=-42.8,v=-56.7)，向外方向(1,0)；生成spawn向外2.6m并对水平门脚点加8px。tscn与manifest均为(2107,744) |
| lane_west | (1742,744)，cell(108,46)中心(1736,744) | 同样合法；与西门Area水平净距27px | 对称的DOORS夹道西，方向(-1,0)，同公式 |

东门Area=[2036,718]..[2072,754]，西门Area=[1777,718]..[1813,754]；StoryActor的box.size=(16,12)、shape.position.y=-6，作为该净距计算的实际角色形状。`anchor("lane_east")`读取Spawns/lane_east，直接用已有点即可，无须造新point或把旧site:lane挪动。建议outdoor_alley采用campus_sw:campus_sw与anchor=lane_east，并保留实际anchor到达检查；该建议已报告主代理，由其实施。数据计算只证明静态目标合法，未执行从其他地图到此点的连续寻路/门切换。

### 本节边界

没有实际截屏、Godot碰撞或正式会话。最终脚点与marker到达误差、表具可见性、cross-map路线、双向进出、动态门屏障与NPC占位须在后续工程和UI验收分别核对。旧site:lane明确保留为错误几何证据，不应被当作本轮到达成功收据。

本节静态基础指纹：campus_n.tscn SHA-256=F6D387E928D60AF33280A27BEA0637A7651E09A0B4917E435611BA4D924599E5；campus_sw.tscn=C0EAA1E525A69238ACB9EBBCD52F85735A4BE9F6FE5BECC19A08400C5FBC5568。本节读取最终native片段时表具已经为(704,1416)，outdoor_alley当时仍暂列site:lane，主代理后续修改应以其最终指纹为准。
