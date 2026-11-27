namespace Daoris.Driver;

/// <summary>
/// Each promoted batch of an ask's branch kept on <c>refs/daoris/train/&lt;ask&gt;/&lt;n&gt;</c>, numbered in the order they landed, so a batch's
/// tip is found after the branch has moved on.
/// </summary>
public static class TrainRefs
{
    public const string Prefix = "refs/daoris/train/";

    /// <summary>Keep <paramref name="tip"/> as the next batch of <paramref name="line"/>'s ask; nothing for a line that is no ask's branch.</summary>
    public static async Task<bool> KeepAsync(string root, string line, string tip, CancellationToken ct)
    {
        if (TrainBranch.AskOf(line) is not { } ask) return false;
        var (listed, names, _) = await WorkingTree.GitAsync(root, ["for-each-ref", "--format=%(refname)", $"{Prefix}{ask}/"], ct).ConfigureAwait(false);
        if (listed != 0) return false;
        var next = names.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length + 1;
        var (kept, _, _) = await WorkingTree.GitAsync(root, ["update-ref", $"{Prefix}{ask}/{next}", tip], ct).ConfigureAwait(false);
        return kept == 0;
    }

    /// <summary>The ref an ask's landing on its repository's line is kept on, beside the batches and not among them; made once and never moved, as a batch's is.</summary>
    public static string LandedRef(string ask) => $"refs/daoris/landed/{ask.TrimStart('#')}";

    public static async Task<bool> KeepLandedAsync(string root, string ask, string commit, CancellationToken ct) =>
        (await WorkingTree.GitAsync(root, ["update-ref", LandedRef(ask), commit, new string('0', commit.Length)], ct).ConfigureAwait(false)).Code == 0;

    public static Task<string?> LandedAsync(string root, string ask, CancellationToken ct) => MachinePorts.Repository.CommitOfAsync(root, LandedRef(ask), ct);
}
