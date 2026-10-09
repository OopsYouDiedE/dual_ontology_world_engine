using DualOntologyWorldEngine.Chapter2.Prototype;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
using System.Collections.Immutable;

/// <summary>可执行示例：替换侦探档案、初始化物资并在原世界运行带选择的剧情。</summary>
// STORY-HARDCODE[CSH-008][text][example]: 可执行示例定义值夜学员、值夜记录、中央厅与调查选择文案。
public sealed class ExampleWorld : IWorldScript
{
    private ScriptCharacter _guest = null!;
    private Essence _note = null!;
    private void Initialize(Game game)
    {
        game.Set(game.Jar, T.Water, 3);
        var world = game.ScriptWorld;
        _guest = world.DefineCharacter("guest", "值夜学员", game.Space[Place.Hall],
            "res://ShareAsset/Internal/Characters/animation_v2/engineer.tres");
        _note = world.DefineObject("note", "值夜记录", game.Space[Place.Hall]);
        var unusedRoom = world.DefineSpace("side-room", "未开放的侧室");
        world.DefineCharacter("unseen", "侧室学员", unusedRoom);
        var known = world.ObserveRoom(_guest).Single(p => p.Name == "值夜记录");
        Observation.Observe(_guest.Soul, new("收起值夜记录", ProjectionKind.Concept)
        {
            Source = ObservationSource.Introspection, Existence = Truth.True,
            Rules = ImmutableArray.Create(new Rule(new Condition.At(known, _guest.Self.Parent!),
                RuleMode.Prefer, new Effect.Act(ActionOp.Take, known), 10, Truth.True))
        });
        world.Activate(_guest);
    }
    public WorldScriptDefinition Define() => new()
    {
        Options = new()
        {
            Profiles = new Dictionary<P, Profile>
            {
                [P.Det] = new()
                {
                    Id = P.Det, Group = Group.Life, Dorm = Place.DormLife,
                    Speed = 1, Cooperation = 2,
                    Voice = "先看清现场，再判断。"
                }
            },
            StartRations = 80,
            StartOil = 30,
            StartMedicine = 10
        },
        Initialize = Initialize,
        Sequences =
        [
            new("opening", 2, Slot.Morning,
            [
                new StoryText("你来到中央厅。水罐已经补满。"),
                new StoryCoreEffect(_ => _guest, _ => new Effect.Think(new Condition.Exists(_guest.Self))),
                new StoryChoice("first-thought", "你准备怎样开始今天？",
                [
                    new("observe", "先记下自己的判断。",
                    [
                        new StoryEffect(P.Det, g => new Effect.Sequence(
                        [
                            new Effect.Set(g.Det.Self, T.Insight, 1),
                            new Effect.Think(new Condition.Exists(g.Det.Self))
                        ])),
                        new StoryText("你记下了此刻亲自确认的事。")
                    ]),
                    new("listen", "先听听大家的说法。",
                    [new StoryText("你决定让亲见与转述各自留在笔记里。")])
                ]),
                new StoryWait(g => g.Day > 2 || g.Slot >= Slot.Noon),
                new StoryBranch(g => Game.Get(g.Det.Body, T.Fed) > 0,
                    [new StoryText("你已经吃过东西，可以继续调查。")],
                    [new StoryText("午间到了，你还没有领到口粮。")]),
                new StoryParallel(
                [
                    [new StoryText("你整理了笔记。")],
                    [new StoryRepeat(2, [new StoryText("铜口的声音从远处传来。")])]
                ])
            ])
        ]
    };
}
