using Godot;
using DualOntologyWorldEngine.Character;

namespace DualOntologyWorldEngine.Scenes;

public partial class CharacterShowcase : Node2D
{
	[Export] public bool EmbeddedInRoom { get; set; }
	public DstCharacter Character => _character;
	private DstCharacter _character = null!;
	private Label _statusLabel = null!;

	public override void _Ready()
	{
		if (!EmbeddedInRoom) BuildEnvironment();
		BuildCharacter();
		if (!EmbeddedInRoom) BuildUI();
	}

	private void BuildEnvironment()
	{
		// 1. 舞台背景（复用庄园舞台背景）
		var bgTexture = DstSpriteLoader.LoadTexture("res://ShareAsset/assets/manor-stage.png");
		if (bgTexture != null)
		{
			var bgSprite = new Sprite2D
			{
				Name = "StageBackground",
				Texture = bgTexture,
				Position = new Vector2(740, 450),
				ZIndex = -10
			};
			AddChild(bgSprite);
		}

		// 2. 地面装饰辅助线（指示站立基线）
		var groundGuide = new Line2D
		{
			Name = "GroundGuide",
			Width = 3f,
			DefaultColor = new Color(0.8f, 0.7f, 0.5f, 0.35f),
			ZIndex = -5
		};
		groundGuide.AddPoint(new Vector2(100, 600));
		groundGuide.AddPoint(new Vector2(1380, 600));
		AddChild(groundGuide);
	}

	private void BuildCharacter()
	{
		_character = new DstCharacter
		{
			Name = "PlayerCharacter",
			Position = new Vector2(740, 600)
		};
		AddChild(_character);
	}

	private void BuildUI()
	{
		var canvas = new CanvasLayer { Name = "HUD" };
		AddChild(canvas);

		var margin = new MarginContainer
		{
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f
		};
		margin.AddThemeConstantOverride("margin_left", 32);
		margin.AddThemeConstantOverride("margin_top", 32);
		margin.AddThemeConstantOverride("margin_right", 32);
		margin.AddThemeConstantOverride("margin_bottom", 32);
		canvas.AddChild(margin);

		// 左上角信息面板
		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(380, 220),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
		};
		margin.AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 10);
		panel.AddChild(vbox);

		var titleLabel = new Label
		{
			Text = "DST 剪纸分件角色运动体系",
			HorizontalAlignment = HorizontalAlignment.Left
		};
		titleLabel.AddThemeFontSizeOverride("font_size", 20);
		titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.88f, 0.45f));
		vbox.AddChild(titleLabel);

		var guideLabel = new Label
		{
			Text = "【操作控制指南】\n" +
				   "• W / ↑ : 冲向内侧（背向奔跑，展现内侧背面与发髻）\n" +
				   "• S / ↓ : 向下正视走来（正眼看镜头迈步）\n" +
				   "• A / D / ← / → : 侧向移动（经典饥荒疾走步态，自动左右翻转）\n" +
				   "• 按住 Shift : 奔跑（快速大步跨越）\n" +
				   "• Space / J : 动作/挥击（蓄力前击）\n" +
				   "• F 键 : 循环切换 正面 / 侧面 / 冲向内侧(背面) 形态\n" +
				   "• 数字键 1-4 : 切换表情（1:平静 2:眨眼/闭眼 3:半睁 4:睁眼）",
			HorizontalAlignment = HorizontalAlignment.Left
		};
		guideLabel.AddThemeFontSizeOverride("font_size", 14);
		guideLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
		vbox.AddChild(guideLabel);

		_statusLabel = new Label
		{
			Text = "状态: 待机 (Idle)",
			HorizontalAlignment = HorizontalAlignment.Left
		};
		_statusLabel.AddThemeFontSizeOverride("font_size", 16);
		_statusLabel.AddThemeColorOverride("font_color", new Color(0.45f, 0.95f, 0.55f));
		vbox.AddChild(_statusLabel);
	}

	private bool _wasFPressed = false;

	public override void _Process(double delta)
	{
		if (_character == null) return;

		bool isFPressed = Input.IsKeyPressed(Key.F);
		if (isFPressed && !_wasFPressed)
		{
			var nextFacing = _character.Visual.CurrentFacing switch
			{
				FacingMode.Front => FacingMode.Side,
				FacingMode.Side => FacingMode.Back,
				_ => FacingMode.Front
			};
			_character.SetFacing(nextFacing, false);
		}
		_wasFPressed = isFPressed;
		if (EmbeddedInRoom) return;

		string facingStr = _character.Visual.IsFacingLeft ? "向左" : "向右";
		string modeStr = _character.Visual.CurrentFacing switch
		{
			FacingMode.Front => "正面 (正视镜头)",
			FacingMode.Side => "侧面 (侧视)",
			FacingMode.Back => "冲向内侧 (背视)",
			_ => "未知"
		};
		float speed = _character.Velocity.Length();

		_statusLabel.Text = $"【实时状态】: {_character.CurrentState}\n" +
							$"【视效朝向】: {modeStr} | {facingStr}\n" +
							$"【当前速度】: {speed:F0} px/s\n" +
							$"【当前表情】: 编号 {_character.Visual.CurrentFaceIndex}";

		HandleCaptureSequence();
	}

	private int _captureFrame;
	private string? _captureDirectory;

	private void HandleCaptureSequence()
	{
		if (_captureFrame == 0)
		{
			_captureDirectory = OS.GetCmdlineUserArgs()
				.FirstOrDefault(arg => arg.StartsWith("--capture-dir="))?[14..];
			if (_captureDirectory == null) return;
			System.IO.Directory.CreateDirectory(_captureDirectory);
			_character.SetPhysicsProcess(false);
		}
		int sample = _captureFrame / 12;
		if (sample >= 56) return;
		if (_captureFrame % 12 == 0)
		{
			var facing = sample < 36 ? (FacingMode)(sample / 12)
				: sample < 48 ? FacingMode.Side : (FacingMode)((sample - 48) / 4);
			var state = sample < 48 ? (CharacterState)(sample / 3 % 4) : CharacterState.Idle;
			_character.SetFacing(facing, sample is >= 36 and < 48);
			_character.SetFace(sample >= 48 ? (sample - 48) % 4 : 0);
			_character.PlayStateAnimation(state);
			_character.AnimPlayer.Pause();
			_character.AnimPlayer.Seek(sample < 48
				? _character.AnimPlayer.CurrentAnimationLength * (sample % 3) / 3 : 0, true);
		}
		if (_captureFrame % 12 == 10)
			SaveScreenshot(sample);
		_captureFrame++;
	}

	private async void SaveScreenshot(int sample)
	{
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		string filename = $"{sample:D2}_{_character.Visual.CurrentFacing}_{_character.CurrentState}.png";
		var error = GetViewport().GetTexture().GetImage().SavePng(
			System.IO.Path.Combine(_captureDirectory!, filename));
		GD.Print($"[Capture] {filename}: {error}");
		if (sample == 55)
		{
			foreach (FacingMode facing in Enum.GetValues<FacingMode>())
			{
				_character.SetFacing(facing, false);
				_character.PlayStateAnimation(CharacterState.Idle);
				_character.TriggerAction();
				_character.AnimPlayer.Advance(_character.AnimPlayer.CurrentAnimationLength + 0.01);
				if (_character.CurrentState != CharacterState.Idle)
				{
					GD.PushError($"[Capture] {facing}: 挥击结束后未恢复待机");
					GetTree().Quit(1);
					return;
				}
			}
			GD.Print("[Capture] 三个朝向的挥击均正确恢复待机");
			GetTree().Quit();
		}
	}
}
