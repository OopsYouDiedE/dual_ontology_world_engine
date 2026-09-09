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
