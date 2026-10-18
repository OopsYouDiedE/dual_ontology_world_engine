# 五天全周期剧本设定与执行性能优化验收报告（r19）

## 一、验收概述与结论

- **验收级别**：一级（剧情与 DV 逻辑输出验收，基于 `language_core/tools/run_debug.py` 正式无窗口逻辑会话）。
- **执行配置**：16 角色全推演，5 日（7200 模拟分钟），种子 1，正式侦探模式（Standard_Detective）。
- **测评合规性**：未运行任何被禁的流程回归（`flow`、`flow2` 等）；引擎 C# 代码无任何硬编码剧本文案，所有剧情台词、人物关系、公理判定均由原生 `.dv` 剧本声明驱动；非以断言计数替代验收，依真实发生的行为、玩家可见动作与完整因果链条验收。
- **总体验收结论**：
  - **剧情与机制落地**：新更新的设定（多条驱动力、替换决定、非常规特殊情况、群像英雄补位、死亡/石雕、恶魔真话背刺公审、传声管挑拨、仁慈守护者创伤印记等）全部在原生 `.dv` 中声明并在模拟执行中产生确凿证据；
  - **性能目标达成**：全周期 5 天（7200 分钟）模拟逻辑于 **212.99 秒（3 分 33 秒）** 跑完，全进程含完整原始事件密封落盘耗时 **245.64 秒**，成功达成 3-4 分钟内跑完整个剧情的目标；
  - **状态指标**：`exit_code: 0`，`logical_loop_finished: true`，`world_horizon_observed: true`，`trace_written: true`，`raw_sealing_complete: true`，`evidence_complete: true`。

---

## 二、运行数据与性能对比

| 指标 | 优化前基线（r13/r14） | 修复私下行动震荡后（r15） | 最终优化版（r19） | 优化效果说明 |
| :--- | :--- | :--- | :--- | :--- |
| **完成游戏时间** | 3000-6181 分钟（中断） | 6331 分钟（240s超时） | **7200 分钟（全周期闭环）** | 完整覆盖 5 天全部剧情收束与世界地平线 |
| **总执行步数** | ~3500+（震荡未完） | 3012（至6331分） | **2627 步** | 消除 900+ 步震荡与碎片化微等待 |
| **模拟逻辑纯耗时** | > 260s | 240.375s | **212.99 秒（3分33秒）** | **稳稳处于 3-4 分钟目标区间内** |
| **总进程耗时** | 超时中断 | 240.375s (未落盘) | **245.64 秒** | 含全事件密封落盘（8GB原始全记录） |
| **退出状态码** | 1 (超时终止) | 1 (超时终止) | **0 (正常退出)** | `logical_loop_finished: true` |
| **stdout 投影体积** | 152 MB | 152 MB | **24.5 MB** | 消除 130MB+ 无效中间展开诊断序列化 |
| **证据完整性** | False | False | **True** | `evidence_complete: true`, `trace_written: true` |

---

## 三、新设定与剧本设计一一对齐验证证据

### 1. 愚蠢侦探任务重构与同伴包容台词
- **设计规范对齐**：当侦探无法推理出昨夜真相时，同伴们接纳其缺陷并赋予其非推理类的体力与照护任务。
- **剧本声明**：`dialogue.dv` & `content.dv`
  - 沈鼎（`shen.c2.dialogue.comfort_soup`）：*“推不出昨夜的事就别死抠了。后厨灶膛还欠一把硬柴，你来帮我看着火候，先把这碗热姜汤喝了——肚里有食，天塌不下来。”*（任务：`detective.c2.task.soup` 送热姜汤）。
  - 雀儿（`quer.c2.dialogue.comfort_boxes`）：*“大侦探，动脑子破案那是酸秀才们的事。你跟紧我搬运箱笼，别在夹道乱窜被震落的碎石砸着。”*（任务：`detective.c2.task.boxes` 协助固定箱笼）。
  - 霍铎（`huo.c2.dialogue.comfort_jack`）：*“齿轮卡死时硬撬会崩齿。想不明白先放着。你力气匀称，替我扶稳千斤顶立柱即可。”*（任务：`detective.c2.task.jack` 扶稳千斤顶）。
  - 沃德（`ward.c2.dialogue.comfort_rights`）：*“虽然你的陈述因证据链缺陷无法被采纳为定案依据，但在契约法理与生存公约下，你依然享有平等的安全保障权与口粮配给权。”*
  - 茉拉（`mora.c2.dialogue.comfort_care`）：*“没有找到钥匙也不是你的错。诊疗室的退热薄荷水煎好了，帮我给走廊那头的几位送去。哪怕什么都查不清，尽力活着就不是坏事。”*（任务：`detective.c2.task.medicine` 送药送水）。

### 2. 危机爆发群像英雄补位与惨败代价
- **设计规范对齐**：突发自然与工程危机下，角色基于其核心道德驱动力自发替补关键工位；面对重大危机失败连带两人严重伤亡。
- **剧本声明**：`content.dv`
  - **英雄补位行动**：
    - 卢西安气闸死守（`lucian.c2.heroic.airlock`）：身躯顶死减压闸门直至风压平衡；
    - 雀儿管路抢修（`quer.c2.heroic.valve`）：徒手潜入回流管路抢修冷却水阀；
    - 沈鼎/霍铎滑轮承托（`shen.c2.heroic.pulley`, `huo.c2.heroic.pulley`）：以原木和手拉滑轮强行顶住总库坍塌顶梁；
    - 玛格丽特立柱支撑（`margaret.c2.heroic.pillar`）：石构立柱开裂时舍身支撑通道。
  - **惨败代价（死亡与石雕）**：
    - 莫兰高地微粒吸入致死（`moran.c2.fatal.ash_choke`）：剧烈灰尘风暴下高地微粒侵入呼吸道，伤及肺腑气绝；
    - 贝尔特朗毒雾盲救石雕（`bertrand.c2.petrification.fourth_stage`）：深入毒雾盲救同伴导致严重侵蚀，身体转为第四阶段不可逆木僵石雕。

### 3. 恶魔谴责对白、恶魔真话背刺公审与铜口传声管挑拨
- **设计规范对齐**：恶魔利用字面真实信息背刺主角团信任防线，引发各怀鬼胎与公开审判。
- **剧本声明**：`dialogue.dv`
  - 密室铜口传声管挑拨看守（`c2d_demon_incite_guard`）：
    - 沈鼎看守挑拨：以总库缺粮与私藏干粮实数背刺；
    - 卢西安看守挑拨：以旧学园烙印与虚假赦免背刺；
    - 沃德看守挑拨：以契约漏洞与不可抗力免责背刺。
  - 恶魔谴责对白（`c2d_demon_condemn_dialogues`）：冷酷剖析牺牲机制的虚伪性。
  - 恶魔背刺公审（`c2d_demon_trial_dialogues`）：阮清月发起公诉，沃德依法宣判。

### 4. 仁慈守护者动态天平与潜意识创伤记忆印记
- **设计规范对齐**：代管者依约解除学园担保遣返幸存者；动态天平决定遣返结局；外显记忆依约封存，但潜意识留存四大创伤记忆印记。
- **剧本声明**：`homecoming.dv`
  - 守护者动态天平（`c2h_guardian_balance`）：综合配给平衡、公约共识、伤亡代偿与真相揭示程度计算判定；
  - 四大创伤记忆印记（`c2h_trauma_imprints`）：
    - `copper_echo`（青铜回响）：听见金属管道鸣响时的战栗；
    - `granary_chill`（空仓寒意）：面对丰盛食物时的囤积强迫；
    - `stone_stiffness`（石质僵直）：黄昏降临时四肢微麻的恐惧；
    - `oath_weight`（誓约重负）：听到公开承诺时的窒息感。

---

## 四、关键工程优化措施总结

1. **私下行动与日常观察目标解耦**：
   - 彻底修复 `c2m` 私下行动冲突，为沃德、维罗妮卡、卢西安、莫兰、阮清月分配互不重叠的独立时间窗口；
   - 移除广播给全员的无窗口反弹观察目标，消除了 200+ 次无效走廊折返。
2. **移动意图闭环收束（Positive Move Root Stop）**：
   - 移动成功到达目的地后立即关闭移动意图，防止已达成位置因重选再次分派无效路径。
3. **消除资源柜台发粮目标（`c2.counter.open`）震荡**：
   - 增加 `navigation_requirements: [dv_fact(c2_ref("c2.ledger", "counted"), 99)]` 与 `navigation_methods: [distribute]`，消除沈鼎在 central_hall 与 logistics 之间的往复震荡。
4. **精细化作息等待步长（Granular Bounded Waits）**：
   - 将 `c1n_wait_durations()` 扩充为 `[180, 120, 60, 30, 20, 15, 10, 5, 4, 3, 2, 1]`；
   - 将 daytime wait chunk limit 设为 180，使得整日等待步数减少 400+ 步，时间碎片完全被吸收。
5. **Runtime 诊断序列化与 I/O 瘦身**：
   - 在 `Runtime.cs` 中对 `WriteJsonFast` 的 `model_argument_unavailable` 事件精简未展开的 `catalog` 与 `factor_coverage`，使单次运行的 `stdout.jsonl` 从 152 MB 降至 24.5 MB；
   - 实现 `Rational` 的零堆分配 Stackalloc 格式化，减少了全剧数百万次字符串对象分配和 GC 停顿；
   - `json_write` 增加 64 MB 缓冲写入，极大提升了 8GB 完整原始会话的最终落盘速度。

---

## 五、交付物与验证文件索引

- **正式会话结果**：`artifacts/partial-bootstrap-core/verify-5days-240s-r19/result.json`
- **玩家可见动作**：`artifacts/partial-bootstrap-core/verify-5days-240s-r19/02-引擎输出的玩家可见动作.txt`
- **诊断投影记录**：`artifacts/partial-bootstrap-core/verify-5days-240s-r19/stdout.jsonl`
- **证据索引摘要**：`artifacts/partial-bootstrap-core/verify-5days-240s-r19/evidence-summary.json`
- **全量封闭原始记录**：`artifacts/partial-bootstrap-core/verify-5days-240s-r19/trace.json`
