# DV 会话模块维护

本目录继承根AGENTS.md和language_core/AGENTS.md。SYNTAX.md定义语言格式，本目录DESIGN.md说明会话结构，README.md说明入口和命令。

## 模块职责

scenarios提供原生DV内容和配置，session组织主体演算与执行，checks独立读取轨迹，report产生中文报告。core+session组合为运行器；core+checks与各入口组合为判定程序。oracle单独导出。

人物、世界、话语、规则、参数和oracle使用DV声明；JSON承接执行后的交换数据。旧.story提供迁移来源，Python计划描述文件和进程。run_verification.py负责源码、构建、导出和进程I/O。

## 运行合同

动作费用使用core/90-action-cost.dv，会话持有成本目录和当前ledger。主体的推知、话语理解、付费重演、竞争论证、规划和回答协议各自保留来源。

新语法的第一章样例位于scenarios/surface-v1-proposal。迁移沿用各入口的原评分、门槛、平分及费用，随机策略为另一配置。

工程进程时限默认为240现实秒，由--process-timeout-seconds配置。--source-root指定源码，--judge-core-root指定判定Core；--rerun-frozen-session使用指定目录的既有程序和输入。游戏预算由场景配置提供。

## 输出与维护

成果继续存入artifacts/partial-bootstrap-core/，模块架构和接口变动同步本文件及DESIGN.md。停止独立Agent复评的用户裁定继续适用。
