using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;
namespace DualOntologyWorldEngine.Examples.ZooSimulator;

/// <summary>实验操作与现场感知使用同一个 Core 认知入口，绝不改写本质或绑定。</summary>
public static class ZooExperiment
{
    public static AstDecoder Decoder(Person p)=>new(p.Soul.World,
        Z.All.Concat(p.Soul.World.Projections.SelectMany(v=>v.Tags.Keys)).Distinct(),
        Z.Relations.Concat(p.Soul.World.Projections.SelectMany(v=>v.Relations.Select(r=>r.Relation))).Distinct());
    public static Projection Edit(Person p,Projection? target,string name,ProjectionKind kind,Truth existence,Projection? parent,string tags,double time)
    {
        var values=ImmutableDictionary.CreateBuilder<Tag,int?>();
        foreach(var line in tags.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
        {
            var parts=line.Split('=',2,StringSplitOptions.TrimEntries);
            if(parts.Length!=2)throw new FormatException("标签格式：名称=整数；名称=? 表示未知。");
            values[new(parts[0])]=parts[1]=="?"?null:int.Parse(parts[1]);
        }
        if(target!=null)foreach(var tag in target.Tags.Keys.Where(t=>!values.ContainsKey(t)))values[tag]=null;
        return Observation.Observe(p.Soul,new(name,kind){Target=target,Source=ObservationSource.Corruption,Time=time,
            Existence=existence,Move=true,Parent=parent,Tags=values.ToImmutable(),Details="实验者修改脑内对象；现实保持原样"});
    }
    public static void Relate(Person p,Projection target,RelationTag relation,Projection other,Truth truth,double time)
        =>Observation.Observe(p.Soul,new(target.Name,target.Kind){Target=target,Source=ObservationSource.Inference,Time=time,
            AddRelations=truth==Truth.True?[(relation,other)]:[],DenyRelations=truth==Truth.False?[(relation,other)]:[],
            RemoveRelations=truth==Truth.Unknown?[(relation,other)]:[],Details="修改脑内关系"});
    public static void SetBelief(Person p,Projection target,string text,double time)
    {
        var rules=Decoder(p).DecodeRules(text).Select(r=>r with {Acceptance=Truth.True,Weight=500}).ToImmutableArray();
        // 推导工作区每次判断都会更新；自定信念持有独立的认知来源。
        if(target.Name is "当前判断" or "日常计划")
            target=Observation.Observe(p.Soul,new("自定信念",ProjectionKind.Concept){Existence=Truth.True,Source=ObservationSource.Inference,Time=time});
        foreach(var book in p.Books.Values.Where(b=>b.Projection==target).ToArray())p.Books.Remove(book.Text.Key);
        Observation.Observe(p.Soul,new(target.Name,target.Kind){Target=target,Source=ObservationSource.Inference,Time=time,Rules=rules,Details=text});
    }
    public static void Forget(Person p,Projection target,double time)
    {
        if(target==p.Self)throw new InvalidOperationException("可修改自身认知；控制身体所需的自身引用不能删除。");
        Observation.Observe(p.Soul,new(target.Name,target.Kind){Target=target,Forget=true,Source=ObservationSource.Corruption,Time=time});
        foreach(var book in p.Books.Values.Where(b=>b.Projection==target).ToArray())p.Books.Remove(book.Text.Key);
    }
    public static string Snapshot(Person p)
    {
        ZooMinds.Decide(p);
        return string.Join("\n",p.Assessments.Select(a=>$"{(a.Forbidden?"禁止":"可选")} {a.Decision.Score,4} · {ZooWorld.Describe(a.Decision.Effect)} · {string.Join("；",a.Reasons)}"));
    }
}
