namespace DualOntologyWorldEngine.Core.Rules;

/// <summary>AST 文本编译器记号类型。</summary>
public enum TokenType
{
    Rule,
    Then,
    Require,
    Forbid,
    Permit,
    Prefer,
    Identifier,
    Slot,
    EntityRef,
    Integer,
    Float,
    String,
    Boolean,
    Arrow,
    OpenParen,
    CloseParen,
    Comma,
    Eof
}

/// <summary>编译管线词法记号记录。</summary>
/// <param name="Type">记号种类。</param>
/// <param name="Lexeme">源码字面量。</param>
/// <param name="Line">起始行号（1-indexed）。</param>
/// <param name="Column">起始列号（1-indexed）。</param>
/// <param name="Value">已解析的常量值。</param>
public readonly record struct Token(
    TokenType Type,
    string Lexeme,
    int Line,
    int Column,
    object? Value = null);

/// <summary>词法分析器，将 DSL 源码切分为 Token 流。</summary>
public sealed class RuleLexer
{
    private readonly string _source;
    private int _start;
    private int _current;
    private int _line = 1;
    private int _column = 1;
    private int _tokenStartColumn = 1;

    public RuleLexer(string source)
    {
        _source = source ?? string.Empty;
    }

    public IReadOnlyList<Token> ScanTokens()
    {
        var tokens = new List<Token>();
        while (!IsAtEnd())
        {
            _start = _current;
            _tokenStartColumn = _column;
            var token = ScanToken();
            if (token.HasValue) tokens.Add(token.Value);
        }
        tokens.Add(new Token(TokenType.Eof, string.Empty, _line, _column));
        return tokens;
    }

    private bool IsAtEnd() => _current >= _source.Length;

    private char Advance()
    {
        var c = _source[_current++];
        _column++;
        return c;
    }

    private char Peek() => IsAtEnd() ? '\0' : _source[_current];
    private char PeekNext() => _current + 1 >= _source.Length ? '\0' : _source[_current + 1];

    private bool Match(char expected)
    {
        if (IsAtEnd() || _source[_current] != expected) return false;
        _current++;
        _column++;
        return true;
    }

    private Token? ScanToken()
    {
        var c = Advance();
        switch (c)
        {
            case '(': return MakeToken(TokenType.OpenParen);
            case ')': return MakeToken(TokenType.CloseParen);
            case ',': return MakeToken(TokenType.Comma);
            case '=':
                if (Match('>')) return MakeToken(TokenType.Arrow, "=>");
                throw new InvalidOperationException($"[{_line}:{_tokenStartColumn}] 意外字符 '='，期望 '=>'");
            case '$': return ScanSlot();
            case '@': return ScanEntityRef();
            case '"':
            case '\'': return ScanString(c);
            case ' ':
            case '\r':
            case '\t':
                return null;
            case '\n':
                _line++;
                _column = 1;
                return null;
            default:
                if (char.IsDigit(c) || (c == '-' && char.IsDigit(Peek())))
                    return ScanNumber(c);
                if (char.IsLetter(c) || c == '_')
                    return ScanIdentifier();
                throw new InvalidOperationException($"[{_line}:{_tokenStartColumn}] 未知字符: '{c}'");
        }
    }

    private Token ScanSlot()
    {
        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_')) Advance();
        var name = _source[(_start + 1).._current];
        return new Token(TokenType.Slot, "$" + name, _line, _tokenStartColumn, name);
    }

    private Token ScanEntityRef()
    {
        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_')) Advance();
        var name = _source[(_start + 1).._current];
        return new Token(TokenType.EntityRef, "@" + name, _line, _tokenStartColumn, name);
    }

    private Token ScanString(char quote)
    {
        var sb = new System.Text.StringBuilder();
        while (!IsAtEnd() && Peek() != quote)
        {
            if (Peek() == '\n')
            {
                _line++;
                _column = 1;
            }
            if (Peek() == '\\' && _current + 1 < _source.Length)
            {
                Advance();
                var esc = Advance();
                sb.Append(esc switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => esc
                });
            }
            else
            {
                sb.Append(Advance());
            }
        }
        if (IsAtEnd()) throw new InvalidOperationException($"[{_line}:{_tokenStartColumn}] 字符串未闭合");
        Advance(); // 消费闭合引文
        var text = sb.ToString();
        return new Token(TokenType.String, text, _line, _tokenStartColumn, text);
    }

    private Token ScanNumber(char first)
    {
        while (char.IsDigit(Peek())) Advance();
        if (Peek() == '.' && char.IsDigit(PeekNext()))
        {
            Advance(); // 消费 '.'
            while (char.IsDigit(Peek())) Advance();
            var text = _source[_start.._current];
            var d = double.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
            return new Token(TokenType.Float, text, _line, _tokenStartColumn, d);
        }
        var intText = _source[_start.._current];
        var i = int.Parse(intText, System.Globalization.CultureInfo.InvariantCulture);
        return new Token(TokenType.Integer, intText, _line, _tokenStartColumn, i);
    }

    private Token ScanIdentifier()
    {
        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_')) Advance();
        var text = _source[_start.._current];
        var type = text.ToUpperInvariant() switch
        {
            "RULE" => TokenType.Rule,
            "THEN" => TokenType.Then,
            "REQUIRE" => TokenType.Require,
            "FORBID" => TokenType.Forbid,
            "PERMIT" => TokenType.Permit,
            "PREFER" => TokenType.Prefer,
            "TRUE" => TokenType.Boolean,
            "FALSE" => TokenType.Boolean,
            _ => TokenType.Identifier
        };
        object? val = type == TokenType.Boolean ? bool.Parse(text.ToLowerInvariant()) : text;
        return new Token(type, text, _line, _tokenStartColumn, val);
    }

    private Token MakeToken(TokenType type, string? text = null) =>
        new(type, text ?? _source[_start.._current], _line, _tokenStartColumn);
}

/// <summary>语法解析器与语义符号绑定器。</summary>
public sealed class RuleParser
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly Func<string, Entity?> _symbolResolver;
    private int _current;

    public RuleParser(IReadOnlyList<Token> tokens, Func<string, Entity?>? symbolResolver = null)
    {
        _tokens = tokens;
        _symbolResolver = symbolResolver ?? (_ => null);
    }

    public Rule ParseRule(string sourceTitle = "")
    {
        Consume(TokenType.Rule, "规则必须以 'RULE' 开头");
        var condition = ParseExpression();
        if (Check(TokenType.Arrow) || Check(TokenType.Then))
        {
            Advance();
        }
        else
        {
            throw Error(Peek(), "期望 '=>' 或 'THEN'");
        }

        var directives = new List<Directive>();
        do
        {
            directives.Add(ParseDirective());
        } while (Match(TokenType.Comma));

        if (!IsAtEnd() && Peek().Type != TokenType.Eof)
        {
            throw Error(Peek(), $"意外的未解析标记: '{Peek().Lexeme}'");
        }

        return new Rule(condition, directives, sourceTitle);
    }

    private Directive ParseDirective()
    {
        var modeToken = Advance();
        var mode = modeToken.Type switch
        {
            TokenType.Require => Mode.Require,
            TokenType.Forbid => Mode.Forbid,
            TokenType.Permit => Mode.Permit,
            TokenType.Prefer => Mode.Prefer,
            _ => throw Error(modeToken, "期望规约指令模式: REQUIRE, FORBID, PERMIT 或 PREFER")
        };

        var effectToken = Consume(TokenType.Identifier, "期望效果操作原语标识符");
        var args = ParseArgumentList();
        return new Directive(mode, new Effect(effectToken.Lexeme, args));
    }

    private Expr ParseExpression()
    {
        var opToken = Consume(TokenType.Identifier, "期望条件谓词标识符");
        var args = ParseArgumentList();
        return new Expr(opToken.Lexeme, args);
    }

    private object[] ParseArgumentList()
    {
        Consume(TokenType.OpenParen, "期望 '('");
        var args = new List<object>();
        if (!Check(TokenType.CloseParen))
        {
            do
            {
                args.Add(ParseArgument());
            } while (Match(TokenType.Comma));
        }
        Consume(TokenType.CloseParen, "期望 ')'");
        return args.ToArray();
    }

    private object ParseArgument()
    {
        var token = Peek();
        switch (token.Type)
        {
            case TokenType.Slot:
                Advance();
                var slotName = token.Value as string ?? token.Lexeme.TrimStart('$');
                if (!Enum.TryParse<RuleSlot>(slotName, true, out var slot))
                    throw Error(token, $"未知规则槽位: '${slotName}'。合法槽位为 Actor, Target, Tool, Scene");
                return slot;

            case TokenType.EntityRef:
                Advance();
                var entityName = token.Value as string ?? token.Lexeme.TrimStart('@');
                var entity = _symbolResolver(entityName);
                if (entity == null)
                    throw Error(token, $"未定义的实体引用符号: '@{entityName}'");
                return entity;

            case TokenType.Integer:
            case TokenType.Float:
            case TokenType.String:
            case TokenType.Boolean:
                Advance();
                return token.Value!;

            case TokenType.Identifier:
                // 嵌套表达式
                if (PeekNext().Type == TokenType.OpenParen)
                {
                    return ParseExpression();
                }
                Advance();
                return token.Lexeme;

            default:
                throw Error(token, $"期望合法表达式参数，但遇到了 '{token.Lexeme}'");
        }
    }

    private Token Advance()
    {
        if (!IsAtEnd()) _current++;
        return Previous();
    }

    private bool Check(TokenType type) => !IsAtEnd() && Peek().Type == type;

    private bool Match(TokenType type)
    {
        if (Check(type))
        {
            Advance();
            return true;
        }
        return false;
    }

    private Token Consume(TokenType type, string message)
    {
        if (Check(type)) return Advance();
        throw Error(Peek(), message);
    }

    private Token Peek() => _tokens[_current];
    private Token PeekNext() => _current + 1 >= _tokens.Count ? _tokens[^1] : _tokens[_current + 1];
    private Token Previous() => _tokens[_current - 1];
    private bool IsAtEnd() => Peek().Type == TokenType.Eof;

    private static Exception Error(Token token, string message) =>
        new InvalidOperationException($"[{token.Line}:{token.Column}] 编译错误: {message}");
}

/// <summary>标准 AST 文本编译器统一门面入口。</summary>
public static class RuleCompiler
{
    /// <summary>将 DSL 规则源码编译解析为强类型不可变 Rule AST。</summary>
    /// <param name="source">DSL 源码字符串。</param>
    /// <param name="symbolResolver">实体引用符号解析函数（查找 @Entity）。</param>
    /// <param name="sourceTitle">规则条文标题或注释说明。</param>
    /// <returns>解析生成的强类型 Rule 对象。</returns>
    public static Rule Compile(
        string source,
        Func<string, Entity?>? symbolResolver = null,
        string sourceTitle = "")
    {
        var lexer = new RuleLexer(source);
        var tokens = lexer.ScanTokens();
        var parser = new RuleParser(tokens, symbolResolver);
        return parser.ParseRule(sourceTitle);
    }

    /// <summary>使用上下文符号表字典编译 DSL 规则源码。</summary>
    public static Rule Compile(
        string source,
        IReadOnlyDictionary<string, Entity> symbols,
        string sourceTitle = "") =>
        Compile(source, name => symbols.GetValueOrDefault(name), sourceTitle);
}
