namespace Daoris.Driver;

/// <summary>
/// <i>Done and failed</i>'s one notice for a held-back pin that has outlasted its edge's lag bound: its kind is <see cref="Kind"/>, its
/// <see cref="Notice.Repository"/> the consumer, <see cref="Notice.Act"/> the producer and <see cref="Notice.Code"/> the commit held. One
/// notice by edge and the hold's first red (<c>family-lag:&lt;consumer&gt;.&lt;producer&gt;.&lt;first red&gt;</c>), so later reds of one hold add no
/// second, and a hold that ends and returns is told again.
/// </summary>
public static class FamilyNotices
{
    /// <summary>The notice's kind.</summary>
    public const string Kind = "family-lag";

    /// <summary>The news teller whose occurrences these are.</summary>
    public const string Teller = "family-lag";

    /// <summary>The occurrences <paramref name="told"/> make, each in its consumer's workspace.</summary>
    public static IReadOnlyList<NewsOccurrence> Occurrences(IReadOnlyList<FamilyHold> told, IReadOnlyList<RepoView> registry, DateTimeOffset now) =>
    [
        .. told.Select(hold =>
        {
            var id = $"{Kind}:{hold.Consumer}.{hold.Producer}.{hold.Since.UtcDateTime:yyyyMMddHHmm}";
            var workspace = registry.FirstOrDefault(row => string.Equals(row.Repository, hold.Consumer, StringComparison.OrdinalIgnoreCase))?.Workspace;
            return new NewsOccurrence(id, now, new Notice(id, Kind, now, Workspace: workspace, Repository: hold.Consumer, Act: hold.Producer,
                Code: hold.Commit.Length > 12 ? hold.Commit[..12] : hold.Commit));
        }),
    ];
}
