using System.Reflection;
using DualOntologyWorldEngine.Gameplay;
using DualOntologyWorldEngine.Story;
using Godot;
using GArray = Godot.Collections.Array;

/// <summary>仅供自动检查：访问统一宿主持有的当前章节探针，不给正式输入增加注入接口。</summary>
public partial class ChapterProbe : RefCounted
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;

    public Variant Run(Node engine, string name, GArray args)
    {
        var game = typeof(GameEngine).GetField("_session", Instance)?.GetValue(engine) as GameSession
            ?? throw new InvalidOperationException("Regression probe requires a started Main host.");
        var chapter = typeof(GameSession).GetField("_session", Instance)?.GetValue(game) as IChapterProbe
            ?? throw new InvalidOperationException("Current chapter has no regression probe.");
        // 与正式宿主使用同一套 Variant 转换；反射边界只留在检查目录。
        var from = typeof(GameEngine).GetMethod("FromGodot", Static)
            ?? throw new MissingMethodException(nameof(GameEngine), "FromGodot");
        var to = typeof(GameEngine).GetMethod("ToGodot", Static)
            ?? throw new MissingMethodException(nameof(GameEngine), "ToGodot");
        var result = chapter.Probe(name, args.Select(value => from.Invoke(null, [value])).ToArray());
        return (Variant)to.Invoke(null, [result])!;
    }
}
