using DualOntologyWorldEngine.Awakening;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
using System.Collections.Immutable;
using Step = DualOntologyWorldEngine.Story.StoryStep<DualOntologyWorldEngine.Awakening.MorningWorld, DualOntologyWorldEngine.Awakening.Person>;

/// <summary>第一章普通 C# 脚本，复用原四人、房间、证物和 Core 会话。</summary>
public sealed class ExampleAwakening : IChapterScript<MorningWorld, Person>
{
    private ScriptCharacter _witness = null!;
    private static Projection Self(MorningWorld world) => world.WorldOf(Person.Detective).Projections
        .Single(p => p.Kind == ProjectionKind.Body && p.Name == Names.Of(Person.Detective));

    public ChapterStoryDefinition<MorningWorld, Person> Define() => new()
    {
        Initialize = world =>
        {
            // 位置直接写入原 MorningWorld，不创建另一份人物或房间模型。
            world.Locate(Person.Detective, Room.R05);
            world.Locate(Person.Huo, Room.Hall);
            world.Locate(Person.Shen, Room.Pantry);
            world.Locate(Person.Su, Room.R08);
            var core = world.ScriptWorld;
            var room = core["room:R05"];
            _witness = core.DefineCharacter("witness", "引航学员", room,
                "res://ShareAsset/Internal/Characters/animation_v2/engineer.tres");
            core.DefineObject("pilot-note", "引航笔记", room);
            var privateRoom = core.DefineSpace("private-room", "未开放的侧室");
            core.DefineCharacter("unseen", "侧室学员", privateRoom);
            var note = core.ObserveRoom(_witness).Single(p => p.Name == "引航笔记");
            Observation.Observe(_witness.Soul, new("收起自己的笔记", ProjectionKind.Concept)
            {
                Source = ObservationSource.Introspection, Existence = Truth.True,
                Rules = ImmutableArray.Create(new Rule(new Condition.At(note, _witness.Self.Parent!),
                    RuleMode.Prefer, new Effect.Act(ActionOp.Take, note), 10, Truth.True))
            });
            core.Activate(_witness);
        },
        Sequences =
        [
            new("first-look", _ => true,
            [
                new Step.CoreAction(_ => _witness, _ => new Effect.Think(new Condition.Exists(_witness.Self))),
                new Step.Choice("awakening-start", "你准备怎样开始调查？",
                [
                    new("pocket", "先查看自己的外衣口袋。",
                    [
                        new Step.Update(world => world.Examine(Person.Detective, "E12")),
                        new Step.Action(Person.Detective, world => new Effect.Think(new Condition.Exists(Self(world)))),
                        new Step.Wait(world => world.WorldOf(Person.Detective).Projections
                            .Any(p => p.Name == "侦探的外衣口袋" && p.Existence == Truth.True)),
                        new Step.Text("你摸到了口袋里的放大镜，记下了这件亲自确认的事。")
                    ]),
                    new("companions", "先与同伴见面。", [new Step.Text("你决定先听同伴怎样讲述这个清晨。")])
                ])
            ])
        ]
    };
}
