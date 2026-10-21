# DV Core 统一命令行 UI（TUI）与智能可视化回放架构设计

## 一、 核心设计哲学：如何让“自动决策”、“AI 手动决策”和“录像回放”拥有 100% 一致的体验？

在传统的游戏引擎中，“跑 AI 自动测试”、“让外部大模型/人工敲命令玩”和“看历史录像回放”往往是三个完全割裂的工具，界面长相、日志格式完全不同。

为了实现你要求的**“完全一致的体验”**，我们在 `dv_core` 的宿主桥接层（`Session.cs` 之上）引入一个核心抽象——**“实时即回放（Live-as-Replay）”与“统一决策帧（`DvTerminalFrame`）”**：

1. **唯一的数据契约（`DvTerminalFrame`）**：
   无论当前这步棋是**内置规划器 (`dvp_plan`) 自动算出来的**，还是**外部 AI Agent / 人类通过命令行手动选的**，亦或是**从 `.dvdag` / `.dvlog` 历史文件里读出来的**，它们在进入 UI 渲染器之前，都会被归一化为完全相同的 `DvTerminalFrame` 数据结构。
2. **统一的“决策透视盒（Unified Decision Box）”**：
   在界面右侧的决策区，永远展示**当前主体的合法候选动作表 (`options`)** 以及**最终被选中的动作 (`selected_option`)**。唯一的区别仅在于决策来源标签与附加理由：
   * 当处于 **内置自动决策 (`AUTO: dvp_plan`)** 时：附加信息显示规划器的启发式评分拆解（如 `Score: +1.45 | Info: +0.8, Progress: +0.9, Cost: -0.25 | Stop: current-affordance-root`）。
   * 当处于 **外部 AI / 手动决策 (`MANUAL: AI-Agent / CLI`)** 时：附加信息显示外部 AI 提交的思维链摘要（`Reason: "铜壶有煤灰，需先确认夜间是谁生火"`）或等待你在底部输入 `act <编号>`。
3. **“实时运行”只是游标停在最后一帧的“回放”**：
   因为每一拍都生成标准帧并存入环形缓存（或 `.dvdag` 流），所以哪怕你正在跑 **实时自动决策** 或 **外部 AI 对局**，你随时按 `Space` 就能暂停，按 `←` / `→` 就能像看录像一样倒退回前几步检查某个 NPC 刚才脑子里想了什么，按 `L` 键又能瞬间回到实时最新帧（`LIVE`）！

---

## 二、 界面视觉布局：五区合一的简洁终端 UI（TUI）

终端界面采用纯字符框线（Box-Drawing）与高对比度语义高亮设计（在 C# 中可由 `Spectre.Console` 或原生 ANSI 序列零依赖渲染，支持标准 $120 \times 36$ 终端窗口）：

```text
┌──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ [DV-CORE STUDIO] 第 1 章 · 初醒  | 🕒 第1日 07:15 (435m) | 💳 预算: 14/240 | 模式: [⚡ AUTO: dvp_plan] | 剔除停滞: 18.4s │
├──────────────────────────────┬──────────────────────────────────────────────────┬────────────────────────────────────┤
│ 🗺️ 空间拓扑与角色分布 (Map)    │ 📜 叙事、法则结算与话语流 (Action & Speech)        │ 🧠 双本体透视与统一决策盒 (Ontology) │
│                              │                                                  │                                    │
│ [living_floor · 生活区]       │ ── Beat #014 [COMPLETE: applied] ──────────────  │ 【当前主体】detective (侦探)        │
│                              │ ▶ 执行主体: detective (侦探)                      │ 【决策模式】AUTO (dvp_plan 深度=1)  │
│  ┌──────────┐  ┌──────────┐  │ ▶ 正式行动: inspect_E1 (检查水房铜壶)              │ ────────────────────────────────── │
│  │ dorm_r01 │  │ dorm_r02 │  │ ▶ 耗时结算: +5m (07:10 -> 07:15) | 精力: -1       │ 🎯 候选动作与决策依据 (Options):    │
│  │   (空)   │  │  [苏 su] │  │                                                  │ >[01] inspect_E1 | 检查铜壶 ★选中   │
│  └────┬─────┘  └────┬─────┘  │ 💬 【观察呈现 / Presentation】                    │       └─ Score: +1.65 (Info +1.80) │
│       🚪开放         🚪开放    │ 「铜壶底部留有新鲜的煤烟薄灰，壶身尚存温热，        │  [02] talk_slate | 询问霍石板代号   │
│  ┌────┴─────────────┴─────┐  │   说明今晨有人在唤醒钟响前使用过炉火。」            │       └─ Score: +0.90 (Comm +1.10) │
│  │      living_hall       │  │                                                  │  [03] move_r02   | 前往二号宿舍     │
│  │  ★[侦探] [霍 huo]      │  │ 🔍 【感知授予 / dv_perceive -> dv_update_mind】   │       └─ Score: +0.20 (Cost -0.20) │
│  │   [沈 shen]            │  │ + E1.kettle_warm = true (置信度: 1.00, 亲眼观察)  │ ────────────────────────────────── │
│  └────┬─────────────┬─────┘  │ + E1.soot_fresh  = true (置信度: 1.00, 亲眼观察)  │ ⚖️ 双本体状态对比 (Physical vs Mind)│
│       🔒门闩         🚪开放    │                                                  │ • E1.soot_fresh : 真=true | 知=true │
│  ┌────┴─────┐  ┌────┴─────┐  │ 🔊 【同房公共传播 / dvs_public_grants】           │ • su.location   : 真=r02  | 知=❓未知│
│  │  pantry  │  │ washroom │  │ • 同房可见者: huo, shen 观察到侦探检查了铜壶       │ • wedge.holder  : 真=shen | 知=❓未知│
│  └──────────┘  └──────────┘  │                                                  │ • 侵蚀度(Erosion): 0.05 (正常可行动)│
├──────────────────────────────┴──────────────────────────────────────────────────┴────────────────────────────────────┤
│ ⏯️ [PLAYING 1.0x]  Beat 014 / 042  ━━━━━━━━━━●───────────────────────  ⏱️ 真实耗时: 14.8s -> 智能节拍: 0.8s (已省 14.0s)│
│ ⌨️ 快捷键: [Space]暂停/播放  [←/→]单步回退/前进  [1/2/4/8]倍速  [M]切换自动/AI手动  [Tab]切换观察角色                      │
│ dv> act 01 --reason "先核实水房铜壶的热度与煤灰物理证据"                                                                │
└──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 三、 智能去停滞回放算法（Smart Time-Compressed Pacer）

你在需求中特别强调：**“允许回放实际操作，忽视掉停滞时间并按照合理间隔播放”**。

### 3.1 为什么原始日志不能直接按时间戳回放？
在真实游玩或外部 AI 测试产生的 `.dvlog` / `.dvdag` 中，两个事件之间的真实墙钟时间差 $\Delta t_{\text{wall}} = t_{i} - t_{i-1}$ 混杂了三种完全无意义的停滞：
1. **人类阅读与发呆时间**：玩家看着屏幕思考了 $25\text{ s}$ 才点击下一个热点。
2. **外部大模型 API 延迟**：外部 AI Agent 调用 LLM 推理耗时 $8.5\text{ s}$。
3. **引擎后台认知卡顿**：NPC 在后台跑 `dvs_prepare_speech_choices` 卡了 $1.2\text{ s}$。

### 3.2 语义节拍归一化公式（Semantic Beat Pacing）
回放器在读取操作流时，**完全丢弃 $\Delta t_{\text{wall}}$**（仅在界面右下角统计“已为你节省 $X$ 秒停滞时间”），而是根据当前帧的**动作语义类型（Beat Kind）**、**文本字数 $L$** 与**播放倍速 $S \in \{1, 2, 4, 8\}$**，动态计算合乎人类阅读直觉的播放间隔 $\Delta t_{\text{play}}$：

$$\Delta t_{\text{play}}(e, S) = \frac{1}{S} \cdot \begin{cases} 
0.25\text{ s}, & \text{若 } e \in \text{环境时间推进 / 空闲调度 (schedule\_tick)} \\
0.40\text{ s}, & \text{若 } e \in \text{房间移动 / 跨门位移 (move)} \\
0.50\text{ s}, & \text{若 } e \in \text{决策门定格展示高亮选项 (decision\_gate)} \\
0.65\text{ s}, & \text{若 } e \in \text{现场调查 / 法则交互 (inspect / law)} \\
\text{clamp}(0.80 + 0.035 \times L,\; 0.80,\; 2.50)\text{ s}, & \text{若 } e \in \text{人物话语 / 观察呈现 (speech / presentation)} \\
1.10\text{ s}, & \text{若 } e \in \text{脑内重演 / 正式答案提交 (replay / answer)}
\end{cases}$$

* **视觉节奏效果**：
  * 当角色连续走过 3 个房间时，你会看到左侧地图上的光标以 `0.4s -> 0.4s -> 0.4s` 的轻快节奏连贯跳跃；
  * 当触发调查或对话时，决策盒先亮起 `0.5s` 让你看清他选了哪项（以及为什么选这项），随后中间叙事流平滑弹出文字并停留约 `1.2s` 供你扫读，**全程零卡顿、零死寂等待**！

---

## 四、 Core 内置的双协议接口设计（兼顾人类可视化与外部 AI 控制）

为了让 `dv_core` 既能由人类打开 TUI 直观调试，又能被外部 AI Agent（如 Claude Code / MCP / 自动化测试脚本）通过纯文本管道无缝控制，我们在 `dv-cli` 中提供两种共享同一套底层帧数据的运行协议：

### 4.1 模式一：交互式可视化终端 (`dv-cli tui`)
* **适用场景**：开发者实时观察内置 AI 跑局、手动介入某一步决策、或者回放外部 AI 刚刚跑完的 `.dvdag` 录像。
* **无缝切换控制权（Hot-Swap Controller）**：
  * 在回放或自动运行途中，按下 `M` 键，即可在任意一帧将当前控制权从 `[AUTO: dvp_plan]` 切换为 `[MANUAL: CLI]`，直接从该历史节点分叉（Fork）输入你自己的指令！

### 4.2 模式二：对 AI 极度友好的纯文本流协议 (`dv-cli agent-stdio`)
如果外部 AI Agent 直接通过标准输入输出（`stdin/stdout`）控制 Core，画满框线的全屏 TUI 会浪费 Token。因此，`--protocol agent` 会将同一个 `DvTerminalFrame` 渲染为**零冗余、确定性的结构化纯文本块**（同时后台依然实时写入 `.dvdag`，供你在另一个窗口用 TUI 同步观看）：

```text
=== TURN #014 | CHAPTER 1 | TIME Day1 07:15 (435m) | ACTOR detective [AWAITING_INPUT] ===
[LOCATION] map=living_floor, room=living_hall
[PEERS_IN_ROOM] huo (霍), shen (沈)
[RECENT_OBSERVATIONS]
- (07:15) E1.kettle_warm = true [confidence=1.00, source=inspect_E1]
- (07:15) E1.soot_fresh  = true [confidence=1.00, source=inspect_E1]
[AVAILABLE_ACTIONS]
[01] talk_slate_code   | kind=talk    | cost=5m,1e | target=huo       | desc="向霍询问石板代号"
[02] talk_cups_known   | kind=talk    | cost=5m,1e | target=shen      | desc="向沈询问茶杯数量"
[03] move_living_r02   | kind=move    | cost=2m,0e | to=dorm_r02      | desc="前往二号宿舍"
[04] replay_sound_test | kind=replay  | cost=10m,2e| hypotheses=3     | desc="【重演】声学传导推演"
[05] wait_15           | kind=wait    | cost=15m,0e|                  | desc="原地等待 15 分钟"
=== COMMAND_PROMPT (Reply with: ACT <id|index> [--hyp <h_id>] [--reason "<text>"]) ===
```

当外部 AI 回复：
```text
ACT 01 --reason "铜壶刚被用过，先向同在生活大厅的霍核对石板代号与晨间动向"
```
Core 会立刻：
1. 将 `--reason` 记录进该步的 `decision_meta`（这样你在可视化回放器里看录像时，就能在右上角决策盒里直接看到外部 AI 当时的思考理由！）；
2. 执行 `Prepare(talk_slate_code) -> Formal Execute -> Complete`；
3. 输出下一回合的观察结果与对白。

---

## 五、 C# Core 宿主接口代码落地结构 (`DvTerminalHost.cs`)

在重构后的 `dv_core/host_bridge/` 中，只需新增一个轻量级的 `DvTerminalHost` 类，即可同时支撑上述所有能力：

```csharp
public sealed record DvOptionItem(
    int Index,
    string ActionId,
    string Kind,
    string Title,
    string CostText,
    string? HeuristicScore,      // AUTO 模式下由 dvp_plan 提供
    string? HeuristicBreakdown   // 如 "Info:+1.8, Cost:-0.2"
);

public sealed record DvOntologyDiffItem(
    string RefKey,
    string PhysicalValue,        // 客观世界真实值 (仅在调试/回放透视面板显示)
    string MindValue,            // 当前角色主观认知值
    double Confidence,
    bool IsNewlyChanged
);

public sealed record DvTerminalFrame(
    int BeatIndex,
    string EventId,
    int Chapter,
    int ClockMinutes,
    string ClockText,
    string Mode,                 // "AUTO_PLAN" | "AI_MANUAL" | "HUMAN_CLI"
    string Phase,                // "DECISION_GATE" | "EXECUTED_COMPLETE"
    string ActorId,
    string MapId,
    string RoomId,
    IReadOnlyList<DvOptionItem> Options,
    string? SelectedActionId,
    string DecisionReason,       // dvp_plan 的 stop/score 摘要，或外部 AI 的 --reason
    IReadOnlyList<string> PresentationLines,
    IReadOnlyList<DvOntologyDiffItem> OntologyDiffs,
    double RawWallDeltaSec,      // 原始日志耗费的真实秒数（含发呆/卡顿）
    double SmartBeatSec          // 去停滞算法计算出的标准播放秒数
);
```