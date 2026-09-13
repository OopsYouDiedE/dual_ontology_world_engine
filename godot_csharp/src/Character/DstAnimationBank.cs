using Godot;
using System.Text.Json;

namespace DualOntologyWorldEngine.Character;

/// <summary>完整分件姿态的离散帧动画，保留贴图、仿射变换与绘制顺序。</summary>
public static class DstAnimationBank
{
    public const string IdleFront = "idle_front", WalkFront = "walk_front", RunFront = "run_front", ActionFront = "action_front";
    public const string IdleSide = "idle_side", WalkSide = "walk_side", RunSide = "run_side", ActionSide = "action_side";
    public const string IdleBack = "idle_back", WalkBack = "walk_back", RunBack = "run_back", ActionBack = "action_back";

    private static readonly JsonDocument Poses = JsonDocument.Parse(
        Godot.FileAccess.GetFileAsString("res://ShareAsset/extracted_wilson_anim/wilson_poses.json"));
    public static readonly JsonElement FaceRects = Poses.RootElement.GetProperty("faces");
    public static readonly JsonElement[] Frames = Poses.RootElement.GetProperty("clips").EnumerateArray()
        .SelectMany(clip => clip.GetProperty("frames").EnumerateArray()).ToArray();

    public static AnimationLibrary CreateLibrary()
    {
        var library = new AnimationLibrary();
        int firstFrame = 0;
        foreach (var clip in Poses.RootElement.GetProperty("clips").EnumerateArray())
        {
            float length = clip.GetProperty("length").GetSingle();
            int count = clip.GetProperty("frames").GetArrayLength();
            var animation = new Animation
            {
                Length = length,
                LoopMode = clip.GetProperty("loop").GetBoolean()
                    ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None
            };
            int track = animation.AddTrack(Animation.TrackType.Value);
            animation.TrackSetPath(track, "Visual:PoseIndex");
            animation.ValueTrackSetUpdateMode(track, Animation.UpdateMode.Discrete);
            for (int i = 0; i < count; i++)
                animation.TrackInsertKey(track, i * (double)length / count, firstFrame + i);
            library.AddAnimation(clip.GetProperty("name").GetString()!, animation);
            firstFrame += count;
        }
        return library;
    }
}
