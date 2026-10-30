namespace Daoris.Driver;

/// <summary>What <c>daoris-driver integrate</c> was asked: a repository to show, and an act on it where one is named.</summary>
public sealed record IntegrateAsk(string Repository)
{
    /// <summary>One of <see cref="IntegrateCommand.Acts"/>; null shows the repository's integration.</summary>
    public string? Act { get; init; }

    /// <summary>The branch, request or candidate the act names.</summary>
    public string? Target { get; init; }
}

/// <summary>
/// <c>daoris-driver integrate &lt;repository&gt;</c> (INTEGRATE1h; the integration design §5.8): shows a repository's integration, read from the
/// record. <c>--request</c>, <c>--drop</c>, <c>--recheck</c>, <c>--decide</c> and <c>--order</c> are the owner's acts, Person routes: a card in
/// Daoris's window (D156), which this terminal asks and never answers.
/// </summary>
public static class IntegrateCommand
{
    public const string Usage =
        "usage: daoris-driver integrate <repository> [--request <branch> | --drop <request> | --recheck | --decide <candidate> split|hand-back|land | --order <request> first|last]";

    /// <summary>The acts, as their flags spell them; the verb is <c>hand-back</c> on every door.</summary>
    public static IReadOnlyList<string> Acts { get; } = ["--request", "--drop", "--recheck", "--decide", "--order"];

    /// <summary>The words after <c>integrate</c>, or null with what is wrong with them.</summary>
    public static IntegrateAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args.Count == 0 || args[0].StartsWith('-'))
        {
            problem = "`integrate` takes the repository first.";
            return null;
        }

        if (args.Count == 1) return new IntegrateAsk(args[0].Trim());
        if (!Acts.Contains(args[1]))
        {
            problem = $"`{args[1]}` is not a word `integrate` takes.";
            return null;
        }

        var wanted = args[1] is "--recheck" ? 0 : args[1] is "--decide" or "--order" ? 2 : 1;
        if (args.Count - 2 != wanted || args.Skip(2).Any(word => word.StartsWith('-')))
        {
            problem = $"`{args[1]}` takes {(wanted == 0 ? "no word" : wanted == 1 ? "one word" : "two words")} after it.";
            return null;
        }

        return new IntegrateAsk(args[0].Trim()) { Act = args[1][2..], Target = args.Count > 2 ? string.Join(' ', args.Skip(2)) : null };
    }

    /// <summary>The lines that show <paramref name="look"/>: the queue, each candidate with its steps, what was handed back, what awaits a disposition, the history.</summary>
    public static IReadOnlyList<string> Said(IntegrationLook look)
    {
        var said = new List<string>();
        void Requests(string title, IReadOnlyList<IntegrationLookRequest> rows)
        {
            if (rows.Count == 0) return;
            said.Add($"{title}:");
            said.AddRange(rows.Select(row => $"  {row.Request}  {row.Branch} @ {Short(row.Tip)} into `{row.Line}`{(row.Said is { Length: > 0 } words ? $" — {words}" : "")}"));
        }

        said.Add($"integrate: `{look.Repository}`");
        Requests("queued", look.Queue);
        foreach (var candidate in look.Candidates)
        {
            said.Add($"candidate {candidate.Candidate} for `{candidate.Line}` on {Short(candidate.Base)}: at {candidate.At ?? "its start"}");
            said.Add($"  steps: {string.Join(" > ", candidate.Steps.Select(step => $"{step.Step} ({step.State})"))}");
            said.AddRange(candidate.Members.Select(member => $"  {member.Request}  {member.Branch}: {member.State}{(member.Said is { Length: > 0 } words ? $" — {words}" : "")}"));
            if (candidate.Holds is { } holds) said.Add($"  held: {holds}; `--{candidate.Release}` by the {candidate.By} releases it.");
        }

        Requests("handed back", look.HandedBack);
        if (look.Superseded.Count > 0)
        {
            said.Add("superseded, awaiting a disposition:");
            said.AddRange(look.Superseded.Select(each => $"  {each.Request}  {Short(each.Commit)} {each.Subject} (superseded by {Short(each.By)}, {each.Files} files)"));
        }

        Requests("integrated", look.Promoted);
        if (said.Count == 1) said.Add("  nothing is asked into this repository's line, and nothing was integrated yet.");
        return said;
    }

    /// <summary>What a terminal says to an act: the owner's acts are cards in the window.</summary>
    public static string ActSaid(IntegrateAsk ask) =>
        $"integrate: `--{ask.Act}` is the owner's act, a card in Daoris's window (D156): open `{ask.Repository}`'s Integration tab there.";

    private static string Short(string commit) => commit.Length > 7 ? commit[..7] : commit;
}
