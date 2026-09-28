using Godot;

namespace DualOntologyWorldEngine.Game.Reasoning;

/// <summary>
/// A small scene-level actor controller for the reasoning room.
/// It owns movement, facing and presentation gestures only; case state and
/// simulation rules remain with the scene that hosts this actor.
/// </summary>
public partial class ReasoningActor : CharacterBody2D
{
	private const float MoveSpeed = 80.0f;
	private const string DetectiveFramesPath =
		"res://ShareAsset/Internal/Characters/animation_v2/detective.tres";
	private const string PlaceholderFramesPath =
		"res://ShareAsset/Internal/Characters/animation_v2/placeholder.tres";

	private static readonly Vector2 SpriteOffset = new(0, -23);
	private static readonly Vector2 CollisionSize = new(16, 12);
	private static readonly Vector2 CollisionOffset = new(0, -6);

	private static readonly Dictionary<string, int> ContactFrames = new()
	{
		["talk"] = 1,
		["inspect"] = 2,
		["interact"] = 2,
		["sit"] = 2,
		["break"] = 3,
		["cast"] = 4,
	};

	private bool _isDetective = true;
	private string _facing = "front";
	private string _currentAction = "idle";
	private string? _gestureAction;
	private Action? _gestureContact;
	private bool _contactTriggered;
    private readonly Queue<Vector2> _navigation = new();
    private Action? _onArrival;
    private double _blockedTime;
    public bool Navigating => _navigation.Count > 0;
    public void FollowPath(IEnumerable<Vector2> points, Action arrived)
    {
        CancelNavigation(); foreach (var point in points) _navigation.Enqueue(point);
        _onArrival = arrived;
        if (_navigation.Count == 0) { _onArrival = null; arrived(); }
    }
    public void CancelNavigation() { _navigation.Clear(); _onArrival = null; _blockedTime = 0; }


	/// <summary>Uses the detective atlas when true, otherwise the placeholder atlas.</summary>
	[Export]
	public bool IsDetective
	{
		get => _isDetective;
		set
		{
			if (_isDetective == value) return;
			_isDetective = value;
			if (IsInsideTree()) LoadFrames();
		}
	}

	/// <summary>Whether this actor consumes the player movement and interaction keys.</summary>
	[Export]
	public bool PlayerControlled { get; set; } = true;

	/// <summary>Allows a player-controlled actor to be temporarily muted by its host.</summary>
	[Export]
	public bool InputEnabled { get; set; } = true;

	[Export]
	public string DisplayName { get; set; } = "侦探";

	/// <summary>The sprite used by the host for additional presentation work.</summary>
	public AnimatedSprite2D Sprite { get; private set; } = null!;

	/// <summary>True while a one-shot gesture is between its first and last frame.</summary>
	public bool Busy { get; private set; }

	/// <summary>Raised once when the controlled actor accepts an E/Enter interaction request.</summary>
	public event Action? InteractRequested;

	public override void _Ready()
	{
		EnsureSprite();
		EnsureCollision();
		LoadFrames();

		Sprite.FrameChanged += OnSpriteFrameChanged;
		Sprite.AnimationFinished += OnSpriteAnimationFinished;
		PlayAction("idle", restart: true);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!PlayerControlled)
		{
			// NPC/replay actors are driven by their host through PresentMotion.
			// Do not overwrite an externally presented walk with idle every frame.
			return;
		}

		if (!InputEnabled || Busy || IsEditingControlFocused())
		{
			if (!InputEnabled) CancelNavigation();
            if (!Busy) StopMotion();
			return;
		}

		var direction = ReadMovementInput();
        if (direction != Vector2.Zero) CancelNavigation();
        if (_navigation.Count > 0)
        {
            var goal = _navigation.Peek(); var offset = goal - Position;
            // Reach each corner before turning; a loose radius cuts into adjacent walls.
            if (offset.Length() <= 0.01f)
            {
                _navigation.Dequeue();
                if (_navigation.Count == 0) { StopMotion(); var done = _onArrival; _onArrival = null; done?.Invoke(); }
                return;
            }
            Face(offset); PlayAction("walk"); var before = Position;
            Velocity = offset.Normalized() * Mathf.Min(MoveSpeed, offset.Length() / (float)delta);
            MoveAndSlide();
            _blockedTime = Position.DistanceSquaredTo(before) < 0.01f ? _blockedTime + delta : 0;
            if (_blockedTime > 0.75) { CancelNavigation(); StopMotion(); }
            return;
        }
		if (direction == Vector2.Zero)
		{
			StopMotion();
			return;
		}

		Face(direction);
		Velocity = direction * MoveSpeed;
		MoveAndSlide();
		PlayAction("walk");
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (!PlayerControlled || !InputEnabled || Busy || IsEditingControlFocused()) return;
		if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

		if (key.Keycode is Key.E or Key.Enter or Key.KpEnter)
		{
			InteractRequested?.Invoke();
			GetViewport().SetInputAsHandled();
		}
	}

	/// <summary>Turns toward the dominant axis of a non-zero direction.</summary>
	public void Face(Vector2 direction)
	{
		if (direction == Vector2.Zero) return;

		_facing = Mathf.Abs(direction.X) > Mathf.Abs(direction.Y)
			? (direction.X < 0 ? "left" : "right")
			: (direction.Y < 0 ? "back" : "front");

		if (!Busy) PlayAction(_currentAction is "walk" ? "walk" : "idle");
	}

	/// <summary>
	/// Plays one non-looping action. The optional callback fires at that action's
	/// contact frame, at most once, and the actor returns to facing idle on finish.
	/// Calls made while another gesture is active are ignored.
	/// </summary>
	public void PlayGesture(string action, Action? contact = null)
	{
		if (Busy || Sprite.SpriteFrames is null) return;
		if (action is "idle" or "walk" || !Sprite.SpriteFrames.HasAnimation(AnimationName(action))) return;

		Busy = true;
		Velocity = Vector2.Zero;
		_gestureAction = action;
		_gestureContact = contact;
		_contactTriggered = false;
		PlayAction(action, restart: true);
	}

	/// <summary>Stops movement and leaves the actor in the current facing's idle pose.</summary>
	public void StopMotion()
	{
		Velocity = Vector2.Zero;
		if (!Busy) PlayAction("idle");
	}

	/// <summary>
	/// Presents one externally scheduled movement sample, such as a replay step.
	/// The host owns timing; this method only applies the position and animation.
	/// </summary>
	public void PresentMotion(Vector2 from, Vector2 to)
	{
		if (Busy) return;

		if (to == from)
		{
			Position = to;
			StopMotion();
			return;
		}

		Face(to - from);
		Position = to;
		PlayAction("walk");
	}

	private void EnsureSprite()
	{
		Sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D") ?? new AnimatedSprite2D
		{
			Name = "AnimatedSprite2D",
			Centered = true,
		};
		if (Sprite.GetParent() is null) AddChild(Sprite);
		Sprite.Centered = true;
		Sprite.Position = SpriteOffset;
		Sprite.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
	}

	private void EnsureCollision()
	{
		var collision = GetNodeOrNull<CollisionShape2D>("CollisionShape2D") ?? new CollisionShape2D
		{
			Name = "CollisionShape2D",
		};
		if (collision.GetParent() is null) AddChild(collision);

		var shape = collision.Shape as RectangleShape2D ?? new RectangleShape2D();
		shape.Size = CollisionSize;
		collision.Shape = shape;
		collision.Position = CollisionOffset;
	}

	private void LoadFrames()
	{
		if (Sprite is null) return;
		Sprite.SpriteFrames = GD.Load<SpriteFrames>(IsDetective ? DetectiveFramesPath : PlaceholderFramesPath);
		Sprite.Position = SpriteOffset;
		Sprite.FlipH = _facing == "right";
	}

	private void PlayAction(string action, bool restart = false)
	{
		if (Sprite.SpriteFrames is null) return;
		var animation = AnimationName(action);
		if (!Sprite.SpriteFrames.HasAnimation(animation)) return;

		var changed = Sprite.Animation != animation;
		Sprite.FlipH = _facing == "right";
		_currentAction = action;
		if (changed || restart)
		{
			Sprite.Play(animation);
			Sprite.SetFrameAndProgress(0, 0);
		}
	}

	private string AnimationName(string action) =>
		$"{action}_{(_facing == "right" ? "left" : _facing)}";

	private void OnSpriteFrameChanged()
	{
		if (!Busy || _gestureAction is null || _contactTriggered || _gestureContact is null) return;
		if (!ContactFrames.TryGetValue(_gestureAction, out var contactFrame)) return;
		if (Sprite.Frame < contactFrame) return;

		_contactTriggered = true;
		_gestureContact.Invoke();
	}

	private void OnSpriteAnimationFinished()
	{
		if (!Busy || _gestureAction is null) return;

		Busy = false;
		_gestureAction = null;
		_gestureContact = null;
		_contactTriggered = false;
		PlayAction("idle", restart: true);
	}

	private static Vector2 ReadMovementInput()
	{
		var direction = Vector2.Zero;
		if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) direction.X -= 1;
		if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) direction.X += 1;
		if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) direction.Y -= 1;
		if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) direction.Y += 1;
		return direction.Normalized();
	}

	private bool IsEditingControlFocused()
	{
		var focusOwner = GetViewport().GuiGetFocusOwner();
		return focusOwner is TextEdit or LineEdit;
	}
}
