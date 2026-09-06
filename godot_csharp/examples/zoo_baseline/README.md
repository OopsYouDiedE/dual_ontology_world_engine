# 动物园场景底座

这是普通动物园的可复用 Core 场景组合。它本身不加载守则、异常规则或超自然物品；`ZooAnomalies` 只作为可选组合层，用于测试“保留异常、没有守则知识”的行为。动物园拓扑和饲养参数是测试假设，不宣称忠实还原任何原作。

默认命令运行异常组合：

```powershell
dotnet run --project godot_csharp/examples/zoo_baseline/ZooBaseline.csproj -- --seed 7 --batches 3 --visitors 4 --trace
```

运行关闭异常的对照组：

```powershell
dotnet run --project godot_csharp/examples/zoo_baseline/ZooBaseline.csproj -- --control --seed 7 --batches 3 --visitors 4
```

`--batches N` 在同一个 `SimulationSession` 中连续加入游客批次，不重置世界；`--trace` 输出异常阶段、模拟时间、行动者、场景和真实触发动作。
