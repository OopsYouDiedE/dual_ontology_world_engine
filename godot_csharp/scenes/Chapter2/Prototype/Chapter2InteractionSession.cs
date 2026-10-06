using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>Semantic chapter events. Presentation acknowledgements advance text, never authorize client state writes.</summary>
public sealed class Chapter2InteractionSession
{
    private readonly Game _game;
    private readonly Chapter2Narrative _narrative;
    private readonly WorldSession _behavior;
    private readonly Dictionary<P,string> _actorIds;
    private readonly IReadOnlyList<Chapter2AuthoredSite> _sites;
    private readonly IReadOnlyDictionary<string,string> _reactionIntents;
    private readonly IReadOnlyList<(string Key,string Text)> _furniture;
    private readonly HashSet<string> _furnitureSpoken = new(StringComparer.Ordinal);
    private HashSet<string>? _nearby;
    private string? _behaviorPresentation;
    private string? _awaitingNarrativeChoice;
    private readonly Queue<Action<List<Dictionary<string, object?>>>> _continuations = new();
    private readonly Dictionary<string, (string Token, Func<bool> Available, Action<List<Dictionary<string, object?>>> Submit)> _choices = new();
    private readonly Dictionary<string, Dictionary<string, object?>> _examined = new();
    private readonly Dictionary<string, string> _talked = new();
    private readonly HashSet<string> _knownPlaces = ["DormLife", "Hall"];
    private readonly List<object> _journal = new();
    private string? _presentation;
    private string? _narrativePresentation;
    private string _stage = "intro";
    private int _version, _replayCount, _confrontIndex;
    private bool _fullMatch, _replayOpen, _ended, _mediating;
    private Dictionary<string, object?> _replayResult = new();
    private readonly List<(int,int)> _pairs = new();
    private static readonly int[] ConfrontClaims = [0, 2, 1, 3];
    private readonly Chapter2InteractionContent _content;
    private readonly string[] _confrontQuestions;
    public Dictionary<string,object?> ReplayMetadata()=>new(){["title"]=_content.Text("replay_title"),["hint"]=_content.Text("replay_hint")};
    public int ShownDay { get; private set; } = 2;
    public Slot ShownSlot { get; private set; } = Slot.Morning;

    public Chapter2InteractionSession(Game game, string script)
    {
        ValidateScript(script);
        _game = game; _narrative = new(script, game.InheritedChapter1);
        _content=new(script); _confrontQuestions=_content.Texts("confront_questions",4);
        _sites=Chapter2AuthoredSites.ReadAll(script);
        using(var document=System.Text.Json.JsonDocument.Parse(script))
        {
            var furniture=document.RootElement.GetProperty("chapters")[0].GetProperty("furniture");
            _furniture=furniture.EnumerateArray().Select(f=>(f.GetProperty("key").GetString()!,f.GetProperty("text").GetString()!)).OrderByDescending(f=>f.Item1.Length).ToArray();
        }
        var chapter=WorldScriptDocument.Parse(script, allowChapterAdapter: true).Single();
        _reactionIntents=chapter.Candidates.Where(c=>c.Kind=="reaction").ToDictionary(c=>c.Id.StartsWith(chapter.Id+":",StringComparison.Ordinal)?c.Id:chapter.Id+":"+c.Id,c=>c.Intent,StringComparer.Ordinal);
        foreach(var intent in _reactionIntents.Values)if(intent is not ("testimony" or "decline"))throw new InvalidDataException("Formal NPC reactions must declare testimony or decline intent.");
        var entities=new Dictionary<string,Essence>();var characters=new Dictionary<string,(SoulEssence,Projection)>();
        _actorIds=new();
        foreach(var declaration in chapter.Entities)
        {
            var id=Chapter2Bindings.Id(declaration);
            var entity=Chapter2Bindings.Resolve(game,declaration);
            if(declaration.Kind=="character")
            {
                var person=game[Chapter2Bindings.PersonId(declaration)];
                characters[id]=(person.Soul,person.Self);_actorIds[person.Id]=id;
            }
            entities.Add(id,entity);
        }
        _behavior=new(game.Rt,game.S);
        _behavior.BindExisting(chapter,entities,characters,game.InheritedChapter1?["awakening"]:[]);
    }
    public static void ValidateScript(string script)
    {
        var chapter=WorldScriptDocument.Parse(script,allowChapterAdapter:true).Single();
        if(chapter.Requires.Count!=1||chapter.Requires[0]!="awakening")throw new InvalidDataException("Formal chapter two requires exactly the live awakening chapter.");
        Chapter2Bindings.Validate(chapter);_=Chapter2AuthoredSites.ReadAll(script);
        var content=new Chapter2InteractionContent(script);
        foreach(var key in new[]{"replay_title","replay_hint","hungry","bed_early","watch_early","meal_closed"})_=content.Text(key);
        _=content.Texts("finding_titles",5);_=content.Texts("confront_questions",4);
        foreach(var reaction in chapter.Candidates.Where(c=>c.Kind=="reaction"))if(reaction.Intent is not ("testimony" or "decline"))throw new InvalidDataException("A formal reaction must declare its executable intent.");
        using var document=System.Text.Json.JsonDocument.Parse(script);
        var furniture=document.RootElement.GetProperty("chapters")[0].GetProperty("furniture");
        var keys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var entry in furniture.EnumerateArray())
            if(string.IsNullOrWhiteSpace(entry.GetProperty("key").GetString())||string.IsNullOrWhiteSpace(entry.GetProperty("text").GetString())||!keys.Add(entry.GetProperty("key").GetString()!))throw new InvalidDataException("Invalid or duplicate furniture description.");
    }

    private static Dictionary<string, object?> Event(string kind, params (string Key, object? Value)[] args)
    { var result = new Dictionary<string, object?> { ["kind"] = kind }; foreach (var (key,value) in args) result[key] = value; return result; }
    private static string Text(IReadOnlyDictionary<string, object?> input, string key, string fallback = "") => input.GetValueOrDefault(key)?.ToString() ?? fallback;
    private static string Text(IReadOnlyDictionary<string, string> input, string key, string fallback = "") => input.GetValueOrDefault(key) ?? fallback;
    private static int Number(IReadOnlyDictionary<string, object?> input, string key, int fallback = -1) => int.TryParse(Text(input,key),out var n) ? n : fallback;

    public IReadOnlyList<Dictionary<string, object?>> Dispatch(IReadOnlyDictionary<string, object?> input)
    {
        var output = new List<Dictionary<string, object?>>();
        try
        {
            if(input.GetValueOrDefault("nearby_people") is IEnumerable<object?> nearby) _nearby=nearby.Select(p=>p?.ToString()??"").ToHashSet(StringComparer.Ordinal);
            var kind = Text(input,"kind");
            if (_presentation is not null && kind != "presented") throw new InvalidOperationException("请先完成当前呈现。");
            switch (kind)
            {
                case "begin":
                    if (_stage != "intro") throw new InvalidOperationException("章节已开始。");
                    _stage = "beginning";
                    Activate("begin.arrival",output);
                    _continuations.Enqueue(o => { Run(new(Cmd.Stay) { Place = Place.Hall },o); Scene(Place.Hall,o); });
                    _continuations.Enqueue(o => Activate("begin.gather",o));
                    // The lesson is presented inside the forenoon it occupies; attending it then advances the clock.
                    _continuations.Enqueue(o => Scene(Place.Lecture,o));
                    _continuations.Enqueue(o => Activate("begin.lesson",o));
                    _continuations.Enqueue(o => Run(new(Cmd.Class),o));
                    _continuations.Enqueue(o => Activate("begin.free",o));
                    _continuations.Enqueue(o => _stage = "field");
                    Pump(output); break;
                case "presented":
                    if (_presentation is null || Text(input,"presentation_id") != _presentation) throw new InvalidOperationException("呈现凭证已失效。");
                    _presentation = null;
                    if (_narrativePresentation is { } narrativeId)
                    { _narrativePresentation = null; Consume(_narrative.Presented(narrativeId),output); }
                    if (_behaviorPresentation is { } behaviorId)
                    { _behaviorPresentation=null;BehaviorEvents(_behavior.Process(new PresentationCompleted(behaviorId)),output); }
                    Pump(output); break;
                case "select":
                    var id = Text(input,"choice_id");
                    if (!_choices.TryGetValue(id,out var choice) || Text(input,"token") != choice.Token || !choice.Available()) throw new InvalidOperationException("选择已失效或当前不可用。");
                    _choices.Clear(); ++_version; choice.Submit(output); Pump(output); break;
                case "interact": Interact(input,output); break;
                case "card":
                    ShownDay=_game.Day; ShownSlot=_game.Slot;
                    if (!_examined.TryGetValue(Text(input,"card_id"),out var card)) throw new InvalidOperationException("尚未观察该卡片。");
                    ShowCard(Text(input,"card_id"),card,output); break;
                case "furniture":
                    ShownDay=_game.Day; ShownSlot=_game.Slot;
                    var hitNodes=input.GetValueOrDefault("nodes") is IEnumerable<object?> hits ? hits.Select(n=>n?.ToString()??"") : [Text(input,"node")];
                    foreach(var hitNode in hitNodes)
                    {
                        var furniture=_furniture.FirstOrDefault(f=>hitNode.StartsWith(f.Key,StringComparison.Ordinal));
                        if(furniture.Key is not null&&_furnitureSpoken.Add(Text(input,"map")+":"+hitNode)) { output.Add(Event("aside",("text",furniture.Text)));break; }
                    }
                    break;
                case "move":
                    if (!Enum.TryParse<Place>(Text(input,"place"),out var place)) throw new InvalidOperationException("未知地点。");
                    _game.HostMoveDet(place); _knownPlaces.Add(place.ToString()); Invalidate(); break;
                case "action":
                    if (_stage != "field" || _choices.Count != 0) throw new InvalidOperationException("当前不能提交时段行动。");
                    if (input.GetValueOrDefault("act") is not IReadOnlyDictionary<string,object?> action) throw new InvalidOperationException("缺少行动。");
                    Commit(Action(action),output); break;
                case "replay": Replay(input,output); break;
                default: throw new InvalidOperationException("未知章节事件：" + kind);
            }
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or KeyNotFoundException or PlayerError)
        { output.Add(Event("error",("message",error.Message))); }
        output.Add(Event("state",("snapshot",Snapshot())));
        return output;
    }

    private void Invalidate() { _choices.Clear(); ++_version; }
    private void Pump(List<Dictionary<string,object?>> output)
    { while (_presentation is null && _choices.Count == 0 && _continuations.TryDequeue(out var next)) next(output); }
    private void Lines(IEnumerable<string> lines,List<Dictionary<string,object?>> output,string speaker = "旁白",string mood = "")
    {
        var array = lines.Where(s=>!string.IsNullOrWhiteSpace(s)).Select(s=>(object)new Dictionary<string,object?> { ["speaker"]=speaker,["text"]=s,["mood"]=mood,["voice_clip"]="" }).ToArray();
        if (array.Length == 0) return;
        _presentation = Guid.NewGuid().ToString("N"); output.Add(Event("lines",("presentation_id",_presentation),("lines",array)));
    }
    private void Activate(string name,List<Dictionary<string,object?>> output)
    {
        // A new authored scene occurs in the current slot. Run's feed remains
        // labelled with the slot it just settled until a new scene is activated.
        ShownDay=_game.Day; ShownSlot=_game.Slot;
        Consume(_narrative.Activate(name,_game.Day,_game.Slot,Variables()),output);
    }
    private IReadOnlyDictionary<string,string> Variables() => new Dictionary<string,string>
    {
        ["detective_name"] = DetectiveName(), ["accused_name"] = _game.Accused is { } accused ? Names.Of(accused) : "",
        ["wrong_name"] = _wrongName
    };
    private string _wrongName = "";
    private string DetectiveName() => _game.Continuation?.Progress.GetProgress("state").GetValueOrDefault("detective_name")?.ToString() ?? "侦探";
    private void Consume(IReadOnlyList<Chapter2NarrativeEvent> frames,List<Dictionary<string,object?>> output)
    {
        foreach (var frame in frames)
        {
            switch (frame.Kind)
            {
                case "lines":
                    _presentation = frame.PresentationId; _narrativePresentation = frame.PresentationId;
                    output.Add(Event("lines",("presentation_id",frame.PresentationId),("lines",frame.Lines.Select(l=>(object)new Dictionary<string,object?> { ["speaker"]=l.Speaker,["text"]=l.Text,["mood"]=l.Mood,["voice_clip"]=l.VoiceClip }).ToArray()))); break;
                case "choice":
                    _behavior.RefreshExistingKnowledge();
                    var offered=_behavior.Process(new RequestCandidates(_actorIds[P.Det])).OfType<CandidatesAvailable>().SelectMany(c=>c.Candidates).ToDictionary(c=>c.Id,StringComparer.Ordinal);
                    var available=frame.Choices.Where(c=>offered.ContainsKey(c.CandidateId)).ToArray();
                    if(available.Length==0)throw new InvalidOperationException("当前没有适用的剧情候选。");
                    Menu("选择",available.Select(c=>(c.CandidateId,c.Label,(Func<bool>)(()=>_behavior.IsCandidateAvailable(c.CandidateId,_actorIds[P.Det])),(Action<List<Dictionary<string,object?>> >)(o=>
                    {
                        IReadOnlyList<WorldOutput> submitted;
                        try { submitted=_behavior.Process(new SelectCandidate(offered[c.CandidateId].Token)); }
                        catch
                        {
                            // Reoffer fresh Core tokens; a rejected submission must not leave an awaiting action.
                            Consume(new[] { frame },o);
                            throw;
                        }
                        if(submitted.OfType<CandidateActionStarted>().Any()) _awaitingNarrativeChoice=c.CandidateId;
                        BehaviorEvents(submitted,o);
                        if(_awaitingNarrativeChoice is not null)BehaviorEvents(_behavior.Process(new AdvanceWorld(_game.Now+5)),o);
                    }))),output,offered.ToDictionary(p=>p.Key,p=>p.Value.Token)); break;
                case "action":
                    switch (frame.ActionName)
                    {
                        case "lesson_answer": _game.HostLessonAnswer(bool.Parse(frame.Args[0])); break;
                        case "mediate": _mediating = true; break;
                        default: throw new InvalidOperationException("未绑定作者行动："+frame.ActionName);
                    }
                    break;
            }
        }
    }
    private void Menu(string title,IEnumerable<(string Id,string Label,Func<bool> Available,Action<List<Dictionary<string,object?>>> Submit)> choices,List<Dictionary<string,object?>> output,IReadOnlyDictionary<string,string>? tokens=null)
    {
        Invalidate(); var options = new List<object>();
        foreach (var c in choices)
        {
            var token = tokens?.GetValueOrDefault(c.Id)??Guid.NewGuid().ToString("N"); _choices[c.Id] = (token,c.Available,c.Submit);
            options.Add(new Dictionary<string,object?> { ["id"]=c.Id,["label"]=c.Label,["token"]=token,["available"]=c.Available(),["lock_hint"]="" });
        }
        output.Add(Event("candidates",("title",title),("options",options)));
    }
    private void Scene(Place place,List<Dictionary<string,object?>> output)
    {
        _game.HostMoveDet(place); _knownPlaces.Add(place.ToString());
        var map = place switch { Place.Hall=>"central_hall",Place.Lecture=>"lecture",Place.Logistics=>"logistics",Place.Clinic=>"clinic",Place.Greenhouse=>"greenhouse",Place.ArchiveFront=>"archive",Place.Oath=>"campus_s",Place.DormW=>"zone_jia",Place.DormS=>"zone_bing",Place.DormL=>"zone_ding",_=>"living_hall" };
        output.Add(Event("scene",("map",map),("spawn",place==Place.Logistics?"west":"south")));
    }
    private void Run(PlayerAction? action,List<Dictionary<string,object?>> output)
    {
        ShownDay=_game.Day; ShownSlot=_game.Slot; _game.RunSlot(action); Invalidate();
        var feed=_game.TakeFeed().ToArray(); _journal.Add(new object[] { Chapter2Content.TimeName(ShownDay,ShownSlot),feed });
        _knownPlaces.Add(_game.Where(_game.Det).ToString());
        Lines(feed,output);
    }
    private void Commit(PlayerAction? action,List<Dictionary<string,object?>> output)
    {
        if (_game.Ended) { Council(output); return; }
        if (_game.Day==3 && _game.Slot==Slot.Evening) { BeginConfront(output); return; }
        if (_game.Slot==Slot.Afternoon && Game.Get(_game.Det.Body,T.Hungry)>0 && action?.Cmd!=Cmd.Stay) { Lines([_content.Text("hungry")],output); return; }
        if (_game.Slot==Slot.Night && action is not null && action.Cmd!=Cmd.Stakeout) { Activate("commit.night_hint",output); return; }
        if (action?.Cmd==Cmd.Stay && _game.Slot is Slot.Noon or Slot.Evening)
        {
            action=new(Cmd.Collect);
            if (_game.Day==2 && _game.Slot==Slot.Noon)
            {
                Scene(Place.Logistics,output); _mediating=false; Activate("commit.noon_dispute",output);
                _continuations.Enqueue(o=>Run(new(_mediating?Cmd.Mediate:Cmd.Collect),o)); Pump(output); return;
            }
        }
        var night=_game.Slot==Slot.Night; Run(action,output);
        if (night)
        {
            if (_game.Ended) _continuations.Enqueue(Council);
            else if (_game.Day==3)
            {
                _continuations.Enqueue(o=>Activate("night.dawn",o));
                _continuations.Enqueue(o=> { Run(new(Cmd.Stay){Place=Place.Hall},o); Scene(Place.Hall,o); });
                _continuations.Enqueue(o=> { if (_game.Accused is null) Activate("night.no_accuse",o); });
                _continuations.Enqueue(o=>Activate("night.after",o));
            }
        }
        Pump(output);
    }
    private PlayerAction? Action(IReadOnlyDictionary<string,object?> a)
    {
        var cmd=Text(a,"cmd","stay");
        P Who() { var who=Enum.Parse<P>(Text(a,"who")); if (who==P.Det || _game.Where(_game[who])!=_game.Where(_game.Det)) throw new InvalidOperationException("人物已离开现场。"); return who; }
        Place WatchPlace() { var place=Enum.Parse<Place>(Text(a,"place"));if(place!=_game.Where(_game.Det))throw new InvalidOperationException("只能在当前空间守夜。");return place; }
        return cmd switch
        {
            "sleep"=>null,"watch"=>new(Cmd.Stakeout){Place=WatchPlace()},"stay"=>new(Cmd.Stay){Place=_game.Where(_game.Det)},
            "collect"=>new(Cmd.Collect),"class"=>new(Cmd.Class),"mediate"=>new(Cmd.Mediate),"help"=>new(Cmd.Help){Who=Who()},
            "share" when Game.Get(_game.Det.Body,T.Fed)>0=>new(Cmd.ShareFood){Who=Who()},
            "request"=>new(Cmd.Ask){Who=Who(),Job=Enum.Parse<Job>(Text(a,"job"))},
            "escort" when _examined.Values.Any(c=>Text(c,"thing")==Text(a,"thing"))=>new(Cmd.Escort){Who=Who(),Thing=Text(a,"thing"),Place=Game.Things_[Text(a,"thing")].Place},
            "tell_rule"=>TellRule(Who(),Number(a,"rule")),
            "tell_wording"=>new(Cmd.TellRule){Who=Who(),Card=Game.CaseWordings[Number(a,"wording")].Card},
            _=>throw new InvalidOperationException("无效或不可用行动："+cmd)
        };
    }
    private PlayerAction TellRule(P who,int id)
    { var spec=_game.HostTellable().FirstOrDefault(s=>s.Id==id)??throw new InvalidOperationException("该规则当前不能转述。"); return new(Cmd.TellRule){Who=who,Spec=spec,Card=spec.Card}; }

    private void Interact(IReadOnlyDictionary<string,object?> input,List<Dictionary<string,object?>> output)
    {
        if (_stage!="field" || _choices.Count>0) throw new InvalidOperationException("请先完成当前选择。");
        ShownDay=_game.Day; ShownSlot=_game.Slot;
        var map=Text(input,"map"); var site=Text(input,"site"); var target=Text(input,"target");
        if (site.StartsWith("person:",StringComparison.Ordinal)) target=site[7..];
        if (Enum.TryParse<P>(target,out var person)) { Talk(person,output); return; }
        var authored=_sites.SingleOrDefault(s=>s.Map==map&&s.LocalId==site)??throw new InvalidOperationException("未知交互地点。");
        var spec=SiteSpec(authored);
        if(_game.Where(_game.Det)!=authored.Room)throw new InvalidOperationException("交互地点已不在当前空间。");
        switch (Text(spec,"kind"))
        {
            case "bed": case "watch":
                if (_game.Slot!=Slot.Night) { Lines([Text(spec,"kind")=="bed"?_content.Text("bed_early"):_content.Text("watch_early")],output); return; }
                var isBed=Text(spec,"kind")=="bed";
                Menu(isBed?"睡下":"守夜",[("confirm",isBed?"在这里睡到天亮":"在这里守一夜",()=>_game.Slot==Slot.Night,o=>Commit(isBed?null:new(Cmd.Stakeout){Place=_game.Where(_game.Det)},o)),("cancel","返回现场",()=>true,o=>{})],output); return;
            case "meal":
                if (_game.Slot is Slot.Noon or Slot.Evening) Commit(new(Cmd.Stay),output); else Lines([_content.Text("meal_closed")],output); return;
            case "tube": if (_game.Day==3 && _game.Slot==Slot.Evening) { BeginConfront(output); return; } break;
        }
        var thing=Text(spec,"thing");
        if(authored.Thing is not null)
        {
            var physical=_behavior.Entity(authored.Thing).Get<SpatialProperty>();
            if(physical is not null && !ReferenceEquals(physical.Space,_game.Space[_game.Where(_game.Det)]))
                throw new InvalidOperationException("物件不在当前真实空间。");
        }
        var facts=thing!=""?_game.HostExamine(thing):authored.Thing is not null?_game.HostExamineEntity(_behavior.Entity(authored.Thing)):"";
        var lore=Text(spec,"lore");var text=lore==""?facts:facts==""?lore:lore+"\n"+facts;
        var id=map+":"+site; var card=new Dictionary<string,object?>{["id"]=id,["title"]=Text(spec,"title"),["text"]=text,["place"]=Names.Of(_game.Where(_game.Det)),["thing"]=thing};
        _examined[id]=card; ShowCard(id,card,output);
        if (thing=="台账" && _game.Day>=3 && !_replayOpen) { _replayOpen=true; _continuations.Enqueue(o=>Activate("card.replay_unlock",o)); }
    }
    private Dictionary<string,string> SiteSpec(Chapter2AuthoredSite site)
    {
        var thing="";
        if(site.Thing is not null)
        {
            var entity=_behavior.Entity(site.Thing);
            thing=Game.Things_.FirstOrDefault(p=>ReferenceEquals(p.Value.Thing(_game),entity)).Key??"";
        }
        return new(){["title"]=site.Title,["kind"]=site.Kind,["lore"]=site.Lore,["thing"]=thing};
    }
    public IReadOnlyDictionary<string,Dictionary<string,string>> SiteDefinitions(string map)=>_sites.Where(s=>s.Map==map).ToDictionary(s=>s.LocalId,SiteSpec,StringComparer.Ordinal);
    private bool Local(P who)=>who!=P.Det && !_game[who].Asleep && _game.Where(_game[who])==_game.Where(_game.Det) && (_nearby is null || _nearby.Contains(who.ToString()));
    private void ShowCard(string id,Dictionary<string,object?> card,List<Dictionary<string,object?>> output)
    {
        var thing=Text(card,"thing");
        var choices=new List<(string,string,Func<bool>,Action<List<Dictionary<string,object?>>>)>();
        if (thing!="") foreach(var person in _game.Npcs.Where(p=>Local(p.Id)).Take(3))
        { var who=person.Id; choices.Add(("show:"+who,"请"+person.Name+"看看（要花上一阵）",()=>Local(who),o=>Commit(new(Cmd.Escort){Who=who,Thing=thing,Place=Game.Things_[thing].Place},o))); }
        choices.Add(("close","返回现场",()=>true,o=>{}));
        Menu(Text(card,"title"),choices,output);
        var options=output[^1]["options"]; output.RemoveAt(output.Count-1); var result=new Dictionary<string,object?>(card){["kind"]="card",["options"]=options}; output.Add(result);
    }
    private void Talk(P who,List<Dictionary<string,object?>> output)
    {
        if (!Local(who)) throw new InvalidOperationException("人物当前不在场。");
        var name=Names.Of(who); var choices=new List<(string,string,Func<bool>,Action<List<Dictionary<string,object?>>>)>
        {
            ("ask","问问"+name+"知道些什么",()=>Local(who),o=>{AskReaction(who,o);_continuations.Enqueue(next=>ResumeTalk(who,next));}),
            ("escort","请他跟你去看一样东西",()=>Local(who),o=>Menu("看什么",_examined.Values.Where(c=>Text(c,"thing")!="").Select(c=>Text(c,"thing")).Distinct().Select(t=>(t,t,(Func<bool>)(()=>Local(who)),(Action<List<Dictionary<string,object?>> >)(next=>Commit(new(Cmd.Escort){Who=who,Thing=t,Place=Game.Things_[t].Place},next)))),o)),
            ("request","请他做一件事",()=>Local(who),o=>Menu("做什么",_content.Requests.Select(r=>(r.Job.ToString(),r.Label,(Func<bool>)(()=>Local(who)),(Action<List<Dictionary<string,object?>> >)(next=>Commit(new(Cmd.Ask){Who=who,Job=r.Job},next)))),o)),
            ("rule","跟他讲一条规矩",()=>Local(who),o=>RuleMenu(who,o)),
            ("help","帮他干一阵活",()=>Local(who),o=>Commit(new(Cmd.Help){Who=who},o)),
            ("share","分给他一份口粮",()=>Local(who)&&Game.Get(_game.Det.Body,T.Fed)>0,o=>Commit(new(Cmd.ShareFood){Who=who},o)),
            ("cancel","没什么了",()=>true,o=>{})
        };
        Menu("跟"+name+"说什么",choices.Where(c=>c.Item3()),output);
    }
    private void ResumeTalk(P who,List<Dictionary<string,object?>> output)
    {
        if(Local(who)) { Talk(who,output); return; }
        // A completed response does not keep a moving or sleeping body in place.
        // Close stale follow-up menus instead of failing the presentation receipt.
        Invalidate();
        output.Add(Event("interaction_closed",("actor",who.ToString()),("reason","actor_unavailable")));
    }
    private void RuleMenu(P who,List<Dictionary<string,object?>> output)
    {
        var choices=new List<(string,string,Func<bool>,Action<List<Dictionary<string,object?>>>)>();
        foreach(var spec in _game.HostTellable()) { var rule=spec; choices.Add(("rule:"+rule.Id,rule.Text,()=>Local(who)&&_game.HostTellable().Contains(rule),o=>Commit(new(Cmd.TellRule){Who=who,Spec=rule,Card=rule.Card},o))); }
        for(var i=0;i<Game.CaseWordings.Length;i++) { var index=i; choices.Add(("wording:"+i,Game.CaseWordings[i].Text,()=>Local(who),o=>Commit(new(Cmd.TellRule){Who=who,Card=Game.CaseWordings[index].Card},o))); }
        choices.Add(("cancel","返回",()=>true,o=>ResumeTalk(who,o))); Menu("讲哪一条",choices,output);
    }
    private void AskReaction(P who,List<Dictionary<string,object?>> output)
    {
        _behavior.RefreshExistingKnowledge();
        var started=_behavior.Process(new RequestReaction(_actorIds[who]));BehaviorEvents(started,output);
        if(started.Any(e=>e is CandidateActionStarted))BehaviorEvents(_behavior.Process(new AdvanceWorld(_game.Now+5)),output);
    }
    private void BehaviorEvents(IReadOnlyList<WorldOutput> events,List<Dictionary<string,object?>> output)
    {
        var reports=new Dictionary<string,(string Actor,string Name,string Text,string Mood)>();
        string? completedNarrative=null;
        foreach(var item in events)
        {
            if(item is CandidateActionFinished narrativeResult&&narrativeResult.CandidateId==_awaitingNarrativeChoice)
            { if(narrativeResult.Succeeded)completedNarrative=_awaitingNarrativeChoice;_awaitingNarrativeChoice=null; }
            if(item is CandidateActionFinished finishedReaction&&finishedReaction.Succeeded&&_reactionIntents.GetValueOrDefault(finishedReaction.CandidateId)=="testimony")
            {
                var who=_actorIds.Single(p=>p.Value==finishedReaction.Actor).Key;
                // Core has already selected and completed this authored reaction. This registry
                // supplies its factual report, not another decision between possible responses.
                var report=_game.HostAskPresentation(who, selectedTestimony: true);_talked[who.ToString()]=report.Text;
                reports[finishedReaction.Actor]=(finishedReaction.Actor,Names.Of(who),report.Text,report.Emotion);
                foreach(var place in _game.Sightings.Where(s=>s.Witness==who&&s.Day>=_game.Day-1).Select(s=>s.At))_knownPlaces.Add(place.ToString());
            }
            if(item is SemanticDialogue dialogue)
            {
                _presentation=dialogue.PresentationId;_behaviorPresentation=dialogue.PresentationId;
                var lines=dialogue.Lines.Select(l=>(object)new Dictionary<string,object?>{["speaker"]=l.Speaker,["speaker_name"]=_actorIds.FirstOrDefault(p=>p.Value==l.Speaker) is var pair&&pair.Value is not null?Names.Of(pair.Key):l.Speaker,["text"]=l.Text,["mood"]=l.Mood,["voice_clip"]=l.VoiceClip}).ToList();
                foreach(var report in reports.Values)lines.Add(new Dictionary<string,object?>{["speaker"]=report.Actor,["speaker_name"]=report.Name,["text"]=report.Text,["mood"]=report.Mood,["voice_clip"]=""});
                reports.Clear(); output.Add(Event("lines",("presentation_id",dialogue.PresentationId),("lines",lines)));
            }
            else if(item is NoApplicableCandidate unavailable) output.Add(Event("error",("message","当前没有适用的回应。"),("actor",unavailable.Actor)));
            else if(item is CandidateActionFinished finished&&!finished.Succeeded) output.Add(Event("error",("message","回应行动未完成："+finished.Failure)));
        }
        if(reports.Count>0)
        {
            _presentation=Guid.NewGuid().ToString("N");
            output.Add(Event("lines",("presentation_id",_presentation),("lines",reports.Values.Select(r=>(object)new Dictionary<string,object?>{["speaker"]=r.Actor,["speaker_name"]=r.Name,["text"]=r.Text,["mood"]=r.Mood,["voice_clip"]=""}).ToArray())));
        }
        if(completedNarrative is { } selected)Consume(_narrative.Select(selected),output);
    }
    private void BeginConfront(List<Dictionary<string,object?>> output)
    {
        _stage="confront"; _pairs.Clear(); _confrontIndex=0; Scene(Place.Hall,output); Activate("confront.evening",output);
        _continuations.Enqueue(o=>Activate("confront.begin",o)); _continuations.Enqueue(ConfrontStep); Pump(output);
    }
    private void ConfrontStep(List<Dictionary<string,object?>> output)
    {
        while(_confrontIndex<ConfrontClaims.Length&&!_game.HostClaimTrue(ConfrontClaims[_confrontIndex])) _confrontIndex++;
        if(_confrontIndex>=ConfrontClaims.Length) { Activate("confront.rule",output); _continuations.Enqueue(ConfrontProposal); Pump(output); return; }
        var expected=ConfrontClaims[_confrontIndex];
        var choices=Game.Claims.Select((claim,i)=>("claim:"+i,claim,(Func<bool>)(()=>true),(Action<List<Dictionary<string,object?>> >)(o=>ConfrontEvidence(expected,i,o)))).ToList();
        choices.Add(("pause","暂且不答",()=>true,o=>{_confrontIndex++;Activate("confront.pause",o);_continuations.Enqueue(ConfrontStep);}));
        Menu(_confrontQuestions[_confrontIndex],choices,output);
    }
    private void ConfrontEvidence(int expected,int said,List<Dictionary<string,object?>> output)
    {
        Menu("出示什么",Game.EvidenceNames.Select((name,i)=>(name,i)).Where(p=>_game.DetHasEvidence(p.i)).Select(p=>("evidence:"+p.i,p.name,(Func<bool>)(()=>_game.DetHasEvidence(p.i)),(Action<List<Dictionary<string,object?>> >)(o=>
        {
            if((said==expected||said==5)&&_game.HostPairValid(said,p.i)) { _pairs.Add((said,p.i));_confrontIndex++; if(said==5) Activate("confront.frame",o); else Lines([_game.HostReaction(said)],o); }
            else { _wrongName=p.name; Activate(said==expected?"confront.wrong_show":"confront.wrong_say",o); }
            _continuations.Enqueue(ConfrontStep);
        }))),output);
    }
    private void ConfrontProposal(List<Dictionary<string,object?>> output)
    {
        var choices=Game.CaseWordings.Select((w,i)=>("proposal:"+i,w.Text,(Func<bool>)(()=>true),(Action<List<Dictionary<string,object?>> >)(o=>ConfrontCommon(i,o)))).ToList();
        choices.Add(("none","不提新规矩",()=>true,o=>ConfrontCommon(-1,o))); Menu("今后如何",choices,output);
    }
    private void ConfrontCommon(int proposal,List<Dictionary<string,object?>> output)
    {
        void Finish(bool common,List<Dictionary<string,object?>> o) { Run(new(Cmd.Confront){Plan=new Game.ConfrontPlan(_pairs.ToArray(),proposal<0?null:Game.CaseWordings[proposal].Card,common)},o);_continuations.Enqueue(next=>Activate("confront.whisper",next));_stage="field"; }
        Menu("公开指控须附可检查的材料？",[("yes","提议共同规则",()=>true,o=>Finish(true,o)),("no","不提议",()=>true,o=>Finish(false,o))],output);
    }
    private void Council(List<Dictionary<string,object?>> output)
    {
        _stage="council"; _game.HostMoveDet(Place.Oath);_knownPlaces.Add(Place.Oath.ToString()); output.Add(Event("scene",("map","campus_s"),("spawn","gate_inside"),("mode","court"))); Activate("council.begin",output); _continuations.Enqueue(CouncilProposal); Pump(output);
    }
    private void CouncilProposal(List<Dictionary<string,object?>> output)
    {
        var choices=Game.ArchiveWordings.Select((w,i)=>("archive:"+i,w.Text,(Func<bool>)(()=>true),(Action<List<Dictionary<string,object?>> >)(o=>{_game.CardSpec(Game.ArchiveWordings[i].Card,P.Det);CouncilSupport(o);}))).ToList();
        choices.Add(("none","不提新写法",()=>true,CouncilSupport)); Menu("名录库之议",choices,output);
    }
    private void CouncilSupport(List<Dictionary<string,object?>> output)
    {
        var choices=_game.ArchivePlans().Select((s,i)=>("support:"+i,s.Text,(Func<bool>)(()=>true),(Action<List<Dictionary<string,object?>> >)(o=>FinishCouncil(i,o)))).ToList();
        choices.Add(("abstain","不表态",()=>true,o=>FinishCouncil(-1,o)));Menu("支持哪一条",choices,output);
    }
    private void FinishCouncil(int support,List<Dictionary<string,object?>> output)
    {
        var ending=_game.Council(support);_stage="closing";
        Lines(_game.CouncilLog,output);
        _continuations.Enqueue(o=>Activate(ending==Ending.Sealed?"council.sealed":"council.box",o));
        _continuations.Enqueue(o=>
        {
            _ended=true;_stage="ended";
            o.Add(Event("end",("result",new Dictionary<string,object?>
            {
                ["result"]=ending.ToString(),["result_text"]=Game.EndingText(ending),
                ["quer"]=_game.QuerCleared?"雀儿洗清了冤屈，住回甲区":_game.QuerOstracized?"雀儿被当成私取之人，搬出甲区独宿":"雀儿的事没有人再提",
                ["det_hooks"]=_game.Det.Hooks,["common_rule"]=_game.CommonRuleEstablished,["case_rule"]=_game.CaseRule?.Text??"",
                ["log"]=_game.CouncilLog.ToArray(),["hooks"]=_game.Npcs.Where(p=>p.Hooks>0).Select(p=>p.Name).ToArray(),["impressions"]=Impressions(),
                ["stands"]=_game.CouncilChoice.ToDictionary(p=>p.Key.ToString(),p=>Game.EndingText(Game.FamilyOf(p.Value)))
            })));
        });
    }
    private void Replay(IReadOnlyDictionary<string,object?> input,List<Dictionary<string,object?>> output)
    {
        if(!_replayOpen) throw new InvalidOperationException("尚未获得重演手稿。");
        var values=(input.GetValueOrDefault("vars") as IEnumerable<object?>)?.Select(v=>Convert.ToInt32(v)).ToArray()??throw new InvalidOperationException("缺少重演变量。");
        if(values.Length!=4||values.Where((v,i)=>v<0||v>=CaseVars.Options[i].Length).Any()) throw new InvalidOperationException("重演变量无效。");
        var (traces,world)=_game.ReplayCase(new(values[0],values[1],values[2],values[3]));var actual=_game.CaseActual??traces;var known=_game.DetEvidence();var r=traces.Describe().ToArray();var a=actual.Describe().ToArray();
        var findings=r.Select((t,i)=>(object)new Dictionary<string,object?>{["index"]=i,["name"]=_content.Texts("finding_titles",5)[i],["status"]=!known.Contains(i)?"unvisited":t.Text==a[i].Text?"match":"conflict",["text"]=!known.Contains(i)?"还没查过":t.Text==a[i].Text?a[i].Text:$"重演中：{t.Text}；现场：{a[i].Text}"}).ToArray();
        var full=r.Select((t,i)=>known.Contains(i)&&t.Text==a[i].Text).All(v=>v);
        _replayResult=new(){["findings"]=findings,["full_match"]=full,["steps"]=Game.InkSteps(world,2).Select(s=>(object)new Dictionary<string,object?>{["t"]=s.Time,["who"]=s.Who.ToString(),["name"]=Names.Of(s.Who),["build"]=s.Build switch{Build.Small=>"瘦小",Build.Large=>"高大",_=>"中等"},["from"]=s.From.ToString(),["to"]=s.To.ToString(),["act"]=s.Act}).ToArray()};
        _replayCount++; if(_replayCount==1&&!full)_continuations.Enqueue(o=>Activate("after_replay.first",o));if(full&&!_fullMatch){_fullMatch=true;_continuations.Enqueue(o=>Activate("after_replay.match",o));} Pump(output);
    }
    private string[] Impressions()=>_game.Npcs.Where(p=>p.Id is P.Ward or P.Quer or P.Mora or P.Huo or P.Ruan or P.Margaret).Select(p=>
    {
        var (c,i)=_game.ImageOfDet(p);
        var text=c>=2?"说话不避着你":c>=1?"对你还算客气":c>=0?"对你话不多":"明显不想跟你多说";
        if(i>=2)text+="，信你的判断"; return p.Name+"："+text;
    }).ToArray();
    public IReadOnlyDictionary<string,object?> Snapshot()=>new Dictionary<string,object?>
    {
        ["detective_name"]=DetectiveName(),["stage"]=_stage,["ended"]=_ended,["replay_open"]=_replayOpen,["replay_result"]=_replayResult,
        ["known_places"]=_knownPlaces.ToDictionary(k=>k,k=>(object?)true),["examined"]=_examined,["talked"]=_talked,["journal"]=_journal,
        ["people"]=_game.Npcs.Select(p=>(object)new Dictionary<string,object?>{["key"]=p.Id.ToString(),["name"]=p.Name,["place"]=_game.Where(p).ToString(),["doing"]=_game.LastDoing(p.Id,ShownDay,ShownSlot)?.Text??"",["frame"]=_game.LastDoing(p.Id,ShownDay,ShownSlot)?.Frame??"",["asleep"]=p.Asleep,["present"]=!p.Asleep,["displayable"]=!p.Asleep,["court_present"]=(_stage is "council" or "closing")&&Game.Get(p.Body,T.Sick)==0,["sick"]=Game.Get(p.Body,T.Sick)>0}).ToArray()
    };
}
