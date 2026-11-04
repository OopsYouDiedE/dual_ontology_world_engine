using System.Globalization;
using System.Text;
using System.Text.Json;
using DuelView.Runtime;

internal static class ReadableEventLog
{
    static string Clock(double minutes)
    {
        var wholeMinutes=(long)Math.Floor(minutes);
        return $"第{wholeMinutes/1440+1}日 {wholeMinutes/60%24:00}:{wholeMinutes%60:00}";
    }
    static string? StartRoom(JsonElement condition,string actor)
    {
        if(condition.ValueKind!=JsonValueKind.Object)return null;
        if(condition.TryGetProperty("op",out var op)&&op.GetString()=="equals"&&
           condition.TryGetProperty("ref",out var reference)&&reference.GetProperty("object").GetString()==actor&&
           reference.GetProperty("field").GetString()=="room"&&condition.GetProperty("value").ValueKind==JsonValueKind.String)
            return condition.GetProperty("value").GetString();
        if(condition.TryGetProperty("items",out var items))
            foreach(var item in items.EnumerateArray())if(StartRoom(item,actor) is { } room)return room;
        return null;
    }
    public static void Write(string source,JsonElement content,string destination)
    {
        var names=content.GetProperty("actors").EnumerateArray().ToDictionary(a=>a.GetProperty("id").GetString()!,a=>a.GetProperty("name").GetString()!);
        var actions=content.GetProperty("actors").EnumerateArray().SelectMany(a=>a.GetProperty("actions").EnumerateArray()).ToDictionary(a=>a.GetProperty("id").GetString()!);
        var rooms=new Dictionary<string,string>();
        if(content.TryGetProperty("room_labels",out var labels))foreach(var p in labels.EnumerateObject())rooms[p.Name]=p.Value.GetString()!;
        foreach(var a in content.GetProperty("actors").EnumerateArray())
            if(a.TryGetProperty("home_room",out var home))rooms[home.GetString()!]=names[a.GetProperty("id").GetString()!]+"的卧室";
        var rows=new List<Dictionary<string,JsonElement>>();var row=new Dictionary<string,JsonElement>();
        var stateSource=Path.ChangeExtension(source,"states.yaml");
        foreach(var line in File.ReadLines(source).Concat(File.Exists(stateSource)?File.ReadLines(stateSource):[]))
        {
            if(string.IsNullOrWhiteSpace(line)){if(row.Count>0){rows.Add(row);row=new();}continue;}
            var text=line.StartsWith("- ")?line[2..]:line.TrimStart();var split=text.IndexOf(": ",StringComparison.Ordinal);
            if(split<0)continue;
            row[text[..split]]=JsonDocument.Parse(text[(split+2)..]).RootElement.Clone();
        }
        // Only complete, blank-line-terminated receipts are presented as completed events.
        rows=rows.OrderBy(r=>r["start_time"].GetDouble()).ThenBy(r=>r["receipt_index"].GetInt64()).ToList();
        // Raw receipts retain every recipient and evidence ID. This is only a human summary.
        string Kind(Dictionary<string,JsonElement> r)=>r.TryGetValue("kind",out var k)?k.GetString()??"":"";
        string FactId(Dictionary<string,JsonElement> r)=>r.TryGetValue("fact_ref",out var f)?f.GetProperty("Object").GetString()+"."+f.GetProperty("Facet").GetString()+"."+f.GetProperty("Field").GetString():"";
        rows=rows.Where(r=>!r.TryGetValue("fact_ref",out var f)||f.GetProperty("Field").GetString() is not ("room" or "spot")).Where(r=>!r["label"].GetString()!.EndsWith("状态更新",StringComparison.Ordinal)).ToList();
        var hidden=content.TryGetProperty("display",out var settings)&&settings.TryGetProperty("hidden_facts",out var hiddenFacts)?hiddenFacts.EnumerateArray().Select(x=>x.GetString()!).ToHashSet():new HashSet<string>();
        rows=rows.Where(r=>!hidden.Contains(FactId(r))).ToList();
        var priorMessages=new HashSet<string>();
        rows=rows.Where(r=>Kind(r)!="communication_received"||r["actor"].GetString()!=r["source"].GetProperty("actor").GetString()&&priorMessages.Add(r["actor"].GetString()+"/"+FactId(r)+"/"+r["fact_value"].GetRawText())).ToList();
        var grouped=new HashSet<Dictionary<string,JsonElement>>();var summaries=new List<Dictionary<string,JsonElement>>();
        foreach(var group in rows.Where(r=>Kind(r)=="communication_received").GroupBy(r=>r["source"].GetProperty("actor").GetString()+"/"+r["source"].GetProperty("execution").GetInt64()+"/"+r["start_time"].GetDouble().ToString("R",CultureInfo.InvariantCulture)))
        {
            var recipients=group.Select(r=>r["actor"].GetString()!).Distinct().ToArray();
            if(recipients.Length<2)continue;
            var summary=new Dictionary<string,JsonElement>(group.Last());
            summary["actor"]=group.First()["source"].GetProperty("actor").Clone();
            var firstReceived=group.Min(r=>r["start_time"].GetDouble());
            summary["start_time"]=JsonSerializer.SerializeToElement(firstReceived);
            summary["duration"]=JsonSerializer.SerializeToElement(0);
            summary["kind"]=JsonSerializer.SerializeToElement("group_knowledge");summary["audience"]=JsonSerializer.SerializeToElement(recipients);
            summary["label"]=JsonSerializer.SerializeToElement("[告知"+string.Join("、",recipients.Select(id=>names.GetValueOrDefault(id,id)))+"] "+string.Join("；",group.Select(r=>FactId(r)+" = "+r["fact_value"].GetRawText()).Distinct()));
            summary.Remove("fact_ref");summary.Remove("fact_value");
            summaries.Add(summary);foreach(var r in group)grouped.Add(r);
        }
        var seenFacts=new HashSet<string>();
        rows=rows.Where(r=>!grouped.Contains(r)).Where(r=>Kind(r)!="communication_received"||seenFacts.Add(r["actor"].GetString()+"/"+FactId(r)+"/"+r["fact_value"].GetRawText())).Concat(summaries)
            .OrderBy(r=>r["start_time"].GetDouble()).ThenBy(r=>r["receipt_index"].GetInt64()).ToList();
        var inferenceGroups=rows.Where(r=>Kind(r)=="inference").GroupBy(r=>r["actor"].GetString()+"/"+r["start_time"].GetRawText()+"/"+r["source"].GetRawText()).Where(g=>g.Count()>1).ToArray();
        foreach(var group in inferenceGroups)
        {
            var summary=new Dictionary<string,JsonElement>(group.First());
            summary["label"]=JsonSerializer.SerializeToElement(summary["label"].GetString()!.Replace("[推理更新]","[推理]")+"〔"+string.Join("；",group.Select(r=>FactId(r)+" = "+r["fact_value"].GetRawText()))+"〕");
            summary.Remove("fact_ref");summary.Remove("fact_value");
            foreach(var item in group)rows.Remove(item);rows.Add(summary);
        }
        rows=rows.OrderBy(r=>r["start_time"].GetDouble()).ThenBy(r=>r["receipt_index"].GetInt64()).ToList();
        var output=new StringBuilder("第一天 · 事件记录（知识以 对象.层面.字段 = 值 表示；倾向数值为行动评分加成）\n\n");
        string Describe(Dictionary<string,JsonElement> r)
        {
            var actor=r["actor"].GetString()!;var room=r["room"].GetString()!;var label=r.GetValueOrDefault("label").GetString()??r["action"].GetString();
            var duration=r["duration"].GetDouble().ToString("0.##",CultureInfo.InvariantCulture);
            var success=r["outcome"].GetString()=="completed";
            if(success&&!r.ContainsKey("kind")&&actions.TryGetValue(r["action"].GetString()!,out var action)&&
               StartRoom(action.GetProperty("precondition"),actor) is { } from&&from!=room)
                label+=$"（从{rooms.GetValueOrDefault(from,from)}到{rooms.GetValueOrDefault(room,room)}）";
            if(r.TryGetValue("fact_ref",out _)&&r.TryGetValue("fact_value",out var factValue))
            {
                var expression=FactId(r)+" = "+factValue.GetRawText();
                if(Kind(r)=="inference")label="[推理] "+label!.Replace("[推理更新] ","")+"〔"+expression+"〕";
                else
                {
                    var prefix=Kind(r)=="communication_received"?"[获知·"+names.GetValueOrDefault(r["source"].GetProperty("actor").GetString()!,"未知")+"] ":Kind(r)=="perception"?"[感知] ":"[观察] ";
                    var explanation=label!;
                    var bracket=explanation.IndexOf(']');
                    if(bracket>=0)explanation=explanation[(bracket+1)..].Trim();
                    if(Kind(r)=="perception"&&explanation.Contains('；'))explanation=explanation[(explanation.LastIndexOf('；')+1)..];
                    label=prefix+(factValue.ValueKind!=JsonValueKind.String||!FactId(r).EndsWith(".exit",StringComparison.Ordinal)?expression:explanation+"〔"+expression+"〕");
                }
            }
            if(!success)label+="（未完成）";
            if((!content.TryGetProperty("display",out var display)||!display.TryGetProperty("action_reasons",out var showReasons)||showReasons.GetBoolean())&&r.TryGetValue("reason",out var why)&&why.ValueKind==JsonValueKind.String&&why.GetString() is {Length:>0} reason)label+="〔缘由："+reason+"〕";
            return $"{Clock(r["start_time"].GetDouble())}，{names.GetValueOrDefault(actor,actor)}，{label}，持续{duration}分钟";
        }
        foreach(var r in rows)output.AppendLine(Describe(r));
        File.WriteAllText(destination,output.ToString(),new UTF8Encoding(true));
    }
    public static void Render(string[] args)
    {
        if(args.Length!=4)throw new ArgumentException("--render-cli-log <actions.yaml> <content.dv> <output.txt>");
        var content=new CharacterSessionDefinition(File.ReadAllText(args[2]),File.ReadAllText("assets/data/98-runtime-session.data.dv"));
        Write(args[1],content.Declaration,args[3]);Console.WriteLine(Path.GetFullPath(args[3]));
    }
}
