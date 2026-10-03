using System.Text;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>
/// 文字界面。玩家扮演侦探：每个时段选一件事；查看人际笔记、公告板、台账不占时段。
/// `debug on` 之后每个时段末打印作者侧日志；`truth 某人` 打印此人的真实认知。
/// </summary>
public sealed class ConsoleUI(Game g, TextReader input, TextWriter output, bool echo)
{
    private bool _debug;
    private int _logShown, _debugShown;
    private void W(string s = "") => output.WriteLine(s);

    public void Run()
    {
        W("《安眠者有福》第二章《第十一课》系统原型（文字界面）。输入 help 查看命令。");
        Flush();
        while (!g.Ended)
        {
            Header();
            var line = input.ReadLine();
            if (line is null) { W("（输入结束）"); return; }
            line = line.Trim();
            if (echo) W("> " + line);
            if (line.Length == 0 || line.StartsWith('#')) continue;
            try
            {
                var act = Parse(line, out var consumed);
                if (!consumed) continue;
                if (g.Day == 4 && g.Slot == Slot.Morning && act?.Cmd != Cmd.Confront)
                {
                    W("（第 4 日清晨是中央厅的公开对质。用 confront 开始；不说话就输入 confront 然后 done。）");
                    continue;
                }
                g.RunSlot(act);
                Flush();
            }
            catch (FormatException e) { W("看不懂：" + e.Message); }
        }
        CouncilPrompt();
    }

    private void Header() => W($"\n【第{g.Day}日 · {Names.Of(g.Slot)}】你在{Names.Of(g.Where(g.Det))}。{(g.Slot == Slot.Night ? "（夜里：sleep 或 stakeout 某地）" : "")}");

    private void Flush()
    {
        foreach (var f in Compact(g.Feed)) W("  " + f);
        g.Feed.Clear();
        if (_debug)
        {
            foreach (var l in g.Log.Skip(_logShown)) W("  [作者侧] " + l);
            foreach (var l in g.DebugLog.Skip(_debugShown)) W("  [分项] " + l);
        }
        _logShown = g.Log.Count;
        _debugShown = g.DebugLog.Count;
    }

    /// <summary>几个人说了同一句话时合成一行。</summary>
    public static IEnumerable<string> Compact(IEnumerable<string> feed)
    {
        var order = new List<string>();
        var tellers = new Dictionary<string, List<string>>();
        foreach (var f in feed)
        {
            var i = f.IndexOf("说：", StringComparison.Ordinal);
            if (i > 0 && i <= 5)
            {
                var text = f[(i + 2)..];
                if (!tellers.TryGetValue(text, out var list)) { tellers[text] = list = []; order.Add("" + text); }
                list.Add(f[..i]);
            }
            else order.Add(f);
        }
        foreach (var o in order)
            yield return o.StartsWith('') ? $"{string.Join("、", tellers[o[1..]])}{(tellers[o[1..]].Count > 1 ? "都" : "")}说：{o[1..]}" : o;
    }

    private P Person(string s) => Names.PersonAlias.TryGetValue(s, out var p) ? p : throw new FormatException("不认识的人：" + s);
    private Place PlaceOf(string s) => Names.PlaceAlias.TryGetValue(s, out var p) ? p : throw new FormatException("不认识的地方：" + s);
    private static Job JobOf(string s) => s switch
    {
        "distribute" or "开粮" => Job.Distribute, "cook" or "煮粥" => Job.Cook, "care" or "照看" => Job.Care, "repair" or "修门闩" => Job.RepairLatch,
        "greenhouse" or "温室" => Job.Greenhouse, "class" or "上课" => Job.Class, "collect" or "领粮" => Job.Collect, "inform" or "补记" => Job.Inform,
        "water" or "挑水" => Job.FetchWater, "hall" or "来中央厅" => Job.Socialize, "count" or "清点" => Job.CountLedger, "patrol" or "夜巡" => Job.Patrol,
        "guard" or "守名录库" => Job.GuardArchive,
        _ => throw new FormatException("不认识的请求：" + s)
    };

    private PlayerAction? Parse(string line, out bool consumed)
    {
        consumed = true;
        var a = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var c = a[0].ToLowerInvariant();
        string Arg(int i) => i < a.Length ? a[i] : throw new FormatException("缺参数");
        switch (c)
        {
            case "help": Help(); consumed = false; return null;
            case "status": Status(); consumed = false; return null;
            case "notes": Notes(a.Length > 1 ? Person(a[1]) : null); consumed = false; return null;
            case "boards": Boards(); consumed = false; return null;
            case "news": News(); consumed = false; return null;
            case "cards": Cards(); consumed = false; return null;
            case "things": W("可调查：" + string.Join(" ", Game.Things_.Keys)); consumed = false; return null;
            case "debug": _debug = a.Length < 2 || a[1] != "off"; W(_debug ? "调试开：每个时段末显示作者侧日志。" : "调试关。"); consumed = false; return null;
            case "truth": Truth(Person(Arg(1))); consumed = false; return null;
            case "factions": Factions(); consumed = false; return null;
            case "wager": W(g.MakeWager(Person(Arg(1)), Person(Arg(2)), PlaceOf(Arg(3)), a.Length > 4 ? int.Parse(a[4]) : null)); consumed = false; return null;
            case "morning": Morning(a.Length > 1 ? int.Parse(a[1]) : g.Day - 1); consumed = false; return null;
            case "rules": Rules(); consumed = false; return null;
            case "replay": Replay(a.Skip(1).ToArray()); consumed = false; return null;
            case "rest" or "wait": return null;
            case "class": return new(Cmd.Class) { Topic = a.Length > 1 ? Array.IndexOf(Game.Topics, a[1]) : -1, Precise = a.Length > 2 && a[2] is "precise" or "精确" };
            case "ration" or "collect": return new(Cmd.Collect);
            case "inspect": { var t = Arg(1); if (!Game.Things_.ContainsKey(t)) throw new FormatException("没有这样东西：" + t); return new(Cmd.Inspect) { Place = Game.Things_[t].Place, Thing = t }; }
            case "escort": { var t = Arg(2); return new(Cmd.Escort) { Who = Person(Arg(1)), Place = Game.Things_[t].Place, Thing = t }; }
            case "ask": return new(Cmd.Ask) { Who = Person(Arg(1)), Job = JobOf(Arg(2)) };
            case "askabout": return new(Cmd.AskAbout) { Who = Person(Arg(1)) };
            case "givekey": return new(Cmd.GiveKey) { Who = Person(Arg(1)), About = Person(Arg(2)) };
            case "tell": return new(Cmd.Tell) { Who = Person(Arg(1)), NewsId = int.Parse(Arg(2)) };
            case "post": return new(Cmd.Post) { Place = PlaceOf(Arg(1)), Card = ParseCard(a, 2) };
            case "telrule" or "tellrule": return new(Cmd.TellRule) { Who = Person(Arg(1)), Card = ParseCard(a, 2) };
            case "greenhouse": return new(Cmd.Greenhouse) { Crew = a.Skip(1).Select(Person).ToArray() };
            case "archive": return new(Cmd.Archive) { Crew = a.Skip(1).Select(Person).ToArray() };
            case "help-out" or "helpout": return new(Cmd.Help) { Who = Person(Arg(1)) };
            case "mediate": return new(Cmd.Mediate);
            case "stakeout": return new(Cmd.Stakeout) { Place = PlaceOf(Arg(1)) };
            case "sleep": return null;
            case "rub": return new(Cmd.Rub) { Who = a.Length > 1 ? Person(a[1]) : null };
            case "share": return new(Cmd.ShareFood) { Who = Person(Arg(1)) };
            case "slant": return new(Cmd.Manipulate) { Who = Person(Arg(1)), About = Person(Arg(2)) };
            case "confront": return new(Cmd.Confront) { Plan = ConfrontPrompt() };
            default: throw new FormatException(line + "（help 查看命令）");
        }
    }

    private static Card ParseCard(string[] a, int from)
    {
        if (a.Length < from + 5) throw new FormatException("规则要五格：主语 条件 行为 例外 记账（主语可写人名；cards 查看编号）");
        var subject = Names.PersonAlias.TryGetValue(a[from], out var p) ? 10 + (int)p : int.Parse(a[from]);
        return new(subject, int.Parse(a[from + 1]), int.Parse(a[from + 2]), int.Parse(a[from + 3]), int.Parse(a[from + 4]));
    }

    /// <summary>侦探读到、听到或自己写下的规则（编号供 replay 与 wager 使用）。</summary>
    private void Rules()
    {
        foreach (var s in g.Specs.Where(s => !s.ByDemon && s != g.RuanLedger && (s.Author == P.Det || g.Det.Acc.ContainsKey(s) || g.Det.M[g.Board[Place.Hall]].Rules.Length > 0 && s.PostedAt.Count > 0 && Read(s))))
            W($"  #{s.Id}「{s.Text}」——{(s.Author is { } a ? Names.Of(a) : "")}{(s.PostedAt.Count > 0 ? "，贴在" + string.Join("、", s.PostedAt.Select(Names.Of)) : "，口头")}");
    }

    private bool Read(RuleSpec s) => s.PostedAt.Any(place => g.Board.TryGetValue(place, out var b) && g.Det.World.History.Any(h => ReferenceEquals(h.Target, g.Det.M[b])));

    /// <summary>清晨痕迹：侦探读到的写出内容，没读到的只给地点。</summary>
    private void Morning(int night)
    {
        if (!g.DawnTraces.TryGetValue(night, out var traces)) { W("还没有那一夜的痕迹。"); return; }
        var known = g.KnownTraces(night);
        W($"第{night}日夜里之后的痕迹：");
        foreach (var t in traces.Where(t => known.ContainsKey(t.Key))) W($"  · {known[t.Key].Text}");
        var unseen = traces.Where(t => !known.ContainsKey(t.Key) && !t.Key.StartsWith("神情:")).Select(t => Names.Of(t.Where)).Distinct().ToArray();
        if (unseen.Length > 0) W($"  · 还没细看过的地方：{string.Join("、", unseen)}");
        foreach (var t in traces.Where(t => t.Key.StartsWith("神情:") && known.ContainsKey(t.Key))) { }
    }

    private void Help()
    {
        W("""
          —— 不占时段 ——
          status                     你的处境、手里的材料与承诺
          notes [某人]               人际笔记：你观察到的迹象与来源（可能错）
          boards / news / cards / things / factions
          morning [日]               昨夜之后的痕迹：你细看过的写出内容，没看过的只给地点
          rules                      你读到、听到或写下的规则（带编号）
          replay 某人+号 某人-号 …    重演昨夜：假设这些人认/不认这几条规则，与你读到的痕迹对照（replay case … 为第 2 夜四变量）
          wager 某人 谁 何处 [规则号] 与身边某人打赌“今晚谁会去何处”；说中了他认你的规则（每日一次）
          debug on|off / truth 某人   调试：作者侧日志 / 某人的真实认知
          —— 占一个时段（每时段只能做一件）——
          rest                       歇着
          class [话题 precise|vague] 上课（上午）；可在课后答疑提问（话题：名录库 唤名 温室 封存匣 契约 担保 导师 第九条 你想要什么）
          ration                     去配给台领口粮（午间/傍晚）
          inspect 物件               调查（things 查看）
          escort 某人 物件           请某人同行去看（同行即作证）
          ask 某人 请求              请求：distribute cook care repair greenhouse class collect inform water hall count patrol guard
          askabout 某人              当面问某人知道什么（他只说自己的感知与判读）
          givekey 持钥人 交给谁       请持钥人把粮仓钥匙交出
          tell 某人 消息号           把你知道的一条消息告诉他（news 查看编号）
          slant 某人 关于谁           片面告知（字面为真的操纵；系统会记录）
          post 地点 主语 c b e a     五格拼规则贴到板上（cards 查看编号；主语可写人名）；tellrule 某人 主语 c b e a 当面讲
          greenhouse 甲 乙           请人下午一起修温室；archive 甲 乙 丙 请人来试开名录库之门
          helpout 某人               帮某人干活 / 兑现承诺；share 某人 把自己今天的口粮给他
          mediate                    （第 2 日午）调停配给
          rub [某人]                 去誓约庭拓碑（可请人同去）
          sleep / stakeout 地点       夜里睡下 / 守在某处看谁来
          confront                   （第 4 日清晨）公开对质
          人名可用拼音（huo shen su lucian gu quer ward selma ruan elena moran veronica bertrand margaret mora）或中文；
          地点：hall lecture logistics clinic greenhouse archive oath life dormw dorms dorml
          """);
    }

    private void Cards()
    {
        W("主语：" + string.Join(" ", Card.Subjects.Select((s, i) => $"{i}={s}")) + " 或直接写某人的名字");
        W("条件：" + string.Join(" ", Card.Conds.Select((s, i) => $"{i}={s}")));
        W("行为：" + string.Join(" ", Card.Behaviors.Select((s, i) => $"{i}={s}")));
        W("例外：" + string.Join(" ", Card.Exceptions.Select((s, i) => $"{i}={s}")));
        W("记账：" + string.Join(" ", Card.Accounts.Select((s, i) => $"{i}={s}")));
    }

    private void Status()
    {
        var det = g.Det;
        W($"第{g.Day}日{Names.Of(g.Slot)}。你在{Names.Of(g.Where(det))}。{(Game.Get(det.Body, T.Hungry) > 0 ? "你饿着。" : "")}今天{(Game.Get(det.Body, T.Fed) > 0 ? "已经" : "还没")}领到口粮。");
        W($"已学：{(det.Lessons.Count == 0 ? "无" : string.Join("、", det.Lessons.Select(l => $"第{l.Key}课{(l.Value ? "" : "（转述，可能走样）")}")))}");
        if (g.Day >= 3) W("《少了一份》你查过的痕迹：" + string.Join("、", g.DetEvidence().Select(i => CaseTraces.Labels[i])) + (g.DetAsked.Count > 0 ? "；问过：" + string.Join("、", g.DetAsked.Select(Names.Of)) : ""));
        foreach (var (who, due, done) in g.Promises.Where(p => !p.Done)) W($"承诺：第{due}日前替{Names.Of(who)}办一件事");
        foreach (var p in g.Predictions) W($"预判：第{p.Day}日 {Names.Of(p.Who)}→{Names.Of(p.Where)}：{(p.Right is null ? "待定" : p.Right.Value ? "说中" : "没说中")}");
        if (g.SteleRubbed) W("你读过石碑上的第九、第十条。");
    }

    /// <summary>人际笔记：只用侦探自己内部世界里的东西——最后见到某人的地方、听到的关于他的话、他当面的态度。</summary>
    private void Notes(P? only)
    {
        var det = g.Det;
        foreach (var p in g.Npcs.Where(p => only is null || p.Id == only))
        {
            var them = det.M[p.Body];
            var seen = them.Parent?.Name ?? "不确定";
            var about = det.World.History.Where(h => h.Target.Kind == ProjectionKind.Event && h.Relations.Contains((Relations.Subject, them)) && h.Details != "")
                .Select(h => $"{h.Details}（{SourceText(h.Source)}）").Distinct().TakeLast(only is null ? 2 : 12).ToArray();
            W($"· {p.Name}（{Names.Of(p.Pro.Group)}）最后见到在{seen}。{(Game.Get(p.Body, T.Sick) > 0 && det.World.History.Any(h => ReferenceEquals(h.Target, them) && h.Tags.GetValueOrDefault(T.Sick) > 0) ? "病着。" : "")}");
            foreach (var x in about) W("    " + x);
            foreach (var n in g.DetNotes.Where(n => n.Who == p.Id).TakeLast(only is null ? 2 : 20)) W($"    D{n.Day} {n.Text}");
        }
    }

    private static string SourceText(ObservationSource s) => s switch
    {
        ObservationSource.Perception => "亲见", ObservationSource.Testimony => "听说", ObservationSource.Recall => "回忆", ObservationSource.Inference => "推断",
        ObservationSource.Corruption => "恶魔之言", _ => "自知"
    };

    private void Boards()
    {
        foreach (var (place, board) in g.Board)
        {
            var mental = g.Det.M[board];
            var read = g.Det.World.History.Any(h => ReferenceEquals(h.Target, mental));
            var specs = g.Specs.Where(s => s.PostedAt.Contains(place) && s != g.RuanLedger).ToArray();
            if (!read) { W($"· {Names.Of(place)}公告板：你没去看过。"); continue; }
            var known = specs.Where(s => mental.Rules.Any(r => s.Build(g.FxOf(g.Det)).Any(m => m.Mode == r.Mode && Ast.Equivalent(m.Effect, r.Effect) && Ast.Equivalent(m.Condition, r.Condition)))).ToArray();
            W($"· {Names.Of(place)}公告板：" + (known.Length == 0 ? "你上次看时是空的。" : string.Join(" ", known.Select(s => $"「{s.Text}」（{(s.Author is { } a ? Names.Of(a) : "恶魔")}）"))));
        }
    }

    private void News()
    {
        foreach (var (id, h) in g.Det.News.OrderBy(n => n.Key).DistinctBy(n => n.Value.Text))
            W($"[{id}] D{h.Day} {h.Text}　——{(h.Hops == 0 ? "亲见" : $"听{Names.Of(h.From)}说（经 {h.Hops} 手）")}");
    }

    private void Factions()
    {
        W("你看到的：谁常和谁在一处、谁复述谁的规则、谁替谁说话。");
        foreach (var n in g.DetNotes.Where(n => n.Kind == "同处").TakeLast(12)) W($"  D{n.Day} {n.Text}");
        if (_debug)
        {
            W("[作者侧] 实际派系：");
            foreach (var f in g.Factions()) W($"  领头 {Names.Of(f.Leader)}：{string.Join("、", f.Members.Select(Names.Of))}｜共享：{string.Join("；", f.SharedRules)}");
        }
    }

    private void Truth(P who)
    {
        var p = g[who];
        W($"[真实认知] {p.Name}：在{Names.Of(g.Where(p))}，欲望 {p.Desire}，钩 {p.Hooks}，连饿 {p.HungerStreak}，{(p.Weak ? "虚弱，" : "")}心中的侦探：可信 {g.ImageOfDet(p).Credible} 见识 {g.ImageOfDet(p).Insight}");
        W("  信任：" + string.Join(" ", g.People.Where(o => o != p).Select(o => $"{o.Name}{g.TrustIn(p, o):+0;-0;0}")));
        W("  接受的规则：" + string.Join("；", p.Acc.Where(a => a.Value == Core.Truth.True).Select(a => $"{a.Key.Text}（{p.Why.GetValueOrDefault(a.Key)}）")));
        W("  不接受：" + string.Join("；", p.Acc.Where(a => a.Value == Core.Truth.False).Select(a => $"{a.Key.Text}（{p.Why.GetValueOrDefault(a.Key)}）")));
        W("  认定：" + string.Join(" ", g.People.Where(o => o != p).Select(o => (o, b: Game.Belief(p, o, T.Branded), s: Game.Belief(p, o, T.Suspect), sick: Game.Belief(p, o, T.Sick)))
            .Where(x => x.b > 0 || x.s > 0 || x.sick > 0).Select(x => $"{x.o.Name}{(x.b > 0 ? "[私取之人]" : "")}{(x.s > 0 ? "[嫌疑]" : "")}{(x.sick > 0 ? "[病]" : "")}")));
        W("  此刻的决策评估（DecisionSystem.Assess 前三）：");
        foreach (var a in DecisionSystem.Assess(p.World, p.Self).Take(3))
            W($"    {(a.Forbidden ? "[禁]" : "")}{(g.JobOf(p, a.Decision.Effect) is { } j ? Names.Of(j) : "请求/说话")} {a.Decision.Score}：{string.Join("；", a.Reasons)}");
        W("  近来：");
        foreach (var d in p.Diary.TakeLast(10)) W("    " + d);
    }

    /// <summary>
    /// 重演昨夜（§11 第 1、10 条）：replay 某人+规则号 某人-规则号 …（+ 表示假设他接受，- 表示假设他不接受）；
    /// replay case V1 V2 V3 V4 是《少了一份》的第 2 夜四变量重演。重演从现实快照重建，含你自己白天的行动。
    /// </summary>
    private void Replay(string[] v)
    {
        if (g.Day < 3) { W("还没有可重演的夜。"); return; }
        if (v.Length >= 1 && v[0] == "case")
        {
            if (v.Length != 5)
            {
                for (var i = 0; i < 4; i++) W($"{CaseVars.Questions[i]}：" + string.Join(" ", CaseVars.Options[i].Select((o, j) => $"{j}={o}")));
                return;
            }
            var vars = new CaseVars(int.Parse(v[1]), int.Parse(v[2]), int.Parse(v[3]), int.Parse(v[4]));
            var (traces, world) = g.ReplayCase(vars);
            W($"重演第 2 夜：{vars}");
            foreach (var t in Game.Ink(world, 2, [new(P.Mora, 0, true), new(P.Quer, 0, true), new(P.Margaret, 0, true)])) W(t);
            foreach (var l in g.CompareTraces(2, world.DawnTraces.GetValueOrDefault(2) ?? [])) W("  " + l);
            return;
        }
        var night = g.Day - 1;
        var hyp = new List<Game.Hypothesis>();
        foreach (var x in v)
        {
            var i = x.IndexOfAny(['+', '-']);
            if (i <= 0) throw new FormatException("写成 某人+规则号 或 某人-规则号（rules 查看编号）");
            hyp.Add(new(Person(x[..i]), int.Parse(x[(i + 1)..]), x[i] == '+'));
        }
        if (hyp.Count == 0)
        {
            W("用法：replay 某人+规则号 某人-规则号 …　假设昨夜这些人心里认/不认这几条规则，重演一夜，与你读到的痕迹对照。rules 查看规则编号。");
            W("      replay case V1 V2 V3 V4　《少了一份》第 2 夜（不带数字看变量）。");
            return;
        }
        var (tr, w) = g.ReplayNight(night, hyp);
        W($"重演第{night}日夜：" + string.Join("，", hyp.Select(h => $"{Names.Of(h.Who)}{(h.Accept ? "认" : "不认")}#{h.Spec}")));
        foreach (var t in Game.Ink(w, night, hyp)) W(t);
        foreach (var l in g.CompareTraces(night, tr)) W("  " + l);
    }

    private Game.ConfrontPlan ConfrontPrompt()
    {
        W("对质。陈述与出示成对：claim 陈述号 证物号；可加 propose c b e a（提出新规则）、common（提议共同规则）；done 结束。");
        for (var i = 0; i < Game.Claims.Length; i++) W($"  陈述 {i}：{Game.Claims[i]}");
        for (var i = 0; i < Game.EvidenceNames.Length; i++) W($"  证物 {i}：{Game.EvidenceNames[i]}");
        var pairs = new List<(int, int)>();
        Card? proposal = null;
        var common = false;
        while (input.ReadLine() is { } line)
        {
            if (echo) W("> " + line);
            var a = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (a.Length == 0) continue;
            if (a[0] == "done") break;
            if (a[0] == "claim" && a.Length == 3) pairs.Add((int.Parse(a[1]), int.Parse(a[2])));
            else if (a[0] == "propose") proposal = ParseCard(a, 1);
            else if (a[0] == "common") common = true;
            else W("claim n e / propose c b e a / common / done");
        }
        return new(pairs, proposal, common);
    }

    private void CouncilPrompt()
    {
        W("\n【第7日 · 名录库之议】");
        var plans = g.ArchivePlans().ToArray();
        for (var i = 0; i < plans.Length; i++) W($"  方案 {i}：{plans[i].Text}（{(plans[i].Author is { } a ? Names.Of(a) : "")}）");
        W("你支持哪个方案？输入 back 编号（或 back -1 不表态）。");
        var choice = -1;
        while (input.ReadLine() is { } line)
        {
            if (echo) W("> " + line);
            var a = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (a.Length == 2 && a[0] == "back" && int.TryParse(a[1], out choice)) break;
            if (a.Length >= 1 && a[0] == "truth" && a.Length == 2) { Truth(Person(a[1])); continue; }
            W("back 编号");
        }
        var ending = g.Council(choice);
        foreach (var l in g.CouncilLog) W("  " + l);
        W($"\n结局：{Game.EndingText(ending)}");
        if (_debug) { foreach (var l in g.DebugLog.Skip(_debugShown)) W("  [分项] " + l); W(Report.ChapterEnd(g)); }
    }
}
