using DualOntologyWorldEngine.Awakening;
using DualOntologyWorldEngine.Chapter2.Prototype;
using DualOntologyWorldEngine.Story;
using static DualOntologyWorldEngine.Story.ChapterEvents;

namespace DualOntologyWorldEngine.Gameplay;

/// <summary>连续游戏的流程所有者。宿主只执行这里给出的视图、界面和空间事件。</summary>
public sealed class GameSession(string campusScript, string livingView, string campusView)
{
    private IChapterSession? _session;
    private bool _waitingForView;

    public IReadOnlyList<Dictionary<string, object?>> Dispatch(IReadOnlyDictionary<string, object?> input)
    {
        var kind = Text(input, "kind");
        if (kind == "start") return Start();
        if (kind == "navigate")
        {
            var destination = Text(input, "to");
            if (destination == "restart") return Start();
            if (destination == "title")
            {
                _session = null;
                _waitingForView = false;
                ChapterWorldContinuity.Clear();
                return [Event("home")];
            }
            if (destination is "continue" or "chapter2") return Continue();
            throw new InvalidOperationException("未知游戏去处：" + destination);
        }
        if (_session is null) throw new InvalidOperationException("请先开始游戏。");
        if (kind == "view_ready")
        {
            if (!_waitingForView) return [];
            _waitingForView = false;
            var begin = input.ToDictionary(p => p.Key, p => p.Value);
            begin["kind"] = "begin";
            return Normalize(_session.Dispatch(begin));
        }
        if (_waitingForView) return [];
        if (kind == "travel")
        {
            var nodeId = Text(input, "node_id");
            var campus = Map(_session.Snapshot(), "campus");
            var node = Map(Map(campus, "nodes"), nodeId);
            var known = Map(campus, "known");
            if (node.Count == 0 || !Flag(known, nodeId) || _session.Snapshot().GetValueOrDefault("field_enabled") is not true)
                return [Event("error", ("message", "这个地点尚未开放。"))];
            return [Event("travel", ("node", node))];
        }
        return Normalize(_session.Dispatch(input));
    }

    public IReadOnlyDictionary<string, object?> Snapshot()
        => _session is null ? new Dictionary<string, object?>() : Present(_session.Snapshot());

    private IReadOnlyList<Dictionary<string, object?>> Start()
    {
        _session = new Chapter1Session();
        _waitingForView = true;
        return [Event("view", ("resource", livingView)), Event("state", ("snapshot", Snapshot()))];
    }

    private IReadOnlyList<Dictionary<string, object?>> Continue()
    {
        if (_session is not Chapter1Session || _session.Snapshot().GetValueOrDefault("ended") is not true)
            throw new InvalidOperationException("当前会话尚未离开宿舍。");
        var live = ChapterWorldContinuity.Current
            ?? throw new InvalidOperationException("缺少正在运行的世界，无法继续。");
        var options = Chapter2AuthoredProfiles.Apply(campusScript);
        _ = new Chapter2Narrative(campusScript, true);
        Chapter2InteractionSession.ValidateScript(campusScript);
        foreach (var character in live.Characters.Values)
            if (character.Self is null || live.Runtime.Representations(character.Body).Count != 1
                || !ReferenceEquals(live.Runtime.KnownProjection(character.Soul, character.Body), character.Self)
                || live.Simulation.IsActing(character.Self))
                throw new InvalidOperationException("人物的运行中引用不完整，或仍在行动。");
        var game = new DualOntologyWorldEngine.Chapter2.Prototype.Game(options, live,
            WorldScriptDocument.Parse(campusScript, allowChapterAdapter: true).Single());
        _session = new Chapter2InteractionSession(game, campusScript);
        ChapterWorldContinuity.Clear();
        _waitingForView = true;
        return [Event("view", ("resource", campusView), ("map", "living_hall"), ("spawn", "east_south")),
            Event("state", ("snapshot", Snapshot()))];
    }

    private static Dictionary<string, object?> Present(IReadOnlyDictionary<string, object?> snapshot)
    {
        var result = snapshot.ToDictionary(p => p.Key, p => p.Value);
        result.Remove("chapter");
        result["presentation"] = new Dictionary<string, object?> { ["window_title"] = "安眠者有福" };
        return result;
    }

    private static IReadOnlyList<Dictionary<string, object?>> Normalize(IReadOnlyList<Dictionary<string, object?>> events)
    {
        foreach (var ev in events)
            if (Text(ev, "kind") == "state") ev["snapshot"] = Present(Map(ev, "snapshot"));
        return events;
    }
}
