# 公共研究厅 · 原生 3D 场景

打开 `ResearchHallShowcase.tscn`，按 F6 运行。场景根节点是 Node3D，采用 Camera3D、Sprite3D、CharacterBody3D 和原生深度缓冲。

## 在 Godot 中维护

- `Architecture`：地板和地毯位于 XZ 平面；三面墙是固定方向的竖直贴图，墙饰作为对应墙面的子节点。碰撞与显示分开，相机靠近的一侧墙面可隐藏。
- `Furniture`：书柜固定在墙前，其余家具使用绕 Y 轴转向相机的立牌。移动节点即可布景。`Desk` 是整组书桌，桌面立绘、书籍和墨水为其子节点，一起转向。
- `Hanging`：吊灯实际放在世界高处，可直接调整 Y 坐标。
- `Player`：3D 角色和碰撞体。透明 SubViewport 复用 CharacterShowcase 的动画、表情、挥击与输入，将结果交给 Sprite3D；二维角色不再移动位置，CharacterBody3D 负责世界移动。角色 Sprite3D 使用完整 Billboard，随相机旋转和俯仰始终正对镜头，以脚点为锚。
- `CameraRig/Pitch/Camera3D`：分别调整环绕、俯仰和镜头参数。支持透视或正交相机，编辑器保存的取景在正常运行时保留。

所有布景节点及贴图引用都已保存在 tscn 中。运行时不生成房间，不裁图，不使用屏幕坐标投影或 Y 排序。贴图位于素材目录 `cutouts/`，源图更改后才需要运行 `tools/prepare_research_hall.gd` 重新裁取。此前 v2 图板保留为美术迭代记录，当前 3D 场景采用原始家具图板裁出的纹理。

## 操作

WASD / 方向键移动，Shift 奔跑，J / 空格挥击，F 朝向，1–4 表情，R 回到中央。Q/E 环绕，PgUp/PgDn 俯仰，右键拖动视角，滚轮缩放。

## 验证

C# 编译通过，0 警告、0 错误。`tools/check_research_hall.gd` 验证了世界 X 轴移动、相机旋转后沿世界 Z 轴移动及 3D 墙体碰撞。

渲染样本保存在仓库的 `artifacts/research-hall/3d-*.png`。截图模式临时使用统一相机距离和 FOV，不写回场景文件：

```powershell
godot --path . --rendering-method gl_compatibility res://scenes/ResearchHallShowcase/ResearchHallShowcase.tscn -- --hall-capture-dir=C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/research-hall
```

当前家具仍是单张立牌，旋转相机会改变真实空间投影和遮挡，不会生成家具不存在的背面美术；家具碰撞与交互尚未添加。验证环境存在系统证书和着色器缓存写入报错，截图仍正常完成。

