# S09：通用 AST 执行规矩与社会请求

## 改动与依据

- 依据 S09 审核摘录、现有 `.story` 与源码迁移；未读取设计规范。
- 删除 `BehaviorIds`、行为同序校验和 `card.Cond` 编号分支。规则格的主体、条件、行为及普通规矩执行行从剧本绑定为既有 `Condition/Effect/Rule`。
- `story_ast.py` 复用原表达式解析器；`Shared/Story/ScriptAst.cs` 只绑定引用、槽位、集合和通用操作。不注册取粮、开库等具名 C# 回调；未知节点、操作、引用及递归过程明确报错。
- 新增 `续篇/10_执行.story`，声明过程、社会行动、目标条件、目的、成本、请求文案与承诺。请求列表支持任意声明 ID；迁移的具名过程从剧本读取。
- 菜单与提交复用相合计算。预览不更新认知、规则注册表或时间；正式请求重新检查当前处境。`condition_world` 区分物理可行性与本人认知，物理预评估不注入通路知识。
- 接受/拒绝使用类型化接受事件；实际执行由 Core 提交、耗时和结算。成功反馈与履约要求 `Completed`，不根据口头接受、旧行动标签或物品现状推定成功。
- 既有接受/拒绝后的态度标签和权重迁入 `receipt_attitudes`，保留社会评估需要的发送者历史；态度以独立 `Delivered` 事件关联接受回执，不表示动作完成。
- 请求须本次 Say 的 Completed 回执并产生新请求；每次清除旧接受判断，递送失败不复用旧请求。接受时保留实际目标对象引用；执行时重检该目标，不替换为另一可选目标。取消关闭收到的请求；拒绝、取消、条件变化和无法完成不发放成功效果。
- 同步模块架构与检查入口到 `godot_csharp/AGENTS.md`。未修改 Core、第一章人物策略、`scenes/UI/` 或布局/样式资源；未提交。

## 新增检查

- 编译器合同：11/11；既有 `test_access_lifetimes.py`：6/6。
- `AstRequestCheck`：34/34，Release 构建 0 警告、0 错误；原 24 项保留，新增 4 项态度、2 项截止结算和 4 项递送/重复请求合同。
- 夹具从正式剧本生成隔离副本，只追加中性规则、规则格行为、社会行动与候选请求；不改运行代码即可编译、被选择并实际生效。
- 覆盖未知 AST/引用/递归过程、观察来源与阈值、跨世界引用拒绝、作者选项选择、菜单认知/时间/注册表纯度、接受与执行分离、目标失效、不替换目标、拒绝、取消和执行前/等待中条件变化。
- 另覆盖发送者/接收者共用声明成本、物理通路预评估与认知分离、元信息规矩空 AST、修改剧本履约 ID、接受/失败/取消不履约、真正完成才履约及超期失败。
- 递送合同先真实接受并取消同文请求，再在 Say 激活前移动接收者；核对 Core OutOfReach 失败回执、无新接受/承诺/请求、旧请求未重用和两种主体均无成功效果。正常同文重复另核对新对象引用及真实 Completed。
- 态度合同只改剧本标签/权重，核对正式告知规矩、请求拒绝、调解后的发送者记忆及因果事件；接受、态度记录均不是请求执行完成。截止检查覆盖已经完成但达到/越过截止时段或越一天仍不能履约，失败闭合可重复调用。
- 未修改既有测试输入或预期。新物理通路夹具最初误选当地可见通路，正式 Say 完成后的正常当地感知更新了记忆；最终用既有 `GameOptions.Hide` 隔离该通路，保留全部原断言。

## 构建与自动回归

最终封存快照通过：Core、scope 27、cognition 372、宿主、save 23、flow 136/136、flow2 109/109；统一回归退出 0，无重试、崩溃或引擎错误。宿主显式构建有 2 条 NU1900（NuGet 漏洞数据网络不可达），无构建错误。Gallery 按要求跳过。独立合同 Python 11+6、C# 34 项及 7 份编译一致性均通过。

执行命令：

```powershell
python godot_csharp/tools/compile_story.py
python godot_csharp/tools/compile_story.py --check
python godot_csharp/tools/check/test_ast_requests.py
python godot_csharp/tools/check/test_access_lifetimes.py
dotnet build godot_csharp/scenes/Chapter2/Game/check/AstRequestCheck/AstRequestCheck.csproj -c Release -m:1 --disable-build-servers
dotnet run --project godot_csharp/scenes/Chapter2/Game/check/AstRequestCheck/AstRequestCheck.csproj -c Release --no-build --no-restore
dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers
python tools/verify.py --skip gallery --keep-going
python tools/check_story_leak.py --report
```

统一入口包含指定的 ScopeRegression Release 构建/`--no-build` 运行和 cognition 回归。
剧本重新生成 7 份输出，实际内容变更仅第二章 JSON；生成物未手改。

## 失败记录与环境

- 首轮统一回归保留：scope 27、cognition 372、save 功能 23、flow 136 项通过；flow2 103/109，6 项失败。save/flow/flow2 另报告沙箱根证书读取错误，因此首轮整体失败。
- 两轮统一回归均有 flow2 的同一 6 项失败。独立正式流程诊断中，同行前后均为第 5 日清晨，实际只显示裸拒绝，确认拒绝发生在宿主预评估阶段，后续未推进导致其余流程失败。
- 源码对照发现迁移遗漏旧接受/拒绝记录的发送者态度历史；本轮将其声明化恢复。实际评分数值未由旧探针取得，不以诊断推测代替最新回归结果。
- 正式行动原先依赖物理通路，本轮将这一依据显式声明；新增合同另暴露物理 AST/认知上下文跨图异常，修复后独立重跑通过。上述修复均未更改旧断言。
- 首轮缺执行块校验拒绝旧元信息寿命夹具；恢复元信息携带空 AST，显式执行行仍严格校验；旧 6 项检查原样通过。
- 新增截止测试曾因元组数组类型推断编译失败；新增递送测试曾将历史记录类型误写为 ObservationInput，按实际 API 修正为 ObservationEvent。均仅修正新测试类型，原断言不变，失败构建日志保留。
- Godot 使用指定 4.6.1 mono console；`APPDATA`/`LOCALAPPDATA` 指向工作区 `tmp/appdata`。
- 最终回归临时使用已有有效 CA bundle，经 `network/tls/certificate_bundle_override` 绕过不可读取的 Windows 根证书存储；没有过滤错误或禁用 TLS 校验。依据：[Godot 4.6 配置](https://docs.godotengine.org/en/4.6/classes/class_projectsettings.html#class-projectsettings-property-network-tls-certificate-bundle-override)、[4.6.1 加载源码](https://github.com/godotengine/godot/blob/4.6.1-stable/modules/mbedtls/crypto_mbedtls.cpp)。临时 `override.cfg` 验证结束后已移除，不作为交付配置。
- 首轮原始统一日志：`artifacts/project-audit/verify-logs/20261010-074814-542304-33512/`。
- 第二轮失败汇总：`artifacts/project-audit/verify-logs/20261010-080929-988357-26476/`；宿主拒绝诊断：`tmp/s09-final-checks/20261010-081813-872168-flow-diag/`。该轮 save/flow 真实退出 0，flow2 退出 1，无证书或引擎错误，完整失败汇总不重试。
- 合同失败与通过日志：`tmp/s09-final-checks/`；24 项通过快照为 `20261010-080833-044366-ast-run/`。初轮合同日志保留在 `tmp/s09-ast-check/`。
- 态度/期限快照：`tmp/s09-receipt-checks/20261010082650/`，11+6+30 项通过；完整统一回归通过，原始日志为 `artifacts/project-audit/verify-logs/20261010-083139-333029-10616/`。
- 最终独立合同与构建：`tmp/s09-delivery-checks/`；最终完整回归原始 stdout/stderr：`tmp/s09-delivery-checks/verify-raw/20261010-084553-486366-24224/`。临时包装仅更改日志目的地，调用未修改的正式 verify.main 与同一参数；save/flow/flow2 均完整结束、实际退出 0。

## 隔离、未完成项与风险

| 扫描范围 | 改前代码命中 | 改后代码命中 |
| --- | ---: | ---: |
| core | 0 | 0 |
| ui | 54 | 54 |
| shared-ui | 15 | 15 |
| interface | 0 | 0 |
| host | 0 | 0 |
| adapter（只报告） | 2091 | 2039 |

- adapter 减少 52；注释命中 362→356。门禁范围未增加；扫描仍为 `RESULT FAIL 69`，属于既有 UI/shared-ui 命中，不能宣称隔离门禁整体通过。 最终扫描见 `tmp/s09-delivery-checks/story-leak-report.stdout.log`；Git HEAD 与当前代码的完整基线比较见 `tmp/s09-receipt-checks/20261010082650/story-leak-diff-complete.json`。
- 删除 `CH2-006/010/011/055`。`CH2-056` 保留于剩余玩家反馈/案情揭示（含同行检视的专案反馈）；社会请求成功判定已改用实际回执，但该 switch 的全部内容尚未外置，不能宣称此标记已清除。
- 具名兼容枚举/人物槽位编号、其他人物策略和生产后果机制仍属既有适配边界；本轮不宣称第二章全部剧情硬编码清零。
- 保留工作区原有三处 `.translation` 二进制修改，未回退既有工作。
- 本轮为独立 Agent 执行的构建与机制自动检查。人物行为合理性、文学质量、游玩体验及一级/二级/三级验收均未验证；状态注入夹具不是盲玩证据。
