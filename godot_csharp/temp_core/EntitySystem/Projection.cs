using DualOntologyWorldEngine.TempCore;

namespace DualOntologyWorldEngine.Core;
/// <summary>
/// Projection反映了一个物体在特定位置下的完全表现形式。
/// 角色在观察时，获取到的信息则是Func(Observer.Mind, Projection) => Result。
/// Projection替代原有Tree结构，成为了新的场景树的节点。
/// 在言灵和规则系统中应当，且必须使用Projection作为实体。
/// Projection不仅是物理世界实体，还是每个人脑子中认知到的实体。
/// </summary>
/// <summary>
/// 表示投影的类，用于构建投影层次结构
/// </summary>
public class Projection
{
    /// <summary>
    /// 获取投影的名称
    /// </summary>
    public string Name { get; }
    public Essence Source { get; }
    public Projection? Parent { get; }
    public HashSet<Projection> Children { get; }
    public bool UpdateByObs = true;

}
public class BoundarySurfaceProjection : Projection
{
}
