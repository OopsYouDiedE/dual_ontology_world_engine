# 动物园网页

Blazor WebAssembly 直接在浏览器运行 C#。依赖链：`zoo_web → ZooSimulator.Runtime → DualOntologyWorldEngine.Core`。网页与终端共用同一模拟，无 Spectre.Console 依赖。

## 本地运行与发布

在仓库根目录执行：

```powershell
dotnet run --project godot_csharp/examples/zoo_web --urls http://localhost:5127
./godot_csharp/examples/zoo_web/publish.ps1
```

静态站点根目录为 `publish_wasm/wwwroot`，包含 .NET WASM、Core 和模拟程序集。可将其中全部文件用于 GitHub Pages；保留 `.nojekyll`，子路径链接以 `/` 结尾，`index.html` 使用相对 base。发布脚本只生成本地文件，不自动上传 GitHub。

## 实验界面

四种初始模式已移除。通过人物面板自由修改脑内对象、关系、信念与理解程度，比较候选权重，提交新行动并跟踪实际路线。文书的保存、广播、撤回均有明确的感知范围。言灵输入支持 Core AST，包括临时状态覆盖与独立到期。展开跨日记录查看各日游客数、黑衣新增及红衣返岗；“它”面板显示行动与记忆分类。

推进按每批最多 20 个 CTB 事件分片并让出浏览器线程。暂停可以中断单次长推演；重建世界会取消旧批次。浏览器的播放计时器只请求推进逻辑时间，不承担人物或异常结算。

完整语法、机制边界与复现步骤见 [模拟器说明](../zoo_simulator/README.md)。

## 2026-09-09 修订

可调用词汇由 `Vocabulary.cs` 统一维护，覆盖全部规则条件、动作、目的地以及 Z 标签与关系。编辑器保存规范词形；英文、日文、简体和繁体显示可反向还原，切换语言保留未保存草稿。Core AST 的函数名保持原语法。原始怪谈文书仍按中文原文展示。

配色与响应式布局集中于 `wwwroot/css/zoo.css`，危险、成功和警示使用语义颜色。编辑操作暂停推演；快捷标签覆盖同名旧值。新增对象可立即继续编辑，“安全”和“兔化”操作使用引擎实际读取的标签。

默认 120 名首批游客对应 19 名正式员工（12 蓝衣、3 保安、3 红衣、1 园长），另有 1 名黑衣。正式编制为 7 + 6 × ceil(首批游客 / 60)。概览列出在园岗位人数。此配置按首批规模分档，不是按每次游客入园自动生成员工。

本次验证使用 `core_acceptance`（20 组）及 `zoo_simulator_audit`（23 组），另完成浏览器英文规则保存与页面配色检查。旧 `tests/deep_audit` 仍引用已迁移的 `Visitors`、`ApplyPreset` 等接口，无法编译，不能作为当前实现的验收结果。
