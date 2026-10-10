using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>
/// 第二章设计数据的唯一读取口：词汇、人物与地点名称、时间、文本。全部来自 chapter_01.story 编译出的
/// generated_chapter_02.json 的 design 子树（同一份文件随程序集嵌入，Godot 宿主读的也是它）。
/// 代码里不再存一份中文名与台词；缺项会在装载时报错，而不是悄悄回退到别处的默认值。
/// </summary>
public static class Chapter2Design
{
    public const string ResourceName = "generated_chapter_02.json";
    private static readonly Lazy<Data> _data = new(Load, true);
    private static Data D => _data.Value;

    /// <summary>嵌入的编译产物（与 Godot 宿主读取的文件相同）。</summary>
    public static string Json => D.Json;
    public static JsonElement Chapter => D.Chapter;
    public static JsonElement Section(string name) => D.Design.TryGetProperty(name, out var value) ? value
        : throw new InvalidDataException("chapter_01.story 缺少设计块：" + name);
    public static bool HasSection(string name) => D.Design.TryGetProperty(name, out _);
    public static JsonElement CaseSection => Section("case");

    // ---------------------------------------------------------------- 名称 --
    public static string PersonName(P p) => D.People[(int)p];
    public static string PlaceName(Place p) => D.Places[(int)p];
    public static string SlotName(Slot s) => D.SlotNames[(int)s];
    public static string GroupName(Group g) => D.GroupNames[(int)g];
    public static string ClusterName(Cluster c) => D.ClusterNames[(int)c];
    public static string BuildFigure(Build b) => D.BuildFigures[(int)b];
    public static string JobText(Job j) => D.JobTexts[(int)j];
    public static string JobFrame(Job j) => D.JobFrames[(int)j];
    public static string FlagText(RF f) => D.FlagTexts.TryGetValue(f, out var text) ? text : f.ToString();

    // ---------------------------------------------------------------- 人物与常数 --
    /// <summary>人物块里的设计数据：群落、值守、工作地点、初始信任（个人对个人的例外）、立绘。</summary>
    public sealed record PersonDesign(Cluster Cluster, bool OnDuty, Place? WorkPlace, IReadOnlyDictionary<P, int> Trust, string Sprite);
    public static PersonDesign PersonData(P p) => D.PersonData[(int)p];
    public static double Const(string name) => D.Design.GetProperty("consts").TryGetProperty(name, out var value) ? value.GetDouble()
        : throw new InvalidDataException("chapter_01.story 缺少常数：" + name);

    public static string EndingText(Ending e)
    {
        foreach (var row in Section("endings").EnumerateArray())
            if (row.GetProperty("id").GetString() == e.ToString()) return row.GetProperty("text").GetString()!;
        throw new InvalidDataException("chapter_01.story 缺少结局：" + e);
    }

    // ---------------------------------------------------------------- 时间 --
    public static int FirstDay => D.Time.GetProperty("days").GetProperty("首日").GetInt32();
    public static int CaseDay => D.Time.GetProperty("days").GetProperty("案发日").GetInt32();
    public static int LastDay => D.Time.GetProperty("days").GetProperty("末日").GetInt32();
    public static string Intro => D.Time.GetProperty("intro").GetString()!;
    public static string Instructions => D.Time.GetProperty("instructions").GetString()!;
    public static string DayName(int day)
    {
        if (D.Time.GetProperty("day_names").TryGetProperty(day.ToString(), out var named)) return named.GetString()!;
        return D.Time.GetProperty("day_other").GetString()!.Replace("{日-1}", (day - 1).ToString()).Replace("{日}", day.ToString());
    }
    public static string SlotDisplay(int day, Slot slot)
    {
        foreach (var alias in D.Time.GetProperty("slot_alias").EnumerateArray())
            if (alias.GetProperty("day").GetInt32() == day && Enum.Parse<Slot>(alias.GetProperty("slot").GetString()!) == slot)
                return alias.GetProperty("name").GetString()!;
        return SlotName(slot);
    }

    // ---------------------------------------------------------------- 文本 --
    /// <summary>取一条文本模板并填入 {名} 形式的占位符。缺键报错。</summary>
    public static string Text(string key, params (string Name, object? Value)[] args)
    {
        var text = RawText(key);
        foreach (var (name, value) in args) text = text.Replace("{" + name + "}", value?.ToString() ?? "");
        return text;
    }
    public static string RawText(string key) => D.TextTable.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
        ? value.GetString()! : throw new InvalidDataException("chapter_01.story 缺少文本：" + key);
    public static IReadOnlyList<string> TextList(string key) => D.TextTable.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Array
        ? [.. value.EnumerateArray().Select(v => v.GetString()!)] : throw new InvalidDataException("chapter_01.story 缺少文本列表：" + key);
    public static bool HasText(string key) => D.TextTable.ContainsKey(key);

    // ---------------------------------------------------------------- 装载 --
    private sealed class Data
    {
        public string Json = "";
        public JsonElement Chapter, Design, Time;
        public string[] People = [], Places = [], SlotNames = [], GroupNames = [], ClusterNames = [], BuildFigures = [], JobTexts = [], JobFrames = [];
        public Dictionary<RF, string> FlagTexts = [];
        public PersonDesign[] PersonData = [];
        public Dictionary<string, JsonElement> TextTable = new(StringComparer.Ordinal);
        public JsonDocument Document = null!;
    }

    private static Data Load()
    {
        using var stream = typeof(Chapter2Design).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException("程序集缺少嵌入的 " + ResourceName + "。");
        using var reader = new StreamReader(stream);
        var data = new Data { Json = reader.ReadToEnd() };
        data.Document = JsonDocument.Parse(data.Json);
        data.Chapter = data.Document.RootElement.GetProperty("chapters")[0];
        data.Design = data.Chapter.TryGetProperty("design", out var design) ? design : throw new InvalidDataException("generated_chapter_02.json 没有 design 子树。");
        data.Time = data.Design.GetProperty("time");
        var vocab = data.Design.GetProperty("vocab");

        data.People = new string[Enum.GetValues<P>().Length];
        data.Places = new string[Enum.GetValues<Place>().Length];
        foreach (var entity in data.Chapter.GetProperty("entities").EnumerateArray())
        {
            var id = entity.GetProperty("id").GetString() ?? "";
            var dot = id.IndexOf('.');
            if (dot < 0) continue;
            var name = entity.GetProperty("name").GetString() ?? "";
            switch (entity.GetProperty("kind").GetString())
            {
                case "character" when id.StartsWith("person.") && Enum.TryParse<P>(id[7..], out var who) && Enum.IsDefined(who) && who.ToString() == id[7..]: data.People[(int)who] = name; break;
                case "room" when id.StartsWith("room.") && Enum.TryParse<Place>(id[5..], out var place) && Enum.IsDefined(place) && place.ToString() == id[5..]: data.Places[(int)place] = name; break;
            }
        }
        Require(data.People, "人物"); Require(data.Places, "地点");

        string[] Table<T>(string kind, string column, Func<string, T> parse, Func<T, int> index) where T : struct, Enum
        {
            var table = new string[Enum.GetValues<T>().Length];
            foreach (var row in vocab.GetProperty(kind).EnumerateArray())
                table[index(parse(row.GetProperty("id").GetString()!))] = column == "word" ? row.GetProperty("word").GetString()! : row.GetProperty(column).GetString()!;
            Require(table, kind);
            return table;
        }
        data.SlotNames = Table("slots", "name", x => Enum.Parse<Slot>(x), s => (int)s);
        data.GroupNames = Table("groups", "word", x => Enum.Parse<Group>(x), g => (int)g);
        data.BuildFigures = Table("builds", "figure", x => Enum.Parse<Build>(x), b => (int)b);
        data.JobTexts = Table("jobs", "text", x => Enum.Parse<Job>(x), j => (int)j);
        data.JobFrames = Table("jobs", "frame", x => Enum.Parse<Job>(x), j => (int)j);
        data.ClusterNames = new string[Enum.GetValues<Cluster>().Length];
        data.ClusterNames[(int)Cluster.None] = "";
        foreach (var row in vocab.GetProperty("clusters").EnumerateArray())
            data.ClusterNames[(int)Enum.Parse<Cluster>(row.GetProperty("id").GetString()!)] = row.GetProperty("word").GetString()!;
        foreach (var row in vocab.GetProperty("flags").EnumerateArray())
            data.FlagTexts[Enum.Parse<RF>(row.GetProperty("id").GetString()!)] = row.GetProperty("text").GetString()!;
        data.PersonData = new PersonDesign[Enum.GetValues<P>().Length];
        var people = data.Design.GetProperty("people");
        foreach (var who in Enum.GetValues<P>())
        {
            var entry = people.GetProperty(who.ToString());
            data.PersonData[(int)who] = new(
                entry.TryGetProperty("cluster", out var cluster) ? Enum.Parse<Cluster>(cluster.GetString()!) : Cluster.None,
                entry.TryGetProperty("on_duty", out var duty) && duty.GetBoolean(),
                entry.TryGetProperty("work_place", out var work) ? Enum.Parse<Place>(work.GetString()!) : null,
                entry.TryGetProperty("trust", out var trust) ? trust.EnumerateObject().ToDictionary(t => Enum.Parse<P>(t.Name), t => t.Value.GetInt32()) : new Dictionary<P, int>(),
                entry.TryGetProperty("sprite", out var sprite) ? sprite.GetString()! : throw new InvalidDataException("人物缺少立绘：" + who));
        }
        if (data.Design.TryGetProperty("text", out var text))
            foreach (var entry in text.EnumerateObject()) data.TextTable[entry.Name] = entry.Value;
        return data;
    }

    private static void Require(string[] table, string what)
    {
        for (var i = 0; i < table.Length; i++)
            if (table[i] is null) throw new InvalidDataException($"chapter_01.story 的{what}缺少第 {i} 项。");
    }
}
