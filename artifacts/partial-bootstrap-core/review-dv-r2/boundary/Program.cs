using System.Collections.Immutable;
using DualOntology.Language;

static class BoundaryReview
{
    private static ImmutableDictionary<string, int> Stock(int value) => ImmutableDictionary<string, int>.Empty.Add("stock", value);
    private static void Show(string name, object? result) => Console.WriteLine($"{name}: {result}");
    public static void Main()
    {
        var world = new WorldCore();
        var room = new EntityId("authority-room");
        var bodyA = new EntityId("authority-body-a");
        var bodyB = new EntityId("authority-body-b");
        var objectA = new EntityId("authority-object-a");
        var objectB = new EntityId("authority-object-b");
        world.Define(room, new("space"));
        world.Define(bodyA, new("body", room));
        world.Define(bodyB, new("body", room));
        world.Define(objectA, new("object", room, Tags: Stock(3)));
        world.Define(objectB, new("object", room, Tags: Stock(11)));
        var subjectA = Guid.NewGuid();
        var subjectB = Guid.NewGuid();
        world.CreateSubject(subjectA, bodyA);
        world.CreateSubject(subjectB, bodyB);
        world.Observe(subjectA,
            new(room, "room", new(Truth.True, "space")),
            new(bodyA, "self", new(Truth.True, "body", "room")),
            new(bodyB, "other-body", new(Truth.True, "body", "room")),
            new(objectA, "object", new(Truth.True, "object", "room", Stock(7))),
            new(objectA, "facet", new(Truth.True, "object", "room", Stock(2))));
        world.Observe(subjectB,
            new(room, "room", new(Truth.True, "space")),
            new(bodyB, "self", new(Truth.True, "body", "room")),
            new(objectB, "object", new(Truth.True, "object", "room", Stock(11))));
        var aBefore = world.Capture(subjectA);
        var bBefore = world.Capture(subjectB);
        var bindings = new ActionBindings("self", "object", Scene: "room");
        Show("A object observed stock>=7", world.Evaluate(aBefore, Condition.Parse("(tagged (ref \"object\") stock 7)"), bindings));
        Show("A facet observed stock>=7", world.Evaluate(aBefore, Condition.Parse("(tagged (ref \"facet\") stock 7)"), bindings));
        Show("A missing facet tag remains Unknown", world.Evaluate(aBefore, Condition.Parse("(tagged (ref \"facet\") missing 1)"), bindings));
        Show("A object stock>=11 before B mutation", world.Evaluate(aBefore, Condition.Parse("(tagged (slot target) stock 11)"), bindings));
        Show("B same local object handle stock>=11", world.Evaluate(bBefore, Condition.Parse("(tagged (slot target) stock 11)"), bindings));
        var receipt = world.Commit(world.Prepare(subjectA,
            Effect.Parse("(seq (change (ref \"object\") stock 1) (change (ref \"facet\") stock 1))"), bindings));
        Show("A alias write receipt", receipt);
        Show("A authority after 3+1+1", world.ReadAuthority(objectA).Tags!["stock"]);
        Show("A old snapshot after authority write", Value.Print(aBefore.Facts));
        Show("A new snapshot after authority write", Value.Print(world.Capture(subjectA).Facts));
        Show("B authority after A commit", world.ReadAuthority(objectB).Tags!["stock"]);
        world.Observe(subjectB, new Observation(objectB, "object", new(Truth.True, "object", "room", Stock(15))));
        Show("B old snapshot stock>=15", world.Evaluate(bBefore, Condition.Parse("(tagged (slot target) stock 15)"), bindings));
        Show("B new snapshot stock>=15", world.Evaluate(world.Capture(subjectB), Condition.Parse("(tagged (slot target) stock 15)"), bindings));
        Show("A object stock>=11 after B mutation", world.Evaluate(world.Capture(subjectA), Condition.Parse("(tagged (slot target) stock 11)"), bindings));
        Show("A trying B body actor", world.Prepare(subjectA, Effect.Parse("(change (slot target) stock 1)"),
            new ActionBindings("other-body", "object", Scene: "room")).Failure);
        Show("snapshot includes authority entity ID text", Value.Print(world.Capture(subjectA).Facts).Contains("authority-", StringComparison.Ordinal));
    }
}
