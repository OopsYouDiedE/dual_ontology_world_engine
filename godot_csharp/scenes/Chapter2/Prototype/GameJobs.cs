using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>
/// 行动的 Core 表达。同一件事在任何人的内部世界里都编译成结构相同的 Effect，只是名词换成各自的投影；
/// 因此公告板上的规则（以现实投影编译，经 WorldRuntime.Observe 映射进读者心中）与读者自己的习惯、
/// 与他人请求里的流程，能被 DecisionSystem 识别为同一件事。
/// </summary>
public sealed class Fx(Game g, Func<Essence, Projection> r)
{
    public static readonly Noun Me = new Noun.Slot(RuleSlot.Actor);
    public static readonly Noun Target = new Noun.Slot(RuleSlot.Target);
    public static readonly RelationTag Asked = new("Asked");
    public static readonly Condition Constant = new Condition.Constant(Truth.True);
    public Projection R(Essence e) => r(e);
    public Effect Go(Place p) => new Effect.Act(ActionOp.Travel, r(g.Way[p]));
    public Effect Look(Essence e) => new Effect.Act(ActionOp.Inspect, r(e));
    public static Effect Seq(params Effect[] items) => new Effect.Sequence([.. items]);
    public Effect Set(Essence e, Tag t, int v) => new Effect.Set(r(e), t, v);
    public Effect Change(Essence e, Tag t, int v) => new Effect.Change(r(e), t, v);
    public Effect Spend(Essence e, Tag t, int v) => new Effect.Spend(r(e), t, v);
    public static Effect MeChange(Tag t, int v) => new Effect.Change(Me, t, v);
    public static Effect When(Condition c, Effect e) => new Effect.When(c, e);
    public static Effect Need(Condition c) => new Effect.Require(c);
    public static Effect Wait(double d, Effect e) => new Effect.Wait(d, e);
    public Condition Tg(Essence e, Tag t, int min = 1) => new Condition.Tagged(r(e), t, min);
    public static Condition Mine(Tag t, int min = 1) => new Condition.Tagged(Me, t, min);
    public static Condition Not(Condition c) => new Condition.Not(c);
    public static Condition All(params Condition[] c) => new Condition.All([.. c]);
    public static Condition Any(params Condition[] c) => new Condition.Any([.. c]);
    public Condition At(Slot s) => Tg(g.Clock, T.SlotTag[s]);
    public Condition AtAny(IEnumerable<Slot> s) { var a = s.Select(At).ToArray(); return a.Length == 1 ? a[0] : Any(a); }
    public Condition NotDone(Job j) => Not(Mine(T.Did(j)));
    /// <summary>此人知道有人病着（任何一个他人身上的 Sick 标签为真）。</summary>
    public Condition AnySick(Person? self) => Any([.. g.People.Where(o => o != self).Select(o => Tg(o.Body, T.Sick))]);

    private Effect OpenGranary() => When(Tg(g.Granary, T.Locked), Seq(
        When(Mine(T.GranaryKey), Set(g.Granary, T.Locked, 0)),
        When(Mine(T.Lockpick), Set(g.Granary, T.Locked, 0)),
        When(Tg(g.Granary, T.LatchLoose), Set(g.Granary, T.Locked, 0))));

    // 取粮要在后勤院待上一会儿（开锁、取、关门），这段时间里别人看得见他；送去诊疗所是下一段。
    private Effect TakeOne(bool record, bool self) => Seq([
        Go(Place.Logistics), OpenGranary(), Need(Not(Tg(g.Granary, T.Locked))),
        Spend(g.Granary, T.Ration, 1),
        .. record ? new[] { Change(g.Ledger, T.NightRecorded, 1) } : new[] { MeChange(T.OweNote, 1) },
        Set(g.Granary, T.Locked, 1),
        Wait(self ? 3 : 1.5, self ? Seq(MeChange(T.Fed, 1), MeChange(T.NightAte, 1)) : Seq(Go(Place.Clinic), Change(g.Bowl, T.Porridge, 1)))]); // 自取的人就地吃完

    public Effect Job(Job job, Person? who = null) => job switch
    {
        Prototype.Job.Sleep => Seq(Go(who!.Pro.Dorm), Look(g.Space[who.Pro.Dorm])),
        Prototype.Job.Rest => Seq(Go(who!.Pro.Dorm), Look(g.Space[who.Pro.Dorm]), Wait(3, Look(g.Space[who.Pro.Dorm]))),
        Prototype.Job.Work => Seq(Go(Game.WorkPlace(who!.Id)), Look(g.Space[Game.WorkPlace(who.Id)])),
        Prototype.Job.Class => Seq(Go(Place.Lecture), Look(g.Blackboard)),
        Prototype.Job.Collect => Seq(Go(Place.Logistics), new Effect.Wait(Tg(g.Desk, T.Open),
            Seq(Need(Tg(g.Granary, T.Ration)), Spend(g.Granary, T.Ration, 1), MeChange(T.Fed, 1), Change(g.Ledger, T.Handed, 1)))),
        Prototype.Job.Distribute => Seq(Go(Place.Logistics), Need(Mine(T.GranaryKey)), Set(g.Granary, T.Locked, 0), Set(g.Desk, T.Open, 1), Look(g.Ledger)),
        Prototype.Job.Cook => Seq(Go(Place.Logistics), Need(Tg(g.Jar, T.Water)), Spend(g.Jar, T.Water, 1), Set(g.Stove, T.Cooked, 1)),
        Prototype.Job.FetchWater => Seq(Go(Place.Logistics), Change(g.Jar, T.Water, 3)),
        Prototype.Job.Care => Seq(Go(Place.Clinic), Look(g.Bed), When(Mine(T.MedKey), Seq(Need(Tg(g.MedCab, T.Medicine)), Spend(g.MedCab, T.Medicine, 1))), Change(g.Bed, T.Treated, 1)),
        Prototype.Job.NightTakeSick => TakeOne(false, false),
        Prototype.Job.NightTakeSickRec => TakeOne(true, false),
        Prototype.Job.NightTakeSelf => TakeOne(false, true),
        Prototype.Job.NightTakeSelfRec => TakeOne(true, true),
        Prototype.Job.Inform => Seq(Go(Place.Logistics), Look(g.Ledger), Change(g.Ledger, T.NightRecorded, 1), MeChange(T.OweNote, -1)),
        Prototype.Job.RepairLatch => Seq(Go(Place.Logistics), Need(Tg(g.Granary, T.LatchLoose)), Set(g.Granary, T.LatchLoose, 0), Set(g.Granary, T.ToolMarks, 1)),
        Prototype.Job.InspectStructure => Seq(Go(Place.Logistics), Look(g.Granary), Wait(5, Look(g.Granary))),
        Prototype.Job.Patrol => Seq(Go(Place.Hall), Wait(2.5, Seq(Go(Place.ArchiveFront), Wait(2.5, Seq(Go(Place.Hall), Wait(2.5, Seq(Go(Place.Hall), Wait(2.5, Go(Place.DormL))))))))),
        Prototype.Job.GuardArchive => Seq(Go(Place.ArchiveFront), Look(g.ArchiveDoor), Wait(6, Look(g.ArchiveDoor))),
        Prototype.Job.VisitArchive => Seq(Go(Place.ArchiveFront), Look(g.ArchiveDoor), Wait(2, Look(g.ArchiveDoor))),
        Prototype.Job.Greenhouse => Seq(Go(Place.Greenhouse), Look(g.Gauge), Change(g.Valve, T.Work, 1)),
        Prototype.Job.Socialize => Seq(Go(Place.Hall), Look(g.Board[Place.Hall])),
        Prototype.Job.CountLedger => Seq(Go(Place.Logistics), Look(g.Granary), Look(g.Ledger), Change(g.Ledger, T.Counted, 1)),
        Prototype.Job.Stele => Seq(Go(Place.Oath), Look(g.Stele)),
        Prototype.Job.Scavenge => Wait(2, Seq(Go(Place.Logistics), Look(g.Granary), Wait(2, Look(g.Granary)))),
        Prototype.Job.Hoard => Seq(Go(Place.Logistics), OpenGranary(), Need(Not(Tg(g.Granary, T.Locked))), Spend(g.Granary, T.Ration, 2), Set(g.Granary, T.Locked, 1), MeChange(T.Stash, 2)),
        Prototype.Job.Accuse => Seq(Go(Place.Hall), Change(g.Board[Place.Hall], T.Accusation, 1)),
        Prototype.Job.PostRules => Seq(Go(Place.Hall), Change(g.Board[Place.Hall], T.Posted, 1)),
        Prototype.Job.Defend => Seq(Go(Place.Hall), Look(g.Board[Place.Hall])),
        Prototype.Job.Seize => Seq(Go(Place.ArchiveFront), Change(g.ArchiveDoor, T.Work, 1)),
        Prototype.Job.AnswerName => Seq(Look(g.ArchiveDoor), MeChange(T.Answered, 1)),
        _ => throw new NotSupportedException(job.ToString())
    };

    /// <summary>沈鼎“先喂弱者”：等开粮，替饿着的人领一份记账送去。</summary>
    public Effect FeedWeak() => Seq(Go(Place.Logistics), new Effect.Wait(Tg(g.Desk, T.Open),
        Seq(Need(Tg(g.Granary, T.Ration)), Spend(g.Granary, T.Ration, 1), Change(g.Ledger, T.Handed, 1), new Effect.Change(Target, T.Fed, 1), MeChange(T.Did(Prototype.Job.FeedWeak), 1))));

    /// <summary>把粮仓钥匙交给某人（一个 Core 效果：自己的钥匙标签 −1，对方 +1）。</summary>
    public Effect GiveKeyTo(Person to) => Seq(Need(Mine(T.GranaryKey)), MeChange(T.GranaryKey, -1), Change(to.Body, T.GranaryKey, 1));

    /// <summary>同行请求的流程：去某处，看某物。</summary>
    public Effect Escort(Place place, Essence thing) => Seq(Go(place), Look(thing));

    /// <summary>玛格丽特“请工匠协作”：对在场、自己认得的工匠说话，请他修门闩；问过就记下（Think）。</summary>
    public Effect AskLatch(Person asker) => Seq(
        new Effect.Say(Target, "粮仓门闩松了。你是行家，搭把手？", new Fx(g, e => asker.M[e]).Job(Prototype.Job.RepairLatch)),
        new Effect.Think(new Condition.Related(Target, Asked, r(g.Granary))));
}

public sealed partial class Game
{
    public static Place WorkPlace(P p) => p switch
    {
        P.Huo => Place.Lecture, P.Su => Place.Lecture, P.Veronica => Place.Clinic, P.Moran => Place.Oath, P.Quer => Place.DormW,
        P.Shen => Place.Logistics, _ => Cast.Of(p).Dorm
    };

    public static readonly ImmutableHashSet<Job> Interruptible = [Job.Rest, Job.InspectStructure, Job.GuardArchive, Job.Scavenge, Job.VisitArchive];

    public Fx FxOf(Person p) => new(this, e => p.M[e]);
    public Fx RealFx => new(this, e => View[e]);

    private readonly record struct HabitRule(Rule Rule, Job Job, string Note);

    /// <summary>习惯规则写入各人自己的 ProjectionWorld（“某某的习惯”概念上的 Rule，Acceptance=True）。</summary>
    public void WriteHabits(Person p)
    {
        var fx = FxOf(p);
        foreach (var j in Enum.GetValues<Job>())
        {
            if (j is Job.AskLatchHelp or Job.Escort or Job.FeedWeak) continue;
            if (j is Job.GiveKey) { p.JobFx[j] = fx.GiveKeyTo(this[P.Shen]); continue; }
            p.JobFx[j] = fx.Job(j, p);
        }
        Observation.Observe(p.Soul, new ObservationInput(p.Habit.Name, p.Habit.Kind)
        { Target = p.Habit, Source = ObservationSource.Introspection, Time = Now, Rules = HabitRules(p) });
    }

    public ImmutableArray<Rule> HabitRules(Person p)
    {
        var fx = FxOf(p);
        var rules = new List<Rule>();
        void Add(Job job, Condition when, int weight) => rules.Add(new Rule(when, RuleMode.Prefer, p.JobFx[job], Math.Max(weight, 0), Truth.True));
        Condition Base(Job job, IEnumerable<Slot> slots) => Fx.All(fx.AtAny(slots), fx.NotDone(job));
        foreach (var h in p.Pro.Habits)
        {
            var weight = h.Weight + (h.Desire ? p.Desire - 1 : 0);
            var c = Base(h.Job, h.Slots);
            c = h.Job switch
            {
                Job.Collect => Fx.All(c, Fx.Not(Fx.Mine(T.Fed))),
                Job.Distribute => Fx.All(c, Fx.Mine(T.GranaryKey)),
                Job.Greenhouse => Fx.All(c, Fx.Not(fx.Tg(Valve, T.Work, GreenhouseNeed))),
                Job.Accuse => Fx.All(c, fx.Tg(Ledger, T.Unrecorded), Fx.Any([.. People.Where(o => o != p).Select(o => fx.Tg(o.Body, T.Suspect))])),
                Job.Defend => Fx.All(c, Fx.Any([.. People.Where(o => o != p).Select(o => Fx.All(fx.Tg(o.Body, T.Accused), fx.Tg(o.Body, Tags.Trust, 1)))])),
                Job.FetchWater => Fx.All(c, Fx.Not(fx.Tg(Jar, T.Water, 3))),
                Job.InspectStructure => Fx.All(c, fx.Tg(Granary, T.LatchLoose, 0)),
                _ => c
            };
            Add(h.Job, c, weight);
        }
        // 反对轻改禁忌的人：夜里不去开名录库。
        if (p.Pro.Values.GetValueOrDefault(RF.SealArchive) >= 2) rules.Add(new Rule(Fx.Constant, RuleMode.Forbid, p.JobFx[Job.Seize], 3, Truth.True));
        // 手里有粮仓钥匙的人，到饭点去开粮（钥匙换了人，开粮的人也跟着换）。
        Add(Job.Distribute, Fx.All(fx.AtAny([Slot.Noon, Slot.Evening]), fx.NotDone(Job.Distribute), Fx.Mine(T.GranaryKey)), 4);
        // 晨间广播在中央厅：没有更要紧的事就去听。
        Add(Job.Socialize, fx.At(Slot.Morning), 3);
        // 人人都有的底线：夜里回寝区睡；白天无事就歇着。
        Add(Job.Sleep, fx.At(Slot.Night), 1);
        Add(Job.Rest, fx.AtAny([Slot.Morning, Slot.Forenoon, Slot.Noon, Slot.Afternoon, Slot.Evening]), 0);
        // 有钥匙或会开锁的人，饿过一天，夜里会多取（恐惧驱动的囤积）。
        if (p.Pro.Lock >= 3 || p.Id == P.Mora)
            Add(Job.Hoard, Fx.All(fx.At(Slot.Night), Fx.Mine(T.Hungry), fx.NotDone(Job.Hoard)), 2);
        switch (p.Id)
        {
            case P.Mora:
                Add(Job.Care, Fx.All(fx.AtAny([Slot.Morning, Slot.Forenoon, Slot.Afternoon]), fx.NotDone(Job.Care), fx.AnySick(p)), 4);
                // 病者优先：知道有人病着、碗里没粥，夜里替病人取一份（她自己的规则的另一半，用在病人身上）。
                Add(Job.NightTakeSick, Fx.All(fx.At(Slot.Night), fx.AnySick(p), Fx.Not(fx.Tg(Bowl, T.Porridge)), fx.NotDone(Job.NightTakeSick)), 4);
                break;
            case P.Margaret:
                // 先查结构：知道门闩松了，夜里再去守着看。
                Add(Job.InspectStructure, Fx.All(fx.At(Slot.Night), fx.Tg(Granary, T.LatchLoose), fx.NotDone(Job.InspectStructure)), 3);
                rules.Add(new Rule(Fx.All(fx.At(Slot.Night), fx.Tg(Granary, T.LatchLoose),
                        new Condition.IsKind(Fx.Target, ProjectionKind.Body), new Condition.Tagged(Fx.Target, T.Craftsman, 1),
                        new Condition.Tagged(Fx.Target, Tags.Trust, 1), new Condition.At(Fx.Target, new Noun.Slot(RuleSlot.Scene)),
                        Fx.Not(new Condition.Tagged(Fx.Target, T.Branded, 1)), Fx.Not(new Condition.Related(Fx.Target, Fx.Asked, fx.R(Granary)))),
                    RuleMode.Prefer, fx.AskLatch(p), 4, Truth.True));
                break;
            case P.Quer:
                Add(Job.Scavenge, Fx.All(fx.At(Slot.Night), Fx.Not(Fx.Mine(T.Fed)), fx.NotDone(Job.Scavenge)), 2);
                // 被当众盯视的那天傍晚，不去配给台。
                rules.Add(new Rule(Fx.All(fx.At(Slot.Evening), Fx.Mine(T.Shamed)), RuleMode.Forbid, p.JobFx[Job.Collect], 1, Truth.True));
                // 以技艺回报信任她的人：知道哪里坏了就修（请托本身的分量由 SocialSystem 另计）。
                Add(Job.RepairLatch, Fx.All(fx.AtAny([Slot.Evening, Slot.Night]), fx.Tg(Granary, T.LatchLoose), fx.NotDone(Job.RepairLatch)), 2);
                break;
            case P.Shen:
                // 先喂弱者：看见谁饿着，开粮时替他多领一份送去（记在台账上）。
                rules.Add(new Rule(Fx.All(fx.AtAny([Slot.Noon, Slot.Evening]), new Condition.IsKind(Fx.Target, ProjectionKind.Body), new Condition.Tagged(Fx.Target, T.Hungry, 1),
                        Fx.Not(new Condition.Tagged(Fx.Target, T.Fed, 1)), fx.NotDone(Job.FeedWeak)),
                    RuleMode.Prefer, fx.FeedWeak(), 4, Truth.True));
                break;
            case P.Huo:
                Add(Job.RepairLatch, Fx.All(fx.AtAny([Slot.Afternoon, Slot.Evening]), fx.Tg(Granary, T.LatchLoose), fx.NotDone(Job.RepairLatch)), 2);
                break;
        }
        // 第五日起名录库会唤名：欲望与钩把人往那里拉。
        var pull = p.Desire + p.Hooks * 2 - 2;
        if (p != Det && pull > 0) Add(Job.VisitArchive, Fx.All(fx.At(Slot.Night), fx.Tg(Clock, T.Day, 5), fx.NotDone(Job.VisitArchive)), pull);
        return [.. rules];
    }

    public const int GreenhouseNeed = 6;
}
