using DualOntologyWorldEngine.Core;
using System.Collections.Immutable;
namespace DualOntologyWorldEngine.Game.FirstAwakening;

/// <summary>现场会话只保管实际材料；候选历史每次单独建立现实模型，不改变现场。</summary>
public sealed class RedTokenSession : IDisposable
{
	private readonly Dictionary<string, Projection> _evidence = new();
	private readonly Dictionary<string, string> _candidate = new();
	private static readonly Tag Inspected = new("Inspected");
	public WorldRuntime World { get; } = new();
	public SoulEssence Investigator { get; } = new("侦探");
	public RedTokenSimulation Physical { get; } = new();
	private readonly RedTokenSimulation _safe = new();
	public int Round { get; private set; } = 1;
	public bool Ended { get; private set; }
	public bool Solved { get; private set; }
	public IEnumerable<string> Available => _evidence.Keys.Where(id => id != "E16" || Physical.Value("pipe", RedTokenSimulation.Taken) == 1);
	public IEnumerable<string> Known => Investigator.World.Projections.Where(p => p.Tags.GetValueOrDefault(Inspected) == 1).Select(p => p.Name);
	public bool WaitingForConfirmation => Physical.Waiting;
	public string? SafeHolder => _safe.Holder;
	public RedTokenSession()
	{
		for (int i = 1; i <= 16; i++) Add($"E{i:00}");
		Add("E09.deep"); Add("E09.completion");
	}
	private void Add(string id)
	{
		var obj = new Essence(id); var state = obj.Add(new StateProperty());
		state.Observable.Add(Inspected); state.Values[Inspected] = 1;
		_evidence.Add(id, World.Project(obj, id));
	}
	public RedTokenResult Inspect(string id)
	{
		if (Ended || !_evidence.TryGetValue(id, out var evidence)) return new(false, "没有这份现场材料。");
		if (id == "E01" && new[] { "侦探", "工程师霍铎", "主厨沈鼎", "音乐家苏泠" }.Any(a => _safe.Value("reward:" + a, RedTokenSimulation.Read) != 1)) return new(false, "请四人分别核验本人的约定报偿与可信凭证。" );
		if (id == "E16" && Physical.Value("pipe", RedTokenSimulation.Taken) != 1) return new(false, "请先拿接收印与椅筹矛盾，在现实铜口质询索取移物单。");
		if (id == "E12")
		{
			// 触本人签收与楔的片段分别进入其自身世界，侦探得到的是具名转述。
			Observation.Observe(Physical.Souls["工程师霍铎"], new ObservationInput("本人签收折页：合门后台侧仍响", ProjectionKind.Event) { Source = ObservationSource.Recall, Existence = Truth.True });
			Observation.Observe(Physical.Souls["音乐家苏泠"], new ObservationInput("本人楔：合门后门侧静", ProjectionKind.Event) { Source = ObservationSource.Recall, Existence = Truth.True });
			Observation.Observe(Investigator, new ObservationInput(id) { Source = ObservationSource.Testimony, Existence = Truth.True, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Inspected, 1), Details = "工程师与音乐家分别转述本人触物片段；并非直接历史实证。" });
		}
		else World.Observe(Investigator, id == "E09" ? new[] { evidence, _evidence["E09.completion"] } : new[] { evidence });
		return new(true, id == "E09.deep" ? "上游当班捕集片有新灰；落水空隙使盆水不能逆流生成此灰。" : $"现场材料{id}已进入侦探认知。");
	}
	private bool Knows(string id) => Known.Contains(id);
	public RedTokenResult Perform(string actor, string action, string station)
	{
		if (Ended) return new(false, "本次调查已经结束。");
		if (action == "open_copper" && (!Knows("E13") || !Knows("E15"))) return new(false, "先核对同物接收印与晨间椅筹的矛盾，再现实质询院务。");
		var before = Physical.Snapshots.Count;
		var result = Physical.Perform(actor, action, station);
		return result with { Snapshots = Physical.Snapshots.Skip(before).ToArray() };
	}
	public IReadOnlyDictionary<string, object> SafeState => new Dictionary<string, object> { ["pressed"] = _safe.Flowing, ["gap_sound"] = _safe.Value("door", RedTokenSimulation.Sound) == 1, ["pipe_sound"] = _safe.Value("pipe", RedTokenSimulation.Sound) == 1, ["red_received"] = _safe.Value("red", RedTokenSimulation.Received) == 1, ["handle_received"] = _safe.Value("handle", RedTokenSimulation.Received) == 1, ["white"] = _safe.WhiteAvailable, ["rewards_verified"] = new[] { "侦探", "工程师霍铎", "主厨沈鼎", "音乐家苏泠" }.Count(a => _safe.Value("reward:" + a, RedTokenSimulation.Read) == 1) };
	public RedTokenResult SafeTest(string operation)
	{
		if (Ended) return new(false, "本次调查已经结束。");
		var parts = operation.Split(':', 2);
		string actor = parts.Length > 1 ? parts[1] : "工程师霍铎";
		var action = parts[0] switch { "reward" => "verify_reward", "press" => "safe_press", "release" => "release", "gap" => "knock_gap", "pipe" => "knock_pipe", "close" => "close_door", "red" => "deposit_red", "handle" => "deposit_other", "push_red" => "push_red", "push_handle" => "push_other", "white" => "take_white", _ => "" };
		if (action == "") return new(false, "没有该安全试验。");
		var before = _safe.Snapshots.Count;
		var result = _safe.Perform(actor, action);
		var message = result.Ok ? parts[0] switch
		{
			"reward" => actor + "已核验本人约定报偿与凭证。",
			"gap" => _safe.Value("door", RedTokenSimulation.Sound) == 1 ? "门侧轻敲：当前门缝传声。" : "门侧轻敲：当前合门试件不传声。",
			"pipe" => _safe.Value("pipe", RedTokenSimulation.Sound) == 1 ? "台侧轻敲：压力铜管仍传声。" : "台侧轻敲：压力铜管没有传声。",
			"close" => _safe.Value("door", RedTokenSimulation.Closed) == 1 ? "试件门缝已合拢。可再轻敲核对门侧声路。" : "试件门缝尚未合拢。",
			"red" => _safe.Value("red", RedTokenSimulation.Received) == 1 ? "筹试片已单向落入匣内；筹接收件尚需分别核对。" : "筹试片尚未落入匣内。",
			"handle" => _safe.Value("handle", RedTokenSimulation.Received) == 1 ? "柄试片已单向落入匣内；柄接收件尚需分别核对。" : "柄试片尚未落入匣内。",
			"push_red" => _safe.WhiteAvailable ? "筹接收件已到位，双槽均已接收并推动，白片可领取。" : "筹接收件已到位；双槽尚未同时完成接收与推动，白片不可领取。",
			"push_handle" => _safe.WhiteAvailable ? "柄接收件已到位，双槽均已接收并推动，白片可领取。" : "柄接收件已到位；双槽尚未同时完成接收与推动，白片不可领取。",
			"white" => _safe.Value("white", RedTokenSimulation.Taken) == 1 ? "白片已领取；试件的双槽交接状态已记录。" : "白片尚未领取。",
			_ => result.Message
		} : result.Message;
		return result with { Message = message, Snapshots = _safe.Snapshots.Skip(before).ToArray() };
	}
	public RedTokenResult Cancel()
	{
		if (Physical.Holder is { } actor) Physical.Perform(actor, "release");
		if (_safe.Holder is { } safeActor) _safe.Perform(safeActor, "release");
		return new(true, "现场与安全试件的持续持柄均已取消，弹簧回路停流。");
	}

	public void ClearCandidate()
	{
		_candidate.Clear();
		Solved = false;
		Round = 1;
	}

	public void InvalidateCandidate() => Solved = false;

	public RedTokenResult EvaluateRound(int round, IReadOnlyDictionary<string, string> fields)
	{
		if (Ended || round < 1 || round > 3) return new(false, "请推演当前轮。");
		Solved = false;
		foreach (var pair in fields) _candidate[pair.Key] = pair.Value;
		using var simulation = new RedTokenSimulation();
		RedTokenHypothesis.Compile(simulation, _candidate);
		var predictions = RedTokenHypothesis.Predict(simulation, _candidate);
		var conflicts = new List<string>(); var unproven = new List<string>();
		foreach (var id in Known)
		{
			if (!predictions.TryGetValue(id, out var prediction) || prediction == "unknown") { unproven.Add(id + "：" + RedTokenHypothesis.UnprovenDetail(id)); continue; }
			if (prediction == "conflict") conflicts.Add(id + "：" + RedTokenHypothesis.ConflictDetail(id, simulation, _candidate));
			if (prediction == "auxiliary") unproven.Add(id + "：候选中的盆灰或擦布尚不能确定灰的来源与先后；仍允许其他来源。");
		}
		var hasActions = simulation.Events.Count > 0;
		bool accepted = hasActions && simulation.Failures.Count == 0 && conflicts.Count == 0;
		if (accepted && round < 3) Round = Math.Max(Round, round + 1);
		if (accepted && round == 3)
		{
			Solved = new[] { "E03", "E09.deep", "E09.completion", "E10", "E11", "E13", "E14", "E15" }.All(Knows) && simulation.Complete && predictions.GetValueOrDefault("E13") == "compatible" && predictions.GetValueOrDefault("E14") == "compatible" && predictions.GetValueOrDefault("E15") == "compatible";
			accepted = Solved;
		}
		var message = accepted ? "候选通过已知材料核对；未证明的部分继续保留。" : conflicts.Count > 0 ? "候选与已知材料冲突，请查看具体差异。" : simulation.Failures.Count > 0 ? "候选有无法提交的动作；请检查器具所有权、岗位和持续占用。" : "这份候选尚未形成可核对的交接因果链。";
		return new(accepted, message) { Predictions = predictions.Where(p => Knows(p.Key)).ToDictionary(p => p.Key, p => p.Value), Conflicts = conflicts.Concat(simulation.Failures).ToArray(), Unproven = unproven, Events = simulation.Events.ToArray(), Snapshots = simulation.Snapshots.ToArray() };
	}

	public RedTokenResult Defend(IReadOnlyDictionary<string, string> fields)
	{
		if (fields.GetValueOrDefault("decision") == "refuse") { Cancel(); Ended = true; Dispose(); return new(true, "你拒绝继续，调查结束。"); }
		if (!Solved) return new(false, "先完成交接因果推演，再答辩。");
		string Get(string key) => fields.GetValueOrDefault(key, "unknown");
		bool Material(string key, params string[] support)
		{
			var cited = Get(key).Split(new[] { ',', '+', ' ', '，' }, StringSplitOptions.RemoveEmptyEntries);
			return cited.Length > 0 && cited.All(Knows) && support.All(cited.Contains);
		}
		if (!Material("open_material", "E02", "E11") || Get("open_alternative") != "special_hold" || Get("open_unknown") != "inner_bolt_operator")
			return new(false, "开放状态不必违规：已接受的排灰特例允许持续持柄等待确认；内销操作者仍未知。请以已取得规程与本人签收支撑。");
		if (!Material("token_material", "E13", "E14") || Get("token_alternative") != "returned_then_taken")
			return new(false, "椅上红筹不能推出从未交回；须用同物接收印与白片签条，提供交回后开屉取回的可执行替代。");
		using (var alternative = new RedTokenSimulation())
		{
			var revision = new Dictionary<string, string>(_candidate) { ["returned"] = "yes", ["take"] = "after_return", ["return_before_room"] = "yes", ["identity"] = "unknown" };
			RedTokenHypothesis.Compile(alternative, revision);
			if (alternative.Failures.Count != 0 || alternative.Value("red", RedTokenSimulation.Received) != 1 || alternative.Value("chair", RedTokenSimulation.Taken) != 1)
				return new(false, "交回后取的替代尚未在Core实际执行成功。");
		}
		var identityBoundary = Get("token_unknown");
		if (identityBoundary == "identity_supported_E16" && !Knows("E16")) return new(false, "未现实取得移物单，不能具名后取者。");
		if (identityBoundary != "taker_without_E16" && identityBoundary != "identity_supported_E16") return new(false, "请保留身份的证据边界。");
		if (!Material("memory_material", "E14") || Get("memory_alternative") != "no_material_causal_link" || Get("memory_unknown") != "sealing_scope")
			return new(false, "签条只证明交接后返房，不能把操作造成失忆写成已证事实；封存范围仍未知。");
		Cancel(); Ended = true; Dispose(); return new(true, "三项前提均保留材料边界；替代解释与未知已记录，答辩完成。");
	}
	public void Dispose() { Physical.Dispose(); _safe.Dispose(); }
}
