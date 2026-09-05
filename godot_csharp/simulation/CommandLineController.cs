using System;
using System.Collections.Generic;
using System.Linq;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;

namespace DualOntologyWorldEngine.Simulation;

/// <summary>
/// 命令行交互控制器（参照模拟人生 3/4 操控机制）。
/// 允许玩家接管 16 位学员中的任何一人或恶魔，实时查看需求仪表盘与环境，并以菜单方式介入决策。
/// </summary>
public sealed class CommandLineController
{
    /// <summary>
    /// 是否开启交互式命令行输入。若为 false 则所有决策全由 AI 自由意志（Autonomy）自动执行。
    /// </summary>
    public bool IsInteractive { get; set; } = false;

    /// <summary>
    /// 当前受玩家直接接管控制的角色（Active Sim）。为 null 时表示全员自由意志。
    /// </summary>
    public Character? ControlledCharacter { get; set; } = null;

    /// <summary>
    /// 学园全部角色引用列表，供玩家在推演中随时切换受控目标。
    /// </summary>
    public Dictionary<string, Character> AllCharacters { get; } = new();

    /// <summary>
    /// 检查指定角色是否当前由玩家接管。
    /// </summary>
    public bool IsControlled(Character character)
    {
        return ControlledCharacter != null && ReferenceEquals(ControlledCharacter, character);
    }

    /// <summary>
    /// 注册全部可选角色供随时切换。
    /// </summary>
    public void RegisterCharacters(IEnumerable<Character> characters)
    {
        foreach (var c in characters)
        {
            AllCharacters[c.Name] = c;
        }
    }

    /// <summary>
    /// 交互决选委托。作为 chooser 注入 Soul.Step。
    /// </summary>
    public Intent? InteractiveChoose(
        Character actor,
        IReadOnlyList<Decision> decisions,
        SimsMotiveProfile motives,
        double currentTime,
        int currentDay)
    {
        if (decisions.Count == 0) return null;

        if (!IsInteractive || !IsControlled(actor))
        {
            // 自由意志：根据效用评分（已内嵌动机赤字加权）自动决选
            return decisions[0].Action;
        }

        while (true)
        {
            RenderCharacterDashboard(actor, motives, currentTime, currentDay);
            RenderSceneContext(actor);

            Console.WriteLine("\n【模拟人生 · 动作指令菜单】（输入数字执行动作，或输入命令）：");
            for (int i = 0; i < decisions.Count; i++)
            {
                var d = decisions[i];
                var action = d.Action;
                if (action == null) continue;
                var adScore = motives.CalculateAdvertisingUtility(action);
                Console.WriteLine($"  [{i + 1}] {action.Label}  (耗费: {action.MotionCost:F0} AV | 动机效用: +{adScore:F1})");
            }

            Console.WriteLine("--------------------------------------------------------------------------");
            Console.WriteLine("  [S] 交给自由意志（Sims Autonomy: 本回合由动机 AI 自动做最优决定）");
            Console.WriteLine("  [C] 切换接管角色（Switch Active Character）");
            Console.WriteLine("  [A] 放弃接管，转为全员自由意志自动推演（Release Control）");
            Console.Write("\n请输入指令 > ");

            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                return decisions[0].Action;
            }

            if (input.Equals("S", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"⚡ 【{actor.Name}】听凭自由意志驱动...");
                return decisions[0].Action;
            }

            if (input.Equals("A", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("🔓 已释放操控权，学园全员转为自由意志自主推演。");
                ControlledCharacter = null;
                return decisions[0].Action;
            }

            if (input.Equals("C", StringComparison.OrdinalIgnoreCase))
            {
                SwitchCharacterPrompt();
                if (!IsControlled(actor))
                {
                    return decisions[0].Action;
                }
                continue;
            }

            if (int.TryParse(input, out int choiceIndex) && choiceIndex >= 1 && choiceIndex <= decisions.Count)
            {
                var chosen = decisions[choiceIndex - 1].Action;
                if (chosen != null)
                {
                    Console.WriteLine($"🎯 玩家指令下达：【{actor.Name}】-> {chosen.Label}！");
                    return chosen;
                }
            }

            Console.WriteLine("❌ 无效的输入，请重新选择。");
        }
    }

    private void RenderCharacterDashboard(Character actor, SimsMotiveProfile m, double currentTime, int currentDay)
    {
        Console.WriteLine("\n==========================================================================");
        Console.WriteLine($" 🎮 玩家操控接管中：【{actor.Name}】 | 第 {currentDay} 日 (当前时间: {currentTime:F1} AV)");
        Console.WriteLine("==========================================================================");
        Console.WriteLine($"所在场景：【{actor.CurrentScene?.Name ?? "未知"}】  | 心绪状态：[{actor.Mind?.GetEmotionalSummary(false)}]");
        Console.WriteLine("【模拟人生 5 大需求动机仪表盘】");
        Console.WriteLine($"  饱腹感 (Hunger) : {RenderProgressBar(m.Hunger)}  {m.Hunger,5:F1}/100");
        Console.WriteLine($"  精力值 (Energy) : {RenderProgressBar(m.Energy)}  {m.Energy,5:F1}/100");
        Console.WriteLine($"  社交度 (Social) : {RenderProgressBar(m.Social)}  {m.Social,5:F1}/100");
        Console.WriteLine($"  娱乐值 (Fun)    : {RenderProgressBar(m.Fun)}  {m.Fun,5:F1}/100");
        Console.WriteLine($"  舒适度 (Comfort): {RenderProgressBar(m.Comfort)}  {m.Comfort,5:F1}/100");
    }

    private void RenderSceneContext(Character actor)
    {
        var scene = actor.CurrentScene;
        if (scene == null) return;

        var others = scene.Children.Keys.OfType<Character>()
            .Where(c => !ReferenceEquals(c, actor))
            .Select(c => c.Name)
            .ToList();

        var items = scene.Children.Keys.OfType<Entity>()
            .Where(e => !(e is Character) && !e.HasTag(CommonTag.Soul) && !e.HasTag("Soul"))
            .Select(e => e.Name)
            .Take(6)
            .ToList();

        Console.WriteLine("\n【当前场景观察】");
        Console.WriteLine($"  同在人员：{(others.Count > 0 ? string.Join("、", others) : "（四下无人）")}");
        Console.WriteLine($"  在场设施与物品：{(items.Count > 0 ? string.Join("、", items) : "（空无一物）")}");
    }

    private void SwitchCharacterPrompt()
    {
        Console.WriteLine("\n可供接管的角色清单：");
        var list = AllCharacters.Values.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            Console.WriteLine($"  [{i + 1}] {list[i].Name} (当前位于: {list[i].CurrentScene?.Name})");
        }
        Console.Write("请输入要接管的角色编号（0 表示取消） > ");
        var input = Console.ReadLine()?.Trim();
        if (int.TryParse(input, out int idx) && idx >= 1 && idx <= list.Count)
        {
            ControlledCharacter = list[idx - 1];
            Console.WriteLine($"✅ 已成功将操控权切换至：【{ControlledCharacter.Name}】！");
        }
    }

    private static string RenderProgressBar(float val)
    {
        int filled = Math.Clamp((int)Math.Round(val / 10.0), 0, 10);
        int empty = 10 - filled;
        return "[" + new string('■', filled) + new string('□', empty) + "]";
    }
}
