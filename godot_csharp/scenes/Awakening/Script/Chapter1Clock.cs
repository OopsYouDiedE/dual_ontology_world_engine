using System.Text.Json;

namespace DualOntologyWorldEngine.Awakening;

public sealed record Chapter1Timing(string Kind, string Id, int StartMinutes, int EndMinutes)
{
    public int Minutes => EndMinutes - StartMinutes;
}

/// <summary>The real first-day clock. Reading and UI polling never advance it or the night replay CTB.</summary>
public sealed partial class AwakeningContentSession
{
    private readonly List<Chapter1Timing> _timing = [];
    private JsonElement Clock => Content.RootElement.GetProperty("DAY_CLOCK");
    public int GameMinutes => Convert.ToInt32(Session.GetProgress("clock").GetValueOrDefault("game_minutes", Clock.GetProperty("start_minutes").GetInt32()));
    public int ExpectedExitMinutes => Clock.GetProperty("expected_exit_minutes").GetInt32();
    // 过了午夜就从 00:00 接着走；第一章只有一天，钟面不显示 24 点以后。
    public string ClockText => $"{GameMinutes / 60 % 24:00}:{GameMinutes % 60:00}";
    public string ExpectedExitText => $"{ExpectedExitMinutes / 60:00}:{ExpectedExitMinutes % 60:00}";

    private void AdvanceClock(string kind, string id, int minutes)
    {
        var start = GameMinutes;
        var end = checked(start + minutes);
        Session.Set("clock", "game_minutes", end);
        Session.Set("clock", "expected_exit_minutes", ExpectedExitMinutes);
        _timing.Add(new(kind, id, start, end));
    }

    /// <summary>Settle a scene once at its first real presentation; reopening the same prose is free.</summary>
    public void PresentScene(string scene)
    {
        if (scene.StartsWith('@') || Session.Has("timed_scenes", scene)) return;
        var scenes = Clock.GetProperty("scenes");
        var minutes = scenes.TryGetProperty(scene, out var duration) ? duration.GetInt32()
            : scene.StartsWith("talk_", StringComparison.Ordinal) ? Clock.GetProperty("actions").GetProperty("交谈").GetInt32()
            : scene.StartsWith("help_", StringComparison.Ordinal) ? Clock.GetProperty("actions").GetProperty("协助").GetInt32()
            : Clock.GetProperty("default_scene_minutes").GetInt32();
        Session.Set("timed_scenes", scene, true);
        AdvanceClock("scene", scene, minutes);
    }

    /// <summary>A fresh performed action, including unsuccessful retries. Never call for a menu or a cached result.</summary>
    public void ConsumeAction(string action, string id = "")
        => AdvanceClock("action", action + (id == "" ? "" : ":" + id), Clock.GetProperty("actions").GetProperty(action).GetInt32());

    public void RecordReplayRun() => ConsumeAction("重演");
    public void RecordSoundTest() => ConsumeAction("声路");

    public void RecordDoorOpen()
    {
        if (!Session.Has("clock", "door_open_minutes"))
            Session.Set("clock", "door_open_minutes", GameMinutes);
    }

    /// <summary>Claim due stage flows once, including a boundary crossed by an action.</summary>
    public string? TakeStageTimeFlow()
    {
        var stage = CurrentStage;
        if (!Stage(stage).TryGetProperty("times", out var times)) return null;
        foreach (var scheduled in times.EnumerateArray())
        {
            var minute = scheduled.GetProperty("minutes").GetInt32();
            var key = stage + ":" + minute;
            if (GameMinutes < minute || Session.Has("stage_times", key)) continue;
            Session.Set("stage_times", key, true);
            return scheduled.GetProperty("flow").GetString();
        }
        return null;
    }

    /// <summary>Drain incremental timing entries for an input/observation trajectory.</summary>
    public IReadOnlyList<Chapter1Timing> TakeTiming()
    {
        var entries = _timing.ToArray();
        _timing.Clear();
        return entries;
    }

    private void ObserveClockRoom(string room)
    {
        if (room == "") return;
        var clock = Session.GetProgress("clock");
        var previous = clock.GetValueOrDefault("room", "") as string ?? "";
        if (previous == room) return;
        if (previous != "") ConsumeAction("走动", previous + "→" + room);
        Session.Set("clock", "room", room);
    }
}
