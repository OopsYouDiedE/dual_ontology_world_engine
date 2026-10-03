using System.Text;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed partial class Game
{
    public sealed record Note(int Day, P Who, string Kind, string Text);
    public readonly List<Note> DetNotes = [];
    /// <summary>只在调试模式显示的分项（接受理由的数值）。</summary>
    public readonly List<string> DebugLog = [];
    public void NoteFor(P who, string kind, string text) => DetNotes.Add(new(Day, who, kind, text));

    /// <summary>侦探见到的同处次数（每个时段计一次）：“他最近常与某某同行”。</summary>
    public readonly Dictionary<(P, P), int> SeenTogether = [];
    private (int, Slot) _companyNoted = (0, Slot.Morning);

    /// <summary>侦探在场时看到的“谁和谁在一处”（派系的可见迹象）。</summary>
    public void NoteCompany()
    {
        if (_companyNoted == (Day, Slot)) return;
        _companyNoted = (Day, Slot);
        var here = Where(Det);
        var others = People.Where(p => p != Det && Where(p) == here && !(Slot == Slot.Night && p.Asleep)).ToArray();
        if (others.Length < 2 || others.Length > 9 || Slot == Slot.Forenoon && here == Place.Lecture) return; // 饭点、对质、上课这种大家都在的场合看不出谁跟谁
        NoteFor(others[0].Id, "同处", $"{Names.Of(Slot)}在{Names.Of(here)}：{string.Join("、", others.Select(o => o.Name))}在一起");
        foreach (var a in others) foreach (var b in others.Where(b => b != a)) SeenTogether[(a.Id, b.Id)] = SeenTogether.GetValueOrDefault((a.Id, b.Id)) + 1;
    }

    /// <summary>侦探见过最常与此人同处的两个人。</summary>
    public IEnumerable<(P Who, int N)> CompanionsOf(P p) => SeenTogether.Where(kv => kv.Key.Item1 == p && kv.Value >= 2)
        .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key.Item2).Take(2).Select(kv => (kv.Key.Item2, kv.Value));

    // ------------------------------------------------------------ 立场 --

    private RuleSpec? _openV, _sealV, _commV;

    /// <summary>名录库的几种去向（还没写成规则的，用一份只供判断的影子方案，不进入任何人的认知）。</summary>
    public IEnumerable<RuleSpec> StancePlans()
    {
        _openV ??= new() { Id = -101, Text = "名录库各取其物：凡学员皆可取回己物。", ByDemon = true, Flags = RF.OpenArchive, Build = _ => [] };
        _sealV ??= new() { Id = -102, Text = "名录库封闭不启：回忆即上钩，谁也不开。", Author = P.Gu, Flags = RF.SealArchive | RF.Tradition, Build = _ => [] };
        _commV ??= new() { Id = -103, Text = "名录库由委员会保管：钥匙在委员会，取回须经委员会登记批准。", Author = P.Ward, Flags = RF.CommitteeArchive | RF.WardAuthority | RF.Records, Build = _ => [] };
        yield return _openPlan ?? _openV;
        yield return WardCommittee ?? _commV;
        yield return _sealPlan ?? _sealV;
        foreach (var s in Specs.Where(s => s.Card is { Behavior: >= 5 and <= 8 } && s.Author == P.Det)) yield return s;
    }

    /// <summary>此人此刻在名录库的事上最向着哪一边（与议事同一判断；没有一边够格则为 null）。</summary>
    public RuleSpec? StanceOf(Person p) => StancePlans().Select(s => (s, score: AcceptScore(p, s, Channel.Public, null, out _)))
        .Where(x => x.score >= Options.AcceptThreshold).OrderByDescending(x => x.score).ThenBy(x => x.s.Id).Select(x => x.s).FirstOrDefault();

    private readonly Dictionary<P, (int Day, RuleSpec? Spec)> _voicedStance = [];
    /// <summary>侦探最后一次听到的各人立场（自动检查用：立场可预读）。</summary>
    public IReadOnlyDictionary<P, (int Day, RuleSpec? Spec)> VoicedStance => _voicedStance;

    /// <summary>
    /// “接受规则”与“立场”分开呈现（§12 第 3 条）：第 4 日起，饭点和侦探同处的人，会顺口复述自己向着的那一边的话。
    /// 每人每天最多一次、每个时段最多两人；立场变了的先说。
    /// </summary>
    private void StanceTalk()
    {
        if (Day < 4 || Slot is not (Slot.Noon or Slot.Evening)) return;
        var here = Where(Det);
        var talkers = Npcs.Where(p => Where(p) == here && Get(p.Body, T.Sick) == 0 && _voicedStance.GetValueOrDefault(p.Id).Day != Day)
            .Select(p => (p, s: StanceOf(p))).Where(x => x.s is not null)
            .OrderByDescending(x => _voicedStance.TryGetValue(x.p.Id, out var v) && v.Spec?.Text != x.s!.Text ? 1 : _voicedStance.ContainsKey(x.p.Id) ? 0 : 2)
            .ThenBy(x => x.p.Id).Take(2).ToArray();
        foreach (var (p, s) in talkers)
        {
            _voicedStance[p.Id] = (Day, s);
            var (who, line) = s!.Author == P.Det ? ("你", $"你那条{LabelOf(s)}「{Short(s)}」，我看行。")
                : s.ByDemon ? ("恶魔", "凡学员皆可取回己物——它自己说的。我的东西在里面。")
                : s.Flags.HasFlag(RF.CommitteeArchive) ? ("沃德", "名录库得有人管、有账可查。沃德这话在理。")
                : ("顾平章", "回忆就是上钩。还是封着好，顾平章说得对。");
            Feed.Add($"{p.Name}在饭桌上说：“{line}”");
            NoteFor(p.Id, "立场", $"复述{who}的话：{line}");
        }
    }

    private void AfterSlot(P? first)
    {
        if (Slot == Slot.Night) return;
        FlushSeen();
        Disclose(first);
        StanceTalk();
    }
}

public static class Report
{
    public static string Image(Game g) => string.Join(" ", g.Npcs.Select(p => { var (c, i) = g.ImageOfDet(p); return $"{p.Name}{c:+0;-0;0}/{i:+0;-0;0}"; }));

    public static string ChapterEnd(Game g)
    {
        var sb = new StringBuilder();
        sb.AppendLine("【章节结束状态（交第三章）】");
        sb.AppendLine("各人心中的侦探（可信/见识）：" + Image(g));
        foreach (var f in g.Factions()) sb.AppendLine($"派系：领头 {Names.Of(f.Leader)}｜{string.Join("、", f.Members.Select(Names.Of))}｜共享规则：{string.Join("；", f.SharedRules)}");
        sb.AppendLine($"名录库：{Game.EndingText(g.Result)}");
        sb.AppendLine("钩与欲望：" + string.Join(" ", g.Npcs.Select(p => $"{p.Name}欲{p.Desire}钩{p.Hooks}")));
        sb.AppendLine("已学课程：" + string.Join(" ", g.People.Select(p => $"{p.Name}[{string.Join(",", p.Lessons.Select(l => l.Key + (l.Value ? "" : "?")))}]")));
        sb.AppendLine($"物资：口粮 {Game.Get(g.Granary, T.Ration)}，灯油 {Game.Get(g.OilStore, T.Oil)}，药品 {Game.Get(g.MedCab, T.Medicine)}；温室{(g.GreenhouseRepaired ? "已修好" : "未修好")}");
        sb.AppendLine($"第九条：{(g.SteleRubbed ? "已拓印发现" : "未发现")}{(g.Clause9Asked ? "，并向恶魔精确追问过" : "")}");
        sb.AppendLine($"《少了一份》：{(g.QuerCleared ? "雀儿洗清" : g.QuerOstracized ? "雀儿被排斥" : "未对质")}；新规则：{g.CaseRule?.Text ?? "无"}；共同规则：{(g.CommonRuleEstablished ? "成立" : "未成立")}");
        sb.AppendLine("操纵记录：" + (g.Manipulations.Count == 0 ? "无" : string.Join("；", g.Manipulations.Select(m => $"D{m.Day} 对{Names.Of(m.Target)}：{m.Means}{(m.Exposed ? "（已揭穿）" : "")}"))));
        sb.Append("三组旧怨：原型未实现（见报告）");
        return sb.ToString();
    }
}
