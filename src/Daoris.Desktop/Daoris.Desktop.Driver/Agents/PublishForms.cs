using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What <c>no-publish</c> denies by its sentence alone, listed in code as <c>no-push</c>'s four forms are: each publishing program's
/// publish and a release's creation, as the shapes a rule takes (<c>Bash(npm publish:*)</c>, <c>Bash(npm -* publish)</c>), on both
/// shell tools. They class a widening and are never handed as denies, so a session's handed rules and the person's own allow of a
/// publish stay as they were.
/// </summary>
public static class PublishForms
{
    private static readonly (string Program, string Verb)[] Publishing =
    [
        ("npm", "publish"), ("pnpm", "publish"), ("yarn", "publish"), ("yarn", "npm publish"), ("cargo", "publish"),
        ("dotnet", "nuget push"), ("twine", "upload"), ("gh", "release create"), ("uv", "publish"), ("poetry", "publish"),
        ("gem", "push"), ("docker", "push"),
    ];

    private static readonly string[] Tools = ["Bash", "PowerShell"];

    /// <summary>Every form, as rules: the plain publish, the publish with arguments, and the options-first shapes.</summary>
    public static IReadOnlyList<string> Rules { get; } =
    [
        .. Tools.SelectMany(tool => Publishing.SelectMany(each => new[]
        {
            $"{tool}({each.Program} {each.Verb})", $"{tool}({each.Program} {each.Verb}:*)",
            $"{tool}({each.Program} -* {each.Verb})", $"{tool}({each.Program} -* {each.Verb} *)",
        })),
    ];

    // The commands the forms match: the publish bare, with an argument, and with options before its subcommand.
    private static readonly IReadOnlyList<(string Tool, string Command)> Commands =
    [
        .. Tools.SelectMany(tool => Publishing.SelectMany(each => new[]
        {
            each.Program + " " + each.Verb, each.Program + " " + each.Verb + " x",
            each.Program + " -g " + each.Verb, each.Program + " -g " + each.Verb + " x",
            each.Program + " --tag x " + each.Verb,
        }.Select(command => (tool, command)))),
    ];

    private static readonly Regex Shape = new(@"^([A-Za-z][A-Za-z0-9_-]*)(?:\((.*)\))?$", RegexOptions.CultureInvariant | RegexOptions.Singleline);

    /// <summary>
    /// Whether <paramref name="rule"/> would match a command one of the forms matches, read as the harness reads it: <c>:*</c> a prefix,
    /// <c>*</c> any text, and a shell tool named with no specifier every command.
    /// </summary>
    public static bool Matches(string rule)
    {
        if (Shape.Match(rule.Trim()) is not { Success: true } shape) return false;
        var tool = shape.Groups[1].Value;
        if (!Tools.Contains(tool, StringComparer.Ordinal)) return false;
        if (!shape.Groups[2].Success) return true;
        var pattern = Pattern(shape.Groups[2].Value);
        return Commands.Any(each => each.Tool == tool && pattern.IsMatch(each.Command));
    }

    private static Regex Pattern(string specifier)
    {
        var prefix = specifier.EndsWith(":*", StringComparison.Ordinal);
        var body = string.Join(".*", (prefix ? specifier[..^2] : specifier).Split('*').Select(Regex.Escape));
        return new Regex(prefix ? $"^{body}( .*)?$" : $"^{body}$", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    }
}
