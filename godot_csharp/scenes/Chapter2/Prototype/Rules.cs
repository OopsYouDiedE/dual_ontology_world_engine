using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>规则怎么到了这个人心里：当面讲、读板、当众（对质/议事）、经人转述（信任链）。</summary>
public enum Channel { Board, Face, Public, Relay }

/// <summary>侦探的“判断”分领域：口粮与案子的事信你，不等于名录库与机关的事也信你（§11 第 4 条）。</summary>
public enum Dom { General = 0, Rations = 1, Archive = 2 }

/// <summary>
/// 一条可张贴、可转告的规则。内容是 Core Rule（以 Fx 编译：张贴时用现实投影，判定时用读者自己的投影）；
/// Flags 是它在“欲望、习惯、利益、底线”上的语义，供接受公式使用。
/// </summary>
public sealed class RuleSpec
{
    public required int Id { get; init; }
    public required string Text { get; init; }
    public P? Author { get; init; }
    public bool ByDemon { get; init; }
    public RF Flags { get; init; }
    public required Func<Fx, IEnumerable<Rule>> Build { get; init; }
    public Card? Card { get; init; }
    /// <summary>规则点名针对的人（主语是某人且行为是限制）。</summary>
    public P? Target { get; init; }
    /// <summary>读者若亲见过这件事实，接受 +2（“带他去看”）。</summary>
    public Func<Game, Person, bool>? Witnessed { get; init; }
    public readonly HashSet<Place> PostedAt = [];
    public readonly HashSet<P> Endorsers = [];
    public bool Manipulative { get; init; }
    public Dom Domain => Flags.HasFlag(RF.Rations) || Flags.HasFlag(RF.EvidenceFirst) ? Dom.Rations
        : (Flags & (RF.OpenArchive | RF.CommitteeArchive | RF.SealArchive | RF.PactArchive | RF.ForbidNightOpen)) != 0 ? Dom.Archive : Dom.General;
    public override string ToString() => $"#{Id}「{Text}」";
}

/// <summary>五格拼规则（§11 第 6 条）：主语 + 条件 + 行为 + 例外 + 记账方式。</summary>
public sealed record Card(int Subject, int Cond, int Behavior, int Exception, int Account)
{
    public static readonly string[] Subjects = ["任何人", "值守者", "生活翼的人", "文契组的人", "星仪组的人", "后勤组的人"];
    public static readonly string[] Conds = ["任何时候", "入夜后", "有人急病时", "自己没领到口粮时", "学过《异象与记忆》之后"];
    public static readonly string[] Behaviors =
    [
        "取一份口粮", "按人头领口粮", "粮仓钥匙交给煮饭的人统一煮粥", "公开指控须附可检查的材料", "课后向缺课的人转述",
        "名录库各取其物", "名录库由委员会保管", "名录库封闭不启", "名录库须本人亲取、两人见证",
        "不得入后勤院", "温室两人同修，指针过红线不开顶窗"
    ];
    public static readonly string[] Exceptions = ["无例外", "急病夜取不在此限", "次日补记者不算私取", "两人在场者不在此限", "入夜一律不行"];
    public static readonly string[] Accounts = ["不记账", "记入台账", "次日补记", "两人见证签名"];

    /// <summary>主语编号：0—5 见 Subjects；10 + (int)P 为某一个人。</summary>
    public static string SubjectText(int s) => s >= 10 ? Names.Of((P)(s - 10)) : Subjects[s];
    public string Text => $"{SubjectText(Subject)}：{Conds[Cond]}，{Behaviors[Behavior]}；{Exceptions[Exception]}；{Accounts[Account]}";

    public static IEnumerable<Card> All(int subjects = 6)
    {
        for (var s = 0; s < subjects; s++)
        for (var c = 0; c < Conds.Length; c++)
        for (var b = 0; b < Behaviors.Length; b++)
        for (var e = 0; e < Exceptions.Length; e++)
        for (var a = 0; a < Accounts.Length; a++)
            yield return new(s, c, b, e, a);
    }

    public P? Target => Subject >= 10 && Behavior == 9 ? (P)(Subject - 10) : null;

    public RF Flags(Game g)
    {
        var f = RF.None;
        if (Cond == 2) f |= RF.SickPriority;
        f |= Behavior switch
        {
            0 => RF.Rations, 1 => RF.PerHead | RF.Rations, 2 => RF.CentralCook | RF.Rations, 3 => RF.EvidenceFirst, 4 => RF.Relay, 5 => RF.OpenArchive,
            6 => RF.CommitteeArchive | RF.WardAuthority, 7 => RF.SealArchive, 8 => RF.PactArchive | RF.TwoWitness | RF.OpenArchive,
            9 => RF.Restricts | RF.Rations, 10 => RF.Safety, _ => RF.None
        };
        if (Target is { } t) f |= RF.Targets | (t == P.Quer ? RF.TargetsQuer : RF.None);
        if (Behavior == 9 && Subject == 0 && g.AccusedQuer) f |= RF.TargetsQuer;
        f |= Exception switch
        {
            0 => RF.NoException, 1 or 2 => RF.NightException, 3 => RF.TwoWitness,
            4 => (f & (RF.OpenArchive | RF.CommitteeArchive | RF.PactArchive)) != 0 ? RF.ForbidNightOpen : RF.NoException | RF.Restricts,
            _ => RF.None
        };
        f |= Account switch { 1 or 2 => RF.Records, 3 => RF.Records | RF.TwoWitness, _ => RF.None };
        return f;
    }
}

public sealed partial class Game
{
    public readonly List<RuleSpec> Specs = [];
    public RuleSpec WardLedger = null!, WardNight = null!, MoraNight = null!, RuanLedger = null!, Warning1 = null!, Warning2 = null!, CommonRule = null!;
    public bool AccusedQuer;
    /// <summary>第一个被当众指控私取的人（可能是雀儿，也可能是被人亲眼看见没记账的别人）。</summary>
    public P? Accused;
    private int _specId;

    public RuleSpec NewSpec(string text, P? author, RF flags, Func<Fx, IEnumerable<Rule>> build, bool demon = false, Card? card = null,
        Func<Game, Person, bool>? witnessed = null, bool manipulative = false, P? target = null)
    {
        var s = new RuleSpec
        {
            Id = ++_specId, Text = text, Author = author, ByDemon = demon, Flags = flags, Build = build, Card = card, Witnessed = witnessed,
            Manipulative = manipulative, Target = target
        };
        Specs.Add(s);
        if (author is { } a) s.Endorsers.Add(a);
        return s;
    }

    /// <summary>作者侧已知的规则：沃德、茉拉、阮清越的板（第三部分 §7）、恶魔的致命警告（§5.3），以及各价值聚落的立规（§11 第 7 条，见 Alliances.cs）。</summary>
    private void WriteRules()
    {
        WardLedger = NewSpec("凡取用公物须登记于台账；无登记取用者视为私取。", P.Ward, RF.Records | RF.NoException | RF.WardAuthority | RF.Rations,
            fx => [new(Fx.Constant, RuleMode.Forbid, fx.Job(Job.NightTakeSelf), 2), new(Fx.Constant, RuleMode.Forbid, fx.Job(Job.NightTakeSick), 2)]);
        WardNight = NewSpec("私取之人（雀儿）入夜不得入后勤院。", P.Ward, RF.Restricts | RF.TargetsQuer | RF.Targets | RF.WardAuthority | RF.Rations,
            fx => [new(Fx.All(fx.At(Slot.Night), Fx.Mine(T.Branded)), RuleMode.Forbid, fx.Go(Place.Logistics), 2)], target: P.Quer);
        MoraNight = NewSpec("病者伤者优先。夜间急需可自取一份，次日告知。", P.Mora, RF.SickPriority | RF.NightException | RF.Rations,
            fx => [new(Fx.All(fx.At(Slot.Night), Fx.Not(Fx.Mine(T.Fed))), RuleMode.Prefer, fx.Job(Job.NightTakeSelf), 3),
                   new(Fx.All(fx.AtAny([Slot.Morning, Slot.Forenoon, Slot.Noon, Slot.Afternoon, Slot.Evening]), Fx.Mine(T.OweNote)), RuleMode.Prefer, fx.Job(Job.Inform), 2)]);
        RuanLedger = NewSpec("每日按人头分配，日末清点。", P.Ruan, RF.PerHead | RF.Records | RF.Rations,
            fx => [new(Fx.All(fx.At(Slot.Evening), Fx.Mine(T.Duty), fx.NotDone(Job.CountLedger)), RuleMode.Prefer, fx.Job(Job.CountLedger), 1)]);
        Warning1 = NewSpec("入夜后，名录库中若有人以你之名唤你，勿应。", null, RF.Safety | RF.ForbidsDesire,
            fx => [new(Fx.Constant, RuleMode.Forbid, fx.Job(Job.AnswerName), 5)], demon: true);
        Warning2 = NewSpec("温室水压表指针过红线时，勿开顶窗。", null, RF.Safety,
            fx => [new(fx.Tg(Gauge, T.Pressure, 2), RuleMode.Forbid, fx.Set(Valve, T.Open, 1), 5)], demon: true);
        CommonRule = NewSpec("公开指控须附可检查的材料。", P.Det, RF.EvidenceFirst,
            fx => [new(Fx.Not(Fx.Mine(T.SawAct)), RuleMode.Forbid, fx.Job(Job.Accuse), 3)], card: new Card(0, 0, 3, 0, 0));
        Post(RuanLedger, Place.Logistics, quiet: true); // 台账上的旧规则（粮仓门内）
        WriteAllianceRules();
    }

    public RuleSpec CardSpec(Card card, P author)
    {
        if (Specs.FirstOrDefault(s => s.Card == card && s.Author == author) is { } existing) return existing;
        var flags = card.Flags(this);
        return NewSpec(WordingOf(card) ?? card.Text, author, flags, fx => CompileCard(fx, card), card: card,
            witnessed: card.Behavior == 0 ? (g, p) => g.SawNightPermission(p) : null,
            manipulative: card.Behavior == 9 && card.Subject != 0, target: card.Target);
    }

    /// <summary>主语格：编译成“我是不是这个主语”的条件（读的是此人心中自己的身份标签）。</summary>
    private static Condition SubjectCondition(int subject) => subject switch
    {
        0 => Fx.Constant,
        1 => Fx.Mine(T.Duty),
        >= 2 and <= 5 => Fx.Mine(T.InGroup[(Group)(subject - 2)]),
        _ => Fx.Mine(T.Is[(P)(subject - 10)])
    };

    private IEnumerable<Rule> CompileCard(Fx fx, Card card)
    {
        var who = SubjectCondition(card.Subject);
        Condition cond = card.Cond switch
        {
            1 => fx.At(Slot.Night),
            // “急病时可取”：病人碗里已经有粥，就不必再取。
            2 => Fx.All(fx.At(Slot.Night), fx.AnySick(null), Fx.Not(fx.Tg(Bowl, T.Porridge))),
            3 => Fx.All(fx.At(Slot.Night), Fx.Not(Fx.Mine(T.Fed))),
            4 => fx.Tg(Clock, T.Day, 6),
            _ => Fx.Constant
        };
        cond = Fx.All(who, cond);
        switch (card.Behavior)
        {
            case 0:
                var sick = card.Cond == 2;
                var rec = sick ? Job.NightTakeSickRec : Job.NightTakeSelfRec;
                var unrec = sick ? Job.NightTakeSick : Job.NightTakeSelf;
                cond = Fx.All(cond, fx.NotDone(rec), fx.NotDone(unrec)); // 试过、没做成，今夜不再试
                if (card.Exception == 4) { yield return new(Fx.All(who, fx.At(Slot.Night)), RuleMode.Forbid, fx.Job(unrec), 3); yield return new(Fx.All(who, fx.At(Slot.Night)), RuleMode.Forbid, fx.Job(rec), 3); break; }
                // “可以取”是许可，不是想要（Core 的 Permit 见报告）。
                if (card.Account is 1 or 3)
                {
                    yield return new(cond, RuleMode.Permit, fx.Job(rec), 2);
                    yield return new(cond, RuleMode.Forbid, fx.Job(unrec), 3);
                }
                else yield return new(cond, RuleMode.Permit, fx.Job(unrec), 2);
                if (card.Account == 2 || card.Exception == 2)
                    yield return new(Fx.All(who, fx.AtAny([Slot.Morning, Slot.Forenoon, Slot.Noon]), Fx.Mine(T.OweNote)), RuleMode.Prefer, fx.Job(Job.Inform), 3);
                break;
            case 1:
                yield return new(Fx.All(who, fx.AtAny([Slot.Noon, Slot.Evening]), Fx.Not(Fx.Mine(T.Fed))), RuleMode.Prefer, fx.Job(Job.Collect), 1);
                break;
            case 2:
                yield return new(Fx.All(who, fx.AtAny([Slot.Noon, Slot.Evening]), Fx.Mine(T.GranaryKey)), RuleMode.Prefer, fx.GiveKeyTo(this[P.Shen]), 4);
                break;
            case 3:
                yield return new(Fx.All(who, Fx.Not(Fx.Mine(T.SawAct))), RuleMode.Forbid, fx.Job(Job.Accuse), 3);
                break;
            case 5 or 6 or 8 when card.Exception == 4:
                yield return new(Fx.All(who, fx.At(Slot.Night)), RuleMode.Forbid, fx.Job(Job.Seize), 4);
                break;
            case 9:
                yield return new(Fx.All(who, fx.At(Slot.Night)), RuleMode.Forbid, fx.Go(Place.Logistics), 2);
                break;
        }
    }

    // ----------------------------------------------------------- posting --

    public void Post(RuleSpec spec, Place place, bool quiet = false)
    {
        spec.PostedAt.Add(place);
        var board = place == Place.Logistics && spec == RuanLedger ? Ledger : Board[place];
        var rules = board.Get<RuleProperty>() ?? board.Add(new RuleProperty());
        rules.Rules = [.. Specs.Where(s => s.PostedAt.Contains(place) && (board == Ledger) == (s == RuanLedger)).SelectMany(s => s.Build(RealFx))];
        PostVersion++;
        Rt.Sync();
        if (quiet) return;
        Log.Add($"D{Day} {Names.Of(Slot)} 张贴于{Names.Of(place)}：{spec}");
        // 涌现记录：张贴是一次“写”。人物在自己动作里贴的，算那个动作；按作者日程贴的（第 2 日傍晚各组、各聚落的立规）是作者固定节点。
        var author = spec.Author ?? P.Det;
        var id = _physicsOf?.Id == author && _currentEvent > 0 ? _currentEvent
            : RecordEvent(author, $"在{Names.Of(place)}贴出「{Short(spec)}」", "操作", place, [], fix: author != P.Det);
        _postedEv[spec.Id] = id;
        Touch(board, T.Posted, id, author);
    }

    /// <summary>从板上撕掉一条规矩（板上的规则随之改变）。</summary>
    public void Unpost(RuleSpec spec, Place place)
    {
        spec.PostedAt.Remove(place);
        var board = Board[place];
        var rules = board.Get<RuleProperty>()!;
        rules.Rules = [.. Specs.Where(s => s.PostedAt.Contains(place) && s != RuanLedger).SelectMany(s => s.Build(RealFx))];
        PostVersion++;
        Rt.Sync();
    }

    /// <summary>当面告知（或经人转述）：规则作为 Testimony 写入对方“听来的规则”，接受与否当场判定。</summary>
    public Truth Tell(Person to, RuleSpec spec, Channel channel = Channel.Face, Person? via = null)
    {
        var verdict = Judge(to, spec, channel, Where(to), via);
        WriteTold(to, spec, verdict);
        return verdict;
    }

    private void WriteTold(Person to, RuleSpec spec, Truth verdict)
    {
        var rules = to.Told.Rules.ToList();
        var mental = spec.Build(FxOf(to)).Select(r => r with { Acceptance = verdict }).ToArray();
        rules.RemoveAll(r => mental.Any(m => SameRule(m, r)));
        rules.AddRange(mental);
        Observation.Observe(to.Soul, new ObservationInput(to.Told.Name, to.Told.Kind)
        { Target = to.Told, Source = ObservationSource.Testimony, Time = Now, Rules = [.. rules], Details = spec.Text });
    }

    /// <summary>重演用：把此人心中这条规则的接受状态直接改写（板上的、听来的都改）。</summary>
    public void OverrideAcceptance(Person p, RuleSpec spec, Truth verdict)
    {
        p.Acc[spec] = verdict;
        p.Why[spec] = "重演假设";
        var mental = spec.Build(FxOf(p)).ToArray();
        foreach (var holder in p.World.Projections.Where(x => x.Rules.Any(r => mental.Any(m => SameRule(m, r)))).ToArray())
            Observation.Observe(p.Soul, new ObservationInput(holder.Name, holder.Kind)
            { Target = holder, Source = ObservationSource.Inference, Time = Now, Rules = [.. holder.Rules.Select(r => mental.Any(m => SameRule(m, r)) ? r with { Acceptance = verdict } : r)] });
        if (verdict == Truth.True && !p.World.Projections.Any(x => x.Rules.Any(r => mental.Any(m => SameRule(m, r))))) WriteTold(p, spec, verdict);
    }

    private static bool SameRule(Rule a, Rule b) => a.Mode == b.Mode && Ast.Equivalent(a.Condition, b.Condition) && Ast.Equivalent(a.Effect, b.Effect);

    /// <summary>
    /// 读板：规则已经由 WorldRuntime.Observe 以“接受未知”带入读者心中的板投影；这里逐条找出它属于哪条规则，
    /// 判定接受与否后写回（Inference）。没判过、或上次没接受（且不是底线否决）的，每次重读都重判。
    /// </summary>
    private void JudgeBoards(Person p, Place place)
    {
        foreach (var board in new[] { Board.GetValueOrDefault(place), place == Place.Logistics ? Ledger : null })
        {
            if (board is null) continue;
            var mental = p.M[board];
            if (mental.Rules.IsEmpty) continue;
            var specs = Specs.Where(s => s.PostedAt.Contains(place) && (board == Ledger) == (s == RuanLedger)).ToArray();
            var changed = false;
            var rules = mental.Rules.Select(rule =>
            {
                var spec = specs.FirstOrDefault(s => s.Build(FxOf(p)).Any(m => SameRule(m, rule)));
                if (spec is null) return rule;
                if (!p.Acc.TryGetValue(spec, out var acc) || acc != Truth.True && !p.Vetoed.Contains(spec)) acc = Judge(p, spec, Channel.Board, place);
                if (rule.Acceptance != acc) changed = true;
                return rule with { Acceptance = acc };
            }).ToImmutableArray();
            if (changed)
                Observation.Observe(p.Soul, new ObservationInput(mental.Name, mental.Kind) { Target = mental, Source = ObservationSource.Inference, Time = Now, Rules = rules });
        }
    }

    // --------------------------------------------------------- acceptance --

    public const int TrustCap = 2, JudgmentCap = 2;

    /// <summary>
    /// 规则接受（§5.3 + §11 第 3、4 条）。完全确定，≥ 阈值即接受；触犯底线即拒绝，信任再高也无用。
    /// 立规者一项分“为人”（不撒谎、不冤枉人）与该领域的“判断”（推得准、办得成），各自有上限；
    /// 经人转述时，转述者的担保代替大半作者的分量；公告板覆盖面大但说服力弱。
    /// </summary>
    public int AcceptScore(Person p, RuleSpec s, Channel channel, Place? at, out string why, Person? via = null, bool stance = false)
    {
        using var _ = Prof.Time("AcceptScore");
        var parts = new List<string>();
        if (s.Author == p.Id) { why = "自己立的"; return 99; }
        if (Veto(p, s) is { } veto) { why = $"底线：{veto}"; return -99; }
        var score = 0;
        var author = AuthorTerm(p, s, parts);
        // 名录库的立场（§13 第 6、12 条）：信不信提方案的人只占小分量；“点过头”也只占小分量。主要看欲望、亲眼所见、饥饱、所属派系。
        if (stance && !s.ByDemon)
        {
            var capped = Math.Clamp(author, -1, 1);
            if (capped != author) parts.Add($"（立场：信谁只算{capped:+0;-0;0}）");
            author = capped;
        }
        if (stance) score += StanceTerms(p, s, parts);
        if (via is not null && via != p && via.Id != s.Author)
        {
            author = Math.Min(author, 1);
            var v = Math.Clamp(TrustIn(p, via), 0, TrustCap);
            score += v; parts.Add($"{via.Name}担保{v:+0;-0;0}");
            // 转述者对立规者有成见：话一出口就走了样（§13 第 5 条）。
            if (s.Author is { } au && via.Pro.Prejudice.GetValueOrDefault(au) < 0) { score -= 1; parts.Add($"{via.Name}讲走了样-1"); }
        }
        score += author;
        if (at is { } place)
        {
            var best = s.Endorsers.Where(e => e != p.Id && e != s.Author && e != via?.Id && Where(this[e]) == place)
                .Select(e => TrustIn(p, this[e])).DefaultIfEmpty(0).Max();
            if (best >= 2) { score += 1; parts.Add("在场背书+1"); }
        }
        foreach (var flag in Enum.GetValues<RF>().Where(f => f != RF.None && s.Flags.HasFlag(f)))
            if (p.Pro.Values.TryGetValue(flag, out var v) && v != 0) { score += v; parts.Add($"{FlagText(flag)}{v:+0;-0;0}"); }
        if ((s.Flags & p.Pro.DesireFlags) != 0 && p.Desire > 1) { score += p.Desire - 1; parts.Add($"欲望+{p.Desire - 1}"); }
        if (s.Flags.HasFlag(RF.ForbidsDesire) && p.Pro.DesireFlags.HasFlag(RF.OpenArchive) && p.Desire > 1) { score -= p.Desire - 1; parts.Add($"与欲望相悖-{p.Desire - 1}"); }
        // 想得最厉害的人，嫌“须等、须见证、夜里不许”太慢（同一条规则无法让所有人满意）。
        if (s.Flags.HasFlag(RF.PactArchive) && s.Flags.HasFlag(RF.ForbidNightOpen) && p.Pro.DesireFlags.HasFlag(RF.OpenArchive) && p.Desire > 2) { score -= p.Desire - 2; parts.Add($"嫌太慢-{p.Desire - 2}"); }
        if (s.Witnessed?.Invoke(this, p) == true) { score += 2; parts.Add("亲见+2"); }
        if (s.Flags.HasFlag(RF.Rations))
            foreach (var (need, penalty, text) in Requirements(p.Id))
                if ((s.Flags & need) == 0) { score -= penalty; parts.Add($"{text}-{penalty}"); }
        if (s == Warning1 && p.KnowsHookCost) { score += 2; parts.Add("当众听过应答的代价+2"); }
        if (p.KnowsHookCost && s.Flags.HasFlag(RF.OpenArchive) && !s.Flags.HasFlag(RF.PactArchive)) { score -= 1; parts.Add("知道回忆的代价-1"); }
        if (p.KnowsHookCost && s.Flags.HasFlag(RF.SealArchive)) { score += 1; parts.Add("知道回忆的代价+1"); }
        if (p.Wagered.Contains(s)) { score += 3; parts.Add("预判赌约+3"); }
        var ch = channel switch { Channel.Face => 1, Channel.Board => -1, _ => 0 };
        if (ch != 0) { score += ch; parts.Add(channel == Channel.Face ? "当面+1" : "只是贴在板上-1"); }
        why = string.Join(" ", parts) + $" = {score}";
        return score;
    }

    /// <summary>立场的主要来源：亲眼所见、点过头、饭桌上被驳、饥饱、所属派系、知道第九条。</summary>
    private int StanceTerms(Person p, RuleSpec s, List<string> parts)
    {
        var fam = FamilyOf(s);
        var score = 0;
        void Add(int v, string why) { if (v == 0) return; score += v; parts.Add($"{why}{v:+0;-0;0}"); }
        if (p.Witnessed.Contains("hook")) Add(fam switch { Ending.OpenEach => -2, Ending.Sealed or Ending.DetectivePact => 1, _ => 0 }, "亲眼见过应了唤名的人");
        if (p.Witnessed.Contains("crack")) Add(fam switch { Ending.OpenEach => -4, Ending.DetectivePact or Ending.Committee => 1, _ => 0 }, "亲眼见过基座的裂纹");
        else if (p.Witnessed.Contains("crack-heard")) Add(fam == Ending.OpenEach ? -1 : 0, "听亲眼见过的人说基座裂了");
        if (p.Witnessed.Contains("machine")) Add(fam == Ending.DetectivePact ? 2 : 0, "亲眼见过侦探把机关安稳地试开");
        if (p.Acc.GetValueOrDefault(s) == Truth.True) Add(1, "点过头");
        if (p.StanceNudge.TryGetValue(fam, out var n)) Add(Math.Min(1, n), "饭桌上被说动");
        if (p.News.Values.Any(h => h.Text.Contains("第九、第十条"))) Add(fam switch { Ending.OpenEach => -1, Ending.DetectivePact => 1, _ => 0 }, "听说石碑上还有第九条");
        if (p.HungryAtCouncil && GrainSide() is { } g && g == fam && People.FirstOrDefault(x => Get(x.Body, T.GranaryKey) > 0) is { } holder && TrustIn(p, holder) >= 1) Add(1, "饿着，跟掌粮的人走");
        if (FactionLeaderOf(p) is { } lead && lead != p && lead != Det && _leaderStance.TryGetValue(lead.Id, out var lf) && lf == fam) Add(1, $"跟着{lead.Name}");
        return score;
    }

    private readonly Dictionary<P, Ending> _leaderStance = [];
    private Person? FactionLeaderOf(Person p) => _factionCache?.FirstOrDefault(f => f.Members.Contains(p.Id)) is { } f ? this[f.Leader] : null;
    private List<Faction>? _factionCache;
    /// <summary>掌粮的一方：手里有粮仓钥匙的人此刻向着的那一边。</summary>
    private Ending? GrainSide() => People.FirstOrDefault(x => Get(x.Body, T.GranaryKey) > 0) is { } k && _leaderStance.TryGetValue(k.Id, out var f) ? f : null;

    /// <summary>每个时段开头重算一次派系与领头人的立场（立场里“跟着领头人”一项用它，避免循环）。</summary>
    public void RefreshStanceContext()
    {
        _factionCache = Factions();
        _leaderStance.Clear();
        var heads = _factionCache.Select(f => f.Leader).Append(People.FirstOrDefault(x => Get(x.Body, T.GranaryKey) > 0)?.Id ?? P.Mora).Distinct();
        foreach (var h in heads.Where(h => h != P.Det))
        {
            var lead = this[h];
            var best = StancePlans().Select(s => (s, sc: AcceptScore(lead, s, Channel.Public, null, out _))).Where(x => x.sc >= Options.AcceptThreshold).OrderByDescending(x => x.sc).ThenBy(x => x.s.Id).FirstOrDefault();
            if (best.s is not null) _leaderStance[h] = FamilyOf(best.s);
        }
    }

    private int AuthorTerm(Person p, RuleSpec s, List<string> parts)
    {
        if (s.ByDemon)
        {
            var d = Math.Min(3, 2 + p.Hooks + (p.Lessons.Count > 0 ? 1 : 0)) + p.Pro.DemonPrejudice - p.DemonDoubt;
            parts.Add($"信恶魔{d:+0;-0;0}");
            return d;
        }
        if (s.Author is not { } a) return 0;
        var score = 0;
        if (a == P.Det)
        {
            var (character, judgment) = ImageOfDet(p, s.Domain);
            var c = Math.Clamp(character, -3, TrustCap);
            var j = Math.Clamp(judgment / 2, -1, JudgmentCap);
            score = c + j;
            parts.Add($"信侦探为人{c:+0;-0;0}");
            if (j != 0) parts.Add($"信侦探在{DomText(s.Domain)}上的判断{j:+0;-0;0}");
        }
        else
        {
            score = Math.Clamp(TrustIn(p, this[a]), -3, TrustCap);
            parts.Add($"信{Names.Of(a)}{score:+0;-0;0}");
        }
        if (p.Pro.Prejudice.TryGetValue(a, out var pj)) { score += pj; parts.Add($"成见{pj:+0;-0;0}"); }
        return score;
    }

    public static string DomText(Dom d) => d switch { Dom.Rations => "口粮与案子", Dom.Archive => "名录库与机关", _ => "一般事" };

    public Truth Judge(Person p, RuleSpec s, Channel channel, Place? at, Person? via = null)
    {
        var score = AcceptScore(p, s, channel, at, out var why, via);
        var verdict = Options.ForceAcceptance?.Invoke(this, p, s) ?? (score >= Options.AcceptThreshold ? Truth.True : Truth.False);
        var before = p.Acc.GetValueOrDefault(s);
        p.Acc[s] = verdict;
        p.Why[s] = why;
        if (score == -99) p.Vetoed.Add(s);
        if (verdict == Truth.True && before != Truth.True)
        {
            s.Endorsers.Add(p.Id);
            p.Diary.Add($"D{Day} {Names.Of(Slot)} 接受{s}（{why}）");
            if (p != Det) Log.Add($"D{Day} {Names.Of(Slot)} {p.Name} 接受 {s}（{why}）{(via is not null ? $"〔经{via.Name}转述〕" : "")}");
            OnAccepted(p, s);
        }
        if (before == Truth.Unknown && channel == Channel.Board && at is { } boardAt && p != Det) QueueDisclosure(p, s, null, boardAt, verdict == Truth.True);
        if (channel == Channel.Board && _postedEv.TryGetValue(s.Id, out var postEv)) _transmit.TryAdd((s.Id, p.Id), postEv);
        if (verdict == Truth.False && before == Truth.Unknown)
        {
            Log.Add($"D{Day} {Names.Of(Slot)} {p.Name} 不接受 {s}（{why}）");
            OnRejected(p, s);
        }
        return verdict;
    }

    private static IEnumerable<(RF Need, int Penalty, string Text)> Requirements(P p) => p switch
    {
        P.Ruan => [(RF.Records, 2, "不记账就对不上账")],
        P.Quer => [(RF.NightException | RF.TwoWitness | RF.EvidenceFirst, 2, "又是一条只抓人的规矩")],
        _ => []
    };

    /// <summary>
    /// 底线价值（§11 第 4 条）：每人一至两条，触犯即拒绝。返回此人会说的那句话；没触犯返回 null。
    /// 上了钩的人多一条：名录库“入夜一律不行”——名字在叫他。
    /// </summary>
    public string? Veto(Person p, RuleSpec s)
    {
        var f = s.Flags;
        bool Has(RF x) => (f & x) != 0;
        var openPlain = Has(RF.OpenArchive) && !Has(RF.PactArchive);
        if (p.Hooks > 0 && Has(RF.ForbidNightOpen)) return "夜里有人在叫我的名字。不许我去，不行。";
        return p.Id switch
        {
            P.Ward when Has(RF.Rations) && !Has(RF.Records | RF.TwoWitness) => "依据：无记录，即不可。",
            P.Ward when openPlain && !Has(RF.Records | RF.TwoWitness) => "依据：无记录，即不可。",
            P.Mora when Has(RF.Rations) && Has(RF.NoException | RF.Restricts) && !Has(RF.SickPriority | RF.NightException) => "病人不能等。谁定的规矩我都不认。",
            P.Mora when openPlain => "取回的东西会伤人。我不同意随便开。",
            P.Quer when Has(RF.Restricts | RF.TargetsQuer) => "又是一条只抓人的规矩。",
            P.Quer when Has(RF.CommitteeArchive) => "让沃德管？省省吧。",
            P.Gu when openPlain && !Has(RF.Tradition) => "禁忌不可轻改。",
            P.Ruan when Has(RF.Rations) && !Has(RF.Records | RF.TwoWitness) => "不记账，账就对不上。不行。",
            P.Shen when Has(RF.Rations) && Has(RF.Restricts) => "拦着人不让吃饭，我不认。",
            P.Su when s.Manipulative || Has(RF.Targets) => "这话说得好听，是冲着一个人去的。",
            P.Lucian when Has(RF.Targets) || Has(RF.Restricts) && Has(RF.TargetsQuer) => "以规为刃，专割弱者——恕难从命。",
            P.Selma when Has(RF.WardAuthority) => "又一层官僚。不要。",
            P.Huo when Has(RF.SealArchive) => "图纸在里面。封？不行。",
            P.Elena when Has(RF.Restricts) => "把一个人关在外面的规矩……我经历过。不。",
            P.Moran when Has(RF.SealArchive) => "颜、颜色……在里面。不封。",
            P.Veronica when s.ByDemon => "神秘主义的指令，不采纳。",
            P.Bertrand when Has(RF.WardAuthority) => "报告：不听政客的。",
            P.Margaret when Has(RF.Restricts) && !Has(RF.EvidenceFirst) => "没查清受力就下结论？不行。",
            _ => null
        };
    }

    public static string FlagText(RF f) => f switch
    {
        RF.Records => "记账", RF.SickPriority => "病者优先", RF.PerHead => "按人头", RF.Restricts => "限制他人", RF.TargetsQuer => "针对雀儿",
        RF.CentralCook => "统一煮粥", RF.EvidenceFirst => "凭证据", RF.OpenArchive => "开库", RF.CommitteeArchive => "委员会", RF.SealArchive => "封库",
        RF.PactArchive => "约束开库", RF.NightException => "夜取例外", RF.TwoWitness => "两人见证", RF.WardAuthority => "沃德定规",
        RF.Tradition => "守旧制", RF.Relay => "转述", RF.Safety => "保命", RF.NoException => "无例外", RF.ForbidNightOpen => "夜不开库",
        RF.Rations => "口粮", RF.Targets => "点名", _ => f.ToString()
    };

    /// <summary>“亲见”夜取许可的事实：读过配给台的茉拉规则，且亲眼看过修好的门闩或诊疗所的碗。</summary>
    public bool SawNightPermission(Person p)
    {
        bool Saw(Essence e, Tag t) => p.World.History.Any(h => h.Source == ObservationSource.Perception && ReferenceEquals(h.Target, p.M[e]) && h.Tags.GetValueOrDefault(t) > 0);
        return p.Acc.ContainsKey(MoraNight) && (Saw(Granary, T.ToolMarks) || Saw(Bowl, T.Residue));
    }

    /// <summary>此人会怎么说出自己的态度（玩家只看到这句话，看不到分数）。</summary>
    public string Voice(Person p, RuleSpec s, Truth verdict)
    {
        if (verdict == Truth.True) return p.Id switch
        {
            P.Ward => "“依据此条，可行。”", P.Ruan => "“划算。”", P.Quer => "“行吧。”", P.Mora => "“照这个办。”", P.Gu => "“合乎情理。”",
            P.Lucian => "“善。此规有韵。”", P.Selma => "“像蜂巢的分工，能行。”", P.Veronica => "“结论：可执行。”", P.Bertrand => "“明白。执行。”",
            P.Margaret => "“结构说得通。”", P.Huo => "“行。”", P.Shen => "“这样大家都有口饭。”", P.Su => "“听着不刺耳。”", P.Elena => "“可以。”", P.Moran => "“……好。”",
            _ => "“好。”"
        };
        if (p.Vetoed.Contains(s)) return "“" + (Veto(p, s) ?? "不。") + "”";
        return p.Id switch
        {
            P.Ward => "“不予采纳。”", P.Ruan => "“不划算。”", P.Quer => "“凭什么。”", P.Mora => "“再说吧。”", P.Gu => "“容老朽再想想。”",
            P.Lucian => "“此规尚欠一韵。”", P.Selma => "“说不上来，不对劲。”", P.Veronica => "“论据不足。”", P.Bertrand => "“暂不执行。”",
            P.Margaret => "“哪里受力不对。”", P.Huo => "“没想好。”", P.Shen => "“再看看。”", P.Su => "“……”", P.Elena => "“恕我不能。”", P.Moran => "“……不、不知道。”",
            _ => "“不了。”"
        };
    }
}
