using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed partial class Game
{
    public const double RoundLength = 5, NightLength = 24, TickLength = 1;
    public bool Ended { get; private set; }
    /// <summary>章节的最后一日（第 4 日清晨即议事，见设计第三部分 §15）。</summary>
    public static int LastDay => Chapter2Design.LastDay;
    public static int CaseDay => Chapter2Design.CaseDay;
    public static int FirstDay => Chapter2Design.FirstDay;
    /// <summary>《少了一份》的案发夜（这一日的夜里）。案发次日（CaseDay）清晨广播、白天搜查、晚饭后对质。</summary>
    public static int CaseNight => CaseDesign.Night;
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
        Story?.Poll();
        if (Story?.PendingChoice is not null) throw new PlayerError("请先回答当前剧情中的选择。");
        PresentationFrames.Clear();
        BeginPresentation();
        Script.Add(act);
        if (Slot == Slot.Morning && !_dawnDone) using (Prof.Time("P:BeginDay")) BeginDay();
        _dawnDone = false;
        SetClock();
        SettleFreeInteractions();
        // 上一阵留下、还没开始的打算（例如剧本占着人时漏过了阵末清理的领粮）到此作废：每一阵的决定都从这一阵的处境出发。
        //CANCELTEST CancelAll();
        NightSnapshotHook();
        if (Slot is Slot.Morning or Slot.Afternoon or Slot.Evening or Slot.Night) RefreshPlans();
        if (Slot is Slot.Noon or Slot.Evening) RefreshStanceContext();
        if (Slot == Slot.Night) RunNight(act);
        else RunDay(act);
        Advance();
        Story?.Poll();
    }

    /// <summary>
    /// 读不耗时（总纲 §四）：两阵之间侦探问话、细看时，宿主剧本让被问的人当场回应（一次简短的执行）。
    /// 这类回应在下一阵开始前就了结，不占住此人这一阵的决定；否则一句问话会让人整夜不再自己拿主意，重演也无从复现。
    /// </summary>
    private void SettleFreeInteractions()
    {
        // 先在当下结算（回应不占时间）；还有没完的，才让钟一刻一刻往前走。
        S.CTB.RunUntil(Now, 100_000);
        for (var guard = 0; guard < 8; guard++)
        {
            var pending = People.Where(p => p != Det && p.Exec is { } e && !_running.ContainsKey(e)).ToArray();
            if (pending.Length == 0) return;
            S.CTB.RunUntil(Now + TickLength, 100_000);
        }
        foreach (var p in People.Where(p => p != Det && p.Exec is { } e && !_running.ContainsKey(e))) { p.Exec = null; p.Doing = null; }
    }

    /// <summary>调试：把钟拨到当前时段并重建计划，不推演。</summary>
    public void ProbeStart() { SetClock(); RefreshPlans(); PerceiveAll(); }

    private void SetClock()
    {
        foreach (var (s, tag) in T.SlotTag) Set(Clock, tag, s == Slot ? 1 : 0);
        Set(Clock, T.Day, Day);
        Rt.Sync();
    }

    private void Advance()
    {
        AdvanceCalendar();
        if (Slot == Slot.Night)
        {
            Day++; Slot = Slot.Morning;
            // 天一亮就结算昨夜（痕迹表、饥饿、碗与饼屑），玩家在清晨行动之前就能读痕迹、做重演。
            BeginDay(); _dawnDone = true;
            if (Day >= LastDay) Ended = true;
        }
        else Slot++;
    }

    // ------------------------------------------------------------------ day --

    private readonly List<(Person Who, Projection Memory)> _failures = [];

    /// <summary>
    /// A failed authored action enters the actor's own cognition and forbids retrying it unchanged
    /// for the rest of the day; the next morning the memory lapses and the person may try again.
    /// </summary>
    private void RememberFailure(Person p, ActionExecution exec)
    {
        var memory = Observation.Observe(p.Soul, new ObservationInput($"今天试过，没做成（{exec.Result?.Failure}）", ProjectionKind.Concept)
        { Source = ObservationSource.Introspection, Existence = Truth.True, Time = Now, Details = "失败记忆" });
        Observation.Observe(p.Soul, new ObservationInput(memory.Name, memory.Kind)
        {
            Target = memory, Source = ObservationSource.Introspection, Time = Now,
            Rules = [new Rule(new Condition.Exists(memory), RuleMode.Forbid, exec.Intent.Changes, 1, Truth.True)]
        });
        _failures.Add((p, memory));
    }

    private void BeginDay()
    {
        if(!_institutionHooks.Add((Day,"dawn")))return;
        SetClock();
        _dayStart[Day] = Now;
        foreach (var (who, memory) in _failures)
            Observation.Observe(who.Soul, new ObservationInput(memory.Name, memory.Kind)
            { Target = memory, Source = ObservationSource.Introspection, Existence = Truth.False, Time = Now });
        _failures.Clear();
        foreach (var p in People)
        {
            foreach (var j in Enum.GetValues<Job>()) Set(p.Body, T.Did(j), 0);
            p.Asleep = false;
            p.Shamed = false;
        }
        WorldProcesses("dawn");
        var hungry=People.Count(p=>Get(p.Body,T.Hungry)>0);
        if(Get(Det.Body,T.Hungry)>0)Feed.Add(AstText("physical.hungry"));
        foreach (var p in People) { Set(p.Body, T.Shamed, 0); }
        if (Day > FirstDay) Curve.Add((Day - 1, Get(Granary, T.Ration), Get(OilStore, T.Oil), Get(MedCab, T.Medicine), hungry));
        Rt.Sync();
        foreach (var p in People)
            Observation.Observe(p.Soul, new ObservationInput(p.Self.Name, p.Self.Kind)
            { Target = p.Self, Source = ObservationSource.Introspection, Time = Now, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(T.Shamed, 0) });
        if (Day == CaseDay) CaseActual = CaptureTraces();
        if (Day > FirstDay) { DawnTraces[Day - 1] = TakeTraces(Day - 1); DawnHint(Day - 1); }
    }

    private void RunDay(PlayerAction? act)
    {
        _distributor = null; _cookedThisSlot = false;
        // 一日两顿：午间与晚饭各开一次配给台。“吃饱”指吃了上一顿：晚饭一开，午间那一顿就不算了，
        // 夜里饿着就是没吃上晚饭（第三部分 §7“没去领晚配，饿着”）。开粮、煮粥、领粮、替人领粮、分粮每顿各算一次。
        // 始业日门开时已是傍晚，只有一顿晚饭（它排在“中午”那一阵），不再重置。
        if (Slot == Slot.Evening && Day != FirstDay)
        {
            foreach (var p in People)
            {
                foreach (var j in new[] { Job.Distribute, Job.Cook, Job.FeedWeak, Job.Collect, Job.Share, Job.Exchange }) Set(p.Body, T.Did(j), 0);
                Set(p.Body, T.Fed, 0);
            }
            Rt.Sync();
            foreach (var p in People)
                Observation.Observe(p.Soul, new ObservationInput(p.Self.Name, p.Self.Kind)
                { Target = p.Self, Source = ObservationSource.Introspection, Time = Now, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(T.Fed, 0) });
        }
        FixedBefore();
        PerceiveAll();
        PlayerBegin(act);
        if (act?.Cmd == Cmd.EatPantry)
        {
            foreach (var p in People.Where(HostDormReleased)) { Set(p.Body, T.Fed, 1); Set(p.Body, T.Hungry, 0); }
            Rt.Sync(); PerceiveAll();
        }
        for (var round = 0; round < Rounds(Slot); round++)
        {
            var start = Now;
            using (Prof.Time("P:Decide")) foreach (var p in Npcs) Decide(p);
            // 侦探在配给台等开粮：一轮没等到，下一轮接着等（与 NPC 的领粮一样）。
            if (round == 0 || act?.Cmd == Cmd.Collect && Det.Exec is null && Get(Det.Body, T.Fed) == 0) PlayerDo(act);
            // 等候中的人（例如在配给台等开粮）每隔一刻再看一眼；其余人只在自己或同处者的动作完成时感知。
            for (var t = TickLength; t < RoundLength; t += TickLength)
                S.CTB.Schedule(t, () => { foreach (var p in People.Where(p => p.Exec?.WaitingFor is not null)) Perceive(p); });
            using (Prof.Time("P:CTB")) S.CTB.RunUntil(start + RoundLength, 100_000);
            CancelAll();
            using (Prof.Time("P:PerceiveAll")) PerceiveAll();
            Gossip();
            using (Prof.Time("P:Susp")) Suspicions();
            using (Prof.Time("P:Hooks")) RoundHooks(round);
            NoteCompany();
            if (Day == CaseDay && Slot == Slot.Evening && round == 0) Confront(act?.Cmd == Cmd.Confront ? act.Plan : null);
            else if (act?.Cmd == Cmd.Reopen && round == 0 && LastAction is not null) Reopen(act.Plan);
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
        if (p.M[Way[Where(p)]].Tags.GetValueOrDefault(Passable) < 1) return false;
        if (Get(p.Body, T.Sick) > 0) return false;
        if (p.InjuredDays > 0) return false;
        if (Slot == Slot.Afternoon && Get(p.Body, T.Hungry) > 0) return false;
        if (Day == RationFirstDay && Slot == Slot.Noon && !Mediated && p.Id is P.Mora or P.Ruan) return false;
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
        {
            if (Ast.Nouns(fx).OfType<Noun.Slot>().Any(n => n.Role != RuleSlot.Actor)) continue; // 带对象槽位的动作（分粮给谁）靠下面的标记认
            if (Ast.Equivalent(Ast.Bind(fx, new ActionContext(p.Self)), effect)) return job;
        }
        if (DidTag(effect) is { } did) return did;
        if (effect is Effect.Sequence { Items: [Effect.Say, ..] }) return Job.AskLatchHelp;
        return null;
    }

    private static readonly Dictionary<Tag, Job> _didToJob = Enum.GetValues<Job>().ToDictionary(T.Did, j => j);
    /// <summary>动作里自己记下的“做过某事”标记（Did 标签）就是它是哪件事。</summary>
    private static Job? DidTag(Effect e) => e switch
    {
        Effect.Change { Tag: var t } when _didToJob.TryGetValue(t, out var j) => j,
        Effect.Sequence s => s.Items.Select(DidTag).FirstOrDefault(x => x is not null),
        Effect.Wait w => DidTag(w.Body),
        _ => null
    };

    private Place? Destination(Person actor, Effect effect) => effect switch
    {
        Effect.Act { Operation: ActionOp.Travel, Target: Noun.Reference r } => Way.Where(w => ReferenceEquals(w.Value, EssenceOf(actor, r.Projection))).Select(w => (Place?)w.Key).FirstOrDefault(),
        Effect.Sequence s => s.Items.Select(e => Destination(actor, e)).FirstOrDefault(d => d is not null),
        Effect.Wait w => Destination(actor, w.Body),
        _ => null
    };

    private void Submit(Person p, Decision d, IReadOnlyList<string> reasons)
    {
        var job = JobOf(p, d.Effect);
        var dest = Destination(p, d.Effect);
        var delay = (dest is { } x && x != Where(p) ? 2.0 / p.Pro.Speed : 0.3) + 0.5;
        var social = _socialAttempts.LastOrDefault(a=>a.Accepted&&ReferenceEquals(a.Request,d.Basis));
        if(social is not null)delay=SocialCost(p,d.Effect,social.Definition);
        else if(p.Weak)delay+=0.5;
        var why = string.Join("；", reasons);
        var basis = d.Basis.Kind == ProjectionKind.Event ? $"应{d.Basis.Relations.FirstOrDefault(r => r.Relation == Relations.Subject).Target?.Name}之请" : d.Basis.Name;
        p.Exec = S.Submit(p.Soul, new ActionIntent(p.Self, d.Effect, delay, Requirements: SocialRequirements(p,d), Basis: d.Basis));
        TrackSocialExecution(p,p.Exec);
        p.Doing = job;
        p.DoingWhy = $"{basis}（{why}）";
        p.DoingScore = d.Score;
        _running[p.Exec] = (job, p.DoingWhy, d.Score);
        _startPlace[p] = Where(p);
        if (job == Job.Follow && d.Effect is Effect.Sequence { Items: [Effect.Act { Target: Noun.Reference fr }, ..] } && PersonOf(p, fr.Projection) is { } followed) _following[p] = followed;
        if (p.Id != P.Det) p.Diary.Add($"D{Day} {Names.Of(Slot)} {(job is { } j ? Names.Of(j) : dest is { } d2 && d.Basis.Name == "重演假设" ? "去" + Names.Of(d2) + "看一眼" : "照请求行事")} ← {p.DoingWhy}");
    }

    public void SubmitDet(Effect effect, double delay = 0.6, Job? job = null)
    {
        if (Det.Exec is not null) CancelExecution(Det.Exec);
        Det.Exec = S.Submit(Det.Soul, new ActionIntent(Det.Self, effect, delay));
        Det.Doing = job;
        _running[Det.Exec] = (job, "玩家", 0);
    }

    private void CancelAll()
    {
        foreach (var p in People.Where(p => p.Exec is not null && Story?.Controls(p.Id) != true).ToArray())
        {
            var exec = p.Exec!;
            if(_socialAttempts.LastOrDefault(a=>ReferenceEquals(a.Execution,exec)) is {} attempt)CancelSocialAction(attempt.Action);
            CancelExecution(exec);
            ReleaseStation(p);
            if (p.Doing is Job.Collect)
            {
                p.Diary.Add($"D{Day} {Names.Of(Slot)} 在配给台没等到开粮");
                // 开了粮却没轮到自己：对开粮的人不满（分配方案必然让某些人不满，§11 第 8 条）。
                if (_distributor is { } d && d != p && Get(Granary, T.Ration) == 0)
                    Witness(d, K("没轮到口粮", d.Id, p.Id), -1, 0, [p]);
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
        using var _ = Prof.Time("C:Flush");
        _flushQueued = false;
        var places = _dirty.ToArray();
        _dirty.Clear();
        foreach (var o in People.Where(o => places.Contains(Where(o)) && !(Slot == Slot.Night && o.Asleep))) Perceive(o);
    }

    // STORY-HARDCODE[CH2-062][logic][production]: 把指定案件行动完成映射为本章固定后果。
    private void OnCompleted(ActionExecution exec)
    {
        using var _ = Prof.Time("C:OnCompleted");
        UpdateWorkOccupancy();
        var p = People.FirstOrDefault(x => ReferenceEquals(x.Self, exec.Intent.Actor));
        if (p is not null && exec.Status == ExecutionStatus.Failed) ObserveFailedAccess(p, exec);
        if (Story?.Owns(exec) == true)
        {
            if (p is not null) { p.Exec = null; p.Doing = null; }
            PerceiveAll();
            return;
        }
        if (p is null) return;
        var (job, why, _) = _running.GetValueOrDefault(exec);
        _running.Remove(exec);
        CaptureAction(p, job, exec, why);
        p.Exec = null;
        p.Doing = null;
        if (exec.Status == ExecutionStatus.Completed)
        {
            if (Inspects(exec.Intent.Changes, p.M[Granary])) RevealLatch(p);
            if (Inspects(exec.Intent.Changes, p.M[Base]) && Get(Base, T.Crack) > 0 && p.Witnessed.Add("crack") && p != Det)
            {
                Log.Add($"D{Day} {Names.Of(Slot)} {p.Name}凑近看见了中央基座的新裂纹");
                NewsFrom(p, NewsKind.Fact, K("基座裂缝", p.Id));
            }
            if (job is { } j) Set(p.Body, T.Did(j), 1);
            if (exec.Intent.Basis is { Kind: ProjectionKind.Event } asked && ReferenceEquals(asked.Relations.FirstOrDefault(r => r.Relation == Relations.Subject).Target, p.M[Det.Body]))
                DetHelpers.Add(p.Id);
            _currentEvent = 0;
            if (!HandleTalk(p, job, exec)) RecordAction(p, job, exec);
            _physicsOf = p;
            Physics(p, job, exec);
            _physicsOf = null;
        }
        else
        {
            Log.Add($"D{Day} {Names.Of(Slot)} {Now:0.0} {p.Name} 未完成：{(job is { } j2 ? Names.Of(j2) : "?")}（{exec.Result?.Failure}）" + (job is Job.GuardArchive or Job.Seize ? $"〔配重室占着={Get(WeightRoom, T.Occupied)} 锁芯={Get(LockCore, T.Occupied)} 中继={Get(RelayPost, T.Occupied)} 在{Names.Of(Where(p))}〕" : ""));
            p.Diary.Add($"D{Day} {Names.Of(Slot)} 没做成：{(job is { } j3 ? Names.Of(j3) : "请求之事")}（{exec.Result?.Failure}）");
            // 失败记忆与完成标记分离：当前身体不在场、条件失效或资源不足，均不能登记已经做完。
            // 工位被占时仍可等岗位空出来后再判断。
            if (job is not (Job.Seize or Job.GuardArchive or Job.Relay or Job.Teach)) RememberFailure(p, exec);
            ReleaseStation(p);
            if (exec.Intent.Basis is { Kind: ProjectionKind.Event } req && p.World.Contains(req))
                Observation.Observe(p.Soul, new ObservationInput(req.Name, req.Kind) { Target = req, Source = ObservationSource.Introspection, Time = Now, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Responded, 1) });
            if (job is Job.RepairLatch) Know(p, Granary, T.LatchLoose, 0, ObservationSource.Perception, "门闩已经修好了");
        }
        // 现实投影的同步推迟到这一刻的感知（FlushPerception / Perceive）之前：同一刻多个动作完成只同步一次。
        using (Prof.Time("C:Track")) { TrackMoves(); RefreshKeys(); }
        // 同一刻完成的动作合并成一次感知：这一处的人各看一眼。
        _dirty.Add(Where(p));
        if (exec.Intent.Changes is Effect.Sequence { Items: [Effect.Act { Operation: ActionOp.Travel }, ..] }) _dirty.Add(_startPlace.GetValueOrDefault(p, Where(p)));
        if (!_flushQueued) { _flushQueued = true; S.CTB.Schedule(0, FlushPerception); }
        if (Slot == Slot.Night && !p.Asleep && p != Det)
            S.CTB.Schedule(0.05, () => { if (++p.NightSteps > 7) p.Asleep = true; else Decide(p); });
    }

    // -------------------------------------------------------------- physics --

    // ---- 立规者自己去贴规矩：打算写进他的认知，贴出是他完成的行动 ----
    private readonly List<(Person Author, RuleSpec Spec, Place Place, Projection Intent, Effect Act)> _postIntents = [];

    private void IntendPost(Person author, RuleSpec spec, Place place)
    {
        if (spec.PostedAt.Contains(place) || _postIntents.Any(i => i.Spec == spec && i.Place == place)) return;
        var fx = new Fx(this, e => author.M[e]);
        var act = Fx.Seq(fx.Go(place), fx.Change(Board[place], T.Posted, 1));
        var intent = Observation.Observe(author.Soul, new ObservationInput($"要在{Names.Of(place)}贴出「{Short(spec)}」", ProjectionKind.Concept)
        { Source = ObservationSource.Introspection, Existence = Truth.True, Time = Now });
        Observation.Observe(author.Soul, new ObservationInput(intent.Name, intent.Kind)
        { Target = intent, Source = ObservationSource.Introspection, Time = Now,
          Rules = [new Rule(new Condition.Exists(intent), RuleMode.Prefer, act, 6, Truth.True)] });
        _postIntents.Add((author, spec, place, intent, act));
        Log.Add($"D{Day} {Names.Of(Slot)} {author.Name}打算在{Names.Of(place)}贴出：{spec}");
    }

    private void CompletePostIntents(Person p, ActionExecution exec)
    {
        foreach (var due in _postIntents.Where(i => i.Author == p && Ast.Equivalent(exec.Intent.Changes, i.Act)).ToArray())
        {
            _postIntents.Remove(due);
            Observation.Observe(p.Soul, new ObservationInput(due.Intent.Name, due.Intent.Kind)
            { Target = due.Intent, Source = ObservationSource.Introspection, Existence = Truth.False, Time = Now });
            Post(due.Spec, due.Place);
            if (Where(Det) == due.Place) Feed.Add(X("规矩.聚落张贴", ("人", p.Name), ("地", Names.Of(due.Place)), ("规矩", due.Spec.Text)));
        }
    }

    // STORY-HARDCODE[CH2-063][logic][production]: 结算本章人物、作案物件与调查线索的专属物理结果。
    private void Physics(Person p, Job? job, ActionExecution exec)
    {
        CompletePostIntents(p, exec);
        var place = Where(p);
        if (Slot == Slot.Night) p.NightVisited.Add(place);
        NightNoise(p, job);
        switch (job)
        {
            case Job.Sleep when Slot == Slot.Night: p.Asleep = true; break;
            case Job.NightTakeSickRec or Job.NightTakeSelfRec:
                NightSeenTaking(p, true);
                NightTruth.Add($"D{Day} {p.Name} {Names.Of(job.Value)}");
                NightLedgerNames.Add($"D{Day} {p.Name}");
                NightLedgerRecords.Add((Day, p));
                if (job == Job.NightTakeSickRec) _bowlFilledTonight = true;
                break;
            case Job.NightTakeSick or Job.NightTakeSelf or Job.Hoard or Job.RepairLatch:
                if (job != Job.RepairLatch) NightSeenTaking(p, false);
                NightTruth.Add($"D{Day} {p.Name} {Names.Of(job.Value)}");
                if (job == Job.RepairLatch) Know(p, Granary, T.LatchLoose, 0, ObservationSource.Introspection, "我修好了门闩");
                if (job == Job.NightTakeSick) _bowlFilledTonight = true;
                break;
            case Job.Inform:
                _nightRecAtCount += 1; // 补记的是已清点过的旧账，不是新的取用
                if (Get(Ledger, T.Unrecorded) > 0) Add(Ledger, T.Unrecorded, -1);
                var note = K("补记", p.Id, night: Day - 1);
                NewsFrom(p, NewsKind.Ledger, note, about: p, trust: 1, on: Ledger, tag: T.Unrecorded, value: Get(Ledger, T.Unrecorded));
                Witness(p, note, 1, 0, People.Where(o => Where(o) == place), spread: false);
                break;
            case Job.CountLedger: Count(p); break;
            case Job.Tear:
            {
                // 撕掉点人名的规矩：雀儿撕沃德那条点她名的；卢西安撕任何点名的。板上少了一张纸，在场的人都看见。
                var torn = Specs.Where(s => s.PostedAt.Contains(Place.Hall) && (p.Id == P.Quer ? s == WardNight : p.Id == P.Lucian ? s.Flags.HasFlag(RF.Targets) : s.Author == P.Det && p.Vetoed.Contains(s))).ToArray();
                foreach (var s in torn) Unpost(s, Place.Hall);
                if (torn.Length > 0)
                {
                    var tore = K("撕规", p.Id, values: ("规矩", Short(torn[0])));
                    NewsFrom(p, NewsKind.Fact, tore, about: p);
                    Witness(p, tore, 0, 0, People.Where(o => o != p && Where(o) == Place.Hall), spread: true);
                    Log.Add($"D{Day} {Names.Of(Slot)} {Plain(tore)}");
                }
                break;
            }
            case Job.Treat: NewsFrom(p, NewsKind.Rumor, K("换药", p.Id), about: p); break;
            case Job.Retreat: NewsFrom(p, NewsKind.Rumor, K("独坐", p.Id), about: p); break;
            case Job.Report:
            {
                var mine = Sightings.Where(s => s.Day == Day - 1 && s.Witness == P.Bertrand).ToArray();
                // 按地点归拢：“后勤院有两个高大的、一个瘦小的身影”。
                var seen = mine.GroupBy(s => s.At).Select(at => X("知识片段.身影", ("地", Names.Of(at.Key)),
                    ("各色", string.Join("、", at.GroupBy(s => s.Build).OrderBy(b => b.Key).Select(b => X("知识片段.身影一种", ("数", Cn(b.Count())), ("身形", BuildText(b.Key)))))))).ToArray();
                var report = seen.Length == 0 ? K("夜巡无事", p.Id, night: Day - 1)
                    : K("夜巡报告", p.Id, night: Day - 1, values: [("所见", string.Join("；", seen)), ("builds", string.Join(",", mine.Select(s => s.Build).Distinct()))]);
                var item = NewsFrom(p, NewsKind.Sighting, report);
                foreach (var o in People.Where(o => o != p && Where(o) == place)) o.News[item.Id] = new(p.Id, 1, ObservationSource.Testimony, Day, item.Id);
                if (Where(Det) == place) Feed.Add(Stop(Present(report)));
                break;
            }
            case Job.Sample: NewsFrom(p, NewsKind.Rumor, K(p.Id == P.Selma ? "温室采样" : "刮炉灰", p.Id), about: p); break;
            case Job.Needle:
                Witness(p, K("追问误差", p.Id, P.Mora), -1, 0, [this[P.Mora]], spread: false);
                NewsFrom(p, NewsKind.Rumor, K("追问误差", p.Id, P.Mora), about: p);
                break;
            case Job.Sketch:
            {
                var seen = Sightings.Where(s => s.Day == Day - 1 && s.Witness == P.Moran).ToArray();
                var drawing = seen.Length == 0 ? $"第{Day - 1}夜：中央厅空荡荡的，只画了月光" :
                    $"第{Day - 1}夜：" + string.Join("；", seen.Select(s => $"{Names.Of(s.At)}，一个{BuildText(s.Build).TrimEnd('的')}的身影，颜色涂的是{Names.Of(s.Truth)}的颜色"));
                Sketches.Add((Day - 1, drawing));
                NewsFrom(p, NewsKind.Rumor, K("速写", p.Id, night: Day - 1), about: p);
                break;
            }
            case Job.Share:
                Log.Add($"D{Day} {Names.Of(Slot)} {p.Name}把自己的口粮分了出去");
                break;
            case Job.Tidy:
                _tidied.Add((Day, p.Pro.Dorm));
                Sound(p.Pro.Dorm, "有人窸窸窣窣地倒废纸篓");
                NightTruth.Add($"D{Day} {p.Name} {Names.Of(job.Value)}");
                break;
            case Job.Distribute: _distributor = p; break;
            case Job.Cook: _cookedThisSlot = true; break;
            case Job.Accuse: AccuseSpeech(p); break;
            case Job.Watch: break;
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

    private Person? _physicsOf;
    private readonly HashSet<Person> _seizeOk = [];

    /// <summary>动作被打断或没做成：他占着的那一站随之空出来，守门的样子也没了（身体离开了岗位）。</summary>
    private void ReleaseStation(Person p)
    {
        if (Get(p.Body, T.Prying) > 0) { Set(p.Body, T.Prying, 0); Set(StationFor(p), T.Occupied, 0); }
        if (Get(p.Body, T.Guarding) == 0) return;
        Set(p.Body, T.Guarding, 0);
        Set(WeightRoom, T.Occupied, 0);
    }
    private int _wardReposts;
    /// <summary>莫兰的速写本：哪一夜、画了什么（认得出是谁）。</summary>
    public readonly List<(int Night, string Text)> Sketches = [];
    private readonly HashSet<(P, P)> _sawTaking = [];

    /// <summary>
    /// 夜里在粮仓边的人，近处看得清是谁。没记账的取粮怎样说出口，看说话人自己认了哪条规矩（多日交互审核 M3）：
    /// 认了“夜间急需可自取”的，说是照规矩取的，不起疑；认了“无记录即私取”的，说是私取，记为嫌疑；两条都没认的，只说取了一份，也起疑。
    /// 起疑的是亲眼所见，可以凭此当众指控。
    /// </summary>
    // STORY-HARDCODE[CH2-064][logic][production]: 判定本案夜间取物是否被特定证据和目击捕捉。
    private void NightSeenTaking(Person taker, bool recorded)
    {
        var take = _currentEvent;
        foreach (var w in People.Where(w => w != taker && w != Det && !w.Asleep && Where(w) == Place.Logistics))
        {
            var permits = MoraNight.Author == w.Id || w.Acc.GetValueOrDefault(MoraNight) == Truth.True
                || Specs.Any(s => s.Card is { Behavior: 0 } && s.Flags.HasFlag(RF.NightException) && w.Acc.GetValueOrDefault(s) == Truth.True);
            var condemns = WardLedger.Author == w.Id || w.Acc.GetValueOrDefault(WardLedger) == Truth.True;
            // 同一件事，说出口的样子随看见的人认了哪条规矩而不同（他心里的这件事本来就不同）。
            var seenAs = K(recorded ? "夜见取粮记账" : condemns && !permits ? "夜见私取" : permits ? "夜见照规取粮" : "夜见取粮", w.Id, taker.Id, Place.Logistics, Day);
            var text = Plain(seenAs);
            var id = RecordEvent(w.Id, Say(seenAs with { Subject = null, Night = 0 }).Trim(), "看守", Place.Logistics, [take], job: Job.Watch);
            NewsFrom(w, NewsKind.Sighting, seenAs, about: taker, trust: recorded || permits && !condemns ? 0 : -1);
            Log.Add($"D{Day} 夜 {text}");
            if (recorded || permits && !condemns) continue;
            Know(w, taker.Body, T.Suspect, 1, ObservationSource.Perception, text);
            _sawTaking.Add((w.Id, taker.Id));
            Know(w, w.Body, T.SawAct, 1, ObservationSource.Introspection, text);
            Touch(taker.Body, T.Suspect, id, w.Id);
        }
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
            case Job.Seize: Sound(Place.ArchiveFront, "铁链和配重的闷响"); _seizeTried.Add(Day); _seizeOk.Add(p); break;
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
            var short_ = K("清点缺口", p.Id, values: ("数", Cn(unrec)));
            var text = Plain(short_);
            if (_currentEvent > 0) Touch(Ledger, T.Unrecorded, _currentEvent, p.Id); // 清点出了缺口：这本账从此“被人动过”
            NewsFrom(p, NewsKind.Ledger, short_, on: Ledger, tag: T.Unrecorded, value: Get(Ledger, T.Unrecorded));
            Log.Add($"D{Day} {Names.Of(Slot)} {p.Name} 清点：{text}");
        }
        LedgerBaseline = current;
        _handedAtCount = Get(Ledger, T.Handed);
        _nightRecAtCount = Get(Ledger, T.NightRecorded);
        Set(Ledger, T.Counted, Get(Ledger, T.Counted) + 1);
        Rt.Sync();
    }

    /// <summary>接受“无登记取用视为私取”的人：账上有无记录取用，就把心里认定的私取之人列为嫌疑（推断）。夜里瞥见的瘦小身影加重这一推断。</summary>
    // STORY-HARDCODE[CH2-065][logic][production]: 按本案特定行为积累或消除嫌疑。
    private void Suspicions()
    {
        foreach (var p in Npcs)
        {
            if (p.Acc.GetValueOrDefault(WardLedger) != Truth.True && WardLedger.Author != p.Id) continue;
            if ((Belief(p, p.M[Ledger], T.Unrecorded) ?? 0) < 1) continue;
            if (Specs.Any(s => s.Card is { Behavior: 0 } && s.Flags.HasFlag(RF.NightException) && s.Flags.HasFlag(RF.Records) && p.Acc.GetValueOrDefault(s) == Truth.True)) continue;
            var smallSeen = ClaimsOf(p).Any(c => SawFigure(c, Build.Small));
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
        // 指控以亲见优先（§13 第 3 条）：先指自己亲眼看见没记账取粮的人，再依转述与成见。
        var suspects = People.Where(q => q != w && (Belief(w, q, T.Suspect) ?? 0) > 0)
            .OrderByDescending(q => _sawTaking.Contains((w.Id, q.Id)) ? 1 : 0).ThenBy(q => q.Id).ToArray();
        if (suspects.Length == 0) return;
        var q = suspects[0];
        var n = Belief(w, w.M[Ledger], T.Unrecorded) ?? 0;
        var build = BuildText(q.Pro.Build);
        var sawFigure = ClaimsOf(w).Any(c => SawFigure(c, q.Pro.Build));
        var charge = K(_sawTaking.Contains((w.Id, q.Id)) ? "指控亲见" : sawFigure ? "指控身影" : "指控台账", w.Id, q.Id, night: Day - 1,
            values: [("数", Cn(n)), ("身形", build)]);
        var text = Plain(charge);
        var item = NewsFrom(w, NewsKind.Accusation, charge, about: q, trust: -2);
        foreach (var l in People.Where(l => l != w && Where(l) == Place.Hall && l != q))
        {
            Receive(w, l, item, w.News[item.Id], Channel.Public);
            if (TrustIn(l, w) >= 2) Know(l, q.Body, T.Branded, 1, ObservationSource.Testimony, text);
        }
        Know(w, q.Body, T.Accused, 1, ObservationSource.Introspection);
        if (_currentEvent > 0) Touch(q.Body, T.Accused, _currentEvent, w.Id);
        if (Where(q) == Place.Hall) { Witness(w, K("当众指控对象", w.Id, q.Id), -2, 0, [q], spread: false); q.News[item.Id] = new(w.Id, 1, ObservationSource.Perception, Day, item.Id); }
        if (q.Id == P.Quer) AccusedQuer = true;
        Accused ??= q.Id;
        Accusations.Add((Day, w.Id, q.Id, n));
        Log.Add($"D{Day} {Names.Of(Slot)} {text}");
        if (Where(Det) == Place.Hall) { Feed.Add(Stop(Present(charge))); NoteFor(w.Id, "指控", text); }
    }

    /// <summary>这条知识里有没有看见某种身形的身影（夜巡报告或远远望见）。</summary>
    private static bool SawFigure(Claim c, Build b) => c.Predicate switch
    {
        "夜巡报告" => c.Value("builds").Split(',').Contains(b.ToString()),
        "望见身影" => c.Value("build") == b.ToString(),
        _ => false
    };

    public readonly List<(int Day, P By, P Whom, int Count)> Accusations = [];

    private void DefendSpeech(Person l)
    {
        var who = People.Where(q => q != l && (Belief(l, q, T.Accused) ?? 0) > 0 && TrustIn(l, q) >= 1).ToArray();
        if (who.Length == 0) return;
        var q = who[0];
        var defense = K("辩护", l.Id, q.Id);
        var text = Plain(defense);
        var item = NewsFrom(l, NewsKind.Defense, defense, about: q, trust: 1);
        foreach (var h in People.Where(h => h != l && Where(h) == Place.Hall)) Receive(l, h, item, l.News[item.Id], Channel.Public);
        Log.Add($"D{Day} {Names.Of(Slot)} {text}");
        if (Where(Det) == Place.Hall) { Feed.Add(Present(defense)); NoteFor(l.Id, "说话", text); }
    }

    private void WardPosts(Person w)
    {
        // 被撕掉的规矩，沃德傍晚再贴回去。
        if (Day >= CaseDay && !WardNight.PostedAt.Contains(Place.Hall) && Belief(w, this[P.Quer], T.Branded) > 0 && _wardReposts++ < 2)
        {
            Post(WardNight, Place.Hall);
            NewsFrom(w, NewsKind.Rumor, K("贴规", w.Id), about: w);
            if (Where(Det) == Place.Hall) Feed.Add("沃德把那张被撕掉的规矩又贴了回去。");
        }
        if (Day >= CaseDay && Slot == Slot.Evening && WardCommittee is null)
        {
            WardCommittee = Spec("CommitteePlan");
            Post(WardCommittee, Place.Hall);
            NewsFrom(w, NewsKind.Rumor, K("贴规", w.Id), about: w);
            if (Where(Det) == Place.Hall) Feed.Add($"沃德在中央厅贴出：{WardCommittee.Text}");
        }
    }

    public RuleSpec? WardCommittee;

    // ------------------------------------------------------- fixed nodes --

    private void FixedBefore()
    {
        // 制度开场只登记一次；世界过程只改变物理状态，不代人物提交行动。
        if (Day == RationFirstDay && Slot == Slot.Forenoon && _institutionHooks.Add((Day, "ration_opening")))
        {
                Feed.Add(X("节点.集合"));
                LedgerBaseline = Get(Granary, T.Ration);
        }
        else if (Day == RationFirstDay && Slot == Slot.Noon && _institutionHooks.Add((Day, "ration_standoff")))
        {
                if (!Mediated) Feed.Add(X("节点.配给僵住"));
        }
        WorldProcesses("begin");
    }

    private void RoundHooks(int round)
    {
        if (Slot == Slot.Morning && round == 0) MorningBroadcast();
        if (Slot == Slot.Forenoon) TeachClass();
        if (round == 0)
        {
            // 剧本里写的张贴时段是立规者“打算贴”的时候：从这时起他心里有了这件事，由他自己走去贴（总纲 §六 第三期）。
            foreach (var rule in RuleDesign.All.Where(r => r.Author is not null))
                foreach (var post in rule.Posts.Where(p => p.Mode == "集中" && p.Day == Day && p.Slot == Slot))
                    IntendPost(this[rule.Author!.Value], RuleById(rule.Id), post.Place);
        }
        if (Slot == Slot.Evening && round == 0) AllianceEvening();
    }

    /// <summary>时段末的物理与账：烹煮效率、生领损耗、温室进度、病人、物资。</summary>
    private void SlotPhysics()
    {
        var previous = Get(Granary, T.Ration);
        WorldProcesses("end");
        LedgerBaseline += Get(Granary, T.Ration) - previous;
        if (Slot == Slot.Afternoon) GreenhousePhysics();
        Rt.Sync();
    }

    public bool GreenhouseRepaired => Get(Valve, new Tag("Repaired")) > 0;

    /// <summary>消费已有的双工位完成回执；物理条件、技能、产出与修复状态均由配方声明。</summary>
    private void GreenhousePhysics()
    {
        CompleteRecipes();
        var crew = _recipeCompletions.Where(r => r.Id == Job.Greenhouse.ToString()).SelectMany(r => r.Actors)
            .Distinct().Select(e => People.Single(p => ReferenceEquals(p.Body, e))).ToArray();
        _recipeCompletions.Clear();
        if (crew.Length == 0) return;
        if (!_warned2)
        {
            _warned2 = true;
            DemonSays(Names.Of(Place.Greenhouse), Warning2.Text);
            foreach (var p in crew.Where(p => p != Det)) Tell(p, Warning2, Channel.Public);
            Det.Acc[Warning2] = Truth.True;
            if (crew.Contains(Det)) Feed.Add(AstText("physical.greenhouse_warning"));
        }
        if (GreenhouseRepaired)
        {
            var names=JoinNames(crew.Select(p=>p.Name));
            Log.Add(AstText("physical.greenhouse_repaired",("人们",names)));
            var item = NewsFrom(crew[0], NewsKind.Fact, K("温室修好", values: ("人们", names)));
            if (crew.Contains(Det)) Witness(Det, K("主持修温室", P.Det), 1, 1, crew, domain: Dom.Rations);
            foreach (var p in crew.Skip(1)) p.News[item.Id] = new(p.Id, 0, ObservationSource.Perception, Day, item.Id);
        }
    }

    /// <summary>诊断：案发夜开始时各人的决策评估前几项。</summary>
    public readonly Dictionary<P, string> NightStartAssessment = [];

    private bool _warned2;
    public int GreenhouseProgress => Get(Valve, T.Work);

    // ---------------------------------------------------------------- night --

    private void RunNight(PlayerAction? act)
    {
        Options.BeforeNight?.Invoke(this);
        if (Day == LastDay - 1)
        {
            // 议事前夜，恶魔对每个人放大欲望一次（§13 第 12 条）。只有本人听见，侦探也只知道自己的那一句（恶魔不自曝）。
            foreach (var p in Npcs) { p.Desire = Math.Min(5, p.Desire + 1); RewriteDesire(p, "议事前夜，恶魔在耳边把那样东西又说了一遍"); }
            Feed.Add("入夜前，广播里没有声音。可你像听见有人贴着耳朵，把你最想知道的那件事又说了一遍。");
        }
        WorldProcesses("night");
        foreach (var p in People) { p.Asleep = false; p.NightSteps = 0; p.NightVisited.Clear(); }
        DropStaleDayActions();
        NightBegins();
        PerceiveAll();
        PlayerBegin(act);
        var start = Now;
        if (Day == CaseDay - 1)
            foreach (var p in Npcs) NightStartAssessment[p.Id] = p.Exec is not null ? $"（入夜时仍在做事：{p.Doing}；{(_running.TryGetValue(p.Exec, out var rr) ? "本章任务 " + rr.Why : "非本章任务")}；状态 {p.Exec.Status}）" : p.Asleep ? "（已睡）" : string.Join("；", DecisionSystem.Assess(p.World, p.Self).Take(4).Select(a => $"{(a.Forbidden ? "禁" : "")}{a.Decision.Score}:{JobOf(p, a.Decision.Effect)?.ToString() ?? "?"}[{string.Join(",", a.Reasons)}]"));
        foreach (var p in Npcs) Decide(p);
        PlayerDo(act);
        for (var t = TickLength; t < NightLength; t += TickLength)
            S.CTB.Schedule(t, NightTick);
        S.CTB.RunUntil(start + NightLength, 200_000);
        //CANCELTEST CancelAll();
        Seizure();
        foreach (var p in People) p.Asleep = false;
        PlayerEnd(act);
        SettlePrediction();
        SettleWagers();
        Gossip();
    }

    /// <summary>
    /// 入夜时还挂着的白天动作（例如傍晚在配给台等开粮、配给台已关）到此作废：夜里的决定从夜里的处境出发。
    /// 剧本占着的执行不动。否则此人整夜不再自己拿主意，现实与从快照重建的重演便各走各的。
    /// </summary>
    private void DropStaleDayActions()
    {
        foreach (var p in Npcs.Where(p => p.Exec is { } e && Story?.Owns(e) != true).ToArray())
        {
            var exec = p.Exec!;
            if(_socialAttempts.LastOrDefault(a=>ReferenceEquals(a.Execution,exec)) is {} attempt)CancelSocialAction(attempt.Action);
            CancelExecution(exec);
            _running.Remove(exec);
            ReleaseStation(p);
            p.Diary.Add($"D{Day} 入夜 放下了白天没做完的事：{(p.Doing is { } j ? Names.Of(j) : "请求之事")}");
            p.Exec = null;
            p.Doing = null;
        }
    }

    private void NightTick()
    {
        // 跟随：跟着的人走到哪里，跟随者就到哪里（房内几何属宿主层）。
        foreach (var (f, t) in _following.ToArray())
        {
            if (f.Doing != Job.Follow) { _following.Remove(f); continue; }
            if (Where(f) != Where(t)) { f.Body.Get<SpatialProperty>()!.Space = SpaceOf(t.Body); RealDirty = true; }
        }
        TrackMoves();
        foreach (var p in People.Where(p => !p.Asleep)) p.NightVisited.Add(Where(p));
        PerceiveAll();
        RuntimeStimuli("tick");
        // 守候中的人看到新情况，重新掂量：更想做的事出现了，就放下手里的等待。
        foreach (var p in Npcs.Where(p => !p.Asleep && p.Exec is not null && p.Doing is { } d && Interruptible.Contains(d)))
        {
            Interpret(p);
            var top = Pick(DecisionSystem.Assess(p.World, p.Self));
            if (top is null || top.Decision.Score <= p.DoingScore || JobOf(p, top.Decision.Effect) == p.Doing) continue;
            CancelExecution(p.Exec!);
            _running.Remove(p.Exec!);
            p.Exec = null;
            ReleaseStation(p);
            Submit(p, top.Decision, top.Reasons);
        }
    }

}
