using Godot;

namespace DualOntologyWorldEngine.Character;

public enum CharacterState
{
	Idle,
	Walk,
	Run,
	Action
}

public partial class DstCharacter : CharacterBody2D
{
	[Export] public bool ExternalMovement { get; set; }
	[Export] public float WalkSpeed { get; set; } = 170.0f;
	[Export] public float RunSpeed { get; set; } = 290.0f;
	[Export] public float Acceleration { get; set; } = 1400.0f;
	[Export] public float Friction { get; set; } = 1200.0f;

	public DstCharacterVisual Visual { get; private set; } = null!;
	public AnimationPlayer AnimPlayer { get; private set; } = null!;
	public CollisionShape2D Collider { get; private set; } = null!;

	public CharacterState CurrentState { get; private set; } = CharacterState.Idle;

	private double _blinkTimer = 2.8;
	private double _blinkDuration = 0.0;
	private bool _isBlinking = false;
	private int _savedFaceIndex = 0;
	private bool _actionWasPressed;

	public override void _Ready()
	{
		SetupComponents();
	}

	private void SetupComponents()
	{
		Visual = GetNodeOrNull<DstCharacterVisual>("Visual");
		if (Visual == null)
		{
			Visual = new DstCharacterVisual { Name = "Visual" };
			AddChild(Visual);
		}

		AnimPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
		if (AnimPlayer == null)
		{
			AnimPlayer = new AnimationPlayer { Name = "AnimationPlayer" };
			AddChild(AnimPlayer);
		}

		if (!AnimPlayer.HasAnimationLibrary(""))
		{
			AnimPlayer.AddAnimationLibrary("", DstAnimationBank.CreateLibrary());
		}

		AnimPlayer.AnimationFinished += OnAnimationFinished;

		Collider = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (Collider == null)
		{
			Collider = new CollisionShape2D
			{
				Name = "CollisionShape2D",
				Position = new Vector2(0, -12),
				Shape = new CircleShape2D { Radius = 18.0f }
			};
			AddChild(Collider);
		}

		// 默认初始化为正面正眼看待机！
		Visual.SetFacingMode(FacingMode.Front, false);
		PlayStateAnimation(CharacterState.Idle);
	}

	public override void _PhysicsProcess(double delta)
	{
		HandleFaceInput();
		UpdateBlink(delta);
		bool actionPressed = Input.IsKeyPressed(Key.J) || Input.IsKeyPressed(Key.Space);
		bool actionJustPressed = actionPressed && !_actionWasPressed;
		_actionWasPressed = actionPressed;

		if (CurrentState == CharacterState.Action)
		{
			Velocity = Velocity.MoveToward(Vector2.Zero, Friction * (float)delta);
			if (!ExternalMovement) MoveAndSlide();
			return;
		}

		if (Input.IsActionJustPressed("ui_accept") || actionJustPressed)
		{
			TriggerAction();
			return;
		}

		Vector2 inputDir = GetMovementInput();
		bool isRunning = Input.IsKeyPressed(Key.Shift);

		float targetSpeed = inputDir == Vector2.Zero ? 0f : (isRunning ? RunSpeed : WalkSpeed);
		Vector2 targetVelocity = inputDir * targetSpeed;

		float rate = inputDir != Vector2.Zero ? Acceleration : Friction;
		Velocity = Velocity.MoveToward(targetVelocity, rate * (float)delta);

		if (!ExternalMovement) MoveAndSlide();

		if (inputDir != Vector2.Zero)
		{
			// 向上移动主导 -> 冲向内侧（背向奔跑）
			if (inputDir.Y < -0.1f && Mathf.Abs(inputDir.X) <= 0.35f)
			{
				SetFacing(FacingMode.Back, false);
			}
			// 垂直向下主导 -> 正面正视走来！
			else if (inputDir.Y > 0.1f && Mathf.Abs(inputDir.X) <= 0.35f)
			{
				SetFacing(FacingMode.Front, false);
			}
			// 水平移动主导 -> 侧面形态
			else if (Mathf.Abs(inputDir.X) > 0.05f)
			{
				SetFacing(FacingMode.Side, inputDir.X < 0);
			}

			CharacterState nextState = isRunning ? CharacterState.Run : CharacterState.Walk;
			ChangeState(nextState);
		}
		else if (Velocity.LengthSquared() < 100f)
		{
			ChangeState(CharacterState.Idle);
		}
	}

	public void SetFacing(FacingMode mode, bool faceLeft)
	{
		if (Visual.CurrentFacing != mode || Visual.IsFacingLeft != faceLeft)
		{
			Visual.SetFacingMode(mode, faceLeft);
			PlayStateAnimation(CurrentState);
		}
	}

	private static Vector2 GetMovementInput()
	{
		Vector2 dir = Vector2.Zero;
		if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) dir.X -= 1f;
		if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) dir.X += 1f;
		if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) dir.Y -= 1f;
		if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) dir.Y += 1f;

		return dir.Normalized();
	}

	public void TriggerAction()
	{
		ChangeState(CharacterState.Action);
	}

	private void ChangeState(CharacterState newState)
	{
		if (CurrentState == newState && AnimPlayer.IsPlaying()) return;

		CurrentState = newState;
		PlayStateAnimation(newState);
	}

	public void PlayStateAnimation(CharacterState state)
	{
		CurrentState = state;
		string animName = (Visual.CurrentFacing, state) switch
		{
			(FacingMode.Front, CharacterState.Walk) => DstAnimationBank.WalkFront,
			(FacingMode.Front, CharacterState.Run) => DstAnimationBank.RunFront,
			(FacingMode.Front, CharacterState.Action) => DstAnimationBank.ActionFront,
			(FacingMode.Front, _) => DstAnimationBank.IdleFront,

			(FacingMode.Side, CharacterState.Walk) => DstAnimationBank.WalkSide,
			(FacingMode.Side, CharacterState.Run) => DstAnimationBank.RunSide,
			(FacingMode.Side, CharacterState.Action) => DstAnimationBank.ActionSide,
			(FacingMode.Side, _) => DstAnimationBank.IdleSide,

			(FacingMode.Back, CharacterState.Walk) => DstAnimationBank.WalkBack,
			(FacingMode.Back, CharacterState.Run) => DstAnimationBank.RunBack,
			(FacingMode.Back, CharacterState.Action) => DstAnimationBank.ActionBack,
			(FacingMode.Back, _) => DstAnimationBank.IdleBack,

			_ => DstAnimationBank.IdleFront
		};

		if (AnimPlayer.CurrentAnimation != animName)
		{
			AnimPlayer.Play(animName);
			AnimPlayer.Advance(0);
		}
	}

	private void OnAnimationFinished(StringName animName)
	{
		if (animName == DstAnimationBank.ActionFront || 
			animName == DstAnimationBank.ActionSide || 
			animName == DstAnimationBank.ActionBack)
		{
			ChangeState(CharacterState.Idle);
		}
	}

	private void HandleFaceInput()
	{
		if (Input.IsKeyPressed(Key.Key1)) SetFace(0);
		else if (Input.IsKeyPressed(Key.Key2)) SetFace(1);
		else if (Input.IsKeyPressed(Key.Key3)) SetFace(2);
		else if (Input.IsKeyPressed(Key.Key4)) SetFace(3);
	}

	public void SetFace(int faceIndex)
	{
		_savedFaceIndex = faceIndex;
		if (!_isBlinking)
		{
			Visual.SetFaceIndex(faceIndex);
		}
	}

	private void UpdateBlink(double delta)
	{
		if (_isBlinking)
		{
			_blinkDuration -= delta;
			if (_blinkDuration <= 0.0)
			{
				_isBlinking = false;
				Visual.SetFaceIndex(_savedFaceIndex);
			}
			return;
		}

		if (CurrentState != CharacterState.Idle) return;

		_blinkTimer -= delta;
		if (_blinkTimer <= 0.0)
		{
			_isBlinking = true;
			_blinkDuration = 0.12;
			Visual.SetFaceIndex(1); // 眨眼贴图
			_blinkTimer = GD.RandRange(2.2, 4.5);
		}
	}
}
