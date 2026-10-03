using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public enum Cmd { Rest, Class, Collect, Inspect, Escort, Ask, GiveKey, Tell, Post, TellRule, Greenhouse, Archive, Help, Mediate, Stakeout, Sleep, Rub, ShareFood, Manipulate, Confront, AskAbout, Reopen, Apologize }

/// <summary>侦探一个时段的行动。每个时段只能做一件事（第三部分 §4）。</summary>
public sealed record PlayerAction(Cmd Cmd)
{
    public P? Who { get; init; }
    public Place? Place { get; init; }
    public string? Thing { get; init; }
    public Job? Job { get; init; }
    public Card? Card { get; init; }
    /// <summary>用称呼引用的已有规矩（讲、贴）。</summary>
    public RuleSpec? Spec { get; init; }
    public int Topic { get; init; } = -1;
    public bool Precise { get; init; }
    public P[] Crew { get; init; } = [];
    public int NewsId { get; init; }
    public P? About { get; init; }
    public Game.ConfrontPlan? Plan { get; init; }
    public string Describe() => Cmd switch
    {
        Cmd.Class => Topic >= 0 ? $"上课，并就「{Game.Topics[Topic]}」{(Precise ? "精确" : "含糊")}提问" : "上课",
        Cmd.Inspect => $"调查：{Names.Of(Place!.Value)}的{Thing}",
        Cmd.Escort => $"请{Names.Of(Who!.Value)}同行去{Names.Of(Place!.Value)}看{Thing}",
        Cmd.Ask => $"请{Names.Of(Who!.Value)}{Names.Of(Job!.Value)}",
        Cmd.Post => $"在{Names.Of(Place!.Value)}张贴「{Card!.Text}」",
        Cmd.TellRule => $"当面告诉{Names.Of(Who!.Value)}「{Card!.Text}」",
        _ => Cmd.ToString()
    };
}

public sealed partial class Game
{
    public readonly List<(int Day, P Who, Place Where, bool? Right)> Predictions = [];
    public readonly List<(P Who, int Due, bool Done)> Promises = [];
    public PlayerAction? LastAction;
    private readonly Dictionary<P, SocialStance> _asked = [];

    public static readonly ImmutableDictionary<string, (Place Place, Func<Game, Essence> Thing)> Things_ = new Dictionary<string, (Place, Func<Game, Essence>)>
    {
        ["粮仓"] = (Place.Logistics, g => g.Granary), ["台账"] = (Place.Logistics, g => g.Ledger), ["配给台公告"] = (Place.Logistics, g => g.Board[Place.Logistics]),
        ["中央厅公告"] = (Place.Hall, g => g.Board[Place.Hall]), ["中央基座"] = (Place.Hall, g => g.Base), ["速写本"] = (Place.DormS, g => g.SketchBook), ["碗"] = (Place.Clinic, g => g.Bowl), ["病床"] = (Place.Clinic, g => g.Bed),
        ["药柜"] = (Place.Clinic, g => g.MedCab), ["黑板"] = (Place.Lecture, g => g.Blackboard), ["石碑"] = (Place.Oath, g => g.Stele),
        ["压力表"] = (Place.Greenhouse, g => g.Gauge), ["名录库之门"] = (Place.ArchiveFront, g => g.ArchiveDoor), ["油库"] = (Place.DormS, g => g.OilStore),
        ["文契废纸篓"] = (Place.DormW, g => g.Litter[Place.DormW]), ["星仪废纸篓"] = (Place.DormS, g => g.Litter[Place.DormS]),
        ["后勤废纸篓"] = (Place.DormL, g => g.Litter[Place.DormL]), ["生活翼废纸篓"] = (Place.DormLife, g => g.Litter[Place.DormLife]), ["文契公告"] = (Place.DormW, g => g.Board[Place.DormW]),
        ["生活翼公告"] = (Place.DormLife, g => g.Board[Place.DormLife]), ["讲堂公告"] = (Place.Lecture, g => g.Board[Place.Lecture]),
        ["星仪公告"] = (Place.DormS, g => g.Board[Place.DormS]), ["后勤公告"] = (Place.DormL, g => g.Board[Place.DormL]),
    }.ToImmutableDictionary();

    /// <summary>同一区域里一并细看的痕迹物件（§12 第 7 条：查证成本）。</summary>
    public static readonly ImmutableDictionary<Place, string[]> AreaThings = new Dictionary<Place, string[]>
    {
        [Place.Logistics] = ["粮仓", "台账", "配给台公告"], [Place.Clinic] = ["碗", "病床", "药柜"], [Place.DormW] = ["文契废纸篓", "文契公告"],
        [Place.DormS] = ["星仪废纸篓", "油库", "速写本", "星仪公告"], [Place.DormL] = ["后勤废纸篓", "后勤公告"], [Place.DormLife] = ["生活翼废纸篓", "生活翼公告"],
        [Place.ArchiveFront] = ["名录库之门"], [Place.Lecture] = ["黑板", "讲堂公告"], [Place.Greenhouse] = ["压力表"], [Place.Oath] = ["石碑"], [Place.Hall] = ["中央基座", "中央厅公告"],
    }.ToImmutableDictionary();

    private Effect SayTo(Fx fx, Person who, string text, Effect request) => Fx.Seq(fx.Go(Where(who)), new Effect.Say(fx.R(who.Body), text, request));

    /// <summary>时段开头：侦探先找到要说话的人，把请求说出口（Effect.Say 携带 Request），对方再与自己的习惯一起决策。</summary>
    private void PlayerBegin(PlayerAction? act)
    {
        LastAction = act;
        _asked.Clear();
        DetHelpers.Clear();
        if (Slot == Slot.Afternoon && Get(Det.Body, T.Hungry) > 0 && act is not null)
        {
            Feed.Add("你饿得发虚，这个下午什么也做不了。");
            LastAction = null;
            return;
        }
        if (act is null) return;
        var fx = FxOf(Det);
        switch (act.Cmd)
        {
            case Cmd.Escort:
                Request(act.Who!.Value, $"跟我去{Names.Of(act.Place!.Value)}看看{act.Thing}。", fx.Escort(act.Place.Value, Things_[act.Thing!].Thing(this)));
                break;
            case Cmd.Ask:
                Request(act.Who!.Value, $"能不能请你{Names.Of(act.Job!.Value)}？", fx.Job(act.Job.Value, this[act.Who.Value]) is var e && act.Job is Job.Rest or Job.Sleep or Job.Work ? e : fx.Job(act.Job.Value));
                break;
            case Cmd.GiveKey:
                Request(act.Who!.Value, $"把粮仓钥匙交给{Names.Of(act.About!.Value)}，统一煮粥，行吗？",
                    Fx.Seq(Fx.Need(Fx.Mine(T.GranaryKey)), Fx.MeChange(T.GranaryKey, -1), fx.Change(this[act.About.Value].Body, T.GranaryKey, 1)));
                break;
            case Cmd.Greenhouse:
                foreach (var c in act.Crew) Request(c, "下午来温室，两人一组修水压。", fx.Job(Job.Greenhouse));
                break;
            case Cmd.Archive:
                foreach (var c in act.Crew) Request(c, "来名录库前厅，帮我试开那道门。", fx.Escort(Place.ArchiveFront, ArchiveDoor));
                break;
            case Cmd.Rub:
                if (act.Who is { } helper) Request(helper, "陪我去誓约庭拓那块碑。", fx.Escort(Place.Oath, Stele));
                break;
            case Cmd.Mediate when Day == 2 && Slot == Slot.Noon:
                var mora = Evaluate(P.Mora, "先按人头开粮，账我来对。");
                var ruan = Evaluate(P.Ruan, "先按人头开粮，账我来对。");
                Mediated = mora != SocialStance.Reject && ruan != SocialStance.Reject && (mora == SocialStance.Accept || ruan == SocialStance.Accept);
                Feed.Add(Mediated ? "茉拉与阮清越各退一步：先开粮，账按人头记。" : "你劝了，没人肯先松手。");
                if (Mediated) Witness(Det, "侦探调停了配给", 1, 1, People.Where(p => Where(p) == Place.Logistics || p.Id is P.Mora or P.Ruan), domain: Dom.Rations);
                break;
        }
        if (_asked.Count > 0) S.CTB.RunUntil(Now + 0.2, 1000);
    }

    private SocialStance Evaluate(P who, string text)
    {
        var p = this[who];
        Observation.Observe(p.Soul, new ObservationInput(text, ProjectionKind.Event)
        { Source = ObservationSource.Testimony, Existence = Truth.True, Time = Now, AddRelations = [(Relations.Subject, p.M[Det.Body])], Procedure = FxOf(p).Look(p.Body), Details = text });
        var req = p.World.Projections.Last(x => x.Kind == ProjectionKind.Event && x.Name == text);
        var stance = SocialSystem.Evaluate(p.World, p.Self, req).Stance;
        Observation.Observe(p.Soul, new ObservationInput(req.Name, req.Kind) { Target = req, Source = ObservationSource.Introspection, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Responded, 1) });
        Feed.Add($"{p.Name}：{Answer(p, stance)}");
        return stance;
    }

    /// <summary>当面请求：经 SimulationSession 提交侦探的 Say；对方的 SocialSystem 立即给出态度（侦探看得见）。协商 = 对方提条件，侦探答应，记一笔承诺。</summary>
    private void Request(P who, string text, Effect procedure)
    {
        var p = this[who];
        var fx = FxOf(Det);
        Det.Body.Get<SpatialProperty>()!.Space = SpaceOf(p.Body);
        Rt.Sync();
        var exec = S.Submit(Det.Soul, new ActionIntent(Det.Self, new Effect.Say(fx.R(p.Body), text, procedure), 0.01));
        S.CTB.RunUntil(Now + 0.05, 1000);
        var req = p.World.Projections.LastOrDefault(x => x.Kind == ProjectionKind.Event && x.Name == text && x.Procedure is not null);
        if (req is null) { Feed.Add($"{p.Name}不在能说话的地方。"); return; }
        var stance = SocialSystem.Evaluate(p.World, p.Self, req);

        _asked[who] = stance.Stance;
        _asks[(P.Det, who)] = RecordEvent(P.Det, $"请{p.Name}：“{text}”", "交谈", Where(Det), []);
        NoteFor(who, "态度", $"你请他：“{text}”他{(stance.Stance == SocialStance.Accept ? "答应了" : stance.Stance == SocialStance.Negotiate ? "要你先办件事" : "拒绝了")}");
        Feed.Add($"你对{p.Name}说：“{text}” {p.Name}：{Answer(p, stance.Stance)}");
    }

    private void Promise(Person p)
    {
        Observation.Observe(p.Soul, new ObservationInput($"侦探答应明天之前替{p.Name}办一件事", ProjectionKind.Event)
        {
            Source = ObservationSource.Testimony, Existence = Truth.True, Time = Now, Details = "承诺",
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, 1), AddRelations = [(Relations.Subject, p.M[Det.Body])]
        });
        Promises.Add((p.Id, Day + 1, false));
        Feed.Add($"（{p.Name}提了条件：明天之前，你得替{(p.Pro.Group == Group.Houqin ? "后勤" : "他们")}办一件事。你答应了。用“帮 {p.Name}”兑现。）");
    }

    private static string Answer(Person p, SocialStance s) => s switch
    {
        SocialStance.Accept => p.Id switch { P.Quer => "“行。”", P.Ward => "“依据情理，可以。”", P.Ruan => "“值得。”", P.Mora => "“说吧，快点。”", _ => "“好。”" },
        SocialStance.Negotiate => (p.Id switch { P.Ward => "“视条件而定。”", P.Ruan => "“代价是什么？”", _ => "“……那你得先替我做点事。”" }) + $"（他在等你先帮他一把：用“帮 {p.Name}”）",
        _ => p.Id switch { P.Quer => "“凭什么？”", P.Ward => "“不予采纳。”", P.Mora => "“没空。”", P.Veronica => "“理由不足。”", _ => "“不了。”" }
    };

    /// <summary>侦探自己的身体：做这一时段选定的事（与 NPC 一起在 CTB 上结算）。</summary>
    private void PlayerDo(PlayerAction? act)
    {
        if (Slot == Slot.Afternoon && Get(Det.Body, T.Hungry) > 0) return;
        var fx = FxOf(Det);
        if (Slot == Slot.Night)
        {
            if (act?.Cmd == Cmd.Stakeout) { SubmitDet(Fx.Seq(fx.Go(act.Place!.Value), Fx.Wait(NightLength - 2, fx.Look(Space[act.Place.Value]))), 0.5); Det.Asleep = false; }
            else { SubmitDet(fx.Job(Job.Sleep, Det), 0.5, Job.Sleep); }
            return;
        }
        if (act is null) { SubmitDet(fx.Job(Job.Rest, Det), 0.5, Job.Rest); return; }
        switch (act.Cmd)
        {
            case Cmd.Class: SubmitDet(fx.Job(Job.Class), 0.5, Job.Class); break;
            case Cmd.Collect: SubmitDet(fx.Job(Job.Collect), 0.8, Job.Collect); break;
            case Cmd.Inspect:
                var (pl, th) = Things_[act.Thing!];
                SubmitDet(Fx.Seq(fx.Go(pl), fx.Look(th(this))), 0.8);
                break;
            case Cmd.Escort: SubmitDet(fx.Escort(act.Place!.Value, Things_[act.Thing!].Thing(this)), 1.2); break;
            case Cmd.Greenhouse: SubmitDet(fx.Job(Job.Greenhouse), 1.0, Job.Greenhouse); break;
            case Cmd.Archive: SubmitDet(fx.Escort(Place.ArchiveFront, ArchiveDoor), 1.2); break;
            case Cmd.Rub: SubmitDet(fx.Escort(Place.Oath, Stele), 1.2); break;
            case Cmd.Help or Cmd.Tell or Cmd.TellRule or Cmd.Manipulate or Cmd.ShareFood or Cmd.AskAbout:
                if (act.Who is { } w) SubmitDet(Fx.Seq(fx.Go(Where(this[w])), fx.Look(this[w].Body)), 0.8);
                break;
            case Cmd.Post: SubmitDet(Fx.Seq(fx.Go(act.Place!.Value), fx.Look(Board[act.Place.Value])), 0.8); break;
            case Cmd.Mediate: SubmitDet(Fx.Seq(fx.Go(Place.Logistics), fx.Look(Desk)), 0.5); break;
            case Cmd.Apologize: SubmitDet(Fx.Seq(fx.Go(Place.Hall), fx.Look(Board[Place.Hall])), 0.5); break;
            case Cmd.Confront or Cmd.Reopen: break;
            default: SubmitDet(fx.Job(Job.Rest, Det), 0.5, Job.Rest); break;
        }
    }

    /// <summary>时段末：侦探看到自己行动的结果；只写看见了什么、听见了什么。</summary>
    private void PlayerEnd(PlayerAction? act)
    {
        // 违约检查
        for (var i = 0; i < Promises.Count; i++)
        {
            var (who, due, done) = Promises[i];
            if (!done && Day > due || !done && Day == due && Slot == Slot.Night)
            {
                Promises[i] = (who, due, true);
                Witness(Det, $"侦探答应{Names.Of(who)}的事没有做到", -2, 0, [this[who]]);
                Feed.Add($"你答应{Names.Of(who)}的事，没有做到。");
            }
        }
        if (act is null || LastAction is null) { AfterSlot(null); return; }
        if (act.Cmd == Cmd.Manipulate && act.Who is { } mw && act.About is { } ma)
            Touch(this[ma].Body, T.Suspect, RecordEvent(P.Det, $"对{Names.Of(mw)}片面告知{Names.Of(ma)}的事", "交谈", Where(Det), []), P.Det);
        if (act.Cmd == Cmd.ShareFood && act.Who is { } sw)
            Touch(this[sw].Body, T.Fed, RecordEvent(P.Det, $"把口粮分给{Names.Of(sw)}", "递物", Where(Det), []), P.Det);
        var here = Where(Det);
        switch (act.Cmd)
        {
            case Cmd.Collect:
                Feed.Add(Get(Det.Body, T.Fed) > 0 ? $"你领到了今天的口粮{(_distributor is { } dd ? $"（开粮的是{dd.Name}{(Get(Stove, T.Cooked) > 0 || _cookedThisSlot ? "，灶上煮着粥" : "")}）" : "")}。" : "你在配给台等到收摊，没有开粮。");
                break;
            case Cmd.Class:
                if (here == Place.Lecture && act.Topic >= 0)
                {
                    var q = $"你问：“{Topics[act.Topic]}——{(act.Precise ? "请精确地说" : "那是怎么回事")}？”";
                    var a = AskDemon(act.Topic, act.Precise, out var praised, out var refused);
                    Feed.Add(q);
                    Feed.Add($"恶魔：“{a}”");
                    var present = People.Where(p => p != Det && Where(p) == Place.Lecture).ToArray();
                    if (praised)
                    {
                        Feed.Add("恶魔当众说：“问得好。”");
                        // 评分是双刃剑：信恶魔或爱学的人更信你，戒备恶魔的人怀疑你是它的宠儿。
                        Witness(Det, "侦探的提问被恶魔当众称赞", 0, 1, present.Where(p => p.Pro.DemonPrejudice >= 0), domain: Dom.Archive);
                        Witness(Det, "侦探被恶魔当众称赞——它的宠儿？", -1, 0, present.Where(p => p.Pro.DemonPrejudice < 0));
                    }
                    if (refused) Feed.Add("（它援引了第七条。拒答本身也是一种回答。）");
                    if (act.Topic == 2 && act.Precise && WarnedPrecisely) NewsFrom(Det, NewsKind.Fact, "温室：先在阀门泄压，指针回落到红线下再开顶窗");
                    if (act.Topic is 0 or 1 && act.Precise && (act.Topic == 1 || Det.Lessons.ContainsKey(14)))
                        foreach (var p in present) { p.KnowsHookCost = true; Tell(p, Warning1, Channel.Public); }
                    // 当众逐字问出“取回”的真相：它自己说了“接通之后，我离诸位更近一些”。在场的人对它那条“凡学员皆可取回己物”多了一分疑心。
                    if (act.Topic == 0 && act.Precise && Det.Lessons.ContainsKey(14))
                    {
                        foreach (var p in present) { p.DemonDoubt = Math.Min(2, p.DemonDoubt + 1); NoteFor(p.Id, "恶魔", "听到恶魔亲口说“接通之后，我离诸位更近一些”时，他皱了眉"); }
                        var word = NewsFrom(Det, NewsKind.DemonWord, "恶魔在课上亲口说：取回即重新接通，接通之后它离我们更近一些");
                        foreach (var p in present) p.News[word.Id] = new(P.Det, 1, ObservationSource.Perception, Day, word.Text);
                    }
                }
                break;
            case Cmd.Inspect:
            case Cmd.Escort:
            case Cmd.Archive when false:
                var thing = Things_[act.Thing!].Thing(this);
                var area = Things_[act.Thing!].Place;
                if (here == area)
                {
                    // 同一区域的痕迹一并细看。
                    var all = new List<Essence> { thing };
                    foreach (var name in AreaThings.GetValueOrDefault(area) ?? []) { var e = Things_[name].Thing(this); if (!all.Contains(e)) all.Add(e); }
                    foreach (var e in all) { if (Day >= 3) DetLooked.Add(e); DetSaw(e); }
                    if (all.Contains(Granary)) RevealLatch(Det); // 凑近细看，门闩的松紧与工具痕才看得出
                    Feed.Add($"你细看了{Names.Of(here)}：");
                    foreach (var e in all) Feed.Add("  · " + Describe(e));
                    if (area == Place.ArchiveFront && DawnTraces.TryGetValue(Day - 1, out var tr) && tr.FirstOrDefault(t => t.Key == "前厅脚印") is { } fp) Feed.Add("  · " + fp.Text + "。");
                }
                else Feed.Add($"你没能走到{Names.Of(area)}。");
                if (act.Cmd == Cmd.Escort)
                {
                    var who = this[act.Who!.Value];
                    var came = DetHelpers.Contains(who.Id);
                    if (thing == Base) DetShown.Add(who.Id);
                    DetEscortTried.Add((who.Id, act.Thing!));
                    if (who.Id == P.Ward) WardEscorts += came ? 10 : 1;
                    if (came && thing == Base && Get(Base, T.Crack) > 0) Feed.Add($"你把那道裂纹指给{who.Name}看。他盯着看了很久。");
                    var saw = who.World.History.Any(h => h.Source == ObservationSource.Perception && ReferenceEquals(h.Target, who.M[thing]) && h.Time >= DayStart(Day));
                    Feed.Add(came ? $"{who.Name}跟你一起看了。" : saw && thing == Base ? $"{who.Name}没答应同行。他人就在{Names.Of(act.Place!.Value)}，可那道裂纹要凑近细看才看得出，你没能指给他。" : saw ? $"{who.Name}没答应同行，不过他本来就在{Names.Of(act.Place!.Value)}，也看见了。" : $"{who.Name}没有跟来（{(_asked.GetValueOrDefault(who.Id) == SocialStance.Accept ? "答应了，但另有更要紧的事" : "没答应")}）。");
                }
                break;
            case Cmd.Ask:
            {
                var who = this[act.Who!.Value];
                var did = DetHelpers.Contains(who.Id) || Get(who.Body, T.Did(act.Job!.Value)) > 0;
                Feed.Add(did ? $"{who.Name}照你说的做了：{Names.Of(act.Job.Value)}。" : $"{who.Name}没有去做（{(_asked.GetValueOrDefault(who.Id) == SocialStance.Accept ? "答应了，但另有更要紧的事" : "没答应")}）。");
                if (did) Witness(Det, $"侦探请{who.Name}{Names.Of(act.Job.Value)}，事办成了", 0, 0, [who], spread: false);
                break;
            }
            case Cmd.GiveKey:
            {
                var holder = this[act.Who!.Value];
                if (Get(holder.Body, T.GranaryKey) == 0 && Get(this[act.About!.Value].Body, T.GranaryKey) > 0)
                {
                    Feed.Add($"{holder.Name}把粮仓钥匙交给了{Names.Of(act.About.Value)}。");
                    foreach (var p in new[] { holder, this[act.About.Value] })
                        Observation.Observe(p.Soul, new ObservationInput(p.Self.Name, p.Self.Kind)
                        { Target = p.Self, Source = ObservationSource.Introspection, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(T.GranaryKey, Get(p.Body, T.GranaryKey)) });
                    var shen = this[act.About.Value];
                }
                else Feed.Add($"{holder.Name}没有交出钥匙。");
                break;
            }
            case Cmd.Greenhouse:
                Feed.Add($"温室：{string.Join("、", People.Where(p => Where(p) == Place.Greenhouse).Select(p => p.Name))}在。{(GreenhouseRepaired ? "水压稳住了，温室修好了。" : $"阀门修了大约{Math.Min(9, GreenhouseProgress * 10 / GreenhouseNeed)}成。")}");
                break;
            case Cmd.Archive:
            {
                var crew = People.Where(p => DetHelpers.Contains(p.Id)).ToArray();
                var ok = Staff(crew, out var report);
                ArchiveTested |= ok;
                Feed.Add($"名录库前厅：{(crew.Length == 0 ? "没有人来" : string.Join("、", crew.Select(c => c.Name)) + "来了")}。{report.TrimEnd('。')}。{(ok ? "门里传出配重落位的声音，第一道门开了一线，又被你们合上。" : "门没开。")}");
                if (crew.Length > 0) Witness(Det, ok ? "侦探主持开了名录库的机关" : "侦探主持的机关没开成", ok ? 1 : 0, ok ? 2 : -1, crew, domain: Dom.Archive);
                if (ok) foreach (var c in crew) c.Witnessed.Add("machine");
                break;
            }
            case Cmd.Rub:
                if (here == Place.Oath)
                {
                    var helper = act.Who is { } h && Where(this[h]) == Place.Oath ? this[h] : null;
                    if (helper is not null && helper.Pro.Id == P.Gu || Get(Stele, T.Rubbed) > 0)
                    {
                        SteleRubbed = true;
                        Feed.Add("顾平章帮你拓下了石碑。风化最重的两条，辨读出来：第九条——经其余十五名学员一致同意，或封印已无法完成时，学员可择定“尽忘归乡”……；第十条——担保耗尽，或封印的必需之物永久失去时，契约以放逐终结……");
                        NewsFrom(Det, NewsKind.Fact, "誓约庭石碑上有恶魔从没说过的第九、第十条");
                    }
                    else
                    {
                        Add(Stele, T.Rubbed, 1);
                        Feed.Add("你拓了一遍。风化太重，第九、第十条只辨出零星几个字：“……十五……归乡……放逐……”。也许懂碑的人能读出来；或者再拓一遍。");
                    }
                }
                break;
            case Cmd.Help:
            {
                var who = act.Who!.Value;
                var idx = Promises.FindIndex(x => x.Who == who && !x.Done);
                if (idx >= 0)
                {
                    Promises[idx] = (who, Promises[idx].Due, true);
                    Witness(Det, $"侦探兑现了对{Names.Of(who)}的承诺", 1, 0, People.Where(p => Where(p) == Where(this[who])));
                    if (Repairs.Remove(who)) { Witness(Det, "侦探认了错，也把答应的事办了", 1, 0, [this[who]], spread: true); Feed.Add($"{Names.Of(who)}点了点头：“行，这事算过去了。”"); }
                    Feed.Add($"你替{Names.Of(who)}办完了答应的事。");
                }
                else
                {
                    Witness(Det, $"侦探来帮{Names.Of(who)}干活", 1, 0, [this[who]], spread: false);
                    DetHelped.Add(who);
                    Feed.Add($"你帮{Names.Of(who)}干了一个时段的活。");
                }
                break;
            }
            case Cmd.Tell:
            {
                var who = this[act.Who!.Value];
                if (Det.News.TryGetValue(act.NewsId, out var heard)) { Receive(Det, who, AllNews[act.NewsId - 1], heard, Channel.Face); DetToldNews.Add((who.Id, act.NewsId)); Feed.Add($"你把「{heard.Text}」告诉了{who.Name}。"); }
                break;
            }
            case Cmd.TellRule:
            {
                var who = this[act.Who!.Value];
                var spec = act.Spec ?? CardSpec(act.Card!, P.Det);
                Det.Acc[spec] = Truth.True;
                var v = Tell(who, spec);
                _transmit[(spec.Id, who.Id)] = RecordEvent(P.Det, $"当面对{who.Name}讲「{Short(spec)}」", "交谈", Where(Det), []);
                DetToldFace.Add((spec, who.Id, v == Truth.True, Day));
                _toDisclose.RemoveAll(d => d.Who == who.Id && d.Spec == spec);
                Feed.Add($"你当面对{who.Name}说了{LabelOf(spec)}「{spec.Text}」。{who.Name}{(v == Truth.True ? "点了头" : "没有接受")}：{Voice(who, spec, v)}");
                NoteFor(who.Id, "规矩", $"{(v == Truth.True ? "当面点了头" : "当面没接受")}：{LabelOf(spec)}「{Short(spec)}」");
                if (spec.Manipulative) Manipulations.Add(new() { Target = who.Id, About = P.Quer, Means = "针对性的规则", Day = Day });
                break;
            }
            case Cmd.Apologize:
            {
                // 公开认错（§13 第 1 条）：当众承认说过的片面话；被你说过的人提一件要你办的事，兑现了才挽回一点“为人”（回不到揭穿前）。
                var present = People.Where(p => p != Det && Where(p) == here).ToArray();
                foreach (var m in Manipulations.Where(m => m.Exposed && !m.Apologized).ToArray())
                {
                    m.Apologized = true;
                    var target = this[m.Target];
                    Feed.Add($"你在中央厅当众说：之前对{target.Name}说的，只说了一半。{(present.Contains(target) ? $"{target.Name}看了你很久：“那你替我办件事，办到了再说。”" : $"{target.Name}不在场，你托人带了话。")}");
                    Promises.Add((target.Id, Day + 1, false));
                    Repairs.Add(target.Id);
                    Witness(Det, "侦探当众认了错", 0, 0, present.Append(target), spread: true);
                    NoteFor(target.Id, "其他", "你当众认了错；他要你明天之前替他办一件事");
                }
                break;
            }
            case Cmd.Post:
            {
                var spec = act.Spec ?? CardSpec(act.Card!, P.Det);
                Det.Acc[spec] = Truth.True;
                Post(spec, act.Place!.Value);
                Feed.Add($"你在{Names.Of(act.Place.Value)}贴出{LabelOf(spec)}「{spec.Text}」");
                if (spec.Manipulative) foreach (var p in Npcs) Manipulations.Add(new() { Target = p.Id, About = P.Quer, Means = "张贴针对性的规则", Day = Day });
                break;
            }
            case Cmd.Manipulate:
            {
                var who = this[act.Who!.Value];
                var about = this[act.About!.Value];
                var text = $"{about.Name}昨夜确实去过后勤院";
                var item = NewsFrom(Det, NewsKind.Rumor, text, about: about, trust: -2, manipulation: true);
                Receive(Det, who, item, Det.News[item.Id], Channel.Face);
                Manipulations.Add(new() { Target = who.Id, About = about.Id, Means = "片面告知：" + text, Day = Day });
                Feed.Add($"你对{who.Name}说：“{text}。”——每个字都是真的。");
                break;
            }
            case Cmd.ShareFood:
            {
                var who = this[act.Who!.Value];
                if (Get(Det.Body, T.Fed) > 0)
                {
                    Add(Det.Body, T.Fed, -1); Add(who.Body, T.Fed, 1);
                    Witness(Det, $"侦探把自己的口粮分给了{who.Name}", 2, 0, People.Where(p => Where(p) == Where(who)));
                    Feed.Add($"你把自己那份给了{who.Name}。");
                }
                else Feed.Add("你自己今天还没领到口粮。");
                break;
            }
            case Cmd.AskAbout:
            {
                var who = this[act.Who!.Value];
                DetAsked.Add(who.Id);
                AskedOn.Add((Day, who.Id));
                Feed.Add($"{who.Name}说：{Testimony(who)}");
                break;
            }
        }
        if (Slot == Slot.Night && act.Cmd == Cmd.Stakeout)
        {
            // 守夜只给身影（身量、来去方向）与声音，不给姓名与动作（§11 第 2 条）。天黑，看不清脸。
            Stakeouts.Add((Day, act.Place!.Value));
            Feed.Add($"你在{Names.Of(act.Place.Value)}守了一夜：");
            foreach (var line in StakeoutReport(Day, act.Place.Value)) Feed.Add("  " + line);
        }
        AfterSlot(act.Who);
    }

    private void ExtraHabit(Person p, Job job)
    {
        var fx = FxOf(p);
        var rules = p.Habit.Rules.Add(new Rule(Fx.All(fx.AtAny([Slot.Noon, Slot.Evening]), fx.NotDone(job), Fx.Mine(T.GranaryKey)), RuleMode.Prefer, p.JobFx[job], 5, Truth.True));
        Observation.Observe(p.Soul, new ObservationInput(p.Habit.Name, p.Habit.Kind) { Target = p.Habit, Source = ObservationSource.Introspection, Time = Now, Rules = rules });
    }

    /// <summary>当面问一个人：他只报告自己的感知与职业判读。</summary>
    public string Testimony(Person p) => Day < 3 || p.Id is not (P.Bertrand or P.Margaret or P.Mora or P.Quer or P.Ruan or P.Ward) ? Chat(p) : p.Id switch
    {
        P.Bertrand => "“报告：" + string.Join("；", Sightings.Where(s => s.Witness == P.Bertrand && (s.Day == Day - 1 || s.Day == 2)).GroupBy(s => (s.Day, s.At))
            .Select(g => $"第{g.Key.Day}日夜，{Names.Of(g.Key.At)}有{string.Join("、", g.GroupBy(s => s.Build).Select(b => $"{b.Count()}个{BuildText(b.Key).TrimEnd('的')}身影"))}").DefaultIfEmpty("夜里没见异常")) + "。看不清是谁。”",
        P.Margaret => NightTruth.Any(t => t.Contains("雀儿 修粮仓门闩")) && p.World.Projections.Any(x => x.Relations.Any(r => r.Relation == Fx.Asked))
            ? "“粮仓门闩松了，前天夜里我在后勤院碰见雀儿，请她修的。她修得很干净。……有人说她偷东西？她当时饿着肚子在修门。”"
            : p.World.Projections.Any(x => x.Relations.Any(r => r.Relation == Fx.Asked)) ? "“门闩松了，我请人修，没人搭理。”" : "“后勤院的门闩松着，没人管。”",
        P.Mora => NightTruth.Any(t => t.StartsWith("D2 茉拉 夜里为病人取")) ? "“我夜里给莫兰取了一份。板上写着的。今早……我还没顾上去说。”" : "“我一直在诊疗所。”",
        P.Quer => NightTruth.Any(t => t.StartsWith("D2 雀儿 夜里自取")) ? (TrustIn(p, Det) >= 2 ? "“……我拿了一份。茉拉的板上写着急需可以自取。门闩是我修的。”" : "“关你什么事。”") : "“我那晚饿着睡的。”",
        P.Ruan => $"“账面与存量差 {Get(Ledger, T.Unrecorded)}。我只记得到配给台发出去的。”",
        P.Ward => Accused is not null ? "“依据台账与所见，事实清楚。”" : "“无记录即违规。仅此而已。”",
        _ => "“我那晚睡着了。”"
    };

    /// <summary>平常的交谈：他最近听说的两件事（他自己的说法，可能走样），以及他对侦探的态度。</summary>
    private string Chat(Person p)
    {
        var heard = p.News.OrderByDescending(n => n.Key).Where(n => AllNews[n.Key - 1].Kind != NewsKind.Lesson).Select(n => n.Value).Where(h => h.Day >= Day - 1).Take(2).Select(h => h.Hops == 0 ? h.Text : $"{h.Text}（听{Names.Of(h.From)}说的）").ToArray();
        var t = TrustIn(p, Det);
        var mood = t >= 3 ? "他说话不避着你。" : t >= 1 ? "他还算客气。" : t >= 0 ? "他话不多。" : "他明显不想跟你多说。";
        return (heard.Length == 0 ? "“没什么新鲜事。”" : "“" + string.Join("；", heard) + "。”") + mood;
    }

    public string Describe(Essence e)
    {
        if (e == Granary) return $"存粮还剩 {Get(Granary, T.Ration)} 份；门闩{(Get(Granary, T.LatchLoose) > 0 ? "松动，一推就开" : Get(Granary, T.ToolMarks) > 0 ? "有新的工具痕，已经紧固" : "完好")}。";
        if (e == Ledger) return $"台账：配给台累计发出 {Get(Ledger, T.Handed)} 份；夜间补记 {Get(Ledger, T.NightRecorded)} 份；阮清越的清点字迹：无记录取用×{Get(Ledger, T.Unrecorded)}。";
        if (e == Bowl) return Get(Bowl, T.Residue) > 0 ? $"莫兰床边一只空碗，碗底有粥渍{(Get(Bowl, T.Porridge) > 0 ? "；另有一碗粥还温着" : "")}。" : "碗是干净的。";
        if (e == Bed) return Get(this[P.Moran].Body, T.Sick) > 0 ? "莫兰躺着，额头很烫。" : "病床空着。";
        if (e == Blackboard) return Get(Blackboard, T.Chalk) > 0 ? $"黑板上有 {Get(Blackboard, T.Chalk)} 堂课的粉笔字；讲台前的灰上有窄底皮鞋印。" : "黑板是干净的。";
        if (e == Stele) return "风化严重。第一至第八条还认得；最后两条只剩笔画的影子，须拓印辨读。";
        if (e == Gauge) return $"压力表指针：{(Get(Gauge, T.Pressure) >= 2 ? "过了红线" : "红线以下")}。";
        if (e == SketchBook)
        {
            foreach (var (n, _) in Sketches) DetSketchSeen.Add(n);
            return Sketches.Count == 0 ? "莫兰的速写本里只有几张颜色练习。" : "莫兰的速写本：" + string.Join("　", Sketches.Select(s => s.Text)) + "。";
        }
        if (e == Base) return Get(Base, T.Crack) > 0 ? "中央基座上有一道新裂纹，像是从里面撑开的。" : "中央基座完好，石面上刻着契约的前八条。";
        if (e == ArchiveDoor) return "三道机关：配重室（要力气）、锁芯（要细手）、传声中继（要信号）。三处须同时施力。";
        if (e == MedCab) return $"药柜：药品 {Get(MedCab, T.Medicine)}。";
        if (e == OilStore) return $"油库：灯油 {Get(OilStore, T.Oil)}。";
        if (Litter.ContainsValue(e)) return Get(e, T.Crumbs) > 0 ? "废纸篓里有一份口粮的包纸和饼屑。" : Get(e, T.Emptied) > 0 ? "废纸篓刚被人倒空过；篓底还粘着几粒饼屑和一角口粮包纸。" : "废纸篓里只有废纸。";
        if (Board.ContainsValue(e))
        {
            var place = Board.First(b => b.Value == e).Key;
            var specs = Specs.Where(s => s.PostedAt.Contains(place) && s != RuanLedger).ToArray();
            return specs.Length == 0 ? "板上什么也没贴。" : "板上贴着：" + string.Join(" ", specs.Select(s => $"「{s.Text}」（{(s.Author is { } a ? Names.Of(a) : "恶魔")}）"));
        }
        return e.TrueName;
    }

    // ------------------------------------------------------------ 预判 --

    /// <summary>每日一次，当众断言某人今夜会去某处。在场者记下；夜间推演判定对错，次晨在场者的“见识”随之升降。</summary>
    // ----------------------------------------------------------- 赌约 --

    public readonly List<(Wager W, bool? Right)> Wagers = [];
    /// <summary>认错之后还等着兑现的人。</summary>
    public readonly HashSet<P> Repairs = [];
    /// <summary>侦探翻过速写的那几夜（速写上画的是谁，可作重演的已知）。</summary>
    public readonly HashSet<int> DetSketchSeen = [];
    /// <summary>侦探带去亲眼看过基座裂纹的人（侦探自己做的事，机器人可用）。</summary>
    public readonly HashSet<P> DetShown = [];
    /// <summary>侦探请谁同去看过什么（不论来没来）。</summary>
    public readonly HashSet<(P, string)> DetEscortTried = [];
    public readonly HashSet<P> DetHelped = [];
    public int WardEscorts;
    /// <summary>侦探转告过谁哪条消息（机器人用：只记侦探自己做过的事）。</summary>
    public readonly HashSet<(P Who, int News)> DetToldNews = [];

    /// <summary>
    /// 与在场的某一个人约定：“今晚某人会去某处；若我说中，你采纳我的那条规则”（§11 第 11 条）。不占时段，每日一次。
    /// 说中：他对侦探“判断”的看法上升，并把那条规则当真考虑（加重，但仍受底线约束）；说错：判断下降。
    /// </summary>
    public string MakeWager(P with, P who, Place where, int? spec)
    {
        if (Wagers.Any(w => w.W.Day == Day)) return "今天已经和人打过赌了。";
        if (Where(this[with]) != Where(Det)) return $"{Names.Of(with)}不在你身边。";
        var w = new Wager(Day, with, who, where, spec);
        Wagers.Add((w, null));
        FreeScript.Add((SlotIndex, w));
        var rule = spec is { } id ? Specs.FirstOrDefault(s => s.Id == id) : null;
        NoteFor(with, "赌约", $"你对他说：今晚{Names.Of(who)}会去{Names.Of(where)}{(rule is not null ? $"；说中了，他就认{LabelOf(rule)}「{Short(rule)}」" : "")}");
        return $"你对{Names.Of(with)}说：“今晚，{Names.Of(who)}会去{Names.Of(where)}。{(rule is not null ? $"我若说中，你就认{LabelOf(rule)}「{rule.Text}」。" : "")}”{Names.Of(with)}：{(TrustIn(this[with], Det) >= 1 ? "“好，明早见分晓。”" : "“……随你。”")}";
    }

    private void SettleWagers()
    {
        for (var i = 0; i < Wagers.Count; i++)
        {
            var (w, right) = Wagers[i];
            if (w.Day != Day || right is not null) continue;
            var ok = this[w.Who].NightVisited.Contains(w.Where);
            Wagers[i] = (w, ok);
            var with = this[w.With];
            var rule = w.Spec is { } id ? Specs.FirstOrDefault(s => s.Id == id) : null;
            Witness(Det, ok ? $"侦探和我打赌{Names.Of(w.Who)}夜里会去{Names.Of(w.Where)}，他说中了" : $"侦探和我打赌{Names.Of(w.Who)}会去{Names.Of(w.Where)}，没说中", 0, ok ? 2 : -1, [with],
                domain: rule?.Domain ?? Dom.General);
            if (ok && rule is not null)
            {
                with.Wagered.Add(rule);
                var v = Tell(with, rule, Channel.Face);
                Feed.Add($"（赌约：{Names.Of(w.Who)}昨夜确实去了{Names.Of(w.Where)}。{with.Name}{(v == Truth.True ? "照约认了你的规则" : "认了你说得准，可那条规则他还是不认：" + Voice(with, rule, v))}）");
            }
            else Feed.Add($"（赌约：{Names.Of(w.Who)}昨夜{(ok ? "确实" : "没有")}去{Names.Of(w.Where)}。{with.Name}{(ok ? "点了点头" : "看了你一眼")}。）");
        }
    }

    public string Predict(P who, Place where)
    {
        if (Predictions.Any(p => p.Day == Day)) return "今天已经预判过了。";
        Predictions.Add((Day, who, where, null));
        _predictionWitnesses = People.Where(p => p != Det && Where(p) == Where(Det)).ToArray();
        return $"你当众说：“今晚，{Names.Of(who)}会去{Names.Of(where)}。”听见的人：{(_predictionWitnesses.Length == 0 ? "没有人" : string.Join("、", _predictionWitnesses.Select(p => p.Name)))}。";
    }
    private Person[] _predictionWitnesses = [];

    private void SettlePrediction()
    {
        var i = Predictions.FindIndex(p => p.Day == Day && p.Right is null);
        if (i < 0) return;
        var (d, who, where, _) = Predictions[i];
        var right = this[who].NightVisited.Contains(where);
        Predictions[i] = (d, who, where, right);
        Witness(Det, right ? $"侦探昨天预判{Names.Of(who)}夜里会去{Names.Of(where)}，说中了" : $"侦探昨天预判{Names.Of(who)}会去{Names.Of(where)}，没说中", 0, right ? 2 : -1, _predictionWitnesses);
        Feed.Add($"（预判：{Names.Of(who)}{(right ? "确实" : "没有")}去{Names.Of(where)}。）");
    }
}
