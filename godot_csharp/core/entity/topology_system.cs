using System;
using System.Collections.Generic;
using System.Linq;

namespace DualOntologyWorldEngine.Core;

/// <summary>
/// 场景拓扑与路径搜索系统。
/// </summary>
/// <remarks>
/// 负责在世界树中收集拓扑通道（<see cref="Passage"/>），
/// 并基于通行代价（Cost）与通行许可（Allows）计算跨场景最优路径。
/// 核心不涉及房间内微观坐标与碰撞，仅计算宏观场景拓扑图。
/// </remarks>
public static class TopologySystem
{
    /// <summary>
    /// 沿世界树向上溯源，查找指定成员所属的场景实体（持有 "Scene" 标签）。
    /// </summary>
    /// <param name="member">世界树成员（物品、角色或通道等）。</param>
    /// <returns>所属场景实体；若未处于任何场景下则返回 null。</returns>
    public static Entity? GetEnclosingScene(TreeMember? member)
    {
        var current = member;
        while (current != null)
        {
            if (current is Entity entity && (entity.HasTag("Scene") || entity.HasTag(CommonTag.Scene)))
            {
                return entity;
            }
            current = current.Parent;
        }
        return null;
    }

    /// <summary>
    /// 查找指定成员所在的世界根节点。
    /// </summary>
    public static TreeMember GetRoot(TreeMember member)
    {
        var current = member;
        while (current.Parent != null)
        {
            current = current.Parent;
        }
        return current;
    }

    /// <summary>
    /// 递归收集指定根节点或世界树分支下的所有通道（<see cref="Passage"/>）。
    /// </summary>
    public static List<Passage> CollectPassages(TreeMember root)
    {
        var passages = new List<Passage>();
        var stack = new Stack<TreeMember>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is Passage p && !passages.Contains(p))
            {
                passages.Add(p);
            }

            foreach (var child in node.Children.Keys)
            {
                stack.Push(child);
            }
        }

        return passages;
    }

    /// <summary>
    /// 计算从起始场景到目标场景的最优通道路径（Dijkstra 算法）。
    /// </summary>
    /// <param name="fromScene">出发端场景实体。</param>
    /// <param name="toScene">目的地场景实体。</param>
    /// <param name="passages">可选的可用通道集。若为 null 则自动向上追溯世界树根节点并收集全部通道。</param>
    /// <param name="actor">通行主体（用于门禁与条件检查）。</param>
    /// <param name="env">环境上下文。</param>
    /// <returns>按通行顺序排列的 Passage 列表；若已在目标场景返回空列表；若不可达返回 null。</returns>
    public static IReadOnlyList<Passage>? FindPath(
        Entity fromScene,
        Entity toScene,
        IEnumerable<Passage>? passages = null,
        Entity? actor = null,
        object? env = null)
    {
        if (ReferenceEquals(fromScene, toScene))
        {
            return Array.Empty<Passage>();
        }

        var allPassages = passages != null
            ? passages.ToList()
            : CollectPassages(GetRoot(fromScene));

        // 构建邻接表: 场景 -> (通道, 对端场景)
        var adj = new Dictionary<Entity, List<(Passage Passage, Entity Next)>>();
        foreach (var p in allPassages)
        {
            if (p.Left is Entity l && p.Right is Entity r)
            {
                if (!adj.ContainsKey(l)) adj[l] = new();
                if (!adj.ContainsKey(r)) adj[r] = new();

                adj[l].Add((p, r));
                adj[r].Add((p, l));
            }
        }

        if (!adj.ContainsKey(fromScene) || !adj.ContainsKey(toScene))
        {
            return null;
        }

        var dist = new Dictionary<Entity, double> { [fromScene] = 0 };
        var prev = new Dictionary<Entity, (Entity PrevScene, Passage UsedPassage)>();
        var unvisited = new HashSet<Entity>(adj.Keys);

        while (unvisited.Count > 0)
        {
            // 选出当前距离最小的未访问节点
            Entity? current = null;
            double minDist = double.PositiveInfinity;
            foreach (var node in unvisited)
            {
                if (dist.TryGetValue(node, out var d) && d < minDist)
                {
                    minDist = d;
                    current = node;
                }
            }

            if (current == null || double.IsPositiveInfinity(minDist))
            {
                break; // 剩余节点均不可达
            }

            if (ReferenceEquals(current, toScene))
            {
                // 回溯构建路径
                var path = new List<Passage>();
                var currNode = toScene;
                while (prev.TryGetValue(currNode, out var step))
                {
                    path.Add(step.UsedPassage);
                    currNode = step.PrevScene;
                }
                path.Reverse();
                return path;
            }

            unvisited.Remove(current);

            if (!adj.TryGetValue(current, out var edges)) continue;

            foreach (var (passage, next) in edges)
            {
                if (!unvisited.Contains(next)) continue;

                // 检查通行许可
                if (!passage.Allows(current, actor, env)) continue;

                var cost = passage.CostFrom(current);
                if (double.IsPositiveInfinity(cost) || cost < 0) continue;

                var newDist = minDist + cost;
                if (!dist.TryGetValue(next, out var curDist) || newDist < curDist)
                {
                    dist[next] = newDist;
                    prev[next] = (current, passage);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 解析前往远端目标（场景、物品或角色）的第一步通行决策。
    /// </summary>
    /// <param name="fromScene">主体当前所处场景。</param>
    /// <param name="destination">目标世界对象（场景本身，或位于某场景内的实体）。</param>
    /// <param name="passages">可选通道集合。</param>
    /// <param name="actor">移动主体。</param>
    /// <param name="env">环境上下文。</param>
    /// <returns>第一步要跨越的通道及对端场景；若已在同一场景返回 (null, fromScene)；若不可达返回 (null, null)。</returns>
    public static (Passage? Passage, Entity? NextScene) GetNextStep(
        Entity fromScene,
        TreeMember destination,
        IEnumerable<Passage>? passages = null,
        Entity? actor = null,
        object? env = null)
    {
        var targetScene = (destination is Entity e && (e.HasTag("Scene") || e.HasTag(CommonTag.Scene)))
            ? e
            : GetEnclosingScene(destination);

        if (targetScene == null)
        {
            return (null, null);
        }

        if (ReferenceEquals(fromScene, targetScene))
        {
            return (null, fromScene);
        }

        var path = FindPath(fromScene, targetScene, passages, actor, env);
        if (path == null || path.Count == 0)
        {
            return (null, null);
        }

        var firstPassage = path[0];
        var nextScene = firstPassage.Other(fromScene);
        return (firstPassage, nextScene);
    }
}
