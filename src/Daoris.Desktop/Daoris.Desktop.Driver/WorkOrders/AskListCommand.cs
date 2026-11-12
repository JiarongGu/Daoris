using Daoris.Contracts.Asks;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris ask list [--workspace &lt;name&gt;|--all] [--open] [--json]</c>: a read of the asks this machine holds, every workspace's
/// unless one is named, closed ones too unless <c>--open</c>. It changes nothing. A word that is a verb of <c>ask</c> and no verb of its
/// own is never an ask's whole text (<see cref="MeantVerb"/>), so a read mistyped is told, not published.
/// </summary>
public static class AskListCommand
{
    /// <summary>Each word that is a verb of <c>ask</c>, or the one a person reaches for to list, and what to run for it.</summary>
    private static readonly IReadOnlyDictionary<string, string> Meant = BuildMeant();

    private static Dictionary<string, string> BuildMeant()
    {
        var meant = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var verb in new[] { "list", "ls", "show", "get", "view", "status", "read" }) meant[verb] = "daoris ask list [--workspace <name>|--all] [--open] [--json]";
        foreach (var verb in new[] { "publish", "close", "delete", "pause", "resume", "abandon", "clear", "go-ahead", "review", "workflow", "through" })
        {
            meant[verb] = $"daoris ask {verb} <id> …";
        }

        meant["channel"] = $"daoris {ChannelCommand.Usage}";
        meant["at-work"] = "daoris ask at-work";
        return meant;
    }

    /// <summary>The refusal of an ask whose whole text is one word that names a verb, saying which verb was probably meant; null for any other ask.</summary>
    public static string? MeantVerb(AskArguments read)
    {
        if (read.Words.Count != 1 || read.Files.Count > 0 || read.Links.Count > 0) return null;
        var word = read.Words[0].Trim();
        return Meant.TryGetValue(word, out var run)
            ? $"`{word}` alone is a verb of `daoris ask`, not an ask, so nothing was asked. Probably meant: `{run}`. To ask it as words, say more than that one word."
            : null;
    }

    /// <summary>What <c>ask list</c> takes: its flags only.</summary>
    public static string? Combined(AskArguments read, string? otherAct)
    {
        if (otherAct is not null) return "`ask list` is a read and takes no other act.";
        if (read.All && read.Workspace is not null) return "`ask list` takes --workspace <name> or --all, not both.";
        if (read.Words.Count > 0 || read.Files.Count > 0 || read.Links.Count > 0 || read.To is not null || read.Short is not null
            || read.Review is not null || read.Kind is not null || read.Workflow is not null || read.Through is not null || read.Requirements.Count > 0)
        {
            return "`ask list` takes --workspace <name> or --all, --open and --json only.";
        }

        return null;
    }

    /// <summary>One ask as a line: its id, state, workspace and its words, cut to a line.</summary>
    public static string Line(string id, string state, string workspace, string sentence)
    {
        var words = sentence.ReplaceLineEndings(" ").Trim();
        if (words.Length > 70) words = words[..69] + "…";
        return $"#{id.TrimStart('#')}  {state}  {workspace}  {words}";
    }

    /// <summary>Print the asks, or their JSON as the service wrote it; 0 read.</summary>
    public static async Task<int> RunAsync(AskArguments read, ServiceClient service, TextWriter output)
    {
        var sent = await service.SendAsync(new ListAsks(read.All ? null : read.Workspace, IncludeClosed: !read.Open)).ConfigureAwait(false);
        var asks = sent.Answered();
        if (read.Json)
        {
            output.WriteLine(sent.Reply.Payload);
            return 0;
        }

        if (asks.Length == 0) output.WriteLine(read.Open ? "no open asks." : "no asks.");
        foreach (var ask in asks) output.WriteLine(Line(ask.Id, ask.State, ask.Workspace, ask.Sentence));
        return 0;
    }
}
