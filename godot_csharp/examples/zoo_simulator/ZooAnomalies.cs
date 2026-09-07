using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Timeline;
using static DualOntologyWorldEngine.Core.Rules.AST;

namespace DualOntologyWorldEngine.Examples.ZooSimulator;

/// <summary>共享的暴露、认知、追踪、捕食和生态过程。不接收守则、预设或条目编号。</summary>
public sealed class ZooAnomalies : IDisposable
{
    public sealed record AnomalyRecord(double Time, Entity Actor, Entity Scene, string Mechanism, double Attention, double Erosion, string Outcome);
    private readonly ZooWorld w;
    private readonly Soul clockMind = new("环境过程");
    private readonly Func<Intent, StateView, IReadOnlyList<Effect>, IReadOnlyList<Effect>> transform;
    private readonly Func<double?> tick;
    private readonly List<Entity> autonomous = [];
    private bool disposed;
    public List<AnomalyRecord> Records { get; } = [];
    public ZooAnomalies(ZooWorld world)
    {
        w = world;
        transform = Contact;
        w.Session.Runtime.EffectTransforms.Add(transform);
        w.Session.Runtime.Executed += Record;
        tick = () =>
        {
            var changes = Evolve().ToArray();
            w.Session.Runtime.ResolveDetailed(new Intent(w.World,clockMind,F("evolve",w.World),changes,1,"环境演化一分钟"));
            return 1;
        };
        w.Session.Timeline.Schedule(1,"生态、时钟与持续暴露",tick);
        foreach (var animal in w.Lions.Concat([w.TheShadow,w.EscapedRabbit,w.FifthLion]))
        {
            var mind = new Soul(animal.Name+"的行为");
            mind.InstallStep((_,_,_,_)=>AnimalOffers(animal,mind));
            w.Session.Activate(animal,mind,idleCost:2,utility:i=>i.Effect.Op switch { "attack"=>80,"roar"=>60,"move"=>20,_=>0 });
            autonomous.Add(animal);
        }
    }

    // 固定一分钟积分，不按游客行动次数收费；待机、睡眠和较慢动作同样承受时间流逝。
    private IEnumerable<Effect> Evolve()
    {
        double now=w.Session.Timeline.Now;
        foreach (var c in w.People.Where(c=>!w.Finished(c)))
        {
            var scene=c.CurrentScene!;
            var local=w.Local(c).ToArray();
            bool sleeping=c.Get<double>("sleep_until")>now;
            bool resting=sleeping || c.Get<double>("rest_until")>now;
            double light=scene.Children.Keys.OfType<Entity>().Where(e=>e.Get<bool>("lit") && e.Get<bool>("powered")).Sum(e=>e.Get<double>("beacon_strength")+0.2);
            bool shelter=scene.Get<bool>("enclosed") && scene.Get<bool>("door_closed") && light==0;
            double resonance=scene.Get<bool>("shaded") ? scene.Get<double>("anomaly_resonance") : 0;
            foreach (var e in local.Where(e=>e.Get<object>("active") is not false && e is not Character))
            {
                if (e.HasTag("cognitive_hazard") && ZooWorld.Anchored(c,e)) continue;
                resonance+=e.Get<double>("anomaly_resonance");
            }
            double roar=w.Lions.Where(l=>l.Get<object>("active") is not false && l.Get<double>("health")>0 && l.Get<double>("roar_until")>now && w.InEarshot(scene,ZooWorld.Place(l))).Sum(l=>l.Get<double>("power"));
            double guard=scene.Get<double>("guard_until")>now ? 2 : 0;
            bool amongPrimates=local.Contains(w.Primates);
            var paper=ZooWorld.HeldPaper(c);
            double shielding=paper?.Parent==c && paper.Get<bool>("detached") ? 0.5 : 1;
            double exposure=Math.Max(0,resonance-roar-guard)*shielding*(shelter ? 0.05 : 1);
            yield return F("set",c,"exposure",exposure);
            double fear=c.Get<double>("fear"), erosion=c.Get<double>("erosion"), attention=c.Get<double>("attention");
            double nextE=Math.Max(0,erosion+exposure*0.02-(resting && shelter ? 0.015 : 0)-roar*0.008);
            double uncertainty=Math.Max(0,c.Get<double>("uncertainty")+exposure*0.006-(resting ? 0.005 : 0));
            double nextA=Math.Max(0,attention+exposure*0.035+light*0.02+(c.Get<string>("identity")!="human" ? 0.01 : 0)-roar*0.08-(amongPrimates ? 0.1 : 0)-(shelter ? 0.08 : 0)-0.01);
            yield return F("set",c,"erosion",nextE);
            yield return F("set",c,"attention",nextA);
            yield return F("set",c,"uncertainty",uncertainty);
            yield return F("set",c,"fear",Math.Max(0,fear+exposure*0.012-(resting ? 0.02 : 0)-roar*0.01));
            yield return F("set",c,"stability",Math.Max(0,10-nextE-uncertainty));
            yield return F("change",c,"thirst",0.015);
            yield return F("change",c,"hunger",0.006);
            yield return F("set",c,"fatigue",Math.Max(0,c.Get<double>("fatigue")+(resting ? -0.08 : 0.015)));
            yield return F("set",c,"symptom_minutes",nextE>1 ? c.Get<double>("symptom_minutes")+1 : 0d);
            if (c.Get<double>("age") is >0 and <=12 && fear>2)
                yield return F("set",scene,"scream_until",now+5);
            if (c.Get<double>("thirst")>20) yield return F("spend",c,"health",Math.Min(0.02,c.Get<double>("health")));
            if (w.IsAquarium(scene))
            {
                if (!c.Get<bool>("in_aquarium")) yield return F("set",c,"aquarium_entry_clock",w.Minute);
                yield return F("set",c,"in_aquarium",true);
                yield return F("change",c,"aquarium_minutes",1d);
            }
            else
            {
                yield return F("set",c,"in_aquarium",false);
                yield return F("set",c,"aquarium_minutes",0d);
            }
            if (c.Get<Entity>("companion") is Character companion)
            {
                bool apart=companion.CurrentScene!=scene;
                if (apart && !c.Get<bool>("apart")) yield return F("change",c,"separation_count",1d);
                yield return F("set",c,"apart",apart);
                yield return F("set",c,"separated_minutes",apart ? c.Get<double>("separated_minutes")+1 : 0d);
                yield return F("set",c,"lost",apart && c.Get<double>("separated_minutes")>5);
            }
            // 光信标来自物件与年龄/持有时长，和是否记住玩偶条款无关。
            if (c.Inventory.Any(e=>e.Get<bool>("is_rabbit_toy")) && c.Get<double>("age")>12)
                yield return F("change",c,"attention",0.01+(now-c.Get<double>("toy_since")>43200 ? 0.04 : 0));
        }

        double growth=w.RabbitBushes.Get<double>("growth")+1d/43200;
        yield return F("set",w.RabbitBushes,"growth",growth);
        yield return F("set",w.RabbitBushes,"shaded",growth>=1);
        yield return F("set",w.RabbitBushes,"overgrown",growth>=1);
        if (w.NightLight.Get<bool>("lit"))
        {
            double battery=Math.Max(0,w.NightLight.Get<double>("battery")-0.002);
            yield return F("set",w.NightLight,"battery",battery);
            if (battery==0) yield return F("set",w.NightLight,"lit",false);
        }
        foreach (var scene in w.Scenes)
            yield return F("set",scene,"dark",!scene.Children.Keys.OfType<Entity>().Any(e=>e.Get<bool>("lit") && e.Get<bool>("powered")));

        // 外生扰动只有实体、库存及环境变化；独立随机流避免决策改变后续天气序列。
        if (w.EnvironmentRandom.NextDouble()<0.025)
        {
            var choices=new[] { w.EscapedRabbit,w.RabbitBlood,w.GoatMeat,w.RabbitEaredElephant,w.FifthLion,w.WhaleProjection };
            var item=choices[w.EnvironmentRandom.Next(choices.Length)];
            if (!item.Get<bool>("active") && item.Get<double>("health")>0)
            {
                Entity destination=item==w.EscapedRabbit ? w.RabbitArea : item==w.RabbitBlood ? w.DrinkShelf : item==w.GoatMeat ? w.AquariumFoodShelf : item==w.RabbitEaredElephant ? w.ElephantDisplay : item==w.FifthLion ? w.LionRockery : w.WhaleArea;
                yield return F("move",item,destination);
                yield return F("set",item,"active",true);
            }
        }
        if (w.EnvironmentRandom.NextDouble()<0.002)
        {
            yield return F("set",w.OfficeLight,"powered",false);
            yield return F("set",w.Camera,"powered",false);
            yield return F("set",w.Camera,"fur",true);
            yield return F("set",w.Camera,"fur_since",now);
        }
        if (w.EnvironmentRandom.NextDouble()<0.002)
            yield return F("set",w.SofaToy,"is_rabbit_toy",!w.SofaToy.Get<bool>("is_rabbit_toy"));
        yield return F("set",w.PrimateArea,"has_double_street",ZooWorld.Place(w.EscapedRabbit)==w.PrimateArea);
        foreach (var p in w.Papers.Where(p=>p.Parent==w.Rabbits))
        {
            yield return F("move",p,w.World);
        }
        foreach (var c in w.People.Where(c=>c.CurrentScene!=null))
        {
            if (w.Scenes.Any(s=>s.Get<double>("laugh_until")>now && w.InEarshot(c.CurrentScene!,s)))
                yield return F("set",c,"heard_laugh_until",now+10);
            if (w.Scenes.Any(s=>s.Get<double>("scream_until")>now && w.InEarshot(c.CurrentScene!,s)))
                yield return F("set",c,"heard_scream",true);
        }
    }

    private IReadOnlyList<Effect> Contact(Intent i, StateView view, IReadOnlyList<Effect> changes)
    {
        if (view.Observer!=null || i.Actor is not Character c || i.Target is not { } target) return changes;
        if (i.Effect.Op is not ("consume" or "touch" or "approach" or "watch")) return changes;
        if (target.Get<object>("active") is false) return changes;
        double dose=target.Get<double>("anomaly_resonance")*(i.Effect.Op switch { "consume"=>0.8,"touch"=>0.3,"approach"=>0.1,_=>0.06 });
        if (i.Effect.Op=="watch" && ZooWorld.Anchored(c,target)) dose*=0.1;
        if (dose<=0) return changes;
        return changes.Concat([F("change",c,"erosion",dose),F("change",c,"attention",dose*0.4),F("change",c,"fear",dose*0.2)]).ToArray();
    }

    private IEnumerable<Intent> AnimalOffers(Entity animal, Soul mind)
    {
        if (animal.Get<double>("health")<=0 || animal.Get<object>("active") is false || ZooWorld.Place(animal) is not { } scene) yield break;
        double now=w.Session.Timeline.Now;
        Intent Act(string op,Entity target,IEnumerable<Effect> effects,double cost=2)=>new(animal,mind,F(op,animal,target),effects,cost,$"{animal.Name} {op} {target.Name}");
        var humans=w.People.Where(c=>c.CurrentScene==scene && w.Alive(c)).ToArray();
        var visibleBlood=humans.SelectMany(c=>c.Inventory).FirstOrDefault(e=>e.Get<double>("display_until")>now);
        if (animal.Get<double>("blood_appetite")>0 && visibleBlood!=null && animal.Get<Entity>("approaching")!=visibleBlood.Parent)
            yield return Act("approach",visibleBlood,[F("set",animal,"approaching",visibleBlood.Parent!),F("set",animal,"approached_at",now)],1);
        bool lion=animal.Get<bool>("is_white_lion");
        if (lion)
        {
            bool guests=w.People.Any(c=>c.CurrentScene is { } location && w.InEarshot(location,scene) && w.Alive(c));
            if (guests && animal.Get<double>("roar_until")<=now)
                yield return Act("roar",scene,[F("set",animal,"roar_until",now+5)],3);
            var prey=scene.Children.Keys.OfType<Entity>().FirstOrDefault(e=>e!=animal && e.Get<double>("health")>0 && (e.Get<bool>("is_escaped_rabbit") || e.Get<double>("scent")>0 || e is Character c && c.Get<double>("erosion")>6));
            if (prey!=null)
            {
                var effects=new List<Effect> { F("spend",prey,"health",Math.Min(2,prey.Get<double>("health"))) };
                if (prey is Character) effects.Add(F("set",prey,"bitten",true));
                yield return Act("attack",prey,effects);
            }
        }
        else if (animal==w.TheShadow)
        {
            double deterrence=w.Lions.Where(l=>l.Get<double>("health")>0 && l.Get<object>("active") is not false && l.Get<double>("roar_until")>now && w.InEarshot(scene,ZooWorld.Place(l))).Sum(l=>l.Get<double>("power"));
            bool maskedByScene = scene.Children.Keys.OfType<Entity>().Any(e => e.Get<bool>("is_dotted_paper") && e.Get<bool>("detached"));
            bool amongPrimates = scene.Children.Keys.OfType<Entity>().Contains(w.Primates);
            var prey = humans.Where(c =>
            {
                if (maskedByScene) return false;
                if (ZooWorld.HeldPaper(c)?.Get<bool>("detached") == true) return false;
                double threshold = amongPrimates ? 3.0 : 1.0;
                return c.Get<double>("attention") > threshold;
            }).OrderByDescending(c => c.Get<double>("attention")).FirstOrDefault();
            if (prey!=null && deterrence==0 && scene.Get<double>("guard_until")<=now)
                yield return Act("attack",prey,[F("spend",prey,"health",Math.Min(0.5,prey.Get<double>("health"))),F("set",scene,"laugh_until",now+5)],3);
            var destinations=w.Topology.Adjacency[scene].Where(e=>e.Next!=null).Select(e=>e.Next!).ToArray();
            if (destinations.Length>0)
            {
                double Attraction(Entity s)
                {
                    double attr = w.People.Where(c=>c.CurrentScene==s && w.Alive(c)).Sum(c=>c.Get<double>("attention"))
                        + s.Children.Keys.OfType<Entity>().Sum(e=>e.Get<bool>("lit") ? e.Get<double>("beacon_strength") : 0)
                        - (s.Get<bool>("enclosed") && s.Get<bool>("door_closed") && s.Get<bool>("dark") ? 10 : 0);
                    if (s == w.DirectorOffice && w.Minute >= 75 && w.Minute < 360 && w.OfficeLight.Get<bool>("lit") && w.OfficeLight.Get<bool>("powered"))
                        attr += 3.5;
                    return attr;
                }
                var dest=destinations.OrderByDescending(Attraction).First();
                if (Attraction(dest)>Attraction(scene) || prey==null || deterrence>0)
                    yield return Act("move",dest,[F("move",animal,dest)],3);
            }
        }
        else
        {
            if (animal.Get<Entity>("following") is { } leader && ZooWorld.Place(leader) is { } dest && dest!=scene)
            {
                var next=w.Topology.Adjacency[scene].FirstOrDefault(e=>e.Next==dest).Next;
                if (next!=null) yield return Act("move",next,[F("move",animal,next)],2);
            }
            else if (animal==w.EscapedRabbit)
            {
                var edges=w.Topology.Adjacency[scene].Where(e=>e.Next!=null).ToArray();
                if (edges.Length>0)
                {
                    var next=edges[w.AnimalRandom.Next(edges.Length)].Next!;
                    yield return Act("move",next,[F("move",animal,next)],5);
                }
            }
        }
        yield return Act("wait",scene,[],2);
    }

    private void Record(Intent intent, ActionResolution resolution)
    {
        if (resolution.Result?.Succeeded!=true || resolution.Event==null) return;
        foreach (var change in resolution.Event.GetChanges())
        {
            if (change.Subject is not Entity e || change.Field is not ("health" or "erosion" or "exposure" or "$parent" or "lit" or "count")) continue;
            if (Equals(change.Before,change.After)) continue;
            static string Show(object? value)=>value is Entity entity ? entity.Name : value?.ToString()??"无";
            Records.Add(new(w.Session.Timeline.Now,e,ZooWorld.Place(e)??w.World,intent.Effect.Op,e.Get<double>("attention"),e.Get<double>("erosion"),$"{change.Field}: {Show(change.Before)} → {Show(change.After)}"));
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        w.Session.Timeline.Cancel(t=>t.Action==tick);
        foreach (var e in autonomous) w.Session.Deactivate(e);
        w.Session.Runtime.EffectTransforms.Remove(transform);
        w.Session.Runtime.Executed-=Record;
        disposed=true;
    }
}
