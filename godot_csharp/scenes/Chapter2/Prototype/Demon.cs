using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>恶魔放大欲望：把与此人核心欲望相连的习惯规则加权（Cognition.Corrupt，来源 Corruption）。</summary>
public sealed class DesireAmplifier(Person p, ImmutableArray<Rule> reweighted) : IProjectionTransform
{
    public ObservationInput? Transform(ObservationInput input)
        => ReferenceEquals(input.Target, p.Habit) ? input with { Rules = reweighted, Details = "恶魔的话让此事更想要" } : null;
}

public sealed partial class Game
{
    /// <summary>恶魔说出的每一句话（自动检查：绝不主动提第 9、10 条）。</summary>
    public readonly List<(int Day, string To, string Text, bool Asked9)> DemonSaid = [];
    public bool SteleRubbed, Clause9Asked;
    public int GuaranteeCracks;

    private void DemonSays(string to, string text, bool asked9 = false)
    {
        DemonSaid.Add((Day, to, text, asked9));
    }

    /// <summary>晨间广播：经铜口传遍学园，人人听到（Testimony）。</summary>
    public void Broadcast(string text)
    {
        DemonSays("全体", text);
        Feed.Add("【广播】" + text);
        foreach (var p in People)
            Observation.Observe(p.Soul, new ObservationInput("晨间广播", ProjectionKind.Event)
            { Source = ObservationSource.Testimony, Existence = Truth.True, Time = Now, Details = text });
    }

    public static readonly ImmutableDictionary<int, (int Day, string Title, string Teach)> Lessons = new Dictionary<int, (int, string, string)>
    {
        [11] = (2, "守则读法", "按字面读它自己的守则与学员立的规，找出条文没写什么。"),
        [12] = (0, "传声与机械", "传声网图纸与信号规程：中继站须按三拍规程转发，否则信号被管道吃掉一半。"),
        [13] = (0, "言灵初阶", "十个词、基本语法：名词居首、条件居中、动词收尾。练习：两盏灯只灭一盏。"),
        [14] = (0, "异象与记忆", "封存与回忆的原理：触物即接通，回忆即上钩。"),
    }.ToImmutableDictionary();

    public static int LessonOfDay(int day) => Lessons.FirstOrDefault(l => l.Value.Day == day).Key;

    /// <summary>上课：在场者获得本课（完整），认知中写入课上所学；恶魔显形板书，留下粉笔字与窄底鞋印；对到场者各说一句放大欲望的真话。</summary>
    public void TeachClass()
    {
        var lesson = LessonOfDay(Day);
        if (lesson == 0) return;
        var (_, title, teach) = Lessons[lesson];
        var present = People.Where(p => Where(p) == Place.Lecture && p.Exec is null || Where(p) == Place.Lecture).Distinct().ToArray();
        Add(Blackboard, T.Chalk, 1);
        Set(Blackboard, T.Shoe, 1);
        Rt.Sync();
        DemonSays("讲堂", $"{title}：{teach}");
        NewsItem? item = null;
        foreach (var p in present)
        {
            p.Lessons[lesson] = true;
            Observation.Observe(p.Soul, new ObservationInput(p.Lore.Name, p.Lore.Kind)
            { Target = p.Lore, Source = ObservationSource.Testimony, Time = Now, Details = title + "：" + teach });
            if (item is null) item = NewsFrom(p, NewsKind.Lesson, $"{title}讲了：{teach}", lesson: lesson);
            else p.News[item.Id] = new(p.Id, 0, ObservationSource.Perception, Day, item.Text);
            if (lesson == 14) LessonFourteen(p);
        }
        Log.Add($"D{Day} 上午 {title}：到课 {string.Join("、", present.Select(p => p.Name))}");
        if (present.Contains(Det))
            Feed.Add($"【{title}】恶魔显形板书。{teach}（到课：{string.Join("、", present.Where(p => p != Det).Select(p => p.Name))}）");
        // 它挑最容易被推动的三个人说话（欲望与钩最高者）。
        foreach (var p in present.Where(p => p != Det).OrderBy(p => p.Whispered).ThenByDescending(p => p.Desire + p.Hooks).ThenBy(p => p.Id).Take(3)) Whisper(p);
    }

    /// <summary>第十四课：讲清回忆为何接上钩。欲望强的人更想去试，欲望弱的人更怕（第三部分 §5.8）。</summary>
    private void LessonFourteen(Person p)
    {
        if (p.Desire >= 3) { p.Desire += 1; RewriteDesire(p, "《异象与记忆》让人更想试一试"); }
        if (p != Det) Tell(p, Warning1, Channel.Face);
    }

    /// <summary>恶魔对此人说一句字面为真、朝向其欲望的话。</summary>
    public void Whisper(Person p)
    {
        if (p.Pro.DemonLine == "" || p.Desire >= 4) return;
        DemonSays(p.Name, p.Pro.DemonLine);
        p.Desire += 1;
        p.Whispered++;
        RewriteDesire(p, p.Pro.DemonLine);
        if (p == Det) Feed.Add($"恶魔低声对你说：“{p.Pro.DemonLine}”");
    }

    public void RewriteDesire(Person p, string why)
    {
        Cognition.Corrupt(p.Soul, new DesireAmplifier(p, HabitRules(p)), Now);
        p.Diary.Add($"D{Day} {Names.Of(Slot)} 欲望升至 {p.Desire}：{why}");
    }

    public void MorningBroadcast()
    {
        switch (Day)
        {
            case 2:
                Broadcast("诸位晨安。今日讲守则的读法。请到讲堂来。");
                Broadcast("封存之物存于名录库，凡学员皆可取回己物。");
                Broadcast("另：入夜后，名录库中若有人以你之名唤你，勿应。");
                foreach (var p in People.Where(p => p != Det)) Tell(p, Warning1, Channel.Public);
                Det.Acc[Warning1] = Truth.True;
                Feed.Add("恶魔逐一对每个人低声说了一句话，说的都是那人最想要的东西。");
                foreach (var p in People) Whisper(p);
                break;
            case 3:
            {
                var shortBy = CaseActual?.LedgerShort ?? 0;
                Broadcast(shortBy > 0 ? $"诸位晨安。台账上，少了{Cn(shortBy)}份。昨夜，有人进过后勤院。" : "诸位晨安。今日无新课。");
                break;
            }
            case 4: Broadcast("今日，诸位要决定名录库的事。我只旁听。"); break;
        }
    }

    // --------------------------------------------------------------- Q&A --

    public static readonly string[] Topics = ["名录库", "唤名", "温室", "封存匣", "守则", "裂纹", "导师", "你想要什么"];

    /// <summary>
    /// 答疑：问得含糊，得到字面为真而误导的答案；问得精确，得到精确的真相；可援引第 7 条拒答。
    /// 提问只能用已学的词：精确提问须学过相应的课（或亲见过相关事物）。课堂回答是公开的。
    /// </summary>
    public string AskDemon(int topic, bool precise, out bool praised, out bool refused)
    {
        praised = false; refused = false;
        var learned = (int l) => Det.Lessons.ContainsKey(l);
        string answer;
        switch (topic)
        {
            case 0:
                answer = !precise ? "封存之物存于名录库，凡学员皆可取回己物。"
                    : learned(14) ? "取回，即重新接通。接通之后，我离诸位更近一些。寻回者，自负其果——守则第一条写着。" : "此事《异象与记忆》会讲。现在讲，诸位听不懂。";
                praised = precise && learned(14);
                break;
            case 1:
                answer = !precise ? "勿应即可。" : "应答即触物回忆，记一枚钩；有钩者，更难抗拒下一次。";
                praised = precise;
                break;
            case 2:
                answer = !precise ? "指针过红线时，勿开顶窗。" : learned(12) ? "先在阀门泄压，指针回落到红线下再开顶窗。红线之上开窗，玻璃会碎。" : "你还不认得那张图。学过《传声与机械》再问。";
                praised = precise && learned(12);
                if (precise && learned(12)) WarnedPrecisely = true;
                break;
            case 3:
                answer = !precise ? "每只匣子只应其主。" : learned(13) ? "须以言灵语法念出本名：名词居首，条件居中，动词收尾。念错一个字，它就当你不在。" : "你还没有那样的词。";
                praised = precise && learned(13);
                break;
            case 4:
                if (!precise) answer = "我所立之规，皆张贴于中央厅的公告框。四条，一字不多。";
                else { answer = "守则之外的事，此知会伤及心志。"; refused = true; }
                break;
            case 5:
                answer = "此知会伤及心志。";
                refused = true;
                break;
            case 6:
                answer = "此知会伤及心志。";
                refused = true;
                break;
            default:
                answer = "我想教。诸位想学。我们要的东西，没有那么不同。";
                break;
        }
        DemonSays("侦探", answer);
        return answer;
    }

    public bool WarnedPrecisely;
}
