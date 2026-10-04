using Godot;
using DualOntologyWorldEngine.Chapter2.Prototype;
using ProtoGame = DualOntologyWorldEngine.Chapter2.Prototype.Game;
using GArray = Godot.Collections.Array;
using GDict = Godot.Collections.Dictionary;

namespace DualOntologyWorldEngine.Chapter2;

/// <summary>
/// 第二章宿主与人物推演（Prototype.Game）之间的桥，对应第一章的 AwakeningEngine。
/// 设计总纲 §四：读（Examine、Ask、读板、重演）不耗时，直接调用；写与等待经 RunSlot 结算下一个时段，
/// 侦探的这件事就是他这一阵的行动，其余十五人同时依各自的认知行动。
/// 宿主始终显示“刚结算完的那个时段”（ShownDay/ShownSlot）。
/// </summary>
public partial class Chapter2Engine : Node
{
    private ProtoGame _g = null!;
    private int _shownDay = 2;
    private Slot _shownSlot = Slot.Morning;
    private bool _started;

    public void Start()
    {
        _g = new ProtoGame();
        _started = true;
    }

    public bool Started() => _started;
    public bool Ended() => _g.Ended;
    public int Day() => _shownDay;
    public int SlotIndex() => (int)_shownSlot;
    public string SlotName() => Names.Of(_shownSlot);
    /// <summary>下一个要结算的时段（宿主据此决定固定场面）。</summary>
    public int NextDay() => _g.Day;
    public int NextSlot() => (int)_g.Slot;

    // ------------------------------------------------------------------ 写与等待 --

    /// <summary>结算下一个时段。act：{cmd, who, place, thing, job, rule, pairs, proposal, common}。返回侦探看得见的文字。</summary>
    public GArray RunSlot(GDict act)
    {
        var day = _g.Day;
        var slot = _g.Slot;
        _g.RunSlot(ToAction(act));
        _shownDay = day;
        _shownSlot = slot;
        return Lines(_g.TakeFeed());
    }

    private PlayerAction? ToAction(GDict a)
    {
        var cmd = a.TryGetValue("cmd", out var c) ? c.AsString() : "stay";
        P Who() => Enum.Parse<P>(a["who"].AsString());
        Place PlaceArg() => a.TryGetValue("place", out var pl) && pl.AsString() != "" ? Enum.Parse<Place>(pl.AsString()) : _g.Where(_g.Det);
        switch (cmd)
        {
            case "sleep": return null;
            case "watch": return new PlayerAction(Cmd.Stakeout) { Place = PlaceArg() };
            case "collect": return new PlayerAction(Cmd.Collect);
            case "class": return new PlayerAction(Cmd.Class);
            case "mediate": return new PlayerAction(Cmd.Mediate);
            case "escort":
            {
                var thing = a["thing"].AsString();
                return new PlayerAction(Cmd.Escort) { Who = Who(), Place = ProtoGame.Things_[thing].Place, Thing = thing };
            }
            case "request": return new PlayerAction(Cmd.Ask) { Who = Who(), Job = Enum.Parse<Job>(a["job"].AsString()) };
            case "tell_rule":
            {
                var spec = _g.Specs.First(s => s.Id == a["rule"].AsInt32());
                return new PlayerAction(Cmd.TellRule) { Who = Who(), Spec = spec, Card = spec.Card };
            }
            case "tell_wording":
                return new PlayerAction(Cmd.TellRule) { Who = Who(), Card = ProtoGame.CaseWordings[a["wording"].AsInt32()].Card };
            case "help": return new PlayerAction(Cmd.Help) { Who = Who() };
            case "share": return new PlayerAction(Cmd.ShareFood) { Who = Who() };
            case "confront":
            {
                var pairs = new List<(int, int)>();
                foreach (var v in a["pairs"].AsGodotArray()) { var pr = v.AsGodotArray(); pairs.Add((pr[0].AsInt32(), pr[1].AsInt32())); }
                var prop = a.TryGetValue("proposal", out var pv) ? pv.AsInt32() : -1;
                var card = prop >= 0 ? ProtoGame.CaseWordings[prop].Card : null;
                return new PlayerAction(Cmd.Confront) { Plan = new ProtoGame.ConfrontPlan(pairs, card, a.TryGetValue("common", out var cv) && cv.AsBool()) };
            }
            default: return new PlayerAction(Cmd.Stay) { Place = PlaceArg() };
        }
    }

    // ------------------------------------------------------------------ 读 --

    public void MoveDet(string place) => _g.HostMoveDet(Enum.Parse<Place>(place));
    public string DetPlace() => _g.Where(_g.Det).ToString();
    public string Examine(string thing) => _g.HostExamine(thing);
    public string Ask(string who) => _g.HostAsk(Enum.Parse<P>(who));
    public void LessonAnswer(bool precise) => _g.HostLessonAnswer(precise);
    public void SeedHook(string who) => _g.HostSeedHook(Enum.Parse<P>(who));
    public bool DetHungry() => ProtoGame.Get(_g.Det.Body, T.Hungry) > 0;
    public bool DetFed() => ProtoGame.Get(_g.Det.Body, T.Fed) > 0;

    /// <summary>十五人此刻在哪里、在做什么（刚结算的时段里各人最后做的一件事）。</summary>
    public GArray People()
    {
        var list = new GArray();
        foreach (var p in _g.Npcs)
        {
            var ev = _g.LastDoing(p.Id, _shownDay, _shownSlot);
            list.Add(new GDict
            {
                ["key"] = p.Id.ToString(), ["name"] = p.Name, ["place"] = _g.Where(p).ToString(),
                ["doing"] = ev?.Text ?? "", ["frame"] = ev?.Frame ?? "", ["asleep"] = p.Asleep,
                ["sick"] = ProtoGame.Get(p.Body, T.Sick) > 0, ["seen"] = ev?.SeenByDet ?? false,
            });
        }
        return list;
    }

    /// <summary>侦探读过或听过的规矩（笔记本“规矩”页；当面讲的选项）。</summary>
    public GArray KnownRules()
    {
        var list = new GArray();
        foreach (var s in _g.DetKnownSpecs())
            list.Add(new GDict
            {
                ["id"] = s.Id, ["text"] = s.Text, ["source"] = _g.SourceOf(s),
                ["posted"] = string.Join("、", s.PostedAt.Select(Names.Of)), ["tellable"] = _g.HostTellable().Contains(s),
            });
        return list;
    }

    public GArray Wordings(string kind)
    {
        var list = new GArray();
        foreach (var w in kind == "archive" ? ProtoGame.ArchiveWordings : ProtoGame.CaseWordings) list.Add(w.Text);
        return list;
    }

    // ------------------------------------------------------------------ 案件与重演 --

    public GArray CaseQuestions()
    {
        var list = new GArray();
        for (var i = 0; i < CaseVars.Questions.Length; i++)
            list.Add(new GDict { ["question"] = CaseVars.Questions[i][3..], ["options"] = new GArray(CaseVars.Options[i].Select(o => (Variant)o).ToArray()) });
        return list;
    }

    /// <summary>侦探查清了哪几处痕迹（0 台账、1 门闩、2 碗、3 饼屑、4 夜巡所见）。</summary>
    public GArray CaseKnown() => new(_g.DetEvidence().Select(i => (Variant)i).ToArray());

    public GDict RunCase(GArray vars)
    {
        var v = new CaseVars(vars[0].AsInt32(), vars[1].AsInt32(), vars[2].AsInt32(), vars[3].AsInt32());
        var (traces, world) = _g.ReplayCase(v);
        var actual = _g.CaseActual ?? traces;
        var known = _g.DetEvidence();
        var r = traces.Describe().ToArray();
        var a = actual.Describe().ToArray();
        var findings = new GArray();
        string[] where = ["粮仓门内的台账", "粮仓门", "诊疗所", "甲区", "贝尔特朗（夜巡）"];
        var full = true;
        for (var i = 0; i < r.Length; i++)
        {
            var same = r[i].Text == a[i].Text;
            full &= same && known.Contains(i);
            findings.Add(new GDict
            {
                ["index"] = i, ["status"] = !known.Contains(i) ? "unvisited" : same ? "match" : "conflict",
                ["text"] = !known.Contains(i) ? "还没查过：" + where[i] : same ? a[i].Text : $"重演中：{r[i].Text}；现场：{a[i].Text}",
            });
        }
        var steps = new GArray();
        foreach (var s in ProtoGame.InkSteps(world, 2))
            steps.Add(new GDict
            {
                ["t"] = s.Time, ["who"] = s.Who.ToString(), ["name"] = Names.Of(s.Who), ["build"] = s.Build switch { Build.Small => "瘦小", Build.Large => "高大", _ => "中等" },
                ["from"] = s.From.ToString(), ["to"] = s.To.ToString(), ["act"] = s.Act,
            });
        return new GDict { ["findings"] = findings, ["full_match"] = full, ["steps"] = steps };
    }

    public GArray Claims() => new(ProtoGame.Claims.Select(c => (Variant)c).ToArray());
    public GArray Evidence()
    {
        var list = new GArray();
        for (var i = 0; i < ProtoGame.EvidenceNames.Length; i++)
            list.Add(new GDict { ["index"] = i, ["name"] = ProtoGame.EvidenceNames[i], ["have"] = _g.DetHasEvidence(i) });
        return list;
    }
    public bool PairValid(int claim, int evidence) => _g.HostPairValid(claim, evidence);
    public bool ClaimTrue(int claim) => _g.HostClaimTrue(claim);
    public string Reaction(int claim) => _g.HostReaction(claim);
    public string AccusedName() => _g.Accused is { } a ? Names.Of(a) : "";
    public bool QuerCleared() => _g.QuerCleared;

    // ------------------------------------------------------------------ 名录库之议 --

    public GArray CouncilPlans(int proposal)
    {
        if (proposal >= 0) _g.CardSpec(ProtoGame.ArchiveWordings[proposal].Card, P.Det);
        var list = new GArray();
        foreach (var s in _g.ArchivePlans())
            list.Add(new GDict { ["text"] = s.Text, ["author"] = s.Author is { } a ? Names.Of(a) : "恶魔所立", ["mine"] = s.Author == P.Det });
        return list;
    }

    public GDict Council(int back)
    {
        var ending = _g.Council(back);
        var stands = new GDict();
        foreach (var (p, s) in _g.CouncilChoice) stands[p.ToString()] = ProtoGame.EndingText(ProtoGame.FamilyOf(s));
        return new GDict
        {
            ["log"] = Lines(_g.CouncilLog), ["result"] = ending.ToString(), ["result_text"] = ProtoGame.EndingText(ending), ["stands"] = stands,
            ["quer"] = _g.QuerCleared ? "雀儿洗清了冤屈，住回甲区" : _g.QuerOstracized ? "雀儿被当成私取之人，搬出甲区独宿" : "雀儿的事没有人再提",
            ["hooks"] = new GArray(_g.Npcs.Where(p => p.Hooks > 0).Select(p => (Variant)p.Name).ToArray()),
            ["det_hooks"] = _g.Det.Hooks,
            ["common_rule"] = _g.CommonRuleEstablished,
            ["case_rule"] = _g.CaseRule?.Text ?? "",
        };
    }

    /// <summary>侦探在几个人眼里的样子：只写他亲眼见过的迹象（态度好坏的直观描述，不给数值）。</summary>
    public GArray Impressions()
    {
        var list = new GArray();
        foreach (var p in _g.Npcs.Where(p => p.Id is P.Ward or P.Quer or P.Mora or P.Huo or P.Ruan or P.Margaret))
        {
            var (c, i) = _g.ImageOfDet(p);
            var text = c >= 2 ? "说话不避着你" : c >= 1 ? "对你还算客气" : c >= 0 ? "对你话不多" : "明显不想跟你多说";
            if (i >= 2) text += "，信你的判断";
            list.Add($"{p.Name}：{text}");
        }
        return list;
    }

    private static GArray Lines(IEnumerable<string> lines)
    {
        var a = new GArray();
        foreach (var l in lines) if (!string.IsNullOrWhiteSpace(l)) a.Add(l);
        return a;
    }
}
