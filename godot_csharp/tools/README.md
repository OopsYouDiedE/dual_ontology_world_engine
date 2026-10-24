# 场景画面采集

`capture_scene.gd` 使用普通键鼠输入和现实时间等待，保存真正渲染的视口及 `capture-provenance.json`。它不读取游戏私有状态，不生成验收结论。窗口渲染必须启用；`--headless` 会明确拒绝。

```powershell
& $env:GODOT --path godot_csharp --rendering-method gl_compatibility --rendering-driver opengl3 --audio-driver Dummy --script res://tools/capture_scene.gd -- --capture-scene=res://scenes/Highlights/Highlights.tscn --capture-output=C:/absolute/task/screenshots --capture-steps=C:/absolute/task/inputs.json
```

输入文件为数组：`[{"label":"initial","wait":0.5},{"key":"Space","wait":8,"label":"after-space"}]`。`key` 使用 Godot 普通键名，`hold` 为按住的现实秒数，`click:[x,y]` 使用窗口坐标，`wait` 为输入后等待的现实秒数；提供 `label` 才保存 PNG。无输入文件时只保存一张当前场景。输出路径必须为绝对目录，标签不能包含路径。PNG 和来源清单在同一固定目录覆盖更新，其他文件保持原状。

自动输入采集属于画面观察材料，独立盲玩的输入与体验评价另行记录。地图单独渲染也只证明该资源的显示情况，不能代表正式会话中的过门、碰撞与剧情表现。
