using DualOntologyWorldEngine.Awakening;
using DualOntologyWorldEngine.Story;
using Step = DualOntologyWorldEngine.Story.StoryStep<DualOntologyWorldEngine.Awakening.MorningWorld, DualOntologyWorldEngine.Awakening.Person>;
public sealed class FinalAwakeningHostScript : IChapterScript<MorningWorld, Person>
{
    public ChapterStoryDefinition<MorningWorld, Person> Define() => new() { Sequences = [new("media", _ => true, [
        new Step.Text("VALIDATION BEFORE IMAGE"),
        new Step.Presentation(new(StoryPresentationKind.Image, "res://ShareAsset/Internal/Props/Chapter2/rooms/clinic.png", Person.Detective)),
        new Step.Text("VALIDATION AFTER IMAGE"),
        new Step.Presentation(new(StoryPresentationKind.Image, "res://ShareAsset/Internal/Props/Chapter2/rooms/clinic.png", Person.Detective)),
        new Step.Text("VALIDATION AFTER SECOND IMAGE")])] };
}
