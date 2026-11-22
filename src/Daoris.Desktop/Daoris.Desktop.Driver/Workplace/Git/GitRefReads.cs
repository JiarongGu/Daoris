using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What a ref names, read from git's own files with no git started and no library opened: <c>rev-parse HEAD</c> and
/// <c>rev-parse --verify --quiet NAME[^{commit}]</c> of <c>HEAD</c> or a full ref name, answered from the loose file and <c>packed-refs</c> (a
/// linked tree's own <c>HEAD</c> from its own directory), and the refs' one listing kept while no ref or branch setting moved.
/// </summary>
/// <remarks>
/// <para><b>Only where the files say it whole.</b> A ref found names its id; a name that no rule's loose file or packed line has is absent
/// (exit 1, nothing printed, as <c>--quiet</c> says it). A directory where a ref is looked for, a file that is empty or not an id, a dangling
/// symbolic ref, an unborn head, a reftable, a replacement that can stand and a name with an operator are git's.</para>
/// <para><b>A commit's peel is kept, never guessed.</b> <c>NAME^{commit}</c> is answered from the files only once a read said which commit
/// that id peels to (<see cref="GitObjectMemo.PeeledCommit"/>): a tag or a non-commit is learned the same way, and asked of the library
/// the first time.</para>
/// <para><b>The listing</b> is the library's (<see cref="GitLibraryRefs"/>) kept in memory under a stamp of every loose ref under
/// <c>refs/heads</c> and <c>refs/remotes/origin</c>, <c>packed-refs</c> and the repository's configuration, taken before and after the read:
/// a listing is kept only if nothing moved across it, and a configuration that includes another file keeps nothing.</para>
/// </remarks>
internal static class GitRefReads
{
    private const int MostListings = 64;

    private static readonly ConcurrentDictionary<string, (string Stamp, GitLibraryAnswer Answer)> Listings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>One ref question: the files' answer where they gave one, else what to learn from the library's.</summary>
    internal sealed record Read(GitLibraryAnswer? Answer, string? Common, string? Id)
    {
        /// <summary>Learns from the library's answer to <c>NAME^{commit}</c> which commit <see cref="Id"/> peels to.</summary>
        internal void Learn(GitLibraryAnswer library)
        {
            if (Common is not null && Id is not null && library.Code == 0 && library.Stdout.Trim() is { Length: >= 40 } commit) GitObjectMemo.LearnPeeled(Common, Id, commit);
        }
    }

    /// <summary>The question of <paramref name="arguments"/> in <paramref name="folder"/>, where it is a ref's the files may answer; null where it is git's.</summary>
    internal static Read? Asked(string folder, IReadOnlyList<string> arguments)
    {
        var words = arguments.Where(word => word != "--no-optional-locks").ToList();
        string name;
        var peel = false;
        if (words is ["rev-parse", "HEAD"] or ["rev-parse", "--verify", "HEAD"]) name = "HEAD";
        else if (words is ["rev-parse", "--verify", "--quiet" or "-q", var asked])
        {
            peel = asked.EndsWith("^{commit}", StringComparison.Ordinal);
            name = peel ? asked[..^"^{commit}".Length] : asked;
            if (!GitLibraryVerify.Plain(name)) return null;
        }
        else return null;

        if (GitLibraryAdmission.DirsOf(folder) is not { } dirs || GitReplacements.CanBeInForce(dirs.Common)) return null;
        var (gitDir, common) = dirs;
        string[] rules = name == "HEAD" ? ["HEAD"] : GitLibraryVerify.Rules.Select(rule => string.Format(rule, name)).ToArray();
        foreach (var rule in rules)
        {
            var (state, id) = Resolve(gitDir, common, rule);
            if (state == Reading.Unknown) return null;
            if (state == Reading.Absent) continue;
            if (!peel) return new Read(new GitLibraryAnswer(0, id + "\n"), null, null);
            return GitObjectMemo.PeeledCommit(common, id!) is { } commit ? new Read(new GitLibraryAnswer(0, commit + "\n"), null, null) : new Read(null, common, id);
        }

        // An unborn HEAD is git's to say; a quiet name no rule has is absent.
        return name == "HEAD" ? null : new Read(new GitLibraryAnswer(1, ""), null, null);
    }

    private enum Reading
    {
        Unknown,
        Found,
        Absent,
    }

    private static (Reading State, string? Id) Resolve(string gitDir, string common, string name)
    {
        try
        {
            var followed = 0;
            while (followed < 6)
            {
                var file = Path.Combine(PerTree(name) ? gitDir : common, name);
                if (Directory.Exists(file)) return (Reading.Unknown, null);
                if (File.Exists(file))
                {
                    var text = File.ReadAllText(file).Trim();
                    if (text.StartsWith("ref:", StringComparison.Ordinal))
                    {
                        name = text["ref:".Length..].Trim();
                        followed++;
                        continue;
                    }

                    return IsId(text) ? (Reading.Found, text) : (Reading.Unknown, null);
                }

                var (packed, refs) = GitPackedRefs.Of(common);
                if (packed.Unread) return (Reading.Unknown, null);
                if (refs.Tips.TryGetValue(name, out var tip)) return (Reading.Found, tip);
                // A name a symbolic ref led to and nothing has is a dangling ref, which git says in its own words.
                return followed == 0 && name != "HEAD" ? (Reading.Absent, null) : (Reading.Unknown, null);
            }

            return (Reading.Unknown, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return (Reading.Unknown, null);
        }
    }

    /// <summary>What a linked tree keeps in its own directory: its head, the pseudo refs and the few namespaces git keeps per tree.</summary>
    private static bool PerTree(string name) =>
        name == "HEAD" || !name.Contains('/')
        || name.StartsWith("refs/bisect/", StringComparison.Ordinal) || name.StartsWith("refs/worktree/", StringComparison.Ordinal) || name.StartsWith("refs/rewritten/", StringComparison.Ordinal);

    private static bool IsId(string text) => text.Length is 40 or 64 && text.All(Uri.IsHexDigit) && text == text.ToLowerInvariant();

    /// <summary>Whether <paramref name="arguments"/> are the refs' one listing.</summary>
    internal static bool Listed(IReadOnlyList<string> arguments) =>
        arguments.Where(word => word != "--no-optional-locks").ToList() is ["for-each-ref", .. var rest] && rest.SequenceEqual(GitLibraryRefs.Listed);

    /// <summary>The listing kept for <paramref name="folder"/> while its stamp stands; null where none is or the stamp cannot be taken.</summary>
    internal static GitLibraryAnswer? Kept(string folder)
    {
        if (GitLibraryAdmission.RefusalOf(folder, GitLibraryQuestion.Refs) is not null || StampOf(folder) is not { } stamp) return null;
        return Listings.TryGetValue(stamp.Common, out var kept) && kept.Stamp == stamp.Value ? kept.Answer : null;
    }

    /// <summary>Keeps <paramref name="answer"/> as the listing of <paramref name="folder"/> if the stamp taken before the read still stands.</summary>
    internal static void Keep(string folder, string? before, GitLibraryAnswer answer)
    {
        if (before is null || answer.Code != 0 || StampOf(folder) is not { } after || after.Value != before) return;
        if (Listings.Count >= MostListings) Listings.Clear();
        Listings[after.Common] = (after.Value, answer);
    }

    /// <summary>The stamp of what the listing rests on, taken now; null where it cannot be.</summary>
    internal static string? Stamped(string folder) => StampOf(folder)?.Value;

    private static (string Common, string Value)? StampOf(string folder)
    {
        try
        {
            if (GitLibraryAdmission.DirsOf(folder) is not { } dirs || GitReplacements.CanBeInForce(dirs.Common)) return null;
            var (gitDir, common) = dirs;
            var text = new StringBuilder();
            foreach (var settings in (string[])[Path.Combine(common, "config"), Path.Combine(gitDir, "config.worktree")])
            {
                if (!File.Exists(settings)) continue;
                var read = File.ReadAllText(settings);
                if (read.Contains("[include", StringComparison.OrdinalIgnoreCase)) return null;
                text.Append(read).Append('\0');
            }

            var packed = Path.Combine(common, "packed-refs");
            text.Append(File.Exists(packed) ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(packed))) : "").Append('\0');
            foreach (var kind in (string[])[Path.Combine("refs", "heads"), Path.Combine("refs", "remotes", "origin")])
            {
                var folderOf = Path.Combine(common, kind);
                if (!Directory.Exists(folderOf)) continue;
                foreach (var file in Directory.EnumerateFiles(folderOf, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                {
                    if (file.EndsWith(".lock", StringComparison.Ordinal)) continue;
                    text.Append(file).Append('=').Append(File.ReadAllText(file)).Append('\0');
                }
            }

            return (Path.GetFullPath(common), Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
