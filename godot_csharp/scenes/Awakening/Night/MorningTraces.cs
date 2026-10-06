using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Awakening;

public enum Sole { Soft, PoloBoot, WorkBoot }
public enum PrintToward { Bolt, DoorGap }
public sealed record BootPrint(Sole Sole, PrintToward Toward);

/// <summary>
/// 一次重演结束时，昨夜留下、到清晨仍可检视的痕迹。全部从重演世界的 Essence 读出。
/// 编码与证物卡一一对应：门(E1)、灰扇与木楔(E2)、靴印(E3)、湿布(E4)、音叉(E5)、乐谱(E6)、石板(E7)、杯子(E9)。
/// </summary>
public sealed record MorningTraces(
    bool InnerBoltDown,
    bool AshFan,
    bool WedgeTopSooted,
    bool WedgeInCorner,
    ImmutableArray<BootPrint> Prints,
    int Cloth,            // 0 无；1 沿闭合的门底压成直线；2 塞在敞开的门缝里
    bool ClothOverHeel,
    bool ForkByFrame,
    bool FrameDent,
    int ScoreLine1,       // 0 无；1 两拍弱音+“开门”记两遍、旁注“门外？”；2 记“救我”；3 记整句“开门，救我”
    bool ScoreLine2,      // “他们关了门。”
    int SlateLine1,       // 0 无；1 G：⊥门 ✕开 ◎；2 G：开门；3 ⊥门 ✕开（无 ◎）
    int SlateLine2,       // 0 无；1 放大镜小图 + ⊥✓；2 认不出是谁的记号 + ⊥✓
    int CupSugar)         // 0 没有泡茶；1 按人放糖；2 四杯一样
{
    /// <summary>今晨的现实（证物卡所写的事实）。来自 chapter_01.story 的“真相”；不由重演生成，穷举检查要求只有一组变量能重演出它。</summary>
    public static MorningTraces Evidence => Truth.Value;

    private static readonly Lazy<MorningTraces> Truth = new(() =>
    {
        var t = Chapter1Data.Root.GetProperty("TRUTH");
        bool B(string key) => t.GetProperty(key).GetBoolean();
        int N(string key) => t.GetProperty(key).GetInt32();
        return new(
            InnerBoltDown: B("InnerBoltDown"), AshFan: B("AshFan"), WedgeTopSooted: B("WedgeTopSooted"), WedgeInCorner: B("WedgeInCorner"),
            Prints: [..t.GetProperty("Prints").EnumerateArray().Select(p => new BootPrint(
                Enum.Parse<Sole>(p.GetProperty("sole").GetString()!), Enum.Parse<PrintToward>(p.GetProperty("toward").GetString()!)))],
            Cloth: N("Cloth"), ClothOverHeel: B("ClothOverHeel"), ForkByFrame: B("ForkByFrame"), FrameDent: B("FrameDent"),
            ScoreLine1: N("ScoreLine1"), ScoreLine2: B("ScoreLine2"), SlateLine1: N("SlateLine1"), SlateLine2: N("SlateLine2"),
            CupSugar: N("CupSugar"));
    });
}

public enum FindingStatus { Match, Conflict, Unvisited }

/// <summary>对照结果中的一条。Text 只写差异或地点，不写结论。</summary>
public sealed record Finding(string Evidence, string Place, FindingStatus Status, string Text);

public static class Comparison
{
    private static string T(string key) => Chapter1Data.Text(key);

    /// <summary>“未查”提示具体到区域：只说哪一带还有东西没看过，不说是什么。区域写在证物的“对照区域”。</summary>
    public static string RegionOf(string id) =>
        Chapter1Data.Root.GetProperty("EVIDENCE").TryGetProperty(id, out var evidence) && evidence.TryGetProperty("compare_region", out var region)
            ? region.GetString()! : "这一带";

    /// <summary>参与对照的证物、它们所在的地点，以及卡片上的自然语言名称：凡写了“对照地点”的证物，按章节顺序。</summary>
    public static ImmutableArray<(string Id, string Place, string Name)> Items => Compared.Value;

    private static readonly Lazy<ImmutableArray<(string Id, string Place, string Name)>> Compared = new(() =>
        [..Chapter1Data.Root.GetProperty("EVIDENCE").EnumerateObject().Where(e => e.Value.TryGetProperty("compare_place", out _))
            .Select(e => (e.Name, e.Value.GetProperty("compare_place").GetString()!, e.Value.GetProperty("name").GetString()!))]);

    public static ImmutableArray<Finding> Compare(MorningTraces predicted, MorningTraces actual, Func<string, bool> examined)
    {
        var findings = ImmutableArray.CreateBuilder<Finding>();
        foreach (var (id, place, name) in Items)
        {
            var differences = Differences(id, predicted, actual);
            if (!examined(id))
            {
                // 未检视之处只给地点，不给结果。
                findings.Add(new(id, place, FindingStatus.Unvisited, Chapter1Data.Text("对照·未查", ("区域", RegionOf(id)))));
                continue;
            }
            findings.Add(differences.Count == 0
                ? new(id, place, FindingStatus.Match, Chapter1Data.Text("对照·一致", ("名称", name)))
                : new(id, place, FindingStatus.Conflict, string.Join("", differences)));
        }
        return findings.ToImmutable();
    }

    /// <summary>所有证物都检视过时的冲突证物编号（穷举检查用）。</summary>
    public static ImmutableArray<string> Conflicts(MorningTraces predicted, MorningTraces actual)
        => [..Items.Where(i => Differences(i.Id, predicted, actual).Count > 0).Select(i => i.Id)];

    /// <summary>
    /// 冲突只描述痕迹之差：重演留下了什么、现场是什么。不描述谁做了什么、没做什么。差在哪一项由此处判定，句子写在 chapter_01.story 的“对照·…”文案。
    /// </summary>
    private static List<string> Differences(string id, MorningTraces p, MorningTraces a)
    {
        var d = new List<string>();
        string Case(int value, params int[] known) => known.Contains(value) ? value.ToString() : "其他";
        switch (id)
        {
            case "E1":
                if (p.InnerBoltDown != a.InnerBoltDown) d.Add(T(p.InnerBoltDown ? "对照·E1·重演有闩" : "对照·E1·重演无闩"));
                break;
            case "E2":
                if (p.AshFan != a.AshFan) d.Add(T(p.AshFan ? "对照·E2·重演有灰" : "对照·E2·重演无灰"));
                else if (p.WedgeTopSooted != a.WedgeTopSooted) d.Add(T(p.WedgeTopSooted ? "对照·E2·重演楔沾灰" : "对照·E2·重演楔干净"));
                if (p.WedgeInCorner != a.WedgeInCorner) d.Add(T(p.WedgeInCorner ? "对照·E2·重演楔在角" : "对照·E2·重演楔不在角"));
                break;
            case "E3":
                if (!p.Prints.SequenceEqual(a.Prints))
                {
                    if (p.Prints.Any(x => x.Sole == Sole.WorkBoot)) d.Add(T("对照·E3·工装靴"));
                    else if (p.Prints.IsEmpty) d.Add(T("对照·E3·无印"));
                    else if (!a.Prints.All(p.Prints.Contains)) d.Add(T("对照·E3·朝门缝"));
                    else d.Add(T("对照·E3·不同"));
                }
                break;
            case "E4":
                if (p.Cloth != a.Cloth) d.Add(T(p.Cloth switch { 0 => "对照·E4·无布", 2 => "对照·E4·塞缝", _ => "对照·E4·不同" }));
                else if (p.ClothOverHeel != a.ClothOverHeel) d.Add(T(p.ClothOverHeel ? "对照·E4·压鞋印" : "对照·E4·不相压"));
                break;
            case "E5":
                if (p.ForkByFrame != a.ForkByFrame || p.FrameDent != a.FrameDent) d.Add(T(p.ForkByFrame ? "对照·E5·重演有叉" : "对照·E5·重演无叉"));
                break;
            case "E6":
                if (p.ScoreLine1 != a.ScoreLine1) d.Add(T("对照·E6·行1·" + Case(p.ScoreLine1, 0, 2, 3)));
                if (p.ScoreLine2 != a.ScoreLine2) d.Add(T(p.ScoreLine2 ? "对照·E6·行2·有" : "对照·E6·行2·无"));
                break;
            case "E7":
                if (p.SlateLine1 != a.SlateLine1) d.Add(T("对照·E7·行1·" + Case(p.SlateLine1, 0, 2, 3)));
                if (p.SlateLine2 != a.SlateLine2) d.Add(T("对照·E7·行2·" + Case(p.SlateLine2, 0, 2)));
                break;
            case "E9":
                if (p.CupSugar != a.CupSugar) d.Add(T("对照·E9·" + Case(p.CupSugar, 0, 2)));
                break;
        }
        return d;
    }
}
