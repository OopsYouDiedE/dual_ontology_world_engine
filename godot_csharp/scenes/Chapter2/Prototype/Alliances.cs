using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>
/// §11 第 7 条：派系由人物自己的价值推动。各价值聚落的领头人自己立规、张贴、当面拉人；
/// 接受同一条规则的人彼此更近（Social.Bonds），同寝的人立了自己不认的规矩则彼此疏远（OnRejected）。
/// 侦探不在场时这一切照样发生。
/// </summary>
public sealed partial class Game
{
    public RuleSpec ShenKitchen = null!, VeronicaEvidence = null!, LucianNoBrand = null!;
    private readonly List<(int Day, P Author, RuleSpec Spec, Place Where)> _agenda = [];

    // STORY-HARDCODE[CH2-001][logic][production]: 聚落阵营规则的具体身份、时段和后果在代码中定义。
    private void WriteAllianceRules()
    {
        ShenKitchen = RuleById("ShenKitchen");
        VeronicaEvidence = RuleById("VeronicaEvidence");
        LucianNoBrand = RuleById("LucianNoBrand");
        // 各聚落领头人何时何处张贴自己的规矩，写在剧本的“规矩”块里（张贴：第 N 日 时段 地点）。
        foreach (var rule in RuleDesign.Read(AstDesign.GetProperty("rules")).Values.Where(r => r.Author is not null))
            foreach (var post in rule.Posts.Where(p => p.Mode == "单条"))
                _agenda.Add((post.Day, rule.Author!.Value, RuleById(rule.Id), post.Place));
    }

    /// <summary>傍晚：到期的聚落规则张贴出来；每个立规者当面拉两个在场、最信他、还没听过的人（人物也受注意力限制）。</summary>
    private void AllianceEvening()
    {
        // 到期的聚落规则成为立规者的打算，由他自己走去贴出（总纲 §六 第三期）。
        foreach (var (day, author, spec, where) in _agenda.Where(a => a.Day == Day))
            IntendPost(this[author], spec, where);
        // 拉人不再由作者每晚替立规者“拉两个”：立规者自己傍晚到配给台找同一聚落的人当面讲（GameJobs.TalkRules），占用他的时段。
    }

    /// <summary>同寝的人立了我不认的规矩：彼此疏远一分（初始寝区从第 2 日就开始瓦解）。</summary>
    private void OnRejected(Person p, RuleSpec s)
    {
        if (s.Author is not { } a || a == P.Det || a == p.Id || Cast.Of(a).Group != p.Pro.Group) return;
        var author = this[a];
        Witness(author, K("规矩被拒", author.Id, p.Id), -1, 0, [p], spread: false);
        Witness(p, K("规矩被拒", author.Id, p.Id), -1, 0, [author], spread: false);
    }

    private void OnAccepted(Person p, RuleSpec s) { }
}
