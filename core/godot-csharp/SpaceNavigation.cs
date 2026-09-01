#nullable enable

namespace DualOntologyEngine.Core;

public sealed class WorldSpace(string name) : Entity(name, null, [])
{
	private readonly Dictionary<Entity, Dictionary<Entity, float>> links = [];

	public void clear_connections() => links.Clear();

	public void connect(Entity from, Entity to, float cost, bool bothWays = true)
	{
		if (cost <= 0)
			throw new ArgumentOutOfRangeException(nameof(cost), "空间边的行动代价必须大于零。");
		links.TryAdd(from, []);
		links.TryAdd(to, []);
		links[from][to] = cost;
		if (bothWays)
			links[to][from] = cost;
	}

	public Route? find_route(Entity from, Entity to)
	{
		if (from == to)
			return new([from], 0);
		if (!links.ContainsKey(from) || !links.ContainsKey(to))
			return null;

		Dictionary<Entity, float> distance = links.Keys.ToDictionary(node => node, _ => float.PositiveInfinity);
		Dictionary<Entity, Entity> previous = [];
		HashSet<Entity> remaining = [.. links.Keys];
		distance[from] = 0;

		while (remaining.Count > 0)
		{
			var current = remaining.MinBy(node => distance[node])!;
			remaining.Remove(current);
			if (float.IsPositiveInfinity(distance[current]))
				break;
			if (current == to)
				break;

			foreach (var (neighbor, edgeCost) in links[current])
			{
				if (!remaining.Contains(neighbor))
					continue;
				var candidate = distance[current] + edgeCost;
				if (candidate >= distance[neighbor])
					continue;
				distance[neighbor] = candidate;
				previous[neighbor] = current;
			}
		}

		if (float.IsPositiveInfinity(distance[to]))
			return null;
		List<Entity> nodes = [to];
		while (nodes[^1] != from)
			nodes.Add(previous[nodes[^1]]);
		nodes.Reverse();
		return new(nodes, distance[to]);
	}
}

public sealed record Route(IReadOnlyList<Entity> Nodes, float Cost)
{
	public string describe() => string.Join(" → ", Nodes.Select(node => node.name));
}
