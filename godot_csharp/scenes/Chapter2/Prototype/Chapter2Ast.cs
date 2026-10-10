using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed partial class Game
{
    internal JsonElement AstDesign => _authored is { Design.ValueKind: JsonValueKind.Object } ? _authored.Design : Chapter2Design.Chapter.GetProperty("design");
    internal JsonElement ExecutionDesign => AstDesign.GetProperty("execution");
    internal string AstText(string key, params (string Name, object? Value)[] values)
    {
        if (!AstDesign.GetProperty("text").TryGetProperty(key,out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Unknown authored text: " + key);
        var text=value.GetString()!;
        foreach (var (name,item) in values) text=text.Replace("{"+name+"}",item?.ToString()??"");
        return text;
    }
    internal ScriptAst BindAst(Fx fx, IReadOnlyDictionary<string, Noun>? bindings = null,
        IReadOnlyDictionary<string, string>? options = null, IReadOnlyDictionary<string, Condition>? parameters = null)
    {
        Noun Resolve(string id)
        {
            if (bindings is not null && bindings.TryGetValue(id, out var noun)) return noun;
            var route = _routes.Values.FirstOrDefault(r => r.Id == id || "world:" + r.Id == id);
            return fx.R(route is null ? ResolveEntity(id) : Way[route.To]);
        }
        IEnumerable<Noun> Collection(string id) => id switch
        {
            "characters" => People.Select(p => (Noun)fx.R(p.Body)),
            "objects" => Objects.Select(e => (Noun)fx.R(e)), "rooms" => Rooms.Select(e => (Noun)fx.R(e)),
            _ => throw new InvalidDataException("Unknown AST collection: " + id)
        };
        var person=People.FirstOrDefault(p=>p.World.Contains(fx.R(Clock)));
        var world=person?.World;
        var conditions=parameters is null ? new Dictionary<string,Condition>() : new Dictionary<string,Condition>(parameters);
        if(person is not null)conditions["is_player"]=new Condition.Constant(ReferenceEquals(person,Det) ? Truth.True : Truth.False);
        if (person is not null && ExecutionDesign.TryGetProperty("belief_parameters", out var beliefs))
            foreach (var entry in beliefs.EnumerateObject())
            {
                var row=entry.Value;
                var known=row.TryGetProperty("claim",out var claim)
                    ? ClaimsOf(person).Any(c=>c.Predicate==claim.GetString())
                    : person.Acc.GetValueOrDefault(_specById[row.GetProperty("rule").GetString()!])==Truth.True;
                if(row.TryGetProperty("accepted",out var accepted) && !accepted.GetBoolean())known=!known;
                conditions[entry.Name]=new Condition.Constant(known ? Truth.True : Truth.False);
            }
        int Number(Projection projection, Tag tag)
            => View.First(v => ReferenceEquals(v.Value, projection)).Key.Get<StateProperty>()?.Values.GetValueOrDefault(tag) ?? 0;
        int SubjectNumber(Projection projection,Tag tag) => person is not null && ReferenceEquals(projection,person.Self)
            ? tag.Name switch { nameof(Person.Hooks)=>person.Hooks, nameof(Person.Desire)=>person.Desire, nameof(Person.Whispered)=>person.Whispered,
                _=>projection.Tags.GetValueOrDefault(tag) }
            : projection.Tags.GetValueOrDefault(tag);
        return new(Resolve, Collection, ExecutionDesign.GetProperty("procedures"), options, conditions, world,
            ReferenceEquals(fx.R(Clock), View[Clock]) ? Number : SubjectNumber,
            People.FirstOrDefault(p => p.World == world)?.Self ?? View[Clock]);
    }
    internal JsonElement CardCell(string axis, int index) => AstDesign.GetProperty("card_kit").GetProperty(axis)[index];
    internal CardOption CardOption(string axis, int index) => CardKit.Option(CardCell(axis, index));
    private IEnumerable<Rule> DeclaredRules(Fx fx, string id)
    {
        var declaration = AstDesign.GetProperty("rules").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        return BindAst(fx).Rules(declaration.GetProperty("rules"));
    }
    private Noun BindReference(Fx fx, string id)
        => id.StartsWith("route.", StringComparison.Ordinal) ? fx.R(Way[Enum.Parse<Place>(id[6..])]) : fx.R(ResolveEntity(id));
    internal P? CardTarget(Card c) => c.Subject >= 10 && CardOption("behaviors",c.Behavior).Targeted ? (P)(c.Subject-10) : null;
    private bool CardWitnessed(Person p, JsonElement expression)
    {
        var known=_specById.ToDictionary(s=>s.Key,s=>(Condition)new Condition.Constant(p.Acc.ContainsKey(s.Value)?Truth.True:Truth.False),StringComparer.Ordinal);
        return Ast.Evaluate(BindAst(FxOf(p),parameters:known).Condition(expression),new(p.Self))==Truth.True;
    }
    internal Effect DeclaredProcedure(Fx fx, string id, Person? actor = null)
    {
        Dictionary<string,Noun>? bindings = actor is null ? null : new()
        {
            ["$work_route"] = fx.R(Way[WorkPlace(actor.Id)]), ["$work_room"] = fx.R(Space[WorkPlace(actor.Id)]),
            ["$dorm_route"] = fx.R(Way[actor.Pro.Dorm]), ["$dorm_room"] = fx.R(Space[actor.Pro.Dorm]),
            ["$litter"] = fx.R(Litter[actor.Pro.Dorm]),
            ["$station"] = fx.R(StationFor(actor)), ["$station_route"] = fx.R(Way[PlaceOf[StationFor(actor)]])
        };
        if (actor is not null && ExecutionDesign.TryGetProperty("bindings", out var rows) && rows.TryGetProperty(id, out var profiles))
        {
            var row = profiles.TryGetProperty(actor.Id.ToString(), out var own) ? own : profiles.GetProperty("default");
            foreach (var entry in row.EnumerateObject()) bindings![entry.Name] = BindReference(fx, entry.Value.GetString()!);
        }
        return BindAst(fx,bindings).Procedure(id);
    }
    private string CardText(Card c) => Chapter2Design.Text("规矩格.格式",
        ("主语", c.Subject >= 10 ? Card.SubjectText(c.Subject) : CardOption("subjects",c.Subject).Text),
        ("条件",CardOption("conds",c.Cond).Text),("行为",CardOption("behaviors",c.Behavior).Text),
        ("例外",CardOption("exceptions",c.Exception).Text),("记账",CardOption("accounts",c.Account).Text));
    private IEnumerable<Rule> CompileCard(Fx fx, Card card)
    {
        var options = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (var (axis,index) in new[] { ("conds",card.Cond),("behaviors",card.Behavior),("exceptions",card.Exception),("accounts",card.Account) })
            options[axis] = CardCell(axis,index).GetProperty("id").GetString()!;
        var ast = BindAst(fx, options:options);
        var subject = card.Subject >= 10 ? Fx.Mine(T.Is[(P)(card.Subject - 10)])
            : ast.Condition(CardCell("subjects",card.Subject).GetProperty("condition"));
        var condition = ast.Condition(CardCell("conds",card.Cond).GetProperty("condition"));
        return BindAst(fx, options:options, parameters:new Dictionary<string,Condition> { ["subject"]=subject,["condition"]=condition })
            .Rules(CardCell("behaviors",card.Behavior).GetProperty("rules"));
    }
}
