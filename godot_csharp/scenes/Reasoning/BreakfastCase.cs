using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Game.Reasoning;

public sealed class Investigator(string name, string room)
{
	public static readonly Tag Place = new("所在房间"), Calibrated = new("已校时");
	public static string[] Rooms { get; } = ["走廊", "厨房", "餐厅"];
	public SoulEssence Soul { get; } = new(name);
	public Essence Body { get; } = new(name, ProjectionKind.Body);
	public Projection Self { get; set; } = null!;
	public Projection Reality { get; set; } = null!;
	public string Name => Soul.TrueName;
	public string InitialRoom { get; } = room;
	public string Room => Rooms[Body.Get<StateProperty>()!.Values.GetValueOrDefault(Place)];
	public bool Synced => Self.Tags.GetValueOrDefault(Calibrated) == 1;
	public Projection Device { get; set; } = null!;
	public bool Listening { get; set; }
	public string Draft { get; set; } = "校时\n监听 25\n前往 餐厅\n提交记录\n比较记录";
	public List<SoundRecord> Records { get; } = [];
	public Queue<Instruction> Plan { get; } = new();
	public string Activity { get; set; } = "尚未执行";
	public bool Busy { get; set; }
}

// Signal 对象引用表示同一次发声；记录不会将该隐藏来源呈现给玩家。
public sealed record Signal(double Emitted, bool CabinetOpen, string Pattern, int Configuration);
public sealed record SoundRecord(Investigator Witness, string Pattern, double Heard, string Room, bool Synced, int Configuration, bool? CabinetOpen);

public sealed class BreakfastCase : IDisposable
{
	private static readonly Tag ActionCount = new("调查动作");
	private readonly WorldRuntime _world = new();
	private readonly Dictionary<ActionExecution, (Investigator Actor, Instruction Instruction)> _pending = [];
	private readonly List<SoundRecord> _board = [];
	private int _configuration;
	private bool _inspected;
	private double _firstDelay, _secondDelay;
	private readonly Essence _device = new("保温柜接收器");
	private static readonly Tag Opened = new("阀门开启");
	public SimulationSession Session { get; }
	public Investigator[] People { get; } = [new("工程师", "走廊"), new("主厨", "厨房")];
	public List<string> Journal { get; } = [];
	public int Stage { get; private set; }
	public bool CabinetOpen => _device.Get<StateProperty>()!.Values[Opened] == 1;
	public int Accepted { get; private set; }
	public double Now => Session.CTB.Now;
	public bool Busy => People.Any(p => p.Busy);
	public event Action? Changed;

	public BreakfastCase()
	{
		Session = new(_world);
		var deviceState = _device.Add(new StateProperty()); deviceState.Values[Opened] = 1;
		var deviceView = _world.Project(_device, "厨房设备");
		foreach (var actor in People)
		{
			var state = actor.Body.Add(new StateProperty());
			state.Observable.UnionWith([ActionCount, Investigator.Place, Investigator.Calibrated]);
			state.Values[Investigator.Place] = Array.IndexOf(Investigator.Rooms, actor.InitialRoom);
			state.Values[Investigator.Calibrated] = 0;
			actor.Soul.Relations.Add((Relations.Controls, actor.Body));
			_world.Project(actor.Soul, actor.Name + "的意识");
			actor.Reality = _world.Project(actor.Body, actor.Name);
			actor.Self = _world.Observe(actor.Soul, [actor.Reality])[0];
			actor.Device = _world.Observe(actor.Soul, [deviceView])[0];
		}
		Session.Completed += Complete;
		Session.CTB.Schedule(8, Bell);
		Log("早餐争执：两人的钟一致，却都认为对方弄错了时间。先在两处校时、监听，再到餐厅提交记录。");
	}

	public void Log(string message)
	{
		Journal.Add($"{Now,5:0}  {message}");
		if (Journal.Count > 160) Journal.RemoveAt(0);
		Changed?.Invoke();
	}

	private void Know(Investigator actor, string fact, ObservationSource source = ObservationSource.Perception)
		=> Observation.Observe(actor.Soul, new(fact, ProjectionKind.Event) { Source = source, Time = Now, Existence = Truth.True });

	private void Unlock(int stage, string reason)
	{
		if (stage <= Stage) return;
		Stage = stage;
		foreach (var actor in People) Know(actor, reason, ObservationSource.Testimony);
		Log("调查札记更新：" + reason);
	}

	public string? Run(Investigator actor, string draft)
	{
		if (actor.Busy) return "此人物正在执行方案，请等待完成或重置实验。";
		var parsed = RuleLanguage.Parse(draft, Stage);
		if (parsed.Errors.Count > 0) return string.Join("\n", parsed.Errors);
		if (parsed.Program.Count == 0) return "请先输入行动方案。";
		actor.Plan.Clear();
		foreach (var instruction in parsed.Program) actor.Plan.Enqueue(instruction);
		actor.Busy = true;
		Next(actor);
		return null;
	}

	private void Next(Investigator actor)
	{
		actor.Listening = false;
		if (!actor.Plan.TryDequeue(out var instruction))
		{ actor.Busy = false; actor.Activity = "方案完成"; Changed?.Invoke(); return; }
		if (instruction.NeedsSync && !actor.Synced)
		{ Log(actor.Name + "：未校时，跳过条件分支。"); Next(actor); return; }
		actor.Listening = instruction.Op == TaskOp.Listen;
		if (actor.Listening) _windows[actor] = (actor.Room, Now, _configuration);
		actor.Activity = $"第 {instruction.Line + 1} 行 · {instruction.Op switch { TaskOp.Listen => "监听中", TaskOp.Go => "前往" + instruction.Argument, TaskOp.Wait => "等待中", _ => "执行中" }}";
		// 每条行动均由 Core 结算，游戏专用后果在成功回调中发生。
		Effect effect = instruction.Op switch
		{
			TaskOp.Go => new Effect.Set(actor.Self, Investigator.Place, Array.IndexOf(Investigator.Rooms, instruction.Argument)),
			TaskOp.Sync => new Effect.Set(actor.Self, Investigator.Calibrated, 1),
			TaskOp.Open => new Effect.Set(actor.Device, Opened, 1),
			TaskOp.Close => new Effect.Set(actor.Device, Opened, 0),
			_ => new Effect.Change(actor.Self, ActionCount, 1)
		};
		Condition? requirement = instruction.Op is TaskOp.Open or TaskOp.Close
			? new Condition.All([new Condition.Tagged(actor.Self, Investigator.Place, 1), new Condition.Not(new Condition.Tagged(actor.Self, Investigator.Place, 2))]) : null;
		var action = Session.Submit(actor.Soul, new(actor.Self, effect, instruction.Duration, requirement));
		_pending.Add(action, (actor, instruction));
		Changed?.Invoke();
	}

	private void Complete(ActionExecution execution)
	{
		if (!_pending.Remove(execution, out var pending)) return;
		var (actor, instruction) = pending;
		if (_windows.Remove(actor, out var window) && window.Configuration == _configuration)
			CompletedWindows.Add((actor, window.Room, window.Start, Now, _configuration));
		if (execution.Status != ExecutionStatus.Completed)
		{ actor.Plan.Clear(); Log(actor.Name + "：行动未完成，设备操作要求人物在厨房。后续行动已停止。"); Next(actor); return; }
		_world.Observe(actor.Soul, [actor.Reality], Now);
		switch (instruction.Op)
		{
			case TaskOp.Go: Log(actor.Name + "抵达" + actor.Room); break;
			case TaskOp.Sync: Know(actor, "已用公共钟校准怀表"); Log(actor.Name + "完成校时。"); break;
			case TaskOp.Publish:
				if (actor.Room != "餐厅") { Log(actor.Name + "：调查札记在餐厅，未提交。"); break; }
				foreach (var record in actor.Records.Where(r => !_board.Contains(r))) _board.Add(record);
				foreach (var span in CompletedWindows.Where(w => w.Witness == actor && !_publicWindows.Contains(w))) _publicWindows.Add(span);
				foreach (var person in People) Know(person, actor.Name + "公开了监听记录", ObservationSource.Testimony);
				Log(actor.Name + "已将记录写入共同调查札记。"); break;
			case TaskOp.Compare: Compare(actor); break;
			case TaskOp.Inspect:
				if (!Kitchen(actor)) break;
				_inspected = true;
				Unlock(2, "检查发现：保温柜的开关同时带动一处旧管阀。现在可以开启、关闭保温柜进行对照。"); break;
			case TaskOp.Open:
			case TaskOp.Close:
				if (!Kitchen(actor)) break;
				_configuration++;
				Log(actor.Name + (CabinetOpen ? "开启" : "关闭") + "保温柜，开始新的实验条件。"); break;
			case TaskOp.Knock:
				if (actor.Room != "走廊") { Log("敲铃装置在走廊，本次没有发声。"); break; }
				var emitted = Emit("两短一长");
				actor.Records.Add(new(actor, emitted.Pattern, Now, "走廊", actor.Synced, _configuration, _inspected ? CabinetOpen : null));
				Know(actor, "亲自敲出两短一长并记录发声时刻");
				Log(actor.Name + "敲出两短一长并记录发声时刻。"); break;
		}
		Next(actor);
	}

	private bool Kitchen(Investigator actor)
	{
		if (actor.Room == "厨房") return true;
		Log(actor.Name + "：需到厨房操作设备，本次未改变设备。"); return false;
	}

	private void Bell() { Emit("早餐铃"); Session.CTB.Schedule(20, Bell); }
	private Signal Emit(string pattern)
	{
		var signal = new Signal(Now, CabinetOpen, pattern, _configuration);
		Hear(signal, "走廊", false);
		Session.CTB.Schedule(1, () => Hear(signal, "厨房", false));
		if (CabinetOpen) Session.CTB.Schedule(5, () => Hear(signal, "厨房", true));
		return signal;
	}

	private void Hear(Signal signal, string room, bool echo)
	{
		if (room == "厨房") Accepted++;
		foreach (var actor in People.Where(p => p.Room == room && p.Listening))
		{
			var record = new SoundRecord(actor, signal.Pattern, Now, room, actor.Synced, _configuration, _inspected ? CabinetOpen : null);
			actor.Records.Add(record);
			Know(actor, $"{room}在 {Now:0} 听见{signal.Pattern}；{(actor.Synced ? "已校时" : "未校时")}");
			Log($"{actor.Name}记录：{room}，{signal.Pattern}，{(actor.Synced ? Now.ToString("0") : "时刻未校准")}。");
		}
		Changed?.Invoke();
	}

	private void Compare(Investigator actor)
	{
		if (actor.Room != "餐厅") { Log("需要在餐厅阅读共同调查札记。"); return; }
		// 比较只读取公开记录，不使用 Signal 来源引用或隐藏的 Echo 标记。
		var starts = _board.Where(r => r.Synced && r.Room == "走廊").ToArray();
		SoundRecord[] Following(SoundRecord start) => _board.Where(r => r.Synced && r.Room == "厨房" &&
			r.Pattern == start.Pattern && r.Configuration == start.Configuration && r.Heard > start.Heard && r.Heard < start.Heard + 10)
			.DistinctBy(r => r.Heard).OrderBy(r => r.Heard).ToArray();
		var repeated = starts.FirstOrDefault(s => Following(s).Length >= 2);
		if (Stage == 0 && repeated is not null)
		{
			var heard = Following(repeated);
			_firstDelay = heard[0].Heard - repeated.Heard; _secondDelay = heard[1].Heard - repeated.Heard;
			Unlock(1, $"校时记录显示：走廊一次铃声之后，厨房在 {_firstDelay:0}、{_secondDelay:0} 行动值后各有一次声音。可检查保温柜寻找原因。"); return;
		}
		var on = starts.Any(s => s.CabinetOpen == true && s.Configuration > 0 && Following(s).Any(r => r.Heard == s.Heard + _secondDelay));
		// 阴性证据要求厨房人物完整监听到可能重放的时刻，不能把没有记录当作没有声音。
		var off = starts.Any(s => s.CabinetOpen == false && Following(s).Any(r => r.Heard == s.Heard + _firstDelay) &&
			!Following(s).Any(r => r.Heard == s.Heard + _secondDelay) && _publicWindows.Any(w =>
			w.Room == "厨房" && w.Start <= s.Heard + _firstDelay && w.End >= s.Heard + _secondDelay && w.Configuration == s.Configuration));
		if (Stage == 2 && _inspected && on && off)
		{ Unlock(3, "开启与关闭的完整对照支持：旧管阀控制额外声音。可敲击两短一长，检验声音是否重复传播。"); return; }
		if (Stage == 3 && starts.Any(s => s.Pattern == "两短一长" && Following(s).Any(r => r.Heard == s.Heard + _firstDelay) &&
			Following(s).Any(r => r.Heard == s.Heard + _secondDelay)))
		{ Unlock(4, "特定节奏同样再次出现，支持重复传播解释。言灵试验台已开放：用道本语精确描述声音的来源与时间范围。"); return; }
		Log("比较尚未推进：需两处已校时记录；设备对照需主动开、关各一轮，并完整监听；新实验后重新提交记录。");
	}

	public List<(Investigator Witness, string Room, double Start, double End, int Configuration)> CompletedWindows { get; } = [];
	private readonly List<(Investigator Witness, string Room, double Start, double End, int Configuration)> _publicWindows = [];
	private readonly Dictionary<Investigator, (string Room, double Start, int Configuration)> _windows = [];
	public bool Step()
	{
		var result = Session.CTB.Step();
		Changed?.Invoke(); return result;
	}
	public void Dispose() => Session.Dispose();
}
