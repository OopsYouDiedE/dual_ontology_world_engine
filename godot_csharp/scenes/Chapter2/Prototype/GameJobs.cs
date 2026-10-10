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
    /// <summary>答应过别人“今夜一起去开名录库”（应约者心里记下的事）。</summary>
    public static readonly RelationTag Promised = new("Promised"), AskedLeave = new("AskedLeave"), AskedFood = new("AskedFood");
    /// <summary>“我已经跟他讲过第 k 条规矩 / 第 l 课”（讲话的人心里记的，初始一律为否）。</summary>
    public const int MaxSpecs = 48;
    private static readonly RelationTag[] _told = [.. Enumerable.Range(0, MaxSpecs + 1).Select(k => new RelationTag("Told" + k))];
    private static readonly Dictionary<int, RelationTag> _taught = new[] { 11, 12, 13, 14 }.ToDictionary(l => l, l => new RelationTag("Taught" + l));
    public static RelationTag Told(int spec) => _told[Math.Min(spec, MaxSpecs)];
    public static RelationTag Taught(int lesson) => _taught[lesson];
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
    // 夜里取粮的人先等一等：晚饭散场、众人回寝区之后才动手，不当着一院子人开粮仓。
    private Effect TakeOne(bool record, bool self) => Wait(2, Seq([
        Go(Place.Logistics), OpenGranary(), Need(Not(Tg(g.Granary, T.Locked))),
        Spend(g.Granary, T.Ration, 1),
        .. record ? new[] { Change(g.Ledger, T.NightRecorded, 1) } : new[] { MeChange(T.OweNote, 1) },
        Set(g.Granary, T.Locked, 1),
        Wait(self ? 3 : 1.5, self ? Seq(MeChange(T.Fed, 1), MeChange(T.NightAte, 1)) : Seq(Go(Place.Clinic), Change(g.Bowl, T.Porridge, 1)))])); // 自取的人就地吃完

    // STORY-HARDCODE[CH2-040][logic][production]: 把具体本章工作映射为案件物证与人物行动结果。
    public Effect Job(Job job, Person? who = null) => job switch
    {
        Prototype.Job.Sleep => Seq(Go(who!.Pro.Dorm), Look(g.Space[who.Pro.Dorm])),
        Prototype.Job.Rest => Seq(Go(who!.Pro.Dorm), Look(g.Space[who.Pro.Dorm]), Wait(3, Look(g.Space[who.Pro.Dorm]))),
        Prototype.Job.Work => Seq(Go(Game.WorkPlace(who!.Id)), Look(g.Space[Game.WorkPlace(who.Id)]), Wait(2.5, Look(g.Space[Game.WorkPlace(who.Id)]))),
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
        // 守门：走到前厅，占住配重室这一站（同一时刻一站只容一个身体），身上带着“守门”的样子，守一阵再让出来（§13 第 4 条）。
        Prototype.Job.GuardArchive => Seq(Go(Place.ArchiveFront), Look(g.ArchiveDoor), Wait(0.3, Seq(Need(Not(Tg(g.WeightRoom, T.Occupied))), Set(g.WeightRoom, T.Occupied, 1),
            new Effect.Set(Me, T.Guarding, 1), Wait(8, Seq(Look(g.ArchiveDoor), Set(g.WeightRoom, T.Occupied, 0), new Effect.Set(Me, T.Guarding, 0)))))),
        Prototype.Job.VisitArchive => Seq(Go(Place.ArchiveFront), Look(g.ArchiveDoor), Wait(2, Look(g.ArchiveDoor))),
        Prototype.Job.Greenhouse => Seq(Go(Place.Greenhouse), Look(g.Gauge), Change(g.Valve, T.Work, 1)),
        Prototype.Job.Socialize => Seq(Go(Place.Hall), Look(g.Board[Place.Hall])),
        Prototype.Job.CountLedger => Seq(Go(Place.Logistics), Look(g.Granary), Look(g.Ledger), Change(g.Ledger, T.Counted, 1)),
        Prototype.Job.Stele => Seq(Go(Place.Oath), Look(g.Stele)),
        Prototype.Job.Scavenge => Wait(2, Seq(Go(Place.Logistics), Look(g.Granary), Wait(2, Look(g.Granary)))),
        Prototype.Job.Hoard => Wait(2, Seq(Go(Place.Logistics), OpenGranary(), Need(Not(Tg(g.Granary, T.Locked))), Spend(g.Granary, T.Ration, 2), Set(g.Granary, T.Locked, 1), MeChange(T.Stash, 2))),
        Prototype.Job.Accuse => Seq(Go(Place.Hall), Change(g.Board[Place.Hall], T.Accusation, 1)),
        Prototype.Job.PostRules => Seq(Go(Place.Hall), Change(g.Board[Place.Hall], T.Posted, 1)),
        Prototype.Job.Defend => Seq(Go(Place.Hall), Look(g.Board[Place.Hall])),
        // 开库：先走到前厅看一眼（看得见哪一站被人占着），再去占自己会的那一站使劲；那一站被占着就做不成（§13 第 4 条）。
        // 不带人的版本只是“动名录库之门”这个动作本身，供“夜里不许开库”一类的禁令匹配。
        Prototype.Job.Seize => who is null ? Change(g.ArchiveDoor, T.Work, 1) : SeizeAt(g.StationFor(who)),
        Prototype.Job.AnswerName => Seq(Look(g.ArchiveDoor), MeChange(T.Answered, 1)),
        Prototype.Job.Share => Seq(new Effect.Change(Target, T.Fed, 1), MeChange(T.Fed, -1), MeChange(T.Did(Prototype.Job.Share), 1)),
        Prototype.Job.Tear => Seq(Go(Place.Hall), Look(g.Board[Place.Hall]), Change(g.Board[Place.Hall], T.Torn, 1)),
        Prototype.Job.Watch => Seq(Go(Place.Logistics), Look(g.Granary), Wait(8, Look(g.Granary))),
        Prototype.Job.Treat => Seq(Go(Place.Clinic), Look(g.Bed), Need(Tg(g.MedCab, T.Medicine)), Spend(g.MedCab, T.Medicine, 1), Wait(1, Look(g.Bed))),
        Prototype.Job.Retreat => Seq(Go(Place.Oath), Look(g.Stele), Wait(3, Look(g.Stele))),
        Prototype.Job.Report => Seq(Go(Place.Hall), Look(g.Board[Place.Hall]), Wait(1, Look(g.Board[Place.Hall]))),
        Prototype.Job.Sample => who!.Id == P.Selma ? Seq(Go(Place.Greenhouse), Look(g.Gauge), Wait(1.5, Look(g.Gauge))) : Seq(Go(Place.Logistics), Look(g.Stove), Wait(1.5, Look(g.Stove))),
        Prototype.Job.NightSketch => Seq(Go(Place.Hall), Look(g.Space[Place.Hall]), Wait(6, Look(g.Space[Place.Hall]))),
        Prototype.Job.Sketch => Seq(Go(Place.DormS), Look(g.Space[Place.DormS]), Wait(2, Look(g.Space[Place.DormS]))),
        Prototype.Job.Tidy => Seq(Go(who!.Pro.Dorm), Look(g.Litter[who.Pro.Dorm]), Set(g.Litter[who.Pro.Dorm], T.Crumbs, 0), Set(g.Litter[who.Pro.Dorm], T.Emptied, 1)),
        _ => throw new NotSupportedException(job.ToString())
    };

    // 三站分处三地（配重室、锁芯在名录库前厅一带；传声中继在中央厅的管道枢纽）：各人走到自己那一站。
    private Effect SeizeAt(Essence st) => Seq(Go(g.PlaceOf[st]), Look(g.ArchiveDoor), Wait(0.3, Seq(Change(g.ArchiveDoor, T.Work, 1), Need(Not(Tg(st, T.Occupied))),
        Set(st, T.Occupied, 1), new Effect.Set(Me, T.Prying, 1), Wait(3, Seq(Look(g.ArchiveDoor), Set(st, T.Worked, 1), Set(st, T.Occupied, 0), new Effect.Set(Me, T.Prying, 0),
            new Effect.Think(new Condition.Not(new Condition.Related(Me, Promised, r(g.ArchiveDoor))))))))) ;

    /// <summary>沈鼎“先喂弱者”：等开粮，替饿着的人领一份记账送去。</summary>
    public Effect FeedWeak() => Seq(Go(Place.Logistics), new Effect.Wait(Tg(g.Desk, T.Open),
        Seq(Need(Tg(g.Granary, T.Ration)), Spend(g.Granary, T.Ration, 1), Change(g.Ledger, T.Handed, 1), new Effect.Change(Target, T.Fed, 1), MeChange(T.Did(Prototype.Job.FeedWeak), 1))));

    /// <summary>把粮仓钥匙交给某人（一个 Core 效果：自己的钥匙标签 −1，对方 +1）。</summary>
    public Effect GiveKeyTo(Person to) => Seq(Need(Mine(T.GranaryKey)), MeChange(T.GranaryKey, -1), Change(to.Body, T.GranaryKey, 1));

    /// <summary>同行请求的流程：去某处，看某物。</summary>
    public Effect Escort(Place place, Essence thing) => Seq(Go(place), Look(thing));

    /// <summary>玛格丽特“请工匠协作”：对在场、自己认得的工匠说话，请他修门闩；问过就记下（Think）。</summary>
    public Effect AskLatch(Person asker) => Seq(
        new Effect.Say(Target, "粮仓门闩松了。你是行家，搭把手？", new Fx(g, e => asker.M[e]).Job(Prototype.Job.RepairLatch),
            new Condition.Tagged(r(g.Granary), T.LatchLoose, 1), SpeechTopics.Latch),
        new Effect.Think(new Condition.Related(Target, Asked, r(g.Granary))));
}

public sealed partial class Game
{
    /// <summary>此人在名录库之门会去站的那一站：会开锁的去锁芯，懂传声（或学过第十二课）的去中继，有力气的去配重室。</summary>
    public Essence StationFor(Person p) => p.Pro.Lock >= 3 ? LockCore : p.Pro.Signal >= 3 ? RelayPost : p.Pro.Strength >= 1 ? WeightRoom
        : p.Pro.Lock >= 2 ? LockCore : p.Lessons.GetValueOrDefault(12) ? RelayPost : WeightRoom;

    /// <summary>此人平日做手艺的地方：人物块的“工作地点”，没写就是自己的寝区。</summary>
    public static Place WorkPlace(P p) => Chapter2Design.PersonData(p).WorkPlace ?? Cast.Of(p).Dorm;

    public static readonly ImmutableHashSet<Job> Interruptible = [Job.Rest, Job.InspectStructure, Job.GuardArchive, Job.Scavenge, Job.VisitArchive, Job.Seize, Job.Watch];

    public Fx FxOf(Person p) => new(this, e => p.M[e]);
    public Fx RealFx => new(this, e => View[e]);

    private readonly record struct HabitRule(Rule Rule, Job Job, string Note);

    /// <summary>习惯规则写入各人自己的 ProjectionWorld（“某某的习惯”概念上的 Rule，Acceptance=True）。</summary>
    public void WriteHabits(Person p)
    {
        var fx = FxOf(p);
        foreach (var j in Enum.GetValues<Job>())
        {
            if (j is Job.AskLatchHelp or Job.Escort or Job.FeedWeak or Job.Wander or Job.AskOpen or Job.Relay or Job.Teach or Job.Exchange or Job.Follow or Job.Avoid or Job.AskLeave or Job.Needle) continue;
            if (j is Job.GiveKey) { p.JobFx[j] = fx.GiveKeyTo(this[P.Shen]); continue; }
            p.JobFx[j] = fx.Job(j, p);
        }
        Observation.Observe(p.Soul, new ObservationInput(p.Habit.Name, p.Habit.Kind)
        { Target = p.Habit, Source = ObservationSource.Introspection, Time = Now, Rules = HabitRules(p) });
    }

    // STORY-HARDCODE[CH2-041][logic][production]: 为具名人物预置习惯行动及其观察规则。
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
                Job.GuardArchive => Fx.All(c, Fx.Not(fx.Tg(WeightRoom, T.Occupied))),
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
        // 会开锁、又饿过一天的人，夜里会多取（恐惧驱动的囤积）。
        // 茉拉“病者优先”不是囤积的来由：她为病人备粮走的是夜取病粮。
        if (p.Pro.Lock >= 3)
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
                // 被当成“私取之人”的她，入夜把自己寝区废纸篓里的饼屑倒掉（出于动机的清理；清理本身留下痕迹，§12 第 4 条）。
                Add(Job.Tidy, Fx.All(fx.At(Slot.Night), fx.Tg(Clock, T.Day, CaseDay), Fx.Mine(T.Branded), fx.Tg(Litter[Place.DormW], T.Crumbs), fx.NotDone(Job.Tidy)), 3);
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
        // 想开库的人夜里会去名录库前厅站一站：欲望越强、钩越多，去得越早、站得越久（§12 第 5 条：私占可预读）。翌日夜里起前厅会唤名。
        var pull = p.Desire + p.Hooks * 2 - 2;
        var linger = 1.5 + Math.Max(0, p.Desire - 2) + 2 * p.Hooks; // 带半拍，免得与“守在前厅”的动作同形
        p.JobFx[Job.VisitArchive] = Fx.Seq(fx.Go(Place.ArchiveFront), fx.Look(ArchiveDoor), Fx.Wait(linger, fx.Look(ArchiveDoor)));
        p.JobFx[Job.Seize] = fx.Job(Job.Seize, p); // 学了第十二课，会站的那一站可能变
        var opener = p.Pro.DesireFlags.HasFlag(RF.OpenArchive);
        if (p != Det && pull > 0 && (opener && p.Desire >= 3 || p.Hooks > 0))
            Add(Job.VisitArchive, Fx.All(fx.At(Slot.Night), fx.Tg(Clock, T.Day, p.Hooks > 0 ? FirstDay : CaseDay), fx.NotDone(Job.VisitArchive)), pull);
        else if (p != Det && pull > 0) Add(Job.VisitArchive, Fx.All(fx.At(Slot.Night), fx.Tg(Clock, T.Day, CaseDay), fx.NotDone(Job.VisitArchive)), pull);
        if (p != Det) PlanRules(p, fx, rules);
        return [.. rules];
    }

    /// <summary>
    /// 人物之间互相“写”与多步计划（§12 第 0 条）。全部是此人心中的规则，经 DecisionSystem 与他人的 SocialSystem 结算，不写成剧本：
    /// · 开库计划：想开库想得厉害（应过唤名、上了钩）、又没被“夜里不开”的约束住的人，翌日下午、傍晚去约会开锁的、力气大的、懂传声的人
    ///   “今夜一起去名录库”（Say + 请求）；答应的人心里记下约定，入夜照约去；约过人的人自己入夜也去。被拒就去约下一个；没人答应就只是去前厅站一站。
    /// · 答应过的人入夜赴约（但自己认的规矩仍可禁止：认了“夜里不开”的、主张封库的）。
    /// · 雀儿撕掉点她名的规矩；卢西安撕掉点名任何人的规矩。
    /// · 沃德在案子没了结的那两夜守在粮仓边上看谁来。
    /// · 心软的人把自己的口粮分给饿着的熟人（自己就少吃一顿）。
    /// 此方法在每个时段开头重建（规则里引用的是此人此刻的处境：欲望、钩、认了哪条规矩、信不信侦探）。
    /// </summary>
    // STORY-HARDCODE[CH2-042][logic][production]: 按本章人物身份与案件状态配置行动计划。
    private void PlanRules(Person p, Fx fx, List<Rule> rules)
    {
        var door = ArchiveDoor;
        var myStation = fx.Tg(StationFor(p), T.Occupied);
        rules.Add(new Rule(Fx.All(fx.At(Slot.Night), new Condition.Related(Fx.Me, Fx.Promised, fx.R(door)), fx.NotDone(Job.Seize), Fx.Not(myStation)), RuleMode.Prefer, p.JobFx[Job.Seize], 6, Truth.True));
        // 赴约或领头去开库的人，在前厅撞见守门的人：请他让开（他让不让，由他自己的 SocialSystem 决定）。
        var leave = Fx.Seq(fx.Set(WeightRoom, T.Occupied, 0), new Effect.Set(Fx.Me, T.Guarding, 0), fx.Go(Place.Hall));
        rules.Add(new Rule(Fx.All(fx.At(Slot.Night), Fx.Any([new Condition.Related(Fx.Me, Fx.Promised, fx.R(door)), .. People.Where(o => o != p).Select(o => (Condition)new Condition.Related(fx.R(o.Body), Fx.Asked, fx.R(door)))]),
                new Condition.IsKind(Fx.Target, ProjectionKind.Body), new Condition.Tagged(Fx.Target, T.Guarding, 1), new Condition.At(Fx.Target, new Noun.Slot(RuleSlot.Scene)),
                Fx.Not(new Condition.Related(Fx.Target, Fx.AskedLeave, fx.R(door)))), RuleMode.Prefer,
            Fx.Seq(new Effect.Say(Fx.Target, "让开。今夜这道门我们要开。", leave, Topic: SpeechTopics.Vacate), new Effect.Think(new Condition.Related(Fx.Target, Fx.AskedLeave, fx.R(door))),
                Fx.MeChange(T.Did(Job.AskLeave), 1)), 7, Truth.True));
        var bound = Specs.Any(s => s.Flags.HasFlag(RF.ForbidNightOpen) && p.Acc.GetValueOrDefault(s) == Truth.True);
        var waits = Specs.Any(s => s.Author == P.Det && s.Card is { Behavior: >= 5 and <= 8 } && p.Acc.GetValueOrDefault(s) == Truth.True) && TrustIn(p, Det) >= 2;
        // 被拉得够紧才会动手：上了钩，或被铜口单独低语过两回（它挑中的人）。
        if (p.Pro.DesireFlags.HasFlag(RF.OpenArchive) && (p.Hooks >= 1 && p.Desire >= 4 || p.Whispered >= 2 && p.Desire >= 3) && !bound && !waits)
        {
            Rule Ask(Tag skill, string text, int w) => new(Fx.All(fx.AtAny([Slot.Afternoon, Slot.Evening]), fx.Tg(Clock, T.Day, CaseDay),
                    new Condition.IsKind(Fx.Target, ProjectionKind.Body), new Condition.Tagged(Fx.Target, skill, 1), new Condition.Tagged(Fx.Target, Tags.Trust, 1),
                    new Condition.At(Fx.Target, new Noun.Slot(RuleSlot.Scene)), Fx.Not(new Condition.Related(Fx.Target, Fx.Asked, fx.R(door)))),
                RuleMode.Prefer, Fx.Seq(new Effect.Say(Fx.Target, text, new Effect.Think(new Condition.Related(Fx.Target, Fx.Promised, fx.R(door))), Topic: SpeechTopics.Invitation),
                    new Effect.Think(new Condition.Related(Fx.Target, Fx.Asked, fx.R(door)))), w, Truth.True);
            var w = p.Desire + Math.Max(p.Hooks, p.Whispered - 1);
            // 多步：缺哪样本事，就去找他心里最信得过、又有这本事的那个人——走到上次见到对方的地方，开口约；人不在就扑空，下个时段再找。
            void Seek(Tag skill, string text, int weight)
            {
                var asked = People.Where(o => o != p && (Belief(p, o, skill) ?? 0) > 0 && p.M[o.Body].Relations.Contains((Fx.Asked, p.M[door]))).Any();
                if (asked) return;
                var cand = People.Where(o => o != p && o != Det && (Belief(p, o, skill) ?? 0) > 0 && TrustIn(p, o) >= 1).OrderByDescending(o => TrustIn(p, o)).ThenBy(o => o.Id).FirstOrDefault();
                if (cand is null) return;
                var where = WorkPlace(cand.Id);
                // 下午去他平常干活的地方找；傍晚去配给台——饭点人都在那儿。
                foreach (var (slot, place) in new[] { (Slot.Afternoon, where), (Slot.Evening, Place.Logistics) })
                    rules.Add(new Rule(Fx.All(fx.At(slot), fx.Tg(Clock, T.Day, CaseDay), Fx.Not(new Condition.Related(fx.R(cand.Body), Fx.Asked, fx.R(door)))), RuleMode.Prefer,
                        Fx.Seq(fx.Go(place), new Effect.Say(fx.R(cand.Body), text, new Effect.Think(new Condition.Related(fx.R(cand.Body), Fx.Promised, fx.R(door))), Topic: SpeechTopics.Invitation),
                            new Effect.Think(new Condition.Related(fx.R(cand.Body), Fx.Asked, fx.R(door)))), weight, Truth.True));
            }
            if (p.Pro.Lock < 3) Seek(T.Picks, "今夜陪我去名录库前厅，帮我开那道锁。", w);
            if (p.Pro.Strength < 2) Seek(T.Strong, "今夜帮我拉一把名录库的配重。", w - 1);
            if (p.Pro.Signal < 3) Seek(T.Signals, "今夜帮我守名录库的传声中继。", w - 1);
            if (p.Pro.Lock < 3) rules.Add(Ask(T.Picks, "今夜陪我去名录库前厅，帮我开那道锁。", w));
            if (p.Pro.Strength < 2) rules.Add(Ask(T.Strong, "今夜帮我拉一把名录库的配重。", w - 1));
            if (p.Pro.Signal < 3) rules.Add(Ask(T.Signals, "今夜帮我守名录库的传声中继。", w - 1));
            rules.Add(new Rule(Fx.All(fx.At(Slot.Night), fx.Tg(Clock, T.Day, CaseDay), Fx.Any([.. People.Where(o => o != p).Select(o => (Condition)new Condition.Related(fx.R(o.Body), Fx.Asked, fx.R(door)))]), fx.NotDone(Job.Seize), Fx.Not(myStation)),
                RuleMode.Prefer, p.JobFx[Job.Seize], 5 + Math.Max(p.Hooks, p.Whispered - 1), Truth.True));
        }
        var tears = Events.Count(e => e.Who == p.Id && e.Job == Job.Tear);
        if (p.Id == P.Quer && WardNight.PostedAt.Contains(Place.Hall) && tears < 2)
            rules.Add(new Rule(Fx.All(fx.AtAny([Slot.Morning, Slot.Evening]), fx.Tg(Clock, T.Day, CaseDay), Fx.Mine(T.Branded), fx.NotDone(Job.Tear)), RuleMode.Prefer, p.JobFx[Job.Tear], 3, Truth.True));
        // 别家撕你的方案（§13 第 12 条）：中央厅板上贴着一条他以底线否决过的、侦探的名录库方案，他就去撕。
        if (p.Id is not (P.Quer or P.Lucian) && tears < 1 && Specs.Any(s => s.Author == P.Det && s.Card is { Behavior: >= 5 and <= 8 } && s.PostedAt.Contains(Place.Hall) && p.Vetoed.Contains(s)))
            rules.Add(new Rule(Fx.All(fx.AtAny([Slot.Morning, Slot.Evening]), fx.NotDone(Job.Tear)), RuleMode.Prefer, p.JobFx[Job.Tear], 4, Truth.True));
        if (p.Id == P.Lucian && Specs.Any(s => s.Flags.HasFlag(RF.Targets) && s.PostedAt.Contains(Place.Hall)) && tears < 2)
            rules.Add(new Rule(Fx.All(fx.AtAny([Slot.Morning, Slot.Evening]), fx.NotDone(Job.Tear)), RuleMode.Prefer, p.JobFx[Job.Tear], 3, Truth.True));
        // 守门的人在前厅撞见有人在动机关：请他回去（他回不回，由他自己的 SocialSystem 决定）。
        if (p.Pro.Habits.Any(h => h.Job == Job.GuardArchive) || Specs.Any(s => s.Flags.HasFlag(RF.ForbidNightOpen) && p.Acc.GetValueOrDefault(s) == Truth.True))
            rules.Add(new Rule(Fx.All(fx.At(Slot.Night), new Condition.IsKind(Fx.Target, ProjectionKind.Body), new Condition.Tagged(Fx.Target, T.Prying, 1),
                    new Condition.At(Fx.Target, new Noun.Slot(RuleSlot.Scene)), Fx.Not(new Condition.Related(Fx.Target, Fx.AskedLeave, fx.R(door)))), RuleMode.Prefer,
                Fx.Seq(new Effect.Say(Fx.Target, "依据：夜里不得动名录库。请回。", Fx.Seq(new Effect.Set(Fx.Me, T.Prying, 0), fx.Go(Place.Hall)), Topic: SpeechTopics.Vacate),
                    new Effect.Think(new Condition.Related(Fx.Target, Fx.AskedLeave, fx.R(door))), Fx.MeChange(T.Did(Job.AskLeave), 1)), 8, Truth.True));
        // 议事前夜，沃德不再守粮仓，改守名录库（委员会的主张：钥匙归章程，夜里谁也不许动）；此前案子没了结的夜里守粮仓看谁来。
        if (p.Id == P.Ward && Day == LastDay - 1)
            rules.Add(new Rule(Fx.All(fx.At(Slot.Night), Fx.Not(fx.Tg(WeightRoom, T.Occupied))), RuleMode.Prefer, p.JobFx[Job.GuardArchive], 7, Truth.True));
        else if (p.Id == P.Ward && Day <= CaseDay + 1)
            rules.Add(new Rule(Fx.All(fx.At(Slot.Night), fx.Tg(Ledger, T.Unrecorded), Fx.Any([.. People.Where(o => o != p).Select(o => fx.Tg(o.Body, T.Suspect))]), fx.NotDone(Job.Watch)),
                RuleMode.Prefer, p.JobFx[Job.Watch], 6, Truth.True));
        // 分粮：心软的人看见身边饿着、今天还没吃上的熟人，把自己那份分出去（自己就少吃一顿）。
        if (p.Id is P.Quer or P.Mora or P.Selma or P.Bertrand or P.Margaret or P.Huo)
            rules.Add(new Rule(Fx.All(fx.AtAny([Slot.Noon, Slot.Afternoon, Slot.Evening]), Fx.Mine(T.Fed), new Condition.IsKind(Fx.Target, ProjectionKind.Body), new Condition.Tagged(Fx.Target, T.Hungry, 1),
                    Fx.Not(new Condition.Tagged(Fx.Target, T.Fed, 1)), new Condition.Tagged(Fx.Target, Tags.Trust, 1), new Condition.At(Fx.Target, new Noun.Slot(RuleSlot.Scene)), fx.NotDone(Job.Share)),
                RuleMode.Prefer, p.JobFx[Job.Share], 3 + Math.Max(0, p.Pro.Cooperation), Truth.True));
        TalkRules(p, fx, rules);
        BackgroundLines(p, fx, rules);
    }

    /// <summary>
    /// 五名背景人物各自的欲望线（§13 第 14 条）——每人每天至少一件自己的事，做了会被人说起：
    /// 伊莲娜清早去诊疗所给脚踝换药、下午去誓约庭静坐；贝尔特朗清晨在中央厅报告昨夜夜巡所见；
    /// 维罗妮卡下午在灶边刮炉灰采样，饭点碰见茉拉就挑她账上的错（旧怨）；赛尔玛清早去温室剪源质样本；
    /// 莫兰病好以后，夜里坐在中央厅看颜色，第二天下午把看见的画成速写（速写认得出是谁，可作证据）。
    /// </summary>
    // STORY-HARDCODE[CH2-029][logic][production]: 背景人物按具体身份、日期和关系执行预设专属行为。
    private void BackgroundLines(Person p, Fx fx, List<Rule> rules)
    {
        void Daily(Job j, Slot[] slots, int w, Condition? extra = null) =>
            rules.Add(new Rule(Fx.All([fx.AtAny(slots), fx.NotDone(j), .. extra is null ? Array.Empty<Condition>() : [extra]]), RuleMode.Prefer, p.JobFx[j], w, Truth.True));
        switch (p.Id)
        {
            case P.Elena: Daily(Job.Treat, [Slot.Morning, Slot.Forenoon], 4); Daily(Job.Retreat, [Slot.Afternoon], 3); break;
            case P.Bertrand: Daily(Job.Report, [Slot.Morning], 5, fx.Tg(Clock, T.Day, FirstDay + 1)); break; // 始业日前夜各寝区都闩着，没有巡夜可报
            case P.Veronica:
                Daily(Job.Sample, [Slot.Afternoon], 3);
                rules.Add(new Rule(Fx.All(fx.AtAny([Slot.Noon, Slot.Evening]), new Condition.At(fx.R(this[P.Mora].Body), new Noun.Slot(RuleSlot.Scene)), fx.NotDone(Job.Needle)), RuleMode.Prefer,
                    Fx.Seq(new Effect.Say(fx.R(this[P.Mora].Body), "你夜里的取用，误差是多少？"), Fx.MeChange(T.Did(Job.Needle), 1)), 4, Truth.True));
                break;
            case P.Selma: Daily(Job.Sample, [Slot.Morning], 4); break;
            case P.Moran:
                Daily(Job.NightSketch, [Slot.Night], 3, Fx.All(Fx.Not(Fx.Mine(T.Sick)), fx.Tg(Clock, T.Day, CaseDay)));
                Daily(Job.Sketch, [Slot.Afternoon], 5, Fx.All(Fx.Not(Fx.Mine(T.Sick)), fx.Tg(Clock, T.Day, CaseDay)));
                break;
        }
    }

    private static readonly Slot[] DaySlots = [Slot.Morning, Slot.Forenoon, Slot.Noon, Slot.Afternoon, Slot.Evening];
    public static string RelayLine(RuleSpec s) => $"跟你说条规矩：「{s.Text}」";
    public static string TeachLine(int l) => $"《{Lessons[l].Title}》那堂课，我给你说说。";

    /// <summary>
    /// 转述、拉人、教人、换吃的、跟随、躲避（§13 第 5、7 条）——全部是此人心中的规则，经 DecisionSystem 选出、ActionRuntime 结算：
    /// 说话要占身体一段时间（同一身体不能同时做两件事），所以转述有成本，一个时段说不了几个人。
    /// · 转述：只转述自己认的、又合自己价值的规矩（自己立的规矩更起劲）；对象是身边信得过的人，讲过的不再讲。
    /// · 拉人：立规者傍晚到配给台找同一聚落的人当面讲自己的规矩。
    /// · 教人：好合作的、看重转述的人，把自己完整听过的课讲给身边的人（听的人从此“完整”学过这一课）。
    /// · 换吃的：昨天没吃上、今天也还没吃上的人，向身边吃过的熟人讨一口，答应明早替他挑水。
    /// · 跟随：沃德在案子没了结的夜里跟着他怀疑的人走。
    /// · 躲避：雀儿一见沃德就走开，伊莲娜一见阮清越就走开。
    /// </summary>
    private void TalkRules(Person p, Fx fx, List<Rule> rules)
    {
        var clock = fx.R(Clock);
        Condition Near() => Fx.All(new Condition.IsKind(Fx.Target, ProjectionKind.Body), new Condition.At(Fx.Target, new Noun.Slot(RuleSlot.Scene)), new Condition.Tagged(Fx.Target, Tags.Trust, 1));
        foreach (var s in Specs.Where(s => !s.ByDemon && s.Author is not null && s != RuanLedger && s.Id <= Fx.MaxSpecs && (s.Author == p.Id || p.Acc.GetValueOrDefault(s) == Truth.True)))
        {
            var own = s.Author == p.Id;
            var fits = own || Enum.GetValues<RF>().Where(f => f != RF.None && s.Flags.HasFlag(f)).Sum(f => p.Pro.Values.GetValueOrDefault(f)) > 0;
            if (!fits || !own && AcceptScore(p, s, Channel.Relay, null, out _) < Options.AcceptThreshold + 1) continue;
            var line = RelayLine(s);
            var told = Fx.Told(s.Id);
            var talk = Fx.Seq(new Effect.Say(Fx.Target, line, Topic: SpeechTopics.Rule(s.Id)), Fx.Wait(2.5, new Effect.Act(ActionOp.Inspect, Fx.Target)), new Effect.Think(new Condition.Related(Fx.Target, told, clock)), Fx.MeChange(T.Did(Job.Relay), 1));
            rules.Add(new Rule(Fx.All(fx.AtAny(DaySlots), Near(), Fx.Not(new Condition.Related(Fx.Target, told, clock))), RuleMode.Prefer, talk, own ? 2 : 1, Truth.True));
            if (!own || Cast.ClusterOf(p.Id) == Cluster.None) continue;
            foreach (var mate in Npcs.Where(m => m != p && Cast.ClusterOf(m.Id) == Cast.ClusterOf(p.Id)))
            {
                var r = fx.R(mate.Body);
                rules.Add(new Rule(Fx.All(fx.At(Slot.Evening), Fx.Not(new Condition.Related(r, told, clock))), RuleMode.Prefer,
                    Fx.Seq(fx.Go(Place.Logistics), new Effect.Say(r, line, Topic: SpeechTopics.Rule(s.Id)), Fx.Wait(1.5, new Effect.Act(ActionOp.Inspect, r)), new Effect.Think(new Condition.Related(r, told, clock)), Fx.MeChange(T.Did(Job.Relay), 1)), 3, Truth.True));
            }
        }
        if (p.Pro.Cooperation >= 1 || p.Pro.Values.GetValueOrDefault(RF.Relay) > 0)
            foreach (var l in p.Lessons.Where(l => l.Value).Select(l => l.Key))
                if (l is 12 or 14) rules.Add(new Rule(Fx.All(fx.AtAny([Slot.Noon, Slot.Afternoon, Slot.Evening]), Near(), fx.NotDone(Job.Teach), Fx.Not(new Condition.Related(Fx.Target, Fx.Taught(l), clock))), RuleMode.Prefer,
                    Fx.Seq(new Effect.Say(Fx.Target, TeachLine(l), Topic: SpeechTopics.Lesson(l)), Fx.Wait(2, new Effect.Act(ActionOp.Inspect, Fx.Target)), new Effect.Think(new Condition.Related(Fx.Target, Fx.Taught(l), clock)), Fx.MeChange(T.Did(Job.Teach), 1)),
                    2, Truth.True));
        // 换吃的：给的人从自己那份里分出来（他答不答应，由他自己的 SocialSystem 决定）。
        rules.Add(new Rule(Fx.All(fx.AtAny([Slot.Noon, Slot.Evening]), Fx.Mine(T.Hungry), Fx.Not(Fx.Mine(T.Fed)), Near(), new Condition.Tagged(Fx.Target, T.Fed, 1),
                Fx.Not(new Condition.Related(Fx.Target, Fx.AskedFood, clock)), fx.NotDone(Job.Exchange)), RuleMode.Prefer,
            Fx.Seq(new Effect.Say(Fx.Target, "分我一口吃的，明天一早我替你挑水。", Fx.Seq(Fx.Need(Fx.Mine(T.Fed)), Fx.MeChange(T.Fed, -1), fx.Change(p.Body, T.Fed, 1), fx.Change(p.Body, T.OwesWater, 1))),
                new Effect.Think(new Condition.Related(Fx.Target, Fx.AskedFood, clock)), Fx.MeChange(T.Did(Job.Exchange), 1)), 4, Truth.True));
        rules.Add(new Rule(Fx.All(fx.AtAny([Slot.Morning, Slot.Forenoon]), Fx.Mine(T.OwesWater)), RuleMode.Prefer, Fx.Seq(fx.Job(Job.FetchWater), Fx.MeChange(T.OwesWater, -1)), 5, Truth.True));
        // 跟随：沃德夜里跟着他心里的嫌疑人。
        if (p.Id == P.Ward && Day <= CaseDay + 2)
            rules.Add(new Rule(Fx.All(fx.At(Slot.Night), new Condition.IsKind(Fx.Target, ProjectionKind.Body), new Condition.Tagged(Fx.Target, T.Suspect, 1),
                    new Condition.At(Fx.Target, new Noun.Slot(RuleSlot.Scene)), fx.NotDone(Job.Follow)), RuleMode.Prefer,
                Fx.Seq(new Effect.Act(ActionOp.Inspect, Fx.Target), Fx.Wait(8, new Effect.Act(ActionOp.Inspect, Fx.Target)), Fx.MeChange(T.Did(Job.Follow), 1)), 7, Truth.True));
        // 躲避：一见那个人就走开。
        foreach (var (who, enemy, away) in new[] { (P.Quer, P.Ward, Place.Oath), (P.Elena, P.Ruan, Place.DormS) })
            if (p.Id == who)
                rules.Add(new Rule(Fx.All(fx.AtAny([Slot.Morning, Slot.Forenoon, Slot.Afternoon]), new Condition.At(fx.R(this[enemy].Body), new Noun.Slot(RuleSlot.Scene)), fx.NotDone(Job.Avoid)),
                    RuleMode.Prefer, Fx.Seq(fx.Go(away), fx.Look(Space[away]), Fx.MeChange(T.Did(Job.Avoid), 1)), 4, Truth.True));
    }

    /// <summary>每个时段开头，按各人此刻的处境重建心中的习惯与计划。</summary>
    private void RefreshPlans()
    {
        foreach (var p in Npcs)
            Observation.Observe(p.Soul, new ObservationInput(p.Habit.Name, p.Habit.Kind) { Target = p.Habit, Source = ObservationSource.Introspection, Time = Now, Rules = HabitRules(p) });
    }

    public const int GreenhouseNeed = 6;
}
