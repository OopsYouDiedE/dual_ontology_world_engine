# 动物园网页发布入口

网页使用 Blazor WebAssembly，在浏览器内运行 C# 模拟器。依赖链为 `zoo_web → ZooSimulator.Runtime → DualOntologyWorldEngine.Core`。终端项目 `ZooSimulator` 引用同一个 Runtime，终端交互依赖 Spectre.Console 不进入网页。

`zoo_simulator/runtime/ZooSimulator.Runtime.csproj` 编译模拟器目录的 C# 文件，排除两个终端入口文件，并内嵌原文。新增模拟器文件自动进入两个入口。网页与终端都通过 `ZooSimulation` 装配世界、知识预设、异常过程和角色行动。

在仓库根目录运行：

```powershell
./godot_csharp/examples/zoo_web/publish.ps1
```

静态托管根目录使用 `publish_wasm/wwwroot`，通过 HTTP 提供其中全部文件。网站支持目录形式的子路径，访问子路径时保留末尾 `/`。不要单独复制源码目录的 `wwwroot`，它尚未包含 .NET 运行时和模拟器程序集。发布输出已从项目输入排除，避免再次嵌入旧发布目录。

网页的播放定时器只请求推进逻辑时间；角色和环境实际行动仍由 Core 的行动值队列结算。每批最多 500 步，达到上限显示实际进度。重置会更换整套模拟实例，旧定时回调不能推进新世界。

人物面板显示情绪、已知状态、查证能力和长期动机，日志显示 C# 事件和嵌套回应。规则预设在重置时应用。自定义正文会广播为文书，新的行为建议仍需在 C# 模拟器内定义。
