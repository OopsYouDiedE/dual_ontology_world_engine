using System.Runtime.CompilerServices;
using System.Text.Json;
using DuelView.Compiled;
using static DuelView.Core.DVKernel;

namespace DuelView.Core;

/// <summary>
/// Instantiates finite, data-only value templates. Operators construct values or
/// inspect supplied evidence; they never install or execute world changes.
/// </summary>
public static class DvValueTemplate
{
    public const int MaximumTemplateNodes = 200_000;
    public const int MaximumEvaluationNodes = 1_000_000;
    public const int MaximumDepth = 128;
    public const int MaximumTextCharacters = 16_000_000;

    private const string TemplateKind = "dv-value-template";
    private static readonly ConditionalWeakTable<object, Definition> Definitions = new();
    private static readonly IReadOnlyDictionary<string, Node> NoBindings = new Dictionary<string, Node>();

    /// <summary>Uses the catalog installed in the current R.UseContent scope.</summary>
    public static object Instantiate(string id, params object[] arguments)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(arguments);
        return new Evaluation().Instantiate(id, arguments, 0);
    }

    /// <summary>Checks every template, including branches that are not selected at runtime.</summary>
    public static void Validate(ContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var all = new Dictionary<string, Definition>(StringComparer.Ordinal);
        var data = R.record_find(catalog.Data, "data", new List<object>());
        foreach (var entry in R.Items(data))
        {
            var pair = R.Items(entry);
            var id = R.Text(pair[0]);
            var declaration = pair[1];
            var value = RequiredField(declaration, "value", id);
            var marked = TryField(value, "kind", out var kind) && kind is string text && text == TemplateKind;
            if (!marked && !id.StartsWith("template.", StringComparison.Ordinal)) continue;
            all.Add(id, DefinitionFor(id, declaration));
        }
        foreach (var (id, definition) in all)
        {
            foreach (var dependency in definition.DataDependencies)
            {
                var declaration = catalog.Get("data", dependency);
                if (declaration is false)
                    throw Error(id, "Unknown data dependency: " + dependency);
                _ = RequiredField(declaration, "value", id);
            }
            foreach (var call in definition.TemplateDependencies)
            {
                if (!all.TryGetValue(call.Id, out var target))
                    throw Error(id, "Unknown template dependency: " + call.Id);
                if (call.Arity != target.Parameters.Length)
                    throw Error(id, "Wrong argument count for template " + call.Id);
            }
        }
        CheckAcyclic(all.ToDictionary(entry => entry.Key,
            entry => (IReadOnlyCollection<string>)entry.Value.TemplateDependencies.Select(call => call.Id)
                .Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal), "template dependencies");
    }

    private static Definition DefinitionFor(string id, object declaration)
    {
        // Catalog declarations are frozen. snapshot is also safe for a caller
        // that supplied its own mutable content scope, without caching stale data.
        if (!R.IsDvList(declaration)) throw Error(id, "Missing template declaration");
        var immutable = R.snapshot(declaration);
        if (Definitions.TryGetValue(immutable, out var cached)) return cached;
        return Definitions.GetValue(immutable, value => Parse(id, RequiredField(value, "value", id)));
    }

    private enum NodeKind { Literal, Parameter, Binding, Index, Data, Template, Choose, List, Operator, Expand, First, Fold }

    private sealed class Node
    {
        internal NodeKind Kind { get; init; }
        internal object Literal { get; init; } = false;
        internal string Name { get; init; } = "";
        internal int ParameterIndex { get; init; }
        internal Node[] Children { get; init; } = [];
        internal bool Copy { get; init; }
        internal IReadOnlyDictionary<string, Node> Bindings { get; init; } = NoBindings;
    }

    private sealed record TemplateCall(string Id, int Arity);

    private sealed class Definition
    {
        internal required string[] Parameters { get; init; }
        internal required IReadOnlyDictionary<string, Node> Bindings { get; init; }
        internal required Node[] Checks { get; init; }
        internal required Node Value { get; init; }
        internal required string[] DataDependencies { get; init; }
        internal required TemplateCall[] TemplateDependencies { get; init; }
    }

    private sealed class Scope
    {
        internal required HashSet<string> Bindings { get; init; }
        internal required HashSet<string> Indices { get; init; }
    }

    private sealed class Parser(string id, string[] parameters)
    {
        private int nodes;
        private readonly Dictionary<string, int> parameterIndices = parameters
            .Select((name, index) => (name, index))
            .ToDictionary(parameter => parameter.name, parameter => parameter.index, StringComparer.Ordinal);
        private readonly HashSet<string> bindingNames = new(StringComparer.Ordinal);
        private readonly HashSet<string> indexNames = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, HashSet<string>> BindingDependencies = new(StringComparer.Ordinal);
        internal readonly HashSet<string> DataDependencies = new(StringComparer.Ordinal);
        internal readonly List<TemplateCall> TemplateDependencies = [];

        internal Dictionary<string, object> DeclareBindings(object value)
        {
            var declarations = Record(value, id);
            foreach (var name in declarations.Keys)
            {
                if (name.Length == 0 || parameterIndices.ContainsKey(name) || indexNames.Contains(name) || !bindingNames.Add(name))
                    throw Error(id, "Binding names must be unique within a template: " + name);
                BindingDependencies.Add(name, new(StringComparer.Ordinal));
            }
            return declarations;
        }

        internal Dictionary<string, Node> ParseBindings(Dictionary<string, object> declarations, Scope scope, int depth)
        {
            var result = new Dictionary<string, Node>(StringComparer.Ordinal);
            foreach (var (name, value) in declarations) result.Add(name, Expression(value, scope, name, depth));
            return result;
        }

        internal Node Expression(object value, Scope scope, string? owner, int depth)
        {
            if (++nodes > MaximumTemplateNodes || depth > MaximumDepth)
                throw Error(id, "Template exceeds its node or depth limit");
            if (value is string or bool || value is double number && double.IsFinite(number))
                return new Node { Kind = NodeKind.Literal, Literal = value };
            var fields = Record(value, id);
            var op = TextField(fields, "$", id);
            switch (op)
            {
                case "param":
                {
                    Only(fields, id, "$", "name");
                    var name = TextField(fields, "name", id);
                    if (!parameterIndices.TryGetValue(name, out var index)) throw Error(id, "Unknown parameter: " + name);
                    return new Node { Kind = NodeKind.Parameter, Name = name, ParameterIndex = index };
                }
                case "value":
                {
                    Only(fields, id, "$", "name");
                    var name = TextField(fields, "name", id);
                    if (!scope.Bindings.Contains(name)) throw Error(id, "Binding is outside the current scope: " + name);
                    if (owner is not null) BindingDependencies[owner].Add(name);
                    return new Node { Kind = NodeKind.Binding, Name = name };
                }
                case "index":
                {
                    Only(fields, id, "$", "name");
                    var name = TextField(fields, "name", id);
                    if (!scope.Indices.Contains(name)) throw Error(id, "Index is outside the current scope: " + name);
                    return new Node { Kind = NodeKind.Index, Name = name };
                }
                case "data":
                {
                    Only(fields, id, "$", "id", "copy");
                    var target = TextField(fields, "id", id);
                    if (Required(fields, "copy", id) is not bool copy) throw Error(id, "Data copy must be Bool");
                    DataDependencies.Add(target);
                    return new Node { Kind = NodeKind.Data, Name = target, Copy = copy };
                }
                case "template":
                {
                    Only(fields, id, "$", "id", "args");
                    var target = TextField(fields, "id", id);
                    var children = Expressions(Required(fields, "args", id), scope, owner, depth + 1);
                    TemplateDependencies.Add(new(target, children.Length));
                    return new Node { Kind = NodeKind.Template, Name = target, Children = children };
                }
                case "list":
                    Only(fields, id, "$", "items");
                    return new Node { Kind = NodeKind.List,
                        Children = Expressions(Required(fields, "items", id), scope, owner, depth + 1) };
                case "choose":
                {
                    Only(fields, id, "$", "args");
                    var children = Expressions(Required(fields, "args", id), scope, owner, depth + 1);
                    if (children.Length != 3) throw Error(id, "choose requires three arguments");
                    return new Node { Kind = NodeKind.Choose, Children = children };
                }
                case "expand":
                case "first":
                case "fold":
                {
                    if (op == "expand") Only(fields, id, "$", "source", "index", "bindings", "value");
                    else if (op == "fold") Only(fields, id, "$", "source", "index", "accumulator", "seed", "bindings", "value");
                    else Only(fields, id, "$", "source", "index", "bindings", "when", "value", "fallback");
                    var source = Expression(Required(fields, "source", id), scope, owner, depth + 1);
                    var seed = op == "fold" ? Expression(Required(fields, "seed", id), scope, owner, depth + 1) : null;
                    var index = TextField(fields, "index", id);
                    if (parameterIndices.ContainsKey(index) || bindingNames.Contains(index) || !indexNames.Add(index))
                        throw Error(id, "Index names must be unique within a template: " + index);
                    var declarations = DeclareBindings(Required(fields, "bindings", id));
                    var accumulator = op == "fold" ? TextField(fields, "accumulator", id) : "";
                    if (op == "fold")
                    {
                        if (parameterIndices.ContainsKey(accumulator) || indexNames.Contains(accumulator) || !bindingNames.Add(accumulator))
                            throw Error(id, "Accumulator names must be unique within a template: " + accumulator);
                        BindingDependencies.Add(accumulator, new(StringComparer.Ordinal));
                        declarations.Add(accumulator, false);
                    }
                    var inner = new Scope
                    {
                        Bindings = new(scope.Bindings, StringComparer.Ordinal),
                        Indices = new(scope.Indices, StringComparer.Ordinal)
                    };
                    inner.Bindings.UnionWith(declarations.Keys);
                    inner.Indices.Add(index);
                    var bindings = ParseBindings(declarations, inner, depth + 1);
                    if (op == "fold")
                        return new Node { Kind = NodeKind.Fold, Name = index, Literal = accumulator, Bindings = bindings,
                            Children = [source, seed!, Expression(Required(fields, "value", id), inner, owner, depth + 1)] };
                    if (op == "expand")
                        return new Node { Kind = NodeKind.Expand, Name = index, Bindings = bindings,
                            Children = [source, Expression(Required(fields, "value", id), inner, owner, depth + 1)] };
                    return new Node { Kind = NodeKind.First, Name = index, Bindings = bindings,
                        Children = [source, Expression(Required(fields, "when", id), inner, owner, depth + 1),
                            Expression(Required(fields, "value", id), inner, owner, depth + 1),
                            Expression(Required(fields, "fallback", id), scope, owner, depth + 1)] };
                }
                default:
                {
                    Only(fields, id, "$", "args");
                    var arity = Arity(op);
                    if (arity < 0) throw Error(id, "Unsupported value operator: " + op);
                    var children = Expressions(Required(fields, "args", id), scope, owner, depth + 1);
                    if (children.Length != arity) throw Error(id, op + " requires " + arity + " arguments");
                    return new Node { Kind = NodeKind.Operator, Name = op, Children = children };
                }
            }
        }

        internal Node[] Expressions(object values, Scope scope, string? owner, int depth)
        {
            if (!R.TryItems(values, out var items) || items.Count > MaximumTemplateNodes)
                throw Error(id, "Expected a finite expression list");
            var result = new Node[items.Count];
            for (var i = 0; i < result.Length; i++) result[i] = Expression(items[i], scope, owner, depth);
            return result;
        }
    }

    private static Definition Parse(string id, object value)
    {
        var fields = Record(value, id);
        Only(fields, id, "kind", "parameters", "bindings", "value", "checks");
        if (TextField(fields, "kind", id) != TemplateKind) throw Error(id, "Expected " + TemplateKind);
        if (!R.TryItems(Required(fields, "parameters", id), out var parameterValues)
            || parameterValues.Count > MaximumTemplateNodes) throw Error(id, "Expected a finite parameter list");
        var names = new HashSet<string>(StringComparer.Ordinal);
        var parameters = new string[parameterValues.Count];
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameterValues[i] is not string name || name.Length == 0 || !names.Add(name))
                throw Error(id, "Parameters must have unique nonempty text names");
            parameters[i] = name;
        }
        var parser = new Parser(id, parameters);
        var declarations = parser.DeclareBindings(Required(fields, "bindings", id));
        var scope = new Scope { Bindings = new(declarations.Keys, StringComparer.Ordinal), Indices = new(StringComparer.Ordinal) };
        var bindings = parser.ParseBindings(declarations, scope, 0);
        Node[] checks = fields.TryGetValue("checks", out var checkValues)
            ? parser.Expressions(checkValues, scope, null, 0) : [];
        var result = parser.Expression(Required(fields, "value", id), scope, null, 0);
        CheckAcyclic(parser.BindingDependencies.ToDictionary(entry => entry.Key,
            entry => (IReadOnlyCollection<string>)entry.Value, StringComparer.Ordinal), id + " binding dependencies");
        return new Definition { Parameters = parameters, Bindings = bindings, Checks = checks, Value = result,
            DataDependencies = parser.DataDependencies.ToArray(), TemplateDependencies = parser.TemplateDependencies.ToArray() };
    }

    private sealed class Frame
    {
        internal required string Template { get; init; }
        internal required object[] Parameters { get; init; }
        internal required IReadOnlyDictionary<string, Node> Bindings { get; init; }
        internal Frame? Parent { get; init; }
        internal string IndexName { get; init; } = "";
        internal double Index { get; init; }
        private Dictionary<string, object>? values;
        private HashSet<string>? resolving;
        internal Dictionary<string, object> Values => values ??= new(StringComparer.Ordinal);
        internal HashSet<string> Resolving => resolving ??= new(StringComparer.Ordinal);
    }

    private sealed class Evaluation
    {
        private int remaining = MaximumEvaluationNodes;
        private long remainingCharacters = MaximumTextCharacters;
        private readonly HashSet<string> activeTemplates = new(StringComparer.Ordinal);

        private void Spend(int depth, int cost = 1)
        {
            if (depth > MaximumDepth || cost < 0 || cost > remaining)
                throw new InvalidDataException($"Value template evaluation exceeded its {(depth > MaximumDepth ? "depth" : "node")} limit "
                    + $"(depth={depth}, remaining_nodes={remaining}, cost={cost}, templates={string.Join(" -> ", activeTemplates)})");
            remaining -= cost;
        }

        private void SpendCharacters(long count)
        {
            if (count < 0 || count > remainingCharacters)
                throw new InvalidDataException("Value template evaluation exceeded its text size limit");
            remainingCharacters -= count;
        }

        private void MeasureShownValue(object value, int depth)
        {
            Spend(depth);
            if (R.TryItems(value, out var items))
            {
                SpendCharacters(2L + Math.Max(0L, 2L * (items.Count - 1L)));
                foreach (var item in items) MeasureShownValue(item, depth + 1);
            }
            else SpendCharacters(R.Text(R.show(value)).Length);
        }

        internal object Instantiate(string id, object[] arguments, int depth, bool ownedArguments = false)
        {
            Spend(depth);
            var definition = DefinitionFor(id, R.content("data", id));
            if (arguments.Length != definition.Parameters.Length) throw Error(id, "Wrong template argument count");
            if (!activeTemplates.Add(id)) throw Error(id, "Cyclic template dependency");
            try
            {
                // Nested Arguments creates a private array for this frame.
                // The public entry still copies caller-owned storage.
                var parameters = ownedArguments ? arguments : new object[arguments.Length];
                for (var i = 0; i < arguments.Length; i++)
                {
                    if (arguments[i] is null) throw Error(id, "Template parameters cannot be null");
                    parameters[i] = arguments[i];
                }
                var frame = new Frame { Template = id, Parameters = parameters, Bindings = definition.Bindings };
                foreach (var check in definition.Checks) _ = Value(check, frame, depth + 1);
                return Value(definition.Value, frame, depth + 1);
            }
            finally { activeTemplates.Remove(id); }
        }

        private object Binding(string name, Frame frame, int depth)
        {
            for (Frame? owner = frame; owner is not null; owner = owner.Parent)
            {
                if (!owner.Bindings.TryGetValue(name, out var expression)) continue;
                if (owner.Values.TryGetValue(name, out var cached)) return cached;
                if (!owner.Resolving.Add(name)) throw Error(frame.Template, "Cyclic binding dependency: " + name);
                try
                {
                    var result = Value(expression, owner, depth + 1);
                    owner.Values.Add(name, result);
                    return result;
                }
                finally { owner.Resolving.Remove(name); }
            }
            throw Error(frame.Template, "Unknown binding: " + name);
        }

        private static object Index(string name, Frame frame)
        {
            for (Frame? owner = frame; owner is not null; owner = owner.Parent)
                if (owner.IndexName == name) return owner.Index;
            throw Error(frame.Template, "Unknown index: " + name);
        }

        private object[] Arguments(Node node, Frame frame, int depth)
        {
            var values = new object[node.Children.Length];
            for (var i = 0; i < values.Length; i++) values[i] = Value(node.Children[i], frame, depth + 1);
            return values;
        }

        private readonly struct OperatorValues(int count, object? first, object? second, object? third, object[]? array = null)
        {
            internal object this[int index]
            {
                get
                {
                    if (array is not null) return array[index];
                    if ((uint)index >= (uint)count) throw new IndexOutOfRangeException();
                    return index switch { 0 => first!, 1 => second!, _ => third! };
                }
            }
        }

        private OperatorValues OperatorArguments(Node node, Frame frame, int depth)
        {
            var count = node.Children.Length;
            if (count > 3) return new(count, null, null, null, Arguments(node, frame, depth));
            // Keep eager, left-to-right evaluation before dispatch, including
            // operators whose implementation can short-circuit its own reads.
            var first = count > 0 ? Value(node.Children[0], frame, depth + 1) : null;
            var second = count > 1 ? Value(node.Children[1], frame, depth + 1) : null;
            var third = count > 2 ? Value(node.Children[2], frame, depth + 1) : null;
            return new(count, first, second, third);
        }

        private object Value(Node node, Frame frame, int depth)
        {
            Spend(depth);
            switch (node.Kind)
            {
                case NodeKind.Literal: return node.Literal;
                case NodeKind.Parameter: return frame.Parameters[node.ParameterIndex];
                case NodeKind.Binding: return Binding(node.Name, frame, depth);
                case NodeKind.Index: return Index(node.Name, frame);
                case NodeKind.Data:
                {
                    var value = RequiredField(R.content("data", node.Name), "value", frame.Template);
                    return node.Copy ? R.copy_value(value) : value;
                }
                case NodeKind.Template: return Instantiate(node.Name, Arguments(node, frame, depth), depth + 1, ownedArguments: true);
                case NodeKind.Choose:
                    return Value(node.Children[R.Bool(Value(node.Children[0], frame, depth + 1)) ? 1 : 2], frame, depth + 1);
                case NodeKind.List:
                {
                    var result = new List<object>(node.Children.Length);
                    for (var i = 0; i < node.Children.Length; i++)
                        result.Add(Value(node.Children[i], frame, depth + 1));
                    return result;
                }
                case NodeKind.Operator: return Apply(node.Name, OperatorArguments(node, frame, depth), depth);
                case NodeKind.Expand:
                case NodeKind.First:
                case NodeKind.Fold:
                {
                    var sourceValue = Value(node.Children[0], frame, depth + 1);
                    if (!R.IsDvList(sourceValue)) throw Error(frame.Template, "Selection source must be a DV list");
                    Spend(depth, R.Items(sourceValue).Count);
                    // An iteration owns its cache; the source stays immutable
                    // while lookups may materialize mutable children elsewhere.
                    var source = R.Items(R.snapshot(sourceValue));
                    var result = node.Kind == NodeKind.Expand ? new List<object>() : null;
                    var accumulated = node.Kind == NodeKind.Fold ? Value(node.Children[1], frame, depth + 1) : false;
                    for (var i = 0; i < source.Count; i++)
                    {
                        var inner = new Frame { Template = frame.Template, Parameters = frame.Parameters,
                            Bindings = node.Bindings, Parent = frame, IndexName = node.Name, Index = i };
                        if (node.Kind == NodeKind.Fold)
                        {
                            inner.Values.Add(R.Text(node.Literal), accumulated);
                            accumulated = Value(node.Children[2], inner, depth + 1);
                        }
                        else if (node.Kind == NodeKind.First)
                        {
                            if (R.Bool(Value(node.Children[1], inner, depth + 1)))
                                return Value(node.Children[2], inner, depth + 1);
                        }
                        else
                        {
                            var expanded = Value(node.Children[1], inner, depth + 1);
                            if (!R.TryItems(expanded, out var items)) throw Error(frame.Template, "expand value must be a DV list");
                            Spend(depth, items.Count);
                            foreach (var item in items) result!.Add(item);
                        }
                    }
                    return node.Kind == NodeKind.Fold ? accumulated
                        : node.Kind == NodeKind.First ? Value(node.Children[3], frame, depth + 1) : result!;
                }
                default: throw Error(frame.Template, "Unsupported template node");
            }
        }

        private object Apply(string op, in OperatorValues a, int depth)
        {
            // This is a closed dispatch table. Content never supplies a CLR
            // method, delegate, reflection target, file path or executable code.
            switch (op)
            {
                case "value.add": return R.add(a[0], a[1]);
                case "value.sub": return R.sub(a[0], a[1]);
                case "value.mul": return R.mul(a[0], a[1]);
                case "value.div": return R.div(a[0], a[1]);
                case "value.eq": return R.eq(a[0], a[1]);
                case "value.lt": return R.lt(a[0], a[1]);
                case "value.not": return R.not(a[0]);
                case "value.floor": return R.floor(a[0]);
                case "value.range":
                    var startNumber = R.Num(a[0]); var endNumber = R.Num(a[1]);
                    if (!double.IsFinite(startNumber) || !double.IsFinite(endNumber) || startNumber < 0 || endNumber < startNumber
                        || endNumber > int.MaxValue || Math.Floor(startNumber) != startNumber || Math.Floor(endNumber) != endNumber
                        || endNumber - startNumber > MaximumEvaluationNodes) throw new InvalidDataException("Invalid finite range.");
                    var range = new List<object>();
                    for (var number = (int)startNumber; number < (int)endNumber; number++) { Spend(depth); range.Add((double)number); }
                    return range;
                case "value.cat":
                    SpendCharacters((long)R.Text(a[0]).Length + R.Text(a[1]).Length);
                    return R.cat(a[0], a[1]);
                case "value.show":
                    MeasureShownValue(a[0], depth + 1);
                    return R.show(a[0]);
                case "value.count": return R.count(a[0]);
                case "value.at": return R.at(a[0], a[1]);
                case "value.length": return R.length(a[0]);
                case "value.slice": return R.slice(a[0], a[1], a[2]);
                case "value.is_text": return R.is_text(a[0]);
                case "value.is_list": return R.is_list(a[0]);
                case "value.is_numeric_value": return R.is_numeric_value(a[0]);
                case "value.content": return R.content(a[0], a[1]);
                case "value.text_join":
                {
                    var parts = R.Items(a[0]);
                    Spend(depth, parts.Count);
                    foreach (var part in parts) SpendCharacters(R.Text(part).Length);
                    return R.text_join(a[0]);
                }
                case "value.pair": return F_dv_pair(a[0], a[1]);
                case "value.single": return F_dv_single(a[0]);
                case "value.list3": return F_dv_list3(a[0], a[1], a[2]);
                case "value.get": return F_dv_get(a[0], a[1]);
                case "value.find": return F_dv_find(a[0], a[1], a[2]);
                case "value.with": return F_dv_with(a[0], a[1], a[2]);
                case "value.with_many": return F_dv_with_many(a[0], a[1]);
                case "value.without": return F_dv_without(a[0], a[1]);
                case "value.has": return F_dv_has(a[0], a[1]);
                case "value.has_key": return F_dv_has_key(a[0], a[1]);
                case "value.clone": return F_dv_clone(a[0]);
                case "value.same": return F_dv_same(a[0], a[1]);
                case "value.append": return F_dv_append(a[0], a[1]);
                case "value.min": return F_dv_min(a[0], a[1]);
                case "value.max": return F_dv_max(a[0], a[1]);
                case "value.and": return F_dv_and(a[0], a[1]);
                case "value.or": return F_dv_or(a[0], a[1]);
                case "value.record_like": return F_dv_record_like(a[0]);
                case "value.require": return F_dv_require(a[0], a[1]);
                case "value.snapshot": return R.snapshot(a[0]);
                case "value.copy": return R.copy_value(a[0]);
                case "value.concat":
                {
                    var count = R.Items(a[1]).Count;
                    Spend(depth, count);
                    var result = F_dv_clone(a[0]);
                    for (var i = 0; i < count; i++) _ = R.push(result, R.at(a[1], (double)i));
                    return result;
                }
                case "domain.ref": return F_dv_ref(a[0], a[1], a[2]);
                case "domain.scoped_ref": return F_dv_scoped_ref(a[0], a[1], a[2], a[3]);
                case "domain.fact": return F_dv_fact(a[0], a[1]);
                case "domain.all": return F_dv_all(a[0]);
                case "domain.not": return F_dv_not(a[0]);
                case "domain.true": return F_dv_true();
                case "domain.at_least": return F_dv_at_least(a[0], a[1]);
                case "domain.held_for": return F_dv_held_for(a[0], a[1], a[2], a[3]);
                case "domain.assign": return F_dv_assign(a[0], a[1]);
                case "domain.increment": return F_dv_increment(a[0], a[1]);
                case "domain.axiom": return F_dv_axiom(a[0], a[1], a[2], a[3]);
                case "domain.contract": return F_dv_contract(a[0], a[1], a[2], a[3], a[4]);
                case "domain.instance": return F_dv_instance(a[0], a[1]);
                case "domain.field": return F_dv_field(a[0]);
                case "domain.cell": return F_dv_cell(a[0], a[1], a[2], a[3]);
                case "domain.unknown": return F_dv_unknown();
                case "domain.object": return F_dv_object(a[0], a[1], a[2]);
                case "domain.binding": return F_dv_binding(a[0], a[1], a[2]);
                case "domain.criterion": return F_dv_criterion(a[0], a[1], a[2], a[3], a[4]);
                case "domain.request": return F_dv_request(a[0], a[1], a[2]);
                case "domain.person_policy": return F_dv_person_policy(a[0], a[1], a[2], a[3], a[4]);
                case "domain.compose": return ComposeValue(a[0]);
                case "domain.read": return F_dv_read(a[0], a[1]);
                case "domain.truth": return F_dv_truth(a[0], a[1]);
                case "domain.uncontested": return F_dv_uncontested(a[0]);
                case "domain.surface_parts": return F_dvs_surface_parts(a[0]);
                case "domain.any_chain":
                {
                    var count = R.Items(a[1]).Count;
                    Spend(depth, count);
                    var result = a[0];
                    for (var i = 0; i < count; i++)
                        result = F_dv_not(F_dv_all(F_dv_pair(F_dv_not(result), F_dv_not(R.at(a[1], (double)i)))));
                    return result;
                }
                default: throw new InvalidDataException("Unsupported value operator: " + op);
            }
        }
    }

    private static int Arity(string op) => op switch
    {
        "domain.true" or "domain.unknown" => 0,
        "value.not" or "value.floor" or "value.show" or "value.count" or "value.length"
            or "value.is_text" or "value.is_list" or "value.is_numeric_value" or "value.text_join"
            or "value.single" or "value.clone" or "value.record_like" or "value.snapshot" or "value.copy"
            or "domain.all" or "domain.not" or "domain.field" or "domain.uncontested" or "domain.surface_parts" or "domain.compose" => 1,
        "value.add" or "value.sub" or "value.mul" or "value.div" or "value.eq" or "value.lt" or "value.cat"
            or "value.at" or "value.content" or "value.pair" or "value.get" or "value.with_many" or "value.without"
            or "value.has" or "value.has_key" or "value.same" or "value.append" or "value.min" or "value.max"
            or "value.and" or "value.or" or "value.require" or "value.concat" or "domain.fact" or "domain.at_least"
            or "domain.assign" or "domain.increment" or "domain.instance" or "domain.read" or "domain.truth"
            or "domain.any_chain" or "value.range" => 2,
        "value.slice" or "value.list3" or "value.find" or "value.with" or "domain.ref" or "domain.object"
            or "domain.binding" or "domain.request" => 3,
        "domain.scoped_ref" or "domain.held_for" or "domain.axiom" or "domain.cell" => 4,
        "domain.contract" or "domain.criterion" or "domain.person_policy" => 5,
        _ => -1
    };

    private static void CheckAcyclic(IReadOnlyDictionary<string, IReadOnlyCollection<string>> graph, string label)
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var completed = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string name, int depth)
        {
            if (depth > MaximumDepth) throw new InvalidDataException("Value template dependency depth exceeded: " + label);
            if (completed.Contains(name)) return;
            if (!visiting.Add(name)) throw new InvalidDataException("Cyclic value template dependency: " + label + "/" + name);
            if (!graph.TryGetValue(name, out var dependencies)) throw new InvalidDataException("Unknown value template dependency: " + name);
            foreach (var dependency in dependencies) Visit(dependency, depth + 1);
            visiting.Remove(name);
            completed.Add(name);
        }
        foreach (var name in graph.Keys) Visit(name, 0);
    }

    private static Dictionary<string, object> Record(object value, string id)
    {
        if (!R.TryItems(value, out var entries) || entries.Count > MaximumTemplateNodes)
            throw Error(id, "Expected a finite template record");
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!R.TryItems(entry, out var pair) || pair.Count != 2 || pair[0] is not string key)
                throw Error(id, "Expected text-keyed template record entries");
            if (!result.TryAdd(key, pair[1])) throw Error(id, "Duplicate template field: " + key);
        }
        return result;
    }

    private static bool TryField(object value, string name, out object result)
    {
        if (R.TryItems(value, out var entries))
            foreach (var entry in entries)
                if (R.TryItems(entry, out var pair) && pair.Count == 2 && pair[0] is string key && key == name)
                { result = pair[1]; return true; }
        result = false;
        return false;
    }

    private static object RequiredField(object value, string name, string id)
        => TryField(value, name, out var field) ? field : throw Error(id, "Missing field: " + name);

    private static object Required(IReadOnlyDictionary<string, object> fields, string name, string id)
        => fields.TryGetValue(name, out var value) ? value : throw Error(id, "Missing field: " + name);

    private static string TextField(IReadOnlyDictionary<string, object> fields, string name, string id)
        => Required(fields, name, id) is string text && text.Length != 0
            ? text : throw Error(id, "Expected nonempty Text: " + name);

    private static void Only(IReadOnlyDictionary<string, object> fields, string id, params string[] permitted)
    {
        foreach (var field in fields.Keys)
            if (!permitted.Contains(field, StringComparer.Ordinal)) throw Error(id, "Unsupported template field: " + field);
    }

    // The compatibility frontend only translates its value ABI. Scene assembly
    // has one implementation in the independent runtime.
    private static object ComposeValue(object value)
    {
        var remaining = MaximumEvaluationNodes;
        object ToJsonValue(object source, int depth)
        {
            if (depth > MaximumDepth || --remaining < 0) throw new InvalidDataException("Scene value conversion budget exceeded.");
            if (source is string or bool or double) return source;
            if (source is Rational rational) return new Dictionary<string, string> { ["$dv_rational"] = rational.ToString() };
            if (R.TryItems(source, out var items)) return items.Select(item => ToJsonValue(item, depth + 1)).ToArray();
            throw new InvalidDataException("Unsupported scene value.");
        }
        object FromJsonValue(JsonElement source, int depth)
        {
            if (depth > MaximumDepth || --remaining < 0) throw new InvalidDataException("Scene value conversion budget exceeded.");
            return source.ValueKind switch
            {
                JsonValueKind.Object when source.TryGetProperty("$dv_rational", out var exact) => Rational.From(exact.GetString()!),
                JsonValueKind.Object => source.EnumerateObject().Select(property => (object)new List<object> { property.Name, FromJsonValue(property.Value, depth + 1) }).ToList(),
                JsonValueKind.Array => source.EnumerateArray().Select(item => FromJsonValue(item, depth + 1)).ToList(),
                JsonValueKind.String => source.GetString()!,
                JsonValueKind.Number => R.Num(source.GetDouble()),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new InvalidDataException("Unsupported composed scene value.")
            };
        }
        var options = new JsonSerializerOptions { MaxDepth = MaximumDepth * 2 + 16 };
        var input = JsonSerializer.SerializeToElement(ToJsonValue(value, 0), options);
        remaining = MaximumEvaluationNodes;
        return FromJsonValue(DuelView.Runtime.SceneComposer.Materialize(input), 0);
    }

    private static InvalidDataException Error(string id, string message)
        => new("Value template '" + id + "': " + message);
}
