using System.Collections.Immutable;

namespace DualOntology.Language;

/// <summary>Seed evaluator. No filesystem, network, reflection, world access or host eval primitive.</summary>
public sealed class Interpreter
{
    private readonly Scope root = new();
    public Interpreter()
    {
        var natives = ImmutableArray.CreateBuilder<Value>();
        void Native(string name, int minimum, int maximum, Func<ImmutableArray<Value>, Evaluation, Value> body)
        {
            var value = new Value.Native(name, (args, budget) =>
            {
                budget.Tick();
                if (args.Length < minimum || args.Length > maximum) throw new LanguageException($"{name}: invalid arity.");
                return body(args, budget);
            });
            root.Set(name, value);
            natives.Add(Value.Seq(new Value.Symbol(name), value));
        }
        void Pure(string name, int arity, Func<ImmutableArray<Value>, Value> body) => Native(name, arity, arity, (a, _) => body(a));
        Pure("+", 2, a => new Value.Number(Value.NumberOf(a[0]) + Value.NumberOf(a[1])));
        Pure("-", 2, a => new Value.Number(Value.NumberOf(a[0]) - Value.NumberOf(a[1])));
        Pure("*", 2, a => new Value.Number(Value.NumberOf(a[0]) * Value.NumberOf(a[1])));
        Pure("/", 2, a => new Value.Number(Value.NumberOf(a[0]) / Value.NumberOf(a[1])));
        Pure("floor", 1, a => new Value.Number(Math.Floor(Value.NumberOf(a[0]))));
        Pure("<", 2, a => new Value.Boolean(Value.NumberOf(a[0]) < Value.NumberOf(a[1])));
        Pure("=", 2, a => new Value.Boolean(Value.Same(a[0], a[1])));
        Pure("not", 1, a => new Value.Boolean(!Value.IsTruthy(a[0])));
        Pure("list?", 1, a => new Value.Boolean(a[0] is Value.List));
        Pure("symbol?", 1, a => new Value.Boolean(a[0] is Value.Symbol));
        Pure("string?", 1, a => new Value.Boolean(a[0] is Value.Text));
        Pure("number?", 1, a => new Value.Boolean(a[0] is Value.Number));
        Pure("native?", 1, a => new Value.Boolean(a[0] is Value.Native));
        Pure("fn?", 1, a => new Value.Boolean(a[0] is Value.Native or Value.Closure or Value.Wrapped));
        Pure("wrap-function", 1, a => a[0] is Value.Native or Value.Closure or Value.Wrapped
            ? new Value.Wrapped(a[0]) : throw new LanguageException("Expected dispatcher function."));
        Pure("empty?", 1, a => new Value.Boolean(Value.ItemsOf(a[0]).IsEmpty));
        Pure("count", 1, a => new Value.Number(Value.ItemsOf(a[0]).Length));
        Pure("first", 1, a => Value.ItemsOf(a[0]).FirstOrDefault() ?? Value.Null);
        Pure("rest", 1, a => new Value.List([.. Value.ItemsOf(a[0]).Skip(1)]));
        Pure("nth", 2, a =>
        {
            var items = Value.ItemsOf(a[0]);
            var index = Value.NumberOf(a[1]);
            return index >= 0 && index < items.Length && index == Math.Truncate(index) ? items[(int)index] : throw new LanguageException("nth: invalid index.");
        });
        Pure("cons", 2, a => new Value.List([a[0], .. Value.ItemsOf(a[1])]));
        Pure("concat", 2, a => new Value.List([.. Value.ItemsOf(a[0]), .. Value.ItemsOf(a[1])]));
        Native("list", 0, int.MaxValue, (a, _) => new Value.List(a));
        Pure("atom", 1, a => new Value.Cell(a[0]));
        Pure("deref", 1, a => a[0] is Value.Cell cell ? cell.Content : throw new LanguageException("Expected cell."));
        Pure("reset!", 2, a => a[0] is Value.Cell cell ? cell.Content = a[1] : throw new LanguageException("Expected cell."));
        Pure("error", 1, a => throw new LanguageException(Value.Print(a[0])));
        Native("apply", 2, 2, (a, b) => Invoke(a[0], Value.ItemsOf(a[1]), b));
        root.Set("native-bindings", new Value.List(natives.ToImmutable()));
    }
    public Value Evaluate(string source, EvaluationLimits? limits = null) => Evaluate(Reader.ReadAll(source), limits);
    public Value Evaluate(IEnumerable<Value> forms, EvaluationLimits? limits = null)
    {
        var budget = new Evaluation(limits ?? new());
        Value result = Value.Null;
        foreach (var form in forms) result = Eval(form, root, budget);
        return result;
    }
    public Value Call(string name, params Value[] args) => Call(name, new EvaluationLimits(), args);
    public Value Call(string name, EvaluationLimits limits, params Value[] args) => Invoke(root.Get(name), [.. args], new Evaluation(limits));
    private static Scope Bind(Value.Closure closure, ImmutableArray<Value> args)
    {
        if (closure.Parameters.Length != args.Length) throw new LanguageException("Function arity mismatch.");
        var scope = new Scope(closure.Environment);
        for (var i = 0; i < args.Length; i++) scope.Set(closure.Parameters[i], args[i]);
        return scope;
    }
    private Value Invoke(Value callable, ImmutableArray<Value> args, Evaluation budget) => callable switch
    {
        Value.Native native => native.Body(args, budget),
        Value.Closure closure => Eval(closure.Body, Bind(closure, args), budget),
        Value.Wrapped wrapped => Invoke(wrapped.Dispatcher, [new Value.List(args)], budget),
        _ => throw new LanguageException("Expected callable.")
    };
    private Value Eval(Value expression, Scope scope, Evaluation budget)
    {
        budget.Enter();
        try
        {
            while (true)
            {
                budget.Tick();
                if (expression is Value.Symbol symbol) return scope.Get(symbol.Name);
                if (expression is not Value.List list || list.Items.IsEmpty) return expression;
                var items = list.Items;
                var head = items[0] as Value.Symbol;
                void Arity(int length) { if (items.Length != length) throw new LanguageException($"{head?.Name}: invalid form arity."); }
                switch (head?.Name)
                {
                    case "quote": Arity(2); return items[1];
                    case "def!": Arity(3); return scope.Set(Value.SymbolOf(items[1]), Eval(items[2], scope, budget));
                    case "if":
                        if (items.Length is not 3 and not 4) throw new LanguageException("if: invalid arity.");
                        expression = Value.IsTruthy(Eval(items[1], scope, budget)) ? items[2] : items.Length == 4 ? items[3] : Value.Null;
                        continue;
                    case "do":
                        if (items.Length == 1) return Value.Null;
                        for (var i = 1; i < items.Length - 1; i++) Eval(items[i], scope, budget);
                        expression = items[^1]; continue;
                    case "let*":
                        Arity(3);
                        var bindings = Value.ItemsOf(items[1]);
                        if (bindings.Length % 2 != 0) throw new LanguageException("let*: uneven bindings.");
                        var nested = new Scope(scope);
                        for (var i = 0; i < bindings.Length; i += 2) nested.Set(Value.SymbolOf(bindings[i]), Eval(bindings[i + 1], nested, budget));
                        scope = nested; expression = items[2]; continue;
                    case "fn*":
                        Arity(3);
                        var parameters = Value.ItemsOf(items[1]).Select(Value.SymbolOf).ToImmutableArray();
                        if (parameters.Distinct(StringComparer.Ordinal).Count() != parameters.Length) throw new LanguageException("fn*: duplicate parameter.");
                        return new Value.Closure(parameters, items[2], scope);
                    default:
                        var function = Eval(items[0], scope, budget);
                        var args = items.Skip(1).Select(item => Eval(item, scope, budget)).ToImmutableArray();
                        if (function is Value.Closure closure) { scope = Bind(closure, args); expression = closure.Body; continue; }
                        return Invoke(function, args, budget);
                }
            }
        }
        finally { budget.Leave(); }
    }
}
