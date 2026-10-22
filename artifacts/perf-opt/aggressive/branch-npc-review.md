# S3 无侦探：当前私人评价修复及正式结果

当前实现 HEAD `e5660d9e`，合并 main `6ce6d133` 后实施。**上一轮 angry 的初始否定与后续肯定冲突已修复：当前正式会话中三房报告者的 angry 实际为 true、conflicted=false、claims=[]，其值进入本人的调查／报告目标条件并影响实际候选估值。** 这不等同于完整独立 S3 因果验收，未执行的深层路径与反事实仍保留边界。

## 通用接口及内容接线

`30-session.dv` 新增 policy.current_appraisals。目标 ref 必须显式归本人 subject 且限 appraisal facet，不能写权威世界或普通事实槽。声明读取本人实际输入，并可要求本人当前已选 hypothesis_id；确定成立输出 value，确定否定输出 otherwise，输入未知则撤回为未知。更新替换当前评价槽、提升 mind revision，并以 private_appraisal_updated 记录 before/after、具体输入、假设事件与来源。它不新增互斥事实 claim，不改变旧来源信任度，也不靠提高新置信度压过旧值。输入来源更新即刷新当前来源；输入未变不重复发布。禁止直接自依赖。

自动认知在本人推理及假设选择后刷新当前评价，人工轻量认知也使用同一接口。保留完整策略与知识输入，不按人物身份降低搜索深度。旧 appraise-conflict/revise-appraisal infer 法则从内容层移除。

`40-chapter1.dv` 将既有成见与本人实际材料冲突声明到该接口。当前所采纳的理由假设必须与该个人成见相符才产生相应愤怒；失去该立场或材料支持、实际取得完整证据时可修订。甲区读取本人已知质问、钥匙、私下取物残纸及无公开授权；丙区读取伤踝、半幅草图、拉闸自述及弃一保三计算；丁区读取酸泡、无医嘱、实际取用者与瓶号。report_basis 旗标本身不等同愤怒。

三房承担报告任务的 ward/selma/mora：继续调查目标要求本人 angry=false，报告目标要求本人 angry=true 或本人完整证据链。实际行动仍全部保留并走正式资格与 Complete。居民真实检视、定向提问、当事人回想／回复、完整答复及独立 complete_receipt/complete_level 路径均保留；shared_inspection 沿用实际共同检视。新接口没有读取 detective_profile。

## 有限工程验证与失败处理

首次新接口会话曾在启动阶段报 Expected List：无假设时误把 false 当记录读取 id。该失败真实发生，未用别轮结果补齐。`e5660d9e` 已修复显式无假设分支。

修复后先运行独立中立机制 fixture，实际检查无声明、无 hypothesis、未知证据撤回、确定 true 且无冲突、证据改 false 后撤销、输入未变不重复事件、再次失去假设等分支。输出为 `isolated appraisal branches completed; no game session`。该 fixture 没有开游戏会话，不是注入生产状态；一次性 .scratch/appraisal-probe 已由主代理清理。随后才冻结并启动下面的新正式会话。固定目录替换了失败输出，不保留滚动历史包。

## 当前正式会话的真实次序

命令：

`python mcp/verification/run_debug.py --out artifacts/perf-opt/aggressive/branch-npc --timeout 300 --causal-timeout 1 --detective-profile Standard --detective-zone none --plan-depth 3`

所有人物统一深度 3、全部默认优化开启，formal-model 不含 Godot 几何。用户要求并行测试，存在资源竞争，不据此宣称同条件性能优化。

| 人物 | 私人假设实际采纳 | 当前 angry=true 实际发布 | 表面答复实际选择 | 实际执行 / 正式接受 |
| --- | --- | --- | --- | --- |
| ward | e257，481，theft，prior 0.8 | e537，483 | e672，484 | e992 / e995，494 |
| mora | e296，481，experiment，prior 0.8 | e1330，662；来源刷新 e1377，663 | e1386，663 | e1421 / e1424，673 |
| selma | e285，481，self_preservation，prior 0.6 | e1370，663 | e1412，664 | e1515 / e1518，674 |
| elena | e280，481，self_preservation，prior 0.8 | e683，484 | e1408，664 | e1502 / e1505，674 |

时间为正式游戏分钟。直接读取上述 private_appraisal_updated.data.after，均为 known=true、value=true、conflicted=false、claims=[]；记录的 hypothesis_event 分别指向表中真实假设采纳事件。早期材料尚未齐备时，elena e281、selma e286、mora e297 真实发布 known=false 的未知评价，未提前授予愤怒。初始 neutral=false 和后续 true 不再作为同权事实 claim 累积。

选择、执行及接受严格区分。上表实际提交全是 `.answer.<group>.<actor>`，随后 identity 阶段被正式接受；不是出现候选就算作答。

## 评价实际进入了什么选择

已读取当前 action_selected.data.plans 的正式候选估值：

| 正式选择记录 | 仍合法且确实被评估的调查候选 | 调查 information / progress / score | 选中的表面答复 information / progress / score |
| --- | --- | --- | --- |
| ward e672 | resident.truth.ward.seal_examined | 0 / 0 / -0.01 | 1 / 1 / 1.95 |
| mora e1386 | resident.truth.mora.old_scar_examined | 0 / 0 / -0.01 | 1 / 1 / 1.95 |
| selma e1412 | selma.truth.ask.moran_calmed | 0 / 0 / -0.01 | 1 / 1 / 1.95 |

这三项不是外部新增评分：原规划器消费本人真实当前评价，使继续调查目标不可用、报告目标可用，进而形成现有 information/progress 项。调查候选没有从行动目录删除，也没有变成物理非法；它们在本次实际认知下没有相应调查意愿收益。其后真正执行与接受见上表。

需保留的因果边界：本轮没有改变私人评价作同知识反事实实验，不能声称已证明愤怒是唯一必要原因；没有新的独立正式会话审查结论。elena 的强成见及真实愤怒成立，但她不是当前声明的报告任务承担者，不能把三位报告者的目标消费证据直接外推到她的全部行动动机。新的 private_appraisal_updated 类型有独立来源事件，但本轮未宣称已扩展全部既有 support 索引消费者来生成完整的评价→选择证明图。

## 未执行路径与验收边界

本轮实际未执行任何 resident/detective truth 行动，未正式提交带 `.reason.` 的完整答复。故不能声称居民实际查明深层真相，亦不能用声明中的完整路径或独立 complete_receipt 字段当作完整作答已接受。当前 none 会话全部普通居民提交的是材料支持的表面事实；已实际打开完整正文，仍保留“授权和动机还没有说明／封门缘由仍需核对／取用目的需要本人说明与材料”等保守叙述。

本分支没有另跑 Standard 在场会话，也没有验证游戏中深层真相到达后自动撤回愤怒及居民完整答复。有限中立 fixture 只验证通用撤回语义，不替代这些行为验收。没有读取设计文件，没有预置答案或伤亡，没有运行 Godot UI、碰撞或盲玩。

## 当前完成、封存与耗时

- 正式引擎退出码 0，会话 142.062 秒；当前构建 9.203 秒，与离线封存／取证分列。该耗时不作为新接口单独提速的归因。
- 客观收束在游戏 6330；quer e30108 于游戏 6331 实际成功执行章末检视。停止同时要求 objective-chapter-two-closing-record 与 actual-viewer-closing-inspection。
- source_unchanged=true，run_config_matches=true，logical_loop_finished=true，evidence_complete=true。
- 当前 raw DAG 542,417,876 字节、30,111 个已提交事件；final checkpoint 和 seal 完整，未投影尾部为 0。SHA-256：`3f083a19ec3b39dd281eebc86081428a44cd67b7966ad3f437cd6e33975173e4`。
- 当前 30 源码 SHA-256：`321c812196be15efc48801dd032cd79025ca83e0a81ae331db4dd26cbdbd3d3a`；40：`6eac5b17d042b0a6f011cf6934fef3ea21b32b319159105f56470e0c84c25072`。
- 玩家可见全文 478,063 字节。大 DAG、全文与生成程序仅保留于本工作树固定目录；提交只有小报告、指纹及摘要，.gitignore 已恢复。
- 按主代理要求，只读全因果检查限 1 秒而超时；causal_check_complete=false、main_causal_supported=false。runner 整体退出码 1 来自该检查未完成，正式引擎会话退出码为 0。没有把正常封存冒充完整因果验收。
