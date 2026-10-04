using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed record InvestigationEntry(string Id, string Name, string Place, string Kind,
    string[] Facts, string[] Sources, string ImageKey);
public sealed record ReplayQuestion(string Question, string Context, string[] Options, int[] Choices);
public sealed record InspectionRecord(int Day, Slot Slot, string Key, Place Place, string Text);

public sealed partial class Game
{
    public readonly List<InspectionRecord> InspectionRecords = [];
}

/// <summary>将侦探已经读到的事实投影为宿主可展示的记录；不推进世界或补充假设。</summary>
public static class InvestigationPresentation
{
    public static InvestigationEntry[] Entries(Game g)
    {
        var entries = new List<InvestigationEntry>();
        foreach (var place in g.InspectionRecords.GroupBy(record => record.Place))
        {
            var last = place.Last();
            var inspection = place.Where(record => record.Day == last.Day && record.Slot == last.Slot).ToArray();
            var first = inspection[0];
            entries.Add(new("inspection:" + place.Key, first.Key, Names.Of(place.Key), "物证",
                inspection.Select(record => record.Text).ToArray(), [InspectionSource(first)], first.Key));
        }
        foreach (var records in g.InspectionRecords.GroupBy(record => record.Key))
        {
            var last = records.Last();
            entries.Add(new("thing:" + last.Key, last.Key, Names.Of(last.Place), "物证",
                [last.Text], [InspectionSource(last)], last.Key));
        }
        foreach (var night in g.DawnTraces.Keys.Order())
            foreach (var trace in g.KnownTraces(night).Values)
                entries.Add(new($"trace:{night}:{trace.Key}", trace.Key, Names.Of(trace.Where), "物证",
                    [trace.Text], [$"第{night}日夜里之后，你已读到的痕迹"], TraceImage(g, trace)));

        var det = g.Det;
        foreach (var p in g.Npcs)
        {
            var them = det.M[p.Body];
            var seen = them.Parent?.Name ?? "不确定";
            var sick = Game.Belief(det, them, T.Sick) > 0;
            var facts = new List<string> { $"· {p.Name}（{Names.Of(p.Pro.Group)}）最后见到在{seen}。{(sick ? "病着。" : "")}" };
            var rules = g.DetNotes.Where(x => x.Who == p.Id && x.Kind is "规矩" or "规则").TakeLast(12).ToArray();
            if (rules.Length > 0) facts.Add("规矩：" + string.Join("；", rules.Select(x => $"第{x.Day}日 {x.Text}")));
            var stance = g.DetNotes.Where(x => x.Who == p.Id && x.Kind == "立场").TakeLast(6).ToArray();
            if (stance.Length > 0) facts.Add("立场：" + string.Join("；", stance.Select(x => $"第{x.Day}日 {x.Text}")));
            var comp = g.CompanionsOf(p.Id).ToArray();
            if (comp.Length > 0) facts.Add("同行：常与 " + string.Join("、", comp.Select(c => $"{Names.Of(c.Who)}（{c.N} 次）")) + " 同处");
            foreach (var x in g.DetNotes.Where(x => x.Who == p.Id && x.Kind is not ("规矩" or "规则" or "立场" or "同处")).TakeLast(8))
                facts.Add($"第{x.Day}日 {x.Text}");
            var about = det.World.History.Where(h => h.Target.Kind == ProjectionKind.Event && h.Relations.Contains((Relations.Subject, them)) && h.Details != "")
                .Select(h => $"{h.Details}（{SourceText(h.Source)}）").Distinct().TakeLast(6).ToArray();
            foreach (var x in about) facts.Add("听说/看到：" + x);
            if (them.Parent is null && facts.Count == 1 && about.Length == 0) continue;
            entries.Add(new("person:" + p.Id, p.Name, them.Parent?.Name ?? "人际笔记", "人际",
                facts.ToArray(), ["你对" + p.Name + "的笔记"], p.Id.ToString()));
        }

        foreach (var (place, board) in g.Board)
        {
            var mental = det.M[board];
            if (!det.World.History.Any(h => ReferenceEquals(h.Target, mental))) continue;
            var known = g.Specs.Where(s => s.PostedAt.Contains(place) && s != g.RuanLedger)
                .Where(s => mental.Rules.Any(r => s.Build(g.FxOf(det)).Any(m => m.Mode == r.Mode && Ast.Equivalent(m.Effect, r.Effect) && Ast.Equivalent(m.Condition, r.Condition)))).ToArray();
            entries.Add(new("board:" + place, Names.Of(place) + "公告板", Names.Of(place), "规矩",
                known.Length == 0 ? ["你上次看时是空的。"] : known.Select(s => "「" + s.Text + "」").ToArray(),
                ["你看过的" + Names.Of(place) + "公告板"], Game.Things_.First(t => ReferenceEquals(t.Value.Thing(g), board)).Key));
        }
        foreach (var (label, spec) in g.PreviewKnownRules())
            entries.Add(new("rule:" + spec.Id, "规矩" + label,
                spec.PostedAt.Count > 0 ? string.Join("、", spec.PostedAt.Select(Names.Of)) : "规矩笔记", "规矩",
                [spec.Text], [g.SourceOf(spec)], ""));
        foreach (var (id, heard) in det.News.OrderBy(kv => kv.Key))
            entries.Add(new("news:" + id, heard.Hops == 0 ? $"第{heard.Day}日的亲见" : $"{Names.Of(heard.From)}的转述（第{heard.Day}日）", "消息笔记", "听闻", [heard.Text],
                [heard.Hops == 0 ? "亲见" : $"听{Names.Of(heard.From)}说" + (heard.Hops > 1 ? "（转了几道手）" : "")], ""));

        var self = new List<string>
        {
            $"第{g.Day}日{Names.Of(g.Slot)}。你在{Names.Of(g.Where(det))}。{(Game.Get(det.Body, T.Hungry) > 0 ? "你饿着（下午做不了事）。" : "")}今天{(Game.Get(det.Body, T.Fed) > 0 ? "已经" : "还没")}领到口粮。",
            $"学过的课：{(det.Lessons.Count == 0 ? "无" : string.Join("、", det.Lessons.Select(l => $"《{Game.Lessons[l.Key].Title}》{(l.Value ? "" : "（听人转述，可能走样）")}")))}"
        };
        if (g.Day >= 3)
        {
            var ev = g.DetEvidence();
            self.Add("台账那件事，你查过的：" + (ev.Count == 0 ? "还没有" : string.Join("、", ev.Select(i => CaseTraces.Labels[i]))) + (g.DetAsked.Count > 0 ? "；当面问过：" + string.Join("、", g.DetAsked.Select(Names.Of)) : ""));
        }
        if (g.Accused is { } acc) self.Add(g.QuerCleared ? $"{Names.Of(acc)}已经洗清。" : g.QuerOstracized ? $"{Names.Of(acc)}被当成私取之人，搬出了寝区。" : "沃德的指控还悬着。");
        foreach (var (who, due, done) in g.Promises.Where(p => !p.Done)) self.Add($"答应过：第{due}日前替{Names.Of(who)}办一件事");
        foreach (var (w, right) in g.Wagers) self.Add($"赌约：第{w.Day}日和{Names.Of(w.With)}赌{Names.Of(w.Who)}夜里去{Names.Of(w.Where)}——{(right is null ? "待揭晓" : right.Value ? "说中" : "没说中")}");
        if (g.SteleRubbed) self.Add("你读过石碑上的第九、第十条。");
        entries.Add(new("self", "自己的记事", Names.Of(g.Where(det)), "自知", self.ToArray(), ["自知"], ""));
        return entries.ToArray();
    }

    public static ReplayQuestion[] Questions(Game g, int night)
    {
        if (g.Day < 3 || night < 2 || night >= g.Day) return [];
        var menu = g.ReplayMenu(night, preview: true);
        var known = g.KnownTraces(night);
        return menu.Select((option, index) => (Option: option, Index: index))
            .GroupBy(x => x.Option.Rule is { } rule ? $"rule:{rule.Who}:{rule.Spec}" : x.Option.Where is { } where ? $"place:{where.Who}:{where.Where}" : "")
            .Where(group => group.Key.Length > 0)
            .Select(group =>
            {
                var first = group.First().Option;
                string question, context;
                if (first.Rule is { } rule)
                {
                    var spec = g.Specs.First(s => s.Id == rule.Spec);
                    question = Names.Of(rule.Who) + "是否接受这条规矩";
                    context = spec.Text + "——" + g.SourceOf(spec);
                }
                else
                {
                    var where = first.Where!;
                    question = Names.Of(where.Who) + "昨夜是否在" + Names.Of(where.Where);
                    context = string.Join("\n", known.Values.Where(t => t.Where == where.Where).Select(t => t.Text));
                }
                return new ReplayQuestion(question, context, ["暂不设定", .. group.Select(x => x.Option.Text)], [0, .. group.Select(x => x.Index + 1)]);
            }).ToArray();
    }

    private static string TraceImage(Game g, TraceItem trace)
    {
        if (Game.Things_.ContainsKey(trace.Key)) return trace.Key;
        if (trace.Key == "门闩") return "粮仓";
        if (trace.Key is "前厅脚印" or "前厅机关") return "名录库之门";
        if (trace.Key.StartsWith("饼屑:"))
            return Game.Things_.FirstOrDefault(kv => kv.Value.Place == trace.Where && ReferenceEquals(kv.Value.Thing(g), g.Litter.GetValueOrDefault(trace.Where))).Key ?? "";
        return "";
    }

    private static string SourceText(ObservationSource source) => source switch
    {
        ObservationSource.Perception => "亲见", ObservationSource.Testimony => "听说", ObservationSource.Recall => "回忆", ObservationSource.Inference => "推断",
        ObservationSource.Corruption => "恶魔之言", _ => "自知"
    };

    private static string InspectionSource(InspectionRecord record)
        => $"第{record.Day}日{Names.Of(record.Slot)}，你在{Names.Of(record.Place)}亲自细看。";
}
