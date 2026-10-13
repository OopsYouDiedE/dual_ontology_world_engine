# 编译 DV 第一章会话

运行器由core/*.dv与session/*.dv组合，并以dvs_main启动。命令行接收场景JSON、轨迹输出路径和enabled/disabled能力配置。场景和配置由scenarios中的DV程序导出，oracle单独导出。

## 运行

```powershell
python language_core/tools/run_verification.py --config <process-plan.json> --out artifacts/partial-bootstrap-core/<new-run>
```

process-plan描述文件与进程：files、copies、exports、sessions、checks、path_documents和platform_manifests。exports指定DV模块、导出函数及输出路径，参数支持root/inputs/out路径展开。

工具构建C0→C1→C2→C3、运行器和判定程序，再按计划执行进程。输出包含源码/程序来源、stdout/stderr、进程时间及会话轨迹。

## 程序入口

| 入口 | 输入与用途 |
| --- | --- |
| main-causal | manifest、trace、oracle，主因果判定 |
| compare-capability | comparison，能力配置对照 |
| check-evidence-boundary | comparison，实际传播与接收 |
| sweep-parameters | comparison，参数邻域 |

report/10-readable.dv根据判定和轨迹生成中文报告。全部领域算法使用DV，Python负责文件与进程编排。

## 相关选项

`--source-root`指定源码目录；`--session-only`执行会话；`--judge-only`读取已有轨迹；`--judge-core-root`指定判定器Core；`--primary-only`执行主判定；`--process-timeout-seconds`设置进程时限，默认240秒。

`--rerun-frozen-session`重新运行指定目录的既有程序与输入，`--reuse-sessions`引用已有会话。过程时限与游戏时间预算是不同配置。

## 内容模块

chapter1-laws、chapter1-pragmatics、chapter1-content、chapter1-history提供章节实例；chapter1-verification-config提供运行配置，chapter1-oracle提供独立判定内容。新的语法样例位于surface-v1-proposal。

模块结构见[DESIGN.md](DESIGN.md)，新书写格式见[SYNTAX.md](../SYNTAX.md)。
