# S3 无侦探：当前候选与真实因果核验

当前实现 HEAD 为 `57b786e2`（包含 main `23cc359b`）。**S3 尚未通过：成见已真正提前采纳，实际表面答复已提交并获接受，但新增愤怒状态与初始否定发生同权冲突，不能把其推导事件称为已生效的愤怒。** 本报告只引用当前固定目录，不复用已替换运行的数据。

## 已实施的源码

- `40-chapter1.dv:2938`：本人实际初查后开放理由话题与假设；强成见私人 prior 为 0.8、弱成见为 0.6，不写入权威理由事实。此前依赖本人答复 receipt 的阻断已移除。
- `40-chapter1.dv:3335`：私人评估核对本人实际取得的具体冲突材料，而不是把 report_basis 旗标等同愤怒。甲区核对质问、钥匙、私下取物残纸与无公开授权；丙区核对伤踝、半幅草图、拉闸者自述及弃一保三计算；丁区核对酸泡、无医嘱、试剂取用者与瓶号。
- `40-chapter1.dv:3343`：居民有真实材料检视、定向提问、当事人回想与回复的路径。复用既有深层线索，不注入答案；shared_inspection 沿用同组实际共同检视投影。调查与报告的目标动机试图消费私人评估，行动目录仍保留完整路径。
- 普通居民完整答复使用独立 complete_receipt/complete_level、接受法则与真实裁决者现场条件，不能用身份 receipt 冒充完整理由已接受。正确理由及情绪修订要求本人逐项完整材料链，不只看公共末端旗标。
- `30-session.dv:1524`：定向回复按同一 parse_event 关联本人 received_requests 与 received_speeches，核对 requester 及真实请求行动 ID；requires_actual_request 现在有实际资格消费者，Complete 消费匹配项。
- 普通表面答复复用已有保守正文，只报材料支持的行为，不把偷窃、单方面弃人或试验动机作为已证明事实。**正文变保守不是因果验收通过的依据。**

这些是用户新授权后的内容与动机策略变化，形成新基线，不是同条件缓存优化。没有读取设计文件，没有让普通居民读取 detective_profile，没有按身份降低规划深度，没有预置答案或伤亡。

## 当前正式会话

命令：

`python mcp/verification/run_debug.py --out artifacts/perf-opt/aggressive/branch-npc --timeout 300 --causal-timeout 1 --detective-profile Standard --detective-zone none --plan-depth 3`

全部默认优化开启，所有人物统一深度 3，formal-model 不含 Godot 几何。用户明确要求并行测试，本轮与其他分支存在资源竞争，不能据此作独占性能比较。

| 人物 | 本人先采纳的私人假设 | 冲突评估推导事件 | 真正选择表面答复 | 真正执行 / 接受 |
| --- | --- | --- | --- | --- |
| ward | e249，481，theft，prior 0.8 | e518/e519，483 | e658，484 | e981 / e984，494 |
| elena | e271，481，self_preservation，prior 0.8 | e668/e669，484 | e1401，664 | e1495 / e1498，674 |
| mora | e286，481，experiment，prior 0.8 | e1318/e1319，662；e1368/e1369，663 | e1379，663 | e1414 / e1417，673 |
| selma | e276，481，self_preservation，prior 0.6 | e1360/e1361，663 | e1405，664 | e1508 / e1511，674 |

时间为正式游戏分钟。选择、执行与接受是不同事件；不能用 e658 等选择记录代替之后成功执行。选择记录确实带着对应的私人理由假设，且引用真实 hypothesis 事件。

本轮全部实际寝区答复都走表面人物答复；没有执行 resident/detective truth 行动，没有正式提交带 `.reason.` 的完整答复。因此只能说完整路径已经声明，不能声称本轮居民实际查明深层真相，亦不能由此推断 Standard 在场的运行效果。本分支没有另跑 Standard 在场测试。

## 发现的实质失败

直接读取当前原始 DAG 的 inference.data.mind_after，得到：

| 事件 | 私人 angry 实际值 | confidence | conflicted |
| --- | --- | --- | --- |
| e518 / e519（ward） | false | 0.5 | true |
| e668 / e669（elena） | false | 0.5 | true |
| e1318 / e1319、e1368 / e1369（mora） | false | 0.5 | true |
| e1360 / e1361（selma） | false | 0.5 | true |

原因可定位到 `c1_other_inquiry_scene`：它先以 `dv_field(false)` 建立确定的初始 angry=false，随后通过 infer 增添同信任度的 angry=true claim。共享认知机制保留两项矛盾来源，并没有把初始状态当可替换的情绪变量。以 ward e518 为例，原始 claims 同时保留 initial=false/trust=1 与 ward.appraise-conflict=true/trust=1；不能把“出现 appraise-conflict 事件”当作 angry=true 已成立。

因此，调查目标中的 angry=false 与报告目标中的 angry=true 均可能面对争议值，而非预期的情绪转移。当前三房仍作表面答复，不能归因为已经实现的愤怒意愿机制；更不能把成见事件与实际表面作答的时间先后单独当作充分因果证明。强 prior 影响私人假设选择，但评估法则当前只依据既有成见声明的存在及材料冲突，并未显式消费实时 hypothesis 选择，这是另一项因果解释边界。

**本轮按用户最新要求保留已有测试，不重跑，也不在测试后修改实现来让报告对应不同源码。** 后续修复需要把可变化的私人情绪评估与不可直接覆盖的事实 claims 分开，保持实际材料和本人来源，并在新授权窗口验收。

## 完成、正文与封存

- 正式引擎退出码 0，实际会话 190.188 秒；构建 32.937 秒，均与离线取证分列。
- 实际客观收束在游戏 6330；quer 于 e30101、游戏 6331 成功执行章末检视。停止原因同时包括 objective-chapter-two-closing-record 与 actual-viewer-closing-inspection；不是仅看到候选或到达终点。
- source_unchanged=true，run_config_matches=true，logical_loop_finished=true，evidence_complete=true。当前 raw DAG 为 542,706,885 字节、30,104 个已提交事件；final checkpoint 与 seal 均存在，未投影尾部为 0。
- 原始 DAG SHA-256：`35dd0061545bb862becd65ad16e0766dd86ccc4805acde97c2c8eb9d75ff22ee`。40 源码 SHA-256：`04861c12df58a7e960eee7a58af46f1379a8b7d9357b32217c365a23ac785648`；30 为 `8cc285cc42d0fa7549f60a3eeb8049b386ec6b74fe043337d4182b1bf7b302ad`。
- 当前 `02-引擎输出的玩家可见动作.txt` 为 478,063 字节，已实际打开核对正文及保守表面答复；没有以标签替代全文。
- 按主代理要求只读自动全因果检查限 1 秒，causal_check_status=timeout、causal_check_complete=false；runner 整体退出码 1 来自该检查未完成，不能写成引擎会话崩溃，也不能写成全因果验收通过。main_causal_supported=false。
- 源码语法/名字/调用/已实现类型检查完成，Release 构建由当前 runner 成功执行；未执行 Godot UI、碰撞或独立盲玩。尝试创建新独立会话审查 Agent 时触及团队线程上限，未获得新的独立会话审查结论；主代理已有独立源码审查与本轮修正分列。
- `.gitignore` 已恢复；巨大原始 DAG、生成程序和全文均留在本工作树固定目录，提交仅含源码及小报告/指纹/结果摘要。无本轮临时脚本遗留。
