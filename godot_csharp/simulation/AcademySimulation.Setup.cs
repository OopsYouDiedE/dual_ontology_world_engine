using System;
using System.Collections.Generic;
using System.Linq;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Kotodama;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Social;
using DualOntologyWorldEngine.Core.Timeline;

namespace DualOntologyWorldEngine.Simulation;

public partial class AcademySimulation
{
    private void ConfigureActionRuntimeHooks()
    {
        Runtime.WitnessesCollector = (ev, ctx) =>
        {
            var actor = ev.Get<Entity>("actor") as Character;
            if (actor?.CurrentScene == null) return Enumerable.Empty<Soul>();
            return actor.CurrentScene.Children.Keys.OfType<Character>()
                .Where(c => !ReferenceEquals(c, actor) && c.Mind != null)
                .Select(c => c.Mind!);
        };

        Runtime.ReactionHandler = (witnessSoul, ev, ctx) =>
        {
            var witness = witnessSoul.Body as Character;
            if (witness == null) return null;
            var effectObj = ev.Properties.TryGetValue("effect", out var eff) ? eff : null;
            string? op = effectObj switch
            {
                Effect effAst => effAst.Op,
                Expr exprAst => exprAst.Op,
                _ => null
            };
            if (op == null) return null;

            // 1. 见证烹饪出锅：饥饿见证者享用热食
            if (op == "cook_food" && Motives.TryGetValue(witness, out var profile) && profile.Hunger < 65f)
            {
                var scene = witness.CurrentScene;
                var meal = scene?.Children.Keys.OfType<Entity>().FirstOrDefault(e => e.Has("Meal") && e.Tag("Servings") > 0);
                if (meal != null)
                {
                    return new Intent(
                        witness,
                        witnessSoul,
                        AST.F("consume_nourishment", witness, meal),
                        motionCost: 10f,
                        validate: () => meal.Parent == witness.CurrentScene && meal.Tag("Servings") > 0,
                        resolve: () =>
                        {
                            meal.SetTag("Servings", meal.Tag("Servings") - 1);
                            profile.Hunger = Math.Min(100f, profile.Hunger + 45f);
                            profile.Comfort = Math.Min(100f, profile.Comfort + 15f);
                            witnessSoul.AdjustEmotion(Emotion.Joy, 2, "享用同伴新鲜烹饪的热食");
                            if (ev.Get<Entity>("actor") is Character cook && cook.Mind != null)
                            {
                                witnessSoul.IncreaseBond(cook.Mind, 1);
                            }
                            SimulationLogs.Add($"😋 🥣 【{witness.Name}】闻香上前，欣喜地分得一份《{meal.Name}》大口享用！（剩余 {meal.Tag("Servings")} 份）");
                            if (meal.Tag("Servings") <= 0 && meal.Has("Consumable"))
                            {
                                meal.Reparent(null);
                                SimulationLogs.Add($"✨ 🍽️ 《{meal.Name}》已被大家享用一空。");
                            }
                            return meal;
                        },
                        label: $"闻香享用《{meal.Name}》",
                        responseMode: ResponseMode.Cooperate);
                }
            }

            // 2. 见证开锁机构：警戒学员喝止质询
            if (op == "unlock_mechanism" && witnessSoul.Dispositions.Any(d => d.Has("Vigilance") || d.Has("Rationality")))
            {
                var args = effectObj switch
                {
                    Effect effAst => effAst.Args,
                    Expr exprAst => exprAst.Args,
                    _ => Array.Empty<object>()
                };
                var target = args.Length > 1 ? args[1] as Entity : null;
                var targetName = target?.Name ?? "锁闭容器";
                if (ev.Get<Entity>("actor") is Character burglar && burglar != witness)
                {
                    return new Intent(
                        witness,
                        witnessSoul,
                        AST.F("interrogate_motive", witness, burglar),
                        motionCost: 10f,
                        resolve: () =>
                        {
                            witnessSoul.AdjustEmotion(Emotion.Fear, 1, "戒备私自开锁");
                            SimulationLogs.Add($"👁️ 🚨 【{witness.Name}】警觉地注视着正在开启《{targetName}》的【{burglar.Name}】：“请说明开启这处机构的意图！”");
                            return burglar;
                        },
                        label: $"质询【{burglar.Name}】开启机构意图",
                        responseMode: ResponseMode.Restrain);
                }
            }

            return null;
        };
    }

    private void BuildCampusTopology()
    {
        var allSceneNames = AcademyData.CharacterSpecs.Select(c => c.InitialScene)
            .Concat(AcademyData.Connections.SelectMany(c => new[] { c.From, c.To }))
            .Distinct().ToList();

        foreach (var name in allSceneNames)
        {
            Scenes[name] = new Entity(name, World, new Dictionary<string, int> { ["Scene"] = 1 });
        }

        foreach (var (from, to) in AcademyData.Connections)
        {
            new Passage($"通道:{from}-{to}", Scenes[from], Scenes[to], cost: 1.0f, parent: World);
        }
    }

    private void BuildSceneFacilities()
    {
        foreach (var spec in AcademyData.Facilities)
        {
            var facility = new Entity(
                spec.Name,
                Scenes[spec.SceneName],
                new Dictionary<string, int>(spec.Tags),
                properties: new Dictionary<string, object> { ["description"] = spec.Description, ["affordance"] = spec.Affordance });
            Facilities.Add(facility);

            if (facility.Has("Danger"))
            {
                Env.AssertFact(AST.E("tag_at_least", facility, "Danger", facility.Tag("Danger")));
            }
        }
    }

    public Expr FactSchoolClosed { get; private set; } = null!;
    public Expr FactMaintainsSurvival { get; private set; } = null!;
    public Expr FactMustSpeakTruth { get; private set; } = null!;
    public Expr FactCanHelpEscape { get; private set; } = null!;

    private void InstallCampusCovenants()
    {
        FactSchoolClosed = AST.E("school_closed", World);
        FactMaintainsSurvival = AST.E("maintains_survival", Demon);
        FactMustSpeakTruth = AST.E("must_speak_truth", Demon);
        FactCanHelpEscape = AST.E("can_help_escape", Demon);

        Env.AssertFact(FactSchoolClosed);
        Env.AssertFact(FactMaintainsSurvival);
        Env.AssertFact(FactMustSpeakTruth);
        Env.AssertFact(FactCanHelpEscape);

        var truthRule = new Rule(AST.E("true"), [new Directive(Mode.Forbid, AST.F("speak_false", Demon, World))], "恶魔发言不得为假");
        Words.Invoke(KotodamaFactory.CreateRecipe("恶魔真言契约", constraints: [truthRule]), DemonSoul, new Dictionary<string, object>());

        var exitRule = new Rule(AST.E("true"), [new Directive(Mode.Forbid, AST.F("pass_boundary_alone", null!))], "严禁在未解除结界前单人强行穿越封锁界标");
        Words.Invoke(KotodamaFactory.CreateRecipe("外缘结界因果契约", constraints: [exitRule]), DemonSoul, new Dictionary<string, object>());

        var survivalTrigger = new Rule(AST.E("state", "student_acute_danger"), [new Directive(Mode.Require, AST.F("demon_intervene_rescue", Demon))], "当学员面临致命危局时，恶魔必须介入维持其生命体征");
        Words.Invoke(KotodamaFactory.CreateRecipe("学园最低生存保障誓约", triggers: [survivalTrigger]), DemonSoul, new Dictionary<string, object>());
    }

    private void BuildStudents()
    {
        var knowledgeMap = AcademyData.DomainKnowledges.ToDictionary(k => k.Profession);

        foreach (var spec in AcademyData.CharacterSpecs)
        {
            var initScene = Scenes[spec.InitialScene];
            var character = new Character(spec.Name, initScene);
            var soul = new Soul($"{spec.Name}的认知灵魂");
            character.Bind(soul);

            character.Set("profession", spec.Profession);
            character.Set("action_name", spec.ActionName);
            character.Set("role_desc", spec.RoleDescription);
            character.SetTag("Student", 1);
            character.SetTag("OccultDegree", spec.OccultDegree);

            if (AcademyData.AspectProfiles.TryGetValue(spec.Profession, out var aspects))
            {
                foreach (var (asp, amt) in aspects)
                {
                    character.SetTag(asp, amt);
                    soul.SetTag(asp, amt);
                }
            }

            if (knowledgeMap.TryGetValue(spec.Profession, out var kSpec))
            {
                var kEntity = new Entity(
                    kSpec.KnowledgeName,
                    soul,
                    new Dictionary<string, int> { ["Knowledge"] = 1, ["DomainKnowledge"] = 1, ["Accepted"] = 1 },
                    properties: new Dictionary<string, object>
                    {
                        ["profession"] = spec.Profession,
                        ["description"] = kSpec.Description,
                        ["stage"] = KnowledgeStage.Accepted
                    });

                foreach (var claimStr in kSpec.Claims)
                {
                    var claimExpr = AST.E(claimStr, character);
                    kEntity.Attach("Claim", claimExpr);
                    soul.Observe(claimExpr);
                }
            }

            InitHeterogeneousEmotions(soul, spec.Profession);

            int law = character.Tag("Law"), mind = character.Tag("Mind"), pres = character.Tag("Preservation"),
                bond = character.Tag("Bond"), strife = character.Tag("Strife"), vestige = character.Tag("Vestige");

            if (law >= 2 || mind >= 3)
                soul.Attach("Disposition", new Entity("求证与法理倾向", soul, new Dictionary<string, int> { ["Disposition"] = 1, ["Personality"] = 1, ["Rationality"] = 2 + law }));
            if (pres >= 2 || bond >= 2)
                soul.Attach("Disposition", new Entity("共情与同伴守望", soul, new Dictionary<string, int> { ["Disposition"] = 1, ["Personality"] = 1, ["Empathy"] = 2 + pres + bond }));
            if (strife >= 2)
                soul.Attach("Disposition", new Entity("战术戒备与警惕", soul, new Dictionary<string, int> { ["Disposition"] = 1, ["Personality"] = 1, ["Vigilance"] = 2 + strife }));
            if (vestige >= 2 || mind >= 3)
                soul.Attach("Disposition", new Entity("考据与求知冲动", soul, new Dictionary<string, int> { ["Disposition"] = 1, ["Personality"] = 1, ["Curiosity"] = 2 + vestige }));

            soul.InstallPreference((s, intent, env, ctx) =>
            {
                float delta = 0f;
                var mode = intent.ResponseMode;
                if (mode == null) return 0f;

                if (s.Dispositions.Any(d => d.Has("Rationality")))
                {
                    if (mode == ResponseMode.Verify) delta += 2.5f;
                    if (mode == ResponseMode.Negotiate) delta += 1.5f;
                }
                if (s.Dispositions.Any(d => d.Has("Empathy")))
                {
                    if (mode == ResponseMode.Protect) delta += 2.5f;
                    if (mode == ResponseMode.Cooperate) delta += 2.0f;
                    if (mode == ResponseMode.Avoid) delta -= 1.0f;
                }
                if (s.Dispositions.Any(d => d.Has("Vigilance")))
                {
                    if (mode == ResponseMode.Restrain) delta += 2.5f;
                    if (mode == ResponseMode.Avoid) delta += 2.0f;
                    if (mode == ResponseMode.Cooperate) delta -= 1.5f;
                }
                if (s.Dispositions.Any(d => d.Has("Curiosity")))
                {
                    if (mode == ResponseMode.Verify) delta += 2.0f;
                }

                return delta;
            }, strength: 1, source: "BasePersonality");

            Students[spec.Profession] = character;
            Souls[spec.Profession] = soul;
            soul.Learn(Demon, [FactSchoolClosed, FactMaintainsSurvival, FactMustSpeakTruth, FactCanHelpEscape], source: Demon, stage: KnowledgeStage.Understood);
        }
    }

    private void BuildSceneItems()
    {
        foreach (var spec in AcademyData.SceneItems)
        {
            if (Scenes.TryGetValue(spec.SceneName, out var scene))
            {
                var tags = new Dictionary<string, int>(spec.Tags) { [spec.Kind] = 1, ["Item"] = 1 };
                var props = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(spec.Content)) props["content"] = spec.Content;
                var item = new Entity(spec.Name, scene, tags, properties: props);
                if (item.Has("Lockable")) item.Lock(strength: 1, source: "机构挂锁");
                SceneItems.Add(item);
            }
        }
    }

    private void BuildStudentItems()
    {
        foreach (var spec in AcademyData.CharacterItems)
        {
            if (Students.TryGetValue(spec.Profession, out var student))
            {
                var tags = new Dictionary<string, int>(spec.Tags) { [spec.Kind] = 1, ["Item"] = 1 };
                var props = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(spec.Content)) props["content"] = spec.Content;
                var item = new Entity(spec.Name, student, tags, properties: props);
                if (spec.IsHeld) item.Hold(student);
                StudentItems.Add(item);
            }
        }
    }

    private static void InitHeterogeneousEmotions(Soul soul, string profession)
    {
        if (AcademyData.EmotionPresets.TryGetValue(profession, out var presets))
        {
            foreach (var (emo, src, val, pers) in presets)
            {
                soul.SetEmotionSource(emo, src, val, pers);
            }
        }
    }
}
