# 公共菜单历史范围独立机制验证 r08

本轮仅验证生产通用机制与中性公开候选。独立验证 Agent 未读取设计文件、正式 `.story`、历史试玩或其他验收证据。修改只涉及 `PlayerControllerCheck` 中性夹具及本目录证据；未修改生产、Core、AGENTS 或生成物，未运行 FullTest，未提交。

## 基线负例

- Release 控制器检查项目构建通过，无警告、无错误。
- 新增 `PublicMenuScopeCases` 在三个中性标签下重复同一序列：与 A 交互 → `Purpose=panel` → 同 A 再查看 → 与 B 交互 → 同面板 → 第三人物 C → 同面板 → 检视无关物件并取得无关材料 → 同面板。
- 首次面板候选评分为 8；在 A 上重复时为 -4，正确保留 `revisit_without_new_accepted_material:1` 并去除 `authored_rule:prefer:untried:6`。切到 B、C 和无关物件后均错误恢复到 8，并重新获得未尝试奖励。
- 回归在 `panel_switch_to_b_retains_repeat_cost_neutral-common-topic` 首次失败时中止，退出非零；失败前已保存所有中性序列的独立观察和预期结果，完整原始输出已保存。
- A/B 相同标签的实际人物话题独立，回到 A 保留旧历史；公共面板跨空间独立且返回保留旧历史。这些对照观察成立。
- 相同可见标签但不同用途的面板/人物话题，在两种选择顺序下也共用历史，观察不成立。

`baseline-build.log`、`baseline-check.log` 保存原始合并输出字节；`baseline-manifest.json` 记录命令、退出码、字节数、SHA256 与被测源文件哈希。封存日志关闭换行转换与文本 diff。

## 修复后验证

最终 Release 控制器回归通过，最新 Debug 宿主构建通过，构建无警告、无错误。基线日志与基线清单未覆盖。

- `Purpose=panel` 首次仍取得未尝试奖励；同 A 重复及切到 B 后面板评分均为 -4，保留原重复代价，未重新取得 `authored_rule:prefer:untried:6`。随后切到 C 和检视无关物件/取得无关材料也保留历史；面板再次被选择后，真实累计尝试导致评分为 -10。
- 三种中性标签结果一致，不凭标签猜用途。相同标签的面板与人物话题在两种选择顺序下使用独立历史。
- A/B 的实际话题菜单仍独立，回到 A 仍保留 A 的旧尝试；公共面板的空间隔离及返回旧空间的历史保留通过。

正式元数据链完成源码复核：`AgentPilot.gd` 的 `_field_offers` 对 Tab、R、M 公共面板入口统一发出 `"purpose": "panel"`；`GameEngine.cs` 将该字段读取为 `PlayerFieldOffer.Purpose`，`PlayerCandidateSession` 再原样投影为公开候选用途；`HistoryKey` 为这类菜单选择独立 `panel` 方法及当前空间/公开入口标签键，人物话题保留人物上下文。此复核为源码链核对，没有执行 GDScript 或 Godot 场景。

`final-build.log`、`final-check.log`、`debug-host-build.log` 保存原始合并输出字节。`final-manifest.json` 保存最终命令、退出码、字节数、SHA256 与被测源指纹，`source-review.json` 保存元数据链复核位置及源指纹。基线和最终封存日志的字节数与哈希重新核对一致。

## 验收范围

这是独立自动机制回归，构建与公共菜单历史范围机制通过。人物行为合理性、一级剧情与引擎验收、二级 UI、三级盲玩与游玩体验均未验证；不以本轮机制结论替代上述验收。本 Agent 未运行 Godot、FullTest 或 Core 全量回归。
