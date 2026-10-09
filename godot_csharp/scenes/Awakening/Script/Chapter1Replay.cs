using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Awakening;

public sealed partial class AwakeningContentSession
{
    /// <summary>Execute a real player-visible replay; predicted worlds never call this boundary.</summary>
    // STORY-HARDCODE[AWK-061][logic][production]: 本章实播重演将固定六项变量、吻合判断和角色见证写入玩家进度。
    public ImmutableArray<Finding> RunVisibleReplay(MorningWorld world, NightVariables variables)
    {
        RecordReplayRun();
        var result = NightReplay.Run(variables);
        var compared = Comparison.Compare(result.Traces, MorningTraces.Evidence, id => Session.Has("examined", id));
        // 没查过的现场不算吻合：推演再对，也只是猜中。
        var checkedMatch = result.FullMatch && compared.All(f => f.Status == FindingStatus.Match);
        WitnessReplay(world, variables, checkedMatch);
        var values = new object[] { (int)variables.V1, (int)variables.V2, (int)variables.V3,
            (int)variables.V4, (int)variables.V5, (int)variables.V6 };
        var state = Session.GetProgress("state");
        state["replay_vars"] = values;
        state["last_run_vars"] = values;
        state["replay_count"] = Convert.ToInt32(state["replay_count"]) + 1;
        var replay = Session.GetProgress("replay_result");
        replay.Clear();
        replay["full_match"] = checkedMatch;
        replay["detective_bolted"] = result.DetectiveBoltedOnHuoRequest;
        replay["detective_refused_stranger"] = result.DetectiveRefusedStranger;
        replay["frames"] = result.Frames.Select(f => (object)new Dictionary<string, object>
        {
            ["t"] = f.Time, ["actor"] = f.Actor, ["label"] = f.Label,
            ["event"] = new Dictionary<string, object>
            {
                ["kind"] = f.Event.Kind, ["who"] = f.Event.Who is { } who ? Names.PersonKeys[(int)who] : "",
                ["subject"] = f.Event.Subject is { } subject ? Names.PersonKeys[(int)subject] : "",
                ["object"] = f.Event.Object, ["room"] = f.Event.Room is { } room ? Names.RoomKeys[(int)room] : "",
            },
            ["thoughts"] = f.Thoughts.ToDictionary(p => Names.PersonKeys[(int)p.Key], p => (object)p.Value)
        }).ToArray();
        // Actual facts belong to the public replay host, not the subjective search model.
        // Uninspected cards remain Unvisited and provide only their public location hint.
        return compared;
    }
}
