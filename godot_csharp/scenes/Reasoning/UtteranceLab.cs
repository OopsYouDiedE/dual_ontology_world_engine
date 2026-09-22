using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Game.Reasoning;

public sealed class LabReceiver(string name, ReceiverScope scope, int rank)
{
    public Essence Body { get; } = new(name);
    public ReceiverScope Scope { get; } = scope;
    public int Rank { get; } = rank;
    public string Name => Body.TrueName;
    public int Accepted { get; set; }
    public int Rejected { get; set; }
}

public sealed class LabSpeaker(string name, int rank)
{
    public SoulEssence Soul { get; } = new(name);
    public Essence Body { get; } = new(name, ProjectionKind.Body);
    public int Rank { get; } = rank;
    public Projection Self { get; set; } = null!;
    public Dictionary<LabReceiver, Projection> Receivers { get; } = [];
    public string Draft { get; set; } = "# 请用已掌握词汇书写咒文\n# 对象 o 动作 e 事物\n";
    public string Name => Soul.TrueName;
}

public sealed class UtteranceLab : IDisposable
{
    private static readonly Tag Gate = new("声音接纳域"), Rank = new("位格");
    private readonly WorldRuntime _world = new();
    private readonly List<(LabReceiver Receiver, bool Current, bool First, bool Accepted)> _results = [];
    private readonly Dictionary<ActionExecution, string> _pending = [];
    public LabReceiver[] Receivers { get; } = [new("厨房接收器", ReceiverScope.Kitchen, 1), new("交谈室接收器", ReceiverScope.Hall, 2)];
    public LabSpeaker[] Speakers { get; } = [new("言灵学员", 2), new("抄写员", 1)];
    public SimulationSession Session { get; }
    public int VocabularyStage { get; private set; }
    public bool TrialRunning { get; private set; }
    public bool Busy => TrialRunning || _pending.Count > 0;
    public bool Solved { get; private set; }
    public double Now => Session.CTB.Now;
    public List<string> Journal { get; } = [];
    public string ActiveMeaning { get; private set; } = "尚无咒文；两台接收器均接纳所有声音。";
    public string LastPulse { get; private set; } = "等待试声";
    public LabReceiver? LastReceiver { get; private set; }
    public bool LastAccepted { get; private set; }
    public event Action? Changed;

    public UtteranceLab()
    {
        Session = new(_world);
        var room = new Essence("低功率言灵试验台", ProjectionKind.Space);
        var space = _world.Project(room, room.TrueName);
        var devices = new Dictionary<LabReceiver, Projection>();
        foreach (var receiver in Receivers)
        {
            var state = receiver.Body.Add(new StateProperty()); state.Values[Gate] = 15; state.Observable.Add(Gate);
            receiver.Body.Add(new SpatialProperty { Space = room });
            devices[receiver] = _world.Project(receiver.Body, receiver.Name, space);
        }
        foreach (var speaker in Speakers)
        {
            var state = speaker.Body.Add(new StateProperty()); state.Values[Rank] = speaker.Rank; state.Observable.Add(Rank);
            speaker.Body.Add(new SpatialProperty { Space = room });
            speaker.Soul.Relations.Add((Relations.Controls, speaker.Body));
            _world.Project(speaker.Soul, speaker.Name + "的意识");
            var self = _world.Project(speaker.Body, speaker.Name, space);
            var seen = _world.Observe(speaker.Soul, [space, self, .. devices.Values]);
            speaker.Self = seen[1];
            for (var i = 0; i < Receivers.Length; i++) speaker.Receivers.Add(Receivers[i], seen[i + 2]);
            Learn(speaker, "导师已将两台接收器的名称与实物锚定。厨房器位格 1，交谈室器位格 2。", ObservationSource.Testimony);
        }
        Session.Completed += Completed;
        Log("课题：修复早餐通知。厨房应接纳本轮每个来源的首次声音，排除重复与旧声；交谈室维持原样。先练习仅改变厨房的接收结果。");
    }

    public void Log(string text)
    {
        Journal.Add($"{Now,4:0}  {text}");
        if (Journal.Count > 120) Journal.RemoveAt(0);
        Changed?.Invoke();
    }
    private void Learn(LabSpeaker speaker, string text, ObservationSource source)
        => Observation.Observe(speaker.Soul, new(text, ProjectionKind.Event) { Source = source, Time = Now, Existence = Truth.True });

    public string? Cast(LabSpeaker speaker, string text)
    {
        if (Busy) return "请先完成当前发声或试验。";
        var parsed = UtteranceLanguage.Parse(text, VocabularyStage);
        if (parsed.Errors.Count > 0) return string.Join("\n", parsed.Errors);
        if (parsed.Clauses.Count == 0) return "请书写至少一句咒文。";
        var targets = Receivers.Where(r => parsed.Clauses.Any(c => c.Scope == ReceiverScope.All || c.Scope == r.Scope)).ToArray();
        if (targets.Any(r => r.Rank > speaker.Rank)) return "位格不足：咒文覆盖了高于施术者位格的接收器，整篇未提交。";

        // 将不同句法的语义条件编译为本实验的有限真值域；不比较答案字符串。
        var effects = ImmutableArray.CreateBuilder<Effect>();
        foreach (var receiver in Receivers)
        {
            var mask = 0;
            for (var flags = 0; flags < 4; flags++)
            {
                var sample = Observation.Observe(speaker.Soul, new("咒文中的假设声音", ProjectionKind.Concept)
                {
                    Source = ObservationSource.Introspection, Time = Now,
                    Tags = ImmutableDictionary<Tag, int?>.Empty.Add(UtteranceLanguage.Current, flags >> 1).Add(UtteranceLanguage.First, flags & 1)
                });
                var accept = true;
                foreach (var clause in parsed.Clauses.Where(c => c.Scope == ReceiverScope.All || c.Scope == receiver.Scope))
                    if (Ast.Evaluate(clause.Sounds, new(speaker.Self, sample)) == Truth.True) accept = clause.Accept;
                if (accept) mask |= 1 << flags;
                Observation.Observe(speaker.Soul, new(sample.Name, sample.Kind) { Target = sample, Source = ObservationSource.Introspection, Time = Now, Forget = true });
            }
            // 每篇咒文替换该施术者有权操作的试验台约束；未提到的器物恢复本底。
            if (receiver.Rank <= speaker.Rank) effects.Add(new Effect.Set(speaker.Receivers[receiver], Gate, mask));
        }
        var needed = targets.Max(r => r.Rank);
        var action = Session.Submit(speaker.Soul, new(speaker.Self, new Effect.Sequence(effects.ToImmutable()), 2,
            new Condition.Tagged(speaker.Self, Rank, needed)));
        _pending.Add(action, UtteranceLanguage.Describe(parsed.Clauses));
        Log(speaker.Name + "开始发声；咒文将在 2 行动值后结算。");
        return null;
    }

    private void Completed(ActionExecution execution)
    {
        if (!_pending.Remove(execution, out var meaning)) return;
        if (execution.Status != ExecutionStatus.Completed) { Log("发声未能成立，现有约束保持原样。"); return; }
        ActiveMeaning = meaning; Solved = false;
        foreach (var speaker in Speakers) Learn(speaker, "本次发声已完成：" + meaning, ObservationSource.Perception);
        Log("咒文已成立。句法成立不保证符合任务目的，请试声检验。");
    }

    public string? StartTrial()
    {
        if (Busy) return "请等待当前发声或试验完成。";
        TrialRunning = true; Solved = false; _results.Clear();
        foreach (var receiver in Receivers) { receiver.Accepted = 0; receiver.Rejected = 0; }
        // 不用数字 ID 定位声音。同一来源的两次到达持有同一个对象引用。
        var bell = new object(); var knock = new object(); var old = new object();
        var heard = Receivers.ToDictionary(r => r, _ => new HashSet<object>());
        void Send(double delay, object source, bool current, string label)
            => Session.CTB.Schedule(delay, () =>
            {
                foreach (var receiver in Receivers)
                {
                    var first = heard[receiver].Add(source);
                    var flags = (current ? 2 : 0) | (first ? 1 : 0);
                    var accepted = (receiver.Body.Get<StateProperty>()!.Values[Gate] & (1 << flags)) != 0;
                    if (accepted) receiver.Accepted++; else receiver.Rejected++;
                    _results.Add((receiver, current, first, accepted));
                    LastReceiver = receiver; LastAccepted = accepted; LastPulse = label;
                    var observation = $"{receiver.Name}：{label} → {(accepted ? "接纳" : "排除")}";
                    foreach (var speaker in Speakers) Learn(speaker, observation, ObservationSource.Perception);
                    Log(observation);
                }
            });
        Send(2, bell, true, "本轮铃声 · 首次到达");
        Send(4, knock, true, "本轮敲击 · 另一个来源");
        Send(6, bell, true, "同一铃声 · 再次到达");
        Send(8, old, false, "上轮留声 · 此来源首次到达");
        Send(10, old, false, "上轮留声 · 再次到达");
        Session.CTB.Schedule(11, FinishTrial);
        Log("试声开始：两种本轮声音、重复声与上轮留声将依次经过两台接收器。");
        return null;
    }

    private void FinishTrial()
    {
        TrialRunning = false;
        var kitchen = _results.Where(r => r.Receiver.Scope == ReceiverScope.Kitchen).ToArray();
        var hallUnchanged = _results.Where(r => r.Receiver.Scope == ReceiverScope.Hall).All(r => r.Accepted);
        var preservesNew = kitchen.Where(r => r.Current && r.First).All(r => r.Accepted);
        var stopsRepeat = kitchen.Where(r => r.Current && !r.First).All(r => !r.Accepted);
        var stopsOld = kitchen.Where(r => !r.Current).All(r => !r.Accepted);
        if (VocabularyStage == 0 && hallUnchanged && kitchen.Any(r => !r.Accepted))
        {
            VocabularyStage = 1;
            Log("对象范围实验通过。记录显示两种来源各有第一次到达，解锁 nanpa、wan、ala。下一步保留两种新声并阻止重复。");
        }
        else if (VocabularyStage == 1 && hallUnchanged && preservesNew && stopsRepeat)
        {
            VocabularyStage = 2;
            Log("来源次序实验通过。上轮留声仍可能是首次到达，解锁时间描述与情境句。现在区分发声时间与到达次序。");
        }
        else if (VocabularyStage >= 2 && hallUnchanged && preservesNew && stopsRepeat && stopsOld)
        { Solved = true; Log("验证完成：厨房仅接纳本轮两个来源的首次声音；交谈室未受影响。旧声音重新抵达，不等于新的发声。"); }
        else
        {
            var problems = new List<string>();
            if (!hallUnchanged) problems.Add("交谈室也被改变");
            if (!preservesNew) problems.Add("本轮有效声音遭到排除");
            if (!stopsRepeat) problems.Add("重复声仍被接纳");
            if (!stopsOld) problems.Add("旧声仍被接纳");
            Log("试验结果：" + string.Join("；", problems) + "。请修改描述后再次发声。");
        }
        foreach (var speaker in Speakers) Learn(speaker, Journal[^1], ObservationSource.Inference);
        Changed?.Invoke();
    }

    public bool Step() { var result = Session.CTB.Step(); Changed?.Invoke(); return result; }
    public void Dispose() => Session.Dispose();
}
