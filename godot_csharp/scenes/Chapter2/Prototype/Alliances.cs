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

    private void WriteAllianceRules()
    {
        // 劳作互助派（沈鼎）：钥匙交给做饭的人，统一煮，先喂饿着的人。接受者若手里有钥匙，就会交出去。
        ShenKitchen = NewSpec("粮仓钥匙交给做饭的人，统一煮粥，先喂饿着的人。", P.Shen, RF.CentralCook | RF.Rations | RF.SickPriority | RF.Records,
            fx => [new(Fx.All(fx.AtAny([Slot.Noon, Slot.Evening]), Fx.Mine(T.GranaryKey), Fx.Not(Fx.Mine(T.Is[P.Shen]))), RuleMode.Prefer, fx.GiveKeyTo(this[P.Shen]), 4)]);
        // 实证派（维罗妮卡）：指控要有实物。
        VeronicaEvidence = NewSpec("指控须有实物为证；取用须有记录。", P.Veronica, RF.EvidenceFirst | RF.Records,
            fx => [new(Fx.Not(Fx.Mine(T.SawAct)), RuleMode.Forbid, fx.Job(Job.Accuse), 3)]);
        // 感官与美派（卢西安）：不得以私取之名限制任何人。
        LucianNoBrand = NewSpec("未经查验，不得以“私取”之名限制任何人行走。", P.Lucian, RF.EvidenceFirst | RF.NightException,
            fx => [new(Fx.All(fx.At(Slot.Morning), Fx.Any([.. People.Select(o => fx.Tg(o.Body, T.Accused))])), RuleMode.Prefer, fx.Job(Job.Defend), 2)]);
        _agenda.AddRange([
            (2, P.Shen, ShenKitchen, Place.DormLife), (2, P.Veronica, VeronicaEvidence, Place.DormL), (3, P.Lucian, LucianNoBrand, Place.Lecture),
        ]);
    }

    /// <summary>傍晚：到期的聚落规则张贴出来；每个立规者当面拉两个在场、最信他、还没听过的人（人物也受注意力限制）。</summary>
    private void AllianceEvening()
    {
        foreach (var (day, author, spec, where) in _agenda.Where(a => a.Day == Day))
        {
            Post(spec, where);
            if (Where(Det) == where) Feed.Add($"{Names.Of(author)}在{Names.Of(where)}贴出：「{spec.Text}」");
        }
        foreach (var spec in Specs.Where(s => s.Author is { } a && a != P.Det && !s.ByDemon && s != RuanLedger && (s.PostedAt.Count > 0)))
        {
            var author = this[spec.Author!.Value];
            if (Get(author.Body, T.Sick) > 0) continue;
            var targets = People.Where(o => o != author && o != Det && Where(o) == Where(author) && !o.Acc.ContainsKey(spec))
                .OrderByDescending(o => TrustIn(o, author)).ThenBy(o => o.Id).Take(2).ToArray();
            foreach (var o in targets)
            {
                var v = Tell(o, spec, Channel.Face);
                Log.Add($"D{Day} 傍晚 {author.Name}当面拉{o.Name}认「{Short(spec)}」：{(v == Truth.True ? "认了" : "没认")}");
                if (Where(Det) == Where(author)) Feed.Add($"{author.Name}拉着{o.Name}说「{Short(spec)}」。{o.Name}{Voice(o, spec, v)}");
            }
        }
    }

    /// <summary>同寝的人立了我不认的规矩：彼此疏远一分（初始寝区从第 2 日就开始瓦解）。</summary>
    private void OnRejected(Person p, RuleSpec s)
    {
        if (s.Author is not { } a || a == P.Det || a == p.Id || Cast.Of(a).Group != p.Pro.Group) return;
        var author = this[a];
        Witness(author, $"{author.Name}立的规矩，我不认", -1, 0, [p], spread: false);
        Witness(p, $"{p.Name}不认我立的规矩", -1, 0, [author], spread: false);
    }

    private void OnAccepted(Person p, RuleSpec s) { }
}
