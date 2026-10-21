internal sealed record TopologyNode(int RoomIndex, int X, int Y, int Width, int Height);
internal sealed record TopologyLink(int EdgeIndex, IReadOnlyList<(int X, int Y)> Points);
internal sealed record TopologyPlan(int Width, int Height, int NodeWidth,
    IReadOnlyList<TopologyNode> Nodes, IReadOnlyList<TopologyLink> Links, IReadOnlyList<string> Notes);

/// <summary>Deterministic observer-only layout; coordinates are terminal cells, not world geometry.</summary>
internal static class TerminalTopology
{
    internal static TopologyPlan Build(TerminalModel model, int width, int heightBudget, string zoom = "fit")
    {
        const int cardHeight = 6, horizontalGap = 5, verticalGap = 4, margin = 2;
        // Preserve the minimum card width even when the caller supplies an unusably small viewport.
        width = Math.Max(14, width);
        heightBudget = Math.Max(1, heightBudget);
        var notes = new List<string>();
        var count = model.Rooms.Count;
        var mode = (zoom ?? "fit").ToLowerInvariant();
        if (mode is not ("fit" or "detail" or "compact"))
        {
            notes.Add($"未知缩放 {zoom}；使用自适应。");
            mode = "fit";
        }
        var nodeWidth = mode == "compact" ? 14 : 26;
        if (mode == "fit")
        {
            // Prefer readable cards, then reduce width when more columns avoid vertical overflow.
            for (var candidate = 26; candidate >= 14; candidate--)
            {
                nodeWidth = candidate;
                var cols = Math.Max(1, (width - margin * 2 + horizontalGap) / (candidate + horizontalGap));
                var rows = (count + cols - 1) / cols;
                if (rows * (cardHeight + verticalGap) - verticalGap + margin * 2 <= heightBudget) break;
            }
        }
        nodeWidth = Math.Max(10, Math.Min(nodeWidth, width - margin * 2));
        var columns = Math.Max(1, (width - margin * 2 + horizontalGap) / (nodeWidth + horizontalGap));
        columns = Math.Min(Math.Max(1, count), columns);
        notes.Add($"缩放：{mode} · 房间卡片 {nodeWidth}×{cardHeight} · 每行最多 {columns} 间；连线仅表示已记录连接，非实际平面距离。");

        var ids = model.Rooms.Select((room, index) => (room.Id, index))
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(item => item.index).ToArray(), StringComparer.Ordinal);
        var adjacent = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
        var endpoints = new (int From, int To)?[model.Edges.Count];
        for (var i = 0; i < model.Edges.Count; i++)
        {
            var edge = model.Edges[i];
            if (!ids.TryGetValue(edge.From, out var from) || from.Length != 1 ||
                !ids.TryGetValue(edge.To, out var to) || to.Length != 1)
            {
                notes.Add($"连接 {i + 1}：{Endpoint(edge.From)} → {Endpoint(edge.To)}（{edge.State}）；端点缺失或标识重复，未绘制。");
                continue;
            }
            endpoints[i] = (from[0], to[0]);
            if (from[0] == to[0]) continue;
            adjacent[from[0]].Add(to[0]);
            adjacent[to[0]].Add(from[0]);
        }

        // Breadth-first order is rooted in input order, never in the current actor's room.
        // Components occupy separate rows; the serpentine fold keeps long chains compact.
        var visited = new bool[count];
        var nodes = new List<TopologyNode>();
        var row = 0;
        var components = 0;
        for (var root = 0; root < count; root++)
        {
            if (visited[root]) continue;
            components++;
            var order = new List<int>();
            var queue = new Queue<int>();
            queue.Enqueue(root);
            visited[root] = true;
            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                order.Add(index);
                foreach (var next in adjacent[index].Distinct().OrderBy(index => index))
                {
                    if (visited[next]) continue;
                    visited[next] = true;
                    queue.Enqueue(next);
                }
            }
            for (var i = 0; i < order.Count; i++)
            {
                var localRow = i / columns;
                var column = i % columns;
                if (localRow % 2 == 1) column = columns - 1 - column;
                nodes.Add(new(order[i], margin + column * (nodeWidth + horizontalGap),
                    margin + (row + localRow) * (cardHeight + verticalGap), nodeWidth, cardHeight));
            }
            row += (order.Count + columns - 1) / columns;
        }
        nodes.Sort((a, b) => a.RoomIndex.CompareTo(b.RoomIndex));
        var height = count == 0 ? margin * 2 : row * (cardHeight + verticalGap) - verticalGap + margin * 2;
        if (height > heightBudget) notes.Add($"结构图需要 {height} 行，超过可用 {heightBudget} 行；保留全部房间并向下展开。");
        if (components > 1) notes.Add($"已记录拓扑包含 {components} 个互不相连的部分。");
        var blocked = new bool[height, width];
        foreach (var node in nodes)
            for (var y = node.Y; y < node.Y + node.Height; y++)
                for (var x = node.X; x < node.X + node.Width; x++) blocked[y, x] = true;

        var links = new List<TopologyLink>();
        for (var i = 0; i < endpoints.Length; i++)
        {
            if (endpoints[i] is not { } ends) continue;
            var edge = model.Edges[i];
            var path = ends.From == ends.To ? null : Route(nodes[ends.From], nodes[ends.To], blocked, width, height);
            if (path is null)
            {
                notes.Add($"连接 {i + 1}：{Endpoint(edge.From)} → {Endpoint(edge.To)}（{edge.State}）；{(ends.From == ends.To ? "自身连接" : "无独立安全线道")}，以此端点说明保留。");
                continue;
            }
            foreach (var point in path) blocked[point.Y, point.X] = true;
            links.Add(new(i, Corners(path)));
        }
        return new(width, height, nodeWidth, nodes, links, notes);

        string Endpoint(string id) => ids.TryGetValue(id, out var matches) && matches.Length == 1
            ? $"{model.Rooms[matches[0]].Title} [{id}]" : $"[{id}]";
    }

    private static List<(int X, int Y)>? Route(TopologyNode from, TopologyNode to,
        bool[,] blocked, int width, int height)
    {
        bool Free((int X, int Y) p) => p.X >= 0 && p.X < width && p.Y >= 0 && p.Y < height && !blocked[p.Y, p.X];
        var targets = Ports(to).Where(Free).ToHashSet();
        var previous = new Dictionary<(int X, int Y), (int X, int Y)>();
        var queue = new Queue<(int X, int Y)>();
        foreach (var start in Ports(from).Where(Free).OrderBy(p => Math.Abs(p.X - to.X) + Math.Abs(p.Y - to.Y)))
        {
            if (!previous.TryAdd(start, start)) continue;
            queue.Enqueue(start);
        }
        while (queue.Count > 0)
        {
            var point = queue.Dequeue();
            if (targets.Contains(point))
            {
                var path = new List<(int X, int Y)> { point };
                while (previous[point] != point)
                {
                    point = previous[point];
                    path.Add(point);
                }
                path.Reverse();
                return path;
            }
            foreach (var next in new[] { (point.X + 1, point.Y), (point.X, point.Y + 1), (point.X - 1, point.Y), (point.X, point.Y - 1) })
            {
                if (!Free(next) || !previous.TryAdd(next, point)) continue;
                queue.Enqueue(next);
            }
        }
        return null;
    }

    private static IEnumerable<(int X, int Y)> Ports(TopologyNode node)
    {
        for (var x = node.X + 1; x < node.X + node.Width - 1; x++)
        {
            yield return (x, node.Y - 1);
            yield return (x, node.Y + node.Height);
        }
        for (var y = node.Y + 1; y < node.Y + node.Height - 1; y++)
        {
            yield return (node.X - 1, y);
            yield return (node.X + node.Width, y);
        }
    }

    private static IReadOnlyList<(int X, int Y)> Corners(List<(int X, int Y)> path)
    {
        var result = new List<(int X, int Y)> { path[0] };
        for (var i = 1; i < path.Count - 1; i++)
            if ((path[i].X - path[i - 1].X, path[i].Y - path[i - 1].Y) !=
                (path[i + 1].X - path[i].X, path[i + 1].Y - path[i].Y)) result.Add(path[i]);
        if (path.Count > 1) result.Add(path[^1]);
        return result;
    }
}
