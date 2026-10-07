using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>
/// 剧本里的条件短语（tools/story_rules.py 的 CondParser 编译出的 JSON 树）的通用求值器。
/// 求值只读客观记录与各人的认知，条件的内容（日期、人物、物件、标记）全部来自 chapter_01.story。
/// </summary>
public abstract record Cond
{
    /// <summary>who 为空表示侦探。</summary>
    public abstract bool Eval(Game g, Person? who = null);

    public static Cond Parse(JsonElement e)
    {
        string S(string name) => e.GetProperty(name).GetString()!;
        switch (S("op"))
        {
            case "true": return new True();
            case "not": return new Not(Parse(e.GetProperty("item")));
            case "all": return new All([.. e.GetProperty("items").EnumerateArray().Select(Parse)]);
            case "any": return new Any([.. e.GetProperty("items").EnumerateArray().Select(Parse)]);
            case "night_did":
                return new NightDid(e.GetProperty("day").GetInt32(), Enum.Parse<P>(S("who")),
                    [.. e.GetProperty("jobs").EnumerateArray().Select(j => Enum.Parse<Job>(j.GetString()!))], e.GetProperty("min").GetInt32());
            case "sighting":
                return new Sighting(e.GetProperty("day").GetInt32(), Enum.Parse<Place>(S("at")), e.TryGetProperty("build", out var b) ? Enum.Parse<Build>(b.GetString()!) : null,
                    e.TryGetProperty("witness", out var w) ? Enum.Parse<P>(w.GetString()!) : null,
                    e.TryGetProperty("truth", out var t) ? Enum.Parse<P>(t.GetString()!) : null);
            case "state": return new State(S("thing"), new Tag(S("tag")), S("cmp"), e.GetProperty("n").GetInt32());
            case "saw_key": return new Saw(S("thing"), new Tag(S("tag")), false);
            case "saw_pos": return new Saw(S("thing"), new Tag(S("tag")), true);
            case "looked": return new Looked(S("thing"));
            case "asked": return new Asked(Enum.Parse<P>(S("who")));
            case "rule_known": return new RuleKnown(S("rule"));
            case "trace_known": return new TraceKnown(e.GetProperty("trace").GetInt32());
            default: throw new InvalidDataException("未知条件：" + S("op"));
        }
    }

    public sealed record True : Cond { public override bool Eval(Game g, Person? who = null) => true; }
    public sealed record Not(Cond Item) : Cond { public override bool Eval(Game g, Person? who = null) => !Item.Eval(g, who); }
    public sealed record All(Cond[] Items) : Cond { public override bool Eval(Game g, Person? who = null) => Items.All(i => i.Eval(g, who)); }
    public sealed record Any(Cond[] Items) : Cond { public override bool Eval(Game g, Person? who = null) => Items.Any(i => i.Eval(g, who)); }

    /// <summary>人物行动记录（NightTruth 里“D{日} {人} {行动说明}”开头的条数）达到下限。</summary>
    public sealed record NightDid(int Day, P Who, Job[] Jobs, int Min) : Cond
    {
        public override bool Eval(Game g, Person? who = null)
        {
            var prefixes = Jobs.Select(j => $"D{Day} {Names.Of(Who)} {Names.Of(j)}").ToArray();
            return g.NightTruth.Count(t => prefixes.Any(p => t.StartsWith(p, StringComparison.Ordinal))) >= Min;
        }
    }

    public sealed record Sighting(int Day, Place At, Build? Build, P? Witness, P? Truth) : Cond
    {
        public override bool Eval(Game g, Person? who = null) => g.Sightings.Any(s => s.Day == Day && s.At == At && (Build is null || s.Build == Build)
            && (Witness is null || s.Witness == Witness) && (Truth is null || s.Truth == Truth));
    }

    public sealed record State(string Thing, Tag Tag, string Cmp, int N) : Cond
    {
        public override bool Eval(Game g, Person? who = null)
        {
            var v = Game.Get(Chapter2Bindings.ResolveId(g, Thing), Tag);
            return Cmp switch { ">=" => v >= N, "<=" => v <= N, "<" => v < N, ">" => v > N, "==" => v == N, "!=" => v != N, _ => throw new InvalidDataException(Cmp) };
        }
    }

    /// <summary>自案发日起亲见：key = 只要看到过这个标记；positive = 看到它的数值大于 0。</summary>
    public sealed record Saw(string Thing, Tag Tag, bool Positive) : Cond
    {
        public override bool Eval(Game g, Person? who = null)
        {
            var p = who ?? g.Det;
            var e = Chapter2Bindings.ResolveId(g, Thing);
            var since = g.DayStart(Game.CaseDay);
            return p.World.History.Any(h => h.Source == ObservationSource.Perception && ReferenceEquals(h.Target, p.M[e]) && h.Time >= since
                && (Positive ? h.Tags.GetValueOrDefault(Tag) > 0 : h.Tags.ContainsKey(Tag)));
        }
    }

    public sealed record Looked(string Thing) : Cond
    {
        public override bool Eval(Game g, Person? who = null) => g.DetLooked.Contains(Chapter2Bindings.ResolveId(g, Thing));
    }

    public sealed record Asked(P Who) : Cond { public override bool Eval(Game g, Person? who = null) => g.DetAsked.Contains(Who); }

    public sealed record RuleKnown(string Rule) : Cond
    {
        public override bool Eval(Game g, Person? who = null) => (who ?? g.Det).Acc.ContainsKey(g.RuleById(Rule));
    }

    public sealed record TraceKnown(int Trace) : Cond { public override bool Eval(Game g, Person? who = null) => g.DetEvidence().Contains(Trace); }
}

/// <summary>剧本里声明的规矩：内容、立规者、价值标记、张贴时间（Core 的 Rule 由代码按编号编译）。</summary>
public sealed record DesignRule(string Id, string Name, string Text, P? Author, bool ByDemon, RF Flags, P? Target, int[]? Card, IReadOnlyList<DesignPost> Posts);
public sealed record DesignPost(int Day, Slot Slot, Place Place, string Mode);

public sealed record CardOption(string Text, string Id, RF Flags, RF IfAny, RF ElseFlags);

/// <summary>规矩格的各格选项与价值标记（读自剧本的“规矩格”）。</summary>
public static class CardKit
{
    private static readonly Lazy<Data> _data = new(Load, true);
    public static IReadOnlyList<string> Subjects => _data.Value.Subjects;
    public static IReadOnlyList<CardOption> Conds => _data.Value.Conds;
    public static IReadOnlyList<CardOption> Behaviors => _data.Value.Behaviors;
    public static IReadOnlyList<CardOption> Exceptions => _data.Value.Exceptions;
    public static IReadOnlyList<CardOption> Accounts => _data.Value.Accounts;
    public static IReadOnlyList<(Card Card, string Text)> CaseWordings => _data.Value.CaseWordings;
    public static IReadOnlyList<(Card Card, string Text)> ArchiveWordings => _data.Value.ArchiveWordings;
    /// <summary>代码按编号认行为；剧本里行为的先后改了，这里立即报错而不是悄悄串位。</summary>
    private static readonly string[] BehaviorIds = ["取粮", "按人头", "交钥匙", "指控须证", "转述", "开库各取", "开库委员会", "开库封闭", "开库约束", "限制入院", "温室"];

    private sealed class Data
    {
        public string[] Subjects = [];
        public CardOption[] Conds = [], Behaviors = [], Exceptions = [], Accounts = [];
        public (Card, string)[] CaseWordings = [], ArchiveWordings = [];
    }

    private static RF Flags(JsonElement list)
    {
        var f = RF.None;
        foreach (var item in list.EnumerateArray()) f |= Enum.Parse<RF>(item.GetString()!);
        return f;
    }

    private static CardOption[] Options(JsonElement kit, string axis) => [.. kit.GetProperty(axis).EnumerateArray().Select(o => new CardOption(
        o.GetProperty("text").GetString()!, o.TryGetProperty("id", out var id) ? id.GetString()! : "", Flags(o.GetProperty("flags")),
        o.TryGetProperty("if_any", out var ifAny) ? Flags(ifAny) : RF.None, o.TryGetProperty("else_flags", out var other) ? Flags(other) : RF.None))];

    public static Card ToCard(IReadOnlyList<int> cells) => new(cells[0], cells[1], cells[2], cells[3], cells[4]);

    private static Data Load()
    {
        var kit = Chapter2Design.Section("card_kit");
        var data = new Data
        {
            Subjects = [.. kit.GetProperty("subjects").EnumerateArray().Select(o => o.GetProperty("text").GetString()!)],
            Conds = Options(kit, "conds"), Behaviors = Options(kit, "behaviors"), Exceptions = Options(kit, "exceptions"), Accounts = Options(kit, "accounts"),
        };
        if (!data.Behaviors.Select(b => b.Id).SequenceEqual(BehaviorIds)) throw new InvalidDataException("规矩格的行为顺序与代码认的编号不符：" + string.Join("、", data.Behaviors.Select(b => b.Id)));
        (Card, string)[] Wordings(string name) => [.. kit.GetProperty("wordings").GetProperty(name).EnumerateArray()
            .Select(w => (ToCard([.. w.GetProperty("card").EnumerateArray().Select(c => c.GetInt32())]), w.GetProperty("text").GetString()!))];
        data.CaseWordings = Wordings("case");
        data.ArchiveWordings = Wordings("archive");
        return data;
    }
}

/// <summary>规矩声明表（读自剧本的“规矩”块）。</summary>
public static class RuleDesign
{
    private static readonly Lazy<IReadOnlyDictionary<string, DesignRule>> _byId = new(() =>
    {
        var map = new Dictionary<string, DesignRule>();
        foreach (var e in Chapter2Design.Section("rules").EnumerateArray())
        {
            var posts = e.TryGetProperty("posts", out var list)
                ? [.. list.EnumerateArray().Select(p => new DesignPost(p.GetProperty("day").GetInt32(), Enum.Parse<Slot>(p.GetProperty("slot").GetString()!),
                    Enum.Parse<Place>(p.GetProperty("place").GetString()!), p.GetProperty("mode").GetString()!))]
                : new List<DesignPost>();
            var flags = RF.None;
            foreach (var f in e.GetProperty("flags").EnumerateArray()) flags |= Enum.Parse<RF>(f.GetString()!);
            var id = e.GetProperty("id").GetString()!;
            map[id] = new(id, e.GetProperty("name").GetString()!, e.GetProperty("text").GetString()!,
                e.TryGetProperty("author", out var a) ? Enum.Parse<P>(a.GetString()!) : null, e.TryGetProperty("demon", out var d) && d.GetBoolean(), flags,
                e.TryGetProperty("target", out var t) ? Enum.Parse<P>(t.GetString()!) : null,
                e.TryGetProperty("card", out var c) ? [.. c.EnumerateArray().Select(x => x.GetInt32())] : null, posts);
        }
        return map;
    });
    public static IReadOnlyCollection<DesignRule> All => _byId.Value.Values.ToArray();
    public static DesignRule Of(string id) => _byId.Value.TryGetValue(id, out var rule) ? rule : throw new InvalidDataException("chapter_01.story 缺少规矩：" + id);
}

/// <summary>《少了一份》：痕迹、事实、重演变量、对质的主张与证据（读自剧本的“案件”）。</summary>
public static class CaseDesign
{
    /// <summary>一句对质里能说的话。Topic：它回答哪一问（take 取用、latch 门闩、ledger 台账、any 哪一问都能说）；
    /// Person/Template：由 {人} 模板展开时，说的是谁、原模板文字（界面先选“什么事”，再选“是谁”）。</summary>
    public sealed record Claim(string Text, Cond True, string Reaction, int[] Relevant, bool Frames, string Topic, P? Person, string? Template);
    public sealed record Evidence(string Key, string Name, Cond Have, Cond? Saw, P? Person, Place? Dorm);
    public sealed record Trace(string Id, string Label, string Where, Cond Known);
    private sealed class Data
    {
        public int Night;
        public Trace[] Traces = [];
        public Dictionary<string, Cond> Facts = [];
        public (string Question, string[] Options)[] Variables = [];
        public Claim[] Claims = [];
        public Evidence[] Evidence = [];
    }
    private static readonly Lazy<Data> _data = new(Load, true);
    private static Data D => _data.Value;
    public static int Night => D.Night;
    public static IReadOnlyList<Trace> Traces => D.Traces;
    public static Cond Fact(string name) => D.Facts.TryGetValue(name, out var c) ? c : throw new InvalidDataException("chapter_01.story 缺少案件事实：" + name);
    public static IReadOnlyList<(string Question, string[] Options)> Variables => D.Variables;
    public static IReadOnlyList<Claim> Claims => D.Claims;
    public static IReadOnlyList<Evidence> EvidenceList => D.Evidence;
    /// <summary>按意思找主张：模板文字（或整句）与所说的人。找不到返回 -1。</summary>
    public static int ClaimIndex(string templateOrText, P? person = null) =>
        D.Claims.Select((c, i) => (c, i)).FirstOrDefault(x => (x.c.Template ?? x.c.Text) == templateOrText && x.c.Person == person, (null!, -1)).Item2;
    /// <summary>按编号找证据（模板展开的编号是“原编号:人物”或“原编号:寝区”）。找不到返回 -1。</summary>
    public static int EvidenceIndex(string key) => D.Evidence.Select((e, i) => (e, i)).FirstOrDefault(x => x.e.Key == key, (null!, -1)).Item2;

    private static Data Load()
    {
        var e = Chapter2Design.Section("case");
        var data = new Data { Night = e.GetProperty("night").GetInt32() };
        data.Traces = [.. e.GetProperty("traces").EnumerateArray().Select(t => new Trace(t.GetProperty("id").GetString()!, t.GetProperty("label").GetString()!,
            t.GetProperty("where").GetString()!, Cond.Parse(t.GetProperty("known"))))];
        foreach (var f in e.GetProperty("facts").EnumerateObject()) data.Facts[f.Name] = Cond.Parse(f.Value);
        data.Variables = [.. e.GetProperty("variables").EnumerateArray().Select(v => (v.GetProperty("question").GetString()!,
            v.GetProperty("options").EnumerateArray().Select(o => o.GetString()!).ToArray()))];
        data.Evidence = [.. e.GetProperty("evidence").EnumerateArray().Select(v => new Evidence(v.GetProperty("key").GetString()!, v.GetProperty("name").GetString()!,
            Cond.Parse(v.GetProperty("have")), v.TryGetProperty("saw", out var saw) ? Cond.Parse(saw) : null,
            v.TryGetProperty("person", out var vp) ? Enum.Parse<P>(vp.GetString()!) : null, v.TryGetProperty("dorm", out var vd) ? Enum.Parse<Place>(vd.GetString()!) : null))];
        data.Claims = [.. e.GetProperty("claims").EnumerateArray().Select(c => new Claim(c.GetProperty("text").GetString()!, Cond.Parse(c.GetProperty("true")),
            c.GetProperty("reaction").GetString()!, [.. c.GetProperty("relevant").EnumerateArray().Select(x => x.GetInt32())], c.TryGetProperty("frames", out var fr) && fr.GetBoolean(),
            c.GetProperty("topic").GetString()!, c.TryGetProperty("person", out var cp) ? Enum.Parse<P>(cp.GetString()!) : null,
            c.TryGetProperty("template", out var ct) ? ct.GetString() : null))];
        return data;
    }
}
