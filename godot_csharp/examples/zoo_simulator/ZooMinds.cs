using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using static DualOntologyWorldEngine.Core.Rules.AST;

namespace DualOntologyWorldEngine.Examples.ZooSimulator;

/// <summary>共用行动词汇。可行性取决于位置、物资和身体；文书只参与效用评分。</summary>
public static class ZooMinds
{
    public static void ActivateAll(ZooWorld w, ZooRules rules, double lionDelay = 0)
    {
        foreach (var c in w.People)
        {
            ActivatePerson(w, c, rules);
        }
    }

    public static void ActivatePerson(ZooWorld w, Character c, ZooRules rules)
    {
        c.Mind!.InstallStep((_,_,_,_) => Offers(w,c,rules));
        w.Session.Activate(c,c.Mind, idleCost: 5,
            utility: i => Utility(w,c,i)+rules.Score(c,i)+w.CognitiveUtility(i),
            chooser: ds =>
            {
                var best = ds.Max(d=>d.Score);
                var choices = ds.Where(d=>Math.Abs(d.Score-best)<0.001).ToArray();
                return choices[w.Random.Next(choices.Length)].Action!;
            },
            completed: (i,r) =>
            {
                if (i==null || r?.Result?.Succeeded!=true) return;
                rules.Reflect(c,i);
                c.Set("repeats", c.Get<string>("last_op")==i.Effect.Op && c.Get<Entity>("last_target")==i.Target ? c.Get<double>("repeats")+1 : 0d);
                c.Set("last_op",i.Effect.Op);
                c.Set("last_target",i.Target!);
                if (i.Effect.Op=="read" && i.Target is { } pub) rules.Read(c,pub);
                
                // 实体状态结算与替补机制
                if (w.Finished(c))
                {
                    w.Session.Deactivate(c);
                    // 若重度侵蚀或身份异化为兔子，在所在场景实体化兔子
                    if (c.Get<double>("erosion") >= 10 || c.Get<string>("identity") == "rabbit")
                    {
                        if (c.CurrentScene is { } scene)
                        {
                            ZooWorld.Item($"异化兔子({c.Name})", scene, new() { ["is_animal"] = true, ["is_rabbit"] = true, ["anomaly_resonance"] = 1.5 });
                        }
                    }
                    // 若游客离园或死亡，从大门生成新游客入园补位
                    if (c.Get<string>("role") == "visitor")
                    {
                        var replacement = w.SpawnVisitor($"补位游客{w.NextVisitorIndex}", w.Entrance, "普通便服", hasMap: true);
                        rules.LearnNewVisitor(replacement);
                        ActivatePerson(w, replacement, rules);
                    }
                }
            });
    }

    public static IEnumerable<Intent> Offers(ZooWorld w, Character c, ZooRules rules)
    {
        if (w.Finished(c) || c.CurrentScene is not { } scene) yield break;
        Observe(w,c);
        w.Perceive(c);
        foreach (var action in LiteraryDialogue.Offers(c.Mind!, c, w.Session.Runtime.Env))
            if (action.Target is not Character peer || !w.Finished(peer)) yield return action;
        foreach (var action in w.Narrative.Offers(c)) yield return action;
        var local = w.Local(c).ToArray();
        var now = w.Session.Timeline.Now;
        Intent Act(string op, Entity target, IEnumerable<Effect> changes, double cost=1, params object[] extra)
        {
            var parent=target.Parent;
            var effects=changes.ToArray();
            var origins=effects.Where(e=>e.Op=="move" && e.Args[0] is Entity)
                .Select(e=>(Entity:(Entity)e.Args[0],Parent:((Entity)e.Args[0]).Parent)).ToArray();
            return new(c,c.Mind!,new Effect(op,new object[] { c,target }.Concat(extra).ToArray()),effects,cost,$"{op} · {target.Name}",
                validate:()=>w.Alive(c) && c.CurrentScene==scene && target.Parent==parent && origins.All(o=>o.Entity.Parent==o.Parent));
        }
        yield return Act("wait",scene,[],10);
        yield return Act("rest",scene,[F("set",c,"rest_until",now+15)],15);
        yield return Act("sleep",scene,[F("set",c,"sleep_until",now+60)],60);
        yield return Act("silence",scene,[F("set",c,"topic",""),F("change",c,"uncertainty",-Math.Min(0.1,c.Get<double>("uncertainty")))],5);
        yield return Act("shout",scene,[F("change",c,"attention",1d)],2);
        foreach (var identity in new[] { "human","elephant","goat" })
            yield return Act("affirm",c,[F("set",c,"identity",identity),F("set",c,"identity_strength",1d),F("change",c,"uncertainty",-Math.Min(0.2,c.Get<double>("uncertainty")))],5,identity);
        foreach (var p in c.Mind!.KnownPassages)
        {
            if (p.Other(scene) is not { } next || !p.Allows(scene,c)) continue;
            // 侵蚀度较高时心智无法识别正规出口，屏蔽通道
            if (next == w.RegularExit && c.Get<double>("erosion") >= 3.0) continue;
            yield return Act("move",next,[F("move",c,next),F("relate",c,"visited",scene)],p.CostFrom(scene));
        }

        foreach (var item in local)
        {
            if (item.Get<bool>("detached") && item.Get<Entity>("owner") is Character sender && sender!=c)
                yield return Act("read_signal",item,[F("set",c,"requester",sender),F("set",c,"received_request",true)],1);
            if (rules.Publications.Contains(item))
            {
                if (!c.Mind.Beliefs.Any(b=>ReferenceEquals(b.Get<Entity>("publication"),item)))
                    yield return Act("read",item,[],1);
                continue;
            }
            if (item.Get<bool>("is_animal") || item.Get<bool>("is_projection"))
            {
                yield return Act("watch",item,[F("relate",c,"visited",scene)],2);
                yield return Act("approach",item,[F("relate",c,"visited",scene)],1);
                yield return Act("touch",item,[F("relate",c,"visited",scene)],1);
                if (c.Get<double>("carried_food")>=1)
                    yield return Act("feed",item,[F("spend",c,"carried_food",1d),F("change",item,"fed_count",1d),F("set",item,"last_feed",now),F("set",c,"rabbit_fed_at",now)],2);
                if (item.Get<bool>("is_escaped_rabbit"))
                    yield return Act("lure",item,[F("set",item,"following",c),F("set",c,"destination",w.LionRockery)],1);
                yield return Act("reinterpret",item,[F("relate",c,"calibrated",item),F("change",c,"uncertainty",-Math.Min(0.2,c.Get<double>("uncertainty")))],2);
            }
            if (item.Get<double>("count")>0 || item.Get<bool>("is_toy") || item.Get<bool>("is_tool") || item.Get<bool>("is_uniform"))
            {
                if (!c.Inventory.Contains(item))
                    yield return Act("take",item,[F("move",item,c),F("set",c,"toy_since",now)],1);
                else
                {
                    yield return Act("drop",item,[F("move",item,scene)],1);
                    var box = local.FirstOrDefault(e=>e.Get<bool>("containment"));
                    if (box!=null) yield return Act("store",item,[F("move",item,box)],1);
                }
            }
            if (item.Get<bool>("is_consumable") && item.Get<double>("count")>=1)
                yield return Act("consume",item,[F("spend",item,"count",1d),F("change",c,"thirst",-Math.Min(2,c.Get<double>("thirst"))),F("change",c,"hunger",-Math.Min(2,c.Get<double>("hunger")))],2);
            if (item.Get<bool>("is_uniform") && c.Get<string>("uniform")!=item.Get<string>("color"))
                yield return Act("wear",item,[F("set",c,"uniform",item.Get<string>("color")!),F("set",c,"changed_uniform",true),F("change",c,"uncertainty",0.4)],2);
            if (item.Get<bool>("is_light"))
            {
                if (item.Get<bool>("lit")) yield return Act("switch_off",item,[F("set",item,"lit",false)],1);
                else if (item.Get<bool>("powered")) yield return Act("switch_on",item,[F("set",item,"lit",true)],1);
                if (item==w.NightLight && item.Get<double>("battery")<10)
                    yield return Act("charge",item,[F("set",item,"battery",10d),F("set",c,"charged_at",now)],15);
            }
                if (item==w.ElephantSign || item==w.DrinkShelf || item==w.Camera || item==w.Mirror || item==w.WhaleProjection)
                yield return Act("inspect",item,[F("observe",c.Mind!,item,"evidence"),F("observe",c.Mind!,item,"claim"),F("relate",c,"calibrated",item),F("set",c,"shelf_check",now)],2);
            if (item.Get<bool>("is_device") && !item.Get<bool>("powered"))
                yield return Act("repair",item,[F("set",item,"powered",true)],10);
            if (item.Get<bool>("is_rabbit_blood") && c.Inventory.Contains(item))
                yield return Act("display",item,[F("set",item,"display_until",now+5)],1);
            if (item.Get<bool>("is_pet"))
                yield return Act("evict_pet",item,[F("move",item,w.Outside)],2);
            if (item.Get<bool>("containment"))
                yield return Act("inspect_containment",item,[F("set",item,"inspected_at",now)],2);
            if (item.Get<double>("health")>0)
                yield return Act("attack",item,[F("spend",item,"health",Math.Min(1,item.Get<double>("health"))),F("change",c,"fatigue",0.2)],2);
            if (item.Get<bool>("is_device") || item.Get<bool>("is_projection") || item==w.Jellyfish)
                yield return Act("destroy",item,[F("set",item,"active",false),F("set",item,"powered",false)],3);
        }

        // 地图纸是实际物件，作者引用随物件流转，不共享一个草坪布尔开关。
        var paper = ZooWorld.HeldPaper(c);
        if (paper?.Parent==c && !paper.Get<bool>("detached"))
            yield return Act("tear",c,[F("set",paper,"detached",true),F("set",c,"has_dotted_paper",true)],1);
        if (paper?.Parent==c && paper.Get<bool>("detached"))
        {
            yield return Act("drop_paper",scene,[F("move",paper,scene),F("set",paper,"owner",c),F("set",c,"has_dotted_paper",false)],1);
            if (local.Contains(w.Rabbits))
                yield return Act("give_paper",w.Rabbits,[F("move",paper,w.Rabbits),F("set",c,"has_dotted_paper",false),F("set",c,"paper_fed",true)],1);
        }
        if ((scene==w.RabbitArea || scene==w.RabbitBushes) && local.Any(e=>e.Get<bool>("is_shears")))
            yield return Act("prune",w.RabbitBushes,[F("set",w.RabbitBushes,"growth",0d),F("set",w.RabbitBushes,"shaded",false),F("set",c,"pruned_at",now)],10);
        if (scene==w.StaffSupply && w.FoodSupply.Get<double>("count")>=2)
            yield return Act("supply",w.FoodSupply,[F("spend",w.FoodSupply,"count",2d),F("change",c,"carried_food",2d)],2);
        if (scene==w.DirectorOffice)
        {
            if (c.Get<double>("money")>=5)
                yield return Act("order_food",w.DirectorOffice,[F("spend",c,"money",5d),F("change",c,"hunger",-Math.Min(2,c.Get<double>("hunger"))),F("change",c,"thirst",-Math.Min(2,c.Get<double>("thirst")))],15);
            yield return Act("rename",w.Computer,[F("set",w.Computer,"label","工作记录")],1);
            if (c.Get<double>("observed_lions")>0)
                yield return Act("record",w.Computer,[F("set",w.Computer,"lion_count",c.Get<double>("observed_lions")),F("set",c,"recorded_at",now),F("change",w.Computer,"records",1d)],3);
            foreach (var supply in new[] { w.SpareMaps,w.AquariumFood })
                if (w.World.Get<double>("budget")>=10)
                    yield return Act("restock",supply,[F("spend",w.World,"budget",10d),F("change",supply,"count",5d),F("set",c,"restocked_at",now)],10);
            if (w.World.Get<double>("budget")>=10)
            {
                yield return Act("grant",w.SecurityGuard,[F("spend",w.World,"budget",10d),F("change",w.SecurityGuard,"stamina",5d),F("set",c,"received_request",false)],3);
                yield return Act("install_camera",w.Camera,[F("spend",w.World,"budget",10d),F("set",w.Camera,"backup",true)],10);
            }
            if (!w.OfficeLight.Get<bool>("powered"))
                yield return Act("repair_power",w.OfficeLight,[F("set",w.OfficeLight,"powered",true)],10);
        }
        if (scene==w.AquariumLobby && w.World.Get<double>("budget")>=10)
            yield return Act("restock",w.AquariumFood,[F("spend",w.World,"budget",10d),F("set",w.AquariumFood,"count",10d),F("set",c,"restocked_at",now)],5);
        if (scene.Get<bool>("domain_lion"))
            yield return Act("count",w.LionRockery,[F("set",c,"counted_at",now),F("set",c,"observed_lions",(double)w.VisibleLions(c))],2);
        foreach (var area in new[] { w.PrimateArea,w.ElephantArea })
            if (scene==area || w.Topology.Adjacency[scene].Any(e=>e.Next==area))
            {
                if (!area.Get<bool>("closed"))
                    yield return Act("close",area,[F("set",area,"closed",true),F("set",area,"closed_at",now)],1);
                else
                    yield return Act("open",area,[F("set",area,"closed",false),F("set",c,"reported_split",false)],1);
            }
        if (scene.Get<bool>("enclosed"))
            yield return Act("close_door",scene,[F("set",scene,"door_closed",true)],1);
        yield return Act("patrol",scene,[F("set",c,"patrolled_at",now),F("relate",c,"visited",scene)],2);
        if (c.Get<bool>("has_map") || local.OfType<Character>().Any())
            yield return Act("report",scene,[F("set",w.BlueStaff,"reported_split",c.Get<bool>("saw_double_street")),F("set",w.Director,"saw_outage",c.Get<bool>("saw_outage")),F("set",c,"saw_aquarium",false),F("set",c,"reported_at",now)],1);

        foreach (var peer in local.OfType<Character>().Where(w.Alive))
        {
            if (paper!=null)
                yield return Act("give_paper",peer,[F("move",paper,peer),F("set",c,"has_dotted_paper",false)],1);
            if (peer.Get<bool>("offers_tickets") && c.Get<double>("money")>=10)
                yield return Act("buy_ticket",peer,[F("spend",c,"money",10d),F("change",peer,"money",10d),F("set",c,"has_ticket",true)],1);
            if (c.Inventory.Contains(w.RabbitPlush) && peer.Get<double>("money")>=5)
                yield return Act("sell",peer,[F("spend",peer,"money",5d),F("change",c,"money",5d),F("move",w.RabbitPlush,peer),F("set",peer,"toy_since",now)],1);
            yield return Act("discuss_organization",peer,[F("set",peer,"topic","organization"),F("change",c,"uncertainty",0.1)],2);
            yield return Act("discuss_zoo",peer,[F("set",peer,"topic","zoo")],2);
            yield return Act("talk",peer,[F("set",peer,"topic",w.IsAquarium(scene) ? "aquarium" : "zoo"),F("change",c,"fear",-Math.Min(0.2,c.Get<double>("fear")))],2);
            yield return Act("ask",peer,[F("set",peer,"requester",c),F("set",peer,"received_request",true)],1);
            if (c.Get<Entity>("requester")==peer)
                yield return Act("accept_help",peer,[F("set",c,"helping",peer),F("set",peer,"following",c),F("set",c,"requester",null!)],1);
            yield return Act("follow",peer,[F("set",c,"following",peer)],1);
            if (c.Get<Entity>("helping")==peer && w.NextStep(c,w.LionRockery) is { } next)
                yield return Act("escort",peer,[F("move",c,next)],2);
            if (scene==w.PrimateArea)
                yield return Act("escort_left",peer,[F("set",peer,"following",c),F("move",c,w.LeftStreet)],2);
            if (scene==w.ElephantArea)
                yield return Act("evacuate",peer,[F("set",peer,"following",c),F("move",c,w.Walkway)],2);
            if (w.IsAquarium(scene))
            {
                yield return Act("dissuade",peer,[F("change",peer,"reluctance_to_leave",1d)],1);
                if (local.Contains(w.Taser))
                    yield return Act("evict",peer,[F("change",peer,"fatigue",1d),F("set",peer,"destination",w.AquariumGate)],2);
            }
            if (peer.Get<double>("sleep_until")>now)
                foreach (var toy in peer.Inventory.Where(e=>e.Get<bool>("is_rabbit_toy")))
                    yield return Act("confiscate_toy",toy,[F("move",toy,c)],1);
        }
        if (c.Get<Entity>("following") is { } leader && ZooWorld.Place(leader) is { } place && place!=scene && w.NextStep(c,place) is { } step)
            yield return Act("follow",leader,[F("move",c,step)],2);
        foreach (var enemy in local.Where(e=>e.Get<double>("power")>0 || e.Get<bool>("is_fake_lion")))
        {
            if (c.Get<double>("stamina")>=1 && enemy.Get<double>("health")>0)
                yield return Act("defend",enemy,[F("spend",c,"stamina",1d),F("spend",enemy,"health",Math.Min(1,enemy.Get<double>("health"))),F("set",scene,"guard_until",now+3)],2);
            if (c.Inventory.Contains(w.RabbitBlood) && w.RabbitBlood.Get<double>("count")>=1)
                yield return Act("splash",enemy,[F("spend",w.RabbitBlood,"count",1d),F("set",enemy,"scent",3d)],1);
        }
    }

    public static void Observe(ZooWorld w, Character c)
    {
        var local = w.Local(c).ToArray();
        var scene = c.CurrentScene!;
        c.Set("has_dotted_paper",ZooWorld.HeldPaper(c)?.Get<bool>("detached")==true);
        foreach (var p in w.Topology.Passages)
            if (p.Other(scene) is { } next && (!w.IsAquarium(next) || c.Get<double>("erosion")>1 || w.IsAquarium(scene)))
            {
                // 侵蚀度较高时心智无法识别正规出口
                if (next == w.RegularExit && c.Get<double>("erosion") >= 3.0) continue;
                c.Mind!.LearnPassage(p);
            }
        if (scene==w.PrimateArea) c.Set("saw_double_street",scene.Get<bool>("has_double_street"));
        if (scene==w.PrimateArea || !w.PrimateArea.Get<bool>("closed") && w.Topology.Adjacency[scene].Any(e=>e.Next==w.PrimateArea))
            c.Set("observed_primate_visitors",(double)w.Visitors.Count(v=>v.CurrentScene==w.PrimateArea));
        if (scene==w.RabbitArea || scene==w.RabbitBushes) c.Set("saw_shade",w.RabbitBushes.Get<bool>("shaded"));
        if (scene.Get<bool>("domain_lion"))
        {
            c.Set("observed_lions",(double)w.VisibleLions(c));
            c.Set("all_roared",w.Lions.All(e=>w.InEarshot(scene,ZooWorld.Place(e)) && e.Get<double>("roar_until")>w.Session.Timeline.Now && e.Get<double>("health")>0));
        }
        if (w.IsAquarium(scene))
        {
            c.Set("saw_aquarium",true);
            c.Set("met_staff",local.OfType<Character>().Any(e=>e.Get<string>("role") is "red" or "staff"));
        }
        if (scene.Get<bool>("domain_elephant")) c.Set("saw_ears",local.OfType<Character>().Any(e=>e.Inventory.Any(t=>t.Get<bool>("is_earring"))));
        if (scene==w.DirectorOffice)
        {
            c.Set("saw_outage",!w.OfficeLight.Get<bool>("powered"));
            c.Set("saw_wrong_toy",w.SofaToy.Get<bool>("is_rabbit_toy"));
            c.Set("saw_fur",w.Camera.Get<bool>("fur"));
            c.Set("fur_since",w.Camera.Get<double>("fur_since"));
            c.Set("observed_maps",w.SpareMaps.Get<double>("count"));
        }
        if (local.Contains(w.ElephantSign)) c.Set("calibrated",c.Related("calibrated").Any(f=>ReferenceEquals(f.Value,w.ElephantSign)));

        // 极度恐慌或侵蚀加深导致心智信念动摇或遗忘
        if (c.Get<double>("fear") > 3.0 && w.Random.Next(20) == 0)
        {
            var accepted = c.Mind!.Beliefs.Where(b => b.HasTag(CommonTag.Accepted)).ToList();
            if (accepted.Count > 0)
            {
                var target = accepted[w.Random.Next(accepted.Count)];
                Soul.Stance(target, CognitiveStage.Evaluated);
            }
        }
        if (c.Get<double>("erosion") > 2.0 && w.Random.Next(20) == 0)
        {
            var accepted = c.Mind!.Beliefs.Where(b => b.HasTag(CommonTag.Accepted)).ToList();
            if (accepted.Count > 0)
            {
                var target = accepted[w.Random.Next(accepted.Count)];
                Soul.Stance(target, CognitiveStage.Rejected);
            }
        }
    }

    private static double Utility(ZooWorld w, Character c, Intent i)
    {
        double score = i.Effect.Op switch
        {
            "consume" => 12*(c.Get<double>("thirst")+c.Get<double>("hunger")),
            "rest" => c.Get<double>("fatigue")*8,
            "sleep" => c.Get<double>("fatigue")*7-10,
            "watch" or "touch" or "approach" => 14,
            "take" => 3,
            "read" => 12,
            "read_signal" => 18,
            "move" => 8,
            "affirm" => c.Get<double>("uncertainty")*12,
            "follow" when c.Get<Entity>("following")==i.Target => 65,
            "defend" => c.Get<double>("fear")*8,
            _ => 0
        };
        if (i.Effect.Op=="move" && i.Target is { } dest)
        {
            var visits = c.Related("visited").Count(f=>ReferenceEquals(f.Value,dest));
            score += visits==0 ? 12 : -visits*2;
            if (dest==w.RegularExit)
            {
                if (c.Get<double>("erosion") >= 3.0) score -= 500;
                else score += c.Get<string>("role")=="visitor" && (c.Get<double>("fatigue")>4 || c.Related("visited").Count()>=5) ? 40 : -30;
            }
            if (dest==c.Get<Entity>("preferred_exhibit")) score+=8;
            if (c.Get<Entity>("destination") is { } goal && dest==w.NextStep(c,goal)) score+=90;
        }
        if (c.Get<string>("role") is "staff" or "red" or "director" or "guard")
        {
            if (i.Effect.Op=="move" && i.Target is { } patrolDestination && patrolDestination.Get<bool>("patrol_"+(c.Get<string>("role")=="staff" ? "blue" : c.Get<string>("role")))) score+=6;
            if (i.Effect.Op=="supply" && c.Get<double>("carried_food")<1) score+=30;
            if (i.Effect.Op=="accept_help") score+=25;
            if (i.Effect.Op=="prune" && w.RabbitBushes.Get<bool>("shaded")) score+=20;
        }
        if (i.Effect.Op==c.Get<string>("last_op") && i.Target==c.Get<Entity>("last_target")) score-=20*(c.Get<double>("repeats")+1);
        return score;
    }
}
