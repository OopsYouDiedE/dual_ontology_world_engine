using System;
using System.Collections.Generic;
using System.Linq;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;

namespace DualOntologyWorldEngine.Simulation;

public partial class AcademySimulation
{
    /// <summary>
    /// 夜间作息调度：执行生理疲劳核算、就寝判定与滞夜排查。
    /// </summary>
    public void ProcessNightPhase(int day)
    {
        SimulationLogs.Add($"\n--- 第 {day} 日 · 夜晚（就寝作息、疲劳判定与滞夜排查） ---");

        var sleeping = new List<Character>();
        var forcedSleep = new List<(Character Student, string Reason)>();
        var awake = new List<(Character Student, string Reason)>();
        var livingQuarters = Scenes["生活院"];

        foreach (var (prof, student) in Students)
        {
            var soul = student.Mind!;
            var currentScene = student.CurrentScene;

            Motives.TryGetValue(student, out var m);

            if (soul.Fatigue >= 2.5f)
            {
                student.Reparent(livingQuarters);
                soul.SetFatigue(0);
                if (m != null)
                {
                    m.Energy = 100f;
                    m.Comfort = Math.Min(100f, m.Comfort + 20f);
                }
                forcedSleep.Add((student, "昨日过度透支精力，今夜体力濒临极限，刚沾床枕便陷入极度沉睡（强制生理休眠）"));
                soul.AdjustEmotion(Emotion.Serenity, 2, "透支后深度补眠恢复");
                continue;
            }

            bool canStayUp = soul.Fatigue < 2.0f;
            bool curiosityLate = canStayUp && soul.Dispositions.Any(d => d.Has("Curiosity")) &&
                                 (currentScene?.Name == "教学图书馆" || currentScene?.Name == "化学实验室");

            bool vigilanceLate = canStayUp && soul.Dispositions.Any(d => d.Has("Vigilance")) &&
                                 (currentScene?.Name == "门楼与誓约庭" || currentScene?.Name == "训练院");

            if (curiosityLate)
            {
                soul.ChangeFatigue(2.5);
                if (m != null)
                {
                    m.Energy = Math.Max(10f, m.Energy - 20f);
                    m.Fun = Math.Min(100f, m.Fun + 15f);
                }
                awake.Add((student, $"在【{currentScene!.Name}】查阅古籍拓本与实验数据，通宵演算（疲劳度大幅累积：{soul.Fatigue:F1}）"));
                soul.SetEmotionSource(Emotion.Fear, "通宵脑力过载", 1.2f, isPersistent: false);
            }
            else if (vigilanceLate)
            {
                soul.ChangeFatigue(2.5);
                if (m != null)
                {
                    m.Energy = Math.Max(10f, m.Energy - 20f);
                    m.Social = Math.Min(100f, m.Social + 10f);
                }
                awake.Add((student, $"在【{currentScene!.Name}】值守巡查，戒备封印异动（疲劳度大幅累积：{soul.Fatigue:F1}）"));
                soul.SetEmotionSource(Emotion.Fear, "夜巡高度警惕", 1.5f, isPersistent: false);
            }
            else
            {
                student.Reparent(livingQuarters);
                soul.ChangeFatigue(-1.5);
                if (m != null)
                {
                    m.Energy = 100f;
                    m.Comfort = Math.Min(100f, m.Comfort + 25f);
                }
                sleeping.Add(student);
                soul.AdjustEmotion(Emotion.Serenity, 1, "寝室安寝休眠恢复");
            }
        }

        if (sleeping.Count > 0)
        {
            SimulationLogs.Add($"🛌 【正常就寝】已回到宿舍安歇学员共 {sleeping.Count} 人：{string.Join("、", sleeping.Select(s => s.Name))}");
        }
        if (forcedSleep.Count > 0)
        {
            foreach (var (st, reason) in forcedSleep)
            {
                SimulationLogs.Add($"⚠️ 💤 【强制休眠】学员【{st.Name}】因{reason}。");
            }
        }
        if (awake.Count > 0)
        {
            foreach (var (st, reason) in awake)
            {
                SimulationLogs.Add($"🕯️ ⏳ 【滞夜未归】学员【{st.Name}】因{reason}。");
            }
        }
        else
        {
            SimulationLogs.Add("🌙 今夜学园一切归于深沉寂静，全员均已入眠恢复精力。");
        }
    }

    /// <summary>
    /// 每日夜晚末尾执行真实双周期情感结算。
    /// </summary>
    public void SettleNightlyEmotions(int day)
    {
        SimulationLogs.Add($"\n--- 第 {day} 日 · 真实异构情感双周期结算 ---");
        var sampleChanges = new List<string>();

        foreach (var (prof, student) in Students)
        {
            var soul = student.Mind!;
            var deltas = soul.SettleDailyEmotions(transientDecay: 0.5f, persistentDecay: 0.95f);
            var summary = soul.GetEmotionalSummary(includeSources: false);

            if (deltas.Count > 0)
            {
                var changeStrs = deltas.Select(kv => $"{kv.Key}: {kv.Value.OldVal:F1} -> {kv.Value.NewVal:F1}");
                sampleChanges.Add($"• 【{student.Name}】({prof}): [{string.Join(", ", changeStrs)}] => 现状: [{summary}]");
            }
        }

        SimulationLogs.Add("代表性学员心智情感演化明细（短时情感半衰衰减，长效驻留，疲劳动态结算）：");
        foreach (var line in sampleChanges.Take(8))
        {
            SimulationLogs.Add($"   {line}");
            Trajectory.AddDailyNote(day, $"心理状态变迁: {line}");
        }
        if (sampleChanges.Count > 8)
        {
            SimulationLogs.Add($"   ... 及其余 {sampleChanges.Count - 8} 位学员各不相同的心理状态变迁。");
        }
    }
}
