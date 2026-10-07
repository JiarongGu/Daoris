using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Where Daoris reads a done's evidence, and why it reads it there (EVID1b, D144 §3): the tree, how the commit was chosen
/// (<see cref="EvidenceCodes.How"/>), the commit (the tree's HEAD where none is named), the tree's base, and the session
/// whose end it reads.
/// </summary>
/// <param name="Tree">The session's own tree, or the root checkout with trees off: where git is asked, and what is uncommitted.</param>
public sealed record EvidenceAt(string Tree, string How)
{
    /// <summary>The commit to read, as the person named it; null reads the tree's HEAD.</summary>
    public string? Commit { get; init; }

    /// <summary>The commit the tree stood at when its session began (SURF6): whether this work changed each path; null reads none.</summary>
    public string? Base { get; init; }

    /// <summary>The session whose end is read; null for the terminal's check.</summary>
    public string? Session { get; init; }
}

/// <summary>What one read came to: a verdict, or why nothing was read. Unread is never found (D57, D143), and says why.</summary>
public sealed record EvidenceReading(EvidenceVerdict? Verdict, string? Unread);

/// <summary>How a folder of the tree is listed (EVID1b3): the disk's own listing, which a test stands a refusal in at.</summary>
internal delegate IEnumerable<FileSystemInfo> FolderList(DirectoryInfo folder);

/// <summary>Where a commit stands to the done's on its history (D144 §3): the same, after it, elsewhere, or not known here.</summary>
public enum EvidencePlace
{
    Same,
    After,
    Elsewhere,
    Unknown,
}

/// <summary>
/// The driver's read of a done's evidence (EVID1b, D144 §3): for each path a met requirement names, whether the commit holds
/// it and its object, whether this work changed it since the tree's base, whether it was left in the tree uncommitted, and a
/// path the commit spells only in another case, named and still missing. A gate is answered <c>no-queue</c> until the landing
/// queue reads gates (EVID1d).
/// </summary>
/// <remarks>
/// <para>🔴 <b>It reads and never writes</b>: <c>rev-parse</c> and <c>ls-tree</c> are all it asks git, and a directory listing is
/// all it asks the disk. <b>Each path goes to git as one argument</b>, <c>commit:path</c>, never through a shell, and is judged
/// again first (<see cref="EvidencePaths"/>), so the service's judgement is not trusted to have reached this side.</para>
///
/// <para><b>A path is matched exactly as git matches it</b>: a tree's entries by their bytes, so <c>Docs/x.md</c> is not
/// <c>docs/x.md</c> on any filesystem. Where the commit holds only another case, the read is <c>case</c>, naming the spelling;
/// what is uncommitted is looked for in the tree's own spelling too.</para>
///
/// <para>🔴 <b>git walks UP</b>: a tree that is gone, or is not the top of its own repository, is unread, since git would answer
/// for the repository above it.</para>
///
/// <para>🔴 <b>A failure is never absence</b> (EVID1b3): each lookup, the commit's object, the base's, the case walk's listings
/// and the tree's own folders, comes to found, absent or unread, and an unread one anywhere in an item's read makes the read
/// unread, so nothing is posted and the done waits as it would for a git that did not answer. Otherwise a read that
/// established nothing would post <c>missing</c> or <c>uncommitted</c> and hold a done for a fact nobody read.</para>
/// </remarks>
public static class EvidenceReader
{
    /// <summary>How many spellings in another case are followed per folder: a walk, not a search of the whole commit.</summary>
    private const int CaseCandidates = 8;

    /// <summary>
    /// What <paramref name="quest"/>'s done waits on (D144 §3; the service's <c>Quest.EvidenceWanted</c>): each item of each
    /// requirement its done answered met, once, in the requirements' order. A departure waits on none of its requirement's
    /// evidence, since the work departed from it. Empty for a quest that is not done.
    /// </summary>
    public static IReadOnlyList<WantedEvidence> Wanted(QuestView quest) =>
        quest.Status != "Done"
            ? []
            : [.. quest.Answers
                .Where(answer => answer.Departed is null && answer.Requirement >= 1 && answer.Requirement <= quest.Requirements.Count)
                .SelectMany(answer => quest.Requirements[answer.Requirement - 1].Evidence.Select(item => new WantedEvidence(answer.Requirement, item)))
                .Distinct()];

    /// <summary>Read <paramref name="wanted"/> where <paramref name="at"/> says, through the git Daoris runs.</summary>
    public static Task<EvidenceReading> ReadAsync(EvidenceAt at, IReadOnlyList<WantedEvidence> wanted, CancellationToken ct = default) =>
        ReadAsync(at, wanted, WorkingTree.ReadGitAsync, ct);

    /// <inheritdoc cref="ReadAsync(EvidenceAt, IReadOnlyList{WantedEvidence}, CancellationToken)"/>
    /// <param name="git">How git is read: the review's seam (REVIEW3), which a test stands git in at.</param>
    /// <param name="list">How a folder of the tree is listed: the disk's own listing where null, and a test's stand-in else.</param>
    internal static async Task<EvidenceReading> ReadAsync(
        EvidenceAt at, IReadOnlyList<WantedEvidence> wanted, WorkingTree.GitRead git, CancellationToken ct, FolderList? list = null)
    {
        list ??= folder => folder.EnumerateFileSystemInfos();

        if (wanted.Count == 0) return Unread("its done waits on no evidence: the requirements it answered met name none.");

        // Judged again before any of it reaches git, and before git is asked anything at all.
        foreach (var (requirement, item) in wanted)
        {
            if (item.Path is { } path && EvidencePaths.Judge(path) is { } why)
            {
                return Unread($"requirement {requirement} names `{path}`, which is not a path Daoris asks git for: {why}.");
            }
        }

        if (at.Commit is { } named && !WorkingTree.IsCommitId(named))
        {
            return Unread($"`{named}` is not a commit id: name one by its hex id, 7 to 64 characters.");
        }

        if (!await WorkingTree.IsTopLevelAsync(at.Tree, git, ct).ConfigureAwait(false))
        {
            return Unread("the tree it is read in is gone, or is not the top of a repository of its own, where git would answer for "
                + "the repository above it.");
        }

        if (await CommitAsync(at.Tree, at.Commit ?? "HEAD", git, ct).ConfigureAwait(false) is not { } commit)
        {
            return Unread(at.Commit is { } asked
                ? $"git holds no commit `{asked}` here."
                : "git could not read the tree's HEAD as a commit.");
        }

        var before = at.Base is { } baseCommit && WorkingTree.IsCommitId(baseCommit)
            ? await CommitAsync(at.Tree, baseCommit, git, ct).ConfigureAwait(false)
            : null;

        var listings = new Dictionary<string, IReadOnlyList<TreeEntry>?>(StringComparer.Ordinal);
        var items = new List<EvidenceRead>();
        foreach (var (requirement, item) in wanted)
        {
            if (item.Path is not { } path)
            {
                // A gate is read from the landing queue's verdict alone (D144 §4), which EVID1d builds.
                items.Add(new EvidenceRead(requirement, null, item.Gate, EvidenceCodes.NoQueue));
                continue;
            }

            // Each lookup below is found, absent or unread, and an unread one, wherever in the item's read, makes the read
            // unread (EVID1b3): a lookup that established nothing is never the item's absence.
            var (looked, found) = await ObjectAsync(at.Tree, commit, path, git, ct).ConfigureAwait(false);
            if (looked is Looked.Unread) return Unread($"git did not answer while `{path}` was read in `{Short(commit)}`.");

            bool? changed = null;
            if (before is not null)
            {
                var (wasLooked, was) = await ObjectAsync(at.Tree, before, path, git, ct).ConfigureAwait(false);
                if (wasLooked is Looked.Unread) return Unread($"git did not answer while `{path}` was read in `{Short(before)}`.");
                changed = !string.Equals(was, found, StringComparison.Ordinal);
            }

            if (looked is Looked.Found)
            {
                items.Add(new EvidenceRead(requirement, path, null, EvidenceCodes.Found) { Object = found, Changed = changed });
                continue;
            }

            var (spelledLooked, spelled) = await SpelledAsync(at.Tree, commit, path, listings, git, ct).ConfigureAwait(false);
            if (spelledLooked is Looked.Unread)
            {
                return Unread($"git did not list a folder of `{Short(commit)}` while `{path}` was looked for in another case.");
            }

            if (spelledLooked is Looked.Found)
            {
                items.Add(new EvidenceRead(requirement, path, null, EvidenceCodes.Case) { Spelled = spelled, Changed = changed });
                continue;
            }

            var standing = InTree(at.Tree, path, list);
            if (standing.Looked is Looked.Unread)
            {
                var folder = standing.Unlisted is { Length: > 0 } unlisted ? $"folder `{unlisted}`" : "own folder";
                return Unread($"the tree's {folder} could not be listed while `{path}` was looked for uncommitted: {standing.Why}.");
            }

            items.Add(new EvidenceRead(
                requirement, path, null, standing.Looked is Looked.Found ? EvidenceCodes.Uncommitted : EvidenceCodes.Missing)
            {
                Changed = changed,
            });
        }

        return new EvidenceReading(new EvidenceVerdict(commit, at.How, items) { Session = at.Session }, null);

        static EvidenceReading Unread(string why) => new(null, why);
    }

    /// <summary>
    /// Where <paramref name="named"/> stands to <paramref name="done"/>, the done's commit, in <paramref name="tree"/> (D144 §3):
    /// a check reads the done's commit or one after it on the same history. Unknown where either is not a commit id, git holds
    /// either not, or the tree is not the top of its own repository.
    /// </summary>
    public static Task<EvidencePlace> PlaceAsync(string tree, string done, string named, CancellationToken ct = default) =>
        PlaceAsync(tree, done, named, WorkingTree.ReadGitAsync, ct);

    /// <inheritdoc cref="PlaceAsync(string, string, string, CancellationToken)"/>
    internal static async Task<EvidencePlace> PlaceAsync(string tree, string done, string named, WorkingTree.GitRead git, CancellationToken ct)
    {
        if (!WorkingTree.IsCommitId(done) || !WorkingTree.IsCommitId(named)) return EvidencePlace.Unknown;
        if (!await WorkingTree.IsTopLevelAsync(tree, git, ct).ConfigureAwait(false)) return EvidencePlace.Unknown;

        var from = await CommitAsync(tree, done, git, ct).ConfigureAwait(false);
        var to = await CommitAsync(tree, named, git, ct).ConfigureAwait(false);
        if (from is null || to is null) return EvidencePlace.Unknown;
        if (from == to) return EvidencePlace.Same;

        // `--is-ancestor` answers 0 for yes and 1 for no; anything else is git failing to answer.
        var (code, _) = await RunAsync(tree, ["merge-base", "--is-ancestor", from, to], git, ct).ConfigureAwait(false);
        return code switch
        {
            0 => EvidencePlace.After,
            1 => EvidencePlace.Elsewhere,
            _ => EvidencePlace.Unknown,
        };
    }

    /// <summary>A commit's full id, or null where git holds none by that name.</summary>
    private static async Task<string?> CommitAsync(string tree, string name, WorkingTree.GitRead git, CancellationToken ct)
    {
        var (code, output) = await RunAsync(tree, ["rev-parse", "--verify", "--quiet", $"{name}^{{commit}}"], git, ct).ConfigureAwait(false);
        var id = output.Trim().ToLowerInvariant();
        return code == 0 && EvidenceCodes.IsObjectId(id) ? id : null;
    }

    /// <summary>
    /// The object <paramref name="commit"/> holds at <paramref name="path"/>, matched exactly: a file's blob or a folder's tree.
    /// Found with its id on git's 0, absent on its 1, and unread on anything else, an answer that is no id included.
    /// </summary>
    private static async Task<(Looked Looked, string? Object)> ObjectAsync(
        string tree, string commit, string path, WorkingTree.GitRead git, CancellationToken ct)
    {
        // One argument, `commit:path`: a tree entry is looked up by its bytes, whatever the filesystem folds.
        var (code, output) = await RunAsync(tree, ["rev-parse", "--verify", "--quiet", $"{commit}:{path}"], git, ct).ConfigureAwait(false);
        var id = output.Trim().ToLowerInvariant();
        return code switch
        {
            0 when EvidenceCodes.IsObjectId(id) => (Looked.Found, id),
            1 => (Looked.Absent, null),
            _ => (Looked.Unread, null),
        };
    }

    /// <summary>
    /// How <paramref name="commit"/> spells <paramref name="path"/> where it holds it only in another case: each folder's entries
    /// listed in turn, a few spellings followed at each. The first in ordinal order is named, judged as a path first. Absent
    /// where every listing the walk needed was given and none spells it; unread where git did not give one (EVID1b3), since
    /// that folder may hold the first spelling, so what the walk would name is not known.
    /// </summary>
    private static async Task<(Looked Looked, string? Spelled)> SpelledAsync(
        string tree, string commit, string path, Dictionary<string, IReadOnlyList<TreeEntry>?> listings, WorkingTree.GitRead git,
        CancellationToken ct)
    {
        var segments = path.Split('/');
        var walking = new List<(string Treeish, string Spelled)> { (commit, "") };
        for (var depth = 0; depth < segments.Length && walking.Count > 0; depth++)
        {
            var last = depth == segments.Length - 1;
            var next = new List<(string, string)>();
            foreach (var (treeish, spelled) in walking)
            {
                if (!listings.TryGetValue(treeish, out var entries))
                {
                    var (code, output) = await RunAsync(tree, ["ls-tree", "-z", treeish], git, ct).ConfigureAwait(false);
                    entries = code == 0 ? TreeEntry.Parse(output) : null;
                    listings[treeish] = entries;
                }

                // Kept as null, so a later item that needs the same folder is unread for the same reason.
                if (entries is null) return (Looked.Unread, null);

                foreach (var entry in entries)
                {
                    if (!string.Equals(entry.Name, segments[depth], StringComparison.OrdinalIgnoreCase)) continue;
                    if (!last && entry.Type != "tree") continue;
                    next.Add((entry.Object, spelled.Length == 0 ? entry.Name : $"{spelled}/{entry.Name}"));
                }
            }

            walking = [.. next.OrderBy(each => each.Item2, StringComparer.Ordinal).Take(CaseCandidates)];
        }

        var first = walking
            .Select(each => each.Spelled)
            .FirstOrDefault(spelled => !string.Equals(spelled, path, StringComparison.Ordinal) && EvidencePaths.Judge(spelled) is null);
        return first is null ? (Looked.Absent, null) : (Looked.Found, first);
    }

    /// <summary>
    /// Whether <paramref name="path"/> stands in the tree as named, its case included: each folder listed in turn rather than
    /// asked by name, since a filesystem that folds case would say yes to another spelling. Found or absent once each listing
    /// it needed was given (a file where a folder is named holds nothing beneath it); unread where the disk would not list one
    /// (EVID1b3), naming that folder from the tree's root and why.
    /// </summary>
    private static Standing InTree(string tree, string path, FolderList list)
    {
        var segments = path.Split('/');
        var at = new DirectoryInfo(tree);
        for (var depth = 0; depth < segments.Length; depth++)
        {
            FileSystemInfo? entry;
            try
            {
                entry = list(at).FirstOrDefault(each => string.Equals(each.Name, segments[depth], StringComparison.Ordinal));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // The folder's name as the path spells it, which is how it was found: never its place on this machine.
                return new(Looked.Unread, string.Join('/', segments.Take(depth)),
                    error is UnauthorizedAccessException ? "access was refused" : "the disk did not answer");
            }

            if (entry is null) return new(Looked.Absent);
            if (depth == segments.Length - 1) return new(Looked.Found);
            if (entry is not DirectoryInfo folder) return new(Looked.Absent);
            at = folder;
        }

        return new(Looked.Absent);
    }

    /// <summary>
    /// What one lookup came to (EVID1b3): found, absent, or unread, where git or the disk did not answer and so established
    /// nothing. Unread is never absent.
    /// </summary>
    private enum Looked
    {
        Found,
        Absent,
        Unread,
    }

    /// <summary>Whether a path stands in the tree; where unread, the folder that would not list, from the tree's root, and why.</summary>
    private readonly record struct Standing(Looked Looked, string? Unlisted = null, string? Why = null);

    /// <summary>git's whole answer, and its code.</summary>
    private static async Task<(int Code, string Output)> RunAsync(
        string tree, IReadOnlyList<string> arguments, WorkingTree.GitRead git, CancellationToken ct)
    {
        var output = new StringBuilder();
        var code = await git(tree, arguments, piece =>
        {
            output.Append(piece.Span);
            return true;
        }, ct).ConfigureAwait(false);
        return (code, output.ToString());
    }

    private static string Short(string commit) => commit.Length > 8 ? commit[..8] : commit;

    /// <summary>One entry of <c>ls-tree -z</c>: <c>mode SP type SP object TAB name</c>, NUL-terminated and never quoted.</summary>
    private sealed record TreeEntry(string Type, string Object, string Name)
    {
        public static IReadOnlyList<TreeEntry> Parse(string listed)
        {
            var entries = new List<TreeEntry>();
            foreach (var line in listed.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                var tab = line.IndexOf('\t');
                if (tab < 0) continue;
                var fields = line[..tab].Split(' ');
                if (fields.Length != 3) continue;
                entries.Add(new TreeEntry(fields[1], fields[2].ToLowerInvariant(), line[(tab + 1)..]));
            }

            return entries;
        }
    }
}
