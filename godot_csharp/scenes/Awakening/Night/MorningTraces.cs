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
    /// <summary>今晨的现实（证物卡所写的事实）。作者侧给定，不由重演生成；穷举检查要求只有一组变量能重演出它。</summary>
    public static readonly MorningTraces Evidence = new(
        InnerBoltDown: true,
        AshFan: true,
        WedgeTopSooted: true,
        WedgeInCorner: true,
        Prints: [new BootPrint(Sole.PoloBoot, PrintToward.Bolt)],
        Cloth: 1,
        ClothOverHeel: true,
        ForkByFrame: true,
        FrameDent: true,
        ScoreLine1: 1,
        ScoreLine2: true,
        SlateLine1: 1,
        SlateLine2: 1,
        CupSugar: 1);
}

public enum FindingStatus { Match, Conflict, Unvisited }

/// <summary>对照结果中的一条。Text 只写差异或地点，不写结论。</summary>
public sealed record Finding(string Evidence, string Place, FindingStatus Status, string Text);

public static class Comparison
{
    /// <summary>参与对照的证物、它们所在的地点，以及卡片上的自然语言名称。</summary>
    /// <summary>“未查”提示具体到区域：只说哪一带还有东西没看过，不说是什么。</summary>
    public static string RegionOf(string id) => id switch
    {
        "E1" => "门厅那扇门上",
        "E2" => "门厅墙角与门底一带",
        "E3" => "门厅插闩下方的地面",
        "E4" => "门厅门底",
        "E5" => "门厅门框一带",
        "E6" => "R-08 的书桌上",
        "E7" => "活动厅长桌上",
        "E9" => "备餐间水槽一带",
        _ => "这一带"
    };

    public static readonly ImmutableArray<(string Id, string Place, string Name)> Items =
    [
        ("E1", "门厅", "闩上的隔离门"),
        ("E2", "门厅", "门底的灰与木楔"),
        ("E3", "门厅", "灰上的靴印"),
        ("E4", "门厅", "门底的湿布"),
        ("E5", "门厅", "门框旁的音叉"),
        ("E6", "R-08 书桌", "苏泠的乐谱"),
        ("E7", "活动厅长桌", "霍铎的石板"),
        ("E9", "备餐间水槽", "四只杯子"),
    ];

    public static ImmutableArray<Finding> Compare(MorningTraces predicted, MorningTraces actual, Func<string, bool> examined)
    {
        var findings = ImmutableArray.CreateBuilder<Finding>();
        foreach (var (id, place, name) in Items)
        {
            var differences = Differences(id, predicted, actual);
            if (!examined(id))
            {
                // 未检视之处只给地点，不给结果。
                findings.Add(new(id, place, FindingStatus.Unvisited, $"你还没查过这里：{RegionOf(id)}还有东西没看过。"));
                continue;
            }
            findings.Add(differences.Count == 0
                ? new(id, place, FindingStatus.Match, name + "：与重演一致。")
                : new(id, place, FindingStatus.Conflict, string.Join("", differences)));
        }
        return findings.ToImmutable();
    }

    /// <summary>所有证物都检视过时的冲突证物编号（穷举检查用）。</summary>
    public static ImmutableArray<string> Conflicts(MorningTraces predicted, MorningTraces actual)
        => [..Items.Where(i => Differences(i.Id, predicted, actual).Count > 0).Select(i => i.Id)];

    /// <summary>
    /// 冲突只描述痕迹之差：重演留下了什么、现场是什么。不描述谁做了什么、没做什么。
    /// </summary>
    private static List<string> Differences(string id, MorningTraces p, MorningTraces a)
    {
        var d = new List<string>();
        switch (id)
        {
            case "E1":
                if (p.InnerBoltDown != a.InnerBoltDown)
                    d.Add(p.InnerBoltDown ? "重演中内闩落在闩槽里；现场内闩没有落下。" : "重演中内闩没有落下；现场内闩落在闩槽里。");
                break;
            case "E2":
                if (p.AshFan != a.AshFan)
                    d.Add(p.AshFan ? "重演中门内地面有一片扇形灰；现场门内没有灰。" : "重演中门内地面没有灰；现场有一片扇形灰。");
                else if (p.WedgeTopSooted != a.WedgeTopSooted)
                    d.Add(p.WedgeTopSooted ? "重演中木楔上沾着灰；现场木楔是干净的。" : "重演中木楔是干净的；现场木楔上面沾灰、底面干净。");
                if (p.WedgeInCorner != a.WedgeInCorner)
                    d.Add(p.WedgeInCorner ? "重演中木楔在墙角；现场木楔不在墙角。" : "重演中木楔不在墙角；现场木楔在墙角。");
                break;
            case "E3":
                if (!p.Prints.SequenceEqual(a.Prints))
                {
                    if (p.Prints.Any(x => x.Sole == Sole.WorkBoot))
                        d.Add("重演中灰上有厚底工装靴印；现场只有一枚马球靴印。");
                    else if (p.Prints.IsEmpty)
                        d.Add("重演中门边的灰上没有鞋印；现场有一枚马球靴印。");
                    else if (!p.Prints.Contains(new BootPrint(Sole.PoloBoot, PrintToward.Bolt)))
                        d.Add("重演中马球靴印的鞋尖朝着门缝；现场的鞋尖朝着插闩。");
                    else
                        d.Add("重演中灰上的鞋印与现场不同。");
                }
                break;
            case "E4":
                if (p.Cloth != a.Cloth)
                    d.Add(p.Cloth switch
                    {
                        0 => "重演中门底没有湿布；现场门底压着一条湿抹布。",
                        2 => "重演中湿布塞在门缝里；现场湿布沿闭合的门底压成一条直线。",
                        _ => "重演中湿布的样子与现场不同。"
                    });
                else if (p.ClothOverHeel != a.ClothOverHeel)
                    d.Add(p.ClothOverHeel ? "重演中湿布压着一枚鞋印；现场湿布下没有鞋印。" : "重演中湿布与鞋印不相压；现场湿布的一端压住了靴印的鞋跟。");
                break;
            case "E5":
                if (p.ForkByFrame != a.ForkByFrame || p.FrameDent != a.FrameDent)
                    d.Add(p.ForkByFrame ? "重演中门框旁有音叉、门框上有凹痕；现场没有。" : "重演中门框旁没有音叉，门框完好；现场音叉落在门框旁，门框齐肩处有一个小凹痕。");
                break;
            case "E6":
                if (p.ScoreLine1 != a.ScoreLine1)
                    d.Add(p.ScoreLine1 switch
                    {
                        0 => "重演中乐谱是空白的；现场乐谱记着两遍重读的“开门”，旁注“门外？”。",
                        2 => "重演中乐谱上的字是“救我”；现场是“开门”。",
                        3 => "重演中乐谱记下了整句话；现场前两拍是听不清的弱音。",
                        _ => "重演中的乐谱与现场不同。"
                    });
                if (p.ScoreLine2 != a.ScoreLine2)
                    d.Add(p.ScoreLine2 ? "重演中乐谱下方多一行字；现场没有。" : "重演中乐谱下方没有字；现场下方换了一种墨迹写着“他们关了门”。");
                break;
            case "E7":
                if (p.SlateLine1 != a.SlateLine1)
                    d.Add(p.SlateLine1 switch
                    {
                        0 => "重演中石板是空的；现场有一行粉笔速记。",
                        2 => "重演中石板上记的是“开门”；现场写着“✕开”。",
                        3 => "重演中石板速记的末尾没有 ◎；现场末尾有 ◎。",
                        _ => "重演中的石板与现场不同。"
                    });
                if (p.SlateLine2 != a.SlateLine2)
                    d.Add(p.SlateLine2 switch
                    {
                        0 => "重演中石板只有一行；现场有两行，第二行是一个小小的放大镜图样加“⊥✓”。",
                        2 => "重演中石板第二行没有小图；现场第二行画的是放大镜。",
                        _ => "重演中的石板第二行与现场不同。"
                    });
                break;
            case "E9":
                if (p.CupSugar != a.CupSugar)
                    d.Add(p.CupSugar switch
                    {
                        0 => "重演中水槽里没有杯子；现场有四只用过的杯子。",
                        2 => "重演中四只杯底的糖渍一样；现场青瓷杯底没有糖渍，锡杯底糖渍很厚。",
                        _ => "重演中的杯子与现场不同。"
                    });
                break;
        }
        return d;
    }
}
