using DualOntologyWorldEngine.Story;
using static DualOntologyWorldEngine.Story.ChapterEvents;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

using Ev = Dictionary<string, object?>;

/// <summary>
/// 第二章对统一界面的投影（设计第四部分 §2）：快照里给出抬头、按键、交互点、人物、笔记本、手稿与学园图，
/// 界面与现场只读这些数据，不再自备第二章的表格与文字。
/// </summary>
public sealed partial class Chapter2InteractionSession
{
    private string _map = "living_hall";
    private string _placeTitle = "";
    private object?[] _replayVars = [-1, -1, -1, -1];
    private object?[] _lastRunVars = [];

    /// <summary>人物立绘与场内动作图集的键。</summary>
    private static readonly Dictionary<string, string> Sprites = new()
    {
        ["Det"] = "detective", ["Huo"] = "engineer", ["Shen"] = "chef", ["Su"] = "musician", ["Lucian"] = "poet", ["Gu"] = "archaeologist",
        ["Quer"] = "thief", ["Ward"] = "lawyer", ["Selma"] = "naturalist", ["Ruan"] = "chessplayer", ["Elena"] = "dancer", ["Moran"] = "painter",
        ["Veronica"] = "chemist", ["Bertrand"] = "marksman", ["Margaret"] = "architect", ["Mora"] = "doctor",
    };
    /// <summary>地图 → 侦探在引擎里的地点（室外过道不改）。</summary>
    private static readonly Dictionary<string, string> MapPlaces = new()
    {
        ["central_hall"] = "Hall", ["lecture"] = "Lecture", ["logistics"] = "Logistics", ["granary"] = "Logistics", ["clinic"] = "Clinic",
        ["greenhouse"] = "Greenhouse", ["archive"] = "ArchiveFront", ["counterweight"] = "ArchiveFront", ["campus_s"] = "Oath", ["living_hall"] = "DormLife",
        ["zone_yi"] = "DormLife", ["zone_jia"] = "DormW", ["zone_bing"] = "DormS", ["zone_ding"] = "DormL", ["resonance"] = "Resonance",
    };
    /// <summary>地点 → 人物站在哪张地图上。</summary>
    private static readonly Dictionary<string, string> PlaceMaps = new()
    {
        ["Hall"] = "central_hall", ["Lecture"] = "lecture", ["Logistics"] = "logistics", ["Clinic"] = "clinic", ["Greenhouse"] = "greenhouse",
        ["ArchiveFront"] = "archive", ["Oath"] = "campus_s", ["DormLife"] = "zone_yi", ["DormW"] = "zone_jia", ["DormS"] = "zone_bing", ["DormL"] = "zone_ding",
        ["Resonance"] = "resonance",
    };
    private static readonly string[] Dayparts = ["dawn", "morning", "noon", "afternoon", "evening", "night"];

    private static string MapPlace(string map) => MapPlaces.GetValueOrDefault(map, "");

    /// <summary>现场报告的几何：侦探所在地图的标题，以及谁在视线内、离得近（问话与请人看要当面）。</summary>
    private void ReadContext(IReadOnlyDictionary<string, object?> input)
    {
        if (input.GetValueOrDefault("context") is not IReadOnlyDictionary<string, object?> context) return;
        _map = ChapterEvents.Text(context, "map", _map);
        var near = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (who, value) in Map(context, "actors"))
        {
            if (value is not IReadOnlyDictionary<string, object?> data) continue;
            if (who == "detective") { _placeTitle = ChapterEvents.Text(data, "room_name", _placeTitle); continue; }
            if (Flag(data, "visible", true) && ChapterEvents.Number(data, "distance", double.PositiveInfinity) < 220.0) near.Add(who);
        }
        _nearby = near;
    }

    private IEnumerable<Ev> EndEvents(Ev result)
    {
        if (result.GetValueOrDefault("stands") is IDictionary<string, string> stands)
            yield return Event("stage", ("op", "group"), ("anchor", "site:oath_court"), ("stands", stands.ToDictionary(p => p.Key, p => (object?)p.Value)));
        var hooked = ((string[])result["hooks"]!).ToList();
        var detHooks = Convert.ToInt32(result["det_hooks"]);
        if (detHooks > 0) hooked.Insert(0, "你自己");
        var rows = new List<object?> { "名录库：" + result["result_text"], result["quer"] };
        if ((string)result["case_rule"]! != "") rows.Add($"你在对账时提出的规矩：「{result["case_rule"]}」");
        rows.Add("第一条共同规则：" + (result["common_rule"] is true ? "成立" : "没有成立"));
        rows.Add("留在恶魔那里的钩：" + (hooked.Count == 0 ? "无" : string.Join("、", hooked)));
        rows.Add("你没有交出的：" + (detHooks == 0 ? "你自己" : "无"));
        rows.AddRange(((string[])result["impressions"]!).Cast<object?>());
        yield return Event("music", ("id", "main_theme"), ("fade", 4.0));
        yield return Event("ending", ("title", "安眠者有福 · 完"), ("outcome", ""), ("rows", rows.ToArray()),
            ("actions", new object?[] { new Ev { ["id"] = "title", ["label"] = "返回标题（Enter）", ["key"] = "Enter" } }));
        yield return Event("save", ("path", "user://chapter2_end.json"), ("data", new Ev
        {
            ["detective_name"] = DetectiveName(), ["result"] = result["result"], ["quer"] = result["quer"], ["case_rule"] = result["case_rule"],
            ["common_rule"] = result["common_rule"], ["hooks"] = result["hooks"], ["det_hooks"] = result["det_hooks"],
        }));
    }

    public IReadOnlyDictionary<string, object?> Snapshot()
    {
        var snapshot = LegacySnapshot();
        var keys = new List<object?> { "E / 点击  检视", "Tab  笔记本" };
        if (_replayOpen) keys.Add("R  重演");
        keys.Add("M  学园图");
        keys.Add("T  过一阵");
        snapshot["chapter"] = new Ev { ["id"] = ChapterId, ["title"] = Chapter2Script.Title, ["window_title"] = "安眠者有福 · 第二章" };
        snapshot["field_enabled"] = _stage == "field" && !_ended && _presentation is null && _choices.Count == 0;
        snapshot["player_control"] = true;
        snapshot["realtime"] = false;
        snapshot["hud"] = new Ev { ["title"] = Chapter2Content.TimeName(ShownDay, ShownSlot) + "　" + _placeTitle, ["time"] = "", ["keys"] = keys.ToArray() };
        snapshot["controls"] = new Ev { ["notebook"] = true, ["manuscript"] = _replayOpen, ["campus"] = true, ["wait"] = true, ["examine"] = true };
        snapshot["access"] = new Ev { ["dorms"] = true };
        snapshot["daypart"] = Dayparts[(int)ShownSlot];
        snapshot["sites"] = SiteDefinitions(_map).ToDictionary(p => p.Key, p => (object?)new Ev
        {
            ["title"] = p.Value["title"], ["enabled"] = true, ["kind"] = p.Value["kind"],
            ["new"] = p.Value["kind"] == "" && !_examined.ContainsKey(_map + ":" + p.Key),
        });
        var cast = new Ev { ["侦探"] = "detective", ["工程师"] = "engineer", ["厨师"] = "chef", ["音乐家"] = "musician" };
        foreach (var person in _game.Npcs) if (person.Id is not (P.Huo or P.Shen or P.Su)) cast[person.Name] = Sprites[person.Id.ToString()];
        snapshot["cast"] = cast;
        snapshot["people"] = ((object[])snapshot["people"]!).Cast<Ev>().Select(p =>
        {
            var doing = (string)p["doing"]!;
            var person = new Ev(p)
            {
                ["sprite"] = Sprites.GetValueOrDefault((string)p["key"]!, "detective"),
                ["map"] = PlaceMaps.GetValueOrDefault((string)p["place"]!, ""),
                ["title"] = (string)p["name"]! + (doing == "" ? "" : "（" + (doing.Length <= 14 ? doing : doing[..14] + "…") + "）"),
            };
            return (object?)person;
        }).ToArray();
        snapshot["campus"] = CampusSnapshot();
        snapshot["notebook"] = Notebook();
        snapshot["replay"] = new Ev
        {
            ["open"] = _replayOpen, ["title"] = _content.Text("replay_title"), ["hint"] = _content.Text("replay_hint"),
            ["questions"] = Enumerable.Range(0, CaseVars.Questions.Length).Select(i => (object?)new Ev
            {
                ["question"] = CaseVars.Questions[i][3..], ["options"] = CaseVars.Options[i].Cast<object?>().ToArray(), ["mark"] = "", ["fresh"] = false,
            }).ToArray(),
            ["vars"] = _replayVars, ["last_run_vars"] = _lastRunVars,
            ["result"] = _replayResult.Count == 0 ? new Ev() : new Ev
            {
                ["full_match"] = _replayResult["full_match"],
                ["findings"] = ((object[])_replayResult["findings"]!).Cast<Ev>().Select(f => (object?)new Ev
                { ["title"] = f["name"], ["status"] = f["status"], ["text"] = f["text"], ["card_id"] = "" }).ToArray(),
            },
        };
        return snapshot;
    }

    private Ev CampusSnapshot()
    {
        static Ev Node(double x, double y, string label, string map, string spawn) => new()
        {
            ["pos"] = new object?[] { x, y }, ["label"] = label, ["map"] = map, ["spawn"] = spawn,
        };
        return new Ev
        {
            ["map"] = _map,
            ["known"] = _knownPlaces.ToDictionary(k => k, k => (object?)true),
            ["nodes"] = new Ev
            {
                ["ArchiveFront"] = Node(500, 70, "名录库", "archive", "south"),
                ["Hall"] = Node(500, 200, "中央厅", "central_hall", "south"),
                ["Lecture"] = Node(300, 200, "讲堂", "lecture", "from_central"),
                ["Resonance"] = Node(120, 300, "星仪院一角", "resonance", "south"),
                ["Commons"] = Node(500, 380, "起居厅", "living_hall", "north_door"),
                ["DormW"] = Node(370, 330, "甲区", "zone_jia", "from_hall"),
                ["DormL"] = Node(630, 330, "丁区", "zone_ding", "from_hall"),
                ["DormS"] = Node(370, 440, "丙区", "zone_bing", "from_hall"),
                ["DormLife"] = Node(630, 440, "乙区", "zone_yi", "from_hall"),
                ["Logistics"] = Node(820, 430, "后勤院", "logistics", "west"),
                ["Granary"] = Node(900, 330, "粮仓", "granary", "south"),
                ["Clinic"] = Node(820, 220, "诊疗所", "clinic", "south"),
                ["Greenhouse"] = Node(880, 90, "温室", "greenhouse", "south"),
                ["Oath"] = Node(500, 560, "誓约庭", "campus_s", "gate_inside"),
            },
            ["links"] = new object?[]
            {
                new object?[] { "ArchiveFront", "Hall" }, new object?[] { "Hall", "Lecture" },
                new object?[] { "Lecture", "Resonance" }, new object?[] { "Hall", "Commons" },
                new object?[] { "Commons", "DormW" }, new object?[] { "Commons", "DormL" },
                new object?[] { "Commons", "DormS" }, new object?[] { "Commons", "DormLife" },
                new object?[] { "Commons", "Oath" }, new object?[] { "Commons", "Logistics" },
                new object?[] { "Logistics", "Granary" }, new object?[] { "Logistics", "Clinic" },
                new object?[] { "Clinic", "Greenhouse" }, new object?[] { "Logistics", "Oath" },
            },
        };
    }

    private Ev Notebook()
    {
        var sections = new List<object?>();
        if (_examined.Count > 0)
            sections.Add(new Ev { ["heading"] = "[b]证物[/b]", ["style"] = "grid", ["entries"] = _examined.Select(p => (object?)new Ev
            { ["label"] = $"{p.Value["place"]} · {p.Value["title"]}", ["card_id"] = p.Key }).ToArray() });
        var rules = _game.DetKnownSpecs().Select(spec => (object?)new Ev
        {
            ["label"] = $"「{spec.Text}」——{_game.SourceOf(spec)}" + (spec.PostedAt.Count > 0 ? "，贴在" + string.Join("、", spec.PostedAt.Select(Names.Of)) : ""),
        }).ToArray();
        if (rules.Length > 0) sections.Add(new Ev { ["heading"] = "[b]规矩[/b]", ["style"] = "list", ["entries"] = rules });
        if (_talked.Count > 0)
            sections.Add(new Ev { ["heading"] = "[b]人[/b]", ["style"] = "list", ["entries"] = _talked.Select(p => (object?)new Ev
            { ["label"] = $"{Names.Of(Enum.Parse<P>(p.Key))}：{p.Value}" }).ToArray() });
        if (_journal.Count > 0)
        {
            var entries = new List<object?>();
            foreach (var entry in Enumerable.Reverse(_journal).Cast<object[]>())
            {
                entries.Add(new Ev { ["label"] = $"[b]{entry[0]}[/b]", ["muted"] = true });
                entries.AddRange(((IEnumerable<string>)entry[1]).Select(line => (object?)new Ev { ["label"] = line }));
            }
            sections.Add(new Ev { ["heading"] = "[b]所见所闻[/b]", ["style"] = "list", ["entries"] = entries.ToArray() });
        }
        return new Ev { ["empty_text"] = "还没有记下任何东西。", ["sections"] = sections.ToArray() };
    }
}
