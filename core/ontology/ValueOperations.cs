// @mechanism: dual_ontology_state
// @layer: core/ontology
// @contracts: ValueOperations

using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using System.Buffers;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;
using System.Threading;
using System;
using static DuelView.Core.DVKernel;

// --- Source: ValueOperations.cs ---
namespace DuelView.Core
{


/// <summary>Values / Records: native Core mechanisms.</summary>
public static partial class DVKernel
{
    public static object F_dv_pair(object v_a, object v_b)
    {
        var result = R.NewValueList(2);
        result.Add(v_a);
        result.Add(v_b);
        return result;
    }

    public static object F_dv_single(object v_a)
    {
        var result = R.NewValueList(1);
        result.Add(v_a);
        return result;
    }

    public static object F_dv_list3(object v_a, object v_b, object v_c)
    {
        var result = R.NewValueList(3);
        result.Add(v_a);
        result.Add(v_b);
        result.Add(v_c);
        return result;
    }

    public static object F_dv_and(object v_a, object v_b)
    {
        if (R.Bool(v_a))
        {
            return v_b;
        }
        else
        {
            return false;
        }
    }

    public static object F_dv_or(object v_a, object v_b)
    {
        if (R.Bool(v_a))
        {
            return true;
        }
        else
        {
            return v_b;
        }
    }

    public static object F_dv_require(object v_condition, object v_message)
    {
        if (R.Bool(v_condition))
        {
            return true;
        }
        else
        {
            return R.fail(v_message);
        }
    }

    public static object F_dv_clone(object v_value)
    {
        return R.copy_value(v_value);
    }

    public static object F_dv_same(object v_a, object v_b)
    {
        return R.same_value(v_a, v_b);
    }

    public static object F_dv_append(object v_items, object v_value)
    {
        object v_r__616 = F_dv_clone(v_items);
        _ = R.push(v_r__616, R.snapshot(v_value));
        return v_r__616;
    }

    public static object F_dv_has(object v_items, object v_value)
    {
        return R.has_value(v_items, v_value);
    }

    public static object F_dv_find(object v_record, object v_key, object v_fallback)
    {
        return R.record_find(v_record, v_key, v_fallback);
    }

    public static object F_dv_has_key(object v_record, object v_key)
    {
        return R.not(R.lt(R.record_index(v_record, v_key), R.number("0")));
    }

    public static object F_dv_record_like(object value) => R.record_like(value);

    public static object F_dv_get(object v_record, object v_key)
    {
        return R.record_find(v_record, v_key, "__dv_missing_field_sentinel__");
    }

    public static object F_dv_with(object v_record, object v_key, object v_value)
    {
        return R.record_with(v_record, v_key, v_value);
    }

    public static object F_dv_without(object v_record, object v_key)
    {
        v_record = R.snapshot(v_record);
        object v_result__1787 = R.NewValueList(0);
        object v_i__1807 = R.number("0");
        while (R.Bool(R.lt(v_i__1807, R.count(v_record))))
        {
            object v_entry__1859 = R.at(v_record, v_i__1807);
            if (R.Bool(R.eq(R.at(v_entry__1859, R.number("0")), v_key)))
            {
                _ = false;
            }
            else
            {
                _ = R.push(v_result__1787, R.snapshot(v_entry__1859));
            }
            v_i__1807 = R.add(v_i__1807, R.number("1"));
        }
        return v_result__1787;
    }

    public static object F_dv_unit(object v_x)
    {
        _ = F_dv_require(F_dv_and(R.not(R.lt(v_x, R.number("0"))), R.not(R.lt(R.number("1"), v_x))), "Expected weight in [0,1]");
        return v_x;
    }

    public static object F_dv_unknown()
    {
        var result = R.NewValueList(6);
        result.Add(F_dv_pair("known", false));
        result.Add(F_dv_pair("value", false));
        result.Add(F_dv_pair("confidence", R.number("0")));
        result.Add(F_dv_pair("time", R.sub(R.number("0"), R.number("1"))));
        result.Add(F_dv_pair("claims", R.NewValueList(0)));
        result.Add(F_dv_pair("conflicted", false));
        return result;
    }

    public static object F_dv_min(object v_a, object v_b)
    {
        if (R.Bool(R.lt(v_a, v_b)))
        {
            return v_a;
        }
        else
        {
            return v_b;
        }
    }

    public static object F_dv_max(object v_a, object v_b)
    {
        if (R.Bool(R.lt(v_a, v_b)))
        {
            return v_b;
        }
        else
        {
            return v_a;
        }
    }

    public static object F_dv_cell(object v_value, object v_confidence, object v_time, object v_claims)
    {
        var cell = R.NewValueList(4);
        cell.Add(F_dv_pair("known", true));
        cell.Add(F_dv_pair("value", R.snapshot(v_value)));
        cell.Add(F_dv_pair("confidence", F_dv_unit(v_confidence)));
        cell.Add(F_dv_pair("time", v_time));
        var updates = R.NewValueList(3);
        updates.Add(F_dv_pair("claims", R.NewValueList(0)));
        updates.Add(F_dv_pair("basis", v_claims));
        updates.Add(F_dv_pair("conflicted", false));
        return F_dv_with_many(cell, updates);
    }

    public static object F_dv_with_many(object v_record, object v_updates)
    {
        return R.record_with_many(v_record, v_updates);
    }

    public static object F_dv_with_path(object v_record, object v_keys, object v_depth, object v_value)
    {
        object v_key__11950 = R.at(v_keys, v_depth);
        object v_result__11976 = R.NewValueList(0);
        object v_found__11996 = false;
        object v_i__12018 = R.number("0");
        while (R.Bool(R.lt(v_i__12018, R.count(v_record))))
        {
            object v_entry__12068 = R.at(v_record, v_i__12018);
            if (R.Bool(R.eq(R.at(v_entry__12068, R.number("0")), v_key__11950)))
            {
                object v_next__12130 = false;
                if (R.Bool(R.eq(R.add(v_depth, R.number("1")), R.count(v_keys))))
                {
                    v_next__12130 = F_dv_clone(v_value);
                }
                else
                {
                    v_next__12130 = F_dv_with_path(F_dv_get(v_record, v_key__11950), v_keys, R.add(v_depth, R.number("1")), v_value);
                }
                _ = R.push(v_result__11976, F_dv_pair(F_dv_clone(R.at(v_entry__12068, R.number("0"))), v_next__12130));
                v_found__11996 = true;
            }
            else
            {
                _ = R.push(v_result__11976, R.snapshot(v_entry__12068));
            }
            v_i__12018 = R.add(v_i__12018, R.number("1"));
        }
        if (R.Bool(R.not(v_found__11996)))
        {
            object v_next__12523 = false;
            if (R.Bool(R.eq(R.add(v_depth, R.number("1")), R.count(v_keys))))
            {
                v_next__12523 = F_dv_clone(v_value);
            }
            else
            {
                v_next__12523 = F_dv_with_path(R.NewValueList(0), v_keys, R.add(v_depth, R.number("1")), v_value);
            }
            _ = R.push(v_result__11976, F_dv_pair(F_dv_clone(v_key__11950), v_next__12523));
        }
        return v_result__11976;
    }

    public static object F_dvs_replace(object v_items, object v_index, object v_value)
    {
        return R.list_with_index(R.snapshot(v_items), v_index, v_value);
    }

    /// <summary>First matching identity in an ordered record list; -1 when absent.</summary>
    public static object F_dv_index_by_id(object items, object id)
    {
        var entries = R.Items(items);
        for (var index = 0; index < entries.Count; index++)
            if (R.Bool(R.eq(F_dv_get(entries[index], "id"), id))) return (double)index;
        return -1d;
    }
}
}

// --- Source: DvValueTemplate.cs ---
namespace DuelView.Core
{


/// <summary>
/// Instantiates finite, data-only value templates. Operators construct values or
/// inspect supplied evidence; they never install or execute world changes.
/// </summary>
public static class DvValueTemplate
{
    public static Func<JsonElement, JsonElement>? SceneComposerHook { get; set; }
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
        var materialized = SceneComposerHook != null ? SceneComposerHook(input) : input;
        return FromJsonValue(materialized, 0);
    }

    private static InvalidDataException Error(string id, string message)
        => new("Value template '" + id + "': " + message);
}
}

// --- Source: Predicates.cs ---
namespace DuelView.Compiled
{


// Shared runtime implementation; state remains owned by the execution context.
public static partial class R
{
    private sealed class DirtyBinding
    {
        internal string Facet = "", Field = "";
        internal readonly SortedSet<int> Rules = new();
        internal object Previous = PredicateMissingCell;
    }
    private sealed class DirtyObject
    {
        internal object Previous = PredicateMissingCell;
        internal readonly Dictionary<(string, string), DirtyBinding> Bindings = new();
    }
    private sealed class DirtyQueue
    {
        internal readonly Dictionary<string, DirtyObject> Objects = new(StringComparer.Ordinal);
        internal readonly SortedSet<int> Pending = new();
        internal readonly List<string> Laws = new();
        internal object Adoptions = PredicateMissingCell;
        internal int Count;
    }
    private sealed class DirtyQueues
    {
        internal readonly Dictionary<string, DirtyQueue> Subjects = new(StringComparer.Ordinal);
    }
    private static object DirtyLookup(object record, string key) => record_find(record, key, PredicateMissingCell);
    private static DirtyQueue DirtyBuild(FrozenList index)
    {
        var queue = new DirtyQueue();
        var entries = Items(index);
        queue.Count = entries.Count;
        for (var i = 0; i < entries.Count; i++)
        {
            queue.Pending.Add(i);
            queue.Laws.Add(Text(PredicateField(entries[i], "law")));
            foreach (var reference in Items(PredicateField(entries[i], "refs")))
            {
                var id = Text(PredicateField(reference, "object"));
                var facet = Text(PredicateField(reference, "facet"));
                var field = Text(PredicateField(reference, "field"));
                if (!queue.Objects.TryGetValue(id, out var entity)) queue.Objects.Add(id, entity = new DirtyObject());
                if (!entity.Bindings.TryGetValue((facet, field), out var binding))
                    entity.Bindings.Add((facet, field), binding = new DirtyBinding { Facet = facet, Field = field });
                binding.Rules.Add(i);
            }
        }
        return queue;
    }
    public static object dirty_next(object worldValue, object indexValue, object afterValue)
    {
        var world = (FrozenList)snapshot(worldValue);
        var index = (FrozenList)snapshot(indexValue);
        var after = checked((int)Num(afterValue));
        var subject = Text(PredicateField(world, "subject"));
        var holders = Caches.DirtyIndices.GetOrCreateValue(index);
        lock (holders)
        {
            if (!holders.Subjects.TryGetValue(subject, out var queue))
                holders.Subjects.Add(subject, queue = DirtyBuild(index));
            var objects = PredicateField(world, "objects");
            // Enumerate relevant objects, not rules. Unchanged immutable objects
            // stop traversal; a changed cell wakes every declared dependent rule.
            foreach (var pair in queue.Objects)
            {
                var entity = DirtyLookup(objects, pair.Key);
                var bindingObject = pair.Value;
                if (ReferenceEquals(entity, bindingObject.Previous)) continue;
                var existenceChanged = ReferenceEquals(entity, PredicateMissingCell) != ReferenceEquals(bindingObject.Previous, PredicateMissingCell);
                foreach (var binding in bindingObject.Bindings.Values)
                {
                    var cell = ReferenceEquals(entity, PredicateMissingCell) ? PredicateMissingCell
                        : PredicateReadCell(entity, binding.Facet, binding.Field);
                    if (existenceChanged || !ReferenceEquals(cell, binding.Previous))
                        queue.Pending.UnionWith(binding.Rules);
                    binding.Previous = cell;
                }
                bindingObject.Previous = entity;
            }
            var adoptions = PredicateField(world, "adoptions");
            if (!ReferenceEquals(adoptions, queue.Adoptions))
            {
                for (var i = 0; i < queue.Laws.Count; i++)
                    if (ReferenceEquals(queue.Adoptions, PredicateMissingCell)
                        || !ReferenceEquals(DirtyLookup(adoptions, queue.Laws[i]), DirtyLookup(queue.Adoptions, queue.Laws[i])))
                        queue.Pending.Add(i);
                queue.Adoptions = adoptions;
            }
            // Earlier dirty rules remain queued for the next original pass.
            // Later rules affected by this pass are inserted before its next step.
            var available = queue.Pending.GetViewBetween(after + 1, queue.Count);
            if (available.Count == 0) return NumberValue(queue.Count);
            var next = available.Min;
            queue.Pending.Remove(next);
            return NumberValue(next);
        }
    }

    private sealed class PredicateCellCache
    {
        internal readonly Dictionary<(string Facet, string Field), object> Cells = new();
    }
    private static readonly object PredicateMissingCell = new();
    private static object PredicateReadCell(object entity, object facet, object field)
    {
        var facets = PredicateField(entity, "facets");
        if (Num(record_index(facets, facet)) < 0) return PredicateMissingCell;
        var fields = record_find(facets, facet, "__dv_missing_field_sentinel__");
        if (Num(record_index(fields, field)) < 0) return PredicateMissingCell;
        return record_find(fields, field, "__dv_missing_field_sentinel__");
    }
    private static object PredicateReadCellCached(object entity, object facet, object field, ValueCaches caches)
    {
        // Non-text keys retain runtime EqValue/reference semantics unchanged.
        if (entity is not FrozenList frozen || facet is not string facetName || field is not string fieldName)
            return PredicateReadCell(entity, facet, field);
        var cache = caches.PredicateCells.GetOrCreateValue(frozen);
        lock (cache)
        {
            var key = (facetName, fieldName);
            if (cache.Cells.TryGetValue(key, out var cell)) return cell;
            // Store only successful reads or declared absence, never exceptions.
            cell = PredicateReadCell(frozen, facetName, fieldName);
            // Bound arbitrary missing-field probes; eviction changes work only.
            if (cache.Cells.Count >= 256) cache.Cells.Clear();
            cache.Cells.Add(key, cell);
            return cell;
        }
    }
    public static object lazyinternal_enabled() => Caches.LazyInternalEnabled ? BoolTrue : BoolFalse;
    public static object predicate_enabled() => Caches.PredicateEnabled ? BoolTrue : BoolFalse;
    private static object PredicateField(object record, string key) => record_find(record, key, "__dv_missing_field_sentinel__");
    // False delegates unsupported representations to the original DV path.
    // Borrow only immutable graphs. No mutable cell or world result is cached.
    public static object predicate_leaf(object world, object proposition)
    {
        // Do not substitute a mutable owner's backbone: reference-valued keys
        // and subjects must retain the original lookup/equality behavior.
        var frozenWorld = world as FrozenList;
        var frozenProposition = proposition as FrozenList;
        if (frozenWorld is null || frozenProposition is null) return BoolFalse;
        var op = PredicateField(frozenProposition, "op");
        if (!EqValue(op, "equals") && !EqValue(op, "at_least")) return BoolFalse;
        var reference = PredicateField(frozenProposition, "ref");
        // Access rejection precedes missing-object handling, as in dv_read.
        if (Num(record_index(reference, "subject")) >= 0)
        {
            var subject = PredicateField(reference, "subject");
            if (!EqValue(subject, "") && !EqValue(subject, PredicateField(frozenWorld, "subject")))
                throw new Exception("Cross-subject reference access");
        }
        var objects = PredicateField(frozenWorld, "objects");
        var id = PredicateField(reference, "object");
        if (Num(record_index(objects, id)) < 0) return "unknown";
        var entity = record_find(objects, id, "__dv_missing_field_sentinel__");
        // dv_read reads both ref fields to build its key before reading facets.
        var facet = PredicateField(reference, "facet");
        var field = PredicateField(reference, "field");
        // World/subject checks and both reference-field reads precede reuse.
        // Immutable object identity permits reuse across unrelated world updates.
        var caches = Caches;
        var cell = caches.PredicateCacheEnabled
            ? PredicateReadCellCached(entity, facet, field, caches)
            : PredicateReadCell(entity, facet, field);
        if (ReferenceEquals(cell, PredicateMissingCell)) return "unknown";
        // dv_or arguments are eager: missing conflicted errors even if unknown.
        // Its boolean type is checked only when known=true.
        if (caches.MaskEnabled && TryCellFlags(cell, out var flags))
        {
            if (flags != 1) return "unknown";
        }
        else
        {
            var known = Bool(PredicateField(cell, "known"));
            var conflicted = PredicateField(cell, "conflicted");
            if (!known || Bool(conflicted)) return "unknown";
        }
        var actual = PredicateField(cell, "value");
        var expected = PredicateField(frozenProposition, "value");
        if (EqValue(op, "equals")) return SameValue(actual, expected) ? "true" : "false";
        return Bool(lt(actual, expected)) ? "false" : "true";
    }
    public static object stateindex_enabled() => Caches.StateIndexEnabled ? BoolTrue : BoolFalse;
    public static object cow_enabled() => Caches.CowEnabled ? BoolTrue : BoolFalse;
    public static object mask_enabled() => Caches.MaskEnabled ? BoolTrue : BoolFalse;
    // Inspect record spines without exposing/thawing any child. Irregular ABI
    // inputs return to the existing DV implementation before any write occurs.
    private static bool TryRawField(object record, string key, out object value, out int slot)
    {
        value = false; slot = -1;
        if (!TryItems(record, out var entries)) return false;
        for (var i = 0; i < entries.Count; i++)
        {
            if (!TryItems(entries[i], out var pair) || pair.Count != 2 || pair[0] is not string name) return false;
            if (name == key) { value = pair[1]; slot = i; }
        }
        return true;
    }
    // Reuse the ordinary record slot index; the only extra metadata certifies
    // strict pair shape. Irregular/duplicate-key ABI values keep the DV path.
    private static bool TryStateField(object record, string key, out object value, out int slot,
        out Dictionary<string, int>? index)
    {
        value = false; slot = -1; index = null;
        if (!TryItems(record, out var entries)) return false;
        var frozen = UnchangedSnapshot(record, Caches);
        if (frozen is not null)
        {
            if (!Bool(record_like_cached(frozen))) return false;
            index = FrozenIndex(frozen);
        }
        else if (record is List<object> mutable)
        {
            var state = MutableStates.GetOrCreateValue(mutable);
            if (!state.StatePairShapeChecked)
            {
                state.StatePairShape = RecordLikeValue(mutable);
                state.StatePairShapeChecked = true;
            }
            if (!state.StatePairShape) return false;
            index = MutableIndex(mutable); // links mutable key pairs for invalidation
        }
        if (index is null || index.Count != entries.Count) return false;
        if (index.TryGetValue(key, out slot)) value = RawItem(entries[slot], 1);
        else slot = -1;
        return true;
    }
    private static object StageStoreIndexed(object owned, object reference, object cell)
    {
        if (!TryRawField(reference, "object", out var rawId, out var idSlot) || idSlot < 0 || rawId is not string id
            || !TryRawField(reference, "facet", out var rawFacet, out var facetSlot) || facetSlot < 0 || rawFacet is not string facet
            || !TryRawField(reference, "field", out var rawField, out var fieldSlot) || fieldSlot < 0 || rawField is not string field)
            return BoolFalse;
        if (!TryStateField(owned, "objects", out var objects, out var objectsSlot, out _) || objectsSlot < 0
            || !TryStateField(objects, id, out var entity, out var entitySlot, out var objectIndex) || entitySlot < 0
            || !TryStateField(entity, "facets", out var facets, out var facetsSlot, out _) || facetsSlot < 0
            || !TryStateField(facets, facet, out var fields, out var fieldsSlot, out _)) return BoolFalse;
        if (fieldsSlot < 0) fields = new List<object>();
        if (!TryStateField(fields, field, out _, out _, out _)) return BoolFalse;
        var updated = record_with(entity, "facets", record_with(facets, facet, record_with(fields, field, cell)));
        var mutableObjects = ReadItem(ReadItem(owned, objectsSlot), 1);
        // The selected key is unchanged and unique. Preserve the validated slots
        // after the value-only write; ancestor snapshots are still invalidated.
        replace(mutableObjects, NumberValue(entitySlot), FrozenTextPair(id, snapshot(updated)));
        var state = MutableStates.GetOrCreateValue(MutableItems(mutableObjects));
        state.Index = objectIndex;
        state.IndexChecked = true;
        state.StatePairShape = true;
        state.StatePairShapeChecked = true;
        return owned;
    }
    public static object stage_store_cow(object owned, object reference, object cell)
    {
        // Admission and law/effect order remain DV's responsibility. This fast
        // path substitutes only the private builder's record update operation.
        if (owned is not List<object>) return BoolFalse;
        if (Caches.StateIndexEnabled)
        {
            var indexed = StageStoreIndexed(owned, reference, cell);
            if (indexed is not false) return indexed;
        }
        if (!TryRawField(reference, "object", out var rawId, out var idSlot) || idSlot < 0 || rawId is not string id
            || !TryRawField(reference, "facet", out var rawFacet, out var facetSlot) || facetSlot < 0 || rawFacet is not string facet
            || !TryRawField(reference, "field", out var rawField, out var fieldSlot) || fieldSlot < 0 || rawField is not string field)
            return BoolFalse;
        if (!TryRawField(owned, "objects", out var objects, out var objectsSlot) || objectsSlot < 0
            || !TryRawField(objects, id, out var entity, out var entitySlot) || entitySlot < 0
            || !TryRawField(entity, "facets", out var facets, out var facetsSlot) || facetsSlot < 0
            || !TryRawField(facets, facet, out var fields, out var fieldsSlot)) return BoolFalse;
        if (fieldsSlot < 0) fields = new List<object>();
        if (!TryRawField(fields, field, out _, out _)) return BoolFalse;
        var updated = record_with(entity, "facets", record_with(facets, facet, record_with(fields, field, cell)));
        // Materialize just the owned world->objects spine. The old DV loop read
        // objects[i] through at(), thawing every unrelated object pair. Raw key
        // reads preserve those frozen siblings and still update every duplicate.
        var mutableObjects = ReadItem(ReadItem(owned, objectsSlot), 1);
        var entries = Items(mutableObjects);
        for (var i = 0; i < entries.Count; i++)
            if (Equals(RawItem(entries[i], 0), id))
                replace(mutableObjects, NumberValue(i), new List<object> { id, copy_value(updated) });
        return owned;
    }
    private sealed record CellFlags(byte Bits);
    private static bool TryCellFlags(object cell, out byte bits)
    {
        bits = 0;
        var caches = Caches;
        var frozen = UnchangedSnapshot(cell, caches);
        if (frozen is null) return false;
        if (!caches.CellMasks.TryGetValue(frozen, out var cached))
        {
            // Malformed cells retain the caller's exact original error order.
            var known = record_find(frozen, "known", MissingSentinel);
            if (known is not bool knownFlag) return false;
            var conflicted = record_find(frozen, "conflicted", MissingSentinel);
            if (conflicted is not bool conflictFlag) return false;
            var value = new CellFlags((byte)((knownFlag ? 1 : 0) | (conflictFlag ? 2 : 0)));
            cached = caches.CellMasks.GetValue(frozen, _ => value);
        }
        bits = cached.Bits;
        if (caches.MaskVerify)
        {
            var known = Bool(PredicateField(frozen, "known"));
            var conflicted = Bool(PredicateField(frozen, "conflicted"));
            var encoded = (byte)((known ? 1 : 0) | (conflicted ? 2 : 0));
            if (bits != encoded || known != ((bits & 1) != 0) || conflicted != ((bits & 2) != 0) || (bits & ~3) != 0)
                throw new InvalidDataException("Cell mask/record bidirectional mismatch: known/conflicted");
        }
        return true;
    }
    public static object cell_uncontested(object cell)
    {
        if (Caches.MaskEnabled && TryCellFlags(cell, out var flags)) return flags == 1 ? BoolTrue : BoolFalse;
        // dv_and receives known before eagerly evaluating not(conflicted).
        var known = PredicateField(cell, "known");
        var conflicted = Bool(PredicateField(cell, "conflicted"));
        return Bool(known) && !conflicted ? BoolTrue : BoolFalse;
    }
    private static readonly object MissingSentinel = new();

    // Structural value equality reads existing branches without materializing mutable copies.
    public static object same_value(object a, object b) => SameValue(a, b) ? BoolTrue : BoolFalse;
    private static bool SameValue(object a, object b)
    {
        if (ReferenceEquals(a, b) || EqValue(a, b)) return true;
        if (!TryItems(a, out var left) || !TryItems(b, out var right) || left.Count != right.Count) return false;
        var leftBase = a as FrozenList;
        var rightBase = b as FrozenList;
        if (leftBase is null && left is List<object> leftMutable && MutableStates.TryGetValue(leftMutable, out var ls)) leftBase = ls.Backbone;
        if (rightBase is null && right is List<object> rightMutable && MutableStates.TryGetValue(rightMutable, out var rs)) rightBase = rs.Backbone;
        if (leftBase is not null && ReferenceEquals(leftBase, rightBase)) return true;
        // The snapshot identity hash distinguishes exact/floating representation.
        // Only the value-compatible membership hash may reject structural equality.
        // Numeric collisions deliberately fall through to the original ordered comparison.
        if (leftBase is not null && rightBase is not null
            && TryMembershipHash(leftBase, out var leftHash)
            && TryMembershipHash(rightBase, out var rightHash) && leftHash != rightHash) return false;
        for (var i = 0; i < left.Count; i++)
            if (!SameValue(left[i], right[i])) return false;
        return true;
    }
    // Membership is a structural-value operation; reading never thaws child lists.
    public static object has_value(object items, object value) => HasValue(items, value) ? BoolTrue : BoolFalse;
    private static bool HasValue(object items, object value)
    {
        var values = Items(items);
        if (value is string name)
        {
            var frozen = items as FrozenList;
            if (frozen is null && values is List<object> mutable && MutableStates.TryGetValue(mutable, out var state)) frozen = state.Backbone;
            if (frozen is not null && values.Count >= 8) return FrozenHasString(frozen, name);
            for (var i = 0; i < values.Count; i++)
            {
                var v = values[i];
                if (ReferenceEquals(v, name) || (v is string s && string.Equals(s, name, StringComparison.Ordinal)))
                    return true;
            }
            return false;
        }
        // Index only an already-frozen container or an unchanged mutable copy.
        // Unsupported/non-finite atoms retain the original ordered scan.
        if (values.Count >= 8)
        {
            var frozen = items as FrozenList;
            if (frozen is null && values is List<object> mutable && MutableStates.TryGetValue(mutable, out var state)) frozen = state.Backbone;
            if (frozen is not null && FrozenMembership(frozen) is { } index
                && TryQueryMembershipHash(value, 8, out var hash))
            {
                if (!index.TryGetValue(hash, out var candidates)) return false;
                foreach (var candidate in candidates)
                    if (ReferenceEquals(candidate, value) || SameValue(candidate, value)) return true;
                return false;
            }
        }
        for (var i = 0; i < values.Count; i++)
            if (ReferenceEquals(values[i], value) || SameValue(values[i], value)) return true;
        return false;
    }
    // Ordered independent-copy record update; unchanged branches stay immutable until read.
}
}

// --- Source: Conditions.cs ---
namespace DuelView.Core
{


/// <summary>World / Conditions: native Core mechanisms.</summary>
public static partial class DVKernel
{
    // The content supplies the temporal reference and contextual object identities.
    public static object F_dv_condition_bounds(object condition, object clockReference, object includeNegated)
    {
        var key = new List<object> { "condition-bounds", clockReference, includeNegated };
        var cached = R.Items(R.memo_get(condition, key));
        if (R.Bool(cached[0])) return cached[1];
        var result = new List<object>();
        var op = R.Text(F_dv_find(condition, "op", ""));
        if (R.Bool(includeNegated) && op == "not")
            return R.memo_put(condition, key, F_dv_condition_bounds(F_dv_get(condition, "item"), clockReference, true));
        if (op == "at_least" && R.Bool(F_dv_same(F_dv_get(condition, "ref"), clockReference)))
            result.Add(F_dv_get(condition, "value"));
        if (op == "all")
        {
            foreach (var child in R.Items(F_dv_get(condition, "items")))
                result.AddRange(R.Items(F_dv_condition_bounds(child, clockReference, false)));
            if (R.Bool(includeNegated))
                foreach (var child in R.Items(F_dv_get(condition, "items")))
                    foreach (var value in R.Items(F_dv_condition_bounds(child, clockReference, true)))
                        if (!R.Bool(F_dv_has(result, value))) result.Add(value);
        }
        return R.memo_put(condition, key, result);
    }

    public static object F_dv_condition_without_context(object condition, object clockReference, object contextualObjects)
    {
        bool Contextual(object node)
        {
            var reference = F_dv_find(node, "ref", false);
            return reference is not false && (R.Bool(F_dv_has(contextualObjects, F_dv_find(reference, "object", ""))) ||
                (R.Text(F_dv_find(node, "op", "")) == "at_least" && R.Bool(F_dv_same(reference, clockReference))));
        }
        var op = R.Text(F_dv_find(condition, "op", ""));
        if (Contextual(condition) || (op == "not" && Contextual(F_dv_get(condition, "item")))) return F_dv_true();
        if (op == "all") return F_dv_all(R.Items(F_dv_get(condition, "items"))
            .Select(child => F_dv_condition_without_context(child, clockReference, contextualObjects)).ToList());
        return condition;
    }

    public static object F_dv_true()
    {
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("op", "constant"), F_dv_pair("value", "true") };
    }

    public static object F_dv_fact(object v_ref, object v_expected)
    {
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("op", "equals"), F_dv_pair("ref", v_ref), F_dv_pair("value", v_expected) };
    }

    public static object F_dv_at_least(object v_ref, object v_minimum)
    {
        return new System.Collections.Generic.List<object>(3) { F_dv_pair("op", "at_least"), F_dv_pair("ref", v_ref), F_dv_pair("value", v_minimum) };
    }

    public static object F_dv_all(object v_conditions)
    {
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("op", "all"), F_dv_pair("items", v_conditions) };
    }

    public static object F_dv_not(object v_condition)
    {
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("op", "not"), F_dv_pair("item", v_condition) };
    }

    public static object F_dv_time_materialize(object v_world, object v_condition)
    {
        if (R.Bool(R.eq(F_dv_get(v_condition, "op"), "held_for")))
        {
            object v_cell__14655 = F_dv_read_query(v_world, F_dv_get(v_condition, "ref"));
            if (R.Bool((R.Bool(F_dv_get(v_cell__14655, "known")) ? (object)(R.not(F_dv_get(v_cell__14655, "conflicted"))) : (object)(false))))
            {
                if (R.Bool(R.not(F_dv_same(F_dv_get(v_cell__14655, "value"), F_dv_get(v_condition, "value")))))
                {
                    return F_dv_fact(F_dv_get(v_condition, "ref"), F_dv_get(v_condition, "value"));
                }
                return F_dv_all(new System.Collections.Generic.List<object>(2) { F_dv_fact(F_dv_get(v_condition, "ref"), F_dv_get(v_condition, "value")), F_dv_at_least(F_dv_get(v_condition, "clock"), R.add(F_dv_get(v_cell__14655, "time"), F_dv_get(v_condition, "duration"))) });
            }
            return v_condition;
        }
        if (R.Bool(R.eq(F_dv_get(v_condition, "op"), "not")))
        {
            return F_dv_not(F_dv_time_materialize(v_world, F_dv_get(v_condition, "item")));
        }
        if (R.Bool(R.eq(F_dv_get(v_condition, "op"), "all")))
        {
            object v_items__15153 = new System.Collections.Generic.List<object>(0) { };
            object v_i__15176 = R.number("0");
            while (R.Bool(R.lt(v_i__15176, R.count(F_dv_get(v_condition, "items")))))
            {
                _ = R.push(v_items__15153, F_dv_time_materialize(v_world, R.at(F_dv_get(v_condition, "items"), v_i__15176)));
                v_i__15176 = R.add(v_i__15176, R.number("1"));
            }
            return F_dv_all(v_items__15153);
        }
        return v_condition;
    }

    public static object F_dv_matches(object v_object_id, object v_facet, object v_fields)
    {
        return F_dv_all(R.Block(() => { object comp_source_15579 = v_fields; var comp_result_15579 = new System.Collections.Generic.List<object>(); for (int comp_i_15579 = 0; comp_i_15579 < R.Items(comp_source_15579).Count; comp_i_15579++) { object v_entry__comp_15579 = R.at(comp_source_15579, (double)comp_i_15579); if (R.Bool(true)) comp_result_15579.Add(F_dv_fact(F_dv_ref(v_object_id, v_facet, R.at(v_entry__comp_15579, R.number("0"))), R.at(v_entry__comp_15579, R.number("1")))); } return (object)comp_result_15579; }));
    }

    public static object F_dv_writes(object v_object_id, object v_facet, object v_fields)
    {
        return R.Block(() => { object comp_source_15696 = v_fields; var comp_result_15696 = new System.Collections.Generic.List<object>(); for (int comp_i_15696 = 0; comp_i_15696 < R.Items(comp_source_15696).Count; comp_i_15696++) { object v_entry__comp_15696 = R.at(comp_source_15696, (double)comp_i_15696); if (R.Bool(true)) comp_result_15696.Add(F_dv_assign(F_dv_ref(v_object_id, v_facet, R.at(v_entry__comp_15696, R.number("0"))), R.at(v_entry__comp_15696, R.number("1")))); } return (object)comp_result_15696; });
    }

    public static object F_dv_condition_inputs(object v_condition)
    {
        object v_unsupported__15892 = new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", false), F_dv_pair("reads", new System.Collections.Generic.List<object>(0) { }) };
        if (R.Bool((R.Bool(R.not(F_dv_record_like(v_condition))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_condition, "op"))))))
        {
            return v_unsupported__15892;
        }
        object v_op__16047 = F_dv_get(v_condition, "op");
        if (R.Bool(R.eq(v_op__16047, "constant")))
        {
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", (R.Bool(F_dv_has_key(v_condition, "value")) ? (object)(F_dv_has(new System.Collections.Generic.List<object>(3) { "true", "false", "unknown" }, F_dv_find(v_condition, "value", ""))) : (object)(false))), F_dv_pair("reads", new System.Collections.Generic.List<object>(0) { }) };
        }
        if (R.Bool(R.eq(v_op__16047, "not")))
        {
            if (R.Bool(R.not(F_dv_has_key(v_condition, "item"))))
            {
                return v_unsupported__15892;
            }
            return F_dv_condition_inputs(F_dv_get(v_condition, "item"));
        }
        if (R.Bool(R.eq(v_op__16047, "all")))
        {
            if (R.Bool((R.Bool(R.not(F_dv_has_key(v_condition, "items"))) ? (object)(true) : (object)(R.not(R.is_list(F_dv_get(v_condition, "items")))))))
            {
                return v_unsupported__15892;
            }
            object v_reads__16530 = new System.Collections.Generic.List<object>(0) { };
            object v_complete__16553 = true;
            object v_i__16581 = R.number("0");
            while (R.Bool(R.lt(v_i__16581, R.count(F_dv_get(v_condition, "items")))))
            {
                object v_part__16648 = F_dv_condition_inputs(R.at(F_dv_get(v_condition, "items"), v_i__16581));
                v_complete__16553 = (R.Bool(v_complete__16553) ? (object)(F_dv_get(v_part__16648, "complete")) : (object)(false));
                object v_j__16761 = R.number("0");
                while (R.Bool(R.lt(v_j__16761, R.count(F_dv_get(v_part__16648, "reads")))))
                {
                    if (R.Bool(R.not(F_dv_has(v_reads__16530, R.at(F_dv_get(v_part__16648, "reads"), v_j__16761)))))
                    {
                        _ = R.push(v_reads__16530, F_dv_clone(R.at(F_dv_get(v_part__16648, "reads"), v_j__16761)));
                    }
                    v_j__16761 = R.add(v_j__16761, R.number("1"));
                }
                v_i__16581 = R.add(v_i__16581, R.number("1"));
            }
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", v_complete__16553), F_dv_pair("reads", v_reads__16530) };
        }
        if (R.Bool(R.eq(v_op__16047, "held_for")))
        {
            if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(F_dv_has_key(v_condition, "ref"))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_condition, "value"))))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_condition, "clock"))))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_condition, "duration"))))))
            {
                return v_unsupported__15892;
            }
            object v_target__17256 = F_dv_condition_inputs(F_dv_fact(F_dv_get(v_condition, "ref"), F_dv_get(v_condition, "value")));
            object v_clock__17338 = F_dv_condition_inputs(F_dv_at_least(F_dv_get(v_condition, "clock"), R.number("0")));
            if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(F_dv_get(v_target__17256, "complete"))) ? (object)(true) : (object)(R.not(F_dv_get(v_clock__17338, "complete"))))) ? (object)(true) : (object)(R.not(R.is_numeric_value(F_dv_get(v_condition, "duration")))))) ? (object)(true) : (object)(R.lt(F_dv_get(v_condition, "duration"), R.number("0"))))))
            {
                return v_unsupported__15892;
            }
            return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", true), F_dv_pair("reads", new System.Collections.Generic.List<object>(2) { F_dv_get(v_condition, "ref"), F_dv_get(v_condition, "clock") }) };
        }
        if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.eq(v_op__16047, "equals"))) ? (object)(R.not(R.eq(v_op__16047, "at_least"))) : (object)(false))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_condition, "ref"))))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_condition, "value"))))))
        {
            return v_unsupported__15892;
        }
        object v_ref__17789 = F_dv_get(v_condition, "ref");
        if (R.Bool((R.Bool(R.not(F_dv_record_like(v_ref__17789))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_ref__17789, "type", ""), "ref"))))))
        {
            return v_unsupported__15892;
        }
        if (R.Bool((R.Bool((R.Bool(R.not(R.is_text(F_dv_find(v_ref__17789, "object", false)))) ? (object)(true) : (object)(R.not(R.is_text(F_dv_find(v_ref__17789, "facet", false)))))) ? (object)(true) : (object)(R.not(R.is_text(F_dv_find(v_ref__17789, "field", false)))))))
        {
            return v_unsupported__15892;
        }
        if (R.Bool(R.not(R.is_text(F_dv_find(v_ref__17789, "subject", "")))))
        {
            return v_unsupported__15892;
        }
        if (R.Bool((R.Bool(R.eq(v_op__16047, "at_least")) ? (object)(R.not(R.is_numeric_value(F_dv_get(v_condition, "value")))) : (object)(false))))
        {
            return v_unsupported__15892;
        }
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", true), F_dv_pair("reads", new System.Collections.Generic.List<object>(1) { F_dv_clone(v_ref__17789) }) };
    }

    public static object F_dv_time_ref_relation(object v_ref, object v_clock)
    {
        if (R.Bool((R.Bool(R.not(F_dv_record_like(v_clock))) ? (object)(true) : (object)(R.not(R.eq(F_dv_find(v_clock, "type", ""), "ref"))))))
        {
            return "unknown";
        }
        if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(R.is_text(F_dv_find(v_clock, "object", false)))) ? (object)(true) : (object)(R.not(R.is_text(F_dv_find(v_clock, "facet", false)))))) ? (object)(true) : (object)(R.not(R.is_text(F_dv_find(v_clock, "field", false)))))) ? (object)(true) : (object)(R.not(R.is_text(F_dv_find(v_clock, "subject", "")))))))
        {
            return "unknown";
        }
        if (R.Bool((R.Bool((R.Bool(R.not(R.eq(F_dv_get(v_ref, "object"), F_dv_get(v_clock, "object")))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_ref, "facet"), F_dv_get(v_clock, "facet")))))) ? (object)(true) : (object)(R.not(R.eq(F_dv_get(v_ref, "field"), F_dv_get(v_clock, "field")))))))
        {
            return "different";
        }
        if (R.Bool(R.not(R.eq(F_dv_find(v_ref, "subject", ""), F_dv_find(v_clock, "subject", "")))))
        {
            return "unknown";
        }
        return "same";
    }

    private readonly record struct TimeBoundaryResult(bool Complete, object Next);

    public static object F_dv_time_boundary(object v_condition, object v_clock, object v_now, object v_until, object v_policy)
    {
        var result = ComputeTimeBoundary(v_condition, v_clock, v_now, v_until, v_policy);
        // The public result remains an independent writable DV record. Recursive
        // analysis only needs two values, so its temporary results stay on stack.
        return new System.Collections.Generic.List<object>(2) { F_dv_pair("complete", result.Complete), F_dv_pair("next", result.Next) };
    }

    private static TimeBoundaryResult ComputeTimeBoundary(object v_condition, object v_clock, object v_now, object v_until, object v_policy)
    {
        _ = F_dv_require((R.Bool(R.eq(v_policy, "invalidate")) ? (object)(true) : (object)(R.eq(v_policy, "minute"))), "Unknown time analysis policy");
        _ = F_dv_require((R.Bool(R.is_numeric_value(v_now)) ? (object)(R.is_numeric_value(v_until)) : (object)(false)), "Invalid time analysis interval");
        if (R.Bool(R.lt(v_until, v_now)))
        {
            return new TimeBoundaryResult(false, v_now);
        }
        // Keep the original eager fallback arithmetic before inspecting the
        // condition, including its numeric validation and exception ordering.
        var v_fallback__19546 = new TimeBoundaryResult(false, F_dv_min(v_until, R.add(v_now, R.number("1"))));
        if (R.Bool((R.Bool(R.not(F_dv_record_like(v_condition))) ? (object)(true) : (object)(R.not(F_dv_has_key(v_condition, "op"))))))
        {
            return v_fallback__19546;
        }
        object v_op__19714 = F_dv_get(v_condition, "op");
        if (R.Bool(R.eq(v_op__19714, "constant")))
        {
            if (R.Bool((R.Bool(R.not(F_dv_has_key(v_condition, "value"))) ? (object)(true) : (object)(R.not(F_dv_has(new System.Collections.Generic.List<object>(3) { "true", "false", "unknown" }, F_dv_get(v_condition, "value")))))))
            {
                return v_fallback__19546;
            }
            return new TimeBoundaryResult(true, v_until);
        }
        if (R.Bool(R.eq(v_op__19714, "not")))
        {
            if (R.Bool(R.not(F_dv_has_key(v_condition, "item"))))
            {
                return v_fallback__19546;
            }
            return ComputeTimeBoundary(F_dv_get(v_condition, "item"), v_clock, v_now, v_until, v_policy);
        }
        if (R.Bool(R.eq(v_op__19714, "all")))
        {
            if (R.Bool((R.Bool(R.not(F_dv_has_key(v_condition, "items"))) ? (object)(true) : (object)(R.not(R.is_list(F_dv_get(v_condition, "items")))))))
            {
                return v_fallback__19546;
            }
            object v_next__20253 = v_until;
            object v_i__20278 = R.number("0");
            while (R.Bool(R.lt(v_i__20278, R.count(F_dv_get(v_condition, "items")))))
            {
                var v_part__20345 = ComputeTimeBoundary(R.at(F_dv_get(v_condition, "items"), v_i__20278), v_clock, v_now, v_until, v_policy);
                if (!v_part__20345.Complete)
                {
                    return v_fallback__19546;
                }
                v_next__20253 = F_dv_min(v_next__20253, v_part__20345.Next);
                v_i__20278 = R.add(v_i__20278, R.number("1"));
            }
            return new TimeBoundaryResult(true, v_next__20253);
        }
        if (R.Bool(R.eq(v_op__19714, "held_for")))
        {
            return v_fallback__19546;
        }
        object v_inputs__20803 = F_dv_condition_inputs(v_condition);
        if (R.Bool(R.not(F_dv_get(v_inputs__20803, "complete"))))
        {
            return v_fallback__19546;
        }
        if (R.Bool(R.eq(v_clock, false)))
        {
            if (R.Bool(R.eq(v_policy, "minute")))
            {
                return new TimeBoundaryResult(true, v_until);
            }
            return v_fallback__19546;
        }
        object v_relation__21034 = F_dv_time_ref_relation(F_dv_get(v_condition, "ref"), v_clock);
        if (R.Bool(R.eq(v_relation__21034, "unknown")))
        {
            return v_fallback__19546;
        }
        if (R.Bool(R.eq(v_relation__21034, "different")))
        {
            return new TimeBoundaryResult(true, v_until);
        }
        if (R.Bool(R.not(R.is_numeric_value(F_dv_get(v_condition, "value")))))
        {
            return v_fallback__19546;
        }
        if (R.Bool(R.eq(v_policy, "minute")))
        {
            if (R.Bool(R.not(R.eq(v_op__19714, "at_least"))))
            {
                return v_fallback__19546;
            }
            if (R.Bool(R.lt(v_until, F_dv_get(v_condition, "value"))))
            {
                return new TimeBoundaryResult(true, v_until);
            }
            if (R.Bool(R.lt(v_now, F_dv_get(v_condition, "value"))))
            {
                object v_delta__21519 = R.sub(F_dv_get(v_condition, "value"), v_now);
                object v_steps__21565 = R.number("1");
                while (R.Bool(R.lt(v_steps__21565, v_delta__21519)))
                {
                    v_steps__21565 = R.add(v_steps__21565, R.number("1"));
                }
                return new TimeBoundaryResult(true, F_dv_min(v_until, R.add(v_now, v_steps__21565)));
            }
            return new TimeBoundaryResult(true, v_until);
        }
        if (R.Bool(R.lt(v_now, F_dv_get(v_condition, "value"))))
        {
            return new TimeBoundaryResult(true, F_dv_min(v_until, F_dv_get(v_condition, "value")));
        }
        if (R.Bool((R.Bool(R.eq(v_op__19714, "equals")) ? (object)(R.eq(v_now, F_dv_get(v_condition, "value"))) : (object)(false))))
        {
            return new TimeBoundaryResult(true, v_now);
        }
        return new TimeBoundaryResult(true, v_until);
    }

    public static object F_dv_analyze_time(object v_condition, object v_clock, object v_now, object v_until, object v_policy)
    {
        object v_boundary__22078 = F_dv_time_boundary(v_condition, v_clock, v_now, v_until, v_policy);
        object v_inputs__22152 = F_dv_condition_inputs(v_condition);
        object v_solver__22200 = "minute-step";
        object v_reason__22231 = "unsupported-predicate";
        if (R.Bool(F_dv_get(v_boundary__22078, "complete")))
        {
            v_solver__22200 = "event-boundary";
            v_reason__22231 = "declared-boundary";
        }
        if (R.Bool((R.Bool((R.Bool(R.not(F_dv_get(v_boundary__22078, "complete"))) ? (object)(F_dv_get(v_inputs__22152, "complete")) : (object)(false))) ? (object)((R.Bool(R.eq(F_dv_find(v_condition, "op", ""), "equals")) ? (object)(true) : (object)(R.eq(F_dv_find(v_condition, "op", ""), "at_least")))) : (object)(false))))
        {
            if (R.Bool(R.eq(v_clock, false)))
            {
                v_reason__22231 = "clock-not-declared";
            }
            else
            {
                if (R.Bool(R.eq(F_dv_time_ref_relation(F_dv_get(v_condition, "ref"), v_clock), "unknown")))
                {
                    v_reason__22231 = "ambiguous-clock-reference";
                }
                else
                {
                    if (R.Bool((R.Bool(R.eq(v_policy, "minute")) ? (object)(R.eq(F_dv_get(v_condition, "op"), "equals")) : (object)(false))))
                    {
                        v_reason__22231 = "clock-equality-needs-sampling";
                    }
                }
            }
        }
        if (R.Bool(R.lt(v_until, v_now)))
        {
            v_solver__22200 = "none";
            v_reason__22231 = "expired-interval";
        }
        return F_dv_with_many(v_boundary__22078, new System.Collections.Generic.List<object>(9) { F_dv_pair("solver", v_solver__22200), F_dv_pair("reason", v_reason__22231), F_dv_pair("policy", v_policy), F_dv_pair("from", v_now), F_dv_pair("until", v_until), F_dv_pair("reads", F_dv_get(v_inputs__22152, "reads")), F_dv_pair("scope", "declaration"), F_dv_pair("assumptions", new System.Collections.Generic.List<object>(2) { "non-clock inputs remain stable", "caller rechecks state changes and ordered effects" }), F_dv_pair("numeric_semantics", "runtime-values") });
    }

    public static object F_dv_analyze_law(object v_world, object v_name)
    {
        object v_adoption__23443 = F_dv_get(F_dv_get(v_world, "adoptions"), v_name);
        object v_law__23492 = F_dv_get(v_adoption__23443, "law");
        object v_inputs__23519 = F_dv_condition_inputs(F_dv_get(v_law__23492, "premise"));
        object v_unknown__23569 = new System.Collections.Generic.List<object>(0) { };
        object v_conflicted__23590 = new System.Collections.Generic.List<object>(0) { };
        object v_i__23614 = R.number("0");
        while (R.Bool(R.lt(v_i__23614, R.count(F_dv_get(v_inputs__23519, "reads")))))
        {
            object v_ref__23670 = R.at(F_dv_get(v_inputs__23519, "reads"), v_i__23614);
            object v_cell__23704 = F_dv_read_query(v_world, v_ref__23670);
            if (R.Bool(R.not(F_dv_get(v_cell__23704, "known"))))
            {
                _ = R.push(v_unknown__23569, v_ref__23670);
            }
            if (R.Bool(F_dv_get(v_cell__23704, "conflicted")))
            {
                _ = R.push(v_conflicted__23590, v_ref__23670);
            }
            v_i__23614 = R.add(v_i__23614, R.number("1"));
        }
        object v_truth__23883 = "unknown";
        if (R.Bool(F_dv_get(v_inputs__23519, "complete")))
        {
            v_truth__23883 = F_dv_truth(v_world, F_dv_get(v_law__23492, "premise"));
        }
        return new System.Collections.Generic.List<object>(15) { F_dv_pair("world", F_dv_get(v_world, "id")), F_dv_pair("subject", F_dv_get(v_world, "subject")), F_dv_pair("revision", F_dv_get(v_world, "revision")), F_dv_pair("law", v_name), F_dv_pair("law_version", F_dv_get(v_law__23492, "version")), F_dv_pair("mode", F_dv_get(v_adoption__23443, "mode")), F_dv_pair("active", F_dv_get(v_adoption__23443, "active")), F_dv_pair("premise", v_truth__23883), F_dv_pair("complete", F_dv_get(v_inputs__23519, "complete")), F_dv_pair("reads", F_dv_get(v_inputs__23519, "reads")), F_dv_pair("unknown", v_unknown__23569), F_dv_pair("conflicted", v_conflicted__23590), F_dv_pair("effects_verified", false), F_dv_pair("scope", "input-snapshot"), F_dv_pair("numeric_semantics", "runtime-values") };
    }

    public static object F_dv_truth(object v_world, object v_proposition)
    {
        if (R.Bool(R.is_list(v_world)))
        {
            if (R.Bool(R.eq(F_dv_get(v_proposition, "op"), "constant")))
            {
                object v_value__24708 = F_dv_get(v_proposition, "value");
                if (R.Bool(R.not(R.is_list(v_value__24708))))
                {
                    return v_value__24708;
                }
            }
        }
        object v_key__24809 = new System.Collections.Generic.List<object>(2) { "dv_truth", v_proposition };
        object v_cached__24849 = R.memo_get(v_world, v_key__24809);
        if (R.Bool(R.at(v_cached__24849, R.number("0"))))
        {
            return R.at(v_cached__24849, R.number("1"));
        }
        return R.memo_put(v_world, v_key__24809, F_dv_truth_uncached(v_world, v_proposition));
    }

    public static object F_dv_truth_uncached(object v_world, object v_proposition)
    {
        object v_op__25050 = F_dv_get(v_proposition, "op");
        if (R.Bool(R.eq(v_op__25050, "constant")))
        {
            return F_dv_get(v_proposition, "value");
        }
        else
        {
            if (R.Bool((R.Bool(R.eq(v_op__25050, "equals")) ? (object)(true) : (object)(R.eq(v_op__25050, "at_least")))))
            {
                if (R.Bool(R.read_ref_view_safe(F_dv_get(v_proposition, "ref"))))
                {
                    v_world = R.read_view(v_world);
                    v_proposition = R.read_view(v_proposition);
                }
            }
            if (R.Bool(R.eq(v_op__25050, "not")))
            {
                object v_inner__25622 = F_dv_truth(v_world, F_dv_get(v_proposition, "item"));
                if (R.Bool(R.eq(v_inner__25622, "unknown")))
                {
                    return "unknown";
                }
                else
                {
                    if (R.Bool(R.eq(v_inner__25622, "true")))
                    {
                        return "false";
                    }
                    else
                    {
                        return "true";
                    }
                }
            }
            else
            {
                if (R.Bool(R.eq(v_op__25050, "all")))
                {
                    object v_items__25945 = F_dv_get(v_proposition, "items");
                    object v_i__25991 = R.number("0");
                    object v_result__26017 = "true";
                    while (R.Bool(R.lt(v_i__25991, R.count(v_items__25945))))
                    {
                        object v_truth__26102 = F_dv_truth(v_world, R.at(v_items__25945, v_i__25991));
                        if (R.Bool(R.eq(v_truth__26102, "false")))
                        {
                            return "false";
                        }
                        else
                        {
                            if (R.Bool(F_dv_and(R.eq(v_truth__26102, "unknown"), R.eq(v_result__26017, "true"))))
                            {
                                v_result__26017 = "unknown";
                            }
                        }
                        v_i__25991 = R.add(v_i__25991, R.number("1"));
                    }
                    return v_result__26017;
                }
                else
                {
                    if (R.Bool((R.Bool(R.predicate_enabled()) ? (object)(R.not(F_dv_has_key(v_world, "CognitionFilter"))) : (object)(false))))
                    {
                        if (R.Bool((R.Bool(R.eq(v_op__25050, "equals")) ? (object)(true) : (object)(R.eq(v_op__25050, "at_least")))))
                        {
                            object v_native_truth__26701 = R.predicate_leaf(v_world, v_proposition);
                            if (R.Bool(R.not(R.eq(v_native_truth__26701, false))))
                            {
                                return v_native_truth__26701;
                            }
                        }
                    }
                    object v_cell__26872 = F_dv_read_query(v_world, F_dv_get(v_proposition, "ref"));
                    if (R.Bool(F_dv_or(R.not(F_dv_get(v_cell__26872, "known")), F_dv_get(v_cell__26872, "conflicted"))))
                    {
                        return "unknown";
                    }
                    else
                    {
                        if (R.Bool(R.eq(v_op__25050, "held_for")))
                        {
                            if (R.Bool(R.not(F_dv_same(F_dv_get(v_cell__26872, "value"), F_dv_get(v_proposition, "value")))))
                            {
                                return "false";
                            }
                            object v_clock__27214 = F_dv_read_query(v_world, F_dv_get(v_proposition, "clock"));
                            if (R.Bool((R.Bool((R.Bool((R.Bool(R.not(F_dv_get(v_clock__27214, "known"))) ? (object)(true) : (object)(F_dv_get(v_clock__27214, "conflicted")))) ? (object)(true) : (object)(R.not(R.is_numeric_value(F_dv_get(v_clock__27214, "value")))))) ? (object)(true) : (object)(R.not(R.is_numeric_value(F_dv_get(v_cell__26872, "time")))))))
                            {
                                return "unknown";
                            }
                            if (R.Bool(R.lt(F_dv_get(v_clock__27214, "value"), R.add(F_dv_get(v_cell__26872, "time"), F_dv_get(v_proposition, "duration")))))
                            {
                                return "false";
                            }
                            return "true";
                        }
                        if (R.Bool(R.eq(v_op__25050, "equals")))
                        {
                            if (R.Bool(F_dv_same(F_dv_get(v_cell__26872, "value"), F_dv_get(v_proposition, "value"))))
                            {
                                return "true";
                            }
                            else
                            {
                                return "false";
                            }
                        }
                        else
                        {
                            if (R.Bool(R.eq(v_op__25050, "at_least")))
                            {
                                if (R.Bool(R.lt(F_dv_get(v_cell__26872, "value"), F_dv_get(v_proposition, "value"))))
                                {
                                    return "false";
                                }
                                else
                                {
                                    return "true";
                                }
                            }
                            else
                            {
                                return R.fail("Unknown proposition operator");
                            }
                        }
                    }
                }
            }
        }
    }
}
}


// --- Appended from runtime/Conditions.cs ---
namespace DuelView.Runtime
{


public enum Truth { Unknown, False, True }

/// <summary>Compiled read dependencies and evaluation. Unknown information stays unknown.</summary>
public abstract record Condition
{
    public abstract Truth Evaluate(Func<FieldKey, Fact?> read);
    public abstract IEnumerable<FieldKey> Inputs { get; }
    public virtual IEnumerable<double> ClockThresholds(FieldKey clock) => [];
    public virtual IEnumerable<double> WakeTimes(Func<FieldKey, Fact?> read, FieldKey clock) => ClockThresholds(clock);
    public static Condition Compile(JsonElement value, string subject)
    {
        return value.Get("op").Text() switch
        {
            "constant" => new Constant(value.Get("value").ValueKind switch
            { JsonValueKind.True => Truth.True, JsonValueKind.False => Truth.False,
                _ => value.Get("value").Text() switch { "true" => Truth.True, "false" => Truth.False, _ => Truth.Unknown } }),
            "equals" => new EqualsValue(FieldKey.Read(value.Get("ref"), subject), value.Get("value")),
            "known" => new KnownValue(FieldKey.Read(value.Get("ref"), subject)),
            "at_least" => new AtLeast(FieldKey.Read(value.Get("ref"), subject), value.Get("value")),
            "not" => new Negate(Compile(value.Get("item"), subject)),
            "all" => new All(value.Get("items").Items().Select(v => Compile(v, subject)).ToArray()),
            "held_for" => new HeldFor(FieldKey.Read(value.Get("ref"), subject), value.Get("value"),
                FieldKey.Read(value.Get("clock"), subject), value.Get("duration").Number()),
            _ => throw new InvalidDataException("Unsupported DV condition: " + value.GetRawText())
        };
    }
    public sealed record Constant(Truth Value) : Condition
    {
        public override Truth Evaluate(Func<FieldKey, Fact?> read) => Value;
        public override IEnumerable<FieldKey> Inputs => [];
    }
    public sealed record KnownValue(FieldKey Key) : Condition
    {
        public override Truth Evaluate(Func<FieldKey, Fact?> read) => read(Key) is { Known: true, Conflicted: false } ? Truth.True : Truth.False;
        public override IEnumerable<FieldKey> Inputs => [Key];
    }
    public sealed record EqualsValue(FieldKey Key, JsonElement Value) : Condition
    {
        public override Truth Evaluate(Func<FieldKey, Fact?> read) => read(Key) is { Known: true, Conflicted: false } fact
            ? Values.Same(fact.Value, Value) ? Truth.True : Truth.False : Truth.Unknown;
        public override IEnumerable<FieldKey> Inputs => [Key];
    }
    public sealed record AtLeast(FieldKey Key, JsonElement Value) : Condition
    {
        public override Truth Evaluate(Func<FieldKey, Fact?> read) => read(Key) is { Known: true, Conflicted: false } fact &&
            ExactNumber.TryRead(fact.Value, out var a) && ExactNumber.TryRead(Value, out var b)
            ? a.CompareTo(b) >= 0 ? Truth.True : Truth.False : Truth.Unknown;
        public override IEnumerable<FieldKey> Inputs => [Key];
        public override IEnumerable<double> ClockThresholds(FieldKey clock) => Key == clock && Value.ValueKind == JsonValueKind.Number ? [Value.GetDouble()] : [];
    }
    public sealed record Negate(Condition Item) : Condition
    {
        public override Truth Evaluate(Func<FieldKey, Fact?> read) => Item.Evaluate(read) switch { Truth.True => Truth.False, Truth.False => Truth.True, _ => Truth.Unknown };
        public override IEnumerable<FieldKey> Inputs => Item.Inputs;
        public override IEnumerable<double> ClockThresholds(FieldKey clock) => Item.ClockThresholds(clock);
        public override IEnumerable<double> WakeTimes(Func<FieldKey, Fact?> read, FieldKey clock) => Item.WakeTimes(read, clock);
    }
    public sealed record All(IReadOnlyList<Condition> Items) : Condition
    {
        public override Truth Evaluate(Func<FieldKey, Fact?> read)
        {
            var unknown = false;
            foreach (var item in Items)
            {
                var value = item.Evaluate(read);
                if (value == Truth.False) return Truth.False;
                unknown |= value == Truth.Unknown;
            }
            return unknown ? Truth.Unknown : Truth.True;
        }
        public override IEnumerable<FieldKey> Inputs => Items.SelectMany(x => x.Inputs).Distinct();
        public override IEnumerable<double> ClockThresholds(FieldKey clock) => Items.SelectMany(x => x.ClockThresholds(clock));
        public override IEnumerable<double> WakeTimes(Func<FieldKey, Fact?> read, FieldKey clock) => Items.SelectMany(x => x.WakeTimes(read, clock));
    }
    public sealed record HeldFor(FieldKey Key, JsonElement Value, FieldKey Clock, double Duration) : Condition
    {
        public override Truth Evaluate(Func<FieldKey, Fact?> read)
        {
            if (read(Key) is not { Known: true, Conflicted: false } fact || read(Clock) is not { Known: true, Conflicted: false } now) return Truth.Unknown;
            if (!Values.Same(fact.Value, Value)) return Truth.False;
            return now.Value.Number() >= fact.ObservedAt + Duration ? Truth.True : Truth.False;
        }
        public override IEnumerable<FieldKey> Inputs => [Key, Clock];
        public override IEnumerable<double> WakeTimes(Func<FieldKey, Fact?> read, FieldKey clock) => Clock == clock &&
            read(Key) is { Known: true, Conflicted: false } fact && Values.Same(fact.Value, Value)
            ? [fact.ObservedAt + Duration] : [];
    }
}

public sealed record Effect(FieldKey Key, JsonElement Value, bool Increment)
{
    public static Effect Compile(JsonElement value, string subject) => new(FieldKey.Read(value.Get("ref"), subject),
        value.Get("value"), value.Get("op").Text() switch
        { "assign" => false, "increment" => true, _ => throw new InvalidDataException("Unsupported DV effect.") });
    public Fact Resolve(Func<FieldKey, Fact?> read, double time, double confidence = 1)
    {
        var result = Value;
        if (Increment)
        {
            var before = read(Key);
            if (before is not { Known: true, Conflicted: false } || !ExactNumber.TryRead(before.Value, out var a) || !ExactNumber.TryRead(Value, out var b))
                throw new InvalidOperationException("Increment needs a known uncontested number: " + Key);
            result = a.Add(b).Json();
        }
        return new(result, true, false, confidence, time, 0);
    }
}

public sealed record Rule(string Id, Condition Premise, IReadOnlyList<Effect> Effects, Condition? Postcondition, string Mode, double Confidence, JsonElement Basis)
{
    public static Rule Compile(string id, JsonElement adoption, string subject)
    {
        var law = adoption.Get("law");
        return new(id, Condition.Compile(law.Get("premise"), subject), law.Get("effects").Items().Select(v => Effect.Compile(v, subject)).ToArray(),
            law.Get("postcondition").ValueKind == JsonValueKind.Object ? Condition.Compile(law.Get("postcondition"), subject) : null,
            adoption.Get("mode").Text(), adoption.Get("confidence").Number(1), adoption.Get("basis"));
    }
    public Dictionary<FieldKey, Fact> Preview(Func<FieldKey, Fact?> read, double time)
    {
        var writes = new Dictionary<FieldKey, Fact>();
        Fact? Read(FieldKey key) => writes.GetValueOrDefault(key) ?? read(key);
        foreach (var effect in Effects) writes[effect.Key] = effect.Resolve(Read, time, Confidence);
        if (Postcondition is not null && Postcondition.Evaluate(Read) != Truth.True)
            throw new InvalidOperationException("DV postcondition failed: " + Id);
        return writes;
    }
}
}
