using System.Collections;
using DualOntologyWorldEngine.Awakening;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
using C = DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>A logical client of the same public protocol as the graphical host.</summary>
public static class ContinuationSimulation
{
    public sealed record Outcome(bool Started, bool Ended, string Stage, int Inputs, int CharacterActions, int FailedActions, string Result, string Limitation);

    // STORY-HARDCODE[AWK-052][schema][tool]: 续篇模拟器按公开菜单、空间与信息制定固定探索策略；未读取正确答案或自动通关。
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
            Emit("交接", $"沿用生活翼的世界、CTB 会话及 {live.Characters.Count} 人身体/灵魂引用。侦探续篇白天按公开输入探索；对质与议事用自己的推断作答；不读取全局人物日志用于选择。种子 {seed}。");
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
            var tried = new Dictionary<string, HashSet<string>>();
            int lastClaim = -1;
            var claimQuestion = "";
            (IReadOnlyDictionary<string, object?> Pick, string Why)? Reasoned(IReadOnlyDictionary<string, object?>[] available)
            {
                IReadOnlyDictionary<string, object?>? Find(string id) => available.FirstOrDefault(o => Text(o, "id") == id);
                var ids = available.Select(o => Text(o, "id")).ToArray();
                if (ids.Any(i => i.StartsWith("claim:") || i.StartsWith("template:")))
                {
                    // 两层菜单：第一层是“什么事”（模板）或不带人的整句，第二层“是谁”。尝试记录按对质的那一问记。
                    if (menu != "是谁") claimQuestion = menu;
                    var (fits, claims) = game.DetectiveInference();
                    if (fits.Count == 0 && game.CaseActual is { } actualTraces && !tried.ContainsKey("诊断"))
                    {
                        tried["诊断"] = [];
                        // 只作诊断记录（不参与选择）：作者真相假设下的重演与现实逐条对照。
                        foreach (var v in C.CaseVars.All()) { var ls = game.Compare(game.ReplayCase(v).Traces, actualTraces, game.DetEvidence()); var bad = ls.Where(l => l.StartsWith("冲突")).ToArray(); if (bad.Length <= 1) Emit("重演诊断", $"{v}：冲突 {bad.Length}：{string.Join(" / ", bad)}"); }
                        { var (_, rw) = game.ReplayCase(new C.CaseVars(1, 0, 1, 0)); foreach (var e in rw.Events.Where(e => e.Day == C.Game.CaseNight && e.Slot == C.Slot.Night)) Emit("重演诊断", "假设夜里：" + e.Short); Emit("重演诊断", "现实茉拉入夜评估：" + game.NightStartAssessment.GetValueOrDefault(C.P.Mora)); Emit("重演诊断", "重演茉拉入夜评估：" + rw.NightStartAssessment.GetValueOrDefault(C.P.Mora)); foreach (var who in new[] { C.P.Mora, C.P.Quer }) { var st = game.CaseNightSnapshot!.People[who]; Emit("重演诊断", $"快照 {who}：在 {st.Place}；身体 {string.Join(",", st.Body.Where(kv => kv.Value != 0).Select(kv => kv.Key.Name + "=" + kv.Value))}；认下 {string.Join(",", st.Accepted.Where(kv => kv.Value == Truth.True).Select(kv => kv.Key))}"); } }
                        Emit("重演诊断", $"侦探行动记录 {game.Script.Count} 条：{string.Join(" | ", game.Script.Take(7).Select(a => a?.Describe() ?? "歇着"))}");
                    }
                    // 一句话在整场对质里只说一次：已经说过的（无论被认下与否）不再拿来答别的问。
                    if (!tried.TryGetValue("对质陈述", out var done)) tried["对质陈述"] = done = [];
                    var wanted = claims.FirstOrDefault(c => !done.Contains("claim:" + c) && (Find("claim:" + c) is not null || Find(C.Chapter2InteractionSession.TopId(c)) is not null), -1);
                    if (wanted < 0)
                    {
                        if (Find("back") is { } back) return (back, "这里没有推断里成立的人，换一句");
                        return Find("pause") is { } pause ? (pause, $"推断中剩下的陈述都已试过或不适用于此问（吻合的假设 {fits.Count} 组），暂且不答") : null;
                    }
                    if (Find("claim:" + wanted) is { } direct)
                    {
                        done.Add("claim:" + wanted); lastClaim = wanted;
                        return (direct, $"与已查痕迹全部吻合的 {fits.Count} 组重演假设里都成立");
                    }
                    return (Find(C.Chapter2InteractionSession.TopId(wanted))!, "先选这件事，再说是谁");
                }
                if (ids.Any(i => i.StartsWith("evidence:")))
                {
                    var key = menu + ":evidence:" + lastClaim;
                    if (!tried.TryGetValue(key, out var done)) tried[key] = done = [];
                    var next = game.DetectiveSupport(lastClaim).Select(e => "evidence:" + e).FirstOrDefault(id => !done.Contains(id) && Find(id) is not null)
                        ?? ids.FirstOrDefault(id => id.StartsWith("evidence:") && !done.Contains(id));
                    if (next is null) return null;
                    done.Add(next);
                    return (Find(next)!, "手里与这句话相关、亲眼查过的东西优先");
                }
                if (ids.Any(i => i.StartsWith("proposal:")))
                {
                    var (_, claims) = game.DetectiveInference();
                    // 推出的形状是“夜里急取、白天不记”：提议让夜取补记入账、补记者不算私取的那一条。
                    return claims.Any(c => C.CaseDesign.Claims[c].Topic == "take") && Find("proposal:0") is { } p0 ? (p0, "推断出夜里的取用合乎急需却没入账，提议补记") : (Find("none")!, "推不出案子的形状，不提新规矩");
                }
                if (ids.Contains("yes") && ids.Contains("no") && menu.Contains("可检查的材料"))
                    return (Find("yes")!, "指控须附材料，正是这一案里缺的");
                if (ids.Any(i => i.StartsWith("support:")))
                {
                    var plans = game.ArchivePlans().ToArray();
                    var best = plans.Select((plan, i) => (i, score: game.AcceptScore(game.Det, plan, C.Channel.Public, C.Place.Oath, out _, stance: true)))
                        .Where(x => x.score >= game.Options.AcceptThreshold).OrderByDescending(x => x.score).ThenBy(x => x.i).FirstOrDefault(x => Find("support:" + x.i) is not null);
                    return best.score > 0 ? (Find("support:" + best.i)!, $"与住民同一条接受规则（得分 {best.score}）") : (Find("abstain")!, "哪个方案都不肯认");
                }
                return null;
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
                    // 对质、提议与议事：侦探用自己的推断作答（DetectiveInference：重演 × 已查痕迹），不读判定函数或他人认知。
                    if (Reasoned(available) is { } reasoned)
                    {
                        var (pick, why) = reasoned;
                        selected.Add(menu + ":" + Text(pick, "id"));
                        Emit("侦探决策", $"菜单 {menu}：选择 {Text(pick, "label")}；依据 {why}。");
                        Dispatch(new() { ["kind"] = "select", ["choice_id"] = Text(pick, "id"), ["token"] = Text(pick, "token") }); continue;
                    }
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
                "续篇侦探：白天的走动与问话仍是公开探索策略；对质、提议与议事用自己的推断（重演 × 已查痕迹）与住民同一条接受规则。没有为游说他人建模。免费阅读与换区未验几何或时长。");
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
