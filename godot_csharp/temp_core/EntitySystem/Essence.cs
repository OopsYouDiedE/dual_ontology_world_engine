public enum EssenceTag
{


}
public enum EssenceRelationTag
{


}

/// <summary>
/// Essence反映了一个物体的本质，它并不直接表现在场景中。
/// </summary>
public class Essence
{

    /// <summary>
    /// 它记录了一个物体和其他物体之间的关系。这个关系对于Essence的特定属性是有意义和必要的。
    /// </summary>
    public HashSet<(Essence, EssenceRelationTag)> Relations { get; private set; } = new();
    public HashSet<EssenceProperty> Properties { get; private set; } = new();
    public Dictionary<String, double> Tags { get; private set; } = new();


}
/// <summary>
/// 它象征着对两个空间起到改变其通行状态的作用。
/// 当
/// </summary>
public class BoundaryEssence : Essence
{
    public HashSet<(SpaceEssence, SpaceEssence)> Connections = new();
    /// <summary>
    /// 它规定了从一个空间进去，必须从唯一一个空间中出来的映射。
    /// </summary>
    public Dictionary<SpaceEssence, SpaceEssence> CurrentPassages = new();
    /// <summary>
    /// 所有可用连接开闭状态一致。
    /// </summary>
    public bool IsOpened = true;


}
public class SpaceEssence : Essence
{
    public HashSet<Essence> ContainedPhysicalEssences { get; private set; } = new();

}


public class EssenceProperty
{

}
public class Lock
{

}
