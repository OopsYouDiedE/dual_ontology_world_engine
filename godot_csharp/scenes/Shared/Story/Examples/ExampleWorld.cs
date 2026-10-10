using DualOntologyWorldEngine.Story;

/// <summary>示例只加载正式 .story 编译结果，声明、候选与文字均由作者源提供。</summary>
public sealed class ExampleWorld
{
    public IReadOnlyList<ChapterWorldDefinition> Chapters { get; }

    public ExampleWorld(string compiledDocument)
    {
        Chapters = WorldScriptDocument.Parse(compiledDocument);
        if (Chapters.Count == 0) throw new InvalidDataException("The example document contains no chapters.");
    }

    public static ExampleWorld Load(string path) => new(File.ReadAllText(path));

    public WorldSession CreateSession()
    {
        var session = new WorldSession();
        try
        {
            session.Load(Chapters[0]);
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }
}
