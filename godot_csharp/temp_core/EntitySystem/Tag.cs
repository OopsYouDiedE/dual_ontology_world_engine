namespace DualOntologyWorldEngine.TempCore;

/// <summary>
/// 强类型数值标签，替代 enum 保证运行期扩展性与值比较语义。
/// </summary>
public readonly record struct Tag(string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// 强类型语义关系标签，替代魔术字符串保证语义类型安全与可扩展性。
/// </summary>
public readonly record struct RelationTag(string Name)
{
    public override string ToString() => Name;
    public static implicit operator string(RelationTag tag) => tag.Name;
    public static implicit operator RelationTag(string name) => new(name);
}

public static class StandardRelations
{
    public static readonly RelationTag Controls = new("Controls");
    public static readonly RelationTag HasIdentity = new("HasIdentity");
    public static readonly RelationTag AcceptsKey = new("AcceptsKey");
    public static readonly RelationTag AttachedTo = new("AttachedTo");
    public static readonly RelationTag IsA = new("IsA");
}


public static class LockTag
{
    /// <summary>锁破解难度等级</summary>
    public static readonly Tag LockLevel = new("LockLevel");
    /// <summary>是否处于锁定状态（>0 为锁定）</summary>
    public static readonly Tag Locked = new("Locked");
    /// <summary>是否支持钥匙开启</summary>
    public static readonly Tag KeyOpenable = new("KeyOpenable");
    public static readonly Tag AutoRelock = new("AutoRelock");
    public static readonly Tag Opening = new("Opening");
}

public static class MindTag
{
    /// <summary>理智/稳定度</summary>
    public static readonly Tag Sanity = new("Sanity");
    /// <summary>恐惧度</summary>
    public static readonly Tag Fear = new("Fear");
    /// <summary>侵蚀/污染度</summary>
    public static readonly Tag Corruption = new("Corruption");
    /// <summary>注意力</summary>
    public static readonly Tag Attention = new("Attention");
    /// <summary>认知置信度</summary>
    public static readonly Tag Confidence = new("Confidence");
}

public static class FieldTag
{
    /// <summary>场域侵蚀强度</summary>
    public static readonly Tag CorruptionIntensity = new("CorruptionIntensity");
    /// <summary>合法/有效性标记</summary>
    public static readonly Tag Valid = new("Valid");
}

public static class SurfaceTag
{
    /// <summary>刻痕线索存在标记</summary>
    public static readonly Tag HasSymbol = new("HasSymbol");
    /// <summary>档案室特定符号线索</summary>
    public static readonly Tag ArchiveSymbol = new("ArchiveSymbol");
}
