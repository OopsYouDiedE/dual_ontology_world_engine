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
        var world=People.FirstOrDefault(p=>p.World.Contains(fx.R(Clock)))?.World;
        return new(Resolve, Collection, ExecutionDesign.GetProperty("procedures"), options, parameters, world);
    }
    internal JsonElement CardCell(string axis, int index) => AstDesign.GetProperty("card_kit").GetProperty(axis)[index];
    internal CardOption CardOption(string axis, int index) => CardKit.Option(CardCell(axis, index));
    private IEnumerable<Rule> DeclaredRules(Fx fx, string id)
    {
        var declaration = AstDesign.GetProperty("rules").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        return BindAst(fx).Rules(declaration.GetProperty("rules"));
    }
    internal P? CardTarget(Card c) => c.Subject >= 10 && CardOption("behaviors",c.Behavior).Targeted ? (P)(c.Subject-10) : null;
    private bool CardWitnessed(Person p, JsonElement expression)
    {
        var known=_specById.ToDictionary(s=>s.Key,s=>(Condition)new Condition.Constant(p.Acc.ContainsKey(s.Value)?Truth.True:Truth.False),StringComparer.Ordinal);
        return Ast.Evaluate(BindAst(FxOf(p),parameters:known).Condition(expression),new(p.Self))==Truth.True;
    }
    internal Effect DeclaredProcedure(Fx fx, string id, Person? actor = null)
    {
        Dictionary<string,Noun>? bindings = actor is null ? null : new()
        { ["$work_route"] = fx.R(Way[WorkPlace(actor.Id)]), ["$work_room"] = fx.R(Space[WorkPlace(actor.Id)]) };
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
