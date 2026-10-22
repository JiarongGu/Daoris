namespace Daoris.Driver;

/// <summary>Why the person's ask of a piece was not written, by code: what the page words and the verb says (COAGENT1b).</summary>
public static class PartnerAskWhy
{
    /// <summary>The piece is blank.</summary>
    public const string Blank = "blank";

    /// <summary>The piece is longer than <see cref="PartnerAsk.PieceLimit"/>.</summary>
    public const string TooLong = "too-long";

    /// <summary>No agent is named, or the name is not one word.</summary>
    public const string Agent = "agent";

    /// <summary>A path leads outside the repository.</summary>
    public const string Path = "path";

    /// <summary>No session of this machine's is held under the id.</summary>
    public const string NotHeld = "not-held";

    /// <summary>The session cannot lead a piece: a partner, a helper, a reviewer, an intake or Ask Daoris's conversation, or one with no tree.</summary>
    public const string Unfit = "unfit";

    /// <summary>The session's tree has no commit to grow a piece from.</summary>
    public const string NoCommit = "no-commit";

    public static readonly IReadOnlyList<string> All = [Blank, TooLong, Agent, Path, NotHeld, Unfit, NoCommit];
}

/// <summary>Where a lead's tree stood at the ask: its commit, or null where none was read, and its uncommitted paths.</summary>
public sealed record PieceBase(string? Head, IReadOnlyList<string> Uncommitted);

/// <summary>What the person's ask came to: the piece's id where it was written, or the code and sentence of why not.</summary>
public sealed record PartnerAsked(string? Asked, string? Why, string Message);

/// <summary>
/// The person's own ask of a piece (COAGENT1b, D199 point 2; the co-agent design §2.3 step 1, §6.1, §6.4): the one judge and writer the
/// terminal's <c>daoris-driver sessions partner</c> and the page's <i>Ask another agent to work on a piece…</i> share. It judges what a
/// reader of the file cannot know (the words, the agent named, the paths, the session's record and its tree's commit), then writes the
/// piece's ask once, at the exact commit the lead's tree stands at and with its uncommitted paths, asked by the person. Everything else
/// (the choice, the measured admission, the reservation) is the driver's look's, which judges it at its next look.
/// </summary>
public static class PartnerPersonAsk
{
    /// <summary>Judge and write the ask, or say why not. <paramref name="read"/> reads the lead's tree, git's in production.</summary>
    public static async Task<PartnerAsked> AskAsync(
        string home, ServiceClient service, string session, string piece, IReadOnlyList<string> paths, string? agent,
        Func<string, CancellationToken, Task<PieceBase>> read, DateTimeOffset at, CancellationToken ct = default)
    {
        piece = piece.Trim();
        static PartnerAsked No(string why, string sentence) => new(null, why, $"{sentence} Nothing was asked.");
        if (piece.Length == 0) return No(PartnerAskWhy.Blank, "A piece says what to write, and none was given.");
        if (piece.Length > PartnerAsk.PieceLimit)
        {
            return No(PartnerAskWhy.TooLong, $"A piece is at most {PartnerAsk.PieceLimit} characters, and this is {piece.Length}.");
        }

        if (agent is not { Length: > 0 } || agent.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return No(PartnerAskWhy.Agent, "A piece names the agent that writes it, as one word as it is listed in Settings.");
        }

        var named = paths.Select(path => path.Trim()).Where(path => path.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (named.FirstOrDefault(path => !PartnerJudge.Inside(path)) is { } outside)
        {
            return No(PartnerAskWhy.Path, $"`{outside}` leads outside the repository, and a piece's paths stay inside it.");
        }

        var lead = await service.RecordAsync(session, ct).ConfigureAwait(false);
        if (lead is null or { Teammate: true }) return No(PartnerAskWhy.NotHeld, $"No session `{session}` of this machine's is held to bring a piece back to.");
        if (lead.Serves is not null || lead.Opinion is not null || lead.Ask is { Length: > 0 } || lead.HelpMode is { Length: > 0 }
            || lead.Tree is not { Length: > 0 })
        {
            return No(PartnerAskWhy.Unfit,
                $"Session `{session}` cannot lead a piece: only a session working in a tree of its own does, never a partner, a helper, a reviewer, "
                + "an intake or Ask Daoris's conversation.");
        }

        var stood = await read(lead.Tree, ct).ConfigureAwait(false);
        if (stood.Head is not { Length: > 0 } head || !head.All(Uri.IsHexDigit))
        {
            return No(PartnerAskWhy.NoCommit, $"Session `{session}`'s tree has no commit to grow a piece from.");
        }

        var id = Guid.NewGuid().ToString("N")[..8];
        new PartnerFiles(home).WriteAsk(new PartnerAsk(id, session, piece, named, agent, head, stood.Uncommitted, PartnerAsk.ByPerson, at));
        return new PartnerAsked(id, null,
            $"Asked piece `#{id}` of {agent} for session {session}: the driver judges it at its next look and starts it only where {agent}'s "
            + "confinement as a partner was measured. It writes in a clone of its own, and its answer comes back to that session naming the "
            + "commit to merge.");
    }

    /// <summary>The lead's tree as git reads it: <c>HEAD</c>'s commit, or null, and the paths <c>status</c> lists, untracked ones included.</summary>
    public static async Task<PieceBase> ReadTreeAsync(string tree, CancellationToken ct)
    {
        if (!Directory.Exists(tree)) return new PieceBase(null, []);
        var (headCode, head, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--verify", "-q", "HEAD"], ct).ConfigureAwait(false);
        var (statusCode, status, _) = await WorkingTree.GitAsync(
            tree, ["--no-optional-locks", "-c", "core.quotePath=false", "status", "--porcelain", "-z", "--untracked-files=all"], ct).ConfigureAwait(false);
        return new PieceBase(headCode == 0 ? head.Trim() : null, statusCode == 0 ? Porcelain(status) : []);
    }

    /// <summary>
    /// The paths of <c>status --porcelain -z</c>: each entry's path after its two status letters, a rename's or a copy's source (the entry
    /// after it) skipped.
    /// </summary>
    public static IReadOnlyList<string> Porcelain(string status)
    {
        var paths = new List<string>();
        var entries = status.Split('\0');
        for (var at = 0; at < entries.Length; at++)
        {
            var entry = entries[at];
            if (entry.Length < 4) continue;
            paths.Add(entry[3..]);
            if (entry[0] is 'R' or 'C' || entry[1] is 'R' or 'C') at++;
        }

        return [.. paths.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }
}
