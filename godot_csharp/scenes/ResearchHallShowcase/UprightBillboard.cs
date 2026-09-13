using Godot;

namespace DualOntologyWorldEngine.Scenes;

// 让带附件的整组立牌绕脚点转向相机，保持 Y 轴竖直。
public partial class UprightBillboard : Node3D
{
    public override void _Process(double delta)
    {
        var camera = GetViewport().GetCamera3D();
        var direction = camera.GlobalPosition - GlobalPosition;
        GlobalRotation = new Vector3(0, Mathf.Atan2(direction.X, direction.Z), 0);
    }
}
