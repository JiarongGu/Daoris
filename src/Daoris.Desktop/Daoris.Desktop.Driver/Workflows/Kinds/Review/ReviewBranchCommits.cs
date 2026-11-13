namespace Daoris.Driver;

/// <summary>One commit of a review record's work: its id, author, time and subject, and the paths it touched.</summary>
public sealed record ReviewCommit(string Sha, string Author, DateTimeOffset? At, string Subject)
{
    public IReadOnlyList<string> Files { get; init; } = [];
}

/// <summary>A record's commits, newest first, and apart from them the paths its work left uncommitted.</summary>
public sealed record ReviewCommitList(IReadOnlyList<ReviewCommit> Commits, IReadOnlyList<string> Uncommitted)
{
    public static readonly ReviewCommitList None = new([], []);
}

/// <summary>
/// The commits a branch's or a checkout's review record holds, read from the commits it names and never from the checkout as it stands: a
/// branch's between its frozen base and its tip, a checkout's within each turn (a commit between two turns is not this work's).
/// </summary>
public static class ReviewBranchCommits
{
    private const int Most = 200;

    public static async Task<ReviewCommitList> ListAsync(string root, ReviewRecord record, CancellationToken ct = default)
    {
        var ranges = new List<(string From, string To)>();
        if (record.Kind == ReviewKinds.Branch)
        {
            var before = await ReviewCommits.CommitAsync(root, record, ReviewContent.Before, ct).ConfigureAwait(false);
            if (before is not null && record.Reference.Tip is { } tip) ranges.Add((before, tip));
        }
        else if (record.Kind == ReviewKinds.Checkout)
        {
            ranges.AddRange(record.Reference.Turns.Reverse().Where(turn => turn is { From: not null, To: not null } && turn.From != turn.To).Select(turn => (turn.From!, turn.To!)));
        }
        else
        {
            return ReviewCommitList.None;
        }

        var commits = new List<ReviewCommit>();
        foreach (var (from, to) in ranges)
        {
            if (commits.Count >= Most) break;
            var (code, raw, _) = await WorkingTree.GitAsync(root,
                ["--no-optional-locks", "-c", "core.quotePath=false", "log", "--no-color", "--no-renames", $"--max-count={Most}", "--format=%x1e%H%x1f%an%x1f%aI%x1f%s", "--name-only", $"{from}..{to}"], ct).ConfigureAwait(false);
            if (code == 0) commits.AddRange(Parse(raw));
        }

        return new([.. commits.Take(Most)], [.. record.Reference.Uncommitted.Select(path => path.Path)]);
    }

    /// <summary>Commits from <c>git log</c> in the format above: each opened by a record separator, its fields by unit separators, then its paths.</summary>
    public static IReadOnlyList<ReviewCommit> Parse(string raw)
    {
        var commits = new List<ReviewCommit>();
        foreach (var block in raw.Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
        {
            var lines = block.Split('\n');
            var fields = lines[0].Split('\u001f');
            if (fields.Length < 4) continue;
            var at = DateTimeOffset.TryParse(fields[2], out var when) ? when : (DateTimeOffset?)null;
            commits.Add(new ReviewCommit(fields[0], fields[1], at, fields[3].TrimEnd('\r'))
            {
                Files = [.. lines.Skip(1).Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)],
            });
        }

        return commits;
    }
}
