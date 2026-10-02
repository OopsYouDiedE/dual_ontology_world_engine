using Godot;
using GDictionary = Godot.Collections.Dictionary;
namespace DualOntologyWorldEngine.Game.FirstAwakening;

public partial class RedTokenBridge : Node
{
    private RedTokenSession _session = new();
    public void Reset() { _session.Dispose(); _session = new(); }
    public void ClearCandidate() => _session.ClearCandidate();
    public void InvalidateCandidate() => _session.InvalidateCandidate();
    public override void _ExitTree() => _session.Dispose();
    public GDictionary Inspect(string id) => Result(_session.Inspect(id));
    public Godot.Collections.Array<string> SearchOpeningRoom() => new(_session.SearchOpeningRoom());
    public Godot.Collections.Array<string> DiscoveredSites() => new(_session.DiscoveredSites);
    public GDictionary IntroduceCompanions() => Result(_session.IntroduceCompanions());
    public GDictionary ReceiveCopperIntroduction() => Result(_session.ReceiveCopperIntroduction());
    public GDictionary FollowCompanion(string actor) => Result(_session.FollowCompanion(actor));
    public GDictionary StopFollowing() => Result(_session.StopFollowing());
    public GDictionary IdentifyInvestigator(string name) => Result(_session.Physical.IdentifyInvestigator(name));
    public GDictionary PrepareSafeAction(string operation)
    {
        var plan = _session.PrepareSafeAction(operation);
        return new GDictionary { ["ok"] = plan.Ok, ["message"] = plan.Message,
            ["actor"] = plan.Actor, ["site"] = plan.Site, ["operation"] = plan.Operation };
    }
    public GDictionary CommitSafeAction() => Result(_session.CommitSafeAction());
    public GDictionary CancelPreparedAction() => Result(_session.CancelPreparedAction());
    public GDictionary SiteKnowledge(string id)
    {
        var knowledge = _session.SiteKnowledge(id);
        return new GDictionary { ["name"] = knowledge.Name, ["noticed"] = knowledge.Noticed,
            ["identified"] = knowledge.Identified, ["understood"] = knowledge.Understood };
    }
    public GDictionary SafeTest(string operation) => Result(_session.SafeTest(operation));
    public GDictionary Cancel() => Result(_session.Cancel());
    public GDictionary Perform(string actor, string action, string station) => Result(_session.Perform(actor, action, station));
    public GDictionary EvaluateRound(int round, GDictionary fields) => Result(_session.EvaluateRound(round, Read(fields)));
    public GDictionary Defend(GDictionary fields) => Result(_session.Defend(Read(fields)));
    public GDictionary State()
    {
        var p = _session.Physical;
        bool Knows(string key, DualOntologyWorldEngine.Core.Tag tag)
            => p.KnownObject("侦探", key)?.Tags.GetValueOrDefault(tag) == 1;
        var capturedAsh = Knows("upstream", RedTokenSimulation.Ash);
        var whiteAvailable = Knows("white", RedTokenSimulation.Identified) ||
            (Knows("receipt_red", RedTokenSimulation.Received) && Knows("receipt_red", RedTokenSimulation.Pushed) &&
             Knows("receipt_handle", RedTokenSimulation.Received) && Knows("receipt_handle", RedTokenSimulation.Pushed));
        return new GDictionary { ["can_leave_room"] = _session.CanLeaveRoom, ["companions_met"] = _session.CompanionsMet,
            ["copper_introduction_received"] = p.CopperIntroductionReceived, ["round"] = _session.Round, ["ended"] = _session.Ended, ["solved"] = _session.Solved,
            ["holder"] = ActorLabel(p.Holder ?? ""), ["safe_holder"] = ActorLabel(_session.SafeHolder ?? ""),
            ["known"] = new Godot.Collections.Array<string>(_session.Known), ["available"] = new Godot.Collections.Array<string>(_session.Available),
            ["waiting_for_confirmation"] = p.Waiting, ["flowing"] = p.Flowing, ["captured_ash"] = capturedAsh,
            ["clear_confirmed"] = Knows("completion_record", RedTokenSimulation.Confirmed), ["historical_gray"] = capturedAsh,
            ["red_received"] = Knows("receipt_red", RedTokenSimulation.Received), ["white_available"] = whiteAvailable,
            ["white_taken"] = Knows("white", RedTokenSimulation.Taken),
            ["work_complete"] = Knows("completion_record", RedTokenSimulation.Clear) && Knows("completion_record", RedTokenSimulation.Released) };
    }
    public GDictionary GetRoundOptions(int round)
    {
        var result = new GDictionary();
        void Add(string key, params string[] values) => result[key] = new Godot.Collections.Array<string>(values);
        if (round == 1) { Add("cloth_before_flow", "yes", "no", "unknown"); Add("flow", "ash", "water", "unknown"); Add("wipe", "before", "after", "unknown"); Add("wedge", "dirty_then_wrap", "clean_wrap", "unknown"); }
        if (round == 2) { Add("engineer_exception", "read", "unread", "unknown"); Add("holder", "engineer", "cook", "unknown"); Add("cook_filter", "yes", "no", "unknown"); Add("sound_gap", "quiet", "ringing", "unknown"); Add("sound_pipe", "quiet", "ringing", "unknown"); Add("release", "after_confirmation", "before", "unknown"); Add("completion_record", "mistaken", "actual", "unknown"); }
        if (round == 3) { Add("returned", "yes", "no", "unknown"); Add("take", "after_return", "never", "unknown"); Add("return_before_room", "yes", "no", "unknown"); Add("identity", "unknown", "receptionist", "student"); }
        return result;
    }
    public GDictionary GetDefenseOptions()
    {
        var result = new GDictionary();
        void Add(string key, params string[] values) => result[key] = new Godot.Collections.Array<string>(values);
        Add("decision", "defend", "refuse"); Add("open_alternative", "special_hold", "daily_reset", "unexplained"); Add("open_unknown", "inner_bolt_operator");
        Add("token_alternative", "returned_then_taken", "never_returned", "white_means_complete"); Add("token_unknown", "taker_without_E16", "identity_supported_E16");
        Add("memory_alternative", "no_material_causal_link", "operation_caused_loss"); Add("memory_unknown", "sealing_scope");
        return result;
    }
    private GDictionary Result(RedTokenResult result)
    {
        var comparisons = new Godot.Collections.Array<GDictionary>();
        foreach (var pair in result.Predictions)
        {
            var detail = result.Conflicts.Concat(result.Unproven).FirstOrDefault(message => message.StartsWith(pair.Key + "：", StringComparison.Ordinal));
            comparisons.Add(new GDictionary { ["source"] = pair.Key, ["status"] = pair.Value == "compatible" ? "compatible" : pair.Value == "conflict" ? "conflict" : "unproven", ["message"] = detail ?? (pair.Value == "compatible" ? "已提交痕迹与已知材料相容。" : "候选尚未证明这部分材料。") });
        }
        var frames = new Godot.Collections.Array<GDictionary>();
        foreach (var f in result.Snapshots)
        {
            var positions = new GDictionary(); foreach (var p in f.Positions) positions[ActorLabel(p.Key)] = p.Value;
            frames.Add(new GDictionary { ["caption"] = f.Caption, ["actor"] = ActorLabel(f.Actor), ["positions"] = positions,
                ["flowing"] = f.Flowing, ["gray"] = f.Gray, ["captured"] = f.Captured,
                ["token_location"] = f.TokenLocation, ["handle_location"] = f.HandleLocation, ["cloth_location"] = f.ClothLocation,
                ["closed"] = f.Closed, ["wiped"] = f.Wiped });
        }
        var preds = new GDictionary();
        foreach (var pair in result.Predictions) preds[pair.Key] = pair.Value;
        var safe = new GDictionary(); foreach (var pair in _session.SafeState) safe[pair.Key] = pair.Value is bool flag ? Variant.From(flag) : Variant.From(Convert.ToInt32(pair.Value));
        return new GDictionary { ["safe_state"] = safe, ["ok"] = result.Ok, ["message"] = result.Message, ["state"] = State(), ["comparisons"] = comparisons, ["predictions"] = preds,
            ["snapshots"] = frames, ["events"] = new Godot.Collections.Array<string>(result.Events),
            ["conflicts"] = new Godot.Collections.Array<string>(result.Conflicts), ["unproven"] = new Godot.Collections.Array<string>(result.Unproven) };
    }
    private static string ActorLabel(string actor) => actor switch { "工程师霍铎" => "工程师", "主厨沈鼎" => "厨师", "音乐家苏泠" => "音乐家", _ => actor };
    private static IReadOnlyDictionary<string, string> Read(GDictionary fields) => fields.Keys.ToDictionary(k => k.AsString(), k => fields[k].AsString());
}
