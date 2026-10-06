using System.Collections;
using DualOntologyWorldEngine.Awakening;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
using C = DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>A logical client of the same public protocol as the graphical host.</summary>
public static class ContinuationSimulation
{
    public sealed record Outcome(bool Started, bool Ended, string Stage, int Inputs, int CharacterActions, int FailedActions, string Result, string Limitation);

    public static Outcome Run(MorningWorld morning, ChapterSession progress, int seed, double handoffGameMinutes,
        double? stopGameMinutes, Action<double, double, int, C.Slot, string, string> record)
    {
        var live = morning.ExportContinuation(progress);
        var json = C.Chapter2Design.Json;
        var game = new C.Game(C.Chapter2AuthoredProfiles.Apply(json), live,
            WorldScriptDocument.Parse(json, allowChapterAdapter: true).Single());
        var session = new C.Chapter2InteractionSession(game, json);
        var random = new Random(seed);
        var baseline = game.Now;
        using var authoredTime = System.Text.Json.JsonDocument.Parse(json);
        var clock = authoredTime.RootElement.GetProperty("chapters")[0].GetProperty("design").GetProperty("time");
        var firstDay = clock.GetProperty("days").GetProperty("首日").GetInt32();
        var handoff = handoffGameMinutes;
        int clockDay = game.Day; C.Slot clockSlot = game.Slot;
        double slotStarted = game.Now, lastMinute = handoff;
        double GameMinute()
        {
            if (clockDay != game.Day || clockSlot != game.Slot)
            { clockDay = game.Day; clockSlot = game.Slot; slotStarted = game.Now; }
            var table = clock.GetProperty(clockDay == firstDay ? "first_day_slot_minutes" : "slot_minutes");
            var range = table.GetProperty(clockSlot.ToString());
            var offset = (clockDay - firstDay) * 1440.0;
            var start = offset + range[0].GetDouble();
            if (clockDay == firstDay && clockSlot == C.Slot.Morning) start = Math.Max(start, handoff);
            var end = offset + range[1].GetDouble();
            var ticks = clockSlot == C.Slot.Night ? C.Game.NightLength : game.Rounds(clockSlot) * C.Game.RoundLength;
            var ratio = ticks > 0 ? Math.Clamp((game.Now - slotStarted) / ticks, 0, 1) : 0;
            lastMinute = Math.Max(lastMinute, start + Math.Max(0, end - start) * ratio);
            return lastMinute;
        }
        int inputs = 0, seenEvents = 0, characterActions = 0, failedActions = 0;
        string result = "未结束";
        bool ended = false, failed = false;
        IReadOnlyDictionary<string, object?> snapshot = session.Snapshot();
        var options = new List<IReadOnlyDictionary<string, object?>>();
        var presentations = new Queue<string>();
        var selected = new HashSet<string>();
        var visible = new List<string>();
        var inspected = new HashSet<string>();
        var asked = new HashSet<string>();
        var sites = C.Chapter2AuthoredSites.ReadAll(json);
        string menu = "";
        void Emit(string kind, string text, int? day = null, C.Slot? slot = null)
            {
            var minute = GameMinute();
            record(Math.Max(0, game.Now - baseline), minute, game.Day, game.Slot, kind, text);
        }
        void Completed(ActionExecution execution)
        {
            var actor = game.People.FirstOrDefault(p => ReferenceEquals(p.Self, execution.Intent.Actor));
            if (actor is null) return;
            characterActions++;
            if(execution.Status==ExecutionStatus.Failed)failedActions++;
            var job = game.JobOf(actor, execution.Intent.Changes);
            var basis=execution.Intent.Basis;
            var why=basis is not null ? actor.DoingWhy : "公开输入/剧本候选（该执行未提供决策依据，不沿用此人的上一动作）";
            var score=basis is not null ? actor.DoingScore.ToString("0.###") : "未提供";
            Emit("人物行动", $"{actor.Name}｜{C.Names.Of(game.Where(actor))}｜{(job is { } j ? C.Names.Of(j) : "剧本候选行动")}｜{execution.Status}；失败原因 {execution.Result?.Failure.ToString() ?? "无"}；依据 {why}；诊断分数 {score}（分数不等于动机合理性）");
            foreach (var item in game.Events.Skip(seenEvents))
                Emit("行动后果", $"事件 {item.Id}｜{C.Names.Of(item.Who)}：{item.Text}；{C.Names.Of(item.Where)}；因果前项 [{string.Join(",", item.Causes)}]；{(item.Fixed ? "作者定：" + (item.Reason.Length > 0 ? item.Reason : "未写理由") : item.Reason.Length > 0 ? "运行结果（" + item.Reason + "）" : "运行结果")}；侦探{(item.SeenByDet ? "可见" : "不可见，仅评估记录")}", item.Day, item.Slot);
            seenEvents = game.Events.Count;
        }
        game.S.Completed += Completed;
        try
        {
            Emit("交接", $"沿用生活翼的世界、CTB 会话及 {live.Characters.Count} 人身体/灵魂引用。侦探续篇使用公开输入探索策略，尚无同住民规则的自主时段入口；不读取全局人物日志用于选择。种子 {seed}。");
            void Dispatch(Dictionary<string, object?> input)
            {
                inputs++;
                input["nearby_people"]=game.Npcs.Where(p=>!p.Asleep&&game.Where(p)==game.Where(game.Det)).Select(p=>(object?)p.Id.ToString()).ToArray();
                Emit("公开输入", DescribeInput(input));
                var output = session.Dispatch(input);
                if (Text(input, "kind") == "select") { options.Clear(); menu = ""; }
                foreach (var e in output)
                {
                    var kind = Text(e, "kind");
                    if (kind == "state") { snapshot = (IReadOnlyDictionary<string, object?>)e["snapshot"]!; continue; }
                    if (kind == "lines")
                    {
                        foreach (var line in Dictionaries(e.GetValueOrDefault("lines")))
                        {
                            var text = Text(line, "speaker_name", Text(line, "speaker")) + "：" + Text(line, "text");
                            visible.Add(text); Emit("对白", text, session.ShownDay, session.ShownSlot);
                        }
                        presentations.Enqueue(Text(e, "presentation_id"));
                    }
                    else if (kind is "candidates" or "card")
                    {
                        options.Clear();
                        menu = Text(e, "title");
                        if (kind == "card") { visible.Add(Text(e, "text")); Emit("证物", menu + "：" + Text(e, "text")); }
                        options.AddRange(Dictionaries(e.GetValueOrDefault("options")));
                        foreach (var choice in options) Emit("可见选项", menu + "｜" + Text(choice, "id") + "｜" + Text(choice, "label"));
                    }
                    else if (kind == "error") { failed = true; Emit("协议失败", Text(e, "message")); }
                    else if (kind == "end")
                    {
                        ended = true;
                        var ending = (IReadOnlyDictionary<string, object?>)e["result"]!;
                        result = Text(ending, "result_text");
                        Emit("结局", result + "；" + Text(ending, "quer") + "；共同规则 " + ending.GetValueOrDefault("common_rule"));
                    }
                    else if (kind == "scene") Emit("场面", Text(e, "map") + "；" + Text(e, "mode"));
                    else if (kind == "interaction_closed") { options.Clear(); Emit("交互结束", Text(e,"actor")+" 的身体已不满足当面交谈条件；取消后续菜单，不改动人物位置。"); }
                    else Emit("呈现", Text(e, "text", Text(e, "message", kind)));
                }
            }
            Dispatch(new() { ["kind"] = "begin" });
            for (var budget = 0; budget < 5000 && !ended && !failed; budget++)
            {
                if (stopGameMinutes is { } stop && GameMinute() >= stop)
                { Emit("运行停点", $"达到运行时长：游戏第 {GameMinute():0} 分钟。之后的剧情不在本次运行范围内。"); break; }
                if (presentations.TryDequeue(out var presentation))
                { Dispatch(new() { ["kind"] = "presented", ["presentation_id"] = presentation }); continue; }
                if (options.Count > 0)
                {
                    var available = options.Where(o => !o.TryGetValue("available", out var a) || a is true).ToArray();
                    // Unsupported argument synthesis must remain an explicit omission.
                    var pause = available.FirstOrDefault(o => Text(o, "id") == "pause");
                    var returning = available.FirstOrDefault(o => Text(o, "id") is "close" or "cancel");
                    var question = available.FirstOrDefault(o => Text(o, "id") == "ask" && !selected.Contains(menu + ":ask"));
                    var remaining = available.Where(o => !selected.Contains(menu + ":" + Text(o, "id"))).ToArray();
                    var choice = pause ?? question ?? returning ?? (remaining.Length > 0 ? remaining : available).ElementAtOrDefault(random.Next(Math.Max(1, remaining.Length > 0 ? remaining.Length : available.Length)));
                    if (choice is null) { failed = true; Emit("停点", "菜单没有可选项"); break; }
                    var id = Text(choice, "id");
                    selected.Add(menu + ":" + id);
                    Emit("侦探决策", $"当前需求：理解环境并保留未知；可见文本累计 {visible.Count} 条；菜单 {menu}，备选 {available.Length} 条。选择 {Text(choice, "label")}；来源 {(pause is not null ? "尚无续篇陈述/举证合成器，保留未答" : question is not null ? "向当前在场者求证" : returning is not null ? "结束免费阅读，避免菜单循环" : "当前可见候选的固定种子采样，未使用隐藏真相或判定查询")}。");
                    Dispatch(new() { ["kind"] = "select", ["choice_id"] = id, ["token"] = Text(choice, "token") }); continue;
                }
                var stage = Text(snapshot, "stage");
                if (stage != "field") { failed = true; Emit("停点", "无待呈现或候选，当前阶段 " + stage); break; }
                // Authored sites are the host's public navigation/interaction catalogue.
                // Their object states, private text and character logs are never policy inputs.
                var site = sites.FirstOrDefault(s => (s.Kind is "" or "school_rules") && !inspected.Contains(game.Day + ":" + s.Id));
                if (site is not null)
                {
                    Emit("侦探决策", "当前需求：在陌生场所查看尚未阅读的公开交互点；目标 " + site.Title + "；来源：宿主导航与交互目录；未验步行几何，阅读/换区不结算时段。");
                    Dispatch(new() { ["kind"] = "move", ["place"] = site.Room.ToString() });
                    if (failed) break;
                    inspected.Add(game.Day + ":" + site.Id);
                    Dispatch(new() { ["kind"] = "interact", ["map"] = site.Map, ["site"] = site.LocalId }); continue;
                }
                var local = Dictionaries(snapshot.GetValueOrDefault("people")).FirstOrDefault(p => Text(p, "place") == game.Where(game.Det).ToString() && p.GetValueOrDefault("present") is true && !asked.Contains(game.Day + ":" + Text(p, "key")));
                if (local is not null)
                {
                    var who = Text(local, "key"); asked.Add(game.Day + ":" + who);
                    Dispatch(new() { ["kind"] = "interact", ["site"] = "person:" + who }); continue;
                }
                var command = game.Slot == C.Slot.Night ? "watch" : "stay";
                Emit("侦探决策", "当前需求：让环境运行，观察变化；已读交互点 " + inspected.Count + "，已问在场者 " + asked.Count + "。提交 " + command + "；夜间留在当前空间观察，不预置嫌疑人与目标地点。尚未建立续篇多步主观推演模型，未尝试用判定函数猜答案。");
                Dispatch(new() { ["kind"] = "action", ["act"] = new Dictionary<string, object?> { ["cmd"] = command, ["place"] = game.Where(game.Det).ToString() } });
            }
            Emit("覆盖范围", $"公开协议输入 {inputs} 条；查看 {inspected.Count} 个日/交互点；询问 {asked.Count} 个日/在场者；结束 {ended}；阶段 {Text(snapshot, "stage")}；结局 {result}。这是一条公开探索策略轨迹，不是全部分支覆盖或人物动机验收。");
            return new(true, ended, Text(snapshot, "stage"), inputs, characterActions, failedActions, result,
                "续篇侦探使用公开探索回退；未接入与其他住民相同的决策规则，也未合成续篇重演及对质论证。免费阅读与换区未验几何或时长。");
        }
        finally { game.S.Completed -= Completed; }
    }

    private static string Text(IReadOnlyDictionary<string, object?> value, string key, string fallback = "")
        => value.GetValueOrDefault(key)?.ToString() ?? fallback;
    private static IEnumerable<IReadOnlyDictionary<string, object?>> Dictionaries(object? value)
        => value is IEnumerable items ? items.Cast<object?>().OfType<IReadOnlyDictionary<string, object?>>() : [];
    private static string DescribeInput(IReadOnlyDictionary<string, object?> input)
        => string.Join("；", input.Where(p => p.Key is not ("token" or "presentation_id")).Select(p => p.Value is IReadOnlyDictionary<string, object?> child ? p.Key + "=[" + DescribeInput(child) + "]" : p.Value is object?[] array ? p.Key+"=["+string.Join(",",array)+"]（逻辑同室代报，未验几何）" : p.Key + "=" + p.Value));
}
