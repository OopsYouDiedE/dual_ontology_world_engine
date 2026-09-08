namespace DualOntologyWorldEngine.TempCore;
/// <summary>
/// 这个方法是用来替代enum实现可拓展性的。
/// </summary>
/// <param name="Name"></param>
public readonly record struct Tag(string Name);


public static class LockTag
{
    /// <summary>锁本身破解难度 </summary>
    public static readonly Tag LockLevel = new("LockLevel");
    /// <summary>是否处于上锁状态 </summary>
    public static readonly Tag Locked = new("Locked");
    /// <summary>是否能被锁等级匹配，且真名对应的钥匙开启 </summary>
    public static readonly Tag KeyOpenable = new("KeyOpenable");
    public static readonly Tag AutoRelock = new("AutoRelock");
    public static readonly Tag Opening = new("Opening");

}
