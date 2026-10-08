namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>
/// 转述：知识说给侦探听时，按说话人的认知说出口（Knowledge.Say），来源写成“听某人说”，带成见的转述带上定性。
/// 同一时段里内容相同的转述合并成一句；侦探此刻已经当面看见的事，不再由人转述一遍。只改呈现，不改谁知道什么。
/// </summary>
public sealed partial class Game
{
    private const string RelayMark = "\u0001relay";
    private readonly List<(Person Teller, NewsItem Item, Heard Heard)> _relaysToDet = [];
    /// <summary>已经当面呈现给侦探的知识（旁白直接说过，或有人当面讲过），按知识的同一性记。</summary>
    private readonly HashSet<string> _presentedToDet = [];
    /// <summary>今天已经跟侦探说过“没什么新鲜事”的人：同一天再问，就不再重复那句话。</summary>
    private readonly HashSet<(P, int)> _idleToDet = [];

    private void QueueRelayToDet(Person teller, NewsItem item, Heard heard)
    {
        if (!Feed.Contains(RelayMark)) Feed.Add(RelayMark);
        _relaysToDet.Add((teller, item, heard));
    }

    /// <summary>旁白直接向侦探呈现一条知识（当众指控、张贴之类），之后同一件事的转述不再重复。</summary>
    private string Present(Claim c)
    {
        _presentedToDet.Add(c.Identity);
        return Say(c, null, Det);
    }

    /// <summary>成见带来的定性：说话人自己添的，就是他自己的话；别人添的，说“据某人说”。</summary>
    private string SlantText(NewsItem item, P? slant, Person speaker)
    {
        if (slant is not { } s || item.About is not { } about) return "";
        var key = about == P.Quer ? "走样私取" : "走样惯常";
        if (s == speaker.Id) return X($"知识片段.{key}自述", ("对象", Ref(about, speaker, Det)));
        return X($"知识片段.{key}", ("传话人", Ref(s, speaker, Det)));
    }

    /// <summary>某人把自己知道的一条知识说给侦探：亲历的照说；听来的说明来源；来源本人就是主语时，说成“某人说他……”。</summary>
    private string Hearsay(Person speaker, NewsItem item, Heard h)
    {
        var said = Say(item.Claim, speaker, Det);
        var slant = SlantText(item, h.Slant, speaker);
        if (h.Hops == 0 || h.From == speaker.Id) return said + slant;
        var from = this[h.From];
        var source = Ref(from.Id, speaker, Det);
        if (item.Claim.Subject == from.Id && X("知识." + item.Claim.Predicate).StartsWith("{主语}", StringComparison.Ordinal))
            return X("知识片段.传话人自述", ("来源", source), ("来源他", from == Det ? "" : from.Pro.Pronoun), ("话", Say(item.Claim with { Subject = null }, speaker, Det).TrimStart())) + slant;
        return X("知识片段.听说", ("来源", source), ("话", said)) + slant;
    }

    /// <summary>把攒下的转述合并成几句旁白：同样的事只说一遍，列出都有谁这么说。</summary>
    private IEnumerable<string> RenderRelays()
    {
        // 先按“除主语外内容相同”分组；再把同一个人对同一类事说的几处（story 有“知识合并”句式的）并成一组。
        var groups = new List<List<(Person Teller, NewsItem Item, Heard Heard)>>();
        foreach (var r in _relaysToDet)
        {
            if (_presentedToDet.Contains(r.Item.Claim.Identity)) continue;
            // 可合并的所见（夜里望见身影）按“谁看见的”分组；其余按内容分组。
            string Key(Claim c) => HasMerge(c.Predicate) ? $"{c.Predicate}|{c.Subject}|{c.Night}" : c.Content;
            var g = groups.FirstOrDefault(x => Key(x[0].Item.Claim) == Key(r.Item.Claim));
            if (g is null) groups.Add([r]);
            else if (!g.Any(i => i.Teller == r.Teller && i.Item.Claim.Identity == r.Item.Claim.Identity)) g.Add(r);
        }
        var entries = new List<(List<(Person Teller, NewsItem Item, Heard Heard)> Items, bool Merged)>();
        foreach (var g in groups)
        {
            var c = g[0].Item.Claim;
            // 同一个人（主语）同一夜的几处所见，不论经谁转述，都并成一句，免得同一个人的所见被拆成几条互相打架的话。
            entries.Add((g, HasMerge(c.Predicate)));
        }
        var lines = new List<string>();
        foreach (var (items, merged) in entries)
        {
            foreach (var i in items) _presentedToDet.Add(i.Item.Claim.Identity);
            var tellers = items.Select(i => i.Teller).Distinct().ToList();
            var tellerNames = JoinNames(tellers.Select(t => t.Name));
            var claim = items[0].Item.Claim;
            if (merged)
            {
                // 同一处的几种身形并在一起：“后勤院有一个中等身量的、一个高大的身影”。
                // 同一条知识可能经几个人传来，先按知识去重再数身影。
                var distinct = items.GroupBy(i => i.Item.Claim.Identity).Select(g => g.First()).ToList();
                var places = string.Join("；", distinct.GroupBy(i => i.Item.Claim.Where).Select(at => X($"知识合并.{claim.Predicate}.每处",
                    ("地", at.Key is { } w ? Names.Of(w) : ""),
                    ("各色", string.Join("、", at.GroupBy(i => i.Item.Claim.Value("身形")).Select(b => X("知识片段.身影一种", ("数", Cn(b.Count())), ("身形", b.Key))))))));
                var mergedClaim = claim.With("各处", places);
                var template = $"知识合并.{claim.Predicate}.句";
                // 看见的人自己也在说，就只听他本人的；别人只是在复述他。
                if (claim.Subject is { } seer && tellers.Any(t => t.Id == seer)) tellers = [this[seer]];
                lines.Add(tellers.Count == 1
                    ? $"{tellers[0].Name}说：“{Stop(Say(mergedClaim, tellers[0], Det, template: template))}”"
                    : X("知识片段.都说", ("人们", tellerNames), ("话", Stop(Say(mergedClaim, null, Det, template: template)))));
                continue;
            }
            if (items.Count == 1)
            {
                var (t, item, heard) = items[0];
                var own = t.News.TryGetValue(item.Id, out var h) ? h : heard;
                lines.Add($"{t.Name}说：“{Stop(Hearsay(t, item, own))}”");
                continue;
            }
            if (items.All(i => i.Item.Claim.Subject == i.Teller.Id))
                lines.Add(X("知识片段.同样的话", ("人们", tellerNames), ("话", Stop(Say(claim, tellers[0], Det)))));
            else
            {
                var subjects = items.Where(i => i.Item.Claim.Subject is not null).Select(i => i.Item.Claim.Subject!.Value).Distinct().ToList();
                var said = Say(claim, tellers.Count == 1 ? tellers[0] : null, Det, subjects: subjects);
                lines.Add(tellers.Count > 1 ? X("知识片段.都说", ("人们", tellerNames), ("话", Stop(said))) : $"{tellerNames}说：“{Stop(said)}”");
            }
        }
        _relaysToDet.Clear();
        return lines;
    }

    private static bool HasMerge(string predicate) => Chapter2Design.HasText($"知识合并.{predicate}.句");

    private List<string> ComposeFeed()
    {
        var lines = new List<string>();
        foreach (var line in Feed)
        {
            if (line != RelayMark) { lines.Add(line); continue; }
            lines.AddRange(RenderRelays());
        }
        _relaysToDet.Clear();
        return lines;
    }
}
