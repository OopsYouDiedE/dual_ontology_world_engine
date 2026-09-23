# 人物动作验收室

运行CharacterAnimationPreview.tscn，加载animation_v2两套真实PNG/SpriteFrames。独立房间352×224，地面网格32px。本场景验证动画及局部交互表现，不接入Core或案件结算。

方向键移动；按钮只转向。I/W/T/R/E/S/B/C选择待机、步行、交谈、检查、交互、坐/起身、破坏、施法。Tab切换人物，Space暂停/续播，Enter单帧，1–4整数倍率，G辅助线。坐姿末帧保持，再按S逆序起身。靠近椅子30px内落座会对齐座位，远处可独立查看姿态。

到左上面板下方朝后按E，在接触帧变色；到右下箱子左侧朝右按B，在挥击帧改变箱子表现并解除碰撞。距离上限30px且须面向物件。椅子、箱子与施法效果独立于人物图集。

从仓库根目录执行（$godot为本机Godot .NET控制台可执行文件）：

```powershell
& $godot --headless --path godot_csharp --log-file "$PWD/artifacts/character-animation-v2/check.log" --script res://scenes/CharacterAnimationPreview/capture.gd
& $godot --path godot_csharp --rendering-method gl_compatibility --log-file "$PWD/artifacts/character-animation-v2/check-visual.log" --script res://scenes/CharacterAnimationPreview/capture.gd
```

验收覆盖222帧尺寸、非空、透明边界及alpha；实际运行镜像、暂停/逐帧/续播、1–4倍、门口和墙体碰撞、桌前后排序、落座保持与逆序起身、交互和破坏接触帧。无窗口模式不截图；有窗口模式在仓库artifacts/character-animation-v2/preview.png保存截图。gallery.gd通过真实AnimatedSprite2D同时播放全部48个动画，截取三个时刻供视觉核查。

图集结构和重建说明见资源目录README。这里通过说明本轮通用动作资源与验收室可运行，不代表完整案件、专门持物动作或脑内推理美术完成。
