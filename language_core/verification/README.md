# 编译 DV 自动验证

会话适配器为 `session/*.dv`，只读领域算法为 `checks/*.dv`。`main-causal.dv` 接收 manifest、trace、oracle 三个路径；`compare-capability.dv`、`check-evidence-boundary.dv`、`sweep-parameters.dv` 各接收 comparison 路径。comparison 的 runs 列表每项提供 manifest、trace、oracle 文件引用。所有入口向 stdout 输出 JSON，原始输出保留，不用数量或通过率评价。

运行器由 `core/*.dv` 与 `session/*.dv` 组合后追加 `(main (dvs_main))` 编译。CLI 接收 DV 导出的场景 JSON 路径、trace 输出路径和 `enabled`/`disabled` 能力配置。运行器是隔离逻辑会话，尚不能代表 Godot 输入、碰撞、界面或完整旧 Core。

用户本轮纠正内容语言边界：人物、世界、话语、规则、参数、答案及 oracle 均写为真实 DV 声明/函数。独立 DV 数据程序将它们导出为 JSON 交换生成物；oracle 数据程序与主体运行器分离，不能编进主体包。旧 `.story` 只读保留迁移来源，不能当正式运行输入。仅改后缀或将 JSON 正文搬入字符串不算迁移。

随后用户进一步明确：因果、推知、移动法则以具名 DV axiom 定义，世界、人物、策略、观察与动作使用命名构造函数并调用 core；规则 body 的修改应直接影响执行。平铺旧 record 树的格式转换也不能作为此要求的实现。导出仅为执行这些原生定义后产生 DTO 的平台边界，构建不解释旧 `.story`。

```powershell
python language_core/tools/run_verification.py --config <process-plan.json> --out artifacts/partial-bootstrap-core/<new-run>
```

process-plan 只描述文件与进程 I/O，不含领域正文：`files` 指定冻结输入；`copies` 复制原字节；`exports` 每项列明 id、DV modules、function 与 output 路径；`sessions` 每项有 id 和运行器 args；`checks` 按四入口名给出参数列表。参数支持 `{root}`、`{inputs}`、`{out}` 路径展开。`path_documents` 引用 DV 导出的比较配置并只展开文件引用；`platform_manifests` 引用 DV 导出的 pair-record 模板，在实际源指纹复核后追加平台 integrity、source/runtime 指纹和范围。模板不能覆盖平台封印字段。批准参数范围、公理和答案由 DV 数据源提供，工具不生成或修改它们。

工具封存 language_core 源文件及指定输入，运行 C0→C1→C2→C3 并记录 C2/C3 生成源码固定点；分别编译运行器与四个 checker，然后按配置顺序实际执行。每个进程保留 stdout/stderr 原始字节、退出状态、现实单调起止与耗时，源码前后比较、生成源码及程序集哈希另存。失败后不继续依赖步骤，旧输出不能替代本轮证据。输出目录只新增。

完整设计合同见 DESIGN.md；动态分域、自然语言解析、完整静态类型/能力安全、Godot/旧 Core 接入和游戏体验不由本工具证明。缺字段和未发生事件应由 DV 报告 `insufficient_evidence`/`not_observed`，不能跳过后宣布成功。停止独立 Agent 复评的用户裁定继续有效。

## 历史重判与中文输出

`--source-root` 使用独立封存源码，避免其它工作区迭代污染本轮。`--session-only` 实际运行计划中的会话并明确延后判定；`--judge-only <previous-directory>` 将新只读checker与报告模块单独封存，以原core/ABI重新判断原字节轨迹。`--primary-only` 只执行主检验，中文报告明确其余目的未执行。新判定合同与原会话/原oracle分别记录，不冒称重新游玩或满足后来改变的条件。

`report/10-readable.dv` 读取真实DV判定、原轨迹及仅含路径/现实进程耗时的I/O索引，输出中文节点、行动、来源、正式拒答轴与缺口。空判定路径明确显示未执行，不生成替代判定。Python只编译、传参和写文件；报告本身仍由DV执行。原始trace无损zip与SHA保留，原字节不删除；禁止提交大型裸trace或bin/obj。
