using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed partial class Game
{
    public const double RoundLength = 5, NightLength = 24, TickLength = 1;
    public bool Ended { get; private set; }
    public bool Mediated;
    public int LedgerBaseline;
    public readonly List<(int Day, int Rations, int Oil, int Medicine, int Hungry)> Curve = [];
    public readonly List<string> NightTruth = [];
    private readonly Dictionary<ActionExecution, (Job? Job, string Why, double Score)> _running = [];

    public int Rounds(Slot s) => s switch { Slot.Morning => 3, Slot.Forenoon => 1, Slot.Night => 0, _ => 2 };

    /// <summary>推进一个时段。侦探的行动（可为 null = 歇着）在时段开头提交。</summary>
    public void RunSlot(PlayerAction? act)
    {
        if (Ended) return;
        Script.Add(act);
        if (Slot == Slot.Morning && !_dawnDone) BeginDay();
        _dawnDone = false;
        SetClock();
        if (Slot == Slot.Night) RunNight(act);
        else RunDay(act);
        Advance();
    }

    private void SetClock()
    {
        foreach (var (s, tag) in T.SlotTag) Set(Clock, tag, s == Slot ? 1 : 0);
        Set(Clock, T.Day, Day);
        Rt.Sync();
    }

    private void Advance()
    {
        if (Slot == Slot.Night)
        {
            Day++; Slot = Slot.Morning;
            // 天一亮就结算昨夜（痕迹表、饥饿、碗与饼屑），玩家在清晨行动之前就能读痕迹、做重演。
            BeginDay(); _dawnDone = true;
            if (Day >= 7) Ended = true;
        }
        else Slot++;
    }

    // ------------------------------------------------------------------ day --

    private void BeginDay()
    {
        _dayStart[Day] = Now;
        var hungry = 0;
        foreach (var p in People)
        {
            var fed = Get(p.Body, T.Fed) > 0 || Day == 2;
            p.HungerStreak = fed ? 0 : p.HungerStreak + 1;
            p.MaxHunger = Math.Max(p.MaxHunger, p.HungerStreak);
            if (!fed) hungry++;
            Set(p.Body, T.Hungry, fed ? 0 : 1);
            Set(p.Body, T.Weak, p.Weak ? 1 : 0);
            Set(p.Body, T.Fed, 0);
            if (Get(p.Body, T.NightAte) > 0) { Add(Litter[p.Pro.Dorm], T.Crumbs, 1); Set(p.Body, T.NightAte, 0); }
            foreach (var j in Enum.GetValues<Job>()) Set(p.Body, T.Did(j), 0);
            p.Asleep = false;
            p.Shamed = false;
            if (!fed && p == Det) Feed.Add("你昨天没吃上东西。今天少一个能做事的时段（下午）。");
        }
        // 病人清晨喝下碗里的粥（碗底留下粥渍）。
        var moran = this[P.Moran];
        if (Get(moran.Body, T.Sick) > 0 && Get(Bowl, T.Porridge) > 0)
        { Set(moran.Body, T.Fed, 1); Add(Bowl, T.Porridge, -1); Set(Bowl, T.Residue, 1); }
        foreach (var p in People) { Set(p.Body, T.Shamed, 0); }
        if (Day > 2) Curve.Add((Day - 1, Get(Granary, T.Ration), Get(OilStore, T.Oil), Get(MedCab, T.Medicine), hungry));
        Rt.Sync();
        foreach (var p in People)
            Observation.Observe(p.Soul, new ObservationInput(p.Self.Name, p.Self.Kind)
            { Target = p.Self, Source = ObservationSource.Introspection, Time = Now, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(T.Shamed, 0) });
        if (Day == 3) CaseActual = CaptureTraces();
        if (Day > 2) { DawnTraces[Day - 1] = TakeTraces(Day - 1); DawnHint(Day - 1); }
    }

    private void RunDay(PlayerAction? act)
    {
        _distributor = null; _cookedThisSlot = false;
        PerceiveAll();
        FixedBefore();
        PlayerBegin(act);
        for (var round = 0; round < Rounds(Slot); round++)
        {
            var start = Now;
            foreach (var p in Npcs) Decide(p);
            // 侦探在配给台等开粮：一轮没等到，下一轮接着等（与 NPC 的领粮一样）。
            if (round == 0 || act?.Cmd == Cmd.Collect && Det.Exec is null && Get(Det.Body, T.Fed) == 0) PlayerDo(act);
            // 等候中的人（例如在配给台等开粮）每隔一刻再看一眼；其余人只在自己或同处者的动作完成时感知。
            for (var t = TickLength; t < RoundLength; t += TickLength)
                S.CTB.Schedule(t, () => { foreach (var p in People.Where(p => p.Exec?.WaitingFor is not null)) Perceive(p); });
            S.CTB.RunUntil(start + RoundLength, 100_000);
            CancelAll();
            PerceiveAll();
            Gossip();
            Suspicions();
            RoundHooks(round);
            NoteCompany();
            if (Day == 4 && Slot == Slot.Morning && round == 0) Confront(act?.Cmd == Cmd.Confront ? act.Plan : null);
        }
        SlotPhysics();
        PlayerEnd(act);
        ExpireRequests();
    }

    /// <summary>当面的请求只对当下有效：时段结束仍没去做的，算是没照做（不会隔天突然想起来）。</summary>
    private void ExpireRequests()
    {
        foreach (var p in People)
            foreach (var req in p.World.Projections.Where(x => x.Kind == ProjectionKind.Event && x.Procedure is not null && x.Tags.GetValueOrDefault(Tags.Responded) == 0).ToArray())
                Observation.Observe(p.Soul, new ObservationInput(req.Name, req.Kind) { Target = req, Source = ObservationSource.Introspection, Time = Now, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Responded, 1) });
    }

    /// <summary>侦探之外的人此时段能不能自主行动。病人在诊疗所；饿过一天的人下午动不了。</summary>
    private bool Available(Person p)
    {
        if (Get(p.Body, T.Sick) > 0) return false;
        if (p.InjuredDays > 0) return false;
        if (Slot == Slot.Afternoon && Get(p.Body, T.Hungry) > 0) return false;
        if (Day == 2 && Slot == Slot.Noon && !Mediated && p.Id is P.Mora or P.Ruan) return false;
        return true;
    }

    public void Decide(Person p)
    {
        if (p.Exec is not null || p.Asleep || !Available(p)) return;
        Interpret(p);
        IReadOnlyList<DecisionAssessment> assessed;
        using (Prof.Time("Assess")) assessed = DecisionSystem.Assess(p.World, p.Self);
        var top = Pick(assessed);
        if (top is null) return;
        Submit(p, top.Decision, top.Reasons);
    }

    private static bool Inspects(Effect e, Projection target) => e switch
    {
        Effect.Act { Operation: ActionOp.Inspect, Target: Noun.Reference r } => ReferenceEquals(r.Projection, target),
        Effect.Sequence s => s.Items.Any(i => Inspects(i, target)),
        Effect.Wait w => Inspects(w.Body, target),
        Effect.When w => Inspects(w.Body, target),
        _ => false
    };

    /// <summary>DecisionSystem 的排序之上只加一条游戏层的裁定：分数打平时，先做别人当面请自己做的事。</summary>
    public static DecisionAssessment? Pick(IReadOnlyList<DecisionAssessment> assessed)
    {
        var top = assessed.FirstOrDefault(a => !a.Forbidden);
        if (top is null) return null;
        return assessed.Where(a => !a.Forbidden && a.Decision.Score == top.Decision.Score).FirstOrDefault(a => a.Decision.Basis.Kind == ProjectionKind.Event) ?? top;
    }

    public Job? JobOf(Person p, Effect effect)
    {
        foreach (var (job, fx) in p.JobFx)
            if (Ast.Equivalent(Ast.Bind(fx, new ActionContext(p.Self)), effect)) return job;
        if (effect is Effect.Sequence { Items: [Effect.Say, ..] }) return Job.AskLatchHelp;
        return null;
    }

    private Place? Destination(Effect effect) => effect switch
    {
        Effect.Act { Operation: ActionOp.Travel, Target: Noun.Reference r } => Way.FirstOrDefault(w => w.Value.TrueName == r.Projection.Name).Key,
        Effect.Sequence s => s.Items.Select(Destination).FirstOrDefault(d => d is not null),
        Effect.Wait w => Destination(w.Body),
        _ => null
    };

    private void Submit(Person p, Decision d, IReadOnlyList<string> reasons)
    {
        var job = JobOf(p, d.Effect);
        var dest = Destination(d.Effect);
        var delay = (dest is { } x && x != Where(p) ? 2.0 / p.Pro.Speed : 0.3) + 0.5;
        if (p.Weak) delay += 0.5;
        var why = string.Join("；", reasons);
        var basis = d.Basis.Kind == ProjectionKind.Event ? $"应{d.Basis.Relations.FirstOrDefault(r => r.Relation == Relations.Subject).Target?.Name}之请" : d.Basis.Name;
        p.Exec = S.Submit(p.Soul, new ActionIntent(p.Self, d.Effect, delay, Basis: d.Basis));
        p.Doing = job;
        p.DoingWhy = $"{basis}（{why}）";
        p.DoingScore = d.Score;
        _running[p.Exec] = (job, p.DoingWhy, d.Score);
        if (p.Id != P.Det) p.Diary.Add($"D{Day} {Names.Of(Slot)} {(job is { } j ? Names.Of(j) : "照请求行事")} ← {p.DoingWhy}");
    }

    public void SubmitDet(Effect effect, double delay = 0.6, Job? job = null)
    {
        if (Det.Exec is not null) S.Cancel(Det.Exec);
        Det.Exec = S.Submit(Det.Soul, new ActionIntent(Det.Self, effect, delay));
        Det.Doing = job;
        _running[Det.Exec] = (job, "玩家", 0);
    }

    private void CancelAll()
    {
        foreach (var p in People.Where(p => p.Exec is not null).ToArray())
        {
            var exec = p.Exec!;
            S.Cancel(exec);
            if (p.Doing is Job.Collect)
            {
                p.Diary.Add($"D{Day} {Names.Of(Slot)} 在配给台没等到开粮");
                // 开了粮却没轮到自己：对开粮的人不满（分配方案必然让某些人不满，§11 第 8 条）。
                if (_distributor is { } d && d != p && Get(Granary, T.Ration) == 0)
                    Witness(d, $"{d.Name}开的粮，没轮到我", -1, 0, [p]);
            }
            p.Exec = null;
            p.Doing = null;
        }
    }

    /// <summary>本时段照侦探的请求做完了事的人。</summary>
    public readonly HashSet<P> DetHelpers = [];
    private readonly HashSet<Place> _dirty = [];
    private readonly Dictionary<Person, Place> _startPlace = [];
    private bool _flushQueued;
    private void FlushPerception()
    {
        _flushQueued = false;
        var places = _dirty.ToArray();
        _dirty.Clear();
        foreach (var o in People.Where(o => places.Contains(Where(o)) && !(Slot == Slot.Night && o.Asleep))) Perceive(o);
    }

    private void OnCompleted(ActionExecution exec)
    {
        var p = People.First(x => ReferenceEquals(x.Self, exec.Intent.Actor));
        var (job, why, _) = _running.GetValueOrDefault(exec);
        _running.Remove(exec);
        p.Exec = null;
        p.Doing = null;
        if (exec.Status == ExecutionStatus.Completed)
        {
            if (Inspects(exec.Intent.Changes, p.M[Granary])) RevealLatch(p);
            if (job is { } j) Set(p.Body, T.Did(j), 1);
            if (exec.Intent.Basis is { Kind: ProjectionKind.Event } asked && ReferenceEquals(asked.Relations.FirstOrDefault(r => r.Relation == Relations.Subject).Target, p.M[Det.Body]))
                DetHelpers.Add(p.Id);
            Physics(p, job, exec);
        }
        else
        {
            Log.Add($"D{Day} {Names.Of(Slot)} {Now:0.0} {p.Name} 未完成：{(job is { } j2 ? Names.Of(j2) : "?")}（{exec.Result?.Failure}）");
            p.Diary.Add($"D{Day} {Names.Of(Slot)} 没做成：{(job is { } j3 ? Names.Of(j3) : "请求之事")}（{exec.Result?.Failure}）");
            if (job is Job.Sleep) p.Asleep = true;
            // 试过、没做成：今天不再试同一件事；请求算是答复过了（做不到）。
            if (job is { } jf) Set(p.Body, T.Did(jf), 1);
            if (exec.Intent.Basis is { Kind: ProjectionKind.Event } req && p.World.Contains(req))
                Observation.Observe(p.Soul, new ObservationInput(req.Name, req.Kind) { Target = req, Source = ObservationSource.Introspection, Time = Now, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Responded, 1) });
            if (job is Job.RepairLatch) Know(p, Granary, T.LatchLoose, 0, ObservationSource.Perception, "门闩已经修好了");
            // 夜里去粮仓没打开：人确实走到了仓门前，推了推，留下推痕和声音，再回去。
            if (Slot == Slot.Night && job is Job.NightTakeSick or Job.NightTakeSickRec or Job.NightTakeSelf or Job.NightTakeSelfRec or Job.Hoard)
            {
                p.Body.Get<SpatialProperty>()!.Space = Space[Place.Logistics];
                _failedAtGranary++;
                Sound(Place.Logistics, "有人推仓门，推不开");
            }
        }
        Rt.Sync();
        TrackMoves();
        RefreshKeys();
        // 同一刻完成的动作合并成一次感知：这一处的人各看一眼。
        _dirty.Add(Where(p));
        if (exec.Intent.Changes is Effect.Sequence { Items: [Effect.Act { Operation: ActionOp.Travel }, ..] }) _dirty.Add(_startPlace.GetValueOrDefault(p, Where(p)));
        if (!_flushQueued) { _flushQueued = true; S.CTB.Schedule(0, FlushPerception); }
        if (Slot == Slot.Night && !p.Asleep && p != Det)
            S.CTB.Schedule(0.05, () => { if (++p.NightSteps > 7) p.Asleep = true; else Decide(p); });
    }

    // -------------------------------------------------------------- physics --

    private void Physics(Person p, Job? job, ActionExecution exec)
    {
        var place = Where(p);
        if (Slot == Slot.Night) p.NightVisited.Add(place);
        NightNoise(p, job);
        switch (job)
        {
            case Job.Sleep when Slot == Slot.Night: p.Asleep = true; break;
            case Job.NightTakeSickRec or Job.NightTakeSelfRec:
                NightTruth.Add($"D{Day} {p.Name} {Names.Of(job.Value)}");
                NightLedgerNames.Add($"D{Day} {p.Name}");
                if (job == Job.NightTakeSickRec) _bowlFilledTonight = true;
                break;
            case Job.NightTakeSick or Job.NightTakeSelf or Job.Hoard or Job.RepairLatch:
                NightTruth.Add($"D{Day} {p.Name} {Names.Of(job.Value)}");
                if (job == Job.RepairLatch) Know(p, Granary, T.LatchLoose, 0, ObservationSource.Introspection, "我修好了门闩");
                if (job == Job.NightTakeSick) _bowlFilledTonight = true;
                break;
            case Job.Inform:
                _nightRecAtCount += 1; // 补记的是已清点过的旧账，不是新的取用
                if (Get(Ledger, T.Unrecorded) > 0) Add(Ledger, T.Unrecorded, -1);
                var text = $"{p.Name}在台账上补记：昨夜取用一份";
                NewsFrom(p, NewsKind.Ledger, text, about: p, trust: 1, on: Ledger, tag: T.Unrecorded, value: Get(Ledger, T.Unrecorded));
                Witness(p, text, 1, 0, People.Where(o => Where(o) == place), spread: false);
                break;
            case Job.CountLedger: Count(p); break;
            case Job.Distribute: _distributor = p; break;
            case Job.Cook: _cookedThisSlot = true; break;
            case Job.Accuse: AccuseSpeech(p); break;
            case Job.Defend: DefendSpeech(p); break;
            case Job.PostRules: WardPosts(p); break;
            case Job.AskLatchHelp: break;
            case Job.AnswerName:
                p.Hooks += 1;
                Observation.Observe(p.Soul, new ObservationInput("唤回的片段", ProjectionKind.Event)
                { Source = ObservationSource.Recall, Existence = Truth.True, Time = Now, Details = "有人在叫我的名字……我记起了一点点在校的事。" });
                RewriteDesire(p, "应了唤名，记起一点在校的事（钩 +1）");
                _answered.Add((Day, p.Id));
                // 上了钩，底线跟着变：先前认过的“夜里一律不开”，现在不认了。
                foreach (var spec in Specs.Where(x => x.Flags.HasFlag(RF.ForbidNightOpen) && p.Acc.GetValueOrDefault(x) == Truth.True).ToArray())
                {
                    p.Vetoed.Add(spec);
                    OverrideAcceptance(p, spec, Truth.False);
                    Log.Add($"D{Day} 夜 {p.Name}上了钩，不再认「{Short(spec)}」");
                }
                Log.Add($"D{Day} 夜 {p.Name} 应了唤名（钩 {p.Hooks}）");
                break;
        }
        if (job is { } jj && p != Det && Slot == Slot.Night) Log.Add($"D{Day} 夜 {Now:0.0} {p.Name}：{Names.Of(jj)}");
    }

    private int _handedAtCount, _nightRecAtCount;
    private Person? _distributor;
    private bool _cookedThisSlot;

    /// <summary>钥匙换了手：持钥人心中自己的钥匙标签跟着更新（钥匙是不可见标签，靠自知）。</summary>
    private void RefreshKeys()
    {
        foreach (var p in People)
        foreach (var t in new[] { T.GranaryKey, T.MedKey, T.ArchiveKey, T.OilKey })
            if ((Belief(p, p.Self, t) ?? 0) != Get(p.Body, t))
            {
                Observation.Observe(p.Soul, new ObservationInput(p.Self.Name, p.Self.Kind) { Target = p.Self, Source = ObservationSource.Introspection, Time = Now, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(t, Get(p.Body, t)) });
                if (t == T.GranaryKey && Get(p.Body, t) > 0) Log.Add($"D{Day} {Names.Of(Slot)} 粮仓钥匙到了{p.Name}手里");
            }
    }

    /// <summary>夜里的声音：只说听见了什么，不说是谁、在做什么。</summary>
    private void NightNoise(Person p, Job? job)
    {
        if (Slot != Slot.Night) return;
        switch (job)
        {
            case Job.NightTakeSick or Job.NightTakeSickRec or Job.NightTakeSelf or Job.NightTakeSelfRec or Job.Hoard:
                if (Get(p.Body, T.GranaryKey) > 0) Sound(Place.Logistics, "钥匙转动，仓门吱呀一声");
                else if (p.Pro.Lock >= 3) { Sound(Place.Logistics, "极轻的金属拨动声，仓门开了"); _pickedGranary++; }
                else Sound(Place.Logistics, "门闩被推开的声音");
                break;
            case Job.RepairLatch: Sound(Place.Logistics, "几下很轻的敲打声"); break;
            case Job.AnswerName: Sound(Place.ArchiveFront, "有人低低地应了一声"); break;
            case Job.Seize: Sound(Place.ArchiveFront, "铁链和配重的闷响"); _seizeTried.Add(Day); break;
            case Job.Patrol: Sound(Place.Hall, "提灯的脚步声走过"); break;
        }
    }
    /// <summary>账面应有：上次清点的存量，减去之后台账上记下的发放与夜间补记（宿主记录的煮粥节省、损耗、温室产出已计入 LedgerBaseline）。</summary>
    public int ExpectedRations() => LedgerBaseline - (Get(Ledger, T.Handed) - _handedAtCount) - (Get(Ledger, T.NightRecorded) - _nightRecAtCount);

    /// <summary>阮清越清点：日末存量与账面差额即“无记录取用”。</summary>
    private void Count(Person p)
    {
        var current = Get(Granary, T.Ration);
        var unrec = ExpectedRations() - current;
        if (unrec > 0)
        {
            Set(Ledger, T.Unrecorded, Get(Ledger, T.Unrecorded) + unrec);
            var text = $"台账：清点少了{unrec}份，无记录取用×{unrec}";
            NewsFrom(p, NewsKind.Ledger, text, on: Ledger, tag: T.Unrecorded, value: Get(Ledger, T.Unrecorded));
            Log.Add($"D{Day} {Names.Of(Slot)} {p.Name} 清点：{text}");
        }
        LedgerBaseline = current;
        _handedAtCount = Get(Ledger, T.Handed);
        _nightRecAtCount = Get(Ledger, T.NightRecorded);
        Set(Ledger, T.Counted, Get(Ledger, T.Counted) + 1);
        Rt.Sync();
    }

    /// <summary>接受“无登记取用视为私取”的人：账上有无记录取用，就把心里认定的私取之人列为嫌疑（推断）。夜里瞥见的瘦小身影加重这一推断。</summary>
    private void Suspicions()
    {
        foreach (var p in Npcs)
        {
            if (p.Acc.GetValueOrDefault(WardLedger) != Truth.True && WardLedger.Author != p.Id) continue;
            if ((Belief(p, p.M[Ledger], T.Unrecorded) ?? 0) < 1) continue;
            if (Specs.Any(s => s.Card is { Behavior: 0 } && s.Flags.HasFlag(RF.NightException) && s.Flags.HasFlag(RF.Records) && p.Acc.GetValueOrDefault(s) == Truth.True)) continue;
            var smallSeen = p.News.Values.Any(h => h.Text.Contains("瘦小的身影"));
            foreach (var q in People.Where(q => q != p && q != Det))
            {
                if ((Belief(p, q, T.Branded) ?? 0) < 1 || (Belief(p, q, T.Suspect) ?? 0) > 0) continue;
                Know(p, q.Body, T.Suspect, 1, ObservationSource.Inference, smallSeen ? "账上少了，夜里又有个瘦小的身影" : "账上少了");
                Log.Add($"D{Day} {Names.Of(Slot)} {p.Name} 推断 {q.Name} 私取{(smallSeen ? "（瘦小身影加重）" : "")}");
            }
        }
    }

    private void AccuseSpeech(Person w)
    {
        var suspects = People.Where(q => q != w && (Belief(w, q, T.Suspect) ?? 0) > 0).ToArray();
        if (suspects.Length == 0) return;
        var q = suspects[0];
        var n = Belief(w, w.M[Ledger], T.Unrecorded) ?? 0;
        var seen = w.News.Values.FirstOrDefault(h => h.Text.Contains("瘦小的身影"));
        var text = $"{w.Name}当众指控：依据台账，昨夜无记录取用{n}份{(seen is not null ? "；依据夜巡所见，有瘦小身影出入后勤院" : "")}；{q.Name}视为私取。";
        var item = NewsFrom(w, NewsKind.Accusation, text, about: q, trust: -2);
        foreach (var l in People.Where(l => l != w && Where(l) == Place.Hall && l != q))
        {
            Receive(w, l, item, w.News[item.Id], Channel.Public);
            if (TrustIn(l, w) >= 2) Know(l, q.Body, T.Branded, 1, ObservationSource.Testimony, text);
        }
        Know(w, q.Body, T.Accused, 1, ObservationSource.Introspection);
        if (Where(q) == Place.Hall) { Witness(w, $"{w.Name}当众指控我私取", -2, 0, [q], spread: false); q.News[item.Id] = new(w.Id, 1, ObservationSource.Perception, Day, text); }
        if (q.Id == P.Quer) AccusedQuer = true;
        Accusations.Add((Day, w.Id, q.Id, n));
        Log.Add($"D{Day} {Names.Of(Slot)} {text}");
        if (Where(Det) == Place.Hall) { Feed.Add(text); NoteFor(w.Id, "指控", text); }
    }

    public readonly List<(int Day, P By, P Whom, int Count)> Accusations = [];

    private void DefendSpeech(Person l)
    {
        var who = People.Where(q => q != l && (Belief(l, q, T.Accused) ?? 0) > 0 && TrustIn(l, q) >= 1).ToArray();
        if (who.Length == 0) return;
        var q = who[0];
        var text = $"{l.Name}替{q.Name}说话：“未经查验的账，不该落成一个人的罪名。”";
        var item = NewsFrom(l, NewsKind.Defense, text, about: q, trust: 1);
        foreach (var h in People.Where(h => h != l && Where(h) == Place.Hall)) Receive(l, h, item, l.News[item.Id], Channel.Public);
        Log.Add($"D{Day} {Names.Of(Slot)} {text}");
        if (Where(Det) == Place.Hall) { Feed.Add(text); NoteFor(l.Id, "说话", text); }
    }

    private void WardPosts(Person w)
    {
        if (Day >= 5 && WardCommittee is null)
        {
            WardCommittee = NewSpec("名录库由委员会保管：钥匙在委员会，取回须经委员会登记批准。", P.Ward, RF.CommitteeArchive | RF.WardAuthority | RF.Records, fx => []);
            Post(WardCommittee, Place.Hall);
            if (Where(Det) == Place.Hall) Feed.Add($"沃德在中央厅贴出：{WardCommittee.Text}");
        }
    }

    public RuleSpec? WardCommittee;

    // ------------------------------------------------------- fixed nodes --

    private void FixedBefore()
    {
        switch (Day, Slot)
        {
            case (2, Slot.Morning):
                Feed.Add("十六人在中央厅会合。文契组里，沃德的目光一直没离开雀儿；星仪组的伊莲娜站得离阮清越很远；后勤组的茉拉不和维罗妮卡说话。");
                LedgerBaseline = Get(Granary, T.Ration);
                break;
            case (2, Slot.Noon):
                if (!Mediated) Feed.Add("配给台前，茉拉握着粮仓钥匙，阮清越拿着人头账，两人争执不下。午间没有开粮。");
                break;
            case (2, Slot.Evening):
            {
                var quer = this[P.Quer];
                Set(quer.Body, T.Shamed, 1);
                Rt.Sync();
                Observation.Observe(quer.Soul, new ObservationInput(quer.Self.Name, quer.Self.Kind)
                { Target = quer.Self, Source = ObservationSource.Introspection, Time = Now, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(T.Shamed, 1) });
                Feed.Add("傍晚，沃德在众人面前盯着雀儿看了很久。雀儿转身走了。");
                break;
            }
            case (2, Slot.Afternoon):
                var moran = this[P.Moran];
                Set(moran.Body, T.Sick, 1);
                moran.Body.Get<SpatialProperty>()!.Space = Space[Place.Clinic];
                Rt.Sync();
                var selma = this[P.Selma];
                NewsFrom(selma, NewsKind.Fact, "莫兰发烧了，送去了诊疗所", about: moran, on: moran.Body, tag: T.Sick, value: 1);
                Know(selma, moran.Body, T.Sick, 1, ObservationSource.Perception);
                // 压力而非节点（§11 第 9 条）：作者保证茉拉知道莫兰在烧——她的夜取规则与沃德的“无记录即私取”必然相撞。
                if (Options.BlockNews is null || !Options.BlockNews(this[P.Mora], new NewsItem { Id = 0, Kind = NewsKind.Fact, Text = "", Origin = P.Selma, FactTag = T.Sick }))
                    Know(this[P.Mora], moran.Body, T.Sick, 1, ObservationSource.Testimony, "赛尔玛把莫兰送来诊疗所，叫了茉拉");
                Log.Add("D2 下午 莫兰发烧，被送去诊疗所（作者固定）");
                break;
            case (5, Slot.Morning):
                break;
        }
        if (Slot == Slot.Morning) { }
    }

    private void RoundHooks(int round)
    {
        if (Slot == Slot.Morning && round == 0) MorningBroadcast();
        if (Slot == Slot.Forenoon) TeachClass();
        if (Day == 2 && Slot == Slot.Evening && round == 0)
        {
            // 傍晚各组张贴自己的规则（第三部分 §6 第 2 日）。沃德当众盯视雀儿。
            Post(WardLedger, Place.Hall);
            Post(WardNight, Place.Hall);
            Post(MoraNight, Place.Logistics);
            Feed.Add("傍晚，各人在板上张贴自己的规则：沃德在中央厅贴了两条，茉拉在配给台贴了一条，沈鼎在生活翼、维罗妮卡在后勤寝区各贴一条。");
        }
        if (Slot == Slot.Evening && round == 0) AllianceEvening();
    }

    /// <summary>时段末的物理与账：烹煮效率、生领损耗、温室进度、病人、物资。</summary>
    private void SlotPhysics()
    {
        if (Slot is Slot.Noon or Slot.Evening)
        {
            var handed = Get(Ledger, T.Handed);
            var meal = handed - _handedBefore;
            _handedBefore = handed;
            if (meal > 0)
            {
                if (Get(Stove, T.Cooked) > 0)
                {
                    var refund = meal / 5;
                    Add(Granary, T.Ration, refund);
                    LedgerBaseline += refund;
                    if (refund > 0) Log.Add($"D{Day} {Names.Of(Slot)} 沈鼎煮粥，{meal} 人只耗 {meal - refund} 份");
                }
                else
                {
                    var loss = Math.Min(Get(Granary, T.Ration), (meal + 4) / 8);
                    Add(Granary, T.Ration, -loss);
                    LedgerBaseline -= loss;
                    if (loss > 0) Log.Add($"D{Day} {Names.Of(Slot)} 生领分发损耗 {loss} 份");
                }
            }
            Set(Stove, T.Cooked, 0);
            Set(Desk, T.Open, 0);
            Set(Granary, T.Locked, 1);
            if (Slot == Slot.Evening)
                foreach (var p in People.Where(p => Get(p.Body, T.Fed) == 0 && Get(p.Body, T.Stash) > 0))
                { Add(p.Body, T.Stash, -1); Set(p.Body, T.Fed, 1); }
        }
        if (Slot == Slot.Afternoon) GreenhousePhysics();
        if (Slot == Slot.Evening)
        {
            var moran = this[P.Moran];
            if (Get(moran.Body, T.Sick) > 0 && Get(Bed, T.Treated) >= 3) { Set(moran.Body, T.Sick, 0); Log.Add($"D{Day} 莫兰退烧"); }
            foreach (var p in People.Where(p => p.InjuredDays > 0)) p.InjuredDays--;
            if (GreenhouseRepaired && Day >= _repairedDay + 1) { Add(Granary, T.Ration, Options.GreenhouseYield); LedgerBaseline += Options.GreenhouseYield; }
        }
        Rt.Sync();
    }

    private int _handedBefore, _repairedDay;
    public bool GreenhouseRepaired;

    /// <summary>温室：阀门与压力表两站须同时有人；指针过红线时，没听过警告的人会开顶窗（受伤，恶魔须显形救人）。</summary>
    private void GreenhousePhysics()
    {
        var crew = People.Where(p => Where(p) == Place.Greenhouse && Get(p.Body, T.Did(Job.Greenhouse)) > 0).ToArray();
        if (crew.Length == 0 || GreenhouseRepaired) return;
        if (crew.Length < 2)
        {
            Log.Add($"D{Day} 下午 温室只有一人（{crew[0].Name}），两站没凑齐，白干");
            if (crew[0] == Det) Feed.Add("温室里只有你一个人。阀门和压力表要两个人同时守，白干了一下午。");
            return;
        }
        var skill = crew.Sum(p => Math.Max(1, p.Pro.GreenhouseSkill + (p.Pro.Structure >= 2 ? 1 : 0)));
        GreenhouseProgress += skill;
        var progress = GreenhouseProgress;
        Set(Valve, T.Work, progress);
        Set(Gauge, T.Pressure, progress >= 3 ? 2 : 1);
        if (!_warned2)
        {
            _warned2 = true;
            DemonSays("温室", Warning2.Text);
            foreach (var p in crew.Where(p => p != Det)) Tell(p, Warning2, Channel.Public);
            Det.Acc[Warning2] = Truth.True;
            if (crew.Contains(Det)) Feed.Add("恶魔的声音从压力表后面传来：“温室水压表指针过红线时，勿开顶窗。”就这一句。");
        }
        var reckless = crew.Where(p => p != Det && p.Acc.GetValueOrDefault(Warning2) != Truth.True && !WarnedPrecisely).FirstOrDefault();
        if (progress >= 3 && reckless is not null)
        {
            reckless.InjuredDays = 2;
            Set(Blackboard, T.Shoe, Get(Blackboard, T.Shoe));
            Log.Add($"D{Day} 下午 {reckless.Name} 在指针过红线时开了顶窗，玻璃碎裂；恶魔显形把人拽开（温室地上留下窄底鞋印）。{reckless.Name}受伤两日");
            NewsFrom(reckless, NewsKind.Fact, $"{reckless.Name}在温室受了伤，有人说看见一只很窄的手把他拽开");
            if (crew.Contains(Det)) Feed.Add($"{reckless.Name}伸手去开顶窗——玻璃炸开。一只很窄的手把人拽了回来。地上多了窄底鞋印。");
            GreenhouseProgress = Math.Max(0, progress - 2);
            Set(Valve, T.Work, GreenhouseProgress);
            return;
        }
        if (progress >= GreenhouseNeed)
        {
            GreenhouseRepaired = true;
            _repairedDay = Day;
            Log.Add($"D{Day} 下午 温室修好（{string.Join("、", crew.Select(p => p.Name))}）");
            var item = NewsFrom(crew[0], NewsKind.Fact, $"温室修好了（{string.Join("、", crew.Select(p => p.Name))}）");
            if (crew.Contains(Det)) Witness(Det, "侦探主持修好了温室", 1, 1, crew, domain: Dom.Rations);
            foreach (var p in crew.Skip(1)) p.News[item.Id] = new(p.Id, 0, ObservationSource.Perception, Day, item.Text);
        }
        Rt.Sync();
    }

    private bool _warned2;
    public int GreenhouseProgress;

    // ---------------------------------------------------------------- night --

    private void RunNight(PlayerAction? act)
    {
        Options.BeforeNight?.Invoke(this);
        OilPhysics();
        PlanSeizure();
        foreach (var p in People) { p.Asleep = false; p.NightSteps = 0; p.NightVisited.Clear(); }
        NightBegins();
        PerceiveAll();
        PlayerBegin(act);
        var start = Now;
        foreach (var p in Npcs) Decide(p);
        PlayerDo(act);
        for (var t = TickLength; t < NightLength; t += TickLength)
            S.CTB.Schedule(t, NightTick);
        S.CTB.RunUntil(start + NightLength, 200_000);
        CancelAll();
        Seizure();
        foreach (var p in People) p.Asleep = false;
        PlayerEnd(act);
        SettlePrediction();
        SettleWagers();
        Gossip();
    }

    private void NightTick()
    {
        TrackMoves();
        foreach (var p in People.Where(p => !p.Asleep)) p.NightVisited.Add(Where(p));
        if (Day >= 5) NameCall();
        PerceiveAll();
        // 守候中的人看到新情况，重新掂量：更想做的事出现了，就放下手里的等待。
        foreach (var p in Npcs.Where(p => !p.Asleep && p.Exec is not null && p.Doing is { } d && Interruptible.Contains(d)))
        {
            Interpret(p);
            var top = Pick(DecisionSystem.Assess(p.World, p.Self));
            if (top is null || top.Decision.Score <= p.DoingScore || JobOf(p, top.Decision.Effect) == p.Doing) continue;
            S.Cancel(p.Exec!);
            _running.Remove(p.Exec!);
            p.Exec = null;
            Submit(p, top.Decision, top.Reasons);
        }
    }

    /// <summary>第五日起，入夜后名录库前厅里有人以你之名唤你：一个请求（Say+Request），由 SocialSystem 判；警告 1 是 Forbid。</summary>
    private void NameCall()
    {
        foreach (var p in People.Where(p => !p.Asleep && Where(p) == Place.ArchiveFront && p != Det))
        {
            if (p.World.Projections.Any(x => x.Kind == ProjectionKind.Event && x.Name == "有人以你之名唤你")) continue;
            var voice = p.M[NameVoice];
            Observation.Observe(p.Soul, new ObservationInput(voice.Name, voice.Kind)
            { Target = voice, Source = ObservationSource.Perception, Time = Now, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, p.Hooks * 2 + p.Desire - 2) });
            Observation.Observe(p.Soul, new ObservationInput("有人以你之名唤你", ProjectionKind.Event)
            {
                Source = ObservationSource.Perception, Existence = Truth.True, Time = Now, Details = $"“{p.Name}。”",
                AddRelations = [(Relations.Subject, voice)], Procedure = FxOf(p).Job(Job.AnswerName)
            });
            Log.Add($"D{Day} 夜 {Now:0.0} 名录库前厅有声音唤 {p.Name}");
        }
    }

    /// <summary>油：阮清越按胜率给油（他信得过的寝区与夜巡）。没有油的地方夜里看不清人。</summary>
    private void OilPhysics()
    {
        var holder = People.FirstOrDefault(p => Get(p.Body, T.OilKey) > 0);
        var lamps = 0;
        if (holder is not null)
        {
            lamps = 1 + People.Count(p => p != holder && p.Pro.Dorm != holder.Pro.Dorm && TrustIn(holder, p) >= 2 && p.Id is P.Bertrand or P.Mora) ;
            lamps += Get(this[P.Moran].Body, T.Sick) > 0 ? 1 : 0;
        }
        lamps = Math.Min(lamps + 1, Get(OilStore, T.Oil));
        Add(OilStore, T.Oil, -lamps);
    }
}
