using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DualOntologyWorldEngine.Gameplay;
using DualOntologyWorldEngine.Chapter2.Prototype;
using Ev = System.Collections.Generic.Dictionary<string, object?>;

var root = Path.GetFullPath(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
try { var transcript=new Transcript(root,args.Contains("--export"));if(args.Contains("--export"))transcript.Export();else transcript.Run(); }
catch(Exception error) {Console.Error.WriteLine(error);Environment.ExitCode=1;}

// One semantic playthrough. All world changes enter the production GameSession.
// This host replaces only spatial arrival reports, never decisions or game state.
// STORY-HARDCODE[CSH-011][logic][tool]: 玩家记录器绑定第一章与学园地点、人物、调查路线及条件分支。
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
    public Transcript(string root,bool exportOnly=false)
    {
        this.root = root;
        this.exportOnly=exportOnly;
        string Read(string p) => File.ReadAllText(Path.Combine(root, p));
        layout = JsonDocument.Parse(Read("godot_csharp/scenes/Awakening/Script/generated_awakening_layout.json"));
        content = JsonDocument.Parse(Read("godot_csharp/scenes/Awakening/Script/generated_awakening_content.json"));
        var script = Read("godot_csharp/scenes/Chapter2/Script/generated_chapter_02.json");
        campusSites = Chapter2AuthoredSites.ReadAll(script);
        game = exportOnly ? null! : new(script, "living", "campus");
        log = new(Path.Combine(root, "artifacts/basic-ui-system/02-引擎输出的玩家可见动作.txt"), false, new UTF8Encoding(false)) { AutoFlush = true };
        var rawDirectory=Path.Combine(root,"artifacts/basic-ui-system/player-traces");Directory.CreateDirectory(rawDirectory);
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
    // STORY-HARDCODE[CSH-012][logic][tool]: 预设第一章作答文本/物证映射及第五日学园议事选择策略。
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
            if(fields.Any(f=>S(f,"id")=="name")) values["name"]="侦探";
            else if(fields.Any(f=>S(f,"id")=="sentence")) { values["sentence"]="0";values["spot"]="foyer";action="start"; }
            else if(fields.Any(f=>S(f,"id")=="say"))
            {
                string[] texts=["是我闩的门。","门外没有人。那声音是从主楼对着铜口说的，从管接头漏到了门边。","他说的是：闩门，别开。","我信霍铎——我们早已相熟。"];
                string[] shows=["E3","E8","E7","E9"];
                var say=Maps(fields.First(f=>S(f,"id")=="say"),"options").Single(o=>S(o,"label")==texts[answer]);
                values["say"]=say["id"];values["show"]=shows[answer++];action="submit";
                var evidence=Maps(fields.First(f=>S(f,"id")=="show"),"options").Single(o=>S(o,"id")==shows[answer-1]);
                Note("【作答】"+texts[answer-1]+"；出示："+S(evidence,"label"));
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
            return Find("detective")??Find("close")??opts.First(o=>!S(o,"label").Contains("歇息"));
        }
        if(opts.FirstOrDefault(o=>S(o,"id").Contains("precise")) is {} precise)return precise;
        if(opts.FirstOrDefault(o=>S(o,"label").StartsWith("出面调停")) is {} mediate)return mediate;
        if(asking && Find("ask") is {} ask) {asking=false;return ask;}
        if(Find("pause") is {} pause){Note("【回应依据】卡片尚不能确定这个话题的完整因果，选择暂且不答。");return pause;}
        if(Find("yes") is {} yes)return yes;
        var proposal=opts.FirstOrDefault(o=>S(o,"id").StartsWith("proposal:") && S(o,"label").Contains("登记"));
        if(proposal is not null)return proposal;
        var archive=opts.FirstOrDefault(o=>S(o,"id").StartsWith("archive:") && S(o,"label").Contains("封存"));
        if(archive is not null)return archive;
        var support=opts.FirstOrDefault(o=>S(o,"id").StartsWith("support:") && S(o,"label").Contains("封存"));
        if(support is not null)return support;
        return Find("close")??Find("confirm")??Find("cancel")??opts[0];
    }
    void Select(Ev ev,Ev option)
    {
        Note("【选择】"+S(option,"label"));
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
            Note("【"+(kind switch {"card"=>"卡片","form"=>"作答","ending"=>"终局",_=>"可选陈述"})+"】"+S(ev,"title"));
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
            string? name = id=="isolation_door" ? "隔离门" : layout.RootElement.GetProperty("sites").EnumerateArray().FirstOrDefault(s=>s.GetProperty("id").GetString()==id) is var site && site.ValueKind!=JsonValueKind.Undefined ? site.GetProperty("title").GetString() : null;
            if(name is not null)Note("【物品】"+name+"："+(status switch {"open"=>"已开启","taken"=>"已被取用","returned"=>"已归还",_=>status}));
        }
        if(kind=="state") VisibleState(M(ev,"snapshot"));
    }
    void Choices(Ev ev,string key){foreach(var o in Maps(ev,key))Note("可选："+S(o,"label")+(Available(o)?"":"（"+S(o,"lock_hint")+"）"));}
    void Changed(string key,string text){if(text!=""&&stateText.GetValueOrDefault(key)!=text){stateText[key]=text;Note(text);}}
    void VisibleState(Ev snap)
    {
        var hud=M(snap,"hud");Changed("time","【当前文字】"+S(hud,"title")+" "+S(hud,"time"));
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
            var status=!chapter2||S(site,"kind")=="" ? "（"+(site.GetValueOrDefault("new") is true?"尚未检视":"已检视")+"）" : "";
            names.Add(S(site,"title")+status);
        }
        if(names.Count>0)Changed("items","【场上物品】"+string.Join("；",names.Distinct()));
        var replay=M(snap,"replay");
        if(replay.GetValueOrDefault("open") is true)
        {
            var sb=new StringBuilder("【重演手稿文字】\n"+S(replay,"title")+"\n"+S(replay,"hint"));
            foreach(var q in Maps(replay,"questions")){sb.AppendLine().Append(S(q,"question")).Append(S(q,"mark"));foreach(var o in A(q,"options"))sb.AppendLine().Append("可选：").Append(o);}
            Changed("replayquestions",sb.ToString());
            var result=M(replay,"result");
            if(result.Count>0)
            {
                var rs=new StringBuilder("【重演反馈】");
                foreach(var f in Maps(result,"findings"))rs.AppendLine().Append(S(f,"title")).Append("：").Append(S(f,"text")).Append("（").Append(S(f,"status") switch {"match"=>"吻合","conflict"=>"冲突",_=>"未检视"}).Append('）');
                Changed("replayresult",rs.ToString());
            }
        }
    }
    void C1Site(string id,string option="")
    {
        var site=layout.RootElement.GetProperty("sites").EnumerateArray().First(s=>s.GetProperty("id").GetString()==id);
        actors["detective"]=(site.GetProperty("x").GetDouble()+15,site.GetProperty("y").GetDouble());
        Input("前往并检视 "+site.GetProperty("title").GetString(),E(("kind","tick"),("delta",.1)));
        desiredCard=option;Input("检视 "+site.GetProperty("title").GetString(),E(("kind","interact"),("site",id)));
        // 请来的人手上有事时要等一会儿才过来：像玩家一样在原地等，不立刻去点下一件东西。
        if(option=="help") for(var i=0;i<200;i++){ Send(E(("kind","tick"),("delta",.1))); Drain(); }
    }
    void Move(string map)
    {
        this.map=map;
        var site=campusSites.First(s=>s.Map==map);
        Input("前往 "+Names.Of(site.Room),E(("kind","move"),("map",map),("place",site.Room.ToString())));
    }
    void InspectMap(string map)
    {
        Move(map);
        foreach(var site in campusSites.Where(s=>s.Map==map&&s.Kind is not ("bed" or "watch" or "meal" or "tube")))
            Input("检视 "+site.Title,E(("kind","interact"),("map",map),("site",site.LocalId)));
    }
    void AskLocal(int limit=2)
    {
        var room=campusSites.First(s=>s.Map==map).Room.ToString();
        var current = (Ev)game.Snapshot();
        var people=Maps(current,"people").Where(p=>S(p,"place")==room&&p.GetValueOrDefault("asleep") is not true).Take(limit).ToArray();
        foreach(var p in people)
        {
            if (!askedThisPeriod.Add((routeDay, S(current,"daypart"), S(p,"key")))) continue;
            asking=true;Input("询问 "+S(p,"name"),E(("kind","interact"),("map",map),("target",S(p,"key"))));
        }
    }
    void Act(string cmd,string label)=>Input(label,E(("kind","action"),("act",E(("cmd",cmd)))));
    void Header()
    {
        Note("02 · 一局完整正常路线的实际引擎玩家可见文字与场上物品");
        Note("记录说明：正式连续会话的语义输入记录。"+(exportOnly?"由现有完整原始记录导出，未重新运行游戏。":"由正式连续会话的实际输出记录。"));
        Note("逻辑宿主代报公开空间到场与呈现完成，未运行 Godot 几何、碰撞、寻路与画面。\n输入路线：正常醒来、相遇、报名及报偿核验；检视外衣和生活翼物证、比对鞋底、声路试验、昨夜重演与始业四问；继续同一局五日学园生活。\n首课选择精确解释、首餐调停；日常领粮、读板、询问、夜里睡觉；案发次晨查看实际痕迹，未充分确认的对质议题暂不答；末日按实际菜单提案与支持。\n正文只保留玩家文字、选项文本、物品名称及公开状态，按实际顺序排列；这是语义记录，不能用于评价现实游玩时长。");
    }
    static object? Decode(JsonElement element)=>element.ValueKind switch
    {
        JsonValueKind.Object=>element.EnumerateObject().ToDictionary(p=>p.Name,p=>Decode(p.Value)),
        JsonValueKind.Array=>element.EnumerateArray().Select(Decode).ToArray(),
        JsonValueKind.String=>element.GetString(), JsonValueKind.True=>true,JsonValueKind.False=>false,
        JsonValueKind.Number=>element.TryGetInt32(out var number)?(object)number:element.GetDouble(), _=>null
    };
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
                if(kind=="start"){waitingView=true;title="开始";}
                if(kind=="view_ready"){waitingView=false;title="场景就绪";}
                if(kind=="navigate"){chapter2=true;waitingView=true;title="继续同一局学园生活";}
                if(kind=="move")
                {
                    map=S(input,"map");var site=campusSites.FirstOrDefault(s=>s.Map==map);
                    title="前往 "+(site is null?"公开空间":Names.Of(site.Room));
                }
                if(kind=="interact")
                {
                    var target=S(input,"target");var site=S(input,"site");
                    if(target!="")title="询问 "+(Enum.TryParse<P>(target,out var person)?Names.Of(person):content.RootElement.GetProperty("PERSON_NAMES").GetProperty(target).GetString());
                    else if(chapter2)title="检视 "+campusSites.First(s=>s.Map==S(input,"map")&&s.LocalId==site).Title;
                    else
                    {
                        var definition=layout.RootElement.GetProperty("sites").EnumerateArray().First(s=>s.GetProperty("id").GetString()==site);
                        title="检视 "+definition.GetProperty("title").GetString();
                    }
                }
                if(kind=="action")title=S(M(input,"act"),"cmd") switch{"stay"=>"度过当前时段","class"=>"参加课程","sleep"=>"睡到天亮",_=>"提交当前行动"};
                if(kind=="replay")title="按已设处境进行昨夜重演";
                if(title!="")Note($"\n【操作 {++request:D4}】{title}");
                if(kind=="select"&&labels.TryGetValue(S(input,"choice_id"),out var chosen))Note("【选择】"+chosen);
                if(kind=="form")
                {
                    var values=M(input,"values");var selectedValues=new List<string>();
                    foreach(var (id,value) in values)
                        selectedValues.Add(id=="name"?"姓名："+value:fields.TryGetValue(id,out var choices)&&choices.TryGetValue(value?.ToString()??"",out var label)?label:"");
                    Note("【作答】"+string.Join("；",selectedValues.Where(s=>s!="")));
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
            Note("\n【记录完成】本局已到第五日正式终局，完整终局文字见上。");
            Console.WriteLine("Exported existing complete record; no game dispatch performed.");
        }
        finally {log.Dispose();layout.Dispose();content.Dispose();}
    }
    // STORY-HARDCODE[CSH-013][logic][tool]: 固定执行第一章检视顺序与连续五日路线以生成语义记录。
    public void Run()
    {
        try
        {
            Header();
            Input("开始",E(("kind","start")));
            Input("场景就绪；正常醒来",E(("kind","view_ready")));
            actors["detective"]=(480,300);
            Input("进入活动厅相遇",E(("kind","tick"),("delta",.1)));
            C1Site("E12");
            foreach(var id in new[]{"E1","E2","E3","E4","E5","E6","E7","E8j","E9"})
                C1Site(id,id is "E1" or "E2" or "E3" or "E8j"?"experiment":id is "E4" or "E5" or "E6" or "E7" or "E9"?"help":"");
            Input("按已设处境进行昨夜重演",E(("kind","replay"),("vars",new object?[]{0,0,0,0,0,1}),("skip",false)));
            actors["detective"]=(500,130);desiredChoice="answer";
            Input("向铜口提交四问",E(("kind","interact"),("site","E8")));
            if(game.Snapshot().GetValueOrDefault("ended") is not true)throw new InvalidOperationException("C1 did not end.");
            chapter2=true;
            Input("继续学园生活：继承当前运行世界",E(("kind","navigate"),("to","continue")));
            Input("学园场景就绪",E(("kind","view_ready")));
            // The two introductory production actions occupy Morning and Forenoon.
            for(int day=2;day<=5;day++)
            {
                routeDay = day;
                Note($"\n========== 玩家第{day-1}日 ==========");
                if(day>2 && day!=5)
                {
                    InspectMap("central_hall");AskLocal();Act("stay","读板问话后度过清晨");
                }
                if(day>2){Move("lecture");AskLocal(1);Act("class","参加上午课程");}
                InspectMap("logistics");AskLocal();Act("stay",day==2?"首餐领粮并调停":"午间领粮");
                if(day==5)
                {
                    foreach(var m in new[]{"granary","clinic","logistics","central_hall","archive"}){InspectMap(m);AskLocal(3);}
                }
                else
                {
                    foreach(var m in day==2?new[]{"central_hall","clinic"}:day==3?new[]{"greenhouse","archive"}:new[]{"logistics","central_hall"}){InspectMap(m);AskLocal();}
                }
                Act("stay","度过午后");Move("logistics");Act("stay",day==5?"晚间正常对质":"晚间领粮");
                Move("zone_yi");
                var bed=campusSites.First(s=>s.Map=="zone_yi"&&s.Kind=="bed");
                Input("夜里睡到天亮",E(("kind","interact"),("map","zone_yi"),("site",bed.LocalId)));
            }
            if(game.Snapshot().GetValueOrDefault("ended") is not true)throw new InvalidOperationException("Continuous session did not reach ended.");
            Note("\n【记录完成】本局正式会话已到末日终局（ended）；终局文字见上。未另建第二章世界。");
            Console.WriteLine("Complete continuous transcript: "+request+" semantic operations; ended.");
        }
        finally { log.Dispose();raw.Dispose();game.Dispose();layout.Dispose();content.Dispose(); }
    }
}
