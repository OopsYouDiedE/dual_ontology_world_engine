# 共享人物母版工序

项目以 VRoid Studio 生成基础人物，以 Blender 整理共享拓扑、骨架、现有服装和游戏导出，十七人名单及外观设计由 `godot_csharp/Content/character_art.dv` 声明；但是此目录属于离线资产制作工具，游戏机制继续由 C# 解释 DV，制作工具不提交游戏交互结果。

VRoid 2.14.0 已通过官方发布者签名和响应 MD5 校验并安装于 `.runtime/vroid/2.14.0/`，源文件保存目录为 `.runtime/character-art/vroid/sources/`；但是当前用户正在亲自调整素体，代理在收到完成通知前保持编辑器不动。安装器保留于 `.runtime/downloads/VRoidStudio-v2.14.0-win.exe`，SHA256 为 `d59ad962cebcaa3375b7add4c457f653b4b82d1b554b3040ce1b092caea18f38`；但是工具许可不代表第三方人物和服装均可自由采用。

`prepare_vroid_character.py` 使用 Blender 原生 glTF 导入器读取 VRM 的几何、蒙皮与形态键，保留人形骨骼元数据和来源校验，输出候选 GLB、可编辑 blend 与检查报告；但是 VRM 表情绑定、弹簧骨、默认 MToon 及其他扩展语义不由原生导入器完整转换，仍须专门整理。当前仅完成语法静态检查；但是在取得用户调整后的实际 VRM 前，不声称该工序已通过真实资产导入验证。

调用形式如下，整体高度由实际样角与家具接触测量决定；但是示例中的 1.32 米仅是当前 E 提案，并非已验收尺寸。

```powershell
& 'C:/Program Files/Blender Foundation/Blender 4.2/blender.exe' --background --python mcp/asset_pipeline/characters/prepare_vroid_character.py -- --source .runtime/character-art/vroid/sources/master.vrm --output .runtime/character-art/vroid/master --height 1.32 --clothing-reference .runtime/character-art/E_sources/detective.glb
```

服装参考集合隐藏保存在工作文件内，便于沿用 E 的造型，正式候选 GLB 在加入参考前导出；但是旧人物参考中的身体不会作为正式新身体自动采用，衣服也不因导入而获得有效蒙皮。

后续角色可用 `--skeleton-reference` 指向母版的 `inspection.json`，工序同时核对骨骼名称、父级、长度和静止矩阵指纹；但是指纹匹配仍不代表服装变形、动作接触与 Godot 运行已通过，指纹不符时先整理或重定向，不强行直接复用动作。

`skin_e_character.py` 保留为未采用的旧实验记录，`prepare_character.py` 保留为旧候选检查工具；但是它们均不属于用户批准的 VRoid 量产路线，旧候选输出不作为已完成人物。`render_character.py` 用于有来源动作的 Blender 预览；但是正式验证继续使用根 AGENTS 指定的 Godot MCP。
