using Godot;

namespace DualOntologyWorldEngine.Character;

public enum FacingMode { Front, Side, Back }

/// <summary>各分件使用原始角色坐标，避免父子变换叠加及左右肢体重新排序。</summary>
public partial class DstCharacterVisual : Node2D
{
    public FacingMode CurrentFacing { get; private set; } = FacingMode.Front;
    public bool IsFacingLeft { get; private set; }
    public int CurrentFaceIndex { get; private set; }
    private int _poseIndex;

    [Export]
    public int PoseIndex
    {
        get => _poseIndex;
        set { _poseIndex = value; QueueRedraw(); }
    }

    public void SetFacingMode(FacingMode mode, bool faceLeft)
    {
        CurrentFacing = mode;
        IsFacingLeft = mode == FacingMode.Side && faceLeft;
        Scale = new Vector2(IsFacingLeft ? -1 : 1, 1);
        QueueRedraw();
    }

    public void SetFaceIndex(int index)
    {
        CurrentFaceIndex = Mathf.Clamp(index, 0, 3);
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var part in DstAnimationBank.Frames[PoseIndex].EnumerateArray())
        {
            string path = part.GetProperty("texture").GetString()!;
            var r = part.GetProperty("rect");
            if (part.GetProperty("face").GetBoolean() && CurrentFaceIndex != 0)
            {
                // 待机也会转头，表情跟随当前帧的脸部视角。
                int sourceFace = int.Parse(path[10..^4]);
                int expression = CurrentFaceIndex switch { 1 => 2, 2 => 1, _ => 3 };
                int face = (sourceFace is >= 4 and <= 7 ? 4 : 0) + expression;
                path = $"face/face-{face}.png";
                r = DstAnimationBank.FaceRects.GetProperty(face.ToString());
            }
            var texture = DstSpriteLoader.LoadTexture(path);
            if (texture == null) continue;
            var m = part.GetProperty("matrix");
            // ANIM 的 a,b 与 c,d 分别是局部 X、Y 轴，直接作为 Godot 的基向量。
            DrawSetTransformMatrix(new Transform2D(
                new Vector2(m[0].GetSingle(), m[1].GetSingle()),
                new Vector2(m[2].GetSingle(), m[3].GetSingle()),
                new Vector2(m[4].GetSingle(), m[5].GetSingle())));
            DrawTextureRect(texture, new Rect2(r[0].GetSingle(), r[1].GetSingle(),
                r[2].GetSingle(), r[3].GetSingle()), false);
        }
        DrawSetTransformMatrix(Transform2D.Identity);
    }
}
