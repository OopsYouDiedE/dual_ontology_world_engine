using System.Collections.Immutable;
using System.Text.RegularExpressions;
using DualOntologyWorldEngine.Core;
namespace DualOntologyWorldEngine.Examples.ZooSimulator;

public sealed record RuleText(string Key, string Original, string Script, string Section);
public sealed record Instruction(string Condition, RuleMode Mode, string Action, int Weight);
public sealed record RememberedRule(RuleText Text, Projection Projection, Truth Acceptance, int Understanding);

/// <summary>受控中文 → 条件/行为语法树 → 人物内部名词 → Core AST。没有客观效果提交权限。</summary>
public sealed class ZooRules
{
    private static readonly string[] Sections = ["Visitor", "Staff", "Gate", "Lost", "Inside", "Red", "Guard", "Director", "Survivor"];
    public Dictionary<string, RuleText> Clauses { get; } = [];
    public ZooRules()
    {
        string Resource(string name)
        {
            using var stream = typeof(ZooRules).Assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        var scripts = Resource("ZooRules").Split('\n').Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith('#'))
            .Select(l => l.Trim().Split('|', 2)).ToDictionary(p => p[0], p => p[1]);
        int section = -1;
        foreach (var line in Resource("ZooOriginal").Split('\n').Select(l => l.Trim()))
        {
            if (line.StartsWith("# 此上原文结束")) break;
            if (line.StartsWith("## ")) { section++; continue; }
            var match = Regex.Match(line, @"^(\d+)\.\s*(.+)$");
            if (!match.Success || section < 0) continue;
            var key = $"{Sections[section]}.{match.Groups[1].Value}";
            var script = scripts[key];
            Parse(script);
            Clauses.Add(key, new(key, match.Groups[2].Value, script, Sections[section]));
        }
    }
    public static Instruction[] Parse(string script) => script.Split('；', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(line =>
    {
        var match = Regex.Match(line, @"^当 (\S+) 时 (优先|必须|禁止|允许) (\S+) 权重 (\d+)$");
        if (!match.Success) throw new FormatException($"规则格式错误：{line}。格式：当 条件 时 优先 动作 权重 80；多条用中文分号分隔。");
        var condition = match.Groups[1].Value;
        foreach (var term in condition.Split('&'))
            if (!ZooMinds.FactNames.Contains(term.TrimStart('!'))) throw new FormatException($"未知条件：{term}");
        var action = match.Groups[3].Value;
        if (!(action.StartsWith("前往:") && ZooWorld.RoomNames.Contains(action[3..])) && !ZooMinds.Actions.ContainsKey(action))
            throw new FormatException($"未知行动：{action}");
        var weight = int.Parse(match.Groups[4].Value);
        if (weight is < 1 or > 1000) throw new FormatException("权重范围为 1–1000。");
        return new Instruction(condition, match.Groups[2].Value switch { "禁止" => RuleMode.Forbid, "必须" => RuleMode.Require, "允许" => RuleMode.Permit, _ => RuleMode.Prefer }, action, weight);
    }).ToArray();

    public RuleText Publish(string key, string script, string original = "现场修订")
    {
        Parse(script);
        var section = Clauses.TryGetValue(key, out var old) ? old.Section : "Custom";
        return Clauses[key] = new(key, original, script, section);
    }
    public bool Remove(string key) => Clauses.Remove(key);

    public static void Read(Person person, RuleText text, double time, ObservationSource source = ObservationSource.Perception)
    {
        var old = person.Books.GetValueOrDefault(text.Key);
        // 角色采信的是自己读到的版本；发行和撤回不会远程改写记忆。
        if (old?.Text == text) return;
        var confidence = old?.Understanding ?? person.Self.Value(Z.Confidence);
        var accepted = old?.Acceptance ?? (text.Section=="Gate"&&person.Self.Value(Z.Role)==(int)Job.Red ? Truth.False : confidence >= 35 ? Truth.True : Truth.Unknown);
        var p = Observation.Observe(person.Soul, new(text.Key, ProjectionKind.Concept)
        {
            Target = old?.Projection, Source = source, Time = time, Existence = Truth.True,
            Rules = [], Details = text.Original + "\n" + text.Script
        });
        person.Books[text.Key] = new(text, p, accepted, confidence);
    }
    public static void Accept(Person person, string key, Truth acceptance, double time)
    {
        var entry = person.Books[key];
        person.Books[key] = entry with { Acceptance = acceptance };
        Observation.Observe(person.Soul, new(entry.Projection.Name, entry.Projection.Kind)
        { Target = entry.Projection, Source = ObservationSource.Introspection, Time = time,
          Rules = [..entry.Projection.Rules.Select(r => r with { Acceptance = acceptance })], Details = $"重新理解：{acceptance}" });
    }
}

