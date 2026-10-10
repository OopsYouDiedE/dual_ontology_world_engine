using DualOntologyWorldEngine.Core;
using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>
/// 人物动作涌现（§12 第 0 条）的记录与度量。
/// 每个有后果的动作记成一个事件，并记下它的“因”：别人对他提的请求、他从谁那里听来并认了的规矩、
/// 他的规则条件或动作所涉对象最近被谁动过（钥匙、门闩、台账、板、碗、废纸篓、某人的身体）。
/// 涌现链 = 沿“因”回溯的事件链：链长 ≥ 2、跨人物、至少两步是人物自己的决策（不是作者固定的节点）。
/// 记录不参与推演，只读推演的结果。
/// </summary>
public sealed partial class Game
{
    public sealed class ActEvent
    {
        public required int Id { get; init; }
        public required int Day { get; init; }
        public required Slot Slot { get; init; }
        public required P Who { get; init; }
        public required string Text { get; init; }
        public required string Frame { get; init; }
        public required Place Where { get; init; }
        public required int[] Causes { get; init; }
        public Job? Job { get; init; }
        public double Time { get; init; }
        public bool Fixed { get; init; }
        /// <summary>作者定的理由（总纲 §五.12“作者覆盖”）：只定过去与呈现时须写明，测试报告单列。</summary>
        public string Reason { get; init; } = "";
        public bool SeenByDet { get; set; }
        public required TypedEventV1<Essence?> Contract { get; init; }
        public Person? Recipient { get; init; }
        public AuthorTermId? ResponseTo { get; init; }
        public string Short => $"{Chapter2Design.DayName(Day)}{Names.Of(Slot)} {Names.Of(Who)}{Text}【{Frame}】";
    }

    public readonly List<ActEvent> Events = [];
    private readonly Dictionary<(Essence, Tag), (int Id, P Who, int SlotIdx)> _touch = [];
    private readonly Dictionary<(P From, P To), int> _asks = [];
    private readonly Dictionary<(int Spec, P Hearer), int> _transmit = [];
    private readonly Dictionary<int, int> _postedEv = [];
    private readonly Dictionary<Person, Dictionary<Projection, Essence>> _rev = [];
    private readonly List<(P Who, string Text)> _seenLines = [];
    private int _currentEvent;

    /// <summary>平常的动作：不成为涌现链的一环（也不单列给玩家看）。</summary>
    private static readonly HashSet<Job> Mundane = [Job.Sleep, Job.Rest, Job.Collect, Job.Socialize, Job.Class, Job.Work, Job.Wander, Job.Escort, Job.FetchWater];

    private Essence? EssenceOf(Person p, Projection proj)
    {
        if (!_rev.TryGetValue(p, out var map)) _rev[p] = map = p.M.ToDictionary(kv => kv.Value, kv => kv.Key);
        return map.GetValueOrDefault(proj);
    }

    private Person? PersonOf(Person viewer, Projection? proj) => proj is null ? null : EssenceOf(viewer, proj) is { } e ? People.FirstOrDefault(o => o.Body == e) : null;

    public int RecordEvent(P who, string text, string frame, Place where, IEnumerable<int> causes, bool fix = false, Job? job = null, string reason = "",
        Person? recipient = null, AuthorTermId? responseTo = null, bool propagation = false, TypedEventV1<Essence?>? actionReceipt = null)
    {
        var causeIds = causes.Where(c => c > 0 && c <= Events.Count).Distinct().ToArray();
        var fields = new EventFieldsV1<Essence?> { Time = Now, TimeUnit = ObservationTimeUnitV1.ActionValue,
            Subject = this[who].Body, Object = recipient?.Body, Location = Space[where], Source = this[who].Body,
            KnowledgeSource = ObservationSource.Perception, Phase = fix ? EventPhaseV1.Presented : EventPhaseV1.Completed,
            Outcome = actionReceipt?.Fields.Outcome ?? EventOutcomeV1.Succeeded,
            Topic = responseTo ?? (job is { } j ? new AuthorTermId("action/" + j) : null),
            Causes = [..causeIds.Select(c => Events[c - 1].Contract.Fields.Id).Concat(actionReceipt is null ? [] : new[] { actionReceipt.Fields.Id }).Distinct()],
            Perceivers = [this[who].Soul, ..People.Where(p => p.Id != who && Slot != Slot.Night && Where(p) == where).Select(p => (Essence?)p.Soul)] };
        TypedEventV1<Essence?> contract = fix ? new PresentationEventV1<Essence?>(fields)
            : propagation ? new PropagationEventV1<Essence?>(fields) : new ActionEventV1<Essence?>(fields);
        Rt.RecordEvent(contract);
        var e = new ActEvent
        {
            Id = Events.Count + 1, Day = Day, Slot = Slot, Who = who, Text = text, Frame = frame, Where = where,
            Causes = causeIds, Fixed = fix, Job = job, Time = Now, Reason = reason,
            Contract = contract, Recipient = recipient, ResponseTo = responseTo,
        };
        // 侦探看得见：白天同处一地；夜里守在那里（只看得见身影）。
        e.SeenByDet = who != P.Det && Where(Det) == where && (Slot != Slot.Night || LastAction?.Cmd == Cmd.Stakeout && LastAction.Place == where);
        Events.Add(e);
        foreach (var p in People.Where(p => p.Id == who || Slot != Slot.Night && Where(p) == where)) Rt.PublishEvent(p.Soul, contract);
        _currentEvent = e.Id;
        if (e.SeenByDet && Slot != Slot.Night && !fix && _seenLines.Count < 4 && !(recipient == Det && responseTo is not null)) _seenLines.Add((who, text));
        return e.Id;
    }

    private void Touch(Essence e, Tag t, int id, P who) => _touch[(e, t)] = (id, who, SlotIndex);

    /// <summary>白天在场者的动作以短句呈现（§12 第 0 条：在场即可见）。</summary>
    private void FlushSeen()
    {
        // 几个人做了同一件事，并成一句：“霍铎和雀儿都把自己的口粮分给了饿着的人”。
        var parts = _seenLines.GroupBy(l => l.Text).Select(g => g.Count() == 1 ? Names.Of(g.First().Who) + g.Key
            : X("看见.都", ("人们", JoinNames(g.Select(x => Names.Of(x.Who)).Distinct())), ("事", g.Key))).ToArray();
        if (parts.Length > 0) Feed.Add(X("看见.句", ("事", string.Join("；", parts))));
        _seenLines.Clear();
    }

    /// <summary>
    /// 转述与教人做完了（说话占过身体）：听的人当场判定认不认这条规矩 / 从此完整学过这一课。返回 true 表示已记成事件。
    /// 转述者对立规者有成见的，话会走样（听者那里多一分折扣，见 AcceptScore）。
    /// </summary>
    private bool HandleTalk(Person p, Job? job, ActionExecution exec)
    {
        if (job is not (Job.Relay or Job.Teach or Job.Avoid or Job.Follow) || p == Det) return false;
        var say = exec.Intent.Changes is Effect.Sequence seq ? seq.Items.OfType<Effect.Say>().FirstOrDefault() : null;
        var hearer = say?.Recipient is Noun.Reference r ? PersonOf(p, r.Projection) : null;
        var spec = say?.Topic is { } topic ? Specs.FirstOrDefault(s => SpeechTopics.Rule(s.Id) == topic) : null;
        if (job == Job.Relay && say is not null && hearer is not null && spec is not null)
        {
            if (hearer == Det) return true;
            var v = Tell(hearer, spec, Channel.Relay, p);
            if (Where(Det) == Where(p) && Slot != Slot.Night) DetHeard.Add(spec);
            RelayLog.Add((Day, Slot, p.Id, hearer.Id, spec.Id, v == Truth.True));
            var from = _transmit.TryGetValue((spec.Id, p.Id), out var tf) ? tf : _postedEv.GetValueOrDefault(spec.Id);
            var own = spec.Author == p.Id;
            var id = RecordEvent(p.Id, $"{(own ? "拉" + hearer.Name + "认自己的" : "向" + hearer.Name + "转述")}「{Short(spec)}」，{hearer.Name}{(v == Truth.True ? "认了" : "没认")}", "交谈", Where(p), [from], job: Job.Relay, recipient: hearer, responseTo: SpeechTopics.Rule(spec.Id), propagation: true, actionReceipt: exec.Events.LastOrDefault());
            _transmit[(spec.Id, hearer.Id)] = id;
            Log.Add($"D{Day} {Names.Of(Slot)} {p.Name}{(own ? "拉" : "向")}{hearer.Name}{(own ? "认" : "转述")}「{Short(spec)}」：{(v == Truth.True ? "认了" : "没认")}");
            if (spec.Author == P.Det)
            {
                if (Where(Det) == Where(p) && Slot != Slot.Night) DetSawRelay(p, [(hearer, v)], spec);
                else QueueDisclosure(hearer, spec, p, null, v == Truth.True);
            }
            return true;
        }
        if (job == Job.Teach && say is not null && hearer is not null && hearer != Det)
        {
            var l = Lessons.Keys.First(k => SpeechTopics.Lesson(k) == say.Topic);
            var had = hearer.Lessons.GetValueOrDefault(l);
            hearer.Lessons[l] = true;
            if (!had) Observation.Observe(hearer.Soul, new ObservationInput(hearer.Lore.Name, hearer.Lore.Kind)
            { Target = hearer.Lore, Source = ObservationSource.Testimony, Time = Now, Details = Lessons[l].Title + "：" + Lessons[l].Teach + $"（{p.Name}讲的）" });
            RecordEvent(p.Id, $"给{hearer.Name}讲《{Lessons[l].Title}》{(had ? "" : $"（{hearer.Pro.Pronoun}原先没学全）")}", "交谈", Where(p), [], job: Job.Teach, recipient: hearer, responseTo: SpeechTopics.Lesson(l), propagation: true, actionReceipt: exec.Events.LastOrDefault());
            return true;
        }
        if (job == Job.Avoid)
        {
            var enemy = p.Id == P.Quer ? P.Ward : P.Ruan;
            RecordEvent(p.Id, $"一见{Names.Of(enemy)}就走开了", "行走", _startPlace.GetValueOrDefault(p, Where(p)), [], job: Job.Avoid);
            return true;
        }
        if (job == Job.Follow)
        {
            var whom = _following.GetValueOrDefault(p);
            RecordEvent(p.Id, $"夜里跟着{(whom is null ? "一个人" : whom.Name)}走了一阵", "行走", Where(p), [], job: Job.Follow);
            _following.Remove(p);
            return true;
        }
        return false;
    }

    /// <summary>跟随：谁在跟着谁（宿主层负责把跟随者的身体带到被跟者所在处）。</summary>
    private readonly Dictionary<Person, Person> _following = [];

    /// <summary>一个人物动作完成：记成事件，找出它的因，并登记它动过的东西。</summary>
    private void RecordAction(Person p, Job? job, ActionExecution exec)
    {
        if (p == Det) return;
        var basis = exec.Intent.Basis;
        var changes = exec.Intent.Changes;
        var request = basis is { Kind: ProjectionKind.Event, Procedure: not null } ? basis : null;
        string text;
        var frame = job is { } jj ? Names.Frame(jj) : "交谈";
        Person? sayTo = null;
        if (changes is Effect.Sequence { Items: [Effect.Act { Operation: ActionOp.Travel }, Effect.Say say0, ..] } || changes is Effect.Sequence { Items: [Effect.Say, ..] })
        {
            var say = ((Effect.Sequence)changes).Items.OfType<Effect.Say>().First();
            sayTo = say.Recipient is Noun.Reference r ? PersonOf(p, r.Projection) : null;
            text = $"对{sayTo?.Name ?? "人"}说：“{say.Text}”";
            frame = "交谈";
            if (say.Topic == SpeechTopics.Invitation) job = Job.AskOpen;
        }
        else if (job is null && request is not null)
            text = $"答应了{PersonOf(p, request.Relations.FirstOrDefault(x => x.Relation == Relations.Subject).Target)?.Name ?? "人"}：“{request.Name}”";
        else if (job is { } j && !Mundane.Contains(j)) text = Names.Of(j);
        else return;

        var causes = new HashSet<int>();
        // 别人对他提的请求。
        if (request is not null && PersonOf(p, request.Relations.FirstOrDefault(x => x.Relation == Relations.Subject).Target) is { } asker && _asks.TryGetValue((asker.Id, p.Id), out var a))
            causes.Add(a);
        // 他认了的、别人写下的规矩（经谁转述、贴在哪里）。
        Rule? rule = null;
        if (basis is not null && basis != p.Habit && request is null)
        {
            foreach (var spec in Specs.Where(s => s.Author != p.Id))
            {
                var hit = spec.Build(FxOf(p)).FirstOrDefault(r => job is not null && SafeJobOf(p, r.Effect) == job || Ast.Equivalent(r.Effect, changes));
                if (hit is null) continue;
                rule = hit;
                if (_transmit.TryGetValue((spec.Id, p.Id), out var t)) causes.Add(t);
                else if (_postedEv.TryGetValue(spec.Id, out var pe)) causes.Add(pe);
            }
        }
        rule ??= basis?.Rules.FirstOrDefault(r => job is not null && SafeJobOf(p, r.Effect) == job);
        // 他做这件事所依的条件（规则条件、动作里的“须先”），其中某个对象的某个状态最近被别人改过。
        var conds = new List<(Noun, Tag)>();
        if (rule is not null) CondTags(rule.Condition, conds);
        CondTagsOf(changes, conds);
        foreach (var (noun, tag) in conds.Distinct())
        {
            var e = noun is Noun.Reference r ? EssenceOf(p, r.Projection) : noun is Noun.Slot { Role: RuleSlot.Actor } ? p.Body : null;
            if (e is not null && _touch.TryGetValue((e, tag), out var tc) && tc.Who != p.Id && SlotIndex - tc.SlotIdx <= 6) causes.Add(tc.Id);
        }
        // 几处由作者以外的人物“写”下、却不经规则条件表达的因果：撕规矩因为有人贴了它；再贴回去因为有人撕了它；赴约因为有人约过。
        if (job == Job.Tear && _postedEv.TryGetValue((p.Id == P.Quer ? WardNight : Specs.LastOrDefault(s => s.Flags.HasFlag(RF.Targets)) ?? WardNight).Id, out var torn)) causes.Add(torn);
        if (job == Job.PostRules && Events.LastOrDefault(e => e.Job == Job.Tear && e.Who != p.Id) is { } tearEv && Day - tearEv.Day <= 1) causes.Add(tearEv.Id);
        if (job == Job.Seize) foreach (var ask in _asks.Where(kv => kv.Key.To == p.Id && kv.Key.From != P.Det && Events[kv.Value - 1].Job == Job.AskOpen)) causes.Add(ask.Value);

        var response = request?.LastObservation?.Event?.Fields.Topic;
        var asker2 = request is not null ? PersonOf(p, request.Relations.FirstOrDefault(x => x.Relation == Relations.Subject).Target) : null;
        var id = RecordEvent(p.Id, text, frame, Where(p), causes, job: job, recipient: sayTo ?? asker2, responseTo: response, actionReceipt: exec.Events.LastOrDefault());
        if (sayTo is not null) _asks[(p.Id, sayTo.Id)] = id;
        // 有后果的动作会被当事人或旁人说出去（经闲谈到达侦探）。
        var said = job switch
        {
            Job.AskOpen when sayTo is not null => K("约开库", p.Id, sayTo.Id),
            null when asker2 is not null && asker2 != Det && response == SpeechTopics.Invitation => K("应约开库", p.Id, asker2.Id),
            Job.Share => K("分粮", p.Id),
            Job.GiveKey => K("交钥匙", p.Id),
            _ => (Claim?)null
        };
        if (said is not null) NewsFrom(p, NewsKind.Rumor, said, about: p, sourceEvent: Events[id - 1]);
        // 登记它动过的东西（开粮、煮粥、领粮这类日常不算）。
        if (job is not (Job.Distribute or Job.Cook or Job.Collect or Job.CountLedger or Job.InspectStructure or Job.Patrol or Job.GuardArchive or Job.VisitArchive or Job.Watch or null))
            foreach (var (target, tag) in Targets(changes))
                if ((target is Noun.Reference r2 ? EssenceOf(p, r2.Projection) : target is Noun.Slot { Role: RuleSlot.Actor } ? p.Body : null) is { } e2) Touch(e2, tag, id, p.Id);
    }

    private Job? SafeJobOf(Person p, Effect e)
    {
        if (Ast.Nouns(e).OfType<Noun.Slot>().Any(n => n.Role != RuleSlot.Actor)) return DidTag(e);
        try { return JobOf(p, e); } catch (InvalidOperationException) { return null; }
    }

    private static IEnumerable<(Noun, Tag)> Targets(Effect e) => e switch
    {
        Effect.Set s when !s.Tag.Name.StartsWith("Did") && s.Tag != T.Work && s.Tag != T.Locked => [(s.Target, s.Tag)], Effect.Change c when !c.Tag.Name.StartsWith("Did") && c.Tag != T.Work => [(c.Target, c.Tag)],
        Effect.Spend s when s.Tag != T.Ration => [(s.Target, s.Tag)],
        Effect.Sequence q => q.Items.SelectMany(Targets), Effect.Wait w => Targets(w.Body), Effect.When w => Targets(w.Body),
        _ => []
    };

    private static void CondTags(Condition c, List<(Noun, Tag)> into)
    {
        switch (c)
        {
            case Condition.Tagged t: into.Add((t.Target, t.Tag)); break;
            case Condition.Not n: CondTags(n.Operand, into); break;
            case Condition.All a: foreach (var x in a.Items) CondTags(x, into); break;
            case Condition.Any a: foreach (var x in a.Items) CondTags(x, into); break;
        }
    }

    private static void CondTagsOf(Effect e, List<(Noun, Tag)> into)
    {
        switch (e)
        {
            case Effect.Require r: CondTags(r.Condition, into); break;
            case Effect.When w: CondTags(w.Condition, into); CondTagsOf(w.Body, into); break;
            case Effect.Sequence s: foreach (var x in s.Items) CondTagsOf(x, into); break;
            case Effect.Wait w: if (w.Until is { } u) CondTags(u, into); CondTagsOf(w.Body, into); break;
        }
    }

    // ------------------------------------------------------------ 涌现链 --

    /// <summary>一条涌现链：事件序列（从因到果）。</summary>
    public sealed record Chain(IReadOnlyList<ActEvent> Steps)
    {
        public int People => Steps.Where(s => !s.Fixed).Select(s => s.Who).Distinct().Count();
        public int Decisions => Steps.Count(s => !s.Fixed);
        public bool ByDetective => Steps.Any(s => s.Who == P.Det);
        public string Signature => string.Join(">", Steps.Where(s => !s.Fixed).Select(s => $"{s.Who}:{s.Job?.ToString() ?? s.Contract.Fields.Topic?.Value ?? s.Contract.GetType().Name}"));
        public int Day => Steps[^1].Day;
    }

    /// <summary>
    /// 全部涌现链：对每个“没有再引出别的事”的事件，沿因回溯取最长的一条（决策步数最多）。
    /// 合格：链长 ≥ 2、至少两步是人物决策、涉及两个以上人物。
    /// </summary>
    public List<Chain> Chains()
    {
        var byId = Events.ToDictionary(e => e.Id);
        var best = new Dictionary<int, (int Len, int Prev)>();
        foreach (var e in Events.OrderBy(e => e.Id))
        {
            var (len, prev) = (0, 0);
            foreach (var c in e.Causes.Where(byId.ContainsKey))
                if (best[c].Len > len) (len, prev) = (best[c].Len, c);
            best[e.Id] = (len + (e.Fixed ? 0 : 1), prev);
        }
        var referenced = Events.SelectMany(e => e.Causes).ToHashSet();
        var chains = new List<Chain>();
        foreach (var sink in Events.Where(e => !referenced.Contains(e.Id)))
        {
            var steps = new List<ActEvent>();
            for (var id = sink.Id; id != 0; id = best[id].Prev) steps.Add(byId[id]);
            steps.Reverse();
            var chain = new Chain(steps);
            if (steps.Count >= 2 && chain.Decisions >= 2 && chain.People >= 2) chains.Add(chain);
        }
        return chains;
    }

    /// <summary>侦探能否读到这一步、怎么读到：在场、守夜、清晨痕迹、别人转述。读不到返回 null。</summary>
    public string? Readable(ActEvent e)
    {
        if (e.Who == P.Det) return "自己做的";
        if (MorningHeard.Contains(e.Id)) return "清晨闲谈";
        if (e.Slot != Slot.Night && Det.World.History.Any(v => v.Event?.Fields.Id == e.Contract.Fields.Id)) return "在场";
        if (e.Contract is PropagationEventV1<Essence?> && SpreadKnown.Any(k => k.Day == e.Day && k.Hearer != P.Det
            && e.Recipient == this[k.Hearer] && k.Teller == e.Who && e.Contract.Fields.Topic == SpeechTopics.Rule(k.Spec.Id))) return "转述";
        if (Det.News.Values.Any(h => ReferenceEquals(AllNews[h.Item - 1].SourceEvent, e))) return "转述";
        return null;
    }

    public bool Visible(Chain c) => c.Steps.Count(s => !s.Fixed && Readable(s) is not null) >= 2;

    public string Describe(Chain c, int max = 7)
    {
        var visible = c.Steps.Where(s => Readable(s) is not null).ToArray();
        return (visible.Length > max ? $"（前略 {visible.Length - max} 步）… " : "")
            + string.Join(" → ", visible.TakeLast(max).Select(s => s.Short + (s.Fixed ? "〔作者固定〕" : $"〔玩家：{Readable(s)}〕")));
    }
}

public static class Emergence
{
    /// <summary>“有意思”的排序：涉及的人多、决策步多、动作帧多样、跨了昼夜、玩家读得到的步多；同一签名只取一条。</summary>
    public static IEnumerable<Game.Chain> Interesting(IEnumerable<Game.Chain> chains, Game g) => chains
        .GroupBy(c => c.Signature).Select(x => x.First()).OrderByDescending(c => Score(c, g)).ThenBy(c => c.Steps[0].Id);

    /// <summary>同一条主干只留一条（去掉只在最后一步不同的变体）。</summary>
    public static string Trunk(Game.Chain c) => string.Join(">", c.Steps.Where(s => !s.Fixed).SkipLast(1).Take(4).Select(s => $"{s.Who}:{s.Job}"));

    /// <summary>涉及的人多、决策步多（封顶）、动作种类与动作帧多样、跨了昼夜、不只是转述、玩家读得到的步多。</summary>
    public static int Score(Game.Chain c, Game g)
    {
        var steps = c.Steps.Where(s => !s.Fixed).ToArray();
        var talk = steps.Count(s => s.Contract is PropagationEventV1<Essence?>);
        var deeds = steps.Count(s => s.Job is Job.Seize or Job.AskOpen or Job.Share or Job.Tear or Job.Watch or Job.Accuse or Job.Defend or Job.GiveKey or Job.Tidy
            or Job.NightTakeSick or Job.NightTakeSelf or Job.NightTakeSickRec or Job.NightTakeSelfRec or Job.RepairLatch || s.ResponseTo is not null);
        return c.People * 3 + Math.Min(c.Decisions, 6) * 2 + steps.Select(s => s.Job).Distinct().Count() * 3 + steps.Select(s => s.Frame).Distinct().Count() * 2 + deeds * 3
            + (steps.Any(s => s.Slot == Slot.Night) && steps.Any(s => s.Slot != Slot.Night) ? 3 : 0) + steps.Count(s => g.Readable(s) is not null) - talk * 3;
    }
}
