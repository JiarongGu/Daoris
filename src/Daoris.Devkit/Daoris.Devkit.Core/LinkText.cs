using System.Text.RegularExpressions;
using Daoris.Knowledge;

namespace Daoris.Devkit;

/// <summary>
/// The text of a markdown document its links are read from: a fenced block and a code span quote a link as an example,
/// and nobody follows it.
/// </summary>
/// <remarks>
/// Where a fence starts and stops is the service's <see cref="MarkdownFence"/>, compiled into this library
/// (<c>Daoris.Devkit.Core.csproj</c>) rather than read again, so a fence is one fence to every reader. A code span is a
/// backtick run closed by the next run of the same length, on one line.
/// </remarks>
internal static class LinkText
{
    private static readonly Regex CodeSpan = new(@"(?<!`)(`+)(?!`).+?(?<!`)\1(?!`)", RegexOptions.Compiled);

    /// <summary>The document with every fenced line and code span removed, its other lines as they were.</summary>
    public static string Of(string markdown)
    {
        var fence = new MarkdownFence();
        var lines = markdown.Split('\n');
        for (var index = 0; index < lines.Length; index++)
            lines[index] = fence.Holds(lines[index]) ? "" : CodeSpan.Replace(lines[index], "");
        return string.Join('\n', lines);
    }
}
