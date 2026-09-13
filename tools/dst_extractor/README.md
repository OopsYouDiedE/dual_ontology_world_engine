# Wilson 动画资源

`generate_godot_bank.py` 从本机 DST 的 `wilson.zip`、`player_basic.zip`、`player_idles_wilson.zip` 和 `player_attacks.zip` 生成项目内的 `wilson_poses.json`。运行场景只读取项目资源，无需安装 DST。

```powershell
python tools/dst_extractor/generate_godot_bank.py "<DST 安装目录>/data/anim"
dotnet build godot_csharp/DualOntologyWorldEngine.csproj
```

每帧保留 build_frame 对应贴图、build.bin 的绘制矩形、完整仿射矩阵及深度顺序。ANIM 的 a,b 和 c,d 分别作为 Godot Transform2D 的 X、Y 基向量。分件在统一的角色坐标系内绘制，深度从后向前排列；不按坐标重新识别左右肢体。无装备角色隐藏 ARM_carry、帽子和换装占位层。

待机使用 idle_wilson，跑步使用 run_loop，走路将同一循环延长至 0.75 秒，挥击使用完整的 punch。侧面待机中的转头来自源动画。

展示场景支持显式抽帧检查，输出路径由调用者指定：

```powershell
godot --path godot_csharp --rendering-method gl_compatibility -- --capture-dir=<截图绝对路径>
```

该模式固定角色位置，输出三个朝向及向左镜像的动作抽帧、正侧面表情，共 56 张截图，并检查挥击结束后是否恢复待机。正常启动不会运行截图逻辑。导出游戏时需将 `*.json` 加入非资源文件导出过滤器，以包含姿态数据。
