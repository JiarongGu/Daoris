using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A brainstorm's packet assembled from the knowledge search or the declared index's files, a meeting's first packet too.</summary>
internal sealed class BrainstormAssembly(LoopContext context)
{
    private readonly LoopContext _context = context;

    /// <summary>
    /// The packet's assembly: the question, and for a repository its sources from the knowledge search in more than
    /// one wording, else from its declared index's files. A meeting's first packet is this assembly.
    /// </summary>
    internal async Task<BrainstormPacket> AssembleAsync(
        string question, string? asked, Snapshot snapshot, CancellationToken ct, IReadOnlyList<string>? named = null)
    {
        var found = new List<BrainstormFound>();
        var left = new List<BrainstormLeftOut>();
        string? tier = null;
        string? revision = null;
        if (asked is { } repository)
        {
            var (root, commit) = await CheckoutAsync(repository, snapshot, ct).ConfigureAwait(false);
            revision = commit;
            if (root is not null) found.AddRange(NamedSources(root, named ?? [], left));
            var searched = new List<BrainstormFound>();
            try
            {
                foreach (var words in BrainstormWordings.Of(question))
                {
                    var search = await SearchAsync(words, repository, ct).ConfigureAwait(false);
                    searched.AddRange(search.Found);
                    tier = tier is null or BrainstormPackets.Meaning ? search.Tier : tier;
                }
            }
            catch (Exception error) when (error is HttpRequestException or JsonException or DriverException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                // No service: the declared index's files answer.
                searched.Clear();
                tier = BrainstormPackets.Files;
                if (root is not null) searched.AddRange(IndexFiles(root, question));
            }

            found.AddRange(BrainstormPackets.Prefer(searched));
        }

        var packet = BrainstormPackets.Freeze(question, asked, tier, revision, found, BrainstormBounds.First);
        return left.Count == 0 ? packet : packet with { LeftOut = [.. left, .. packet.LeftOut] };
    }

    /// <summary>
    /// The paths the asker named, each read whole from the checkout and taken before any search's find; one that
    /// is not a file there is left out as removed.
    /// </summary>
    private static IEnumerable<BrainstormFound> NamedSources(string root, IReadOnlyList<string> named, List<BrainstormLeftOut> left)
    {
        foreach (var path in named)
        {
            var full = Path.GetFullPath(Path.Combine(root, path));
            string text;
            try
            {
                if (!full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) throw new FileNotFoundException();
                text = File.ReadAllText(full);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                left.Add(new BrainstormLeftOut(path, "removed"));
                continue;
            }

            var kind = path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? "document" : "code";
            yield return new BrainstormFound(kind, path, null, null, text, path, BrainstormPackets.NamedTier, "a source the asker named with --source");
        }
    }

    private async Task<(string? Root, string? Revision)> CheckoutAsync(string repository, Snapshot snapshot, CancellationToken ct)
    {
        if (_context.BrainstormCheckouts is { } read) return await read(repository, ct).ConfigureAwait(false);
        var root = snapshot.Repositories.FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Root;
        return root is null ? (null, null) : (root, await ReviewGit.HeadAsync(root, ct).ConfigureAwait(false));
    }

    private Task<BrainstormSearch> SearchAsync(string words, string repository, CancellationToken ct) =>
        _context.BrainstormSearches is { } search ? search(words, repository, ct) : _context.Service.BrainstormSearchAsync(words, repository, ct);

    /// <summary>The declared index's files, whole, each a source: what answers where no service does.</summary>
    private static IEnumerable<BrainstormFound> IndexFiles(string root, string question)
    {
        foreach (var index in RepositoryIndexes.Find(root))
        {
            var path = Path.Combine(root, index);
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            yield return new BrainstormFound("index", index, null, null, text, question);
        }
    }
}
