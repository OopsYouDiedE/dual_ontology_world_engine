using Godot;
using DualOntologyWorldEngine.Character;

namespace DualOntologyWorldEngine.Scenes;

// 只处理相机和输入；房间、材质、角色桥接节点均保存在 tscn 中。
public partial class ResearchHallShowcase : Node3D
{
	private Node3D _rig = null!;
	private Node3D _pitch = null!;
	private Camera3D _camera = null!;
	private CharacterBody3D _body = null!;
	private DstCharacter _actor = null!;
	private string? _captureDirectory;
	private int _frame;

	public override void _Ready()
	{
		_rig = GetNode<Node3D>("CameraRig");
		_pitch = GetNode<Node3D>("CameraRig/Pitch");
		_camera = GetNode<Camera3D>("CameraRig/Pitch/Camera3D");
		_body = GetNode<CharacterBody3D>("Player");
		_actor = GetNode<CharacterShowcase>("Player/Portrait/CharacterShowcase").Character;
		_actor.Position = new Vector2(256, 450);
		_actor.Scale = Vector2.One * .85f;
		_actor.ExternalMovement = true;
		ProcessPhysicsPriority = 10;
		_captureDirectory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--hall-capture-dir="))?[19..];
		if (_captureDirectory != null)
		{
			System.IO.Directory.CreateDirectory(_captureDirectory);
			// 截图使用统一取景，不改写编辑器保存的相机参数。
			_camera.Position = new Vector3(0, 0, 22);
			_camera.Fov = 43;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		var right = _camera.GlobalBasis.X;
		var forward = -_camera.GlobalBasis.Z;
		right.Y = forward.Y = 0;
		_body.Velocity = (right.Normalized() * _actor.Velocity.X - forward.Normalized() * _actor.Velocity.Y) / 100f;
		_body.MoveAndSlide();
		if (Input.IsKeyPressed(Key.R)) _body.Position = new Vector3(0, 0, 1);
		float turn = (Input.IsKeyPressed(Key.Q) ? 1 : 0) - (Input.IsKeyPressed(Key.E) ? 1 : 0);
		float tilt = (Input.IsKeyPressed(Key.Pageup) ? -1 : 0) + (Input.IsKeyPressed(Key.Pagedown) ? 1 : 0);
		Orbit(turn * (float)delta, tilt * (float)delta * .5f);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion && Input.IsMouseButtonPressed(MouseButton.Right))
			Orbit(-motion.Relative.X * .005f, -motion.Relative.Y * .005f);
		if (@event is InputEventMouseButton { Pressed: true } button)
		{
			float step = button.ButtonIndex == MouseButton.WheelUp ? -1 : button.ButtonIndex == MouseButton.WheelDown ? 1 : 0;
			if (_camera.Projection == Camera3D.ProjectionType.Orthogonal)
				_camera.Size = Mathf.Clamp(_camera.Size + step, 10, 25);
			else
				_camera.Position = new Vector3(0, 0, Mathf.Clamp(_camera.Position.Z + step, 12, 30));
		}
	}

	private void Orbit(float yaw, float tilt)
	{
		_rig.RotateY(yaw);
		_pitch.Rotation = new Vector3(Mathf.Clamp(_pitch.Rotation.X + tilt, -1.15f, -.3f), 0, 0);
		// 隐藏靠近相机的一侧墙面，仅影响显示；实体边界仍然保留。
		Vector3 view = _camera.GlobalPosition - _rig.GlobalPosition;
		GetNode<Node3D>("Architecture/Back/Art").Visible = view.Z > -2;
		GetNode<Node3D>("Architecture/Left/Art").Visible = view.X > -2;
		GetNode<Node3D>("Architecture/Right/Art").Visible = view.X < 2;
	}

	public override void _Process(double delta)
	{
		if (_captureDirectory == null) return;
		int frame = _frame++;
		if (frame == 45) Orbit(.65f, 0);
		if (frame == 75) Orbit(-1.3f, -.3f);
		if (frame is 30 or 60 or 90) Capture(frame);
	}

	private async void Capture(int frame)
	{
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		string file = frame == 30 ? "3d-overview.png" : frame == 60 ? "3d-orbit.png" : "3d-high-angle.png";
		var error = GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_captureDirectory!, file));
		GD.Print($"[ResearchHall3D] {file}: {error}");
		if (frame == 90) GetTree().Quit(error == Error.Ok ? 0 : 1);
	}
}
