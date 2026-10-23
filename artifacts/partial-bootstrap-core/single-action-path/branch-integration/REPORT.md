# 分支整理与有效改动整合（2026-10-23）

整合基线为已抓取 origin 的 main `a2db464d3`，独立工作区 `C:/Users/iii/Desktop/dual_ontology_branch_integration`，工作分支 `codex/branch-integration-20261023`。逐项清单见 [BRANCHES.md](BRANCHES.md)，精确引用与工作区状态见 [branches.json](branches.json)、[worktrees.json](worktrees.json)。本地及远端同名引用分别盘点，共93项；34个目录可读取，已失效目录未恢复或清理。

## 采用的改动

1. 以快进方式纳入 `codex/decision-candidate-20261023` 至 `a5184ddb1`，其14个增量提交包含真实CLI窗口桥、固定主观检阅者、可见证据索引、现场过期位置撤回、实际纠错发言、贡献后留场和真实记录后的共同配药。此前contact/medical/granary分支的有效块已包含在其中，不重复套用旧文件。
2. 保留该候选第二章单一派生导航意图、本人合法路线预测与缺失前提现场检视，连廊候选分别命名，成功换气只向现场可见者发布已完成操作。此为策略调整；合入不表示减少往返、合作达成或整局体验已获得新实测证明。
3. 保留紧邻dvs_choose的选策墙钟与每游戏小时累计指标，保留人工/LLM检阅范围；未闭合和失败区间不填作完整计量。未把计时当成纯CPU或同条件性能收益。
4. 从 `codex/export-evidence-stream` 选取 `43f7a0de` 为 `96ac5666`。融合只读DAG平坦原子解码/校验、连续节点偏移数组，保留复杂格式回退、帧/链/引用/提交/封存校验。冲突合并时保留最新 taskgraph默认off、显式消融翻转及CLI窗口接口，并添加独立evidence-stream工具开关。
5. 将引入文档中的历史“本轮并行验收”“无45秒上限”等授权收敛为通用接口说明。新任务的AI、盲玩、时限与并行范围仍以当前用户明确授权为准；不从历史任务继承执行要求。

设计核对使用现行设计规范第二章的翻案、专业协作、现场读数和实际救治条件，以及第六部分的主体认知/输入边界。未修改设计规范、答案、伤亡判定、预算或正式Prepare/现场回执/Complete链。工程取舍与尚未验证的剧情效果分别报告。

## 不合并及已覆盖项

- 51个引用已经是原main的祖先，含原生语法、模块重构、界面、美术/音乐及先前已整合工作；不再次合并。
- 六个引用由a5184ddb的祖先链覆盖，另十个引用的有效内容已由main或该候选同源/等价块覆盖。声路补闻、taskgraph默认off、粮仓/医疗/生存因果守卫、stateindex及Python后处理现役改动均保留，不从旧分支树回退。
- 14个旧planning/r20引用依赖已退役的csharp决策器和旧PlayerController；不得恢复第二条生产路径。旧备份UID/对白分支及cloud-360缺失基线/旧language_core快照同样不恢复。
- `opt/fix-replay-regression` 及未提交任务图续改保持隔离。现存轨迹不完整且与最新未提交指纹不同，不能据其证明新策略成立；主线taskgraph继续默认off。
- `opt/selectivefast` 暂不合入：分支自身报告明确未证实速度收益，且CLI与run_debug缺省不一致。`opt/fix-native-evidence` 是另一套离线解码原型，尚缺现役路由、v1回退、完整文件与资源失败合同，暂不增加并行维护实现。
- 本地和远端 `gh-pages` 是旧动物园Web发布树，不作为现役DV/Godot源码合并。
- 不删除、重置、强推或伪造合并任何未采用分支；“暂不合并”与“内容已覆盖”分别记录。

## 未提交工作与清理

原工作区开工时有1291项变动，其中1286项为已有artifacts变动，另外5项为Godot生成的未跟踪UID；全部原地保留。其他工作区同样不stash、不重置、不提交、不删除其文件。

`decision_navigation` 的未提交导航块与a5184ddb相同；`decision_evidence` 的未提交连廊块也已包含；`decision_scheduler` 的计时实现与a5184ddb一致，不重复迁移。`survival_candidate` 中已被采用的同房配药块来自独立修复，额外的医者自行取记录/化学家自行中和抽屉替代路线未采用：缺少完整设计依据与救治/策略对照证据。旧chapter-one和seamless检出的遗留代码、资产、导入文件及临时脚本保留，不能将整个旧树覆盖进现役工程。

本轮没有创建一次性脚本或运行游戏，没有需要清理的本轮scratch、窗口、会话和原始运行轨迹。工程日志与静态汇总只写本任务固定目录，不触碰其他任务证据。

## 当前整合源码的工程验证

- `dotnet build dv_core/LanguageCore.sln -c Release --disable-build-servers -p:NuGetAudit=false`：成功，0警告、0错误，50.66秒。
- `dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers -p:NuGetAudit=false`：成功，0警告、0错误，14.62秒。
- 现有 `test_postprocess_speed.py` 离线完整/损坏证据差分检查执行成功；`test_tool_hardening.py` 离线文件、参数与进程合同检查执行成功。它们没有运行DV会话，结果不作为游戏质量评分。
- `audit_action_paths.py` 静态源码清单完成，issues为空；仍列13项工具预览/测试资源引用缺失，全部位于未变动的mcp/rpg_adapter文件。对应资源在整合前main也未跟踪；不将静态审计宣称为资源或游戏验收。
- 完整已选差异及本轮文档修改人工审读、`git diff --check`完成；未改生成源码，构建由现役生成器再生obj内产物。

日志和静态清单见 [engineering](engineering/)。本轮未启动Core AI、formal-model正式会话、Godot游戏/UI或盲玩；没有新的剧情达成、治疗成功、伤亡差异、性能提升或画面效果结论。历史报告仅为取舍依据，其失败、超时和未完成记录原样保留。
