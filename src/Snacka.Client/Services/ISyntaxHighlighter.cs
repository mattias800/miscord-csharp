using Avalonia.Controls.Documents;

namespace Snacka.Client.Services;

/// <summary>
/// Interface for syntax highlighting code blocks.
/// Implement this interface to provide custom syntax highlighting.
/// </summary>
public interface ISyntaxHighlighter
{
    /// <summary>
    /// Highlights code and returns a list of styled inline elements.
    /// </summary>
    /// <param name="code">The code to highlight</param>
    /// <param name="language">Optional language hint (e.g., "csharp", "python", "javascript")</param>
    /// <returns>List of styled Inline elements</returns>
    List<Inline> Highlight(string code, string? language = null);
}
