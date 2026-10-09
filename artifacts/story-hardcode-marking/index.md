# 剧情内容与专用绑定标记索引

基线：`2384076f065acf32c95aad6e1732bd06a94f1bf3`。本轮审读 206 份 `.cs` / `.gd`：205 份手写源码全文审读，1 份剧本生成 C# 登记来源并排除手改。

共 196 处标记，位于 83 份文件。标记以类或方法为单位时覆盖其实现块，并非每个分支单独计数；因此数量不表示独立缺陷数量。

程序原有内容对照通过；Core 和 Godot 两套构建通过。修改过的 GDScript 语法检查 26/27 通过，唯一失败是旧画廊脚本缺失的预载依赖，已用基线版本复现。详细记录见 [独立验证报告](verification/REPORT.md)。

## 范围与解释

- `logic` 是具体因果、人物特例、事件流程、固定答案或行动策略；`text` 是内嵌具体文案；`schema` 是专用实体、资产或契约绑定，不能把它一律视为剧情因果。
- `production` 表示正式工程编译或脚本包含，不能据此断言每条路径在主游戏中实际执行。Core 的两份 Hall 实现默认编译包含，但审计搜索未发现正式运行调用。
- 测试、工具、示例、预览分别标记。预写答案、注入状态或模拟策略不能当作独立盲玩证据。
- 通用机制、运动参数、纯几何尺寸、正常读取外部故事数据和章节集成未作剧情标记。源码中的明确事实不等于设计规范，标记也不表示已经修复。
- `.story`、译文、生成 JSON/C#、图片音频、缓存不按手写剧情逻辑重复标记；生成 C# 保持原样。Godot 场景资源另列筛查记录，不直接手改生成地图。

## 分类数量

| 范围 | logic | text | schema | 合计 |
| --- | ---: | ---: | ---: | ---: |
| 正式工程中的代码 (`production`) | 80 | 20 | 49 | 149 |
| 测试夹具 (`fixture`) | 8 | 2 | 10 | 20 |
| 开发与采集工具 (`tool`) | 12 | 0 | 9 | 21 |
| 独立示例 (`example`) | 1 | 1 | 0 | 2 |
| 编辑器或作者预览 (`preview`) | 1 | 1 | 2 | 4 |

## 每个标记

### 正式工程中的代码

| 编号 | 类型 | 源码位置 | 说明 |
| --- | --- | --- | --- |
| `CSH-001` | `schema` | [csharp/HallCausalityRules.cs:4](C:/Users/iii/Desktop/dual_ontology_world_engine/csharp/HallCausalityRules.cs:4) | 具体隔离门、木楔、灰烟、纸转印、双锁与擦地证据因果模型。 |
| `CSH-002` | `text` | [csharp/HallCausalityRules.cs:44](C:/Users/iii/Desktop/dual_ontology_world_engine/csharp/HallCausalityRules.cs:44) | 针对隔离门案件假设输出结构结论与排除边界。 |
| `CSH-003` | `logic` | [csharp/HallCausalityRules.cs:64](C:/Users/iii/Desktop/dual_ontology_world_engine/csharp/HallCausalityRules.cs:64) | 模拟隔离门案件动作顺序并生成具体证据痕迹和反馈。 |
| `CSH-004` | `logic` | [csharp/HallReplayProgram.cs:4](C:/Users/iii/Desktop/dual_ontology_world_engine/csharp/HallReplayProgram.cs:4) | 将案件重演稿编译为音乐家在隔离门观察烟气并操作门楔的固定流程。 |
| `CSH-005` | `text` | [csharp/HallReplayProgram.cs:13](C:/Users/iii/Desktop/dual_ontology_world_engine/csharp/HallReplayProgram.cs:13) | 重演事件与失败反馈直接描述门厅音乐家、热烟和案件动作。 |
| `CSH-009` | `text` | [csharp/Reasoner.cs:124](C:/Users/iii/Desktop/dual_ontology_world_engine/csharp/Reasoner.cs:124) | 将腐化来源专称为“铜口之言”，是项目世界专属的证据反馈文案。 |
| `AWK-059` | `schema` | [godot_csharp/scenes/Awakening/AwakeningMap.gd:14](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/AwakeningMap.gd:14) | 本章人物键与正式现场角色节点名称固定绑定。 |
| `AWK-041` | `schema` | [godot_csharp/scenes/Awakening/AwakeningMap.gd:284](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/AwakeningMap.gd:284) | 日常行动目标映射到本章 piano/tube/sink/door/score 等现场物件。 |
| `AWK-003` | `schema` | [godot_csharp/scenes/Awakening/Night/AwakeningCase.cs:20](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/AwakeningCase.cs:20) | 将本章 V1–V6、证物字段及第六问默认认知装配成案件假设。 |
| `AWK-038` | `schema` | [godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs:63](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs:63) | 装配本章 V1–V6 假设、证物、陈述步与搜索模型。 |
| `AWK-039` | `schema` | [godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs:177](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs:177) | 答辩固定为本章四步，并允许 me 作为自闩陈述。 |
| `AWK-060` | `logic` | [godot_csharp/scenes/Awakening/Night/MorningScriptWorld.cs:51](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/MorningScriptWorld.cs:51) | 离开生活翼时清除本章旧物牵引习惯，避免它压过后续日常。 |
| `AWK-004` | `schema` | [godot_csharp/scenes/Awakening/Night/MorningTraces.cs:13](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/MorningTraces.cs:13) | 字段枚举本案固定证物事实及其编码取值。 |
| `AWK-005` | `logic` | [godot_csharp/scenes/Awakening/Night/MorningTraces.cs:95](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/MorningTraces.cs:95) | E1–E9 的逐件痕迹比较与字段解释是本案专属映射。 |
| `AWK-017` | `logic` | [godot_csharp/scenes/Awakening/Night/MorningWorld.cs:95](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/MorningWorld.cs:95) | 为有具体旧物回忆的人写入欲望、守则、铜口信任、劝告与触物行为规则。 |
| `AWK-020` | `logic` | [godot_csharp/scenes/Awakening/Night/MorningWorld.cs:254](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/MorningWorld.cs:254) | 固定以公告框第一条作为角色读到的代价规则。 |
| `AWK-018` | `logic` | [godot_csharp/scenes/Awakening/Night/MorningWorld.cs:303](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/MorningWorld.cs:303) | 将本案角色取舍、知守则与劝告分支映射到专属心声。 |
| `AWK-019` | `logic` | [godot_csharp/scenes/Awakening/Night/MorningWorld.cs:336](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/MorningWorld.cs:336) | 独处时犹豫即放下是本章角色回忆规则。 |
| `AWK-021` | `schema` | [godot_csharp/scenes/Awakening/Night/MorningWorld.cs:439](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/MorningWorld.cs:439) | 将现实声路接头状态投影到 E8 铜口证物的固定事实标签。 |
| `AWK-006` | `schema` | [godot_csharp/scenes/Awakening/Night/NightReplay.cs:91](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/NightReplay.cs:91) | 固定本案九个空间的夜间通行拓扑与行动耗时。 |
| `AWK-007` | `logic` | [godot_csharp/scenes/Awakening/Night/NightReplay.cs:194](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/NightReplay.cs:194) | 构造具体案件物件、四名角色的起始地点与鞋底，并将 V1/V2/V6 绑定到世界状态。 |
| `AWK-008` | `logic` | [godot_csharp/scenes/Awakening/Night/NightReplay.cs:281](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/NightReplay.cs:281) | 按 V5/V6 设定侦探对特定同伴的信任偏移与熟人信任。 |
| `AWK-009` | `logic` | [godot_csharp/scenes/Awakening/Night/NightReplay.cs:309](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/NightReplay.cs:309) | 逐人写入昨夜具体行为、认知规则、门/烟/物证互动和请求回应。 |
| `AWK-010` | `logic` | [godot_csharp/scenes/Awakening/Night/NightReplay.cs:449](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/NightReplay.cs:449) | 固定警铃、两次声音、烟到门与侦探醒来的事件阶段构成本案昨夜时序。 |
| `AWK-011` | `logic` | [godot_csharp/scenes/Awakening/Night/NightReplay.cs:493](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/NightReplay.cs:493) | 把本案话语的语义标记映射到人物对指令、门外人和求救的认知。 |
| `AWK-012` | `logic` | [godot_csharp/scenes/Awakening/Night/NightReplay.cs:670](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/NightReplay.cs:670) | 将本案熟悉度与拒绝请求映射为专属心声及案件标记。 |
| `AWK-013` | `schema` | [godot_csharp/scenes/Awakening/Night/NightReplay.cs:711](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/NightReplay.cs:711) | 本案请求类型名映射到固定重演事件与门边站位。 |
| `AWK-014` | `schema` | [godot_csharp/scenes/Awakening/Night/NightReplay.cs:829](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/NightReplay.cs:829) | 案件物件分别映射到门、石板、铜口、书桌和水池回放站位。 |
| `AWK-015` | `logic` | [godot_csharp/scenes/Awakening/Night/NightReplay.cs:846](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/NightReplay.cs:846) | 本案门状态变化、烟灰、鞋印、湿布压痕与喊声听闻的物理后果。 |
| `AWK-016` | `logic` | [godot_csharp/scenes/Awakening/Night/NightReplay.cs:901](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/NightReplay.cs:901) | 按苏泠、沈鼎、霍铎在特定场面中的认知生成等待心声。 |
| `AWK-063` | `schema` | [godot_csharp/scenes/Awakening/Night/NightVariables.cs:6](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/NightVariables.cs:6) | 本案角色/房间及 V1–V6 选项由连续固定枚举编码。 |
| `AWK-001` | `logic` | [godot_csharp/scenes/Awakening/Night/NightVariables.cs:91](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/NightVariables.cs:91) | 固定传播表规定铜口完整/接头句尾/隔墙低语/其余警铃，以及门内外喊声组合；拓扑与接头配置读取故事数据。 |
| `AWK-002` | `schema` | [godot_csharp/scenes/Awakening/Night/NightVariables.cs:130](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Night/NightVariables.cs:130) | 枚举键与本章人物、房间名称按固定顺序绑定。 |
| `AWK-058` | `schema` | [godot_csharp/scenes/Awakening/RoomDoor.gd:18](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/RoomDoor.gd:18) | 宿舍门所有者键与本章角色节点名称固定绑定。 |
| `AWK-035` | `logic` | [godot_csharp/scenes/Awakening/Script/Chapter1Interaction.cs:37](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Script/Chapter1Interaction.cs:37) | E11 手印卡有先比自己、再比在场同伴的本章专属门槛。 |
| `AWK-061` | `logic` | [godot_csharp/scenes/Awakening/Script/Chapter1Replay.cs:8](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Script/Chapter1Replay.cs:8) | 本章实播重演将固定六项变量、吻合判断和角色见证写入玩家进度。 |
| `AWK-028` | `logic` | [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:76](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:76) | E8 只开听位菜单且 E3 选对象前不结算；其余试验按证物编号调度。 |
| `AWK-029` | `logic` | [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:122](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:122) | 侦探与三名同伴的旧物回忆分别走固定角色流程。 |
| `AWK-030` | `logic` | [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:226](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:226) | 固定同伴顺序、调查痕迹阈值与离场低语触发条件属于本章流程。 |
| `AWK-031` | `logic` | [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:280](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:280) | E1/E3 开放重演、指定熟悉旁证揭示 V6 的触发映射。 |
| `AWK-032` | `schema` | [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:312](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:312) | E12 展示 E7 石板小图是本案证物间固定关联。 |
| `AWK-033` | `logic` | [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:318](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:318) | E11 四人手印进度及 E8 声路试验笔记为本案专属摘要逻辑。 |
| `AWK-034` | `logic` | [godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:463](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Script/Chapter1Script.cs:463) | 从本章固定案件真相建立清晨证物实体与醒来人物认知。 |
| `AWK-036` | `schema` | [godot_csharp/scenes/Awakening/Script/Chapter1Session.cs:26](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Script/Chapter1Session.cs:26) | 登记固定的第一章四人组及三名同伴。 |
| `AWK-037` | `schema` | [godot_csharp/scenes/Awakening/Script/Chapter1Session.cs:576](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Script/Chapter1Session.cs:576) | 手稿固定将第六问作为末项并按认知解锁隐藏显示。 |
| `AWK-022` | `logic` | [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:14](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:14) | 第一章开场固定摆放侦探与三名同伴并启动醒来流程。 |
| `AWK-023` | `logic` | [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:141](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:141) | 开门命令固定联动隔离门状态、音效和主旋律。 |
| `AWK-024` | `logic` | [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:216](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:216) | 相遇阶段固定角色身份与对应会面锚点。 |
| `AWK-025` | `logic` | [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:373](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:373) | E11 手印与 E3 鞋底试验使用本章专属处理分支。 |
| `AWK-026` | `schema` | [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:596](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:596) | 把本章声路听位绑定到对应现场证物热点。 |
| `AWK-027` | `logic` | [godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:601](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs:601) | 按已知石板、乐谱、霍铎回忆开放本章试验句。 |
| `AWK-062` | `schema` | [godot_csharp/scenes/Awakening/Script/Chapter1Speech.cs:12](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/Script/Chapter1Speech.cs:12) | 回应键把本章言语行为/立场固定映射到剧本词汇键。 |
| `AWK-042` | `schema` | [godot_csharp/scenes/Awakening/TraceDecals.gd:8](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/TraceDecals.gd:8) | 绘制本案铜管两条具体路径及松动接头位置。 |
| `AWK-043` | `schema` | [godot_csharp/scenes/Awakening/TraceDecals.gd:14](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/TraceDecals.gd:14) | 绘制本案灰扇、靴印、湿布、接头与门框凹痕的位置方向。 |
| `AWK-044` | `logic` | [godot_csharp/scenes/Awakening/TraceDecals.gd:60](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/TraceDecals.gd:60) | 按本章 R-05 半枚手印、住人房方框与空房均匀灰尘呈现固定证物。 |
| `CH2-001` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Alliances.cs:16](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Alliances.cs:16) | 聚落阵营规则的具体身份、时段和后果在代码中定义。 |
| `CH2-086` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Case.cs:10](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Case.cs:10) | 定义本案缺粮、门闩、空碗、废纸篓与夜巡身影的证物模型。 |
| `CH2-087` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Case.cs:32](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Case.cs:32) | 固定本案反事实重演所用的四项人物信念/处境变量。 |
| `CH2-003` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Case.cs:61](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Case.cs:61) | 案发痕迹、人物知识变量与反事实重演映射为特定案件事实。 |
| `CH2-088` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Case.cs:230](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Case.cs:230) | 结算本案公开指控、证据说服、雀儿洗清或排斥及规则提出。 |
| `CH2-089` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Case.cs:344](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Case.cs:344) | 处理案件对质后的特定议案表态、议事站队与共同规则结果。 |
| `CH2-090` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Case.cs:399](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Case.cs:399) | 允许案发后援引本章共同规则重开中央厅对质。 |
| `CH2-091` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Case.cs:423](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Case.cs:423) | 按本案指控结局改变雀儿的寝区、信任与名誉认知。 |
| `CH2-009` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Chapter2Access.cs:8](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Chapter2Access.cs:8) | 具体宿舍、走廊和名录库区域的通行身份硬编码。 |
| `CH2-083` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Chapter2AuthoredSites.cs:22](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Chapter2AuthoredSites.cs:22) | 白名单固定本章专用的站点交互类型。 |
| `CH2-100` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Chapter2AuthoredSites.cs:30](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Chapter2AuthoredSites.cs:30) | 豁免本章时钟和传声物件的房间一致性校验。 |
| `CH2-084` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:8](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:8) | 将剧本里的具体世界物件身份绑定到本章运行对象。 |
| `CH2-096` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:38](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:38) | 限定本章公告板实体可绑定的具体地点集合。 |
| `CH2-097` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:40](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Chapter2Bindings.cs:40) | 限定本章废纸篓实体只存在于四个宿舍区。 |
| `CH2-010` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Chapter2DesignRules.cs:119](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Chapter2DesignRules.cs:119) | 允许的行为编号清单是本项目规则语义的硬编码 schema。 |
| `CH2-033` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:331](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:331) | 调查台账在案发日后解锁本案重演的预写逻辑。 |
| `CH2-025` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:410](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:410) | 对质议程固定为本案的取用、门闩、台账及特定人物结局。 |
| `CH2-026` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:542](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Chapter2InteractionSession.cs:542) | 人物印象候选名单固定为本案关键角色。 |
| `CH2-024` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Chapter2SessionView.cs:20](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Chapter2SessionView.cs:20) | 角色身份到专属肖像标识及地图地点的映射写在宿主代码。 |
| `CH2-080` | `text` | [godot_csharp/scenes/Chapter2/Prototype/Chapter2SessionView.cs:61](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Chapter2SessionView.cs:61) | 构造本章议事结果、角色归宿与章末卡文本。 |
| `CH2-011` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Chapter2SocialActions.cs:3](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Chapter2SocialActions.cs:3) | 本文件按本章具体社会行动实现角色互动及预写反馈。 |
| `CH2-023` | `text` | [godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:425](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:425) | 帮助展示具体人名、物证提示与本章行动例句。 |
| `CH2-081` | `text` | [godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:494](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:494) | 状态页显示本案特定证物、指控与石碑阅读结果。 |
| `CH2-082` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:621](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/ConsoleUI.cs:621) | 过滤本案清晨跟踪的特定无变文字。 |
| `CH2-004` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Council.cs:24](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Council.cs:24) | 议事前厅三站、角色资格与夜间夺取的具体规则及反馈。 |
| `CH2-045` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Council.cs:54](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Council.cs:54) | 按具名人物、地点和本案时间安排议事前的控制与封锁。 |
| `CH2-046` | `text` | [godot_csharp/scenes/Chapter2/Prototype/Council.cs:86](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Council.cs:86) | 为本章议事结局编写人物组合对应的开场反馈。 |
| `CH2-047` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Council.cs:127](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Council.cs:127) | 结算本章誓约庭议案、具名派系立场及具体结局。 |
| `CH2-027` | `text` | [godot_csharp/scenes/Chapter2/Prototype/Demon.cs:35](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Demon.cs:35) | 具体课次、日期、标题和教师讲授文字写入代码。 |
| `CH2-043` | `text` | [godot_csharp/scenes/Chapter2/Prototype/Demon.cs:100](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Demon.cs:100) | 逐日晨间播报包含本章特定课程与事件。 |
| `CH2-044` | `text` | [godot_csharp/scenes/Chapter2/Prototype/Demon.cs:130](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Demon.cs:130) | 恶魔对本案各话题的知识边界与回答逐项预写。 |
| `CH2-028` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Emergence.cs:84](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Emergence.cs:84) | 事件追踪按本章人物与案件谓词生成特定因果链。 |
| `CH2-038` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Emergence.cs:138](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Emergence.cs:138) | 将本章特定人物行动映射为案件因果事件。 |
| `CH2-039` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Emergence.cs:289](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Emergence.cs:289) | 仅对本章具体人物与行动组合开放可读线索。 |
| `CH2-040` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:61](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:61) | 把具体本章工作映射为案件物证与人物行动结果。 |
| `CH2-041` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:163](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:163) | 为具名人物预置习惯行动及其观察规则。 |
| `CH2-042` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:259](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:259) | 按本章人物身份与案件状态配置行动计划。 |
| `CH2-029` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:341](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/GameJobs.cs:341) | 背景人物按具体身份、日期和关系执行预设专属行为。 |
| `CH2-061` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:108](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:108) | 按本章日序初始化病情、案件发现与线索状态。 |
| `CH2-062` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:332](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:332) | 把指定案件行动完成映射为本章固定后果。 |
| `CH2-063` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:428](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:428) | 结算本章人物、作案物件与调查线索的专属物理结果。 |
| `CH2-064` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:556](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:556) | 判定本案夜间取物是否被特定证据和目击捕捉。 |
| `CH2-065` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:637](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:637) | 按本案特定行为积累或消除嫌疑。 |
| `CH2-066` | `text` | [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:655](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:655) | 为本案指控对象选择具名指控反馈。 |
| `CH2-067` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:730](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:730) | 执行本案预置的按日/时段线索及人物状态变更。 |
| `CH2-068` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:836](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:836) | 结算本案温室特定事件、人物和物件状态。 |
| `CH2-069` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:985](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:985) | 在本案案发后按固定条件触发点名事件。 |
| `CH2-070` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:1011](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs:1011) | 结算阮、贝特朗、莫拉与茉拉相关的专属油料剧情。 |
| `CH2-072` | `text` | [godot_csharp/scenes/Chapter2/Prototype/Host.cs:48](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Host.cs:48) | 为温室阀门与中继站提供本章专属物证反馈。 |
| `CH2-073` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Host.cs:96](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Host.cs:96) | 指定首课答题后特定人群对教师的反应。 |
| `CH2-032` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:18](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:18) | 调查条目与本章具体证物、案件变量的映射逻辑。 |
| `CH2-071` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:128](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/InvestigationPresentation.cs:128) | 把案件特定痕迹键映射到对应调查物件。 |
| `CH2-074` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Model.cs:7](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Model.cs:7) | 定义本章十六位学员的具体身份标识。 |
| `CH2-092` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Model.cs:9](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Model.cs:9) | 固定本章具名学员所属的四个社会群体。 |
| `CH2-085` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Model.cs:12](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Model.cs:12) | 定义本章具名场所与宿舍区身份。 |
| `CH2-098` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Model.cs:17](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Model.cs:17) | 固定本章价值聚落类别及其社会阵营语义。 |
| `CH2-093` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Model.cs:21](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Model.cs:21) | 固定本章角色日程可执行的专属行为枚举。 |
| `CH2-099` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Model.cs:34](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Model.cs:34) | 固定本章议事规则语义位，包括具名目标、档案院权限及夜间议案条件。 |
| `CH2-075` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Model.cs:43](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Model.cs:43) | 定义本案粮仓、痕迹与被指控等专用状态标识。 |
| `CH2-094` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Model.cs:87](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Model.cs:87) | 固定本章人物的内部别名与身份映射。 |
| `CH2-095` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Model.cs:94](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Model.cs:94) | 固定本章地点的内部别名与地点映射。 |
| `CH2-048` | `text` | [godot_csharp/scenes/Chapter2/Prototype/Morning.cs:93](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Morning.cs:93) | 生成针对本案夜间行动与地点的固定守望报告。 |
| `CH2-030` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Morning.cs:144](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Morning.cs:144) | 清晨生成台账、门闩、病碗、废纸篓与目击等具体案发痕迹。 |
| `CH2-049` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Morning.cs:211](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Morning.cs:211) | 指定本案各类物证达到何种组合才可读全。 |
| `CH2-050` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Morning.cs:414](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Morning.cs:414) | 将本案特定行动地点编码为重演档案中的位置名称。 |
| `CH2-051` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Morning.cs:483](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Morning.cs:483) | 用本案预设体型与可见轨迹筛选嫌疑人。 |
| `CH2-052` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Morning.cs:497](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Morning.cs:497) | 将案夜特定证词和现场记录折算为可见体型。 |
| `CH2-053` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Player.cs:44](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Player.cs:44) | 固定本案可调查物件、对应地点和真实实体。 |
| `CH2-054` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Player.cs:58](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Player.cs:58) | 固定各案发相关地点的调查物件集合。 |
| `CH2-055` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Player.cs:69](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Player.cs:69) | 处理玩家介入后本案指定人物请求及证据相关分支。 |
| `CH2-056` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Player.cs:195](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Player.cs:195) | 结算本章玩家行动、证据反馈与案情特定揭示。 |
| `CH2-031` | `text` | [godot_csharp/scenes/Chapter2/Prototype/Player.cs:464](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Player.cs:464) | 不同具体角色对案发夜的预写口供反馈直接写在代码中。 |
| `CH2-057` | `text` | [godot_csharp/scenes/Chapter2/Prototype/Player.cs:537](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Player.cs:537) | 为本案专属物证、人物特征及状态撰写调查描述。 |
| `CH2-058` | `text` | [godot_csharp/scenes/Chapter2/Prototype/Report.cs:67](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Report.cs:67) | 按具名人物立场生成议事前的本案反馈。 |
| `CH2-059` | `text` | [godot_csharp/scenes/Chapter2/Prototype/Report.cs:117](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Report.cs:117) | 输出本章案件与议事结局的专属总结。 |
| `CH2-060` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Retell.cs:30](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Retell.cs:30) | 绑定特定人物与本章习惯证词的文本键。 |
| `CH2-078` | `text` | [godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs:11](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs:11) | 按本章时段、地点与案件状态提供场景动作选项。 |
| `CH2-079` | `text` | [godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs:54](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/RoomPresentation.cs:54) | 定义本章当面交互可执行的具体人物行动。 |
| `CH2-005` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Rules.cs:114](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Rules.cs:114) | 为具体人物初始化特定规则、时段、目标与结果。 |
| `CH2-006` | `schema` | [godot_csharp/scenes/Chapter2/Prototype/Rules.cs:153](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Rules.cs:153) | 将本项目特定规则卡编号和行为映射为具体游戏后果。 |
| `CH2-034` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Rules.cs:353](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Rules.cs:353) | 按具体人物身份写入对议案措辞的立场偏好。 |
| `CH2-035` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Rules.cs:447](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Rules.cs:447) | 为各具体人物预设议案条件与反对理由。 |
| `CH2-036` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Rules.cs:459](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Rules.cs:459) | 指定人物对特定议案的否决底线。 |
| `CH2-037` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Social.cs:211](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Social.cs:211) | 固定本章恶魔词、基座裂缝指认及指控/教训后果。 |
| `CH2-007` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Social.cs:260](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Social.cs:260) | 对“门闩松了”证词执行本案专属解释。 |
| `CH2-008` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Social.cs:282](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Social.cs:282) | 阵营成员集合固定引用本章具体人物身份。 |
| `CH2-012` | `logic` | [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:93](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:93) | 初始化具体角色、案件实体、持钥身份、时空位置与案件状态。 |
| `CH2-076` | `schema` | [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:167](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:167) | 初始化本章具体空间、案件物件、持钥人身份与知情模型。 |
| `CH2-013` | `logic` | [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:297](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:297) | 初始化不同具体人物对角色、身份、关系、规则及旧案的认知。 |
| `CH2-014` | `logic` | [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:495](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:495) | 限定具体空间的夜间视线并生成身形目击和专案反馈。 |
| `CH2-015` | `text` | [godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:549](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs:549) | 章节专属教师身份替换与标题常量写在宿主代码中。 |
| `CSH-033` | `schema` | [godot_csharp/scenes/Shared/Field/WorldView.gd:234](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Shared/Field/WorldView.gd:234) | 特定章节纹理到油灯/柜体可见名称的绑定；不是剧情因果规则。 |
| `CSH-035` | `schema` | [godot_csharp/scenes/Shared/Field/WorldView.gd:703](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Shared/Field/WorldView.gd:703) | 专用盟誓庭锚点与侦探位置绑定；名单由外部快照驱动。 |
| `CSH-022` | `logic` | [godot_csharp/scenes/UI/Panels/Dialogue/ChapterDialogue.gd:555](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/UI/Panels/Dialogue/ChapterDialogue.gd:555) | 铜口之声每段首次开口触发盖片与管道共鸣音。 |

### 测试夹具

| 编号 | 类型 | 源码位置 | 说明 |
| --- | --- | --- | --- |
| `AWK-047` | `schema` | [godot_csharp/scenes/Awakening/check/CognitionCheck/Program.cs:45](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/check/CognitionCheck/Program.cs:45) | 具体人物、证物与预期认知/言语结果仅用于自动测试。 |
| `AWK-048` | `schema` | [godot_csharp/scenes/Awakening/check/LawCheck/Program.cs:7](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/check/LawCheck/Program.cs:7) | 声路和证物破坏检查中的具体状态及答案是自动测试夹具。 |
| `AWK-045` | `schema` | [godot_csharp/scenes/Awakening/check/ReplayCheck/ReplayCheckProgram.cs:19](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/check/ReplayCheck/ReplayCheckProgram.cs:19) | 穷举检查内的真值组合、逐项反证表和答案是验证夹具数据。 |
| `AWK-050` | `schema` | [godot_csharp/scenes/Awakening/check/SessionCheck/SessionCheckProgram.cs:8](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/check/SessionCheck/SessionCheckProgram.cs:8) | 模拟会话中的角色站位、E1 检视与固定状态跳转属于测试夹具。 |
| `AWK-049` | `schema` | [godot_csharp/scenes/Awakening/check/StoryPacketCheck/Program.cs:41](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/check/StoryPacketCheck/Program.cs:41) | 预写角色、剧情分支与固定台词断言属于自动测试夹具。 |
| `AWK-046` | `schema` | [godot_csharp/scenes/Awakening/check/VariantCheck/Program.cs:8](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/check/VariantCheck/Program.cs:8) | 选角、心愿以及案件变体中的具体人物与预期答案属于验证夹具。 |
| `AWK-056` | `schema` | [godot_csharp/scenes/Awakening/check/cabinet_fog_current_check.gd:3](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/check/cabinet_fog_current_check.gd:3) | E11 固定人物、证物顺序与结论只作专项回归夹具。 |
| `AWK-053` | `schema` | [godot_csharp/scenes/Awakening/check/flow_check.gd:10](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/check/flow_check.gd:10) | 流程回归中的具体台词/场景/答案约束只作 fixture 断言。 |
| `AWK-057` | `schema` | [godot_csharp/scenes/Awakening/check/hand_outline_check.gd:17](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/check/hand_outline_check.gd:17) | E11 人物、证物和结果断言只作 UI 回归夹具。 |
| `AWK-054` | `schema` | [godot_csharp/scenes/Awakening/check/state_gallery.gd:17](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/check/state_gallery.gd:17) | 诊断画廊注入案件状态并按固定菜单推进，只作 UI 回归夹具。 |
| `CH2-002` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Bots.cs:26](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Bots.cs:26) | 自动推演目录预写具体策略、时间表、角色与议事选择，仅属开发夹具。 |
| `CH2-020` | `logic` | [godot_csharp/scenes/Chapter2/check/chapter2_fixture.gd:46](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/check/chapter2_fixture.gd:46) | 夹具自动填写固定侦探名、代按对白与菜单并跳过回放。 |
| `CSH-010` | `logic` | [godot_csharp/scenes/Shared/check/chapter1_smoke.gd:30](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Shared/check/chapter1_smoke.gd:30) | 自动脚本执行第一章固定调查、重演、始业问答与结尾检查，并预置角色回答。 |
| `CSH-019` | `logic` | [godot_csharp/scenes/Shared/check/check_room_fog.gd:191](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Shared/check/check_room_fog.gd:191) | 验证第一章E11a书架痕迹仅在进入其房间后可知。 |
| `CSH-020` | `logic` | [godot_csharp/scenes/Shared/check/check_room_fog.gd:328](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Shared/check/check_room_fog.gd:328) | 用第二章Huo检验未探索宿舍与当前房间人物的可见性。 |
| `CSH-015` | `text` | [godot_csharp/scenes/Shared/check/ui_modules_check.gd:183](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Shared/check/ui_modules_check.gd:183) | UI测试自带的案件问答样例，不视为正式剧情规则。 |
| `CSH-016` | `text` | [godot_csharp/scenes/Shared/check/ui_modules_check.gd:210](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Shared/check/ui_modules_check.gd:210) | UI测试自带的具体调查样例文本，未证明与正式案件相同。 |
| `CSH-007` | `logic` | [godot_csharp/tools/verify_ch1_locker_ui.gd:159](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/tools/verify_ch1_locker_ui.gd:159) | UI 检查定位第一章领取柜并跟踪固定角色的领取动画。 |
| `CSH-037` | `logic` | [godot_csharp/tools/verify_ch1_locker_ui.gd:164](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/tools/verify_ch1_locker_ui.gd:164) | 依项目奖励对白行号定位开场与空柜台词，核对固定物证阶段的位置/输入状态。 |
| `CSH-036` | `logic` | [godot_csharp/tools/verify_ch1_locker_ui.gd:233](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/tools/verify_ch1_locker_ui.gd:233) | 用第一章铜口热点E8及黑板热点BOARD验证其相对领取柜的具体命中优先级。 |

### 开发与采集工具

| 编号 | 类型 | 源码位置 | 说明 |
| --- | --- | --- | --- |
| `AWK-052` | `schema` | [godot_csharp/scenes/Awakening/check/DetectiveSimulation/ContinuationSimulation.cs:12](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/check/DetectiveSimulation/ContinuationSimulation.cs:12) | 续篇模拟器按公开菜单、空间与信息制定固定探索策略；未读取正确答案或自动通关。 |
| `AWK-051` | `schema` | [godot_csharp/scenes/Awakening/check/DetectiveSimulation/Program.cs:94](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Awakening/check/DetectiveSimulation/Program.cs:94) | 自动模拟器含固定侦探输入/空间采样与选项策略，仅作工具模拟。 |
| `CH2-021` | `logic` | [godot_csharp/scenes/Chapter2/Game/check_gallery.gd:43](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Game/check_gallery.gd:43) | 画廊脚本注入章一继承状态并预写第二章场景、行动与日期路线。 |
| `CH2-022` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Checks.cs:8](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Checks.cs:8) | 自动检查中的预写章节性断言、推演路线和游戏结局策略。 |
| `CH2-077` | `logic` | [godot_csharp/scenes/Chapter2/Prototype/Program.cs:40](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/Prototype/Program.cs:40) | 宿主冒烟流程固定演示本章查案、对质与议事。 |
| `CH2-016` | `logic` | [godot_csharp/scenes/Chapter2/check/check_flow.gd:7](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/check/check_flow.gd:7) | 自动流程预写具体地图出生点、角色、案件操作顺序和终局检查。 |
| `CH2-017` | `logic` | [godot_csharp/scenes/Chapter2/check/check_flow.gd:50](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/check/check_flow.gd:50) | 自动验收按预定证词、证据、支持与分支策略代选。 |
| `CH2-018` | `logic` | [godot_csharp/scenes/Chapter2/check/check_flow.gd:76](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/check/check_flow.gd:76) | 测试构造章一继承状态并预置姓名、答案和章二交接。 |
| `CH2-019` | `logic` | [godot_csharp/scenes/Chapter2/check/check_flow.gd:122](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Chapter2/check/check_flow.gd:122) | 测试硬编码第二章时间推进、规则转述、特定人物问话、证物和议事流程。 |
| `CSH-011` | `logic` | [godot_csharp/tools/PlayerTranscript/Program.cs:14](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/tools/PlayerTranscript/Program.cs:14) | 玩家记录器绑定第一章与学园地点、人物、调查路线及条件分支。 |
| `CSH-012` | `logic` | [godot_csharp/tools/PlayerTranscript/Program.cs:132](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/tools/PlayerTranscript/Program.cs:132) | 预设第一章作答文本/物证映射及第五日学园议事选择策略。 |
| `CSH-013` | `logic` | [godot_csharp/tools/PlayerTranscript/Program.cs:396](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/tools/PlayerTranscript/Program.cs:396) | 固定执行第一章检视顺序与连续五日路线以生成语义记录。 |
| `CSH-028` | `schema` | [godot_csharp/tools/build_breakfast_exchange.gd:17](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/tools/build_breakfast_exchange.gd:17) | 素材工具为早餐交换场景绑定人物姿态、桌面与汤碗资源；不推进剧情或映射答案。 |
| `CSH-029` | `schema` | [godot_csharp/tools/build_maxwell_intro.gd:24](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/tools/build_maxwell_intro.gd:24) | Maxwell素材工具将开场角色绑定为手按胸口/鞠躬姿态及白手套像素；不推进剧情或映射答案。 |
| `CSH-030` | `schema` | [godot_csharp/tools/build_story_tiles.gd:23](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/tools/build_story_tiles.gd:23) | 资产工具固定生成项目故事场景道具（钟、传送门、罗盘、早餐桌、书柜、客厅钢琴及横幅等）。 |
| `CSH-031` | `schema` | [godot_csharp/tools/build_study.gd:38](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/tools/build_study.gd:38) | 素材工具绑定推理书房的道具与空间摆位；不推进剧情或映射答案。 |
| `CSH-025` | `schema` | [godot_csharp/tools/capture_player_transcript.gd:26](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/tools/capture_player_transcript.gd:26) | 回放工具硬编码学园场景地图与项目地点的对应关系。 |
| `CSH-026` | `logic` | [godot_csharp/tools/capture_player_transcript.gd:309](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/tools/capture_player_transcript.gd:309) | 用正式快照中的人物/地点（包括侦探及实际热点）复原场景坐标，只重放不执行剧情答案。 |
| `CSH-027` | `schema` | [godot_csharp/tools/capture_player_transcript.gd:783](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/tools/capture_player_transcript.gd:783) | 可选截图阶段按E1、say字段、support选项及两场景对白识别剧情节点。 |
| `CSH-024` | `schema` | [godot_csharp/tools/capture_story_ui_samples.gd:8](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/tools/capture_story_ui_samples.gd:8) | 采样清单固定选择两章对白、E1证物、防辩表、午间争执、议事支持项与结局。 |
| `CSH-006` | `logic` | [godot_csharp/tools/capture_ui_start.gd:9](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/tools/capture_ui_start.gd:9) | 采集器按正式开场的“开始游戏”与“如何称呼你”剧情节点推进。 |

### 独立示例

| 编号 | 类型 | 源码位置 | 说明 |
| --- | --- | --- | --- |
| `CSH-008` | `text` | [godot_csharp/scenes/Shared/Story/Examples/ExampleWorld.cs:7](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Shared/Story/Examples/ExampleWorld.cs:7) | 可执行示例定义值夜学员、值夜记录、中央厅与调查选择文案。 |
| `CSH-017` | `logic` | [godot_csharp/scenes/Shared/check/WorldScriptCheck/Program.cs:4](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Shared/check/WorldScriptCheck/Program.cs:4) | 回归测试实际执行侦探、守卫、内门闩及arrival/morning项目示例章节。 |

### 编辑器或作者预览

| 编号 | 类型 | 源码位置 | 说明 |
| --- | --- | --- | --- |
| `CSH-038` | `schema` | [godot_csharp/scenes/Shared/Story/Examples/WorldScript/Host/WorldScriptPreview.gd:15](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Shared/Story/Examples/WorldScript/Host/WorldScriptPreview.gd:15) | 作者预览固定以侦探与内门闩控件代表编译故事示例中的实体。 |
| `CSH-018` | `logic` | [godot_csharp/scenes/Shared/check/WorldScriptCheck/preview_ui_check_01.gd:10](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/Shared/check/WorldScriptCheck/preview_ui_check_01.gd:10) | 逐步呈现并切换实际arrival/morning示例章节内容。 |
| `CSH-021` | `schema` | [godot_csharp/scenes/UI/Panels/CampusMap/CampusMap.gd:31](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/UI/Panels/CampusMap/CampusMap.gd:31) | 编辑器地图预览固定使用项目学园地点及其连线。 |
| `CSH-014` | `text` | [godot_csharp/scenes/UI/Panels/Notebook/Notebook.gd:24](C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/scenes/UI/Panels/Notebook/Notebook.gd:24) | 编辑器笔记样例写入窗台水渍、桌钥匙、门锁和夜间离室禁令等案件事实。 |

## 可复核清单与验证

- [解决方案审核与归并清单](solution-review.md)：将全部源码标记与资源记录按主根因合并为 21 组处理建议；对应关系见 [solution-groups.json](solution-groups.json)。仅提出方案，尚未实施改造。
- [全部源码覆盖表](file-coverage.csv)：每个文件的审读方式、分类与保留/排除理由。
- [机器可读标记索引](index.json) 与 [标记表格](index.csv)：源码当前行号、类型、范围和说明；分片原说明如较长则保留在 `audit_note`。
- [正则候选记录](regex-candidates.json)：记录实际候选模式和命中行；正则筛选之后由独立低级模型分片全文审读、主代理复核边界。
- [Godot 资源索引](resource-index.md) 与 [资源机器清单](resource-review.json)：补充筛查 59 份 `.tscn` / `.tres` 的结构与内容字段，外部索引 97 处具体文案、样例或资源绑定。此数量不合并进 196 处源码注释，也不表示 97 条剧情因果规则；生成资源未手改。
- [独立验证目录](verification/)：基线比较、构建和 GDScript 语法检查的原始日志与结论。
- 本轮只做审计标记，没有执行一级、二级或三级剧情/界面/游戏验收；这些等级均未验证。
