using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DualOntologyWorldEngine.Core;

public sealed class AstParseException(string message, int line, int column) : FormatException($"{line}:{column} {message}")
{
    public int Line { get; } = line;
    public int Column { get; } = column;
}

/// <summary>文本与嵌入只解码为 AST。名称解析限定于传入世界，扩展语义在此边界登记。</summary>
public sealed class AstDecoder(ProjectionWorld world, IEnumerable<Tag>? tags = null, IEnumerable<RelationTag>? relations = null)
{
    private readonly ProjectionWorld _world = world;
    private readonly Tag[] _tags = [Tags.Locked, Tags.Confidence, Tags.Cooperation, Tags.Trust, Tags.Success, Tags.Responded, ..tags ?? []];
    private readonly RelationTag[] _relations = [Relations.Controls, Relations.HasIdentity, Relations.AcceptsKey,
        Relations.IsA, Relations.Subject, Relations.Object, ..relations ?? []];

    public Effect DecodeEffect(string text, params Projection[] embeddings)
    {
        var parser = new Parser(this, text, embeddings);
        var effect = parser.Effect();
        parser.End();
        return effect;
    }
    public ImmutableArray<Rule> DecodeRules(string text, params Projection[] embeddings)
    {
        var parser = new Parser(this, text, embeddings);
        var rules = parser.Rules();
        parser.End();
        return rules;
    }

    private sealed class Parser
    {
        private static readonly Regex Lexer = new("\\G(?:\\s+|//[^\\r\\n]*|=>|[(),@$]|\"(?:\\\\.|[^\"\\\\])*\"|-?[0-9]+(?:\\.[0-9]+)?|[\\p{L}_][\\p{L}\\p{N}_]*)", RegexOptions.CultureInvariant);
        private readonly AstDecoder _decoder;
        private readonly string _text;
        private readonly Projection[] _embeddings;
        private readonly List<(string Text, int Offset)> _tokens = [];
        private int _position;
        private string Current => _tokens[_position].Text;

        public Parser(AstDecoder decoder, string text, Projection[] embeddings)
        {
            (_decoder, _text, _embeddings) = (decoder, text, embeddings);
            for (var offset = 0; offset < text.Length;)
            {
                var match = Lexer.Match(text, offset);
                if (!match.Success) throw ErrorAt("无法识别的字符。", offset);
                if (!string.IsNullOrWhiteSpace(match.Value) && !match.Value.StartsWith("//", StringComparison.Ordinal))
                    _tokens.Add((match.Value, offset));
                offset += match.Length;
            }
            _tokens.Add(("", text.Length));
        }

        private AstParseException ErrorAt(string message, int offset)
        {
            var before = _text[..offset];
            return new(message, before.Count(c => c == '\n') + 1, offset - before.LastIndexOf('\n'));
        }
        private AstParseException Error(string message) => ErrorAt(message, _tokens[_position].Offset);
        private bool Match(string expected)
        {
            if (!Current.Equals(expected, StringComparison.OrdinalIgnoreCase)) return false;
            _position++;
            return true;
        }
        private void Expect(string expected)
        {
            if (!Match(expected)) throw Error($"预期 {expected}。");
        }
        public void End()
        {
            if (Current.Length != 0) throw Error("存在多余输入。");
        }
        private string Name()
        {
            var token = Current;
            if (token.Length == 0) throw Error("预期名称。");
            if (token.StartsWith('"'))
            {
                try
                {
                    var value = JsonSerializer.Deserialize<string>(token)!;
                    _position++;
                    return value;
                }
                catch (JsonException) { throw Error("字符串转义无效。"); }
            }
            if (!char.IsLetter(token[0]) && token[0] != '_') throw Error("预期名称。");
            _position++;
            return token;
        }
        private int Integer()
        {
            if (!int.TryParse(Current, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) throw Error("预期整数。");
            _position++;
            return value;
        }
        private double Delay()
        {
            if (!double.TryParse(Current, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || value < 0)
                throw Error("延迟必须是有限非负数。");
            _position++;
            return value;
        }
        private T EnumName<T>() where T : struct, Enum
        {
            var name = Name();
            return Enum.TryParse<T>(name, true, out var value) && Enum.IsDefined(value) ? value : throw Error($"未知 {typeof(T).Name}: {name}。");
        }
        private Tag Tag()
        {
            var name = Name();
            return _decoder._tags.FirstOrDefault(tag => tag.Name == name) is { Name: not null } tag ? tag : throw Error($"未登记标签 {name}。");
        }
        private RelationTag Relation()
        {
            var name = Name();
            return _decoder._relations.FirstOrDefault(tag => tag.Name == name) is { Name: not null } tag ? tag : throw Error($"未登记关系 {name}。");
        }
        private Noun Noun()
        {
            if (Match("$")) return new Noun.Slot(EnumName<RuleSlot>());
            Expect("@");
            Projection projection;
            if (int.TryParse(Current, out var index))
            {
                _position++;
                if (index < 0 || index >= _embeddings.Length) throw Error("投影嵌入下标越界。");
                projection = _embeddings[index];
            }
            else
            {
                var name = Name();
                var found = _decoder._world.Projections.Where(p => p.Name == name).Take(2).ToArray();
                if (found.Length != 1) throw Error(found.Length == 0 ? $"未认知名词 {name}。" : $"名词 {name} 有歧义，请使用嵌入。");
                projection = found[0];
            }
            if (!_decoder._world.Contains(projection)) throw Error("嵌入必须来自当前 ProjectionWorld。");
            return new Noun.Reference(projection);
        }
        private T Next<T>(Func<T> read) { Expect(","); return read(); }
        private ImmutableArray<T> Items<T>(Func<T> read)
        {
            var result = ImmutableArray.CreateBuilder<T>();
            if (Current == ")") return result.ToImmutable();
            do { result.Add(read()); } while (Match(","));
            return result.ToImmutable();
        }
        public Condition Condition()
        {
            var op = Name().ToLowerInvariant();
            Expect("(");
            Condition result = op switch
            {
                "true" => new Condition.Constant(Truth.True),
                "false" => new Condition.Constant(Truth.False),
                "unknown" => new Condition.Constant(Truth.Unknown),
                "exists" => new Condition.Exists(Noun()),
                "tag_at_least" => new Condition.Tagged(Noun(), Next(Tag), Next(Integer)),
                "related" => new Condition.Related(Noun(), Next(Relation), Next(Noun)),
                "at" => new Condition.At(Noun(), Next(Noun)),
                "kind" => new Condition.IsKind(Noun(), Next(EnumName<ProjectionKind>)),
                "not" => new Condition.Not(Condition()),
                "all" => new Condition.All(Items(Condition)),
                "any" => new Condition.Any(Items(Condition)),
                _ => throw Error($"未知条件 {op}。")
            };
            Expect(")");
            return result;
        }
        public Effect Effect()
        {
            var op = Name().ToLowerInvariant();
            Expect("(");
            Effect result = op switch
            {
                "unlock" => new Effect.Act(ActionOp.Unlock, Noun(), Next(Noun)),
                "travel" => new Effect.Act(ActionOp.Travel, Noun()),
                "take" => new Effect.Act(ActionOp.Take, Noun()),
                "inspect" => new Effect.Act(ActionOp.Inspect, Noun()),
                "destroy" => new Effect.Act(ActionOp.Destroy, Noun()),
                "set" => new Effect.Set(Noun(), Next(Tag), Next(Integer)),
                "change" => new Effect.Change(Noun(), Next(Tag), Next(Integer)),
                "temporary_set" => new Effect.TemporarySet(Noun(), Next(Tag), Next(Integer), Next(Delay)),
                "temporary_set_until_end" => new Effect.TemporarySetUntilEnd(Noun(), Next(Tag), Next(Integer)),
                "spend" => ReadSpend(),
                "relate" => new Effect.Relate(Noun(), Next(Relation), Next(Noun)),
                "unrelate" => new Effect.Relate(Noun(), Next(Relation), Next(Noun), true),
                "sequence" => new Effect.Sequence(Items(Effect)),
                "when" => new Effect.When(Condition(), Next(Effect)),
                "require" => new Effect.Require(Condition()),
                "wait" => new Effect.Wait(Delay(), Next(Effect)),
                "wait_until" => new Effect.Wait(Condition(), Next(Effect)),
                "say" => ReadSay(),
                "infer" => new Effect.Think(Condition()),
                _ => throw Error($"未知效果 {op}。")
            };
            Expect(")");
            return result;
        }
        private Effect ReadSpend()
        {
            var target = Noun();
            var tag = Next(Tag);
            var amount = Next(Integer);
            if (amount < 0) throw Error("支付量不能为负。");
            return new Effect.Spend(target, tag, amount);
        }
        private Effect ReadSay()
        {
            var recipient = Noun();
            var text = Next(Name);
            return new Effect.Say(recipient, text, Match(",") ? Effect() : null);
        }
        public ImmutableArray<Rule> Rules()
        {
            Expect("RULE");
            var condition = Condition();
            Expect("=>");
            var rules = ImmutableArray.CreateBuilder<Rule>();
            do
            {
                var mode = EnumName<RuleMode>();
                rules.Add(new Rule(condition, mode, Effect()));
            } while (Match(","));
            return rules.ToImmutable();
        }
    }
}

