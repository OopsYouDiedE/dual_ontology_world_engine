using DualOntologyWorldEngine.Story;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>第二章自动回归探针；游玩路径不经过这里。</summary>
public sealed partial class Chapter2InteractionSession : IChapterProbe
{
    public object? Probe(string name, object?[] args) => name switch
    {
        "first_day" => Game.FirstDay, "case_day" => Game.CaseDay, "last_day" => Game.LastDay, "ended" => _game.Ended, "next_day" => _game.Day, "next_slot" => (int)_game.Slot,
        "valid_confront_pairs" => _game.ValidConfrontPairs().Select(p => (object?)new object?[] { p.Claim, p.Evidence }).ToArray(),
        "confront_top_id" => TopId(Convert.ToInt32(args[0])),
        "known_rules" => _game.DetKnownSpecs().Select(s => (object?)new Dictionary<string, object?> { ["id"] = s.Id, ["text"] = s.Text, ["tellable"] = _game.HostTellable().Contains(s) }).ToArray(),
        "people" => LegacySnapshot()["people"],
        "accused" => _game.Accused is { } accused ? Names.Of(accused) : "",
        _ => throw new ArgumentException("未知探针：" + name),
    };
}
