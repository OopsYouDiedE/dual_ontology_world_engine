using System.Collections.Immutable;
namespace DualOntologyWorldEngine.Core;

/// <summary>短期状态覆盖。基础值继续由普通效果修改；最后生效的覆盖优先，到期显露下层。</summary>
public sealed record StateLease(Tag Tag, int Value, double ExpiresAt);
public sealed class TemporaryStateProperty : Property
{
    internal List<StateLease> Layers { get; } = [];
    public IReadOnlyList<StateLease> Active => Layers.AsReadOnly();
    public int? Override(Tag tag) => Layers.LastOrDefault(l=>l.Tag==tag)?.Value;
    internal IEnumerable<KeyValuePair<Tag,int>> Values => Layers.GroupBy(l=>l.Tag).Select(g=>new KeyValuePair<Tag,int>(g.Key,g.Last().Value));
}
