# 当前仍成立的问题

2026-09-23 对当前源码、资源及既有试玩记录复核。Core、言灵与茶案独立检查已运行；前三场的验证范围见[场景说明](../godot_csharp/scenes/Story/README.md)。不将旧试玩现象一律当作现存故障。

1. **旧摆景重建脚本仍使用 Lanczos 缩放。** `godot_csharp/tools/shots/layout_shot01.gd`、`layout_shot02.gd` 仍有调用。这是旧产物重建路径的像素处理遗留，不能推广成当前全部资源均用错误缩放，也未据此确认当前画面的具体失真。
2. **茶案整案体验与表达负担尚未完成验证。** [第二轮设计记录](reasoning-round2-design.md)和[原始盲测](../artifacts/tea-reasoning/round2-player-report.md)仍保留行为录入像配置事件的反馈，未完成收齐材料后的再次提交和完整独立解答。主要替代因果路线也尚未验证。已修复的椅布标记遮挡不列为当前故障。
3. **美术与场景表演仍未完成最终验收。** 宣传前三场已扩产八名角色的侦探同系列基础动作，但基础动作不等于端盘、取食等专门表演，自动帧数检查也不证明动作质量。前三场装修仍待审美审看；其他镜头及茶案人物不属于此次扩产范围。不能再写成“除侦探外20人全是占位符”。依据：[动作图集说明](../godot_csharp/ShareAsset/Images/characters/animation_v2/README.md)、[前三场说明](../godot_csharp/scenes/Story/README.md)。
