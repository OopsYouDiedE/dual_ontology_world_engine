using Godot;
using DualOntologyWorldEngine.Chapter2.Prototype;
using DualOntologyWorldEngine.Story;
using ProtoGame = DualOntologyWorldEngine.Chapter2.Prototype.Game;
using GArray = Godot.Collections.Array;
using GDict = Godot.Collections.Dictionary;

namespace DualOntologyWorldEngine.Chapter2;

/// <summary>
/// 第二章 Godot 与纯 C# 章节会话之间的语义事件桥。
/// 宿主通过 Dispatch 发送输入并呈现输出；人物策略、时段结算与剧本候选由 Core/章节层处理。
/// 显示时钟始终指向刚结算完的时段（ShownDay/ShownSlot）。
/// </summary>
public partial class Chapter2Engine : Node
{
    private ProtoGame _g = null!;
    private int _shownDay = 2;
    private Slot _shownSlot = Slot.Morning;
    private bool _started;
    private Chapter2InteractionSession _interaction = null!;

    public static LiveChapterWorld EnsureContinuation()
    {
        if (ChapterWorldContinuity.Current is not null)
            return ChapterWorldContinuity.Current;

        var chapter = new DualOntologyWorldEngine.Awakening.Chapter1Script();
        var morning = chapter.CreateWorld();
        morning.InitializePlayerPerception();
        var content = chapter.CreateContent();

        if (Godot.FileAccess.FileExists("user://chapter1_end.json"))
        {
            try
            {
                var saveText = Godot.FileAccess.GetFileAsString("user://chapter1_end.json");
                using var doc = System.Text.Json.JsonDocument.Parse(saveText);
                var root = doc.RootElement;
                if (root.TryGetProperty("detective_name", out var detName) && detName.GetString() is { } name && !string.IsNullOrWhiteSpace(name))
                    content.Session.Set("state", "detective_name", name);
                if (root.TryGetProperty("trust_familiar", out var trust) && trust.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)
                    content.Session.Set("state", "full_match_seen", trust.GetBoolean());
                if (root.TryGetProperty("asked_hand", out var askedHand) && askedHand.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)
                    content.Session.Set("state", "asked_hand", askedHand.GetBoolean());
            }
            catch
            {
                // Fall back cleanly if save file is corrupt or unreadable
            }
        }

        var liveWorld = morning.ExportContinuation(content.Session);
        ChapterWorldContinuity.Publish(liveWorld);
        return liveWorld;
    }

    public void Start()
    {
        if (_started) throw new InvalidOperationException("This chapter session is already running.");
        var continuation = EnsureContinuation();
        var json = Godot.FileAccess.GetFileAsString("res://scenes/Chapter2/Script/generated_chapter_02.json");
        var options = Chapter2AuthoredProfiles.Apply(json);
        // Validate all author data before changing the live world.
        _ = new Chapter2Narrative(json, true);
        Chapter2InteractionSession.ValidateScript(json);
        foreach (var character in continuation.Characters.Values)
            if(character.Self is null || continuation.Runtime.Representations(character.Body).Count!=1 || !ReferenceEquals(continuation.Runtime.KnownProjection(character.Soul,character.Body),character.Self) || continuation.Simulation.IsActing(character.Self))
                throw new InvalidOperationException("The live awakening character binding is missing or still acting.");
        _g = new ProtoGame(options, continuation,WorldScriptDocument.Parse(json,allowChapterAdapter:true).Single());
        _interaction = new(_g, json);
        ChapterWorldContinuity.Clear(); // The second chapter now owns this live session.
        _started = true;
    }
    public bool CanStart() => !_started;
    public GDict TryStart()
    {
        try { Start(); return new() { ["ok"] = true, ["error"] = "" }; }
        catch (Exception error) { return new() { ["ok"] = false, ["error"] = error.Message }; }
    }
    public GArray Dispatch(GDict input)
    {
        if (!_started) return new GArray { new GDict { ["kind"]="error",["message"]="章节尚未启动。" } };
        var source = FromDictionary(input);
        var events = _interaction.Dispatch(source);
        _shownDay = _interaction.ShownDay; _shownSlot = _interaction.ShownSlot;
        return new GArray(events.Select(e => ToGodot(e)));
    }
    public GDict Snapshot() => ToGodot(_interaction.Snapshot()).AsGodotDictionary();
    public GDict SiteDefinitions(string map) => ToGodot(_interaction.SiteDefinitions(map)).AsGodotDictionary();
    public GDict ReplayMetadata() => ToGodot(_interaction.ReplayMetadata()).AsGodotDictionary();
    public bool InheritedChapter1() => _g.InheritedChapter1;
    private static Dictionary<string,object?> FromDictionary(GDict input) => input.ToDictionary(p=>p.Key.AsString(),p=>FromGodot(p.Value));
    private static object? FromGodot(Variant value) => value.VariantType switch
    {
        Variant.Type.Nil => null, Variant.Type.Dictionary => FromDictionary(value.AsGodotDictionary()),
        Variant.Type.Array => value.AsGodotArray().Select(FromGodot).ToArray(), Variant.Type.Bool => value.AsBool(),
        Variant.Type.Int => value.AsInt64(), Variant.Type.Float => value.AsDouble(), _ => value.AsString()
    };
    private static Variant ToGodot(object? value)
    {
        if (value is null) return default;
        if (value is System.Collections.IDictionary dictionary)
        {
            var result=new GDict(); foreach(System.Collections.DictionaryEntry pair in dictionary) result[pair.Key.ToString()!]=ToGodot(pair.Value); return result;
        }
        if (value is IReadOnlyDictionary<string,object?> readOnly) { var result=new GDict();foreach(var pair in readOnly)result[pair.Key]=ToGodot(pair.Value);return result; }
        if (value is System.Collections.IEnumerable sequence && value is not string) { var result=new GArray();foreach(var item in sequence)result.Add(ToGodot(item));return result; }
        return value switch { bool b=>b,int i=>i,long l=>l,double d=>d,float f=>f,string s=>s,_=>value.ToString()! };
    }

    public bool Started() => _started;
    public bool Ended() => _g.Ended;
    public int Day() => _shownDay;
    public int SlotIndex() => (int)_shownSlot;
    public string SlotName() => Chapter2Content.SlotName(_shownDay, _shownSlot);
    public string ClockText() => Chapter2Content.TimeName(_g.Day, _g.Slot);
    /// <summary>下一个要结算的时段（宿主据此决定固定场面）。</summary>
    public int NextDay() => _g.Day;
    public int NextSlot() => (int)_g.Slot;

    // Read-only presentation queries. Authoritative input goes through Dispatch.

    public string DetPlace() => _g.Where(_g.Det).ToString();
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

}
