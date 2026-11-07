namespace Daoris.Driver;

/// <summary>
/// One readiness check as the plugin's page draws it: what it means, whether it stands once the person has asked,
/// and the press its <c>fix</c> makes.
/// </summary>
/// <param name="Fix">The command as the manifest wrote it, shown beside the press.</param>
/// <param name="SignIn">Whether the press reads <i>Sign in</i>, not <i>Set up</i>.</param>
/// <param name="Press">Whether the fix may be pressed: it names a command of the plugin's own tools.</param>
/// <param name="Refusal">Why a fix cannot be pressed, where there is one.</param>
/// <param name="Ready">Whether the check passed; null until the person has asked.</param>
/// <param name="Sentence">What the check answered; null until asked.</param>
public sealed record CheckStanding(string Says, string? Fix, bool SignIn, bool Press, string? Refusal, bool? Ready, string? Sentence);

/// <summary>
/// A declared tool as the plugin's page draws it: what the manifest says of it, and, once the person has asked, what
/// the machine answered. A tool read only from the manifest has <see cref="Found"/> null.
/// </summary>
/// <param name="Install">The plugin's words for getting the tool on this machine's platform.</param>
/// <param name="Problem">Why the entry is not read, beside the tool.</param>
/// <param name="Found">Whether the tool is found; null until asked.</param>
/// <param name="Ok">Whether it is found in its range; null until asked.</param>
/// <param name="Sentence">The tool's own sentence from the check; null until asked.</param>
public sealed record ToolStanding(
    string Id, string Name, string? For, string? Versions, string? Install, string? Problem, bool? Found, bool? Ok, string? Version,
    string? Sentence, IReadOnlyList<CheckStanding> Checks)
{
    /// <summary>A tool as its manifest says it, before anything has been asked of the machine.</summary>
    public static ToolStanding Declared(PluginTool tool, IReadOnlyList<PluginTool> tools) =>
        new(tool.Id ?? "", tool.Name ?? tool.Id ?? "", tool.For, tool.Versions, PluginTools.InstallHere(tool), tool.Problem, null, null, null, null,
            [.. tool.Ready.Select(check => One(check, tools, null))]);

    /// <summary>A tool as the machine answered the person's check of it.</summary>
    public static ToolStanding Of(PluginToolFound found, IReadOnlyList<PluginTool> tools)
    {
        var tool = found.Tool;
        return new(tool.Id ?? "", tool.Name ?? tool.Id ?? "", tool.For, tool.Versions, PluginTools.InstallHere(tool), tool.Problem,
            found.File is not null, found.Ok, found.Version, found.Sentence,
            [.. tool.Ready.Select((check, at) => One(check, tools, at < found.Ready.Count ? found.Ready[at] : null))]);
    }

    private static CheckStanding One(ReadyCheck check, IReadOnlyList<PluginTool> tools, ReadyAnswer? answer)
    {
        var (words, problem) = PluginTools.FixWords(tools, check);
        return new(check.Says, check.Fix, check.SignIn, words is not null, problem, answer?.Ready, answer?.Sentence);
    }
}
