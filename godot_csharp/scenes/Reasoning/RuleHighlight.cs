using Godot;

namespace DualOntologyWorldEngine.Game.Reasoning;

public partial class RuleHighlight : SyntaxHighlighter
{
    public int Stage { get; set; }
    public bool Utterance { get; set; }
    public override Godot.Collections.Dictionary _GetLineSyntaxHighlighting(int line)
    {
        var text = GetTextEdit().GetLine(line);
        var output = new Godot.Collections.Dictionary();
        if (Utterance)
        {
            var known = UtteranceLanguage.Vocabulary(Stage).Select(w => w.Word).ToHashSet();
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text, @"#.*|\s+|[.。]|[^\s.。#]+"))
            {
                var token = match.Value;
                var color = token.StartsWith('#') ? new Color("768274") : string.IsNullOrWhiteSpace(token) || token is "." or "。" ? new Color("202830") :
                    !known.Contains(token) ? new Color("c43a42") : UtteranceLanguage.Particles.Contains(token) ? new Color("246bc4") : new Color("202830");
                output[match.Index] = new Godot.Collections.Dictionary { ["color"] = color };
            }
            return output;
        }
        var vocabulary = RuleLanguage.Vocabulary(Stage).OrderByDescending(w => w.Length).ToArray();
        var black = new Color("202830");
        var blue = new Color("246bc4");
        var red = new Color("c43a42");
        for (var i = 0; i < text.Length;)
        {
            var start = i;
            Color color;
            if (text[i] == '#') { color = new Color("768274"); i = text.Length; }
            else if (char.IsWhiteSpace(text[i]) || "。；;".Contains(text[i])) { color = black; i++; }
            else if (char.IsAsciiDigit(text[i]))
            {
                while (i < text.Length && char.IsAsciiDigit(text[i])) i++;
                color = int.TryParse(text[start..i], out var n) && n is >= 1 and <= 120 ? black : red;
            }
            else
            {
                var word = vocabulary.FirstOrDefault(w => text.AsSpan(i).StartsWith(w));
                color = word is null ? red : RuleLanguage.Keywords.Contains(word) ? blue : black;
                i += word?.Length ?? 1;
            }
            output[start] = new Godot.Collections.Dictionary { ["color"] = color };
        }
        return output;
    }
}
