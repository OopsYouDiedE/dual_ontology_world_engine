using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public enum Cmd { Stay, Rest, Class, Collect, Inspect, Escort, Ask, GiveKey, Tell, Post, TellRule, Greenhouse, Archive, Help, Mediate, Stakeout, Sleep, Rub, ShareFood, Manipulate, Confront, AskAbout, Reopen, Apologize, EatPantry }

/// <summary>侦探一个时段的行动。每个时段只能做一件事（第三部分 §4）。</summary>
public sealed record PlayerAction(Cmd Cmd)
{
    public string? Wording { get; init; }
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
        Cmd.TellRule => Chapter2Design.Text("social.tell_description", ("name", Names.Of(Who!.Value)), ("rule", Spec?.Text ?? Card?.Text ?? "")),
        _ => Cmd.ToString()
    };
}

public sealed partial class Game
{
    public readonly List<(int Day, P Who, Place Where, bool? Right)> Predictions = [];
    public readonly List<(P Who, int Due, bool Done)> Promises = [];
    public PlayerAction? LastAction;
    private readonly Dictionary<P, SocialStance> _asked = [];

    public IReadOnlyDictionary<string, (Place Place, Essence Thing)> Things_ { get; private set; } =
        new Dictionary<string, (Place, Essence)>();
    public IReadOnlyDictionary<Place, string[]> AreaThings { get; private set; } =
        new Dictionary<Place, string[]>();
    private IReadOnlyDictionary<Essence, string> _notebookImages = new Dictionary<Essence, string>();
    public string NotebookImage(Essence entity) => _notebookImages.TryGetValue(entity, out var key) ? key
        : throw new InvalidDataException("Missing notebook image binding: " + entity.TrueName);

    internal void BindInvestigation(string script)
    {
        var authored = DualOntologyWorldEngine.Story.WorldScriptDocument.Parse(script, allowChapterAdapter: true).Single();
        var declarations = authored.Entities.ToDictionary(Chapter2Bindings.Id, StringComparer.Ordinal);
        using var document = System.Text.Json.JsonDocument.Parse(script);
        var presentation = document.RootElement.GetProperty("chapters")[0].GetProperty("design").GetProperty("presentation");
        var images = new Dictionary<Essence, string>();
        foreach (var item in presentation.GetProperty("notebook_images").EnumerateObject())
        {
            if (!declarations.TryGetValue(item.Name, out var declaration) || declaration.Kind != "object"
                || item.Value.ValueKind != System.Text.Json.JsonValueKind.String)
                throw new InvalidDataException("Invalid notebook image binding: " + item.Name);
            images.Add(Chapter2Bindings.Resolve(this, declaration), item.Value.GetString()!);
        }
        foreach (var board in Board.Values)
            if (!images.ContainsKey(board)) throw new InvalidDataException("Missing notebook image binding: " + board.TrueName);
        var entries = presentation.GetProperty("investigation").EnumerateObject().Select(item =>
        {
            if (!declarations.TryGetValue(item.Name, out var declaration) || declaration.Kind != "object")
                throw new InvalidDataException("Missing investigation entity: " + item.Name);
            var entity = Chapter2Bindings.Resolve(this, declaration);
            var space = entity.Get<SpatialProperty>()?.Space
                ?? throw new InvalidDataException("Investigation entity has no physical space: " + item.Name);
            var label = item.Value.GetProperty("label").GetString()!;
            if (string.IsNullOrWhiteSpace(label)) throw new InvalidDataException("Missing investigation label: " + item.Name);
            return (Label: label, Place: PlaceOf[space], Thing: entity, Order: item.Value.GetProperty("order").GetInt32());
        }).ToArray();
        Things_ = entries.ToDictionary(item => item.Label, item => (item.Place, item.Thing), StringComparer.Ordinal);
        AreaThings = entries.GroupBy(item => item.Place).ToDictionary(group => group.Key,
            group => group.OrderBy(item => item.Order).Select(item => item.Label).ToArray());
        _notebookImages = images;
        foreach (var (key, thing) in Things_) Chapter.Place(key, thing.Place.ToString());
    }

    private Effect SayTo(Fx fx, Person who, string text, Effect request) => Fx.Seq(fx.Go(Where(who)), new Effect.Say(fx.R(who.Body), text, request));

    /// <summary>时段开头：侦探先找到要说话的人，把请求说出口（Effect.Say 携带 Request），对方再与自己的习惯一起决策。</summary>
    // STORY-HARDCODE[CH2-055][logic][production]: 处理玩家介入后本案指定人物请求及证据相关分支。
    private void PlayerBegin(PlayerAction? act)
    {
        LastAction = act;
        if (ReferenceEquals(_preparedAction, act) && act is not null) { _preparedAction = null; DetHelpers.Clear(); return; }
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
                Request(act.Who!.Value, X("social.escort", ("place", Names.Of(act.Place!.Value)), ("thing", act.Thing)), fx.Escort(act.Place.Value, Things_[act.Thing!].Thing), Compatibility(this[act.Who.Value], act));
                break;
            case Cmd.Ask:
                var recipient = this[act.Who!.Value];
                var procedure = RequestProcedure(recipient, act.Job!.Value);
                if (procedure is null) { _asked[recipient.Id] = SocialStance.Reject; Feed.Add(Answer(recipient, SocialStance.Reject)); break; }
                Request(recipient.Id, X("social.request", ("job", Names.Of(act.Job.Value))), procedure, Compatibility(recipient, act));
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
            case Cmd.Mediate when Day == RationFirstDay && Slot == Slot.Noon:
                HostMediate(act.Wording ?? throw new PlayerError("Missing mediation wording."));
                break;
        }
        if (_asked.Count > 0) S.CTB.RunUntil(Now + 0.2, 1000);
    }

    /// <summary>当面请求：经 SimulationSession 提交侦探的 Say；对方的 SocialSystem 立即给出态度（侦探看得见）。协商 = 对方提条件，侦探答应，记一笔承诺。</summary>
    private void Request(P who, string text, Effect procedure, int compatibility = 0)
    {
        var p = this[who];
        var fx = FxOf(Det);
        Det.Body.Get<SpatialProperty>()!.Space = SpaceOf(p.Body);
        Rt.Sync();
        var exec = S.Submit(Det.Soul, new ActionIntent(Det.Self, new Effect.Say(fx.R(p.Body), text, procedure), 0.01));
        S.CTB.RunUntil(Now + 0.05, 1000);
        var req = p.World.Projections.LastOrDefault(x => x.Kind == ProjectionKind.Event && x.Name == text && x.Procedure is not null);
        if (req is null) { Feed.Add(X("social.absent", ("name", p.Name))); return; }
        Observation.Observe(p.Soul, new ObservationInput(req.Name, req.Kind)
        {
            Target = req, Source = ObservationSource.Testimony, Time = Now,
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(SocialSystem.RequestCompatibility, compatibility)
        });
        var stance = SocialSystem.Evaluate(p.World, p.Self, req);

        _asked[who] = stance.Stance;
        _asks[(P.Det, who)] = RecordEvent(P.Det, X("social.request_log", ("name", p.Name), ("text", text)), Names.Of(Job.Socialize), Where(Det), []);
        NoteFor(who, X("social.note_kind"), X("social.request_note", ("he", p.Pro.Pronoun), ("text", text), ("result", X("social." + (stance.Stance == SocialStance.Accept ? "accepted" : stance.Stance == SocialStance.Negotiate ? "negotiated" : "rejected")))));
        Feed.Add(X("social.request_feed", ("name", p.Name), ("text", text), ("response", Answer(p, stance.Stance))));
    }

    private void Promise(Person p)
    {
        Observation.Observe(p.Soul, new ObservationInput($"侦探答应明天之前替{p.Name}办一件事", ProjectionKind.Event)
        {
            Source = ObservationSource.Testimony, Existence = Truth.True, Time = Now, Details = "承诺",
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, 1), AddRelations = [(Relations.Subject, p.M[Det.Body])]
        });
        Promises.Add((p.Id, Day + 1, false));
        Feed.Add($"（{p.Name}提了条件：明天之前，你得替{(p.Pro.Group == Group.Houqin ? "后勤" : "他们")}办一件事。你答应了。找{p.Pro.Pronoun}“帮{p.Pro.Pronoun}一把”即算兑现。）");
    }

    private static string Answer(Person p, SocialStance stance)
    {
        var key = "social." + p.Id + "." + stance;
        var response = Chapter2Design.HasText(key) ? X(key) : X("social.other." + stance);
        return stance == SocialStance.Negotiate ? X("social.condition", ("response", response), ("he", p.Pro.Pronoun)) : response;
    }

    /// <summary>侦探自己的身体：做这一时段选定的事（与 NPC 一起在 CTB 上结算）。</summary>
    private void PlayerDo(PlayerAction? act)
    {
        if (Slot == Slot.Afternoon && Get(Det.Body, T.Hungry) > 0) return;
        var fx = FxOf(Det);
        if (Slot == Slot.Night)
        {
            if (act?.Cmd == Cmd.Stakeout) { SubmitDet(Fx.Seq(fx.Go(act.Place!.Value), Fx.Wait(NightLength - 2, fx.Look(Space[act.Place.Value]))), 0.5); Det.Asleep = false; }
            else { SubmitDet(fx.Look(Space[Where(Det)]), 0.5, Job.Sleep); }
            return;
        }
        if (act is null) { SubmitDet(fx.Job(Job.Rest, Det), 0.5, Job.Rest); return; }
        switch (act.Cmd)
        {
            case Cmd.Stay: SubmitDet(Fx.Seq(fx.Go(act.Place ?? Where(Det)), fx.Look(Space[act.Place ?? Where(Det)])), 0.5); break;
            case Cmd.Class: SubmitDet(fx.Job(Job.Class), 0.5, Job.Class); break;
            case Cmd.Collect: SubmitDet(fx.Job(Job.Collect), 0.8, Job.Collect); break;
            case Cmd.EatPantry: SubmitDet(fx.Look(Space[Place.DormLife]), 0.5); break;
            case Cmd.Inspect:
                var (pl, th) = Things_[act.Thing!];
                SubmitDet(Fx.Seq(fx.Go(pl), fx.Look(th)), 0.8);
                break;
            case Cmd.Escort: SubmitDet(fx.Escort(act.Place!.Value, Things_[act.Thing!].Thing), 1.2); break;
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
    // STORY-HARDCODE[CH2-056][logic][production]: 结算本章玩家行动、证据反馈与案情特定揭示。
    private void PlayerEnd(PlayerAction? act)
    {
        // 违约检查
        for (var i = 0; i < Promises.Count; i++)
        {
            var (who, due, done) = Promises[i];
            if (!done && Day > due || !done && Day == due && Slot == Slot.Night)
            {
                Promises[i] = (who, due, true);
                Witness(Det, K("食言", P.Det, who), -2, 0, [this[who]]);
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
                    Feed.Add($"铜口：“{a}”");
                    var present = People.Where(p => p != Det && Where(p) == Place.Lecture).ToArray();
                    if (praised)
                    {
                        Feed.Add("铜口当众说：“问得好。”");
                        // 评分是双刃剑：信恶魔或爱学的人更信你，戒备恶魔的人怀疑你是它的宠儿。
                        Witness(Det, K("提问受赞", P.Det), 0, 1, present.Where(p => p.Pro.DemonPrejudice >= 0), domain: Dom.Archive);
                        Witness(Det, K("宠儿", P.Det), -1, 0, present.Where(p => p.Pro.DemonPrejudice < 0));
                    }
                    if (refused) Feed.Add("（它援引了第七条。拒答本身也是一种回答。）");
                    if (act.Topic == 2 && act.Precise && WarnedPrecisely) NewsFrom(Det, NewsKind.Fact, K("温室泄压法"));
                    if (act.Topic is 0 or 1 && act.Precise && (act.Topic == 1 || Det.Lessons.ContainsKey(14)))
                        foreach (var p in present) { p.KnowsHookCost = true; Tell(p, Warning1, Channel.Public); }
                    // 当众逐字问出“取回”的真相：它自己说了“接通之后，我离诸位更近一些”。在场的人对它那条“凡学员皆可取回己物”多了一分疑心。
                    if (act.Topic == 0 && act.Precise && Det.Lessons.ContainsKey(14))
                    {
                        foreach (var p in present) { p.DemonDoubt = Math.Min(2, p.DemonDoubt + 1); NoteFor(p.Id, "铜口", "听到铜口亲口说“接通之后，我离诸位更近一些”时，他皱了眉"); }
                        var word = NewsFrom(Det, NewsKind.DemonWord, K("铜口亲口"));
                        foreach (var p in present) p.News[word.Id] = new(P.Det, 1, ObservationSource.Perception, Day, word.Id);
                    }
                }
                break;
            case Cmd.Inspect:
            case Cmd.Escort:
            case Cmd.Archive when false:
                var thing = Things_[act.Thing!].Thing;
                var area = Things_[act.Thing!].Place;
                if (here == area)
                {
                    // 同一区域的痕迹一并细看。
                    var all = new List<Essence> { thing };
                    foreach (var name in AreaThings.GetValueOrDefault(area) ?? []) { var e = Things_[name].Thing; if (!all.Contains(e)) all.Add(e); }
                    foreach (var e in all) { if (Day >= CaseDay) DetLooked.Add(e); DetSaw(e); }
                    if (all.Contains(Granary)) RevealLatch(Det); // 凑近细看，门闩的松紧与工具痕才看得出
                    Feed.Add($"你细看了{Names.Of(here)}：");
                    foreach (var e in all)
                    {
                        var description = Describe(e);
                        var key = ReferenceEquals(e, thing) ? act.Thing! : Things_.First(t => ReferenceEquals(t.Value.Thing, e)).Key;
                        InspectionRecords.Add(new(Day, Slot, key, area, description));
                        Feed.Add("  · " + description);
                    }
                    if (area == Place.ArchiveFront && DawnTraces.TryGetValue(Day - 1, out var tr) && tr.FirstOrDefault(t => t.Key == "前厅脚印") is { } fp)
                    {
                        InspectionRecords.Add(new(Day, Slot, "前厅脚印", area, fp.Text + "。"));
                        Feed.Add("  · " + fp.Text + "。");
                    }
                }
                else Feed.Add($"你没能走到{Names.Of(area)}。");
                if (act.Cmd == Cmd.Escort)
                {
                    var who = this[act.Who!.Value];
                    var came = DetHelpers.Contains(who.Id);
                    if (thing == Base) DetShown.Add(who.Id);
                    DetEscortTried.Add((who.Id, act.Thing!));
                    if (who.Id == P.Ward) WardEscorts += came ? 10 : 1;
                    if (came && thing == Base && Get(Base, T.Crack) > 0) Feed.Add($"你把那道裂纹指给{who.Name}看。{who.Pro.Pronoun}盯着看了很久。");
                    var saw = who.World.History.Any(h => h.Source == ObservationSource.Perception && ReferenceEquals(h.Target, who.M[thing]) && h.Time >= DayStart(Day));
                    Feed.Add(came ? $"{who.Name}跟你一起看了。" : saw && thing == Base ? $"{who.Name}没答应同行。{who.Pro.Pronoun}人就在{Names.Of(act.Place!.Value)}，可那道裂纹要凑近细看才看得出，你没能指给{who.Pro.Pronoun}。" : saw ? $"{who.Name}没答应同行，不过{who.Pro.Pronoun}本来就在{Names.Of(act.Place!.Value)}，也看见了。" : $"{who.Name}没有跟来（{(_asked.GetValueOrDefault(who.Id) == SocialStance.Accept ? "答应了，但另有更要紧的事" : "没答应")}）。");
                }
                break;
            case Cmd.Ask:
            {
                var who = this[act.Who!.Value];
                var job = act.Job!.Value;
                var did = DetHelpers.Contains(who.Id) || Get(who.Body, T.Did(job)) > 0;
                Feed.Add(did ? $"{who.Name}照你说的做了：{Names.Of(job)}。" : $"{who.Name}没有去做（{(_asked.GetValueOrDefault(who.Id) == SocialStance.Accept ? "答应了，但另有更要紧的事" : "没答应")}）。");
                if (did) Witness(Det, K("请托办成", P.Det, who.Id, values: ("事", Names.Of(job))), 0, 0, [who], spread: false);
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
                if (crew.Length > 0) Witness(Det, K(ok ? "主持开机关" : "主持开机关未成", P.Det), ok ? 1 : 0, ok ? 2 : -1, crew, domain: Dom.Archive);
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
                        NewsFrom(Det, NewsKind.Fact, K("石碑条文"));
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
                    Witness(Det, K("兑现承诺", P.Det, who), 1, 0, People.Where(p => Where(p) == Where(this[who])));
                    if (Repairs.Remove(who)) { Witness(Det, K("认错补过", P.Det), 1, 0, [this[who]], spread: true); Feed.Add($"{Names.Of(who)}点了点头：“行，这事算过去了。”"); }
                    Feed.Add($"你替{Names.Of(who)}办完了答应的事。");
                }
                else
                {
                    Witness(Det, K("帮工", P.Det, who), 1, 0, [this[who]], spread: false);
                    DetHelped.Add(who);
                    Feed.Add($"你帮{Names.Of(who)}干了一个时段的活。");
                }
                break;
            }
            case Cmd.Tell:
            {
                var who = this[act.Who!.Value];
                if (Det.News.TryGetValue(act.NewsId, out var heard)) { Receive(Det, who, AllNews[act.NewsId - 1], heard, Channel.Face); DetToldNews.Add((who.Id, act.NewsId)); Feed.Add($"你把「{Say(AllNews[act.NewsId - 1].Claim, Det, who)}」告诉了{who.Name}。"); }
                break;
            }
            case Cmd.TellRule:
            {
                var who = this[act.Who!.Value];
                var spec = OwnSpec(act.Spec) ?? CardSpec(act.Card!, P.Det);
                Det.Acc[spec] = Truth.True;
                var v = _preparedRuleVerdict ?? Tell(who, spec);
                _preparedRuleVerdict = null;
                _transmit[(spec.Id, who.Id)] = RecordEvent(P.Det, X("social.tell_log", ("name", who.Name), ("rule", Short(spec))), Names.Of(Job.Socialize), Where(Det), [], recipient: who, responseTo: SpeechTopics.Rule(spec.Id), propagation: true);
                DetToldFace.Add((spec, who.Id, v == Truth.True, Day));
                _toDisclose.RemoveAll(d => d.Who == who.Id && d.Spec == spec);
                Feed.Add(X("social.tell_feed", ("name", who.Name), ("label", LabelOf(spec)), ("rule", spec.Text), ("result", X(v == Truth.True ? "social.tell_yes" : "social.tell_no")), ("response", Voice(who, spec, v))));
                NoteFor(who.Id, X("social.rule_note_kind"), X("social.tell_note", ("result", X(v == Truth.True ? "social.tell_note_yes" : "social.tell_note_no")), ("label", LabelOf(spec)), ("rule", Short(spec))));
                if (spec.Manipulative) Manipulations.Add(new() { Target = who.Id, About = P.Quer, Means = X("social.targeted_rule"), Day = Day });
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
                    Witness(Det, K("当众认错", P.Det), 0, 0, present.Append(target), spread: true);
                    NoteFor(target.Id, "其他", "你当众认了错；他要你明天之前替他办一件事");
                }
                break;
            }
            case Cmd.Post:
            {
                var spec = OwnSpec(act.Spec) ?? CardSpec(act.Card!, P.Det);
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
                var told = K("去过后勤院", about.Id, night: Day - 1);
                var text = Say(told, Det, who);
                var item = NewsFrom(Det, NewsKind.Rumor, told, about: about, trust: -2, manipulation: true);
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
                    Witness(Det, K("分粮给人", P.Det, who.Id), 2, 0, People.Where(p => Where(p) == Where(who)));
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
    public string Testimony(Person p) => TestimonyPresentation(p).Text;

    // Presentation accompanies the selected wording; rules and testimony text are unchanged.
    /// <summary>
    /// 案发次日起问一个人“知道些什么”：他说自己那夜做了什么（多日交互审核 M1：谁做了什么由推演定，不按作者版本的名字）。
    /// 夜里取了口粮的人，信得过侦探才照实说；守夜的贝尔特朗报身影；请人修门闩的玛格丽特说请了谁；记账的、立规的人说账与规矩。
    /// 那夜什么也没做的人，就是平常的交谈。
    /// </summary>
    private (string Text, string Emotion) TestimonyPresentation(Person p, bool selectedTestimony = false)
    {
        if (Day < CaseDay) return ChatPresentation(p);
        var did = NightActions(CaseNight, p).ToArray();
        bool Did(params Job[] jobs) => did.Any(d => d.Job is { } j && jobs.Contains(j));
        var open = selectedTestimony || TrustIn(p, Det) >= 2;
        var latch = Did(Job.RepairLatch) ? X("证词.修闩") : "";
        switch (p.Id)
        {
            case P.Bertrand:
            {
                var reports = Sightings.Where(s => s.Witness == P.Bertrand && (s.Day == Day - 1 || s.Day == CaseNight))
                    .GroupBy(s => (s.Day, s.At)).Select(group => {
                        var figures = group.GroupBy(s => s.Build).Select(b => X("证词.守夜身影", ("数", b.Count()), ("身量", BuildText(b.Key).TrimEnd('的'))));
                        return X("证词.守夜地点", ("日", Chapter2Design.DayName(group.Key.Day)), ("地", Names.Of(group.Key.At)), ("身影", string.Join("、", figures)));
                    }).DefaultIfEmpty(X("证词.守夜无事"));
                return (X("证词.守夜报告", ("所见", string.Join("；", reports))), "calm");
            }
            case P.Ruan: return (X("证词.账面", ("缺", Get(Ledger, T.Unrecorded))), "thoughtful");
            case P.Ward: return (X(Accused is not null ? "证词.事实清楚" : "证词.无记录"), "calm");
            case P.Margaret:
            {
                var ask = Events.FirstOrDefault(e => e.Who == P.Margaret && e.Day == CaseNight && e.Job == Job.AskLatchHelp);
                var asked = ask?.Recipient;
                if (asked is not null && NightActions(CaseNight, asked).Any(a => a.Job == Job.RepairLatch))
                    return (X("证词.请人修闩", ("人", asked.Name), ("他", asked.Pro.Pronoun)), "calm");
                if (ask is not null) return (X("证词.请人未成"), "worried");
                break;
            }
        }
        if (Did(Job.NightTakeSick, Job.NightTakeSickRec))
        {
            if (!open) return (X("证词.病粮未公开"), "calm");
            var taken = did.Count(d => d.Job is Job.NightTakeSick or Job.NightTakeSickRec or Job.NightTakeSelf or Job.NightTakeSelfRec);
            var recorded = NightLedgerRecords.Count(n => n.Night == CaseNight && n.Actor == p)
                + AllNews.Count(n => n.Kind == NewsKind.Ledger && n.Claim.Predicate == "补记"
                    && n.Claim.Subject == p.Id && n.Claim.Night == CaseNight);
            var pending = Math.Max(0, taken - recorded);
            var key = recorded == 0 ? "病粮未记" : pending > 0 ? "病粮部分已记" : "病粮已记";
            return (X("证词." + key, ("夜", NightWord(CaseNight)), ("取用", taken), ("已记", recorded), ("待记", pending)), "worried");
        }
        if (Did(Job.NightTakeSelf, Job.NightTakeSelfRec))
            return open ? (X("证词.自取", ("修闩", latch)), "worried") : (X("证词.拒答"), "angry");
        if (latch.Length > 0) return (X("证词.只有修闩", ("修闩", latch)), "calm");
        return did.Length > 0 || p.Id is P.Margaret or P.Mora or P.Quer ? (X("证词.睡着"), "calm") : ChatPresentation(p);
    }

    /// <summary>平常的交谈：他最近听说、还没跟侦探讲过的两件事（按他自己的视角说，可能走样）。态度由开口那句回应表现，不再旁注。</summary>
    private (string Text, string Emotion) ChatPresentation(Person p)
    {
        var recent = p.News.OrderByDescending(n => n.Key).Where(n => AllNews[n.Key - 1].Kind != NewsKind.Lesson).Select(n => n.Value).Where(h => h.Day >= Day - 1).ToArray();
        // 只说侦探还不知道的：侦探已经亲见、听过，或旁白当面呈现过的，不再重复。
        var fresh = recent.Where(h => AllNews[h.Item - 1] is var item && !_presentedToDet.Contains(item.Claim.Identity) && !KnowsClaim(Det, item.Claim)).Take(2).ToArray();
        var t = TrustIn(p, Det);
        var he = p.Pro.Pronoun;
        // 说什么之前先定言语行为（Core 言语规则）：他知道自己听来的事，信不信侦探决定说几件、说不说。
        var act = fresh.Length == 0 ? SpeechAct.Tell : SpeechPolicy.Assess(1.0, t, new SpeechHabits())[0].Act;
        var told = act switch { SpeechAct.Hedge => fresh.Take(1).ToArray(), SpeechAct.Refuse or SpeechAct.Withhold => [], _ => fresh };
        foreach (var h in told) _presentedToDet.Add(AllNews[h.Item - 1].Claim.Identity);
        var lines = told.Select(h => Hearsay(p, AllNews[h.Item - 1], h)).ToArray();
        string Idle(string table) => X($"{table}.{p.Id}");
        var said = act switch
        {
            SpeechAct.Refuse => $"“{X("知识片段.不想说")}”",
            SpeechAct.Withhold => $"“{Idle("闲话无新事")}”{X("知识片段.欲言又止", ("他", he))}",
            _ when lines.Length == 0 && !_idleToDet.Add((p.Id, Day)) => Chapter2Design.HasText($"闲话又问.{p.Id}") ? X($"闲话又问.{p.Id}") : X("知识片段.又问", ("他", he)),
            _ when lines.Length == 0 => $"“{Idle(recent.Length > 0 ? "闲话都听过" : "闲话无新事")}”",
            _ when lines.Length == 1 => $"“{Stop(lines[0])}”",
            _ => $"“{Stop(lines[0])}{X("知识片段.还有")}{Stop(lines[1])}”",
        };
        if (t < 0 && act != SpeechAct.Refuse) said += X("知识片段.转开脸", ("他", he));
        // Recalling witnessed/heard news uses the thinking pose. Only the wording
        // explicitly saying he does not want to speak selects the annoyed pose.
        return (said, t < 0 ? "angry" : lines.Length > 0 ? "thoughtful" : "calm");
    }

    // STORY-HARDCODE[CH2-057][text][production]: 为本案专属物证、人物特征及状态撰写调查描述。
    public string Describe(Essence e)
    {
        if (e == Granary) return $"存粮还剩 {Get(Granary, T.Ration)} 份；门闩{(Get(Granary, T.LatchLoose) > 0 ? "松动，一推就开" : Get(Granary, T.ToolMarks) > 0 ? "有新的工具痕，已经紧固" : "完好")}。";
        if (e == Ledger) return X("物件.台账", ("发出", Get(Ledger, T.Handed)), ("补记", Get(Ledger, T.NightRecorded)))
            + string.Concat(AllNews.Where(n => n.Kind == NewsKind.Ledger && n.Claim.Predicate == "补记")
                .Select(n => "\n" + X("物件.台账补记记录", ("人", Names.Of(n.Claim.Subject!.Value)),
                    ("日", Chapter2Design.DayName(n.Day)), ("时段", Names.Of(n.Slot)), ("夜", Chapter2Design.DayName(n.Claim.Night)))))
            + (Get(Ledger, T.Counted) > 0 ? "\n" + (Get(Ledger, T.Unrecorded) > 0 ? X("物件.台账清点", ("缺", Get(Ledger, T.Unrecorded))) : X("物件.台账清点无缺")) : "");
        if (e == Bowl) return Get(Bowl, T.Residue) > 0 ? $"莫兰床边一只空碗，碗底有粥渍{(Get(Bowl, T.Porridge) > 0 ? "；另有一碗粥还温着" : "")}。" : "碗是干净的。";
        if (e == Bed) return Get(this[P.Moran].Body, T.Sick) > 0 ? "莫兰躺着，额头很烫。" : "病床空着。";
        if (e == Blackboard) return Get(Blackboard, T.Chalk) > 0 ? $"黑板上有 {Get(Blackboard, T.Chalk)} 堂课的粉笔字；讲台前的灰上有窄底皮鞋印。" : "黑板是干净的。";
        if (e == Stele) return "铺石顺坡，几级台阶。石面上刻着磨平的纹样，看不出原本写的是什么。";
        if (e == Gauge) return Get(Gauge, T.Pressure) >= 2 ? "指针已经过了红线。" : "指针还在红线以下。";
        if (e == SketchBook)
        {
            foreach (var (n, _) in Sketches) DetSketchSeen.Add(n);
            return Sketches.Count == 0 ? "莫兰的速写本里只有几张颜色练习。" : "莫兰的速写本：" + string.Join("　", Sketches.Select(s => s.Text)) + "。";
        }
        if (e == Base) return Get(Base, T.Crack) > 0 ? "中央基座上有一道新裂纹，像是从里面撑开的。" : "中央基座完好，石面上光洁，没有刻字。";
        if (e == ArchiveDoor) return "三道机关：配重室（要力气）、锁芯（要细手）、传声中继（要信号）。三处须同时施力。";
        if (e == MedCab) return X("物件.药柜", ("数", Get(MedCab, T.Medicine)));
        if (e == OilStore) return $"油库：灯油 {Get(OilStore, T.Oil)}。";
        if (Litter.ContainsValue(e)) return Get(e, T.Crumbs) > 0 ? "废纸篓里有一份口粮的包纸和饼屑。" : Get(e, T.Emptied) > 0 ? "废纸篓刚被人倒空过；篓底还粘着几粒饼屑和一角口粮包纸。" : "废纸篓里只有废纸。";
        if (Board.ContainsValue(e))
        {
            var place = Board.First(b => b.Value == e).Key;
            var specs = Specs.Where(s => s.PostedAt.Contains(place) && s != RuanLedger).ToArray();
            return specs.Length == 0 ? "板上什么也没贴。" : "板上贴着：" + string.Join(" ", specs.Select(s => $"「{s.Text}」（{(s.Author is { } a ? Names.Of(a) : "代管者")}）"));
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
        NoteFor(with, "赌约", $"你对{this[with].Pro.Pronoun}说：今晚{Names.Of(who)}会去{Names.Of(where)}{(rule is not null ? $"；说中了，{this[with].Pro.Pronoun}就认{LabelOf(rule)}「{Short(rule)}」" : "")}");
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
            Witness(Det, K(ok ? "赌约说中" : "赌约落空", P.Det, with.Id, values: [("某人", Names.Of(w.Who)), ("某地", Names.Of(w.Where))]), 0, ok ? 2 : -1, [with],
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
        Witness(Det, K(right ? "预判说中" : "预判落空", P.Det, values: [("某人", Names.Of(who)), ("某地", Names.Of(where))]), 0, right ? 2 : -1, _predictionWitnesses);
        Feed.Add($"（预判：{Names.Of(who)}{(right ? "确实" : "没有")}去{Names.Of(where)}。）");
    }
}
