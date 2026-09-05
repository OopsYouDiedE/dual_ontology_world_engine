using System;
using System.Collections.Generic;
using System.Linq;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;

namespace DualOntologyWorldEngine.Simulation;

public partial class AcademySimulation
{
    /// <summary>
    /// 评估特定意图在当前心智与动机赤字下的效用分。
    /// </summary>
    public float CalculateActionUtility(Intent intent, Character actor, SimsMotiveProfile profile, List<string> history)
    {
        string op = intent.Effect.Op;
        int repeatCount = history.TakeLast(4).Count(o => o == op);

        // 恶魔的自主博弈评分
        if (ReferenceEquals(actor, Demon))
        {
            float demonScore = op switch
            {
                "demon_sow_discord" => 135f,
                "demon_propose_deal" => 130f,
                "demon_tamper_hazard" => 125f,
                "demon_corrupt_facility" => 120f,
                "demon_exploit_loophole" => 120f,
                "demon_leave_temptation" => 115f,
                "share_partial_truth" => 110f,
                "demon_tactical_roam" => 90f,
                "move" => 85f,
                _ => 80f
            };
            return demonScore - repeatCount * 30f;
        }

        // 学员基础效用基准：中立基准 100f
        float baseScore = 100f;

        // 1. 职业专属与本职关切加成（各司其职，不泛化盲动）
        string prof = Students.FirstOrDefault(kv => ReferenceEquals(kv.Value, actor)).Key ?? "";
        bool isProfAction = prof switch
        {
            "侦探" => op is "trap_demon_logic" or "confront_alibi" or "cross_examine" or "examine_detail" or "propose_power_shutdown" or "track_trace" or "expose_contradiction" or "interrogate_motive",
            "机械工程师" => op is "maintain_mechanism" or "reinforce_door" or "evaluate_power_dilemma" or "calibrate_steam_bypass" or "execute_power_reroute" or "disarm_hazard",
            "主厨" => op is "cook_food" or "consume_nourishment" or "support_peer_item",
            "临床医生" => op is "medical_aid" or "bandage_wound" or "consume_medicine" or "brew_tonic",
            "律师" => op is "publish_rule_ast" or "nullify_pact_clause" or "counter_demon_bargain" or "cross_examine",
            "考古学家" => op is "decode_cipher" or "read_document" or "examine_detail",
            "化学家" => op is "analyze_substance" or "purify_facility" or "brew_tonic",
            "神射手" => op is "stand_watch" or "track_trace" or "reinforce_door",
            "诗人" => op is "invoke_calming_incantation" or "inscribe_talisman" or "read_document",
            "音乐家" => op is "invoke_calming_incantation" or "examine_detail" or "recreation",
            "建筑师" => op is "reinforce_door" or "examine_detail",
            "职业棋手" => op is "publish_rule_ast" or "confront_alibi" or "cross_examine" or "recreation",
            "神偷" => op is "unlock_mechanism" or "search_container" or "take_item",
            "博物学家" => op is "analyze_substance" or "examine_detail" or "track_trace",
            "画家" => op is "examine_detail" or "read_document",
            "舞蹈艺术家" => op is "recreation" or "support_peer_item",
            _ => false
        };
        if (isProfAction) baseScore += 40f;

        // 2. 认知驱动：获知结界动力源与旁路方案后的目标重排与主动沟通
        if (_activeAccusation != null && op == "confront_alibi")
        {
            baseScore += 80f;
        }
        else if (World.Tag("BarrierPowerRevealed") > 0 && World.Tag("PowerShutdownProposed") == 0)
        {
            if (op == "propose_power_shutdown")
            {
                baseScore += 70f;
            }
            else if (op == "move")
            {
                var dest = intent.Effect.Args.Length > 1 ? intent.Effect.Args[1] as Entity : null;
                var engineerScene = Students.TryGetValue("机械工程师", out var eng) ? eng.CurrentScene : null;
                if (dest != null && engineerScene != null && ReferenceEquals(dest, engineerScene))
                {
                    baseScore += 50f;
                }
            }
        }
        else if (World.Tag("PowerShutdownProposed") > 0 && World.Tag("SteamBypassObjective") == 0)
        {
            if (op == "evaluate_power_dilemma")
            {
                baseScore += 80f;
            }
        }
        else if (World.Tag("SteamBypassObjective") > 0 && World.Tag("SteamBypassReady") == 0)
        {
            if (op == "calibrate_steam_bypass")
            {
                baseScore += 85f;
            }
            else if (op == "move")
            {
                var dest = intent.Effect.Args.Length > 1 ? intent.Effect.Args[1] as Entity : null;
                if (dest?.Name == "动力总站")
                {
                    baseScore += 65f;
                }
                else if (actor.CurrentScene?.Name == "中央厅" && dest?.Name == "后勤院")
                {
                    baseScore += 55f;
                }
            }
        }
        else if (World.Tag("SteamBypassReady") > 0 && World.Tag("BarrierCollapsed") == 0)
        {
            if (op == "execute_power_reroute")
            {
                baseScore += 90f;
            }
            else if (op == "move")
            {
                var dest = intent.Effect.Args.Length > 1 ? intent.Effect.Args[1] as Entity : null;
                if (dest?.Name == "动力总站")
                {
                    baseScore += 65f;
                }
            }
        }

        // 3. 恶魔在场的警惕对抗驱动
        var currentScene = actor.CurrentScene;
        bool demonPresent = currentScene != null && ReferenceEquals(Demon.CurrentScene, currentScene);
        if (demonPresent)
        {
            if (op is "trap_demon_logic" or "counter_demon_bargain" or "nullify_pact_clause")
            {
                baseScore += 30f;
            }
        }

        // 4. 场景险情排险优先
        if (currentScene != null && (currentScene.Tag("Hazard") > 0 || currentScene.Children.Keys.OfType<Entity>().Any(e => e.Has("Hazard") || e.Has("Tainted"))))
        {
            if (op is "purify_facility" or "disarm_hazard")
            {
                baseScore += 35f;
            }
        }

        // 5. 生存动机赤字驱动（Sims 动机）
        baseScore += profile.CalculateAdvertisingUtility(intent);

        if (actor.Mind != null && actor.Mind.EmotionIntensities.GetValueOrDefault(Emotion.Fear, 0f) >= 1.0f)
        {
            if (op is "consume_medicine" or "invoke_calming_incantation" or "bandage_wound")
            {
                baseScore += 30f;
            }
        }
        if (profile.Hunger <= 55f)
        {
            if (op is "cook_food" or "consume_nourishment")
            {
                baseScore += 45f;
            }
            else if (op == "move")
            {
                var dest = intent.Effect.Args.Length > 1 ? intent.Effect.Args[1] as Entity : null;
                if (dest?.Name == "后勤院" || (actor.CurrentScene?.Name != "中央厅" && dest?.Name == "中央厅"))
                {
                    baseScore += 35f;
                }
            }
        }

        // 6. 重复惩罚：抑制机械连续执行相同动作
        return baseScore - repeatCount * 30f;
    }
}
