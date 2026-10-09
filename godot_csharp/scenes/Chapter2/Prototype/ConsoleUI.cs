using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>玩家看得懂的错误：直接显示给玩家。</summary>
public sealed class PlayerError(string message) : Exception(message);

/// <summary>子提示符里输入结束了：停下，不代玩家作答（§13 第 16 条）。</summary>
public sealed class EndOfInput() : Exception("输入结束");

/// <summary>同一次重演的玩家可见结果，供命令行与 Godot 手稿共用。</summary>
public sealed record ReplayPresentation(int Night, string[] Selected, string[] Lines, string[] Ink, bool Confirmed, string Summary, Game.InkFrame[] Frames);

/// <summary>
/// 文字界面（§12：可读性）。玩家扮演侦探：每个时段选一件事；看笔记、规矩、痕迹、重演不占时段。
/// 命令可用中文或英文；界面里不出现内部编号，规矩用“甲乙丙”称呼，拼规矩用选项文字。
/// 调试命令（作者侧日志、真实认知）只在以 --debug 启动时可用。
/// </summary>
public sealed class ConsoleUI(Game g, TextReader input, TextWriter output, bool echo, bool debugAllowed = false, bool roomScoped = false, bool spatialHost = false)
{
    public bool HasRoom { get; set; } = true;
    private bool _debug;
    public ReplayPresentation? LastReplay { get; private set; }
    public int ReplayNightNumber => g.Day - 1;
    public IReadOnlyList<Game.ReplayOption> ReplayOptions() =>
        g.Day < Game.CaseDay ? Array.Empty<Game.ReplayOption>() : g.ReplayMenu(ReplayNightNumber, preview: true);
    /// <summary>当前等待的输入：main 主命令、card 拼规矩、yesno 确认、confront 对质、council 名录库之议、ended 结束（Godot 宿主据此给出选项）。</summary>
    public volatile string Mode = "main";
    public int CardCell { get; private set; }
    private int _logShown, _debugShown;
    private readonly List<int> _newsOrder = [];
    private void W(string s = "") => output.WriteLine(Chapter2Content.PlayerText(s));

    // ------------------------------------------------------------ 命令表 --

    private static readonly Dictionary<string, string> Alias = new()
    {
        ["帮助"] = "help", ["help"] = "help", ["?"] = "help", ["？"] = "help",
        ["状态"] = "status", ["status"] = "status", ["笔记"] = "notes", ["notes"] = "notes",
        ["规矩"] = "rules", ["rules"] = "rules", ["传播"] = "spread", ["spread"] = "spread",
        ["清晨"] = "morning", ["痕迹"] = "morning", ["morning"] = "morning", ["重演"] = "replay", ["replay"] = "replay",
        ["消息"] = "news", ["news"] = "news", ["公告"] = "boards", ["boards"] = "boards", ["物件"] = "things", ["things"] = "things",
        ["打赌"] = "wager", ["wager"] = "wager", ["拼规矩"] = "cards", ["cards"] = "cards", ["同行"] = "company", ["company"] = "company",
        ["歇"] = "rest", ["歇着"] = "rest", ["rest"] = "rest", ["wait"] = "rest", ["上课"] = "class", ["class"] = "class",
        ["领粮"] = "ration", ["ration"] = "ration", ["collect"] = "ration", ["查"] = "inspect", ["细看"] = "inspect", ["inspect"] = "inspect",
        ["同去"] = "escort", ["escort"] = "escort", ["请"] = "ask", ["ask"] = "ask", ["问"] = "askabout", ["askabout"] = "askabout",
        ["交钥匙"] = "givekey", ["givekey"] = "givekey", ["转告"] = "tell", ["tell"] = "tell", ["讲"] = "tellrule", ["tellrule"] = "tellrule",
        ["贴"] = "post", ["post"] = "post", ["修温室"] = "greenhouse", ["greenhouse"] = "greenhouse", ["开库"] = "archive", ["archive"] = "archive",
        ["帮"] = "helpout", ["helpout"] = "helpout", ["分粮"] = "share", ["share"] = "share", ["调停"] = "mediate", ["mediate"] = "mediate",
        ["片面告知"] = "slant", ["slant"] = "slant", ["对质"] = "confront", ["confront"] = "confront",
        ["认错"] = "apologize", ["apologize"] = "apologize",
        ["重开对质"] = "reopen", ["reopen"] = "reopen", ["睡"] = "sleep", ["sleep"] = "sleep", ["守夜"] = "stakeout", ["stakeout"] = "stakeout",
        ["证物"] = "evidence", ["evidence"] = "evidence",
        ["去往"] = "go", ["go"] = "go",
        ["调试"] = "debug", ["debug"] = "debug", ["真相"] = "truth", ["truth"] = "truth",
    };

    public void Run()
    {
        W(Chapter2Content.Intro);
        if (!spatialHost)
        {
            W();
            W(Chapter2Content.Instructions);
            W();
            W("《安眠者有福》第二章。输入“帮助”查看命令。");
        }
        Flush();
        while (!g.Ended)
        {
            g.Story?.Poll();
            g.Story?.Pump();
            if (g.Story is not null) Flush();
            if (g.Story?.PendingChoice is not null)
            {
                try { StoryPrompt(); Flush(); }
                catch (EndOfInput) { W("（输入结束：停在当前剧情选择）"); return; }
                continue;
            }
            if (!spatialHost) Header();
            Mode = "main";
            var line = Read();
            if (line is null) { W("（输入结束）"); return; }
            if (line.Length == 0 || line.StartsWith('#')) continue;
            try
            {
                var act = Parse(line, out var consumed);
                if (!consumed) continue;
                if (g.Day == Game.CaseDay && g.Slot == Slot.Evening && act?.Cmd != Cmd.Confront)
                    throw new PlayerError("第 3 日傍晚是中央厅的公开对质。输入“对质”开始（不想说话，就输入“对质”再输入“完”）。");
                if (g.Slot == Slot.Night && act is not null && act.Cmd is not (Cmd.Stakeout or Cmd.Sleep))
                    throw new PlayerError("夜里只能“睡”或“守夜 某地”。");
                if (g.Slot != Slot.Night && act?.Cmd is Cmd.Stakeout) throw new PlayerError("守夜要等到夜里。");
                g.RunSlot(act);
                g.Story?.Pump();
                Flush();
            }
            catch (PlayerError e) { W("  " + e.Message); }
            catch (EndOfInput) { W("（输入结束：停在这里，没有替你作答）"); return; }
            catch (Exception e) { W("  没听懂这条命令。输入“帮助”查看命令。" + (_debug ? $"（调试：{e.GetType().Name} {e.Message}）" : "")); }
        }
        try { CouncilPrompt(); }
        catch (EndOfInput) { W("（输入结束：你还没有表态，名录库之议停在这里）"); }
        Mode = "ended";
    }

    private string? Read()
    {
        var line = input.ReadLine();
        if (line is null) return null;
        line = line.Trim();
        if (echo) W("> " + line);
        return line;
    }

    private void StoryPrompt()
    {
        var story = g.Story!;
        while (story.PendingChoice is { } choice)
        {
            W(choice.Prompt);
            if (!spatialHost) W(Menu(choice.Options.Select(o => o.Text).ToArray()));
            Mode = "story";
            var answer = Read() ?? throw new EndOfInput();
            try
            {
                var option = choice.Options.FirstOrDefault(o => o.Id == answer)
                    ?? choice.Options[Pick(answer, choice.Options.Select(o => o.Text).ToArray(), "剧情选项")];
                g.ChooseStory(option.Id);
                Flush();
            }
            catch (PlayerError e) { W("  " + e.Message); }
        }
    }

    private void Header() => W($"\n【{Chapter2Content.TimeName(g.Day, g.Slot)}】你在{Names.Of(g.Where(g.Det))}。{(g.Slot == Slot.Night ? "（夜里：睡，或 守夜 某地）" : g.Day == Game.CaseDay && g.Slot == Slot.Evening ? "（傍晚公开对质：输入“对质”）" : "")}");

    private void Flush()
    {
        foreach (var f in OrderedFeed(g.TakeFeed()))
        {
            if (f.StartsWith(StoryRuntime.MediaMarker, StringComparison.Ordinal))
            {
                if (roomScoped) output.WriteLine(f);
                else W("  （剧情展示附加媒体）");
            }
            else W("  " + f);
        }
        if (_debug)
        {
            foreach (var l in g.Log.Skip(_logShown)) W("  [作者侧] " + l);
            foreach (var l in g.DebugLog.Skip(_debugShown)) W("  [分项] " + l);
        }
        _logShown = g.Log.Count;
        _debugShown = g.DebugLog.Count;
    }

    private static IEnumerable<string> OrderedFeed(IEnumerable<string> feed)
    {
        var ordinary = new List<string>();
        foreach (var text in feed)
        {
            if (text.StartsWith(StoryRuntime.MediaMarker, StringComparison.Ordinal) || text.StartsWith(StoryRuntime.TextMarker, StringComparison.Ordinal))
            {
                foreach (var line in Compact(ordinary)) yield return line;
                ordinary.Clear();
                yield return text.StartsWith(StoryRuntime.MediaMarker, StringComparison.Ordinal) ? text : text[StoryRuntime.TextMarker.Length..];
            }
            else ordinary.Add(text);
        }
        foreach (var line in Compact(ordinary)) yield return line;
    }

    /// <summary>几个人说了同一句话时合成一行。</summary>
    public static IEnumerable<string> Compact(IEnumerable<string> feed)
    {
        var order = new List<string>();
        var tellers = new Dictionary<string, List<string>>();
        foreach (var f in feed.Distinct())
        {
            var i = f.IndexOf("说：", StringComparison.Ordinal);
            if (i > 0 && i <= 5)
            {
                var text = f[(i + 2)..];
                if (!tellers.TryGetValue(text, out var list)) { tellers[text] = list = []; order.Add("\u0001" + text); }
                list.Add(f[..i]);
            }
            else order.Add(f);
        }
        foreach (var o in order)
            yield return o.StartsWith('\u0001') ? $"{string.Join("、", tellers[o[1..]])}{(tellers[o[1..]].Count > 1 ? "都" : "")}说：{o[1..]}" : o;
    }

    // ------------------------------------------------------------ 解析工具 --

    private static readonly string PeopleList = string.Join(" ", Enum.GetValues<P>().Where(p => p != P.Det).Select(Names.Of));
    private static readonly Dictionary<string, Place> PlaceShort = new()
    {
        ["前厅"] = Place.ArchiveFront, ["名录库"] = Place.ArchiveFront, ["文契"] = Place.DormW, ["星仪"] = Place.DormS, ["后勤"] = Place.DormL,
        ["粮仓"] = Place.Logistics, ["配给台"] = Place.Logistics, ["诊疗"] = Place.Clinic,
    };
    private static readonly string PlaceList = string.Join(" ", Enum.GetValues<Place>().Select(Names.Of));

    private static P Person(string s)
    {
        if (Names.PersonAlias.TryGetValue(s, out var p) && p != P.Det) return p;
        throw new PlayerError($"不认识“{s}”。人名：{PeopleList}");
    }

    private static Place PlaceOf(string s)
    {
        if (Names.PlaceAlias.TryGetValue(s, out var p)) return p;
        if (PlaceShort.TryGetValue(s, out p)) return p;
        throw new PlayerError($"不认识“{s}”这个地方。地点：{PlaceList}");
    }

    private static readonly string[] CnDigits = ["零", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十"];
    /// <summary>读一个序号（阿拉伯数字或一到十）。</summary>
    private static bool TryNum(string s, out int n)
    {
        if (int.TryParse(s, out n)) return true;
        n = Array.IndexOf(CnDigits, s);
        if (s == "两") n = 2;
        return n > 0;
    }

    /// <summary>从选项里找：序号（从 1 起），或文字（完全相同，或唯一包含）。</summary>
    private static int Pick(string s, IReadOnlyList<string> options, string what)
    {
        if (TryNum(s, out var n)) { if (n >= 1 && n <= options.Count) return n - 1; throw new PlayerError($"{what}只有 1—{options.Count} 号。"); }
        var exact = options.ToList().FindIndex(o => o == s);
        if (exact >= 0) return exact;
        var hits = options.Select((o, i) => (o, i)).Where(x => x.o.Contains(s) || s.Contains(x.o)).ToArray();
        if (hits.Length == 1) return hits[0].i;
        if (hits.Length > 1) throw new PlayerError($"“{s}”对得上好几个{what}：{string.Join("、", hits.Select(h => h.o))}。说得再具体些，或用序号。");
        throw new PlayerError($"“{s}”不是{what}。可选：{Menu(options)}");
    }

    private static string Menu(IReadOnlyList<string> options) => string.Join("  ", options.Select((o, i) => $"{i + 1} {o}"));

    // ------------------------------------------------------------ 主解析 --

    private PlayerAction? Parse(string line, out bool consumed)
    {
        consumed = true;
        var a = line.Split([' ', '　'], StringSplitOptions.RemoveEmptyEntries);
        var key = a[0].ToLowerInvariant();
        if (!Alias.TryGetValue(key, out var c)) throw new PlayerError($"没有“{a[0]}”这个命令。输入“帮助”查看命令。");
        if (roomScoped && !HasRoom && c is not ("help" or "status" or "evidence" or "notes" or "news" or "cards" or "rules" or "spread" or "morning" or "replay" or "things" or "boards" or "company" or "go" or "rest" or "sleep"))
            throw new PlayerError("这里没有可用的场景动作。请先走进对应房间。");
        if (roomScoped && HasRoom) RoomPresentation.Validate(g, c, a);
        string Arg(int i, string what) => i < a.Length ? a[i] : throw new PlayerError($"缺少{what}。输入“帮助”查看用法。");
        switch (c)
        {
            case "go": g.SetPlayerPlace(PlaceOf(Arg(1, "目的地"))); consumed = false; return null;
            case "help": Help(); consumed = false; return null;
            case "status": Status(); consumed = false; return null;
            case "evidence": Evidence(); consumed = false; return null;
            case "notes": Notes(a.Length > 1 ? Person(a[1]) : null); consumed = false; return null;
            case "boards": Boards(); consumed = false; return null;
            case "news": News(); consumed = false; return null;
            case "cards": Cards(); consumed = false; return null;
            case "things": Things(); consumed = false; return null;
            case "company": Company(); consumed = false; return null;
            case "rules": Rules(); consumed = false; return null;
            case "spread": Spread(a.Length > 1 ? a[1] : null); consumed = false; return null;
            case "morning": Morning(a.Length > 1 ? Night(a[1]) : g.Day - 1); consumed = false; return null;
            case "replay": Replay(a.Skip(1).ToArray()); consumed = false; return null;
            case "wager":
                W("  " + g.MakeWager(Person(Arg(1, "和谁打赌")), Person(Arg(2, "猜谁")), PlaceOf(Arg(3, "猜他去哪里")), a.Length > 4 ? SpecOf(a[4]).Id : null));
                consumed = false; return null;
            case "debug":
                if (!debugAllowed) throw new PlayerError("调试命令只在以 --debug 启动时可用。");
                _debug = a.Length < 2 || a[1] is not ("off" or "关"); W(_debug ? "调试开：每个时段末显示作者侧日志。" : "调试关。"); consumed = false; return null;
            case "truth":
                if (!debugAllowed) throw new PlayerError("调试命令只在以 --debug 启动时可用。");
                Truth(Person(Arg(1, "人名"))); consumed = false; return null;
            case "rest": return null;
            case "sleep": if (g.Slot != Slot.Night) throw new PlayerError("还没到夜里。白天什么也不做，用“歇”。"); return null;
            case "class":
            {
                if (a.Length == 1) return new(Cmd.Class);
                var topics = Topics();
                var t = Pick(a[1], topics, "话题");
                var precise = a.Length > 2 && a[2] is "精确" or "precise" or "精确地";
                if (a.Length > 2 && !precise && a[2] is not ("含糊" or "vague")) throw new PlayerError("提问方式写“精确”或“含糊”。");
                return new(Cmd.Class) { Topic = Array.IndexOf(Game.Topics, topics[t]), Precise = precise };
            }
            case "ration": return new(Cmd.Collect);
            case "inspect": { var t = ThingOf(Arg(1, "要看的东西或地点")); if (roomScoped && Game.Things_[t].Place != g.Where(g.Det)) throw new PlayerError("请先去" + Names.Of(Game.Things_[t].Place) + "。"); return new(Cmd.Inspect) { Place = Game.Things_[t].Place, Thing = t }; }
            case "escort": { var t = ThingOf(Arg(2, "要去看的东西或地点")); return new(Cmd.Escort) { Who = Person(Arg(1, "请谁同去")), Place = Game.Things_[t].Place, Thing = t }; }
            case "ask": return new(Cmd.Ask) { Who = Person(Arg(1, "请谁")), Job = JobOf(Arg(2, "请他做什么")) };
            case "askabout":
                if (a.Length > 2) throw new PlayerError("问人会听他当下愿意说的事：写“问 某人”。课堂提问才选择话题：写“上课 温室 精确”等。此次没有花掉时段。");
                return new(Cmd.AskAbout) { Who = Person(Arg(1, "问谁")) };
            case "givekey": return new(Cmd.GiveKey) { Who = Person(Arg(1, "持钥匙的人")), About = Person(Arg(2, "交给谁")) };
            case "tell":
            {
                var who = Person(Arg(1, "告诉谁"));
                if (!TryNum(Arg(2, "消息序号（“消息”里的序号）"), out var n) || n < 1 || n > NewsOrder().Count) throw new PlayerError("没有这条消息。先输入“消息”看序号。");
                return new(Cmd.Tell) { Who = who, NewsId = NewsOrder()[n - 1] };
            }
            case "tellrule":
            {
                var who = Person(Arg(1, "对谁讲"));
                if (a.Length == 3 && g.SpecByLabel(a[2]) is { } known) return new(Cmd.TellRule) { Who = who, Spec = known, Card = known.Card };
                return new(Cmd.TellRule) { Who = who, Card = CardOf(a, 2) ?? throw new PlayerError("这次不讲了。") };
            }
            case "post": { var place = PlaceOf(Arg(1, "贴在哪里")); if (!g.Board.ContainsKey(place)) throw new PlayerError($"{Names.Of(place)}没有公告板。有板的地方：{string.Join(" ", g.Board.Keys.Select(Names.Of))}");
                if (roomScoped && place != g.Where(g.Det)) throw new PlayerError("请先去" + Names.Of(place) + "。");
                if (a.Length == 3 && g.SpecByLabel(a[2]) is { } known) return new(Cmd.Post) { Place = place, Spec = known, Card = known.Card };
                return new(Cmd.Post) { Place = place, Card = CardOf(a, 2) ?? throw new PlayerError("这次不贴了。") }; }
            case "greenhouse": return new(Cmd.Greenhouse) { Crew = a.Skip(1).Select(Person).ToArray() };
            case "archive": return new(Cmd.Archive) { Crew = a.Skip(1).Select(Person).ToArray() };
            case "helpout": return new(Cmd.Help) { Who = Person(Arg(1, "帮谁")) };
            case "share": return new(Cmd.ShareFood) { Who = Person(Arg(1, "分给谁")) };
            case "mediate": return new(Cmd.Mediate);
            case "rub": return new(Cmd.Rub) { Who = a.Length > 1 ? Person(a[1]) : null };
            case "slant": return new(Cmd.Manipulate) { Who = Person(Arg(1, "对谁说")), About = Person(Arg(2, "关于谁")) };
            case "stakeout": return new(Cmd.Stakeout) { Place = PlaceOf(Arg(1, "守在哪里")) };
            case "confront":
                if (!(g.Day == Game.CaseDay && g.Slot == Slot.Evening)) throw new PlayerError("公开对质在第 3 日傍晚。之后要再对，用“重开对质”。");
                return new(Cmd.Confront) { Plan = ConfrontPrompt(false) };
            case "apologize":
                if (!g.Manipulations.Any(m => m.Exposed && !m.Apologized)) throw new PlayerError("你没有被揭穿过的片面话要认。");
                return new(Cmd.Apologize);
            case "reopen":
                if (g.Day < Game.CaseDay || g.Day == Game.CaseDay && g.Slot == Slot.Evening) throw new PlayerError("第 3 日傍晚的对质之后，才能重开对质。");
                if (g.QuerCleared || g.Accused is null) throw new PlayerError("眼下没有悬着的指控要重开。");
                return new(Cmd.Reopen) { Plan = ConfrontPrompt(true) };
        }
        throw new PlayerError("没听懂这条命令。输入“帮助”查看命令。");
    }

    private static int Night(string s)
    {
        var t = s.Trim('第', '夜', '日');
        if (TryNum(t, out var n)) return n;
        throw new PlayerError("写成“第3夜”或“3”。");
    }

    private List<string> Topics() => [.. Game.Topics];

    private static string ThingOf(string s)
    {
        if (Game.Things_.ContainsKey(s)) return s;
        if (Names.PlaceAlias.TryGetValue(s, out var place) || PlaceShort.TryGetValue(s, out place))
            if (Game.AreaThings.TryGetValue(place, out var list)) return list[0];
        var hits = Game.Things_.Keys.Where(k => k.Contains(s)).ToArray();
        if (hits.Length == 1) return hits[0];
        throw new PlayerError($"没有“{s}”这样东西。输入“物件”看能细看什么（也可以直接写地点）。");
    }

    public static readonly (string Name, Job Job)[] Jobs =
    [
        ("开粮", Job.Distribute), ("煮粥", Job.Cook), ("照看病人", Job.Care), ("修门闩", Job.RepairLatch), ("修温室", Job.Greenhouse), ("上课", Job.Class),
        ("领粮", Job.Collect), ("补记台账", Job.Inform), ("挑水", Job.FetchWater), ("来中央厅", Job.Socialize), ("清点", Job.CountLedger),
        ("夜巡", Job.Patrol), ("守名录库", Job.GuardArchive),
    ];
    private static Job JobOf(string s) => Jobs[Pick(s, Jobs.Select(j => j.Name).ToArray(), "请求")].Job;

    private RuleSpec SpecOf(string s)
    {
        if (g.SpecByLabel(s) is { } byLabel) return byLabel;
        var known = g.DetKnownSpecs().ToArray();
        var hits = known.Where(k => k.Text.Contains(s)).ToArray();
        if (hits.Length == 1) return hits[0];
        throw new PlayerError(hits.Length > 1 ? $"“{s}”对得上好几条规矩，用称呼（甲、乙……）。" : $"不知道哪条是“{s}”。输入“规矩”看称呼。");
    }

    // ------------------------------------------------------------ 拼规矩 --

    private static readonly (string Cell, string Ask, string[] Options)[] Cells =
    [
        ("主语", "谁来做", Card.Subjects), ("条件", "什么时候", Card.Conds), ("行为", "做什么", Card.Behaviors),
        ("例外", "什么情况不算", Card.Exceptions), ("记账", "怎么留记录", Card.Accounts),
    ];

    /// <summary>五格拼一条规矩：写在命令后面（五格文字或序号），或不写、按提示一格一格选。</summary>
    private Card? CardOf(string[] a, int from)
    {
        var cells = a.Skip(from).ToArray();
        if (cells.Length == 1 && cells[0].Contains('/')) cells = cells[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
        var picks = new int[5];
        if (cells.Length == 5)
        {
            for (var i = 0; i < 5; i++) picks[i] = i == 0 ? SubjectOf(cells[0]) : Pick(cells[i], Cells[i].Options, Cells[i].Cell);
            var card = new Card(picks[0], picks[1], picks[2], picks[3], picks[4]);
            W($"  拼成：「{card.Text}」");
            return card;
        }
        if (cells.Length != 0) throw new PlayerError("规矩要五格：主语 条件 行为 例外 记账（写文字或序号，用空格隔开）；或者只写命令，按提示一格一格选。输入“拼规矩”看选项。");
        if (!spatialHost) W("  拼一条规矩（每格写序号或文字；写“算了”放弃）：");
        for (var i = 0; i < 5; i++)
        {
            while (true)
            {
                CardCell = i;
                if (!spatialHost) W($"  {Cells[i].Cell}（{Cells[i].Ask}）：{Menu(Cells[i].Options)}{(i == 0 ? "；也可以直接写一个人名" : "")}");
                Mode = "card";
                var line = Read() ?? throw new EndOfInput();
                if (line is "算了" or "取消") return null;
                try { picks[i] = i == 0 ? SubjectOf(line) : Pick(line, Cells[i].Options, Cells[i].Cell); break; }
                catch (PlayerError e) { W("  " + e.Message); }
            }
        }
        var built = new Card(picks[0], picks[1], picks[2], picks[3], picks[4]);
        W($"  拼成：「{built.Text}」。就这么说吗？（是/否）");
        Mode = "yesno";
        var yes = Read() ?? throw new EndOfInput();
        return yes is "是" or "y" or "yes" or "好" or "对" ? built : null;
    }

    private static int SubjectOf(string s) => Names.PersonAlias.TryGetValue(s, out var p) && p != P.Det ? 10 + (int)p : Pick(s, Card.Subjects, "主语");

    private void Cards()
    {
        W("拼规矩的五格（讲 某人 五格 / 贴 地点 五格；也可以只写“讲 某人”按提示选）：");
        foreach (var (cell, ask, options) in Cells) W($"  {cell}（{ask}）：{Menu(options)}{(cell == "主语" ? "；或直接写一个人名" : "")}");
        W("  例：讲 茉拉 值守者 有人急病时 取一份口粮 次日补记者不算私取 记入台账");
    }

    // ------------------------------------------------------------ 查看 --

    // STORY-HARDCODE[CH2-023][text][production]: 帮助展示具体人名、物证提示与本章行动例句。
    private void Help()
    {
        W("""
          —— 不占时段（随时可看）——
            状态                 你的处境、学过的课、答应别人的事
            笔记 [某人]          人际笔记：他认了你的哪条规矩、他嘴上向着谁、他常与谁同处（只记你亲见亲闻的）
            规矩                 你知道的规矩。称呼（甲、乙……）是你自己记的，后面的命令里可以用
            传播 [称呼]          你的规矩传到了谁：当面讲过谁、谁跟你说他从哪里听来的、你亲眼看见谁讲给谁
            清晨 [第N夜]         某一夜之后的痕迹：细看过的写全，路过的只记明显的变化，没去过的只给地点
            重演 …               假设昨夜某人认/不认某条规矩、在/不在某处，重放一夜，与你读到的痕迹对照
                                 （只写“重演”会列出可选的假设；“重演 1 3”选其中几项；
                                  也可直接写：重演 茉拉 认 甲 / 重演 雀儿 在 后勤院；可加“第3夜”）
            消息                 你听到的消息（带序号，“转告”用）
            公告                 你看过的公告板
            物件                 能细看的东西与地点
            同行                 你见过谁常和谁在一处
            证物                 你手里可以出示的东西（对质时也能用）
            拼规矩               拼规矩的五格选项
            打赌 某人 谁 何处 [称呼]   和身边的某人打赌“今晚谁会去何处”；说中了，他认你的那条规矩（每天一次）
          —— 占一个时段（白天每个时段只能做一件事）——
            歇                   什么也不做
            上课 [话题 精确|含糊]  上课（上午）；课后可以提问
            领粮                 去配给台领今天的口粮（午间、傍晚开粮）
            查 物件|地点          细看一处：同一区域的痕迹一并看
            同去 某人 物件|地点   请某人一起去看（同去的人就亲眼见过）
            请 某人 事            请人做事：开粮 煮粥 照看病人 修门闩 修温室 上课 领粮 补记台账 挑水 来中央厅 清点 夜巡 守名录库
            问 某人               当面问他知道什么（他只说自己看见、听见的）
            交钥匙 持钥人 交给谁   请拿着粮仓钥匙的人交出去
            转告 某人 消息序号     把你知道的一条消息告诉他
            讲 某人 [五格|称呼]   当面对他讲一条规矩（不写五格就按提示选；写称呼就讲一条已有的规矩）
            贴 地点 [五格]        把一条规矩贴到某处的公告板（看到的人多，但不如当面有分量）
            修温室 甲 乙          请两个人下午一起修温室
            开库 甲 乙 丙 …       请人来名录库前厅试开那道门
            帮 某人               帮他干一个时段的活，或兑现答应他的事
            分粮 某人             把你今天的口粮给他
            调停                  （第 2 日午间）调停配给台前的争执
            片面告知 某人 关于谁   只说一半实话（会被记下）
            对质                  第 3 日傍晚，中央厅的公开对质
            重开对质              第 4 日之后：援引共同规则“公开指控须附可检查的材料”，把人请回中央厅再对一次
            认错                  当众承认你说过的片面话（要被揭穿过才用得上）
          —— 夜里 ——
            睡                    睡下
            守夜 地点             守在某处一夜（只看得见身影、听得见动静）
          守夜只观察，不会拦住别人。有人夜里凑齐机关便可能私开名录库，最后投票无法撤回已发生的事。
          白天可以请人“守名录库”、讲规则或张贴公告干预；是否奏效，要看谁接受、谁实际行动，次晨查痕迹确认。
          温室过红线后可以在上午写“上课 温室 精确”，问清泄压方法；修理仍须两人实际协作。
          人名：霍铎 沈鼎 苏泠 卢西安 顾平章 雀儿 沃德 赛尔玛 阮清越 伊莲娜 莫兰 维罗妮卡 贝尔特朗 玛格丽特 茉拉
          地点：中央厅 讲堂 后勤院 诊疗所 温室 名录库前厅（前厅） 誓约庭 生活翼 文契寝区 星仪寝区 后勤寝区
          """);
        if (debugAllowed) W("  调试（仅 --debug）：调试 开|关　真相 某人");
    }

    /// <summary>手里可出示之物（§13 第 16 条）：你查过、问过、读过的东西，带对质时的证物号。</summary>
    private void Evidence()
    {
        var ev = g.DetEvidence();
        var have = new List<string>();
        for (var i = 0; i < Game.EvidenceNames.Length; i++) if (g.DetHasEvidence(i)) have.Add($"证物 {i + 1}：{Game.EvidenceNames[i]}");
        W(have.Count == 0 ? "你手里还没有可以出示的东西。去细看痕迹、当面问人。" : "你手里可以出示的：\n  " + string.Join("\n  ", have));
        if (g.Det.Witnessed.Contains("crack")) W("  （你还亲眼看过中央基座的裂纹，可以带人去看）");
    }

    private void Things()
    {
        W("能细看的东西（“查”东西或地点都行；同一处的痕迹会一并看）：");
        foreach (var grp in Game.Things_.Where(t => !roomScoped || HasRoom && t.Value.Place == g.Where(g.Det)).GroupBy(t => t.Value.Place)) W($"  {Names.Of(grp.Key)}：{string.Join(" ", grp.Select(t => t.Key))}");
    }

    // STORY-HARDCODE[CH2-081][text][production]: 状态页显示本案特定证物、指控与石碑阅读结果。
    private void Status()
    {
        var det = g.Det;
        W($"{Chapter2Content.TimeName(g.Day, g.Slot)}。你在{Names.Of(g.Where(det))}。{(Game.Get(det.Body, T.Hungry) > 0 ? "你饿着（下午做不了事）。" : "")}今天{(Game.Get(det.Body, T.Fed) > 0 ? "已经" : "还没")}领到口粮。");
        W($"学过的课：{(det.Lessons.Count == 0 ? "无" : string.Join("、", det.Lessons.Select(l => $"《{Game.Lessons[l.Key].Title}》{(l.Value ? "" : "（听人转述，可能走样）")}")))}");
        if (g.Day >= Game.CaseDay)
        {
            var ev = g.DetEvidence();
            W("台账那件事，你查过的：" + (ev.Count == 0 ? "还没有" : string.Join("、", ev.Select(i => CaseTraces.Labels[i]))) + (g.DetAsked.Count > 0 ? "；当面问过：" + string.Join("、", g.DetAsked.Select(Names.Of)) : ""));
        }
        if (g.Accused is { } acc) W(g.QuerCleared ? $"{Names.Of(acc)}已经洗清。" : g.QuerOstracized ? $"{Names.Of(acc)}被当成私取之人，搬出了寝区。" : "沃德的指控还悬着。");
        foreach (var (who, due, done) in g.Promises.Where(p => !p.Done)) W($"答应过：第{due}日前替{Names.Of(who)}办一件事（用“帮 {Names.Of(who)}”兑现）");
        foreach (var (w, right) in g.Wagers) W($"赌约：{Chapter2Content.DayName(w.Day)}和{Names.Of(w.With)}赌{Names.Of(w.Who)}夜里去{Names.Of(w.Where)}——{(right is null ? "待揭晓" : right.Value ? "说中" : "没说中")}");
        if (g.SteleRubbed) W("你读过石碑上的第九、第十条。");
    }

    /// <summary>人际笔记：只用侦探亲见亲闻的东西；把“认了你的哪条规矩”和“嘴上向着谁”“常与谁同处”分开记（§12 第 3 条）。</summary>
    private void Notes(P? only)
    {
        var det = g.Det;
        foreach (var p in g.Npcs.Where(p => only is null || p.Id == only))
        {
            var them = det.M[p.Body];
            var seen = them.Parent?.Name ?? "不确定";
            var sick = Game.Get(p.Body, T.Sick) > 0 && det.World.History.Any(h => ReferenceEquals(h.Target, them) && h.Tags.GetValueOrDefault(T.Sick) > 0);
            W($"· {p.Name}（{Names.Of(p.Pro.Group)}）最后见到在{seen}。{(sick ? "病着。" : "")}");
            var n = only is null ? 2 : 12;
            var rules = g.DetNotes.Where(x => x.Who == p.Id && x.Kind is "规矩" or "规则").TakeLast(n).ToArray();
            if (rules.Length > 0) W("    规矩：" + string.Join("；", rules.Select(x => $"{Chapter2Content.DayName(x.Day)} {x.Text}")));
            var stance = g.DetNotes.Where(x => x.Who == p.Id && x.Kind == "立场").TakeLast(only is null ? 1 : 6).ToArray();
            if (stance.Length > 0) W("    立场：" + string.Join("；", stance.Select(x => $"{Chapter2Content.DayName(x.Day)} {x.Text}")));
            var comp = g.CompanionsOf(p.Id).ToArray();
            if (comp.Length > 0) W("    同行：常与 " + string.Join("、", comp.Select(c => $"{Names.Of(c.Who)}（{c.N} 次）")) + " 同处");
            var other = g.DetNotes.Where(x => x.Who == p.Id && x.Kind is not ("规矩" or "规则" or "立场" or "同处")).TakeLast(only is null ? 1 : 8).ToArray();
            foreach (var x in other) W($"    {Chapter2Content.DayName(x.Day)} {x.Text}");
            if (only is not null)
            {
                var about = det.World.History.Where(h => h.Target.Kind == ProjectionKind.Event && h.Relations.Contains((Relations.Subject, them)) && h.Details != "")
                    .Select(h => $"{h.Details}（{InvestigationPresentation.SourceText(h.Source)}）").Distinct().TakeLast(6).ToArray();
                foreach (var x in about) W("    听说/看到：" + x);
            }
        }
    }

    private void Company()
    {
        W("你见过的“谁和谁常在一处”（饭点、对质这种全员都在的场合不算）：");
        var pairs = g.SeenTogether.Where(kv => kv.Key.Item1 < kv.Key.Item2 && kv.Value >= 2).OrderByDescending(kv => kv.Value).Take(12).ToArray();
        if (pairs.Length == 0) W("  还看不出来。");
        foreach (var (k, v) in pairs) W($"  {Names.Of(k.Item1)} 与 {Names.Of(k.Item2)}：{v} 次");
        if (_debug)
        {
            W("[作者侧] 实际派系：");
            foreach (var f in g.Factions()) W($"  领头 {Names.Of(f.Leader)}：{string.Join("、", f.Members.Select(Names.Of))}");
        }
    }

    private void Boards()
    {
        foreach (var (place, board) in g.Board)
        {
            if (roomScoped && (!HasRoom || place != g.Where(g.Det))) continue;
            var mental = g.Det.M[board];
            var read = g.Det.World.History.Any(h => ReferenceEquals(h.Target, mental));
            var specs = g.Specs.Where(s => s.PostedAt.Contains(place) && s != g.RuanLedger).ToArray();
            if (!read) { W($"· {Names.Of(place)}公告板：你没去看过。"); continue; }
            var known = specs.Where(s => mental.Rules.Any(r => s.Build(g.FxOf(g.Det)).Any(m => m.Mode == r.Mode && Ast.Equivalent(m.Effect, r.Effect) && Ast.Equivalent(m.Condition, r.Condition)))).ToArray();
            W($"· {Names.Of(place)}公告板：" + (known.Length == 0 ? "你上次看时是空的。" : string.Join(" ", known.Select(s => $"{g.LabelOf(s)}「{s.Text}」（{(s.Author is { } a ? Names.Of(a) : "代管者")}）"))));
        }
    }

    public List<int> NewsOrder()
    {
        foreach (var id in g.Det.News.Keys.Order()) if (!_newsOrder.Contains(id) && _newsOrder.All(x => g.AllNews[x - 1].Claim.Identity != g.AllNews[id - 1].Claim.Identity)) _newsOrder.Add(id);
        return _newsOrder;
    }

    private void News()
    {
        var order = NewsOrder();
        if (order.Count == 0) { W("还没有听到什么。"); return; }
        for (var i = 0; i < order.Count; i++)
        {
            var h = g.Det.News[order[i]];
            W($"  {i + 1}. {Chapter2Content.DayName(h.Day)} {g.Plain(g.AllNews[h.Item - 1])}　——{(h.Hops == 0 ? "亲见" : $"听{Names.Of(h.From)}说" + (h.Hops > 1 ? "（转了几道手）" : ""))}");
        }
    }

    private void Rules()
    {
        var specs = g.DetKnownSpecs().ToArray();
        if (specs.Length == 0) { W("你还不知道任何规矩。"); return; }
        W("你知道的规矩（称呼是你自己记的）：");
        foreach (var s in specs)
            W($"  {g.LabelOf(s)}「{s.Text}」——{g.SourceOf(s)}{(s.PostedAt.Count > 0 ? "，贴在" + string.Join("、", s.PostedAt.Select(Names.Of)) : s.ByDemon ? "" : "，口头")}");
    }

    /// <summary>你的规矩传到了谁（§12 第 2 条）：只列你经由亲见或他人亲口得知的，不列全知结果。</summary>
    private void Spread(string? which)
    {
        var mine = g.DetKnownSpecs().Where(s => s.Author == P.Det).ToArray();
        if (which is not null) mine = [SpecOf(which)];
        if (mine.Length == 0) { W("你还没有写过规矩。"); return; }
        foreach (var s in mine)
        {
            W($"{g.LabelOf(s)}「{s.Text}」{(s.PostedAt.Count > 0 ? "（贴在" + string.Join("、", s.PostedAt.Select(Names.Of)) + "）" : "")}");
            var face = g.DetToldFace.Where(x => x.Spec == s).ToArray();
            if (face.Length > 0) W("  当面讲过：" + string.Join("、", face.Select(x => $"{Names.Of(x.Who)}（{Chapter2Content.DayName(x.Day)}，{(x.Nodded ? "点了头" : "没接受")}）")));
            var spread = g.SpreadKnown.Where(k => k.Spec == s).ToArray();
            foreach (var k in spread) W($"  {Chapter2Content.DayName(k.Day)} {k.Text}{(k.How == "亲见" ? "（你亲眼看见）" : "")}");
            if (face.Length == 0 && spread.Length == 0) W("  还不知道它传到了谁。");
        }
    }

    private void Morning(int night)
    {
        if (!g.DawnTraces.TryGetValue(night, out var traces)) { W("还没有那一夜的痕迹。"); return; }
        var known = g.KnownTraces(night);
        W($"{Chapter2Content.DayName(night)}夜里之后的痕迹：");
        foreach (var t in traces.Where(t => known.ContainsKey(t.Key)))
            W($"  · {known[t.Key].Text}{(known[t.Key].Text != t.Text ? "（路过时一眼看到的；细看才知全貌）" : "")}");
        var unseen = traces.Where(t => !known.ContainsKey(t.Key) && !t.Key.StartsWith("神情:") && Changed(t)).Select(t => Names.Of(t.Where)).Distinct().ToArray();
        if (unseen.Length > 0) W($"  · 有变化、你还没去看的地方：{string.Join("、", unseen)}");
        if (known.Count == 0 && unseen.Length == 0) W("  · 你读到的地方都没什么变化。");
    }

    // STORY-HARDCODE[CH2-082][logic][production]: 过滤本案清晨跟踪的特定无变文字。
    private static bool Changed(TraceItem t) => !(t.Text is "三处机关没人碰过" or "前厅积灰上没有新脚印" or "病床边没有新送来的东西" or "贝尔特朗：夜巡无异常" or "门闩完好")
        && !t.Text.Contains("没有新东西") && !(t.Key == "台账" && t.Text.StartsWith("账面与存量差 0 份；夜间记账一栏是空的"));

    /// <summary>重演（§12 第 6 条）：变量由“你知道的规矩 × 你怀疑认了它的人”和“某人那一夜在不在某处”组合而成。</summary>
    private void Replay(string[] v)
    {
        LastReplay = null;
        if (g.Day < Game.CaseDay) { W("还没有可以重演的夜。"); return; }
        var night = g.Day - 1;
        if (v.Length > 0 && v[0].StartsWith('第') && v[0].EndsWith('夜')) { night = Night(v[0]); v = v[1..]; }
        if (night < 2 || night >= g.Day) throw new PlayerError($"能重演的是第 2—{g.Day - 1} 夜。");
        var menu = g.ReplayMenu(night);
        if (v.Length == 0)
        {
            W($"第{night}夜可以假设的（选一到三项，例如“重演 1 4”；也可以直接写“重演 茉拉 认 甲”“重演 雀儿 在 后勤院”）：");
            if (menu.Count == 0) W("  你对那一夜还知道得太少：先去看看有变化的地方，或问问人。");
            for (var i = 0; i < menu.Count; i++) W($"  {i + 1,2}. {menu[i].Text}");
            foreach (var place in menu.Where(m => m.Where is not null).Select(m => m.Where!.Where).Distinct())
            {
                var builds = g.BuildsSeenAt(night, place);
                if (builds.Count > 0) W($"  （{Names.Of(place)}那夜出现过的身量：{string.Join("、", builds.Order().Select(b => Game.BuildText(b).TrimEnd('的')))}。身量对得上的人：{string.Join(" ", g.Npcs.Where(p => builds.Contains(p.Pro.Build)).Select(p => p.Name))}；名单外的人可以直接写“重演 某人 在 {Names.Of(place)}”）");
            }
            return;
        }
        var rules = new List<Game.Hypothesis>();
        var where = new List<Game.Presence>();
        if (v.All(x => TryNum(x, out _)))
        {
            foreach (var x in v)
            {
                TryNum(x, out var n);
                if (n < 1 || n > menu.Count) throw new PlayerError($"没有第 {n} 项。只写“重演”看列表。");
                if (menu[n - 1].Rule is { } r) rules.Add(r);
                if (menu[n - 1].Where is { } w) where.Add(w);
            }
        }
        else
        {
            if (v.Length % 3 != 0) throw new PlayerError("每项假设写三个词：某人 认/不认 规矩称呼，或 某人 在/不在 地点。");
            for (var i = 0; i < v.Length; i += 3)
            {
                var who = Person(v[i]);
                switch (v[i + 1])
                {
                    case "认" or "不认": rules.Add(new(who, SpecOf(v[i + 2]).Id, v[i + 1] == "认")); break;
                    case "在" or "不在": where.Add(new(who, PlaceOf(v[i + 2]), v[i + 1] == "在")); break;
                    default: throw new PlayerError($"“{v[i + 1]}”看不懂：写“认”“不认”“在”或“不在”。");
                }
            }
        }
        if (rules.Count + where.Count > 3) throw new PlayerError("一次最多假设三项。");
        var cands = g.ReplayCandidates(night);
        foreach (var who in rules.Select(r => r.Who).Concat(where.Select(w => w.Who)).Distinct())
            if (!cands.Contains(who))
                throw new PlayerError(cands.Count == 0 ? "你对那一夜还一无所知：先守夜、细看痕迹，或问问夜巡的人，知道那夜有哪些身影在动。"
                    : $"那一夜你读到的身影、脚印、签名里，没有对得上{Names.Of(who)}的。可假设的人：{string.Join(" ", cands.Select(Names.Of))}");
        if (rules.GroupBy(r => (r.Who, r.Spec)).Any(x => x.Count() > 1) || where.GroupBy(w => (w.Who, w.Where)).Any(x => x.Count() > 1))
            throw new PlayerError("同一个人对同一件事只能假设一次。");
        W("  （重演中……）");
        var (tr, world) = g.ReplayNight(night, rules, where);
        var said = rules.Select(r => $"{Names.Of(r.Who)}{(r.Accept ? "认" : "不认")}{g.LabelOf(g.Specs.First(s => s.Id == r.Spec))}").Concat(where.Select(w => $"{Names.Of(w.Who)}{(w.There ? "在" : "不在")}{Names.Of(w.Where)}")).ToArray();
        var lines = g.CompareTraces(night, tr);
        foreach (var w in where.Where(w => w.There && !world.Moves.Any(m => m.Day == night && m.Who == w.Who && m.To == w.Where)))
            lines.Insert(0, $"冲突：照这组假设，那个{Game.BuildText(g[w.Who].Pro.Build).TrimEnd('的')}的身影那夜根本没去{Names.Of(w.Where)}");
        var conflicts = lines.Count(l => l.StartsWith("冲突"));
        var unread = lines.Count(l => l.StartsWith("未查"));
        var confirmed = conflicts == 0 && lines.Any(l => l.StartsWith("吻合"));
        var frames = Game.InkFrames(world, night, rules.Select(r => r.Who).Concat(where.Select(w => w.Who)), confirmed).ToArray();
        var ink = frames.Select(frame => frame.Text).ToArray();
        var summary = conflicts == 0 ? $"—— 与你读到的痕迹没有冲突{(unread > 0 ? "（还有没去看的地方）" : "")}。{(confirmed ? "墨影的心声浮了出来。" : "")}" : $"—— 有 {conflicts} 处对不上：昨夜不是这样。";
        LastReplay = new(night, said, lines.ToArray(), ink, confirmed, summary, frames);
        W($"重演第{LastReplay.Night}日夜：{string.Join("，", LastReplay.Selected)}");
        foreach (var t in LastReplay.Ink) W(t);
        foreach (var l in LastReplay.Lines) W("  " + l);
        W("  " + LastReplay.Summary);
    }

    private void Truth(P who)
    {
        var p = g[who];
        W($"[真实认知] {p.Name}：在{Names.Of(g.Where(p))}，欲望 {p.Desire}，钩 {p.Hooks}，连饿 {p.HungerStreak}，{(p.Weak ? "虚弱，" : "")}心中的侦探：可信 {g.ImageOfDet(p).Credible} 见识 {g.ImageOfDet(p).Insight}");
        W("  信任：" + string.Join(" ", g.People.Where(o => o != p).Select(o => $"{o.Name}{g.TrustIn(p, o):+0;-0;0}")));
        W("  接受的规则：" + string.Join("；", p.Acc.Where(a => a.Value == Core.Truth.True).Select(a => $"{a.Key.Text}（{p.Why.GetValueOrDefault(a.Key)}）")));
        W("  不接受：" + string.Join("；", p.Acc.Where(a => a.Value == Core.Truth.False).Select(a => $"{a.Key.Text}（{p.Why.GetValueOrDefault(a.Key)}）")));
        W($"  名录库立场：{g.StanceOf(p)?.Text ?? "无"}");
        W("  此刻的决策评估（DecisionSystem.Assess 前三）：");
        foreach (var a in DecisionSystem.Assess(p.World, p.Self).Take(3))
            W($"    {(a.Forbidden ? "[禁]" : "")}{(g.JobOf(p, a.Decision.Effect) is { } j ? Names.Of(j) : "请求/说话")} {a.Decision.Score}：{string.Join("；", a.Reasons)}");
        W("  近来：");
        foreach (var d in p.Diary.TakeLast(10)) W("    " + d);
    }

    // ------------------------------------------------------------ 对质与议事 --

    private Game.ConfrontPlan? ConfrontPrompt(bool reopen)
    {
        W(reopen ? "重开对质。先写好要说的：" : "公开对质。");
        W("  每一句陈述配一件证物：写“陈述号 证物号”（例：1 1）；“提规”当众提出一条新规矩；“共同”提议共同规则“公开指控须附可检查的材料”；写“完”结束。");
        for (var i = 0; i < Game.Claims.Length; i++) W($"  陈述 {i + 1}：{Game.Claims[i]}");
        W("  这里只列你已取得的证物；其余需要先细看现场或问人。");
        for (var i = 0; i < Game.EvidenceNames.Length; i++)
            if (g.DetHasEvidence(i)) W($"  证物 {i + 1}：{Game.EvidenceNames[i]}");
        var pairs = new List<(int, int)>();
        Card? proposal = null;
        var common = false;
        while (true)
        {
            Mode = "confront";
            var line = Read() ?? throw new EndOfInput();
            var a = line.Split([' ', '　'], StringSplitOptions.RemoveEmptyEntries);
            if (a.Length == 0) continue;
            try
            {
                if (a[0] is "完" or "done" or "结束") break;
                if (a[0] is "证物") { Evidence(); continue; }
                if (a[0] is "共同" or "common") { common = true; W("  （记下：提议共同规则）"); continue; }
                if (a[0] is "提规" or "propose") { proposal = CardOf(a, 1); continue; }
                var nums = a.Where(x => x is not ("陈述" or "证物" or "claim")).ToArray();
                if (nums.Length == 2 && TryNum(nums[0], out var c) && TryNum(nums[1], out var e) && c >= 1 && c <= Game.Claims.Length && e >= 1 && e <= Game.EvidenceNames.Length)
                {
                    if (!g.DetHasEvidence(e - 1)) throw new PlayerError("你还没有取得这件证物：先细看现场或问人，再重开对质。");
                    pairs.Add((c - 1, e - 1)); W($"  （记下：{Game.Claims[c - 1]}——{Game.EvidenceNames[e - 1]}）"); continue;
                }
                W("  写“陈述号 证物号”、“提规”、“共同”或“完”。");
            }
            catch (PlayerError err) { W("  " + err.Message); }
        }
        return new(pairs, proposal, common);
    }

    private void CouncilPrompt()
    {
        W("\n【第7日 · 名录库之议】");
        var plans = g.ArchivePlans().ToArray();
        for (var i = 0; i < plans.Length; i++) W($"  方案 {i + 1}：{plans[i].Text}（{(plans[i].Author is { } a ? Names.Of(a) : "代管者所立")}）");
        W("你支持哪个方案？写“支持 方案号”，或“不表态”。");
        var choice = -1;
        while (true)
        {
            Mode = "council";
            var line = Read() ?? throw new EndOfInput();
            var a = line.Split([' ', '　'], StringSplitOptions.RemoveEmptyEntries);
            if (a.Length == 0) continue;
            if (a[0] is "不表态") break;
            if (a.Length == 2 && a[0] is "支持" or "back" && TryNum(a[1], out var n) && n >= 1 && n <= plans.Length) { choice = n - 1; break; }
            if (a.Length == 2 && a[0] is "back" && a[1] == "-1") break;
            if (debugAllowed && a.Length == 2 && a[0] is "真相" or "truth") { try { Truth(Person(a[1])); } catch (PlayerError e) { W("  " + e.Message); } continue; }
            W($"写“支持 1”到“支持 {plans.Length}”，或“不表态”。");
        }
        var ending = g.Council(choice);
        foreach (var l in g.CouncilLog) W("  " + l);
        W($"\n结局：{Game.EndingText(ending)}");
        if (_debug) { foreach (var l in g.DebugLog.Skip(_debugShown)) W("  [分项] " + l); W(Report.ChapterEnd(g)); }
    }
}
