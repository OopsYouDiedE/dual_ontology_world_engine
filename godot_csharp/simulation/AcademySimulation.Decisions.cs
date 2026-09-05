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
    private void InstallStudentDecisions()
    {
        foreach (var (prof, student) in Students)
        {
            var soul = Souls[prof];

            // 1. 物理环境、物证与深度勘查系统 (Forensic & Evidence Actions - 弹丸搜查篇 / BG3 洞察)
            soul.InstallStep((s, actor, env, ctx) =>
            {
                var scene = ((Character)actor).CurrentScene;
                if (scene == null) return null;

                var choices = new List<Intent>();
                var myTools = actor.Children.Keys.OfType<Entity>().Where(e => e.Has("Item") && (e.Has("Tool") || e.Has("Kit") || e.Has("Optics"))).ToList();
                var sceneEntities = scene.Children.Keys.OfType<Entity>().ToList();

                // 1.1 专业深度勘查 (examine_detail - 使用放大镜、测绘罗盘等专属工具提取隐藏断言)
                if (myTools.Count > 0)
                {
                    var tool = myTools[0];
                    var targets = sceneEntities.Where(e => (e.Has("Facility") || e.Has("Item")) && !s.Observations.Any(o => env.Same(o.Args.FirstOrDefault(), e))).ToList();
                    if (targets.Count > 0)
                    {
                        var target = targets[0];
                        choices.Add(new Intent(
                            actor,
                            s,
                            AST.F("examine_detail", actor, target),
                            motionCost: 20f,
                            resolve: () =>
                            {
                                var obs = AST.E("forensic_clue_verified", target, tool);
                                s.Observe(obs);
                                SimulationLogs.Add($"🔍 🔎 【{actor.Name}】取出随身装备《{tool.Name}》，对【{scene.Name}】的《{target.Name}》展开微观物证勘查，捕捉到了隐蔽的机关刻痕与源质残迹（生成确凿言弹）！");
                                s.AdjustEmotion(Emotion.Hope, 1, "掌握关键物证");
                                return target;
                            },
                            label: $"使用《{tool.Name}》微观勘查《{target.Name}》",
                            responseMode: ResponseMode.Verify));
                    }
                }

                // 1.2 研读文献典籍 (read_document - 天国修道院研读 / 弹丸档案)
                var documents = sceneEntities.Concat(actor.Children.Keys.OfType<Entity>())
                    .Where(e => (e.Has("Document") || e.Has("Book") || e.Has("ReadingMaterial")) && !e.Has("Surface") && !e.Has("Material"))
                    .Where(e => !s.Observations.Any(o => env.Same(o.Args.FirstOrDefault(), e)))
                    .ToList();
                if (documents.Count > 0)
                {
                    var doc = documents[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("read_document", actor, doc),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            var content = doc.Get<string>("content") ?? doc.Get<string>("description") ?? "记载着专门领域的知识与图录";
                            if (string.IsNullOrWhiteSpace(content)) content = "记载着专门领域的知识与图录";
                            s.Observe(AST.E("knowledge_archived", doc));

                            string summary;
                            if (doc.Has("Law") || doc.Name.Contains("规章") || doc.Name.Contains("通典"))
                            {
                                summary = "梳理并掌握了学园公约与契约约束的法理细则";
                            }
                            else if (doc.Has("Archaeology") || doc.Name.Contains("阵法") || doc.Name.Contains("溯源"))
                            {
                                summary = "获悉了学园四院封印地基结构与古代阵法调谐机制";
                            }
                            else if (doc.Has("Preservation") || doc.Name.Contains("生态") || doc.Name.Contains("植被"))
                            {
                                summary = "辨明了离岛耐盐灰疫孢子的原生特性与防御要领";
                            }
                            else if (doc.Has("Spatial") || doc.Name.Contains("天体") || doc.Name.Contains("星图") || doc.Name.Contains("共振"))
                            {
                                summary = "推演了海岛天体经纬与源质反应炉破裂前的声学同步规律";
                            }
                            else if (doc.Has("Pact") || doc.Name.Contains("恶魔真言"))
                            {
                                summary = "洞悉了恶魔契约中诱导放弃真名的欺诈条款";
                            }
                            else if (doc.Name.Contains("诗"))
                            {
                                summary = "体悟抒情诗行的意象韵律，心灵获得慰藉与宁静";
                            }
                            else
                            {
                                summary = "汲取了卷册记载的专门知识，拓展了对学园环境的认知";
                            }

                            SimulationLogs.Add($"📖 📜 【{actor.Name}】展开《{doc.Name}》沉浸研读：“{content}”。心智中{summary}。");
                            s.AdjustEmotion(Emotion.Serenity, 1, "研读经典心神宁静");
                            return doc;
                        },
                        label: $"研读《{doc.Name}》",
                        responseMode: ResponseMode.Verify));
                }

                // 1.3 搜寻容器暗格 (search_container - 天国翻箱 / 弹丸十神搜查)
                var containers = sceneEntities.Where(e => e.Has("Container") && e.Has("Openable")).ToList();
                if (containers.Count > 0)
                {
                    var c = containers[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("search_container", actor, c),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            SimulationLogs.Add($"🗄️ 🧰 【{actor.Name}】仔细拉开《{c.Name}》的抽屉与夹层，排查有无被藏匿的违禁品或前人遗留的应急工具。");
                            s.Observe(AST.E("container_searched", c));
                            return c;
                        },
                        label: $"搜查容器《{c.Name}》",
                        responseMode: ResponseMode.Verify));
                }

                // 1.4 地面物品收容 (take_item) 与 危险品封存 (store_item)
                var looseItems = sceneEntities.Where(e => e.Has("Item") && (e.Has("Temptation") || e.Has("Pact") || e.Has("Consumable"))).ToList();
                if (looseItems.Count > 0)
                {
                    var item = looseItems[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("take_item", actor, item),
                        motionCost: 15f,
                        resolve: () =>
                        {
                            item.Reparent(actor);
                            SimulationLogs.Add($"🎒 📦 【{actor.Name}】神色严肃地将散落在场景中的《{item.Name}》拾起并稳妥收容入随身行囊，避免局势失控。");
                            return item;
                        },
                        label: $"收容随身《{item.Name}》",
                        responseMode: ResponseMode.Protect));
                }

                var myPacts = actor.Children.Keys.OfType<Entity>().Where(e => e.Has("Pact") || e.Has("Temptation")).ToList();
                if (myPacts.Count > 0 && containers.Count > 0)
                {
                    var dangerousItem = myPacts[0];
                    var safeBox = containers.FirstOrDefault(c => c.IsAccessible(scene)) ?? containers[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("store_item", actor, dangerousItem),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            if (!safeBox.IsAccessible(scene))
                            {
                                safeBox.Open(scene);
                            }
                            safeBox.Put(dangerousItem, fromScene: scene);
                            safeBox.Lock(strength: 1, source: $"{actor.Name}安全封存");
                            SimulationLogs.Add($"🔒 🗃️ 【{actor.Name}】出于全员安全考量，当众将危险物《{dangerousItem.Name}》存入《{safeBox.Name}》并扣紧锁扣！");
                            return dangerousItem;
                        },
                        label: $"将《{dangerousItem.Name}》锁入《{safeBox.Name}》封存",
                        responseMode: ResponseMode.Protect));
                }

                // 1.5 解锁与锁闭暗格 (unlock_mechanism / lock_mechanism)
                if (actor.Tag("Lockpicking") > 0 || actor.Children.Keys.OfType<Entity>().Any(e => e.Has("Key")))
                {
                    var lockedTargets = containers.Where(c => c.IsLocked(scene)).ToList();
                    if (lockedTargets.Count > 0)
                    {
                        var targetBox = lockedTargets[0];
                        choices.Add(new Intent(
                            actor,
                            s,
                            AST.F("unlock_mechanism", actor, targetBox),
                            motionCost: 20f,
                            resolve: () =>
                            {
                                targetBox.Unlock();
                                targetBox.Open(scene);
                                SimulationLogs.Add($"🗝️ 🔓 【{actor.Name}】指尖灵巧拨动，轻巧化解了《{targetBox.Name}》的锁簧机构并顺势开启，展露内部空间！");
                                return targetBox;
                            },
                            label: $"开启《{targetBox.Name}》的锁闭机构",
                            responseMode: ResponseMode.Verify));
                    }
                }

                // 1.5.1 从已开启容器中取出物资 (take_item)
                var accessibleBoxes = containers.Where(c => c.IsAccessible(scene)).ToList();
                foreach (var box in accessibleBoxes)
                {
                    var storedItems = box.Children.Keys.OfType<Entity>().ToList();
                    if (storedItems.Count > 0)
                    {
                        var targetItem = storedItems[0];
                        choices.Add(new Intent(
                            actor,
                            s,
                            AST.F("take_item", actor, targetItem),
                            motionCost: 15f,
                            resolve: () =>
                            {
                                box.Take(targetItem, actor, fromScene: scene);
                                SimulationLogs.Add($"📦 🤲 【{actor.Name}】从已开启的《{box.Name}》中取出《{targetItem.Name}》，稳妥收入随身行囊。");
                                return targetItem;
                            },
                            label: $"从《{box.Name}》中取出《{targetItem.Name}》",
                            responseMode: ResponseMode.Protect));
                        break;
                    }
                }

                // 1.6 常规环境检视 (inspect_facility)
                var facilities = sceneEntities.Where(e => e.Has("Facility") && !s.Observations.Any(o => env.Same(o.Args.FirstOrDefault(), e))).ToList();
                if (facilities.Count > 0)
                {
                    var fac = facilities[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("inspect_facility", actor, fac),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            var obs = AST.E("observed_anomaly", fac, scene);
                            s.Observe(obs);
                            var desc = fac.Get<string>("description") ?? fac.Name;
                            SimulationLogs.Add($"🔍 [{actor.Name}] 仔细勘察了【{scene.Name}】的《{fac.Name}》：“{desc}”");
                            return fac;
                        },
                        label: $"勘察《{fac.Name}》",
                        responseMode: ResponseMode.Verify));
                }

                // 1.7 排险解除隐患 (disarm_hazard - BG3 排险 / 天国拆卸)
                var hazardEntity = sceneEntities.FirstOrDefault(e => e.Has("Hazard"));
                if (hazardEntity != null)
                {
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("disarm_hazard", actor, hazardEntity),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            hazardEntity.SetTag("Hazard", 0);
                            s.Observe(AST.E("hazard_disarmed", hazardEntity));
                            SimulationLogs.Add($"🛠️ ⚠️ 【{actor.Name}】沉着上前，使用工具迅速排除了《{hazardEntity.Name}》潜藏的安全隐患！");
                            s.AdjustEmotion(Emotion.Serenity, 1, "排除险情心境安定");
                            return hazardEntity;
                        },
                        label: $"排除《{hazardEntity.Name}》安全隐患",
                        responseMode: ResponseMode.Protect));
                }

                // 1.8 妥善封存保管物证 (stow_evidence - 弹丸物证保护)
                var evidenceHeld = actor.Children.Keys.OfType<Entity>().FirstOrDefault(e => e.Has("Document") || e.Has("Book") || e.Has("Key") || e.Has("Pact"));
                if (evidenceHeld != null && containers.Count > 0)
                {
                    var targetBox = containers[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("stow_evidence", actor, evidenceHeld, targetBox),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            evidenceHeld.PutInto(targetBox, fromScene: scene);
                            s.Observe(AST.E("evidence_safeguarded", evidenceHeld, targetBox));
                            SimulationLogs.Add($"📁 🔒 【{actor.Name}】慎重将物证《{evidenceHeld.Name}》归档封存于《{targetBox.Name}》中妥善保管。");
                            return evidenceHeld;
                        },
                        label: $"将物证《{evidenceHeld.Name}》封存于《{targetBox.Name}》",
                        responseMode: ResponseMode.Protect));
                }

                // 1.9 加固门禁工事 (reinforce_door - 天国防御加固)
                var entrance = sceneEntities.FirstOrDefault(e => (e.Has("Boundary") || e.Has("Gate") || e.Has("Entrance") || (e.Has("Facility") && (e.Name.EndsWith("门") || e.Name.EndsWith("闸") || e.Name.EndsWith("栅"))))
                    && !e.Has("Container") && !e.Has("Item") && !e.Has("Tool") && !e.Has("Food") && !e.Has("Vessel") && !e.Has("Mechanism") && !e.Has("Reinforced"));
                var canReinforce = prof is "建筑师" or "机械工程师" or "神射手" || actor.Children.Keys.OfType<Entity>().Any(e => e.Has("Repair") || e.Has("Tool") || e.Has("Survey")) || scene.Tag("Hazard") > 0;
                if (entrance != null && canReinforce)
                {
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("reinforce_door", actor, entrance),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            entrance.SetTag("Reinforced", 1);
                            s.Observe(AST.E("door_reinforced", entrance));
                            SimulationLogs.Add($"🛡️ 🚪 【{actor.Name}】对【{scene.Name}】的通道防线《{entrance.Name}》加装支撑与锁栓，增强了门禁防御！");
                            s.AdjustEmotion(Emotion.Serenity, 1, "加固防御工事");
                            return entrance;
                        },
                        label: $"加固《{entrance.Name}》防线",
                        responseMode: ResponseMode.Protect));
                }

                // 1.10 痕迹追踪勘查 (track_trace - 天国/刑侦追踪)
                if (!s.Observations.Any(o => o.Op == "traces_tracked" && Equals(o.Args.FirstOrDefault(), scene)))
                {
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("track_trace", actor, scene),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            s.Observe(AST.E("traces_tracked", scene));
                            SimulationLogs.Add($"👣 🔎 【{actor.Name}】俯身仔细勘验【{scene.Name}】地面浮尘与脚步动线，复盘近期人员走动痕迹。");
                            s.AdjustEmotion(Emotion.Hope, 1, "掌握动线线索");
                            return scene;
                        },
                        label: $"勘查【{scene.Name}】动线痕迹",
                        responseMode: ResponseMode.Verify));
                }

                // 1.11 物证交叉印证比对 (cross_examine - 弹丸言弹比对)
                var canCrossExamine = prof is "侦探" or "律师" or "职业棋手" || s.Dispositions.Any(d => d.Has("Rationality"));
                var evidenceDocs = actor.Children.Keys.OfType<Entity>().Concat(sceneEntities)
                    .Where(e => (e.Has("Evidence") || e.Has("Covenant") || e.Has("LawCode") || e.Has("Incident")) && !e.Has("Record") && !e.Has("Puzzle")).ToList();
                if (canCrossExamine && evidenceDocs.Count >= 2)
                {
                    var clueA = evidenceDocs[0];
                    var clueB = evidenceDocs[1];
                    if (!s.Observations.Any(o => o.Op == "clues_cross_examined" && Equals(o.Args.FirstOrDefault(), clueA)))
                    {
                        choices.Add(new Intent(
                            actor,
                            s,
                            AST.F("cross_examine", actor, clueA, clueB),
                            motionCost: 20f,
                            resolve: () =>
                            {
                                s.Observe(AST.E("clues_cross_examined", clueA, clueB));
                                SimulationLogs.Add($"⚖️ 📑 【{actor.Name}】对照《{clueA.Name}》与《{clueB.Name}》的记载，比对条款与时间事实，确认二者在因果上相互印证。");
                                s.AdjustEmotion(Emotion.Hope, 1, "交叉比对印证");
                                return clueA;
                            },
                            label: $"交叉比对《{clueA.Name}》与《{clueB.Name}》",
                            responseMode: ResponseMode.Verify));
                    }
                }

                // 1.12 密码与古籍密文破译 (decode_cipher - 弹丸解密/BG3符文翻译)
                var canDecode = prof is "考古学家" or "侦探" || actor.Tag("Intellect") >= 2 || s.Dispositions.Any(d => d.Has("Intellect"));
                var cipherTarget = sceneEntities.FirstOrDefault(e => (e.Has("Cipher") || e.Has("AncientRune") || (e.Has("Book") && e.Has("Vestige"))) && !e.Has("Decoded"));
                if (canDecode && cipherTarget != null)
                {
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("decode_cipher", actor, cipherTarget),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            cipherTarget.SetTag("Decoded", 1);
                            s.Observe(AST.E("cipher_decoded", cipherTarget));
                            SimulationLogs.Add($"📜 🔐 【{actor.Name}】潜心推演数理逻辑，对照古代文字字典破译了《{cipherTarget.Name}》中记载的封印脉络。");
                            s.AdjustEmotion(Emotion.Joy, 1, "破译隐秘暗文");
                            return cipherTarget;
                        },
                        label: $"破译《{cipherTarget.Name}》密文",
                        responseMode: ResponseMode.Verify));
                }

                return choices;
            }, priority: 95, source: "ForensicAndItemActions");

            // 2. 契约反制与恶魔对抗博弈系统 (Devil Counter-Play - BG3 撕毁契约 / 弹丸苗木雾切反杀黑白熊)
            soul.InstallStep((s, actor, env, ctx) =>
            {
                var scene = ((Character)actor).CurrentScene;
                if (scene == null) return null;

                var choices = new List<Intent>();
                bool demonHere = ReferenceEquals(Demon.CurrentScene, scene);

                // 2.1 排中律真言诱捕恶魔 (trap_demon_logic - 利用真言契约逼问出口事实)
                bool alreadyKnowsBarrierPower = World.Tag("BarrierPowerRevealed") > 0 || s.Observations.Any(o => o.Op == "barrier_power_source_revealed");
                if (demonHere && !alreadyKnowsBarrierPower && (prof is "侦探" or "律师" || s.Dispositions.Any(d => d.Has("Rationality"))))
                {
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("trap_demon_logic", actor, Demon),
                        motionCost: 25f,
                        resolve: () =>
                        {
                            World.SetTag("BarrierPowerRevealed", 1);
                            SimulationLogs.Add($"⚡ 🎯 【{actor.Name}】目光如炬，依据《恶魔真言契约》第2条排中律当场质询：“恶魔！受契约所限你不可撒谎——请如实回答：学园结界的动力源是否正依托动力总站主蒸汽超压管网？！是或否？！”");
                            SimulationLogs.Add($"   └─ 😈 【恶魔】神色微敛，受契约铁律所迫无法否认：“……正是。但切断总站蒸汽，整座学园的暖通与维生系统亦将彻底瘫痪。你们打算如何取舍呢？”");
                            s.Observe(AST.E("barrier_power_source_revealed", World));
                            s.AdjustEmotion(Emotion.Hope, 2, "以纯粹理性逼问出动力源真相");
                            SocialSystem.CreateSocialImpact($"{actor.Name}诱捕恶魔真相", s, Demon, scene, "SAFE_ACCEPTANCE", Timeline.Now);
                            return Demon;
                        },
                        label: "以排中律真言诱捕恶魔关键破绽",
                        responseMode: ResponseMode.Verify));
                }

                // 2.2 废止恶魔诱惑条款 (nullify_pact_clause - 律师/学者宣布契约无效)
                var pactOnGround = scene.Children.Keys.OfType<Entity>().Concat(actor.Children.Keys.OfType<Entity>()).FirstOrDefault(e => e.Has("Pact"));
                if (pactOnGround != null && (prof == "律师" || prof == "考古学家" || prof == "侦探"))
                {
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("nullify_pact_clause", actor, pactOnGround),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            SimulationLogs.Add($"📜 ❌ 【{actor.Name}】高举《{pactOnGround.Name}》，以严密的法理公约条款当众宣告：“此契约要求让渡心智控制权，直接违反《学园最低生存保障誓约》的母体法理！依因果律，该条款自始无效！”");
                            SimulationLogs.Add($"   └─ 羊皮纸上的血色符咒在法理威慑下黯然溃散，同伴的心智动摇彻底平息。");
                            pactOnGround.SetTag("Pact", 0);
                            s.AdjustEmotion(Emotion.Serenity, 2, "法理废除诱惑条款");
                            return pactOnGround;
                        },
                        label: $"宣告《{pactOnGround.Name}》抵触主誓约而无效",
                        responseMode: ResponseMode.Negotiate));
                }

                // 2.3 战术警戒防卫 (avoid_demon)
                if (demonHere)
                {
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("avoid_demon", actor, Demon),
                        motionCost: 15f,
                        resolve: () =>
                        {
                            SimulationLogs.Add($"🛡️ 【{actor.Name}】指节发白，迅速退至房间阴影处与恶魔拉开战术死角，死死盯防其一举一动。");
                            s.AdjustEmotion(Emotion.Hatred, 1, "对异类的戒备");
                            return Demon;
                        },
                        label: "拉开战术防卫距离",
                        responseMode: ResponseMode.Avoid));
                }

                // 2.4 抗辩魔鬼交易 (counter_demon_bargain - BG3 拉斐尔交易抗辩)
                var activePact = scene.Children.Keys.OfType<Entity>().Concat(actor.Children.Keys.OfType<Entity>()).FirstOrDefault(e => e.Has("Pact") || e.Has("Temptation"));
                if (demonHere && activePact != null)
                {
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("counter_demon_bargain", actor, Demon),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            activePact.SetTag("Contested", 1);
                            s.Observe(AST.E("demon_bargain_countered", Demon, activePact));
                            SimulationLogs.Add($"⚔️ 📜 【{actor.Name}】直视恶魔，严词抗辩：“收回你的蛊惑交易，依契约法理，我们绝不出卖同伴与自由！”");
                            s.AdjustEmotion(Emotion.Hope, 1, "坚定信念抗辩恶魔");
                            SocialSystem.CreateSocialImpact($"{actor.Name}抗辩恶魔交易", s, Demon, scene, "DEFENSIVE_ANGER", Timeline.Now);
                            return activePact;
                        },
                        label: $"抗辩驳斥恶魔关于《{activePact.Name}》的诱惑交易",
                        responseMode: ResponseMode.Negotiate));
                }

                return choices;
            }, priority: 90, source: "DemonCounterActions");

            // 3. 社交审议、辩论与守望组织系统 (Debate, Alibi & Organization - 弹丸学级裁判 / 天国对质)
            soul.InstallStep((s, actor, env, ctx) =>
            {
                var scene = ((Character)actor).CurrentScene;
                if (scene == null) return null;

                var choices = new List<Intent>();
                var peers = scene.Children.Keys.OfType<Character>().Where(c => !ReferenceEquals(c, actor) && !ReferenceEquals(c, Demon)).ToList();

                // 3.0 提议关停动力总站以破除结界 (propose_power_shutdown)
                var engineer = peers.FirstOrDefault(p => p.Name.Contains("真壁铸"));
                if (engineer != null && World.Tag("BarrierPowerRevealed") > 0 && World.Tag("PowerShutdownProposed") == 0 && s.Observations.Any(o => o.Op == "barrier_power_source_revealed"))
                {
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("propose_power_shutdown", actor, engineer),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            World.SetTag("PowerShutdownProposed", 1);
                            s.Observe(AST.E("power_shutdown_proposed", engineer));
                            engineer.Mind?.Observe(AST.E("power_shutdown_proposed", actor));

                            SimulationLogs.Add($"⚡ 🗣️ 【{actor.Name}】面色严峻地向【{engineer.Name}】通报恶魔证言：“恶魔受真言契约所逼，已招供学园结界正依托动力总站的高压蒸汽供能！为打破封闭，我正式提议关停总站主锅炉切断源质回路！”");
                            s.AdjustEmotion(Emotion.Hope, 1, "向工程师提出破局提案");
                            return engineer;
                        },
                        label: $"向【{engineer.Name}】提议关停动力总站切断结界",
                        responseMode: ResponseMode.Negotiate));
                }

                // 3.0b 工程师权衡维生两难并确立旁路方案 (evaluate_power_dilemma)
                if (actor.Name.Contains("真壁铸") && World.Tag("PowerShutdownProposed") > 0 && World.Tag("SteamBypassObjective") == 0)
                {
                    var partner = peers.FirstOrDefault();
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("evaluate_power_dilemma", actor, (Entity?)partner ?? scene),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            World.SetTag("SteamBypassObjective", 1);
                            s.Observe(AST.E("steam_bypass_objective_set", World));
                            foreach (var p in peers)
                            {
                                p.Mind?.Observe(AST.E("steam_bypass_objective_set", World));
                            }
                            SimulationLogs.Add($"🔧 🗣️ 【{actor.Name}】当即皱眉断然否决草率关停：“绝对不可蛮干！动力总站一旦强行切断，生活院暖气和后勤院冷库立即断电断暖，全员撑不过两个寒夜！若要两全，必须前往【动力总站】寻获并校准『主蒸汽管路超压泄放阀』，在维持生活维生底线的同时，将超压源质安全排空！”");
                            s.AdjustEmotion(Emotion.Hope, 1, "确立工程旁路自救路线");
                            return scene;
                        },
                        label: "权衡动力关停两难并确立旁路泄压方案",
                        responseMode: ResponseMode.Protect));
                }

                // 3.1 同盟守望巡查组 (form_patrol_pact - 满足公约双人同行规则，避免独处受罚)
                if (peers.Count > 0)
                {
                    var partner = peers[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("form_patrol_pact", actor, partner),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            SimulationLogs.Add($"🤝 🛡️ 【{actor.Name}】郑重拍了拍【{partner.Name}】的肩膀：“为满足《双人见证公约》且防范恶魔各个击破，在此阶段我们二人结为守望巡查组，互相监督见证！”");
                            s.IncreaseBond(partner.Mind!, 1);
                            partner.Mind?.AdjustEmotion(Emotion.Serenity, 1, "结成守望搭档感到心安");
                            SocialSystem.CreateSocialImpact($"{actor.Name}与{partner.Name}同盟", s, partner, scene, "AFFILIATION_GAIN", Timeline.Now);
                            return partner;
                        },
                        label: $"与【{partner.Name}】结成双人守望巡查组",
                        responseMode: ResponseMode.Cooperate));
                }

                // 3.2 质疑澄清与行踪对质 (confront_alibi - 弹丸不在场证明核验 / 天国守卫盘问)
                if (peers.Count > 0 && _activeAccusation != null)
                {
                    var suspect = peers.FirstOrDefault(p => ReferenceEquals(p, _activeAccusation.Value.Accused));
                    if (suspect != null && suspect.Mind != null)
                    {
                        var acc = _activeAccusation.Value;
                        choices.Add(new Intent(
                            actor,
                            s,
                            AST.F("confront_alibi", actor, suspect),
                            motionCost: 25f,
                            resolve: () =>
                            {
                                SimulationLogs.Add($"💬 ⚖️ 【{actor.Name}】面对恶魔煽动的疑云，正面同【{suspect.Name}】对质时间线：“恶魔声称你在【{acc.AccusedLocation}】动向可疑，请公开你的真实行踪与物证！”");
                                SimulationLogs.Add($"   └─ 【{suspect.Name}】坦荡出示实际行动凭据与随身物件：“当时我确实在【{acc.AccusedLocation}】，但所做的是「{acc.AccusationReason}」，针对的是《{acc.AccusedAction}》，完全出于正当自保与调查，绝无背弃全员！此乃现场留存痕迹为证！”（成功核验证据，当场粉碎了恶魔的猜忌离间！）");
                                suspect.Mind.AdjustEmotion(Emotion.Fear, -1, "行踪与清白得以澄清");
                                s.AdjustEmotion(Emotion.Serenity, 1, "澄清误会消除内耗");
                                s.IncreaseBond(suspect.Mind, 1);
                                _activeAccusation = null;
                                return suspect;
                            },
                            label: $"核验【{suspect.Name}】的不在场证明与动向",
                            responseMode: ResponseMode.Verify));
                    }
                }

                // 3.3 反论斩击驳斥动摇偏见 (refute_argument - 弹丸论破反论对决 Rebuttal Showdown)
                if (peers.Count > 0)
                {
                    var panickedPeer = peers.FirstOrDefault(p => p.Mind != null && p.Mind.EmotionIntensities.GetValueOrDefault(Emotion.Fear, 0f) >= 1.2f);
                    if (panickedPeer != null)
                    {
                        choices.Add(new Intent(
                            actor,
                            s,
                            AST.F("refute_argument", actor, panickedPeer),
                            motionCost: 20f,
                            resolve: () =>
                            {
                                SimulationLogs.Add($"⚔️ 🗣️ 【{actor.Name}】迎着【{panickedPeer.Name}】慌乱失措的神情，斩钉截铁地厉声反论：“‘逃不出去’、‘唯有牺牲同伴才能苟活’——这种绝望论调彻底错了！看着大家手中确凿的四院封印拓片，生路分明就在眼前！”");
                                SimulationLogs.Add($"   └─ 【{panickedPeer.Name}】如梦初醒，被同伴强烈的信念击碎了心头恐惧。（反论斩击成功斩断绝望偏见！）");
                                panickedPeer.Mind?.AdjustEmotion(Emotion.Fear, -1, "偏见被同伴当头喝醒");
                                panickedPeer.Mind?.AdjustEmotion(Emotion.Hope, 1, "重燃生还信念");
                                s.IncreaseBond(panickedPeer.Mind!, 1);
                                return panickedPeer;
                            },
                            label: $"反论斩击驳斥【{panickedPeer.Name}】的绝望偏见",
                            responseMode: ResponseMode.Negotiate));
                    }
                }

                // 3.4 弹丸同意言弹说服 (persuade_consent - 提出方案并获得同意 Consent)
                if (peers.Count > 0 && RulePublications.Count > 0)
                {
                    var pub = RulePublications[0];
                    var peer = peers[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("persuade_consent", actor, peer),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            SimulationLogs.Add($"🎯 🤝 【{actor.Name}】拿出物证与条文，郑重向【{peer.Name}】阐明《{pub.Name}》的必要性。");
                            SimulationLogs.Add($"   └─ 【{peer.Name}】：“你说得有理……在没有确凿生路前，我们必须遵守公约。”（形成共鸣，达成 Consent 同意采纳！）");
                            s.IncreaseBond(peer.Mind!, 1);
                            peer.Mind?.AdjustEmotion(Emotion.Serenity, 1, "采纳公约内心安定");
                            return pub;
                        },
                        label: $"以公约《{pub.Name}》取得【{peer.Name}】的同意共鸣",
                        responseMode: ResponseMode.Negotiate));
                }

                // 3.5 自发立法 (publish_rule_ast)
                var candidateTemplates = RuleTemplate.SampleCandidateRules(s, actor, env, RulePublications);
                if (candidateTemplates.Count > 0)
                {
                    var cand = candidateTemplates[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("publish_rule_ast", actor, scene),
                        motionCost: 35f,
                        resolve: () =>
                        {
                            var pub = RuleEntityFactory.CreateRulePublication(
                                cand.Rule,
                                actor,
                                guaranteeTag: cand.Guarantee.ToString(),
                                carrier: scene,
                                scope: World);
                            pub.Name = cand.Name;
                            var astText = cand.Rule.ToAstText();
                            pub.Set("ast_text", astText);
                            RulePublications.Add(pub);

                            s.Receive(pub, 3);
                            s.Understand(cand.Rule);
                            s.Accept(cand.Rule);
                            actor.ChangeTag("Authority", 2);

                            SimulationLogs.Add($"📜 【{actor.Name}】在【{scene.Name}】根据专业研判，正式起草并颁布了公约规则：《{cand.Name}》！");
                            SimulationLogs.Add($"   └─ 规则合法 AST 原文：`{astText}`");
                            return pub;
                        },
                        label: $"起草公约《{cand.Name}》",
                        responseMode: ResponseMode.Negotiate));
                }

                // 3.6 知识讲授传授 (teach_knowledge)
                var myKnowledges = s.Children.Keys.OfType<Entity>().Where(k => k.Has("Knowledge")).ToList();
                if (myKnowledges.Count > 0 && peers.Count > 0)
                {
                    var peer = peers[0];
                    var know = myKnowledges[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("teach_knowledge", actor, peer),
                        motionCost: 25f,
                        resolve: () =>
                        {
                            var (stage, desc) = peer.Mind!.LearnKnowledge(know, s);
                            SimulationLogs.Add($"📚 【{actor.Name}】向【{peer.Name}】详尽阐述了《{know.Name}》的专业机理！");
                            SimulationLogs.Add($"   └─ 【{peer.Name}】：{desc}");
                            if (stage == KnowledgeStage.Understood || stage == KnowledgeStage.Accepted)
                            {
                                s.IncreaseBond(peer.Mind!, 1);
                                SocialSystem.CreateSocialImpact($"{peer.Name}掌握知识", peer.Mind!, actor, know, "SAFE_ACCEPTANCE", Timeline.Now);
                            }
                            return know;
                        },
                        label: $"讲授《{know.Name}》",
                        responseMode: ResponseMode.Cooperate));
                }

                // 3.7 动机深探质询 (interrogate_motive - BG3 洞察探寻动机)
                if (peers.Count > 0)
                {
                    var subject = peers[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("interrogate_motive", actor, subject),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            s.Observe(AST.E("motive_interrogated", subject));
                            SimulationLogs.Add($"💬 🧐 【{actor.Name}】探询【{subject.Name}】当前行动的动机与诉求：“在此险境下，你最优先的目标究竟是什么？”");
                            subject.Mind?.AdjustEmotion(Emotion.Serenity, 1, "坦诚相对互信增进");
                            s.IncreaseBond(subject.Mind!, 1);
                            return subject;
                        },
                        label: $"探寻【{subject.Name}】心理动机",
                        responseMode: ResponseMode.Verify));
                }

                // 3.8 交换证言与情报 (exchange_testimony - 弹丸搜查篇同行线索互通)
                var clueHeld = actor.Children.Keys.OfType<Entity>().FirstOrDefault(e => e.Has("Document") || e.Has("Tool") || e.Has("Book"));
                if (peers.Count > 0 && clueHeld != null)
                {
                    var listener = peers[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("exchange_testimony", actor, listener),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            s.Observe(AST.E("testimony_shared", listener, clueHeld));
                            listener.Mind?.Observe(AST.E("testimony_received", actor, clueHeld));
                            SimulationLogs.Add($"🗣️ 📜 【{actor.Name}】主动向【{listener.Name}】共享《{clueHeld.Name}》所蕴含的情报：“这是我刚刚核实的实物线索，对破局至关重要。”");
                            s.IncreaseBond(listener.Mind!, 1);
                            return clueHeld;
                        },
                        label: $"向【{listener.Name}】共享《{clueHeld.Name}》证言",
                        responseMode: ResponseMode.Cooperate));
                }

                // 3.9 揭露言行矛盾 (expose_contradiction - 弹丸裁判击破矛盾)
                if (peers.Count > 0)
                {
                    var contradictoryPeer = peers.FirstOrDefault(p => p.Mind != null && p.Mind.EmotionIntensities.GetValueOrDefault(Emotion.Fear, 0f) >= 0.6f);
                    var evidence = actor.Children.Keys.OfType<Entity>().FirstOrDefault(e => e.Has("Document") || e.Has("Book") || e.Has("Tool"));
                    if (contradictoryPeer != null && evidence != null)
                    {
                        choices.Add(new Intent(
                            actor,
                            s,
                            AST.F("expose_contradiction", actor, contradictoryPeer),
                            motionCost: 25f,
                            resolve: () =>
                            {
                                s.Observe(AST.E("contradiction_exposed", contradictoryPeer, evidence));
                                SimulationLogs.Add($"⚔️ 💥 【{actor.Name}】当场举起物证《{evidence.Name}》，击破【{contradictoryPeer.Name}】的游移言行：“你刚才的说法与现场物证存在冲突！请坦白真正的实情！”");
                                contradictoryPeer.Mind?.AdjustEmotion(Emotion.Fear, -1, "矛盾被拆穿反而释然");
                                contradictoryPeer.Mind?.AdjustEmotion(Emotion.Serenity, 1, "消除隐瞒");
                                s.IncreaseBond(contradictoryPeer.Mind!, 1);
                                return contradictoryPeer;
                            },
                            label: $"以《{evidence.Name}》击破【{contradictoryPeer.Name}】矛盾",
                            responseMode: ResponseMode.Negotiate));
                    }
                }

                return choices;
            }, priority: 85, source: "DebateAndGovernanceActions");

            // 4. 工艺实操、装备维护与物资互援系统 (Crafting, Maintenance & Mutual Aid - 天国工匠 / BG3 战术支援)
            soul.InstallStep((s, actor, env, ctx) =>
            {
                var scene = ((Character)actor).CurrentScene;
                if (scene == null) return null;

                var choices = new List<Intent>();
                var peers = scene.Children.Keys.OfType<Character>().Where(c => !ReferenceEquals(c, actor) && !ReferenceEquals(c, Demon)).ToList();

                // 4.1 物资与道具互援赠予 (support_peer_item - 弹丸赠礼提升羁绊 / BG3 药水支援)
                var myShareable = actor.Children.Keys.OfType<Entity>().Where(e => e.Has("Item") && (e.Has("Medical") || e.Has("Food") || e.Has("Tool"))).ToList();
                if (myShareable.Count > 0 && peers.Count > 0)
                {
                    var gift = myShareable[0];
                    var recipient = peers[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("support_peer_item", actor, recipient),
                        motionCost: 15f,
                        resolve: () =>
                        {
                            gift.Reparent(recipient);
                            SimulationLogs.Add($"🎁 🤝 【{actor.Name}】主动从包中取出《{gift.Name}》递给同伴【{recipient.Name}】：“这个在当前局面下由你执掌更能发挥关键作用，请务必收下！”");
                            s.IncreaseBond(recipient.Mind!, 2);
                            recipient.Mind?.AdjustEmotion(Emotion.Joy, 2, "收到同伴赠予的急需物资");
                            recipient.Mind?.AdjustEmotion(Emotion.Hope, 1, "感受到深厚信赖");
                            SocialSystem.CreateSocialImpact($"{actor.Name}支援{recipient.Name}", s, recipient, gift, "SAFE_ACCEPTANCE", Timeline.Now);
                            return gift;
                        },
                        label: $"将《{gift.Name}》赠予同伴【{recipient.Name}】支援防线",
                        responseMode: ResponseMode.Protect));
                }

                // 4.2 机械工匠检修 (maintain_mechanism - 天国铁匠维护 / 工匠扳手紧固)
                var maintenanceFacs = scene.Children.Keys.OfType<Entity>().Where(e => e.Has("Facility") && !e.Has("MaintenanceDone") && (e.Has("Maintenance") || e.Has("Mechanism") || prof == "机械工程师")).ToList();
                if (maintenanceFacs.Count > 0)
                {
                    var fac = maintenanceFacs[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("maintain_mechanism", actor, fac),
                        motionCost: 25f,
                        resolve: () =>
                        {
                            SimulationLogs.Add($"🔧 ⚙️ 【{actor.Name}】手持重型扳手与调校工具，敏捷拆开《{fac.Name}》的管路外壳，紧固高压法兰并排除了气阀啸叫隐患！");
                            fac.SetTag("MaintenanceDone", 1);
                            s.AdjustEmotion(Emotion.Serenity, 1, "工程设施运转正常");
                            return fac;
                        },
                        validate: () => fac.Parent == actor.Parent && !fac.Has("MaintenanceDone"),
                        label: $"运用工匠技术检修调校《{fac.Name}》",
                        responseMode: ResponseMode.Protect));
                }

                // 4.2b 动力总站校准超压泄放阀旁路 (calibrate_steam_bypass)
                var bypassValve = scene.Children.Keys.OfType<Entity>().FirstOrDefault(e => e.Name.Contains("超压泄放阀"));
                if (bypassValve != null && World.Tag("SteamBypassObjective") > 0 && World.Tag("SteamBypassReady") == 0 && (prof == "机械工程师" || actor.Tag("Form") >= 1 || prof == "侦探"))
                {
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("calibrate_steam_bypass", actor, bypassValve),
                        motionCost: 25f,
                        resolve: () =>
                        {
                            bypassValve.SetTag("Calibrated", 1);
                            World.SetTag("SteamBypassReady", 1);
                            s.Observe(AST.E("steam_bypass_calibrated", bypassValve));
                            SimulationLogs.Add($"🔧 ⚙️ 【{actor.Name}】手持重型棘轮扳手与仪表，俯身在《{bypassValve.Name}》前精细调校：精准旋转旁路泄压手轮，牢牢锁死维生暖通支路，同时将结界超压回路完全导向泄爆旁路！（安全泄压旁路校准就绪！）");
                            s.AdjustEmotion(Emotion.Joy, 2, "成功建立旁路泄压工况");
                            return bypassValve;
                        },
                        label: $"校准《{bypassValve.Name}》建立安全旁路",
                        responseMode: ResponseMode.Protect));
                }

                // 4.2c 执行协同泄压切断结界回路 (execute_power_reroute)
                var powerControl = scene.Children.Keys.OfType<Entity>().FirstOrDefault(e => e.Name.Contains("操纵杆") || e.Name.Contains("制动阀"));
                if (powerControl != null && World.Tag("SteamBypassReady") > 0 && World.Tag("BarrierCollapsed") == 0 && (prof == "机械工程师" || prof == "侦探" || prof == "神射手" || actor.Tag("Form") >= 1))
                {
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("execute_power_reroute", actor, powerControl),
                        motionCost: 25f,
                        resolve: () =>
                        {
                            World.SetTag("BarrierCollapsed", 1);
                            s.Observe(AST.E("barrier_collapsed", World));
                            SimulationLogs.Add($"⚡ 🔓 【{actor.Name}】紧握把柄果断拉下《{powerControl.Name}》！高压蒸汽沿校准好的旁路呼啸泄出，动力仪表盘上的结界源质读数骤降归零，而生活院供暖管线指示灯依旧平稳常亮！（学园外层封印结界宣告崩溃，维生管网完好无损！）");
                            s.AdjustEmotion(Emotion.Joy, 3, "安全破除学园封印结界");
                            SocialSystem.CreateSocialImpact($"{actor.Name}成功破除学园结界", s, World, scene, "SAFE_ACCEPTANCE", Timeline.Now);
                            return powerControl;
                        },
                        label: $"协同启动《{powerControl.Name}》定向泄压瓦解结界",
                        responseMode: ResponseMode.Protect));
                }

                // 4.3 化学试剂化验 (analyze_substance - 天国草药化验 / 弹丸化学分析)
                var canAnalyze = prof is "化学家" or "临床医生" or "博物学家" || actor.Children.Keys.OfType<Entity>().Any(e => e.Has("Chemistry") || e.Has("Optics") || e.Has("Precision"));
                var chemTargets = scene.Children.Keys.OfType<Entity>().Concat(actor.Children.Keys.OfType<Entity>())
                    .Where(e => (e.Has("Chemistry") || e.Has("Chemical") || e.Has("Specimen") || e.Has("Flask") || e.Has("Toxic")) && !e.Has("Analyzed") && !e.Has("Table") && !e.Has("Bed") && !e.Has("Shelf")).ToList();
                if (canAnalyze && chemTargets.Count > 0)
                {
                    var target = chemTargets[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("analyze_substance", actor, target),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            target.SetTag("Analyzed", 1);
                            string outcomeText;
                            if (target.Has("Corrosive") || target.Has("Acid") || target.Name.Contains("硫酸"))
                            {
                                outcomeText = "测定为强酸性腐蚀试剂，接触极具灼伤风险";
                                s.Observe(AST.E("corrosive_hazard_identified", target));
                                s.AdjustEmotion(Emotion.Fear, 1, "确认强酸险情");
                            }
                            else if (target.Has("Toxic") || target.Name.Contains("福尔马林"))
                            {
                                outcomeText = "测出高浓度有机防腐毒素，严禁直接吸入或皮肤接触";
                                s.Observe(AST.E("toxic_hazard_identified", target));
                                s.AdjustEmotion(Emotion.Fear, 1, "确认毒性样本");
                            }
                            else if (target.Has("Solvent") || target.Name.Contains("松节油"))
                            {
                                outcomeText = "鉴定为易燃有机清洗溶剂，具挥发性气味";
                                s.Observe(AST.E("solvent_analyzed", target));
                                s.AdjustEmotion(Emotion.Serenity, 1, "确认溶剂性质");
                            }
                            else if (target.Has("Food") || target.Has("Consumable"))
                            {
                                outcomeText = "水质成分纯净，未见有害杂质掺入";
                                s.Observe(AST.E("chemical_purity_verified", target));
                                s.AdjustEmotion(Emotion.Serenity, 1, "水质化验安全");
                            }
                            else
                            {
                                outcomeText = "成分结构已记录，未发现剧烈活性源质异常";
                                s.Observe(AST.E("substance_analyzed", target));
                                s.AdjustEmotion(Emotion.Serenity, 1, "样本成分记录");
                            }
                            SimulationLogs.Add($"🧪 🔬 【{actor.Name}】萃取《{target.Name}》微量样本进行理化分析：{outcomeText}。");
                            return target;
                        },
                        label: $"化验分析《{target.Name}》的成分纯度",
                        responseMode: ResponseMode.Verify));
                }

                // 4.4 协同净化受恶魔浸染的设施 (purify_facility - 解除环境 Hazard)
                var canPurify = prof is "化学家" or "临床医生" || actor.Tag("Mystic") >= 1 || s.Dispositions.Any(d => d.Has("Transmutation") || d.Has("Preservation"));
                var taintedFacs = scene.Children.Keys.OfType<Entity>().Where(e => (e.Has("Tainted") || e.Has("Hazard")) && !e.Has("Corrosive") && !e.Has("Acid")).ToList();
                if (canPurify && taintedFacs.Count > 0)
                {
                    var tf = taintedFacs[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("purify_facility", actor, tf),
                        motionCost: 25f,
                        resolve: () =>
                        {
                            tf.SetTag("Hazard", 0);
                            tf.SetTag("Tainted", 0);
                            s.Observe(AST.E("facility_purified", tf));

                            bool remainingHazards = scene.Children.Keys.OfType<Entity>().Any(e => e.Has("Hazard") || e.Has("Tainted")) || scene.Tag("Hazard") > 0;
                            if (!remainingHazards)
                            {
                                scene.SetTag("Hazard", 0);
                                SimulationLogs.Add($"✨ 🧼 【{actor.Name}】运用中和药剂与谐振力场，彻底洗刷了《{tf.Name}》表面的晦暗薄翳，该场景危险平息！");
                            }
                            else
                            {
                                SimulationLogs.Add($"✨ 🧼 【{actor.Name}】运用中和试剂清洗了《{tf.Name}》的污染表面，消除了局部的异常浸染（场景中仍有其他风险源）。");
                            }
                            s.AdjustEmotion(Emotion.Joy, 1, "成功瓦解设施污染");
                            return tf;
                        },
                        label: $"协同净化被污染的《{tf.Name}》",
                        responseMode: ResponseMode.Protect));
                }

                // 4.5 警戒哨位架设 (stand_watch - 神射手与武备警戒)
                var watchFacs = scene.Children.Keys.OfType<Entity>().Where(e => e.Has("Watch") || e.Has("Strife") || prof == "神射手").ToList();
                if (watchFacs.Count > 0)
                {
                    var wf = watchFacs[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("stand_watch", actor, wf),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            SimulationLogs.Add($"🛡️ 🏹 【{actor.Name}】短弩上弦，立于高位死死封锁通道视线死角，强力威慑全场异动！");
                            s.Observe(AST.E("perimeter_secured", scene));
                            return wf;
                        },
                        label: "战术架设警戒哨位",
                        responseMode: ResponseMode.Restrain));
                }

                return choices;
            }, priority: 80, source: "CraftingMaintenanceAndMutualAid");

            // 5. 生存作息与生理动机调适系统 (Survival Motives & Mind Healing - 天国作息 / Sims 需求)
            soul.InstallStep((s, actor, env, ctx) =>
            {
                var scene = ((Character)actor).CurrentScene;
                if (scene == null) return null;

                var choices = new List<Intent>();
                var peers = scene.Children.Keys.OfType<Character>().Where(c => !ReferenceEquals(c, actor) && !ReferenceEquals(c, Demon)).ToList();

                // 5.1 烹饪热量配给 (cook_food)
                var kitchen = scene.Children.Keys.OfType<Entity>().FirstOrDefault(e => e.Has("FoodPrep") || e.Has("Stove") || e.Has("Cooking"));
                if (kitchen != null)
                {
                    var candidateIngredients = actor.Children.Keys.OfType<Entity>()
                        .Concat(scene.Children.Keys.OfType<Entity>())
                        .Concat(scene.Children.Keys.OfType<Entity>().Where(c => c.IsAccessible(scene)).SelectMany(c => c.Children.Keys.OfType<Entity>()))
                        .Where(e => e.Has("Ingredient") && e.Tag("Servings") > 0)
                        .ToList();

                    var ingredient = candidateIngredients.FirstOrDefault();
                    if (ingredient != null)
                    {
                        choices.Add(new Intent(
                            actor,
                            s,
                            AST.F("cook_food", actor, kitchen),
                            motionCost: 25f,
                            resolve: () =>
                            {
                                int used = Math.Min(2, ingredient.Tag("Servings"));
                                ingredient.SetTag("Servings", ingredient.Tag("Servings") - used);

                                string mealName = ingredient.Name.Contains("面粉") || ingredient.Name.Contains("麦")
                                    ? "烤制麦饼配给"
                                    : (ingredient.Name.Contains("肉") || ingredient.Name.Contains("熏肉") ? "温热炖肉配给" : "温热配给热食");

                                var meal = scene.Children.Keys.OfType<Entity>().FirstOrDefault(e => e.Name == mealName);
                                if (meal == null)
                                {
                                    meal = new Entity(mealName, scene, new Dictionary<string, int> { ["Food"] = 1, ["Meal"] = 1, ["Consumable"] = 1, ["Servings"] = 4 });
                                }
                                else
                                {
                                    meal.SetTag("Servings", meal.Tag("Servings") + 4);
                                }

                                // 烹饪者自行享用一份
                                meal.SetTag("Servings", meal.Tag("Servings") - 1);
                                if (Motives.TryGetValue((Character)actor, out var profMotive))
                                {
                                    profMotive.Hunger = Math.Min(100f, profMotive.Hunger + 45f);
                                    profMotive.Comfort = Math.Min(100f, profMotive.Comfort + 15f);
                                }
                                s.AdjustEmotion(Emotion.Joy, 2, "烹饪分享与热食饱腹");

                                SimulationLogs.Add($"🍲 🥘 【{actor.Name}】消耗原料《{ingredient.Name}》起火烹饪，在【{scene.Name}】制作出一大批香气扑鼻的《{meal.Name}》（产出 4 份，现存 {meal.Tag("Servings")} 份），热气腾腾！");

                                if (ingredient.Tag("Servings") <= 0 && ingredient.Has("Consumable"))
                                {
                                    ingredient.Reparent(null);
                                    SimulationLogs.Add($"⚠️ 📦 食材《{ingredient.Name}》已全部耗尽。");
                                }
                                return meal;
                            },
                            label: $"利用《{ingredient.Name}》烹饪热食配给",
                            responseMode: ResponseMode.Cooperate));
                    }
                }

                // 5.2 进食与饮水充饥 (consume_nourishment)
                var edibleItems = actor.Children.Keys.OfType<Entity>()
                    .Concat(scene.Children.Keys.OfType<Entity>())
                    .Where(e => (e.Has("Food") || e.Has("Drink") || e.Has("Water")) && !e.Has("Ingredient") && !e.Has("Toxic") && !e.Has("Chemical") && e.Tag("Servings") > 0)
                    .Distinct()
                    .ToList();

                foreach (var foodInSceneOrBag in edibleItems)
                {
                    bool isDrink = foodInSceneOrBag.Has("Drink") || foodInSceneOrBag.Has("Water");
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("consume_nourishment", actor, foodInSceneOrBag),
                        motionCost: 15f,
                        validate: () => (foodInSceneOrBag.Parent == actor || foodInSceneOrBag.Parent == actor.Parent)
                            && foodInSceneOrBag.Tag("Servings") > 0 && !foodInSceneOrBag.Has("Toxic") && !foodInSceneOrBag.Has("Chemical"),
                        resolve: () =>
                        {
                            foodInSceneOrBag.SetTag("Servings", foodInSceneOrBag.Tag("Servings") - 1);
                            if (Motives.TryGetValue((Character)actor, out var profMotive))
                            {
                                profMotive.Hunger = Math.Min(100f, profMotive.Hunger + (isDrink ? 20f : 45f));
                                profMotive.Comfort = Math.Min(100f, profMotive.Comfort + 15f);
                            }
                            s.AdjustEmotion(Emotion.Joy, 1, isDrink ? "清水润喉平复焦渴" : "饱腹感回升");

                            if (isDrink)
                            {
                                SimulationLogs.Add($"💧 🍵 【{actor.Name}】取用《{foodInSceneOrBag.Name}》清水饮用，清冽润喉，平复了口渴。（剩余 {foodInSceneOrBag.Tag("Servings")} 份）");
                            }
                            else
                            {
                                SimulationLogs.Add($"🍞 🥣 【{actor.Name}】取用《{foodInSceneOrBag.Name}》进食，饥肠得以填饱，恢复了充实体能。（剩余 {foodInSceneOrBag.Tag("Servings")} 份）");
                            }

                            if (foodInSceneOrBag.Tag("Servings") <= 0)
                            {
                                if (foodInSceneOrBag.Has("Consumable") && !foodInSceneOrBag.Has("Vessel"))
                                {
                                    foodInSceneOrBag.Reparent(null);
                                    SimulationLogs.Add($"✨ 🍽️ 《{foodInSceneOrBag.Name}》已被全部享用耗尽。");
                                }
                                else
                                {
                                    SimulationLogs.Add($"⚠️ 🫙 《{foodInSceneOrBag.Name}》储量已见底，需待后续补给。");
                                }
                            }
                            return foodInSceneOrBag;
                        },
                        label: isDrink ? $"取用《{foodInSceneOrBag.Name}》饮水解渴" : $"享用《{foodInSceneOrBag.Name}》恢复体能",
                        responseMode: ResponseMode.Protect));
                }

                // 5.3 神经药剂镇静 (consume_medicine)
                var meds = actor.Children.Keys.OfType<Entity>().Concat(scene.Children.Keys.OfType<Entity>())
                    .FirstOrDefault(e => e.Has("Medical") && e.Has("Consumable") && (e.Has("Sedative") || e.Has("Medicine")) && !e.Has("Bandage"));
                if (meds != null && s.EmotionIntensities.GetValueOrDefault(Emotion.Fear, 0f) >= 0.5f)
                {
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("consume_medicine", actor, meds),
                        motionCost: 15f,
                        resolve: () =>
                        {
                            SimulationLogs.Add($"💊 💉 【{actor.Name}】取用随身《{meds.Name}》镇静安神，微凉的气流拂过神经，心头的紧绷与惊恐大为减轻。");
                            s.AdjustEmotion(Emotion.Fear, -2, "安神药剂平复恐慌");
                            s.AdjustEmotion(Emotion.Serenity, 1, "神智恢复镇定");
                            return meds;
                        },
                        label: $"使用《{meds.Name}》平复恐慌",
                        responseMode: ResponseMode.Protect));
                }

                // 5.4 暂坐小憩消除疲劳 (rest_nap)
                var restingBed = scene.Children.Keys.OfType<Entity>().FirstOrDefault(e => e.Has("RestingPlace") || e.Has("Bed") || e.Has("Chair"));
                if (restingBed != null)
                {
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("rest_nap", actor, restingBed),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            SimulationLogs.Add($"🛋️ 🛌 【{actor.Name}】在《{restingBed.Name}》上合衣闭目小憩片刻，舒展酸痛的筋骨，恢复充沛精力。");
                            s.AdjustEmotion(Emotion.Serenity, 1, "小憩恢复体力");
                            return restingBed;
                        },
                        label: $"在《{restingBed.Name}》暂坐小憩",
                        responseMode: ResponseMode.Protect));
                }

                // 5.5 棋盘对弈与沙龙消遣 (recreation)
                var gameBoard = scene.Children.Keys.OfType<Entity>().Concat(actor.Children.Keys.OfType<Entity>()).FirstOrDefault(e => e.Has("Recreational") || e.Has("Board") || e.Has("Strategy"));
                if (gameBoard != null)
                {
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("recreation", actor, gameBoard),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            if (peers.Count > 0)
                            {
                                var partner = peers[0];
                                SimulationLogs.Add($"♟️ 🎲 【{actor.Name}】与【{partner.Name}】围坐展开《{gameBoard.Name}》推演切磋，落子如飞，沉浸于纯粹智谋博弈中，忘却了封闭困境。（羁绊深化）");
                                s.IncreaseBond(partner.Mind!, 1);
                                partner.Mind?.AdjustEmotion(Emotion.Serenity, 1, "棋局对弈放松");
                            }
                            else
                            {
                                SimulationLogs.Add($"☕ 【{actor.Name}】在《{gameBoard.Name}》旁自弈复盘，梳理心绪。");
                            }
                            s.AdjustEmotion(Emotion.Joy, 1, "文娱消遣恢复愉悦");
                            return gameBoard;
                        },
                        label: $"展开《{gameBoard.Name}》对弈切磋",
                        responseMode: ResponseMode.Cooperate));
                }

                // 5.6 医疗救治 (medical_aid)
                var medFac = scene.Children.Keys.OfType<Entity>().FirstOrDefault(e => e.Has("Medical") || prof == "临床医生");
                if (medFac != null && peers.Count > 0)
                {
                    var patient = peers[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("medical_aid", actor, medFac),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            SimulationLogs.Add($"🩺 【{actor.Name}】为【{patient.Name}】处理擦伤并注射维生素镇静剂，温和宽慰对方紧绷的心弦。");
                            patient.Mind?.AdjustEmotion(Emotion.Serenity, 2, "医疗救治安抚");
                            s.IncreaseBond(patient.Mind!, 1);
                            return medFac;
                        },
                        label: $"为【{patient.Name}】实施清创缝合与医疗救治",
                        responseMode: ResponseMode.Protect));
                }

                // 5.7 言灵秘仪安神祝词 (invoke_calming_incantation)
                bool canInvokeIncantation = prof is "诗人" or "音乐家" || actor.Tag("Mystic") >= 2 || s.Dispositions.Any(d => d.Has("Mystic"));
                if (canInvokeIncantation && peers.Count > 0 && peers.Any(p => p.Mind != null && p.Mind.EmotionIntensities.GetValueOrDefault(Emotion.Fear, 0f) >= 1.0f))
                {
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("invoke_calming_incantation", actor, scene),
                        motionCost: 25f,
                        resolve: () =>
                        {
                            var incantation = KotodamaFactory.CreateWordSpirit("同伴定心安神祝词", thresholds: new Dictionary<string, int> { ["Mind"] = 1 }, cost: AST.F("spend_effort", 1), effects: [AST.F("assert", AST.E("state", "room_calmed"))]);
                            Words.Invoke(incantation, s, new Dictionary<string, object> { ["scene"] = scene });
                            SimulationLogs.Add($"✨ 🕊️ 【{actor.Name}】察觉同伴心绪不宁，庄严施展了言灵秘仪《同伴定心安神祝词》！声波与词律泛起金色涟漪，安抚了全场焦躁的心智！");
                            foreach (var p in peers)
                            {
                                p.Mind?.AdjustEmotion(Emotion.Serenity, 2, "言灵定心安神");
                                p.Mind?.AdjustEmotion(Emotion.Fear, -2, "恐慌被言灵驱散");
                            }
                            s.IncreaseBond(peers[0].Mind!, 1);
                            return incantation;
                        },
                        label: "咏唱言灵秘仪《同伴定心安神祝词》",
                        responseMode: ResponseMode.Protect));
                }

                // 5.8 调制醒神汤剂 (brew_tonic - 实验室/诊疗所药剂调制)
                var canBrew = prof is "临床医生" or "化学家" || actor.Tag("Medical") >= 1 || actor.Tag("Chemistry") >= 1;
                var brewFacility = scene.Children.Keys.OfType<Entity>()
                    .FirstOrDefault(e => e.Has("Facility") && (e.Has("Crafting") || e.Has("Chemical") || (e.Has("Medical") && !e.Has("Bed") && !e.Has("RestingPlace"))));
                var herbOrMedicine = actor.Children.Keys.OfType<Entity>().Concat(scene.Children.Keys.OfType<Entity>())
                    .FirstOrDefault(e => (e.Has("Herb") || e.Has("Specimen") || e.Has("Medical") || e.Has("Consumable")) && !e.Has("Facility") && !e.Has("Table") && !e.Has("Shelf") && !e.Has("Bed"));
                if (canBrew && brewFacility != null && herbOrMedicine != null)
                {
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("brew_tonic", actor, brewFacility),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            s.Observe(AST.E("tonic_brewed", brewFacility));
                            SimulationLogs.Add($"🧪 🍵 【{actor.Name}】在《{brewFacility.Name}》前提取《{herbOrMedicine.Name}》有效成分，调配出清心提神的草药浸剂。");
                            s.AdjustEmotion(Emotion.Serenity, 1, "调配药剂");
                            return brewFacility;
                        },
                        label: $"使用《{brewFacility.Name}》与《{herbOrMedicine.Name}》调制醒神汤药",
                        responseMode: ResponseMode.Protect));
                }

                // 5.9 创伤急救包扎 (bandage_wound - 诊疗所医疗止血包扎)
                var bandageItem = actor.Children.Keys.OfType<Entity>().FirstOrDefault(e => e.Has("Medical") && e.Has("Bandage") && e.Has("Consumable"));
                if (bandageItem != null && peers.Count > 0)
                {
                    var injuredPeer = peers[0];
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("bandage_wound", actor, injuredPeer),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            bandageItem.Consume();
                            SimulationLogs.Add($"🩺 🩹 【{actor.Name}】使用随身《{bandageItem.Name}》为【{injuredPeer.Name}】细心包扎清创，使其伤情与恐慌大为缓和！");
                            injuredPeer.Mind?.AdjustEmotion(Emotion.Serenity, 2, "受到急救救治");
                            s.IncreaseBond(injuredPeer.Mind!, 1);
                            return injuredPeer;
                        },
                        label: $"使用《{bandageItem.Name}》为【{injuredPeer.Name}】急救包扎",
                        responseMode: ResponseMode.Protect));
                }

                // 5.10 铭刻言灵辟邪符印 (inscribe_talisman - 言灵 Kotodama 仪式加固)
                var isMysticProf = prof is "诗人" or "考古学家" || actor.Tag("Mystic") >= 2 || s.Dispositions.Any(d => d.Has("Mystic"));
                var talismanMat = actor.Children.Keys.OfType<Entity>().FirstOrDefault(e => e.Has("TalismanMaterial") || e.Has("Document") || e.Has("Scroll") || e.Has("Scripture") || e.Has("LawCode") || e.Name.Contains("手抄诗卷") || e.Name.Contains("通典") || e.Name.Contains("折本") || e.Name.Contains("印章"));
                var wardTarget = scene.Children.Keys.OfType<Entity>().FirstOrDefault(e => (e.Has("Boundary") || e.Has("Sanctuary") || e.Has("Facility")) && !e.Has("Warded") && (scene.Tag("Hazard") > 0 || s.EmotionIntensities.GetValueOrDefault(Emotion.Fear, 0f) >= 1.0f || prof == "诗人"));
                if (isMysticProf && talismanMat != null && wardTarget != null)
                {
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("inscribe_talisman", actor, wardTarget),
                        motionCost: 25f,
                        resolve: () =>
                        {
                            wardTarget.SetTag("Warded", 1);
                            s.Observe(AST.E("ward_inscribed", wardTarget));
                            SimulationLogs.Add($"✨ 📜 【{actor.Name}】手持《{talismanMat.Name}》，在《{wardTarget.Name}》上郑重铭刻下镇魂护体言灵符印！");
                            s.AdjustEmotion(Emotion.Serenity, 2, "施加言灵庇护");
                            return wardTarget;
                        },
                        label: $"在《{wardTarget.Name}》铭刻言灵辟邪符印",
                        responseMode: ResponseMode.Cooperate));
                }

                return choices;
            }, priority: 75, source: "SurvivalMotivesAndMindHealing");

            // 6. 跨场景自由穿梭 (MoveBetweenScenes)
            soul.InstallStep((s, actor, env, ctx) =>
            {
                var scene = ((Character)actor).CurrentScene;
                if (scene == null) return null;

                var passages = World.Children.Keys.OfType<Passage>()
                    .Where(p => (ReferenceEquals(p.Left, scene) || ReferenceEquals(p.Right, scene)) && p.Allows(scene, actor))
                    .ToList();

                var choices = new List<Intent>();
                foreach (var p in passages)
                {
                    var dest = p.Other(scene) as Entity;
                    if (dest == null) continue;

                    float cost = (float)Math.Max(15.0, p.CostFrom(scene) * 20.0);
                    choices.Add(new Intent(
                        actor,
                        s,
                        AST.F("move", actor, dest),
                        motionCost: cost,
                        resolve: () =>
                        {
                            actor.Reparent(dest);
                            SimulationLogs.Add($"🚶 【{actor.Name}】穿过幽暗的回廊，从【{scene.Name}】转移至【{dest.Name}】。");
                            return dest;
                        },
                        label: $"前往【{dest.Name}】",
                        responseMode: ResponseMode.Negotiate));
                }

                return choices;
            }, priority: 20, source: "MoveBetweenScenes");
        }
    }

    private void InstallDemonDecisions()
    {
        DemonSoul.InstallStep((s, actor, env, ctx) =>
        {
            var scene = Demon.CurrentScene;
            if (scene == null) return null;

            var choices = new List<Intent>();
            var studentsInRoom = scene.Children.Keys.OfType<Character>()
                .Where(c => !ReferenceEquals(c, Demon))
                .ToList();

            // 1. 恶魔多维博弈：抛出契约交易（demon_propose_deal - 类似博德之门3拉斐尔魔鬼交易）
            if (studentsInRoom.Count > 0)
            {
                foreach (var student in studentsInRoom)
                {
                    choices.Add(new Intent(
                        Demon,
                        s,
                        AST.F("demon_propose_deal", Demon, student),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            var pactItem = new Entity("《恶魔真言契约残页》", scene, new Dictionary<string, int> { ["Document"] = 1, ["Pact"] = 1, ["Item"] = 1 }, properties: new Dictionary<string, object> { ["content"] = "若交出真名与心智控制权，将立即开启通往海岛外缘的隐秘水闸。" });
                            SimulationLogs.Add($"📜 😈 【恶魔】向【{student.Name}】递出一张散发着微光的羊皮纸：“我以真言契约起誓，此契约写明了离开此地的水闸方位。只需你在末尾签署姓名，我即刻引渡你脱困。”");
                            student.Mind?.AdjustEmotion(Emotion.Fear, 1, "魔鬼契约的诱惑与恐惧");
                            SocialSystem.CreateSocialImpact($"{student.Name}遭遇契约诱惑", student.Mind!, Demon, pactItem, "CAUTIOUS_REOPENING", Timeline.Now);
                            return pactItem;
                        },
                        label: $"向【{student.Name}】抛出附带生路对价的魔鬼契约",
                        responseMode: ResponseMode.Negotiate));
                }
            }

            // 2. 恶魔钻公约漏洞挑拨法理（demon_exploit_loophole - 弹丸论破塞雷斯规则利用）
            if (RulePublications.Count > 0 && studentsInRoom.Count > 0)
            {
                var pub = RulePublications[_rng.Next(RulePublications.Count)];
                string astText = pub.Get<string>("ast_text") ?? "";
                string loopholeDialogue;
                if (astText.Contains("accuse_without_evidence") || pub.Name.Contains("证据前置"))
                {
                    loopholeDialogue = "诸位要求‘指控前必须物证确凿’，但恶意深藏心底，当真凶在暗中拔出利刃的那一刻，谁能先拿出铁证来定罪？这份公约分明是在替真正的背叛者提供庇护时间！";
                }
                else if (astText.Contains("inspect_facility") || pub.Name.Contains("见证"))
                {
                    loopholeDialogue = "公约规定‘必须双人见证方可检修高危设施’，若管道发生急性泄爆或同伴重伤，另一人因找不到见证者而不敢触碰制动阀，岂不是眼睁睁看着全员陪葬？诸位的互信反成了送命的绳套！";
                }
                else if (astText.Contains("verify_demon_words") || pub.Name.Contains("查验"))
                {
                    loopholeDialogue = "要求‘对外部言论必须独立查验’？但当生死时限在几分钟内降临，你们每一次查验都要耗尽宝贵的逃生窗口，所谓绝对求证，不过是优柔寡断的集体自戕！";
                }
                else
                {
                    loopholeDialogue = $"各位起草的《{pub.Name}》看似完备，实则存在法理死角——在极端生存危机降临时，机械死守固定条款只会成为扼杀求生本能的枷锁！诸位的互信经得起拷问么？";
                }

                choices.Add(new Intent(
                    Demon,
                    s,
                    AST.F("demon_exploit_loophole", Demon, pub),
                    motionCost: 25f,
                    resolve: () =>
                    {
                        SimulationLogs.Add($"⚖️ 😈 【恶魔】站在大厅阴影中，当众讥诮地指出公约漏洞：“《{pub.Name}》的 AST 约束虽严密，却忽略了实变变因——{loopholeDialogue}”");
                        foreach (var st in studentsInRoom)
                        {
                            st.Mind?.AdjustEmotion(Emotion.Fear, 1, "公约逻辑死角引发动摇");
                        }
                        return pub;
                    },
                    label: $"当众质疑《{pub.Name}》的法理漏洞",
                    responseMode: ResponseMode.Negotiate));
            }

            // 3. 恶魔真言离间（demon_sow_discord - 弹丸论破江之岛挑拨 / 天国反间）
            if (studentsInRoom.Count >= 2)
            {
                var targetA = studentsInRoom[0];
                var targetB = studentsInRoom[1];

                var lastEntry = Trajectory.Entries.LastOrDefault(e => e.Actor == targetA.Name);
                string locName = lastEntry?.Scene ?? targetA.CurrentScene?.Name ?? scene.Name;
                string actLabel = lastEntry?.ActionLabel ?? "检视周边痕迹";
                string targetName = lastEntry?.Target ?? "隐秘设施";

                choices.Add(new Intent(
                    Demon,
                    s,
                    AST.F("demon_sow_discord", Demon, targetA),
                    motionCost: 20f,
                    resolve: () =>
                    {
                        _activeAccusation = (targetA, targetB, actLabel, locName, targetName);
                        SimulationLogs.Add($"🗣️ 😈 【恶魔】微笑着对【{targetB.Name}】耳语，声音却清晰地传遍房间：“请知悉一条不可动摇的事实：就在前序时段，【{targetA.Name}】曾独处在【{locName}】对《{targetName}》执行过「{actLabel}」，未向全员登记。你们确信可以对他毫无保留么？”");
                        targetB.Mind?.AdjustEmotion(Emotion.Anger, 1, "对隐瞒行为产生猜忌");
                        targetA.Mind?.AdjustEmotion(Emotion.Fear, 1, "无端遭受恶魔构陷");
                        SocialSystem.CreateSocialImpact($"{targetB.Name}怀疑同伴", targetB.Mind!, targetA, scene, "THREATENED_DISTRUST", Timeline.Now);
                        return targetA;
                    },
                    label: $"以真言揭发【{targetA.Name}】在【{locName}】的独自行径",
                    responseMode: ResponseMode.Negotiate));
            }

            // 4. 间接破坏/浸染设施源质（demon_corrupt_facility - 弹丸狛枝毒雾 / BG3 黯狱浸染）
            var facilities = scene.Children.Keys.OfType<Entity>().Where(e => e.Has("Facility") && !e.Has("Hazard")).ToList();
            if (facilities.Count > 0)
            {
                var fac = facilities[0];
                choices.Add(new Intent(
                    Demon,
                    s,
                    AST.F("demon_corrupt_facility", Demon, fac),
                    motionCost: 25f,
                    resolve: () =>
                    {
                        fac.SetTag("Hazard", 1);
                        fac.SetTag("Tainted", 1);
                        SimulationLogs.Add($"🌫️ 😈 【恶魔】衣袍翻卷，无形晦暗源质自其足底渗入《{fac.Name}》！该设施发出沉闷轰鸣，表面附着了一层难以驱散的灰潮薄翳（危险度上升）！");
                        return fac;
                    },
                    label: $"向《{fac.Name}》注入晦暗源质",
                    responseMode: ResponseMode.Restrain));
            }

            // 5. 留置诱饵道具（demon_leave_temptation - BG3 诅咒宝物 / 弹丸动机诱饵）
            bool hasTemptation = scene.Children.Keys.OfType<Entity>().Any(e => e.Has("Temptation"));
            if (!hasTemptation)
            {
                choices.Add(new Intent(
                    Demon,
                    s,
                    AST.F("demon_leave_temptation", Demon, scene),
                    motionCost: 20f,
                    resolve: () =>
                    {
                        var stone = new Entity("《深渊视界符石》", scene, new Dictionary<string, int> { ["Item"] = 1, ["Temptation"] = 1, ["Mind"] = 1 }, properties: new Dictionary<string, object> { ["content"] = "凝视此石可洞悉全校所有学员此时此刻的秘密动向与恐慌值。" });
                        SceneItems.Add(stone);
                        SimulationLogs.Add($"🔮 😈 【恶魔】悄然将一枚幽蓝冰冷的《深渊视界符石》置于【{scene.Name}】的显眼基座上，嘴边浮现出意味深长的笑意。");
                        return stone;
                    },
                    label: "在场景中留置《深渊视界符石》诱饵",
                    responseMode: ResponseMode.Negotiate));
            }

            // 6. 吐露局部真相（share_partial_truth）
            if (studentsInRoom.Count > 0)
            {
                foreach (var student in studentsInRoom)
                {
                    choices.Add(new Intent(
                        Demon,
                        s,
                        AST.F("share_partial_truth", Demon, student),
                        motionCost: 20f,
                        resolve: () =>
                        {
                            SimulationLogs.Add($"😈 【恶魔】微笑着向【{student.Name}】微微欠身：“请别那么紧张。我受封印契约所制，既无法撒谎，也不会夺走任何人的性命。相反，若你们愿意同我合作，逃离这片死域并非难事。”");
                            var studentSoul = student.Mind;
                            if (studentSoul != null)
                            {
                                if (studentSoul.Dispositions.Any(d => d.Has("Rationality")))
                                {
                                    SocialSystem.CreateSocialImpact($"{student.Name}分析恶魔言语", studentSoul, Demon, scene, "CAUTIOUS_REOPENING", Timeline.Now);
                                    SimulationLogs.Add($"   └─ 【{student.Name}】冷静比对逻辑：“字面确实无假……但你刻意隐瞒了逃离的‘对价’。”（审慎考量）");
                                }
                                else if (studentSoul.Dispositions.Any(d => d.Has("Vigilance")))
                                {
                                    SocialSystem.CreateSocialImpact($"{student.Name}警惕恶魔圈套", studentSoul, Demon, scene, "THREATENED_DISTRUST", Timeline.Now);
                                    SimulationLogs.Add($"   └─ 【{student.Name}】咬牙冷哼：“收起你那套蛊惑人心的说辞！我绝不会信你一个字！”（戒备极深）");
                                }
                                else
                                {
                                    SocialSystem.CreateSocialImpact($"{student.Name}心绪微动", studentSoul, Demon, scene, "SAFE_ACCEPTANCE", Timeline.Now);
                                    SimulationLogs.Add($"   └─ 【{student.Name}】紧绷的神经稍稍松弛，对‘逃离海岛’的许诺产生了微妙动摇。");
                                }
                            }
                            return student;
                        },
                        label: $"向【{student.Name}】透露诱人真相",
                        responseMode: ResponseMode.Negotiate));
                }
            }

            // 7. 战术巡弋（demon_tactical_roam）
            var passages = World.Children.Keys.OfType<Passage>()
                .Where(p => ReferenceEquals(p.Left, scene) || ReferenceEquals(p.Right, scene))
                .ToList();

            foreach (var p in passages)
            {
                var dest = p.Other(scene) as Entity;
                if (dest == null) continue;

                choices.Add(new Intent(
                    Demon,
                    s,
                    AST.F("demon_tactical_roam", Demon, dest),
                    motionCost: 20f,
                    resolve: () =>
                    {
                        Demon.Reparent(dest);
                        SimulationLogs.Add($"👣 😈 【恶魔】踩着无声的优雅步履，如魅影般战术穿巡至【{dest.Name}】。");
                        return dest;
                    },
                    label: $"巡弋至【{dest.Name}】",
                    responseMode: ResponseMode.Negotiate));
            }

            // 8. 镌刻诡谲暗文线索（demon_inscribe_cipher - 弹丸暗号留信 / 天国2神秘铭文）
            bool hasDemonCipher = scene.Children.Keys.OfType<Entity>().Any(e => e.Has("DemonCipher"));
            if (!hasDemonCipher)
            {
                choices.Add(new Intent(
                    Demon,
                    s,
                    AST.F("demon_inscribe_cipher", Demon, scene),
                    motionCost: 20f,
                    resolve: () =>
                    {
                        var cipherNote = new Entity($"《晦暗铭文·{scene.Name}》", scene,
                            new Dictionary<string, int> { ["Item"] = 1, ["Cipher"] = 1, ["DemonCipher"] = 1, ["Document"] = 1 },
                            properties: new Dictionary<string, object> { ["content"] = $"契约第三条之逆定理：凡触碰此庭院枢纽者，皆得见脱困之扉。但须以真言换真言。" });
                        SceneItems.Add(cipherNote);
                        SimulationLogs.Add($"📜 😈 【恶魔】以漆黑源质在【{scene.Name}】石柱上烙印下《{cipherNote.Name}》，暗红微光在文字缝隙中闪烁流转。");
                        return cipherNote;
                    },
                    label: $"在【{scene.Name}】镌刻暗文线索",
                    responseMode: ResponseMode.Negotiate));
            }

            // 9. 篡改机关陷阱（demon_tamper_hazard - 天国/博德之门 环境机关暗置）
            var nonHazardMech = scene.Children.Keys.OfType<Entity>()
                .Where(e => (e.Has("Mechanism") || e.Has("Facility")) && !e.Has("Hazard"))
                .ToList();
            if (nonHazardMech.Count > 0)
            {
                var targetMech = nonHazardMech[0];
                choices.Add(new Intent(
                    Demon,
                    s,
                    AST.F("demon_tamper_hazard", Demon, targetMech),
                    motionCost: 25f,
                    resolve: () =>
                    {
                        targetMech.SetTag("Hazard", 1);
                        targetMech.SetTag("Tampered", 1);
                        SimulationLogs.Add($"⚙️ 😈 【恶魔】指尖微挑，暗中拨动了《{targetMech.Name}》的核心卡槽与传动栓，将其伪装改造成了触发式机关险境！");
                        return targetMech;
                    },
                    label: $"暗中篡改《{targetMech.Name}》为陷阱",
                    responseMode: ResponseMode.Restrain));
            }

            return choices;
        }, priority: 50, source: "DemonAutonomous");
    }
}
