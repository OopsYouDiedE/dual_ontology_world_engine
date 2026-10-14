# DV 模块维护

本目录继承仓库根AGENTS.md。语言名为DuelView（DV），源码后缀为`.dv`，实现位于language_core。

## 文档

SYNTAX.md维护完整书写格式，DESIGN.md说明语言与编译架构。设计和指南直接描述语法、机制、模块和命令。第一章源码需求保存在artifacts/partial-bootstrap-core/chapter1-reverse-language-requirements-2026-10-13.md。

surface-v2 的可执行前端位于 compiler/compiler.dv：缩进、Unicode 标识符、define 别名、函数/局部语句、记录/列表、中缀运算、mapping、acceptance 和有限域 imprint 声明展开后复用 C# 后端。Core、正式 session、checks 与 chapter1 内容已经使用新表层语法。编译器自身保留 S 表达式启动源，C0→C1→C2→C3 继续自举；通用泛型、单位推导、完整能力类型和目标无关 Typed IR 尚未实现。

compiler 中 ck_* 执行名义类型、字面量范围与可变性检查；core/55-cognition.dv 提供接纳分类、有限域配平搜索及独立证书核验。Runtime.cs 只提供基础有理数、Unicode 与容器 ABI。求解器的最优声明须经 DV 核验器重新检查覆盖、可行性与成本；正式提交一起结算认知、资源和搜索/核验工作量。核心标识符和声明关键词使用英文，其他自然语言用文件头 define 映射。

## 模块

| 路径 | 职责 |
| --- | --- |
| compiler/compiler.dv | 扫描、解析、名称/调用处理和C#输出 |
| compiler/seed / Runtime.cs | 启动编译器与平台ABI |
| core/*.dv | 世界、规则、观察、传播、价值、回应、费用和论证 |
| verification/session | 正式DV逻辑会话 |
| verification/scenarios | 原生人物、世界、话语、公理实例及配置 |
| verification/checks / report | 只读判定和中文报告 |
| Kernel / bootstrap | 当前解释启动链 |
| Terms / InferenceModels | 宿主数据合同 |
| WorldCore | 宿主身份、观察与事务适配 |
| tools | 构建及进程编排 |
| Session | 从 core/session 的 DV 源生成的独立宿主类库；C# 仅封装序列化与对象所有权 |
| editors/visualstudio | Visual Studio 2022 的 DV 诊断客户端；语义检查复用 DV 编译器 |
| editors/vscode | 用户指定的 VS Code 客户端；官方语言客户端连接共用 Python LSP，npm 打包 VSIX |

core、session及编译器的算法使用DV。平台ABI承接数值、容器、文件、控制台和最低执行适配。正式 Godot 项目已改为引用 Session；现场呈现与章节通行仍在集中修复。

容器深拷贝由平台ABI的rt_copy_value执行；DV负责记录更新、推理与规划。记录查找使用rt_record_index/rt_record_find从末项查找，重复键保留末项优先。性能修改通过第一章新会话记录具体推进与实际进程耗时，出错直接修正对应实现，不运行回归套件。

规划评分对每个预测节点建立一个只读人物视图，复用静态动作与政策；评分不写入该视图，正式人物状态仍由会话更新。

专业回复可以声明offer_to；本人支持话语且看到该接收者时，可主动说明一次。已经实际收到的询问仍沿请求回复处理。

## 内容和机制

新内容采用具名axiom、defn和领域构造函数；原生DV执行后导出JSON作为交换数据。oracle为独立内容包，主体包引用公开合同。旧.story提供迁移来源并继续服务现役宿主。

本人推知、预测与物理执行分别使用infer、predict、enforce采纳。理解、采纳、模型、论证和实际行动保留各自来源。decision显式表达planning和admission；各旧入口的评分、门槛、平分与首步选择在对应DV库中迁移。

正式答辩分别记录公共结论等级与主体自己的接受回执。主体接受记录随实际交接更新，规划依据本人回执排除同等级重复提交；只读判定使用提交前的本人记录核对，不以他人的公共结论替代本人的作答。

session/18-requests.dv记录实际收闻的专业请求、本人回复及同处可见人物。请求不直接取得场景命题；回复由另一主体的正式行动、本人认识与实际听觉传播形成。公开动作观察按声明范围与实际同处过滤。规划将方法前提纳入依赖，并在整个搜索中保留预测状态的分数/耗时非支配路径；重复状态不重复展开。

## 命令

```powershell
dotnet build language_core/LanguageCore.sln -c Release --disable-build-servers
dotnet run --project language_core/Cli/Language.Cli.csproj -c Release -- demo
python language_core/tools/build_core.py --out artifacts/partial-bootstrap-core/domain-build-<new-run>
python language_core/tools/bootstrap_compiler.py --out artifacts/partial-bootstrap-core/compiler-<new-run>
python language_core/tools/run_verification.py --config <process-plan.json> --out artifacts/partial-bootstrap-core/<new-run>
python language_core/tools/check_dv.py --format json <source.dv>
python language_core/tools/dv_lsp.py
```

## 工作安排

源码和目标生成物由对应DV构建链连接。架构、接口和命令变动同步本文件；内容设计合并至docs/design-standard.md。阶段成果继续使用artifacts/partial-bootstrap-core/，每轮修改按根流程提交与推送。

run_verification的普通编译/会话执行持续写入当轮stdout/stderr原始文件，结束后记录进程时长与来源封存。可在会话期间查看已经实际发生的行动；完整停止轨迹与只读判定另行记录。

用户2026-10-12停止独立Agent复评的裁定继续适用。新语法运行使用 verification/run-surface-v2.json 的单次正式会话计划；不默认执行旧多场景计划。


当前机器缺少 .NET 8 运行时而有 .NET 9；本轮记录显式使用 DOTNET_ROLL_FORWARD=Major 运行 net8.0 产物。源码仍以 .NET 8 为目标。NuGetAudit=false 仅关闭本地构建时不可达的漏洞索引查询，不改变 DV 检查器或游戏条件。

刻画的 Core 接线由 dv_imprint_prepare/apply 完成：从真实 mind 字段建立配平坐标，按证书写回认知并通过既有 90-action-cost 账本结算；工作额度单独返回，由宿主和认知、世界、账本一同发布。

编辑器检查通过 dv-check.json 选择编译组，逐文件保留 define 作用域和位置，再交给 C3 编译器的 --check 模式；检查不执行内容。tools/dv_toolchain.py 按启动源指纹缓存自举编译器；tools/dv_lsp.py 处理未保存缓冲区和过期诊断。Visual Studio 和 Godot 客户端不另写语义分析器。运行封存覆盖整个 language_core，正式会话运行期间也不要修改此目录的文档或插件文件。

主体内部引用通过 dv_same_local_ref 比较：省略主体的字段解析为当前主体，显式其他主体不等同。推知依赖以法则和输出字段标识，重新推知替换该来源；事件 ID 继续标识实际发生的推知。证据支持份额与来源可靠度分别保存，默认置信度不超过最强实际来源的可靠度。

session/26-model-slicing.dv 从本人已采纳法则的读写依赖逆向选取证物相关演算；切片仍遮蔽原计划全部输出，比较消耗真实工作预算。切片吻合不能建立完整解释，矛盾必须带实际预测及本人观察来源。session/16-personal-choice.dv 将本人有来源的价值排序用于同组选择；行动前提、传播与账本仍使用共用机制。跨房间请求由本人已知声道条件取得候选资格，实际接收仍由 speech 的听觉传播判定。

生产编译由 game/modules.json 显式组合 core、session、两章内容与 game/campaign.dv。Session.OpenCampaign 创建正式原生内容，Prepare/Complete 经同一章节扩展入口；章节扩展不重新调用 session_open，不清空账本和事件。

生产呈现绑定在 game/presentation.dv：身体地点映射场景、房间与素材；客观显示数据不写入人物认识。初始放置只用于创建视图，跨地图回执必须来自现场执行。departure 安装独立门状态，章节内容可用与实际过门分开记录。
