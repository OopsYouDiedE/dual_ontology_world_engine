namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed partial class Game
{
    /// <summary>取剧本里的文本模板（chapter_01.story 的“文本”块），并填入 {名} 占位符。</summary>
    private static string X(string key, params (string Name, object? Value)[] args) => Chapter2Design.Text(key, args);
}
