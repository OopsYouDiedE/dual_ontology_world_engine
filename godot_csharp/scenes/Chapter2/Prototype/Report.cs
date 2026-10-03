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

    /// <summary>侦探在场时看到的“谁和谁在一处”（派系的可见迹象）。</summary>
    public void NoteCompany()
    {
        var here = Where(Det);
        var others = People.Where(p => p != Det && Where(p) == here).ToArray();
        if (others.Length >= 2) NoteFor(others[0].Id, "同处", $"{Names.Of(Slot)}在{Names.Of(here)}：{string.Join("、", others.Select(o => o.Name))}在一起");
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
