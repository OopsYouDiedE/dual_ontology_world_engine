# 正式一级输入矩阵（计划，2026-10-18）

本文只记录待执行计划和源码静态核对。未启动构建、探针或会话；不代表行为验收。未读取设计规范，剧情评价与设计对应关系由主代理另行审查。

## 冻结与执行

全部修改、公共接线和文档完成后，由主代理冻结同一份源码、记录源码及运行程序集指纹。以下窗口串行运行，各自独占新证据目录、全新会话，最多180现实秒；配置间不改源码。若发现需要修复，保留该批已有证据，结束冻结批次，修复后另建批次。工程生成、构建失败及封存失败分别记录，不把构建当作游戏评价。

共同入口：`python language_core/tools/run_debug.py --out <独占新目录> --timeout 180 --raw-format dag`。共同默认值：`--opening-seed 1 --su-initial-room R08 --private-execution-benefit-multiplier 1`。`--homecoming-negotiation`默认省略，即false；只有CF8窗口显式开启。下表每行都是独立启动；不得恢复前一行的存档。使用DAG仅为在线原始证据留存，所有窗口保持一致。

| 窗口 | detective-profile | detective-zone | opening-seed | su-initial-room | private-execution-benefit-multiplier | homecoming-negotiation | 对照与独立变量 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| standard-yi-seed1 | Standard_Detective | zone_yi | 1 | R08 | 1 | false | 默认参照 |
| standard-jia-seed1 | Standard_Detective | zone_jia | 1 | R08 | 1 | false | 对照standard-yi，仅侦探初始区 |
| standard-bing-seed1 | Standard_Detective | zone_bing | 1 | R08 | 1 | false | 对照standard-yi，仅侦探初始区 |
| standard-ding-seed1 | Standard_Detective | zone_ding | 1 | R08 | 1 | false | 对照standard-yi，仅侦探初始区 |
| foolish-yi-seed1 | Foolish_Detective | zone_yi | 1 | R08 | 1 | false | 对照standard-yi，声明认知配置整体变化 |
| foolish-jia-seed1 | Foolish_Detective | zone_jia | 1 | R08 | 1 | false | 与standard-jia比较配置；与foolish-yi比较初始区 |
| foolish-bing-seed1 | Foolish_Detective | zone_bing | 1 | R08 | 1 | false | 与standard-bing比较配置；与foolish-yi比较初始区 |
| foolish-ding-seed1 | Foolish_Detective | zone_ding | 1 | R08 | 1 | false | 与standard-ding比较配置；与foolish-yi比较初始区 |
| acoustic-su-R05 | Standard_Detective | zone_yi | 1 | R05 | 1 | false | 对照standard-yi，仅苏本次开场实际位置和本人位置认识 |
| private-benefit2 | Standard_Detective | zone_yi | 1 | R08 | 2 | false | 对照standard-yi，仅private_execution本人私下收益倍率 |
| shura-yi-seed1 | Shura_Detective | zone_yi | 1 | R08 | 1 | false | 对照standard-yi，价值顺序及已声明manipulation自动候选资格；其余初始认知参数同标准 |
| standard-yi-seed7 | Standard_Detective | zone_yi | 7 | R08 | 1 | false | 对照standard-yi-seed1，仅认知初始化随机种子 |

| homecoming-seed1 | Standard_Detective | zone_yi | 1 | R08 | 1 | true | 对照standard-yi-seed1，仅开启归乡游说候选与目标 |
| homecoming-seed7 | Standard_Detective | zone_yi | 7 | R08 | 1 | true | 对照standard-yi-seed7，仅开启归乡游说候选与目标 |

Foolish配置自身包含多项认知声明差异，不称为单项价值实验。Shura配置同时改变价值排序和明确授权的操纵候选资格，报告这两项，不称为纯价值单变量。种子驱动全体实际认知采样，不宣称只影响侦探。私下收益倍率不改变物理成本、行动时间、认知预算、成功判据或执行规则；需要核对实际候选比较记录中的原benefit与缩放值。

## 实际证据核对

每个窗口核对stdout的debug_start.run_config、result.json的run_config与actual_run_config及run_config_matches，确保实际启动配置匹配请求。保留完整stdout/stderr、源码指纹、原始DAG及部分文件、只读事件索引、调试投影和玩家可见正文。debug_start缺失时记录启动阶段未知或失败，不用请求参数冒充实际运行配置。

逐段建立事件链：行动前本人已有信息及来源、本人价值/目的和资格、真实选择、prepare、formal-model执行回执、complete结算、实际观察/收闻与后续结果。候选出现、计算结束和移动不能替代问题解决。操纵建议需要实际说出、实际传播和解析、接收者自身选择及后续执行；采信、执行和因果归因分列。私行动需要当时本人的动机、现场条件、无人监督机会及实际结算；未执行分支保持未知。

声学窗口核对初始checkpoint中的物理苏位置与本人位置一致，再核对她实际移动后的所在位置、真实声音传播、本人接收和解析。开场R05不预设她一直留在R05，不注入听觉结论。昨夜过去态、原作者事实和推演假设继续保持各自来源；本窗口不改变事故时位置，也不能作为“昨夜苏在R05”的反事实。

一级仅涵盖DV正式逻辑，无Godot几何、碰撞、输入或画面结论。分别记录行动上限、180秒工程停止、逻辑停止、封存完成、五日时域和实际剧情节点。窗口内未发生的节点记为本窗口未发生，资料不足记为记录不足；不补猜节点时刻。每轮证据交新上下文只读代理审查，审查者不读取设计规范。

## 本次入口的静态审核

`c1_campaign_scene`先安装历史模型，再由`c1_opening_install_scene`安装本次开场；`c1_op_wake_room("su")`的既有默认为R08。默认R08分支直接返回原scene，未重写其位置。反事实R05分支仅写物理`su.state.location`、苏本人`actor.location`和`mind/location_ref`，并核对原有su→su本人绑定；没有写`past.su`、history.inputs.su_origin、历史模型、声音记录、听觉先验或作者事实。`dvg_place("R05")`将逻辑R05映射到zone_yi:R05。

`opening-seed`经真实dvs_initialize_cognition的采样流程；profile初始化于session_open，因此操纵自动候选开放同时读取初始化声明和已初始化policy。扩章重新应用该政策，覆盖第二章新声明的操纵动作；共同前提、本人知识、实际收闻与执行资格仍然适用。

## 尚缺入口与范围

CF8通过`--homecoming-negotiation`显式开启主代理新增的归乡游说声明。参数保存于run_config并传入actor.policy.homecoming_negotiation，默认false；候选与目标由内容模块按该政策开放。种子1、7各自与同种子的默认窗口对照。核对实际游说、听取/解析、本人采信与表决、规范结算和后续行为，不将开启参数或候选出现写成投票、离场或归乡达成；实际未实现或未执行的后续阶段分别报告缺失或本窗口未发生。未注入票数或预置协商结果。

本次“声学R05”只改变当前开场位置。若要求改变昨夜事故发生时的位置及相应声音物理过程，需要独立授权并建立不同内容基线；不得通过当前开场参数偷偷改写历史。Shura第二章仍适用既有章节通用规划参数与价值补充规则，不能把开局参数解释成整局永不变化。
