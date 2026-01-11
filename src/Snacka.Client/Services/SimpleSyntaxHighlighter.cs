using System.Text.RegularExpressions;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Snacka.Client.Services;

/// <summary>
/// A simple regex-based syntax highlighter that provides basic highlighting
/// for common code patterns across multiple languages.
/// </summary>
public class SimpleSyntaxHighlighter : ISyntaxHighlighter
{
    // Colors matching common dark theme syntax highlighting
    private static readonly IBrush KeywordColor = new SolidColorBrush(Color.Parse("#c586c0"));    // Purple
    private static readonly IBrush StringColor = new SolidColorBrush(Color.Parse("#ce9178"));     // Orange/brown
    private static readonly IBrush CommentColor = new SolidColorBrush(Color.Parse("#6a9955"));    // Green
    private static readonly IBrush NumberColor = new SolidColorBrush(Color.Parse("#b5cea8"));     // Light green
    private static readonly IBrush TypeColor = new SolidColorBrush(Color.Parse("#4ec9b0"));       // Teal
    private static readonly IBrush FunctionColor = new SolidColorBrush(Color.Parse("#dcdcaa"));   // Yellow
    private static readonly IBrush DefaultColor = new SolidColorBrush(Color.Parse("#d4d4d4"));    // Light gray

    private static readonly FontFamily CodeFont = new("Consolas, Monaco, 'Courier New', monospace");

    // Common keywords across many languages
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Control flow
        "if", "else", "elif", "switch", "case", "default", "for", "foreach", "while", "do",
        "break", "continue", "return", "yield", "throw", "try", "catch", "finally", "except",
        "raise", "with", "async", "await", "goto",

        // Declarations
        "var", "let", "const", "function", "fn", "func", "def", "class", "struct", "enum",
        "interface", "trait", "impl", "type", "namespace", "package", "module", "import",
        "export", "from", "using", "include", "require", "extends", "implements", "override",
        "virtual", "abstract", "static", "public", "private", "protected", "internal", "readonly",
        "final", "sealed", "partial", "record",

        // Values
        "true", "false", "null", "nil", "None", "undefined", "void", "this", "self", "super",
        "base", "new", "delete", "typeof", "instanceof", "sizeof", "as", "is", "in", "not",
        "and", "or", "lambda", "where", "select", "from", "join", "on", "group", "by", "into",
        "orderby", "ascending", "descending", "get", "set", "value", "init", "required"
    };

    // Types that are commonly capitalized
    private static readonly Regex TypePattern = new(@"\b([A-Z][a-zA-Z0-9_]*)\b", RegexOptions.Compiled);

    // Function calls pattern
    private static readonly Regex FunctionPattern = new(@"\b([a-zA-Z_][a-zA-Z0-9_]*)\s*\(", RegexOptions.Compiled);

    public List<Inline> Highlight(string code, string? language = null)
    {
        var inlines = new List<Inline>();
        var tokens = Tokenize(code);

        foreach (var token in tokens)
        {
            var run = new Run(token.Text)
            {
                FontFamily = CodeFont,
                Foreground = GetColorForToken(token)
            };
            inlines.Add(run);
        }

        return inlines;
    }

    private static IBrush GetColorForToken(Token token)
    {
        return token.Type switch
        {
            TokenType.Keyword => KeywordColor,
            TokenType.String => StringColor,
            TokenType.Comment => CommentColor,
            TokenType.Number => NumberColor,
            TokenType.Type => TypeColor,
            TokenType.Function => FunctionColor,
            _ => DefaultColor
        };
    }

    private static List<Token> Tokenize(string code)
    {
        var tokens = new List<Token>();
        var i = 0;

        while (i < code.Length)
        {
            // Check for single-line comment
            if (i < code.Length - 1 && code[i] == '/' && code[i + 1] == '/')
            {
                var end = code.IndexOf('\n', i);
                if (end == -1) end = code.Length;
                tokens.Add(new Token(code.Substring(i, end - i), TokenType.Comment));
                i = end;
                continue;
            }

            // Check for multi-line comment
            if (i < code.Length - 1 && code[i] == '/' && code[i + 1] == '*')
            {
                var end = code.IndexOf("*/", i + 2);
                if (end == -1) end = code.Length - 2;
                tokens.Add(new Token(code.Substring(i, end + 2 - i), TokenType.Comment));
                i = end + 2;
                continue;
            }

            // Check for Python/shell comment
            if (code[i] == '#')
            {
                var end = code.IndexOf('\n', i);
                if (end == -1) end = code.Length;
                tokens.Add(new Token(code.Substring(i, end - i), TokenType.Comment));
                i = end;
                continue;
            }

            // Check for string (double quote)
            if (code[i] == '"')
            {
                var end = FindStringEnd(code, i + 1, '"');
                tokens.Add(new Token(code.Substring(i, end - i + 1), TokenType.String));
                i = end + 1;
                continue;
            }

            // Check for string (single quote)
            if (code[i] == '\'')
            {
                var end = FindStringEnd(code, i + 1, '\'');
                tokens.Add(new Token(code.Substring(i, end - i + 1), TokenType.String));
                i = end + 1;
                continue;
            }

            // Check for template string (backtick)
            if (code[i] == '`')
            {
                var end = FindStringEnd(code, i + 1, '`');
                tokens.Add(new Token(code.Substring(i, end - i + 1), TokenType.String));
                i = end + 1;
                continue;
            }

            // Check for number
            if (char.IsDigit(code[i]) || (code[i] == '.' && i + 1 < code.Length && char.IsDigit(code[i + 1])))
            {
                var end = i;
                while (end < code.Length && (char.IsDigit(code[end]) || code[end] == '.' ||
                       code[end] == 'x' || code[end] == 'X' ||
                       (code[end] >= 'a' && code[end] <= 'f') ||
                       (code[end] >= 'A' && code[end] <= 'F') ||
                       code[end] == '_'))
                {
                    end++;
                }
                tokens.Add(new Token(code.Substring(i, end - i), TokenType.Number));
                i = end;
                continue;
            }

            // Check for identifier/keyword
            if (char.IsLetter(code[i]) || code[i] == '_')
            {
                var end = i;
                while (end < code.Length && (char.IsLetterOrDigit(code[end]) || code[end] == '_'))
                {
                    end++;
                }
                var word = code.Substring(i, end - i);

                // Check if it's followed by ( to identify function calls
                var nextNonSpace = end;
                while (nextNonSpace < code.Length && char.IsWhiteSpace(code[nextNonSpace]))
                {
                    nextNonSpace++;
                }

                TokenType type;
                if (Keywords.Contains(word))
                {
                    type = TokenType.Keyword;
                }
                else if (nextNonSpace < code.Length && code[nextNonSpace] == '(')
                {
                    type = TokenType.Function;
                }
                else if (char.IsUpper(word[0]) && word.Length > 1)
                {
                    type = TokenType.Type;
                }
                else
                {
                    type = TokenType.Default;
                }

                tokens.Add(new Token(word, type));
                i = end;
                continue;
            }

            // Default: single character
            tokens.Add(new Token(code[i].ToString(), TokenType.Default));
            i++;
        }

        return tokens;
    }

    private static int FindStringEnd(string code, int start, char quote)
    {
        var i = start;
        while (i < code.Length)
        {
            if (code[i] == '\\' && i + 1 < code.Length)
            {
                i += 2; // Skip escaped character
                continue;
            }
            if (code[i] == quote)
            {
                return i;
            }
            i++;
        }
        return code.Length - 1;
    }

    private enum TokenType
    {
        Default,
        Keyword,
        String,
        Comment,
        Number,
        Type,
        Function
    }

    private record Token(string Text, TokenType Type);
}
