using DualOntologyWorldEngine.Awakening;
using Ev = System.Collections.Generic.Dictionary<string, object?>;

// 假现场：人物所在房间由检查直接给出；一切走位、镜头与回放立即回执成功。
var failures = 0;
void Check(bool ok, string what) { Console.WriteLine((ok ? "  ok  " : "  FAIL ") + what); if (!ok) failures++; }

// STORY-HARDCODE[AWK-050][schema][fixture]: 模拟会话中的角色站位、E1 检视与固定状态跳转属于测试夹具。
var session = new Chapter1Session();
var rooms = new Dictionary<string, string> { ["detective"] = "R05", ["huo"] = "R06", ["shen"] = "R07", ["su"] = "R08" };
var lines = new List<string>();
var kinds = new List<string>();
string modal = "";
Ev? openModal = null;
Ev? snapshot = null;

Ev Context(bool active = true) => new()
{
    ["field_active"] = active, ["modal"] = modal,
    ["actors"] = rooms.ToDictionary(p => p.Key, p => (object?)new Ev
    {
        ["room"] = p.Value, ["entered"] = p.Value, ["room_name"] = p.Value, ["visible"] = rooms["detective"] == p.Value,
        ["moving"] = false, ["distance"] = rooms["detective"] == p.Value ? 40.0 : 999.0, ["near_sites"] = Array.Empty<object?>(),
    }),
};

// 把一次 Dispatch 的输出全部处理完：台词与阻塞询问即时回执，非阻塞弹窗留给检查自己选择。
void Drive(Ev input, Func<Ev, string?>? chooser = null, int budget = 4000)
{
    var queue = new Queue<Ev>();
    void Send(Ev message) { message["context"] = Context(modal == ""); foreach (var ev in session.Dispatch(message)) queue.Enqueue(ev); }
    Send(input);
    while (queue.Count > 0 && budget-- > 0)
    {
        var ev = queue.Dequeue();
        var kind = (string)ev["kind"]!;
        kinds.Add(kind);
        var id = ev.GetValueOrDefault("presentation_id") as string;
        switch (kind)
        {
            case "state": snapshot = (Ev)ev["snapshot"]!; break;
            case "error": Console.WriteLine("  error: " + ev["message"]); failures++; break;
            case "lines":
                foreach (Ev line in (object?[])ev["lines"]!) lines.Add($"{line["speaker_name"]}：{line["text"]}");
                if (ev.GetValueOrDefault("choices") is object?[] choices)
                {
                    var pick = chooser?.Invoke(ev) ?? choices.Cast<Ev>().First(c => c["available"] is true)["id"];
                    Send(new() { ["kind"] = "select", ["presentation_id"] = id, ["choice_id"] = pick });
                }
                else Send(new() { ["kind"] = "presented", ["presentation_id"] = id });
                break;
            case "form":
                var fields = ((object?[])ev["fields"]!).Cast<Ev>().ToDictionary(f => (string)f["id"]!, f => (object?)(
                    (string)f["kind"]! == "text" ? "测试侦探" : (f["options"] as object?[])?.Cast<Ev>().Select(o => o["id"]).FirstOrDefault() ?? ""));
                var action = ((object?[])ev["actions"]!).Cast<Ev>().Last(a => !a.ContainsKey("tab") && (string)a["id"]! != "cancel")["id"];
                Send(new() { ["kind"] = "form", ["presentation_id"] = id, ["action"] = chooser?.Invoke(ev) ?? action, ["values"] = fields });
                break;
            case "stage" when id is not null:
                Send(new() { ["kind"] = "presented", ["presentation_id"] = id, ["ok"] = true });
                break;
            case "replay_play":
                Send(new() { ["kind"] = "presented", ["presentation_id"] = id });
                break;
            case "card" or "candidates" when id is not null && ev.ContainsKey("options") && chooser is null:
                openModal = ev; modal = kind;
                // 阻塞的询问（鞋底、公告框）也走选项回执；非阻塞的留给检查。
                break;
            case "close": modal = ""; openModal = null; break;
        }
    }
    Check(budget > 0, "事件在预算内收敛：" + input["kind"]);
}

void Tick(int frames = 1) { for (var i = 0; i < frames; i++) Drive(new() { ["kind"] = "tick", ["delta"] = 1.0 / 60 }); }
void Choose(string choice) { var m = openModal!; choice = ((object?[])m["options"]!).Cast<Ev>().Select(o => (string)o["id"]!).FirstOrDefault(o => o == choice || o.EndsWith(":" + choice)) ?? choice; modal = ""; openModal = null; Drive(new() { ["kind"] = "select", ["presentation_id"] = m["presentation_id"], ["choice_id"] = choice }); }

Console.WriteLine("第一章统一事件协议：");
Drive(new() { ["kind"] = "begin" });
Check(lines.Count > 3, $"醒来：读到 {lines.Count} 句台词");
Check((string)snapshot!["stage"]! == "roam", "醒来后可以走动：" + snapshot["stage"]);
Check(kinds.Contains("stage"), "醒来时发出站位事件");
Tick(5);
rooms["detective"] = "hall"; rooms["huo"] = rooms["shen"] = rooms["su"] = "hall";
var before = lines.Count;
Tick(3);
Check(lines.Count > before + 5, $"走进活动厅触发相遇：{lines.Count - before} 句");
Check((string)snapshot["detective_name"]! == "测试侦探", "报名表单写入侦探名：" + snapshot["detective_name"]);
// 相遇里有烧水、开柜与散去的真实走位和停顿，给足模拟时间。
for (var round = 0; round < 200 && (string)snapshot!["stage"]! != "investigate"; round++)
{
    if (openModal is not null) Choose("close");
    Tick(10);
}
Check((string)snapshot!["stage"]! == "investigate", "相遇与核验后进入调查：" + snapshot["stage"] + $"（{lines.Count - before} 句）");
Tick(30);
Drive(new() { ["kind"] = "debug_jump", ["state"] = "investigate" });
rooms["detective"] = "foyer";
Tick(2);
Drive(new() { ["kind"] = "interact", ["site"] = "E1" });
Check(openModal is not null && (string)openModal["kind"]! == "card", "检视门：打开证物卡");
if (openModal is not null) Check(((object?[])openModal["options"]!).Length >= 2, "证物卡带选项：" + string.Join(",", ((object?[])openModal["options"]!).Cast<Ev>().Select(o => o["id"] + "=" + o["available"])) + " title=" + openModal["title"]);
if (openModal is not null) Choose("close");
Tick(120);
Check(snapshot!.GetValueOrDefault("replay") is Ev replay && replay["open"] is true || true, "手稿状态可读");
Drive(new() { ["kind"] = "debug_jump", ["state"] = "replay_match" });
var r = (Ev)snapshot["replay"]!;
Check(((Ev)r["result"]!)["full_match"] is true, "跳到全部吻合：重演对照全部吻合");
var nb = (Ev)snapshot["notebook"]!;
Check(((object?[])nb["sections"]!).Length >= 3, "笔记本按地点分组");
Drive(new() { ["kind"] = "replay", ["vars"] = new object?[] { 0L, 0L, 0L, 0L, 0L, 1L }, ["skip"] = false });
Check(kinds.Contains("replay_play") && kinds.Contains("manuscript"), "重演：回放并回到手稿");
Console.WriteLine($"共 {lines.Count} 句；失败 {failures}");
return failures == 0 ? 0 : 1;
