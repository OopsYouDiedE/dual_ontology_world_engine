using static DualOntologyWorldEngine.TestSupport.FixtureData;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DualOntologyWorldEngine.Gameplay;
using DualOntologyWorldEngine.Chapter2.Prototype;
using Ev = System.Collections.Generic.Dictionary<string, object?>;

var root = Path.GetFullPath(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
var outputArgument = Array.IndexOf(args, "--output");
var outputDirectory = outputArgument < 0 ? null : outputArgument + 1 < args.Length ? args[outputArgument + 1] : throw new ArgumentException("--output requires a directory");
try { var transcript=new Transcript(root,args.Contains("--export"),outputDirectory);if(args.Contains("--export"))transcript.Export();else transcript.Run(); }
catch(Exception error) {Console.Error.WriteLine(error);Environment.ExitCode=1;}

// One semantic playthrough. All world changes enter the production GameSession.
// This host replaces only spatial arrival reports, never decisions or game state.
sealed class Transcript
{
    readonly string root;
    readonly GameSession game;
    readonly JsonDocument layout, content;
    readonly IReadOnlyList<Chapter2AuthoredSite> campusSites;
    readonly StreamWriter log;
    readonly StreamWriter raw;
    readonly Queue<Ev> events = new();
    readonly Dictionary<string, (double X, double Y)> actors = new();
    readonly Dictionary<string, string> propStates = new();
    readonly Dictionary<string, string> stateText = new();
    readonly HashSet<string> superseded = new();
    string map = "", desiredCard = "", desiredChoice = "";
    bool chapter2, asking, waitingView;
    int request, answer;
    int dispatch;
    int routeDay;
    readonly HashSet<(int Day, string Daypart, string Person)> askedThisPeriod = [];
    readonly bool exportOnly;
    string visibleRoom="";
    public Transcript(string root,bool exportOnly=false,string? outputDirectory=null)
    {
        this.root = root;
        this.exportOnly=exportOnly;
        string Read(string p) => File.ReadAllText(Path.Combine(root, p));
        layout = JsonDocument.Parse(Read("godot_csharp/scenes/Awakening/Script/generated_awakening_layout.json"));
        content = JsonDocument.Parse(Read("godot_csharp/scenes/Awakening/Script/generated_awakening_content.json"));
        var script = Read("godot_csharp/scenes/Chapter2/Script/generated_chapter_02.json");
        campusSites = Chapter2AuthoredSites.ReadAll(script);
        game = exportOnly ? null! : new(script, "living", "campus");
        var destination = outputDirectory is null ? Path.Combine(root, "artifacts/basic-ui-system") : Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(destination);
        log = new(Path.Combine(destination, "02-引擎输出的玩家可见动作.txt"), false, new UTF8Encoding(false)) { AutoFlush = true };
        var rawDirectory=Path.Combine(destination,"player-traces");Directory.CreateDirectory(rawDirectory);
        raw = exportOnly ? null! : new(Path.Combine(rawDirectory,"engine-full-output.jsonl"),false,new UTF8Encoding(false)){AutoFlush=true};
    }
    static Ev E(params (string K, object? V)[] ps) => ps.ToDictionary(p => p.K, p => p.V);
    static string S(IReadOnlyDictionary<string,object?> e, string k) => e.GetValueOrDefault(k)?.ToString() ?? "";
    static Ev M(IReadOnlyDictionary<string,object?> e, string k) => e.GetValueOrDefault(k) as Ev ?? new();
    static object?[] A(IReadOnlyDictionary<string,object?> e,string k) => e.GetValueOrDefault(k) is IEnumerable<object?> xs ? xs.ToArray() : [];
    static IEnumerable<Ev> Maps(IReadOnlyDictionary<string,object?> e,string k) => A(e,k).OfType<Ev>();
    static readonly Regex StyleTags=new(@"\[/?(?:b|i|u|s|code|kbd|color|bgcolor|font|font_size|outline_size|outline_color|center|left|right|fill|indent|url|hint|wave|tornado|shake|fade|rainbow|pulse)(?:=[^\]]*)?\]",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    void Note(string t) => log.WriteLine(StyleTags.Replace(t,""));
    void Input(string label, Ev input)
    {
        Note($"\n【操作 {++request:D4}】{label}");
        if(S(input,"kind") is "start" or "navigate")waitingView=true;
        if(S(input,"kind")=="view_ready")waitingView=false;
        Send(input); Drain();
    }
    void Send(Ev input)
    {
        if (!chapter2 && actors.Count > 0) input["context"] = Context();
        var output=game.Dispatch(input);
        raw.WriteLine(JsonSerializer.Serialize(new { sequence=++dispatch, input, output }));
        if(!chapter2)
        {
            var cards=output.Where(e=>S(e,"kind")=="card").ToArray();
            foreach(var earlier in cards.SkipLast(1))superseded.Add(S(earlier,"presentation_id"));
        }
        foreach (var ev in output) events.Enqueue(ev);
    }
    Ev Context()
    {
        var people = new Ev();
        var det = actors.GetValueOrDefault("detective");
        foreach (var (who,p) in actors)
        {
            var room = Room(p);
            var near = layout.RootElement.GetProperty("sites").EnumerateArray().Where(s => Distance(s.GetProperty("x").GetDouble()-p.X,s.GetProperty("y").GetDouble()-p.Y)<70).Select(s => (object?)s.GetProperty("id").GetString()).ToArray();
            people[who] = E(("room",room), ("entered",room), ("room_name",RoomName(room)), ("visible",Room(p)==Room(det)), ("moving",false), ("distance",Distance(p.X-det.X,p.Y-det.Y)), ("near_sites",near));
        }
        var sites = new Ev();
        foreach(var site in layout.RootElement.GetProperty("sites").EnumerateArray()) sites[site.GetProperty("id").GetString()!] = Room((site.GetProperty("x").GetDouble(),site.GetProperty("y").GetDouble()));
        return E(("field_active",true), ("modal",""), ("actors",people), ("site_rooms",sites));
    }
    string Room((double X,double Y) p)
    {
        foreach(var r in content.RootElement.GetProperty("ROOM_REGIONS").EnumerateArray())
        {
            var a=r.GetProperty("rect").EnumerateArray().ToArray();
            if((a[0].ValueKind==JsonValueKind.Null||p.X>=a[0].GetDouble()) && (a[1].ValueKind==JsonValueKind.Null||p.Y>=a[1].GetDouble()) && (a[2].ValueKind==JsonValueKind.Null||p.X<a[2].GetDouble()) && (a[3].ValueKind==JsonValueKind.Null||p.Y<a[3].GetDouble()))
            {
                var name = r.GetProperty("name").GetString();
                foreach(var n in content.RootElement.GetProperty("ROOM_NAMES").EnumerateObject()) if(n.Value.GetString()==name) return n.Name;
                return name ?? "";
            }
        }
        return "";
    }
    string RoomName(string r) => content.RootElement.GetProperty("ROOM_NAMES").TryGetProperty(r,out var n) ? n.GetString()! : r;
    static double Distance(double x,double y)=>Math.Sqrt(x*x+y*y);
    (double,double) Point(JsonElement p) => (p[0].GetDouble(),p[1].GetDouble());
    (double,double) Resolve(Ev to)
    {
        string a=S(to,"anchor");
        if(a.StartsWith("post:")) return Point(layout.RootElement.GetProperty("posts").GetProperty(a[5..]));
        if(a.StartsWith("meeting:")) return a[8..] switch { "Engineer" => (430,230), "Chef" => (480,250), "Musician" => (540,230), _ => (480,300) };
        if(a!="" && layout.RootElement.GetProperty("staging").TryGetProperty(a,out var pos)) return Point(pos);
        var site = S(to,"approach_site"); if(site=="")site=S(to,"near_site");
        if(site!="") { var s=layout.RootElement.GetProperty("sites").EnumerateArray().First(x=>x.GetProperty("id").GetString()==site); return (s.GetProperty("x").GetDouble()+20,s.GetProperty("y").GetDouble()); }
        if(S(to,"near_actor") is { Length:>0 } who) { var p=actors[who];return(p.X+30,p.Y); }
        var pair=A(to,"point"); if(pair.Length==2)return(Convert.ToDouble(pair[0]),Convert.ToDouble(pair[1]));
        throw new InvalidOperationException("Unrecognized spatial destination.");
    }
    void Drain()
    {
        int idle=0, guard=0;
        while(true)
        {
            if(++guard>25000) throw new InvalidOperationException("Presentation did not settle.");
            if(events.TryDequeue(out var ev)) { Observe(ev); Respond(ev); continue; }
            var snap=game.Snapshot();
            if(waitingView || chapter2 || snap.GetValueOrDefault("ended") is true || snap.GetValueOrDefault("field_enabled") is true) return;
            if(++idle>400) throw new InvalidOperationException("C1 stalled at "+S(snap,"stage"));
            Send(E(("kind","tick"),("delta",.1)));
        }
    }
    void Respond(Ev ev)
    {
        string kind=S(ev,"kind"), pid=S(ev,"presentation_id");
        if(superseded.Contains(pid))return;
        if(kind=="error") throw new InvalidOperationException(S(ev,"message"));
        if(kind=="scene") map=S(ev,"map");
        if(kind=="stage")
        {
            var op=S(ev,"op");var who=S(ev,"who");
            if(op is "place" or "walk") actors[who]=Resolve(M(ev,"to"));
            if(op=="daily_go")
            {
                var room=S(ev,"room");
                var p=room switch { "hall" => (490d,280d), "pantry" => (470d,600d), "foyer" => (230d,370d), "R05" => (970d,190d),"R06"=>(930d,590d),"R07"=>(710d,590d),"R08"=>(710d,190d), _ => actors.GetValueOrDefault(who) };
                actors[who]=p;
                Send(E(("kind","daily"),("who",who),("serial",ev["serial"]),("room",room),("status","arrived"),("action",ev.GetValueOrDefault("action"))));
            }
            if(pid!="") Send(E(("kind","presented"),("presentation_id",pid),("ok",true)));
            return;
        }
        if(kind=="lines")
        {
            var choices=Maps(ev,"choices").Where(Available).ToArray();
            if(choices.Length>0) Select(ev,Pick(choices));
            else Send(E(("kind","presented"),("presentation_id",pid)));
        }
        if(kind is "candidates" or "card")
        {
            var options=Maps(ev,"options").Where(Available).ToArray();
            if(options.Length>0) Select(ev,Pick(options,kind=="card" ? S(ev,"id") : ""));
        }
        if(kind=="form")
        {
            var fields=Maps(ev,"fields").ToArray(); var values=new Ev(); string action="confirm";
            if(fields.Any(f=>S(f,"id")=="name")) values["name"]=SampleText("player-transcript", "text-001");
            else if(fields.Any(f=>S(f,"id")=="sentence")) { values=(Ev)Decode(Sample("player-transcript", "sentence-form-values"))!;action="start"; }
            else if(fields.Any(f=>S(f,"id")=="say"))
            {
                var pick=Sample("player-transcript", "answers")[answer++];
                var say=Maps(fields.First(f=>S(f,"id")=="say"),"options").Single(o=>S(o,"id")==pick.GetProperty("say").GetString());
                var evidence=Maps(fields.First(f=>S(f,"id")=="show"),"options").Single(o=>S(o,"id")==pick.GetProperty("show").GetString());
                values["say"]=say["id"];values["show"]=evidence["id"];action="submit";
                Note(SampleText("player-transcript", "text-006")+S(say,"label")+SampleText("player-transcript", "text-007")+S(evidence,"label"));
            }
            else throw new InvalidOperationException("Unexpected form "+S(ev,"title"));
            Send(E(("kind","form"),("presentation_id",pid),("action",action),("values",values)));
        }
        if(kind=="replay_play") Send(E(("kind","presented"),("presentation_id",pid)));
    }
    static bool Available(Ev o) => o.GetValueOrDefault("available") is not false;
    Ev Pick(Ev[] opts,string card="")
    {
        Ev? Find(string id)=>opts.FirstOrDefault(o=>S(o,"id")==id);
        if(desiredChoice!="" && Find(desiredChoice) is { } desired) { desiredChoice="";return desired; }
        if(card!="" && desiredCard!="") { var picked=opts.FirstOrDefault(o=>S(o,"id").Contains(":"+desiredCard)); if(picked is not null){desiredCard="";return picked;} }
        if(!chapter2)
        {
            if(card!="")return opts.First(o=>S(o,"id").EndsWith(":close")||S(o,"id")=="close");
            foreach(var id in SampleStrings("player-transcript", "chapter1-choices"))
                if(Find(id) is {} preferred)return preferred;
            return opts.First(o=>!SampleStrings("player-transcript", "avoid-choices").Contains(S(o,"id")));
        }
        foreach(var id in SampleStrings("player-transcript", "priority-choices"))
            if(Find(id) is {} preferred)return preferred;
        if(asking && Find("ask") is {} ask) {asking=false;return ask;}
        if(Find("pause") is {} pause){Note(SampleText("player-transcript", "text-010"));return pause;}
        if(Find("yes") is {} yes)return yes;
        foreach(var id in SampleStrings("player-transcript", "council-choices"))
            if(Find(id) is {} chosen)return chosen;
        return Find("close")??Find("confirm")??Find("cancel")??opts[0];
    }
    void Select(Ev ev,Ev option)
    {
        Note(SampleText("player-transcript", "text-013")+S(option,"label"));
        Send(E(("kind","select"),("presentation_id",S(ev,"presentation_id")),("choice_id",option["id"]),("token",option.GetValueOrDefault("token"))));
    }
    void Text(Ev ev,params string[] keys) {foreach(var k in keys)if(S(ev,k)!="")Note(S(ev,k));}
    void Observe(Ev ev)
    {
        string kind=S(ev,"kind");
        if(kind=="lines")
        {
            foreach(var line in Maps(ev,"lines"))
            {
                var speaker=S(line,"speaker_name");if(speaker=="")speaker=S(line,"speaker");
                Note((speaker==""?"":speaker+"：")+S(line,"text"));
            }
            Choices(ev,"choices");
        }
        if(kind is "card" or "candidates" or "form" or "ending")
        {
            Note("【"+(kind switch {"card"=>SampleText("player-transcript", "text-014"),"form"=>SampleText("player-transcript", "text-015"),"ending"=>SampleText("player-transcript", "text-016"),_=>SampleText("player-transcript", "text-017")})+"】"+S(ev,"title"));
            Text(ev,"prompt","intro","source","facts","outcome");
            foreach(var k in new[]{"items","notes","rows","table"})foreach(var x in A(ev,k))Note(x is IEnumerable<object?> row ? string.Join("｜",row) : x?.ToString()??"");
            Text(M(ev,"header"),"question","instructions");
            Choices(ev,"options");
            foreach(var field in Maps(ev,"fields")){Text(field,"heading","placeholder");if(!Maps(field,"options").Any())Text(field,"empty_text");Choices(field,"options");}
        }
        if(kind=="aside")Text(ev,"text");
        if(kind=="replay_play")
        {
            Text(ev,"caption");
            foreach(var frame in Maps(ev,"frames")) Text(frame,"label");
        }
        if(kind=="stage" && S(ev,"op")=="prop")
        {
            var id=S(ev,"id");var status=S(ev,"state");propStates[id]=status;
            string? name = id=="isolation_door" ? SampleText("player-transcript", "text-018") : layout.RootElement.GetProperty("sites").EnumerateArray().FirstOrDefault(s=>s.GetProperty("id").GetString()==id) is var site && site.ValueKind!=JsonValueKind.Undefined ? site.GetProperty("title").GetString() : null;
            if(name is not null)Note(SampleText("player-transcript", "text-019")+name+"："+(status switch {"open"=>SampleText("player-transcript", "text-020"),"taken"=>SampleText("player-transcript", "text-021"),"returned"=>SampleText("player-transcript", "text-022"),_=>status}));
        }
        if(kind=="state") VisibleState(M(ev,"snapshot"));
    }
    void Choices(Ev ev,string key){foreach(var o in Maps(ev,key))Note(SampleText("player-transcript", "text-023")+S(o,"label")+(Available(o)?"":"（"+S(o,"lock_hint")+"）"));}
    void Changed(string key,string text){if(text!=""&&stateText.GetValueOrDefault(key)!=text){stateText[key]=text;Note(text);}}
    void VisibleState(Ev snap)
    {
        var hud=M(snap,"hud");Changed("time",SampleText("player-transcript", "text-024")+S(hud,"title")+" "+S(hud,"time"));
        var sites=M(snap,"sites");
        var names=new List<string>();
        foreach(var (id,obj) in sites)
        {
            if(obj is not Ev site||S(site,"title")==""||id.StartsWith("person:")||id is "huo" or "shen" or "su")continue;
            if(waitingView||(!chapter2&&exportOnly&&visibleRoom=="")||(!chapter2&&!exportOnly&&!actors.ContainsKey("detective")))continue;
            if(!chapter2)
            {
                var def=layout.RootElement.GetProperty("sites").EnumerateArray().FirstOrDefault(s=>s.GetProperty("id").GetString()==id);
                if(def.ValueKind==JsonValueKind.Undefined)continue;
                if(Room((def.GetProperty("x").GetDouble(),def.GetProperty("y").GetDouble()))!=(exportOnly?visibleRoom:Room(actors.GetValueOrDefault("detective"))))continue;
            }
            var status=!chapter2||S(site,"kind")=="" ? "（"+(site.GetValueOrDefault("new") is true?SampleText("player-transcript", "text-025"):SampleText("player-transcript", "text-026"))+"）" : "";
            names.Add(S(site,"title")+status);
        }
        if(names.Count>0)Changed("items",SampleText("player-transcript", "text-027")+string.Join("；",names.Distinct()));
        var replay=M(snap,"replay");
        if(replay.GetValueOrDefault("open") is true)
        {
            var sb=new StringBuilder(SampleText("player-transcript", "text-028")+S(replay,"title")+"\n"+S(replay,"hint"));
            foreach(var q in Maps(replay,"questions")){sb.AppendLine().Append(S(q,"question")).Append(S(q,"mark"));foreach(var o in A(q,"options"))sb.AppendLine().Append(SampleText("player-transcript", "text-023")).Append(o);}
            Changed("replayquestions",sb.ToString());
            var result=M(replay,"result");
            if(result.Count>0)
            {
                var rs=new StringBuilder(SampleText("player-transcript", "text-029"));
                foreach(var f in Maps(result,"findings"))rs.AppendLine().Append(S(f,"title")).Append("：").Append(S(f,"text")).Append("（").Append(S(f,"status") switch {"match"=>SampleText("player-transcript", "text-030"),"conflict"=>SampleText("player-transcript", "text-031"),_=>SampleText("player-transcript", "text-032")}).Append('）');
                Changed("replayresult",rs.ToString());
            }
        }
    }
    void C1Site(string id,string option,int waitTicks)
    {
        var site=layout.RootElement.GetProperty("sites").EnumerateArray().First(s=>s.GetProperty("id").GetString()==id);
        actors["detective"]=(site.GetProperty("x").GetDouble()+15,site.GetProperty("y").GetDouble());
        Input(SampleText("player-transcript", "text-033")+site.GetProperty("title").GetString(),E(("kind","tick"),("delta",.1)));
        desiredCard=option;Input(SampleText("player-transcript", "text-034")+site.GetProperty("title").GetString(),E(("kind","interact"),("site",id)));
        for(var i=0;i<waitTicks;i++){ Send(E(("kind","tick"),("delta",.1))); Drain(); }
    }
    void Move(string map)
    {
        this.map=map;
        var site=campusSites.First(s=>s.Map==map);
        Input(SampleText("player-transcript", "text-035")+Names.Of(site.Room),E(("kind","move"),("map",map),("place",site.Room.ToString())));
    }
    void InspectMap(string map)
    {
        Move(map);
        foreach(var site in campusSites.Where(s=>s.Map==map&&!SampleStrings("player-transcript", "inspection-exclude-kinds").Contains(s.Kind)))
            Input(SampleText("player-transcript", "text-034")+site.Title,E(("kind","interact"),("map",map),("site",site.LocalId)));
    }
    void AskLocal(int limit=2)
    {
        var room=campusSites.First(s=>s.Map==map).Room.ToString();
        var current = (Ev)game.Snapshot();
        var people=Maps(current,"people").Where(p=>S(p,"place")==room&&p.GetValueOrDefault("asleep") is not true).Take(limit).ToArray();
        foreach(var p in people)
        {
            if (!askedThisPeriod.Add((routeDay, S(current,"daypart"), S(p,"key")))) continue;
            asking=true;Input(SampleText("player-transcript", "text-036")+S(p,"name"),E(("kind","interact"),("map",map),("target",S(p,"key"))));
        }
    }
    void Act(string cmd,string label)=>Input(label,E(("kind","action"),("act",E(("cmd",cmd)))));
    void Header()
    {
        Note(SampleText("player-transcript", "text-037"));
        Note(SampleText("player-transcript", "text-038")+(exportOnly?SampleText("player-transcript", "text-039"):SampleText("player-transcript", "text-040")));
        Note(SampleText("player-transcript", "text-041"));
    }
    static object? Decode(JsonElement element)=>element.ValueKind switch
    {
        JsonValueKind.Object=>element.EnumerateObject().ToDictionary(p=>p.Name,p=>Decode(p.Value)),
        JsonValueKind.Array=>element.EnumerateArray().Select(Decode).ToArray(),
        JsonValueKind.String=>element.GetString(), JsonValueKind.True=>true,JsonValueKind.False=>false,
        JsonValueKind.Number=>element.TryGetInt32(out var number)?(object)number:element.GetDouble(), _=>null
    };
    void ExecuteSteps(JsonElement steps)
    {
        foreach(var step in steps.EnumerateArray())
        {
            string Value(string key)=>step.GetProperty(key).GetString()!;
            string Label()=>SampleText("player-transcript",Value("label"));
            switch(Value("op"))
            {
                case "input": Input(Label(),(Ev)Decode(step.GetProperty("input"))!);break;
                case "position": actors[Value("actor")]=Point(step.GetProperty("point"));break;
                case "prefer-choice": desiredChoice=Value("id");break;
                case "site": C1Site(Value("id"),Value("action"),step.GetProperty("wait_ticks").GetInt32());break;
                case "move": Move(Value("map"));break;
                case "inspect": InspectMap(Value("map"));break;
                case "ask": AskLocal(step.GetProperty("limit").GetInt32());break;
                case "action": Act(Value("cmd"),Label());break;
                case "interact-kind":
                    var site=campusSites.First(s=>s.Map==Value("map")&&s.Kind==Value("kind"));
                    Input(Label(),E(("kind","interact"),("map",site.Map),("site",site.LocalId)));break;
                default: throw new InvalidDataException("Unknown route operation: "+Value("op"));
            }
        }
    }
    public void Export()
    {
        var labels=new Dictionary<string,string>();var fields=new Dictionary<string,Dictionary<string,string>>();
        Ev final=new();
        try
        {
            Header();
            foreach(var line in File.ReadLines(Path.Combine(root,"artifacts/basic-ui-system/player-traces/engine-full-output.jsonl")))
            {
                using var packet=JsonDocument.Parse(line);var data=(Ev)Decode(packet.RootElement)!;
                var input=M(data,"input");var kind=S(input,"kind");
                var reported=M(M(input,"context"),"actors");var det=M(reported,"detective");
                if(S(det,"room")!="")visibleRoom=S(det,"room");
                string title="";
                if(kind=="start"){waitingView=true;title=SampleText("player-transcript", "text-042");}
                if(kind=="view_ready"){waitingView=false;title=SampleText("player-transcript", "text-043");}
                if(kind=="navigate"){chapter2=true;waitingView=true;title=SampleText("player-transcript", "text-044");}
                if(kind=="move")
                {
                    map=S(input,"map");var site=campusSites.FirstOrDefault(s=>s.Map==map);
                    title=SampleText("player-transcript", "text-035")+(site is null?SampleText("player-transcript", "text-045"):Names.Of(site.Room));
                }
                if(kind=="interact")
                {
                    var target=S(input,"target");var site=S(input,"site");
                    if(target!="")title=SampleText("player-transcript", "text-036")+(Enum.TryParse<P>(target,out var person)?Names.Of(person):content.RootElement.GetProperty("PERSON_NAMES").GetProperty(target).GetString());
                    else if(chapter2)title=SampleText("player-transcript", "text-034")+campusSites.First(s=>s.Map==S(input,"map")&&s.LocalId==site).Title;
                    else
                    {
                        var definition=layout.RootElement.GetProperty("sites").EnumerateArray().First(s=>s.GetProperty("id").GetString()==site);
                        title=SampleText("player-transcript", "text-034")+definition.GetProperty("title").GetString();
                    }
                }
                if(kind=="action")title=S(M(input,"act"),"cmd") switch{"stay"=>SampleText("player-transcript", "text-046"),"class"=>SampleText("player-transcript", "text-047"),"sleep"=>SampleText("player-transcript", "text-048"),_=>SampleText("player-transcript", "text-049")};
                if(kind=="replay")title=SampleText("player-transcript", "text-050");
                if(title!="")Note($"\n【操作 {++request:D4}】{title}");
                if(kind=="select"&&labels.TryGetValue(S(input,"choice_id"),out var chosen))Note(SampleText("player-transcript", "text-013")+chosen);
                if(kind=="form")
                {
                    var values=M(input,"values");var selectedValues=new List<string>();
                    foreach(var (id,value) in values)
                        selectedValues.Add(id=="name"?SampleText("player-transcript", "text-051")+value:fields.TryGetValue(id,out var choices)&&choices.TryGetValue(value?.ToString()??"",out var label)?label:"");
                    Note(SampleText("player-transcript", "text-006")+string.Join("；",selectedValues.Where(s=>s!="")));
                }
                foreach(var ev in Maps(data,"output"))
                {
                    if(!chapter2&&S(ev,"kind")=="stage"&&S(ev,"op") is "place" or "walk")
                    {
                        actors[S(ev,"who")]=Resolve(M(ev,"to"));
                        if(S(ev,"who")=="detective")visibleRoom=Room(actors["detective"]);
                    }
                    foreach(var option in Maps(ev,"options").Concat(Maps(ev,"choices")))labels[S(option,"id")]=S(option,"label");
                    if(S(ev,"kind")=="form")
                    {
                        fields.Clear();foreach(var field in Maps(ev,"fields"))fields[S(field,"id")]=Maps(field,"options").ToDictionary(o=>S(o,"id"),o=>S(o,"label"));
                    }
                    Observe(ev);
                    if(S(ev,"kind")=="state")final=M(ev,"snapshot");
                }
            }
            if(!chapter2||final.GetValueOrDefault("ended") is not true)throw new InvalidDataException("Saved continuous record is incomplete.");
            Note(SampleText("player-transcript", "text-052"));
            Console.WriteLine("Exported existing complete record; no game dispatch performed.");
        }
        finally {log.Dispose();layout.Dispose();content.Dispose();}
    }
    public void Run()
    {
        try
        {
            Header();
            ExecuteSteps(Sample("player-transcript", "chapter1-route"));
            if(game.Snapshot().GetValueOrDefault("ended") is not true)throw new InvalidOperationException("C1 did not end.");
            chapter2=true;
            ExecuteSteps(Sample("player-transcript", "continuation-start"));
            foreach(var day in Sample("player-transcript", "continuation-days").EnumerateArray())
            {
                routeDay=day.GetProperty("day").GetInt32();
                Note(string.Format(SampleText("player-transcript", "day-heading"),routeDay-1));
                ExecuteSteps(day.GetProperty("steps"));
            }
            if(game.Snapshot().GetValueOrDefault("ended") is not true)throw new InvalidOperationException("Continuous session did not reach ended.");
            Note(SampleText("player-transcript", "text-066"));
            Console.WriteLine("Complete continuous transcript: "+request+" semantic operations; ended.");
        }
        finally { log.Dispose();raw.Dispose();game.Dispose();layout.Dispose();content.Dispose(); }
    }
}
