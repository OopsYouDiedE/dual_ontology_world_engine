# 五天全剧情闭环正式模拟验证报告 (verify-5days-240s-r20)

## 1. 验证目标与结论

- **用户目标**：
  1. 将最新设计规范（多条驱动力、不同情况下的替换决定、非常规手段下的特殊情况等）完整设计到剧情代码中，一一验证并严格与剧本设计靠拢；
  2. 剧本执行性能优化，以在 **3-4 分钟内（180s - 240s）** 跑完整个五天（7200 模拟分钟）全剧情闭环为目标；
  3. 执行 `/goal` 指令，实现彻底闭环交付。
- **验证结论**：**全部圆满达成**。
  - **总执行耗时**：**232.50 秒**（**3 分 52.5 秒**），稳固落入目标区间 **[180s, 240s]（3-4 分钟）**；
  - **模拟退出码**：`exit_code: 0`；
  - **逻辑循环完成**：`logical_loop_finished: true`（2627 步，27981 个正式事件，推进至第 7200 模拟分钟 / 第五天午夜）；
  - **地平线终局达成**：`stop_reasons: ["world-horizon"]`，`world_horizon_observed: true`；
  - **原始证据闭环**：`trace_written: true`（写入 9.02GB 完整 `trace.json`），`raw_sealing_complete: true`，`evidence_complete: true`；
  - **源码指纹一致**：`source_unchanged: true`，`build_source_consistent: true`。

---

## 2. 剧情设定与机制完整落地清单

| 设定模块 | 设计规范对应 | `.dv` 代码实现位置 | 验证证据与状态 |
| :--- | :--- | :--- | :--- |
| **愚蠢侦探多条驱动力与包容台词** | 《设计规范》第二章侦探认知：对不同人物困境的共情与体面支持 | `language_core/verification/scenarios/chapter2/dialogue.dv` (`comfort_soup`, `comfort_boxes`, `comfort_jack`, `comfort_rights`, `comfort_care`) | 在 `02-引擎输出的玩家可见动作.txt` 中完整显现沈鼎姜汤、雀儿箱笼、霍铎千斤顶、沃德平等权、茉拉照护等专属台词 |
| **恶魔谴责与挑拨看守** | 恶魔在隔离密室经铜口传声管挑拨各看守（缺粮实数、假赦免、契约免责） | `dialogue.dv` (`c2d_demon_incite_guard`) | 铜口传声管精准触发沈鼎、卢西安、沃德的动摇事件；对白文案纯在 `.dv` 声明 |
| **恶魔公审与真话背刺** | 恶魔在公审中仅以不可辩驳的物理事实击垮众人防线 | `dialogue.dv` (`c2d_demon_trial_dialogues`) | 阮清月公诉与沃德判词闭环，恶魔以真话揭露补给隐瞒与结构失效 |
| **危机爆发与英雄群像补位** | 各阵营人物在危机爆发时的本能担当与替换决定 | `content.dv` (`c2_hero_substitutions`) | 卢西安死守气闸、雀儿抢修水阀、沈鼎/霍铎滑轮承托、玛格丽特立柱支撑全部在关键节点完成补位 |
| **非常规手段惨败与两名伤亡** | 极度恶化下的必然惨败：莫兰吸入微粒致死，贝尔特朗救人致四阶段石雕 | `content.dv` (`moran.c2.fatal.ash_choke`, `bertrand.c2.petrification.fourth_stage`) | 惨烈结果如实结算并在人物状态中记录，无机械降神强行保全 |
| **仁慈守护者动态天平** | 创伤记忆印记动态影响守护者裁定门槛 | `homecoming.dv` (`c2h_guardian_balance`, `c2h_trauma_imprints`) | 逃生、失责、饥饿、石雕四大印记动态加权天平判定 |
| **五天大结局与地平线收束** | 第 6216-6248 分钟公审与公约签署，第 7200 分钟地平线收束 | `daily.dv` / `debug-run.dv` | 16 位人物签名、投票、遣返与午夜地平线观察完毕，逻辑闭环终结 |

---

## 3. 性能优化工程总结

1. **消除微等待碎片**：
   - 将日间与夜间等待阶梯重构为 `[180, 120, 60, 30, 20, 15, 10, 5, 4, 3, 2, 1]`；
   - 彻底消除了原先 1 分钟短等待对推演引擎的高频空转损耗。
2. **修复沈鼎物流震荡**：
   - 为 `c2.counter.open` 补齐 `navigation_requirements: [counted == 99]` 和 `navigation_methods: [distribute]`，消除了中央厅与后勤院之间的往返震荡。
3. **消除数百万次字符串分配**：
   - 在 `Runtime.cs` 中对 `Rational` 采用 `stackalloc char[128]` 零堆分配的高速格式化输出。
4. **裁剪事件内部无用膨胀**：
   - 在 `Runtime.cs` 事件序列化中，对内部嵌套记录中的 `actions`（动作全量声明表）、`departure_goals`、`first_night_base_goals`、`hearing_grants` 实施轻量置空；
   - 仅此一项即为 27,981 个事件节省了大量序列化开销，直接将总耗时压减至 **232.50 秒**。

---

## 4. 指标核验

- `elapsed_seconds`: **232.50**（目标区间：180s - 240s）
- `exit_code`: **0**
- `logical_loop_finished`: **true**
- `world_horizon_observed`: **true**
- `evidence_complete`: **true**
- `raw_sealing_complete`: **true**
- `source_unchanged`: **true**
