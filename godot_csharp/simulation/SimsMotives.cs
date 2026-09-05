using System;
using System.Collections.Generic;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;

namespace DualOntologyWorldEngine.Simulation;

/// <summary>
/// 模拟人生 3/4 核心需求动机系统。
/// 为每个角色维护 5 大核心动机槽，计算赤字并根据动作广告值驱动自发决策。
/// </summary>
public sealed class SimsMotiveProfile
{
    public float Hunger { get; set; } = 80f;   // 饱腹感 (0~100)
    public float Energy { get; set; } = 85f;   // 精力值 (0~100)
    public float Social { get; set; } = 75f;   // 社交满足 (0~100)
    public float Fun { get; set; } = 70f;      // 娱乐消遣 (0~100)
    public float Comfort { get; set; } = 80f;  // 舒适度 (0~100)

    public float HungerDeficit => Math.Max(0f, 100f - Hunger);
    public float EnergyDeficit => Math.Max(0f, 100f - Energy);
    public float SocialDeficit => Math.Max(0f, 100f - Social);
    public float FunDeficit => Math.Max(0f, 100f - Fun);
    public float ComfortDeficit => Math.Max(0f, 100f - Comfort);

    public void ClampAll()
    {
        Hunger = Math.Clamp(Hunger, 0f, 100f);
        Energy = Math.Clamp(Energy, 0f, 100f);
        Social = Math.Clamp(Social, 0f, 100f);
        Fun = Math.Clamp(Fun, 0f, 100f);
        Comfort = Math.Clamp(Comfort, 0f, 100f);
    }

    /// <summary>
    /// 伴随时间流逝消耗动机槽。
    /// </summary>
    public void Decay(double motionCost)
    {
        float hours = (float)(motionCost / 20.0); // 20 AV 约合 1 个标准活动时段
        Hunger -= hours * 6.0f;
        Energy -= hours * 4.0f;
        Social -= hours * 5.0f;
        Fun -= hours * 4.5f;
        Comfort -= hours * 2.0f;
        ClampAll();
    }

    /// <summary>
    /// 根据动作效果应用动机满足增益。
    /// </summary>
    public void ApplyActionReward(string op)
    {
        switch (op)
        {
            case "eat_ration":
                Hunger += 45f;
                Comfort += 15f;
                break;
            case "consume_medicine":
            case "bandage_wound":
                Comfort += 40f;
                Energy += 15f;
                break;
            case "brew_tonic":
                Fun += 25f;
                Comfort += 20f;
                break;
            case "rest":
            case "rest_nap":
            case "deep_sleep":
                Energy += 60f;
                Comfort += 25f;
                break;
            case "teach_knowledge":
            case "persuade_rule":
            case "persuade_consent":
            case "refute_argument":
            case "confront_alibi":
            case "form_patrol_pact":
            case "support_peer_item":
            case "share_partial_truth":
            case "trap_demon_logic":
            case "nullify_pact_clause":
            case "counter_demon_bargain":
            case "expose_contradiction":
            case "propose_power_shutdown":
            case "evaluate_power_dilemma":
            case "calibrate_steam_bypass":
            case "execute_power_reroute":
                Social += 40f;
                Fun += 15f;
                break;
            case "invoke_calming_incantation":
                Social += 20f;
                Comfort += 35f;
                break;
            case "inscribe_talisman":
                Comfort += 25f;
                Fun += 10f;
                break;
            case "recreation":
                Fun += 50f;
                Social += 20f;
                break;
            case "study_archives":
            case "read_document":
            case "examine_detail":
            case "search_container":
            case "track_trace":
            case "cross_examine":
            case "decode_cipher":
                Fun += 35f;
                break;
            case "maintain_mechanism":
            case "analyze_substance":
            case "purify_facility":
            case "disarm_hazard":
            case "stow_evidence":
            case "reinforce_door":
            case "store_item":
            case "retrieve_item":
            case "lock_mechanism":
            case "unlock_mechanism":
            case "take_item":
            case "drop_item":
                Fun += 20f;
                Comfort += 10f;
                break;
            case "inspect_facility":
            case "stand_watch":
                Fun += 15f;
                Comfort -= 5f;
                break;
            case "move":
                Energy -= 3f;
                Comfort -= 2f;
                break;
        }
        ClampAll();
    }

    /// <summary>
    /// 模拟人生 3/4 广告效用计算：需求赤字加权动作满足值。
    /// </summary>
    public float CalculateAdvertisingUtility(Intent intent)
    {
        var op = intent.Effect.Op;
        float score = 0f;

        switch (op)
        {
            case "cook_food":
            case "eat_ration":
            case "consume_nourishment":
                score += (HungerDeficit / 100f) * 120f;
                break;
            case "consume_medicine":
            case "bandage_wound":
            case "brew_tonic":
                score += (ComfortDeficit / 100f) * 110f;
                break;
            case "rest":
            case "rest_nap":
            case "deep_sleep":
                score += (EnergyDeficit / 100f) * 130f;
                break;
            case "teach_knowledge":
            case "persuade_rule":
            case "persuade_consent":
            case "refute_argument":
            case "confront_alibi":
            case "form_patrol_pact":
            case "support_peer_item":
            case "trap_demon_logic":
            case "nullify_pact_clause":
            case "counter_demon_bargain":
            case "interrogate_motive":
            case "expose_contradiction":
            case "share_partial_truth":
            case "propose_power_shutdown":
            case "evaluate_power_dilemma":
            case "calibrate_steam_bypass":
            case "execute_power_reroute":
                score += (SocialDeficit / 100f) * 90f;
                break;
            case "invoke_calming_incantation":
                score += (SocialDeficit / 100f) * 30f + (ComfortDeficit / 100f) * 50f;
                break;
            case "inscribe_talisman":
                score += (ComfortDeficit / 100f) * 35f;
                break;
            case "recreation":
                score += (FunDeficit / 100f) * 100f;
                break;
            case "study_archives":
            case "read_document":
            case "examine_detail":
            case "search_container":
            case "track_trace":
            case "cross_examine":
            case "decode_cipher":
                score += (FunDeficit / 100f) * 75f;
                break;
            case "maintain_mechanism":
            case "analyze_substance":
            case "purify_facility":
            case "disarm_hazard":
            case "stow_evidence":
            case "reinforce_door":
                score += 35f;
                break;
            case "take_item":
            case "retrieve_item":
            case "store_item":
            case "lock_mechanism":
            case "unlock_mechanism":
                score += 25f;
                break;
            case "inspect_facility":
                score += 20f;
                break;
            case "stand_watch":
                score += 25f;
                break;
            case "move":
                score += 15f;
                break;
            default:
                score += 10f;
                break;
        }

        return score;
    }
}
