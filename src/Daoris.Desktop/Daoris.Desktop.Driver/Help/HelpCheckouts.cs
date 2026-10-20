using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's reading of the checkouts, the driver's half (HELPMODE1c4, D183's note; the Ask Daoris modes design §4.1): each request
/// its connector's <c>checkout_read</c> wrote is judged here at the call, then answered with what was read: a file's text, a folder's
/// entries, <c>git status</c> or <c>git branch --list</c>. One mechanism for every agent: it replaces the <c>Read</c> and git rules
/// restricted mode handed Claude, which held no agent that takes no rules (Codex). What the loop running the conversation answers its
/// <see cref="SessionRequestWatch.Checkout"/> with.
/// </summary>
/// <remarks>
/// <para><b>Judged at every call, whatever the agent</b>, in housekeeping's order (<see cref="HelpHousekeeping"/>): the record named is an
/// Ask Daoris conversation's; the repository is registered in the conversation's workspace, read from its record
/// (<see cref="HelpCircle.OfRecord"/>), with a checkout here; D107 lets agents outside it read that checkout now; the person has not
/// turned it off in <see cref="ConfiguredAccess"/> now, so a narrowing refuses the next call; and it was handed at this start (its newest
/// receipt, <see cref="HelpWired"/>), so one turned on since takes the conversation's next turn (ACCESSSWAP1).</para>
///
/// <para><b>Never a path beyond the checkout</b> (the design's trap of a typed tool that takes a path): the tool takes a repository and a
/// path relative to its checkout, and the path is walked here a name at a time, each name as the disk spells it: <c>..</c>, a rooted
/// path, a drive or a stream, a pattern and git's own folder are refused before anything is opened, and a link on the way whose final
/// target lies outside the checkout is refused. Only the names found on disk are opened, never the agent's spelling.</para>
///
/// <para><b>Never what git ignores</b> (<c>local/</c>, build output, secrets kept ignored), by git's own answer through the driver's git
/// (<c>check-ignore</c>, each folder on the way asked too); a read git cannot judge is refused, never let through as though nothing
/// were ignored. Never <c>git log</c>, <c>diff</c> or <c>show</c>: their output options write files (D107 §2). Nothing is written: the
/// status takes no optional lock.</para>
/// </remarks>
/// <param name="service">The loop's service once it answers; null before, when every request is refused saying so.</param>
public sealed class HelpCheckouts(string home, Func<ServiceClient?> service)
{
    /// <summary>The connector's tool, as it is named on the wire.</summary>
    public const string Tool = "checkout_read";

    /// <summary>A file's text, or a folder's entries: the path says which.</summary>
    public const string Read = "read";

    /// <summary><c>git status</c> of the whole checkout.</summary>
    public const string Status = "status";

    /// <summary><c>git branch --list</c>.</summary>
    public const string Branches = "branches";

    /// <summary>The reads the tool asks, and never another.</summary>
    public static readonly IReadOnlyList<string> Operations = [Read, Status, Branches];

    /// <summary>The most of a file's text, or of git's answer, one call carries: 200 KB, the rest cut and said so.</summary>
    public const int Bound = 200 * 1024;

    /// <summary>The most entries of one folder one call lists, the rest said so.</summary>
    public const int Entries = 1_000;

    /// <summary>How long git is given for a status or the branch list: under the tool's own wait for the answer.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    /// <summary>Judge one request and read where it holds; a refusal is an answer, never thrown.</summary>
    public async Task<CheckoutAnswer> AnswerAsync(SessionRequest request, CancellationToken ct)
    {
        var operation = request.Operation ?? "";
        if (!Operations.Contains(operation))
        {
            return CheckoutAnswer.Refusal(operation, CheckoutAnswer.Named, $"`{operation}` is no read of a checkout: it is `read`, `status` or `branches`.");
        }

        if (service() is not { } ledger)
        {
            return CheckoutAnswer.Refusal(operation, CheckoutAnswer.NoService, "Daoris's service is not answering yet, so the conversation's record could not be read.");
        }

        var record = (await ledger.SessionRecordsAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(each => string.Equals(each.Id, request.Session, StringComparison.Ordinal));
        if (record is not { Repository: HelpRoom.Repository })
        {
            return CheckoutAnswer.Refusal(operation, CheckoutAnswer.NotHelp,
                $"Reading the checkouts through this tool is Ask Daoris's alone, and session `{request.Session}` is no Ask Daoris conversation of this machine's.");
        }

        if (string.IsNullOrWhiteSpace(request.Repository))
        {
            return CheckoutAnswer.Refusal(operation, CheckoutAnswer.Named, "It names no repository: a checkout is read by its repository's registered name.");
        }

        var registry = await ledger.RegistryAsync(ct).ConfigureAwait(false);
        var (row, refused) = Place(operation, request.Repository.Trim(), registry, HelpCircle.OfRecord(home, record));
        if (refused is not null) return refused;
        if (Granted(operation, row!, request.Session) is { } ungranted) return ungranted;

        var root = row!.Root!;
        if (operation != Read && !string.IsNullOrWhiteSpace(request.Path))
        {
            return CheckoutAnswer.Refusal(operation, CheckoutAnswer.Named,
                $"`{operation}` answers for the whole checkout and names no path; `read` takes one.");
        }

        if (!OwnGit(root))
        {
            return CheckoutAnswer.Refusal(operation, CheckoutAnswer.Failed,
                $"`{row.Repository}`'s checkout has no git of its own, so git cannot say what it ignores or how it stands.");
        }

        return operation switch
        {
            Status => await GitAsync(operation, row.Repository, root, ["--no-optional-locks", "-c", "color.ui=false", "-c", "core.quotePath=false", "status"], "git status", ct)
                .ConfigureAwait(false),
            Branches => await GitAsync(operation, row.Repository, root, ["-c", "color.ui=false", "branch", "--list"], "git branch --list", ct)
                .ConfigureAwait(false),
            _ => await ReadAsync(row.Repository, root, request.Path, ct).ConfigureAwait(false),
        };
    }

    /// <summary>The registry's row a request names, or the refusal: none registered, another workspace's, no checkout here.</summary>
    private static (RepoView? Row, CheckoutAnswer? Refusal) Place(string operation, string repository, IReadOnlyList<RepoView> registry, string? circle)
    {
        var named = registry.Where(row => string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase)).ToList();
        if (named.Count == 0)
        {
            return (null, CheckoutAnswer.Refusal(operation, CheckoutAnswer.Unknown, $"`{repository}` is no repository registered here."));
        }

        if (named.FirstOrDefault(row => Within(row.Workspace, circle)) is not { } row)
        {
            return (null, CheckoutAnswer.Refusal(operation, CheckoutAnswer.Outside,
                $"This conversation is workspace `{circle}`'s, and `{named[0].Repository}` is a repository of another workspace: its checkout is "
                + "read from a conversation started from a window on that workspace."));
        }

        return string.IsNullOrWhiteSpace(row.Root) || !Directory.Exists(row.Root)
            ? (null, CheckoutAnswer.Refusal(operation, CheckoutAnswer.NoCheckout, $"`{row.Repository}` has no checkout on this machine, so there is nothing of it to read here."))
            : (row, null);
    }

    /// <summary>
    /// Why the grant does not reach this checkout now, or null where it does: D107's reading by agents outside it, the person's
    /// narrowing now, and what this start was handed.
    /// </summary>
    private CheckoutAnswer? Granted(string operation, RepoView row, string session)
    {
        if (!AcrossRules.Reading(DriverConfig.Load(DriverConfig.ResolvePath(home)), row.Repository, row.Workspace).Read)
        {
            return CheckoutAnswer.Refusal(operation, CheckoutAnswer.NotReadable,
                $"`{row.Repository}` lets no agent outside it read its checkout. The person turns that on under Repositories → the repository's "
                + $"page → Setup → Reach, or with `daoris driver across {row.Repository} read on`.");
        }

        if (!ConfiguredAccess.Read(home).Reads(row.Repository))
        {
            return CheckoutAnswer.Refusal(operation, CheckoutAnswer.NotGranted,
                $"The person turned `{row.Repository}`'s checkout off under Ask Daoris's configured access, so it is not yours to read: say what "
                + "you would read, and that they turn it on there.");
        }

        var handed = new HelpWired(home).Read(session).FirstOrDefault()?.Checkouts ?? [];
        return handed.Any(read => string.Equals(read.Name, row.Repository, StringComparison.OrdinalIgnoreCase))
            ? null
            : CheckoutAnswer.Refusal(operation, CheckoutAnswer.NotHanded,
                $"`{row.Repository}`'s checkout was not handed at this conversation's start: one the person turned on, or that became readable, "
                + "after its turn began is yours at this conversation's next turn.");
    }

    /// <summary>A file's text, or a folder's entries, at a path walked inside the checkout and out of what git ignores.</summary>
    private static async Task<CheckoutAnswer> ReadAsync(string repository, string root, string? path, CancellationToken ct)
    {
        var (segments, shape) = CheckoutPath.Segments(path);
        if (shape is not null) return CheckoutAnswer.Refusal(Read, CheckoutAnswer.Escapes, $"`{path}` {shape}.");

        var walked = CheckoutPath.Walk(root, segments!);
        if (walked.Refusal is { } escapes) return CheckoutAnswer.Refusal(Read, CheckoutAnswer.Escapes, $"`{path}` {escapes}.");
        if (walked.Found is not { } found) return CheckoutAnswer.Refusal(Read, CheckoutAnswer.Missing, $"`{repository}`'s checkout has no `{path?.Trim()}`.");

        var relative = string.Join('/', walked.Names);
        var folder = found is DirectoryInfo;
        // Each folder on the way, then the path itself, so what lies in an ignored folder is ignored whatever git says of the file alone.
        var asked = walked.Names.Select((_, at) => string.Join('/', walked.Names.Take(at + 1)) + (at + 1 < walked.Names.Count || folder ? "/" : "")).ToList();
        if (asked.Count > 0)
        {
            if (await IgnoredAsync(root, asked, ct).ConfigureAwait(false) is not { } ignored)
            {
                return CheckoutAnswer.Refusal(Read, CheckoutAnswer.Failed,
                    $"Git could not say whether it ignores `{relative}` in `{repository}`'s checkout, so it is not read.");
            }

            if (asked.Any(each => ignored.Contains(each.TrimEnd('/'))))
            {
                return CheckoutAnswer.Refusal(Read, CheckoutAnswer.Ignored,
                    $"`{relative}` in `{repository}`'s checkout is what git ignores, or lies in a folder git ignores, and what a repository "
                    + "ignores is never read here.");
            }
        }

        return folder
            ? await ListAsync(repository, root, (DirectoryInfo)found, relative, ct).ConfigureAwait(false)
            : ReadFile(repository, (FileInfo)found, relative);
    }

    /// <summary>A folder's entries, each folder with a slash, folders first, git's own folder and what git ignores left out.</summary>
    private static async Task<CheckoutAnswer> ListAsync(string repository, string root, DirectoryInfo folder, string relative, CancellationToken ct)
    {
        var prefix = relative.Length == 0 ? "" : relative + "/";
        var entries = folder.EnumerateFileSystemInfos("*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 })
            .Where(entry => !string.Equals(entry.Name, ".git", StringComparison.OrdinalIgnoreCase))
            .Select(entry => (entry.Name, Folder: entry is DirectoryInfo))
            .ToList();
        var named = entries.Select(entry => prefix + entry.Name + (entry.Folder ? "/" : "")).ToList();
        HashSet<string> ignored = [];
        if (named.Count > 0)
        {
            if (await IgnoredAsync(root, named, ct).ConfigureAwait(false) is not { } answered)
            {
                return CheckoutAnswer.Refusal(Read, CheckoutAnswer.Failed, $"Git could not say what it ignores in `{repository}`'s checkout, so the folder is not listed.");
            }

            ignored = answered;
        }

        var shown = entries.Where(entry => !ignored.Contains(prefix + entry.Name))
            .OrderByDescending(entry => entry.Folder).ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Name + (entry.Folder ? "/" : "")).ToList();
        var where = relative.Length == 0 ? $"`{repository}`'s checkout, its top" : $"`{repository}`'s `{relative}/`";
        var count = shown.Count > Entries ? $"the first {Entries} of {shown.Count} entries" : shown.Count == 1 ? "1 entry" : $"{shown.Count} entries";
        var said = shown.Count == 0
            ? $"{where} is empty, git's own folder and what git ignores left out."
            : $"{where}, {count}, folders first, git's own folder and what git ignores left out:";
        return new CheckoutAnswer(Read, true, null, said) { Text = string.Concat(shown.Take(Entries).Select(name => name + "\n")) };
    }

    /// <summary>A file's text, decoded as UTF-8 and cut at <see cref="Bound"/> between characters; one that is no text is said so.</summary>
    private static CheckoutAnswer ReadFile(string repository, FileInfo file, string relative)
    {
        using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var length = stream.Length;
        var bytes = new byte[(int)Math.Min(length, Bound)];
        var got = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
        var cut = length > got;
        var where = $"`{repository}`'s `{relative}`";
        if (bytes.AsSpan(0, got).Contains((byte)0)) return new CheckoutAnswer(Read, true, null, $"{where} is not text ({Size(length)}), so it is not read.");

        var start = got >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        var decoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false).GetDecoder();
        var chars = new char[got];
        // Not flushed where it is cut, so a character whose bytes run past the bound is left out whole, never half of it.
        var count = decoder.GetChars(bytes, start, got - start, chars, 0, flush: !cut);
        var said = cut
            ? $"{where}, the first 200 KB of {Size(length)}: the rest is cut, so say so where it matters."
            : $"{where}, {Size(length)}, whole:";
        return new CheckoutAnswer(Read, true, null, said) { Text = new string(chars, 0, count) };
    }

    /// <summary>A git that only reads, its answer cut at <see cref="Bound"/> and said so.</summary>
    private static async Task<CheckoutAnswer> GitAsync(
        string operation, string repository, string root, IReadOnlyList<string> arguments, string command, CancellationToken ct)
    {
        var (code, stdout, stderr) = await WorkingTree.GitAsync(root, arguments, environment: null, Patience, ct).ConfigureAwait(false);
        if (code != 0)
        {
            var why = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? $"it ended with {code}";
            return CheckoutAnswer.Refusal(operation, CheckoutAnswer.Failed, $"Git could not answer `{command}` in `{repository}`'s checkout ({why.TrimEnd('.')}).");
        }

        var text = stdout;
        var cut = Encoding.UTF8.GetByteCount(text) > Bound;
        if (cut)
        {
            // Cut between characters, as a file is.
            var bytes = Encoding.UTF8.GetBytes(text);
            var chars = new char[Bound];
            text = new string(chars, 0, Encoding.UTF8.GetDecoder().GetChars(bytes, 0, Bound, chars, 0, flush: false));
        }

        return new CheckoutAnswer(operation, true, null, cut
            ? $"`{repository}`'s checkout, `{command}`, its first part: the rest is cut."
            : $"`{repository}`'s checkout, `{command}`:") { Text = text };
    }

    /// <summary>
    /// What git ignores of <paramref name="paths"/> (repository-relative, a folder with a trailing slash), by git's own answer, each
    /// without its slash; null where git could not answer, which refuses the read.
    /// </summary>
    private static async Task<HashSet<string>?> IgnoredAsync(string root, IReadOnlyList<string> paths, CancellationToken ct)
    {
        var input = string.Concat(paths.Select(path => path + "\0"));
        var (code, stdout, _) = await WorkingTree.GitAsync(root, ["check-ignore", "-z", "--stdin"], input, ct).ConfigureAwait(false);
        // 0 where any is ignored, 1 where none is; anything else is git failing to say.
        return code is 0 or 1
            ? stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries).Select(path => path.TrimEnd('/')).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;
    }

    /// <summary>Whether the checkout has a git of its own: its <c>.git</c> folder, or the file a worktree has there. Never one above it.</summary>
    private static bool OwnGit(string root) => Directory.Exists(Path.Combine(root, ".git")) || File.Exists(Path.Combine(root, ".git"));

    /// <summary>Whether a registration's workspace is the conversation's; every one where it has none (<c>default</c>'s room, the whole machine).</summary>
    private static bool Within(string? workspace, string? circle) =>
        circle is not { Length: > 0 }
        || string.Equals(RemoteTarget.Workspace(workspace), RemoteTarget.Workspace(circle), StringComparison.OrdinalIgnoreCase);

    private static string Size(long bytes) => bytes switch
    {
        < 1024 => bytes == 1 ? "1 byte" : $"{bytes.ToString(CultureInfo.InvariantCulture)} bytes",
        < 1024 * 1024 => $"{(bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture)} KB",
        _ => $"{(bytes / (1024.0 * 1024)).ToString("0.#", CultureInfo.InvariantCulture)} MB",
    };
}

/// <summary>
/// A path the agent wrote, walked inside a checkout (HELPMODE1c4): what <see cref="HelpCheckouts"/> opens is only ever the names the
/// disk holds, found one at a time from the checkout's top, never the agent's spelling.
/// </summary>
internal static class CheckoutPath
{
    private static readonly EnumerationOptions Exact = new()
    {
        MatchType = MatchType.Simple, MatchCasing = MatchCasing.PlatformDefault, IgnoreInaccessible = true, AttributesToSkip = 0,
        RecurseSubdirectories = false,
    };

    /// <summary>
    /// The names of a path relative to the checkout, <c>.</c> and empty names dropped, or why it is no such path: rooted, a drive or a
    /// stream, a pattern, a step out (<c>..</c>, or a name of dots that one spelling reads as it), or git's own folder however spelled.
    /// </summary>
    public static (IReadOnlyList<string>? Names, string? Refusal) Segments(string? path)
    {
        var text = path?.Trim() ?? "";
        if (text.Length == 0) return ([], null);
        if (text.Any(char.IsControl)) return (null, "holds a control character, so it is no path");
        if (text[0] is '/' or '\\' || text.Contains(':') || Path.IsPathRooted(text))
        {
            return (null, "is no path inside the checkout: name it from the checkout's top, as `src/Game.cs`, never from a drive or a root");
        }

        if (text.IndexOfAny(['*', '?', '<', '>', '|', '"']) >= 0) return (null, "is a pattern, and the tool reads one path, a file or a folder");

        var names = text.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Where(name => name != ".").ToList();
        // Windows reads a name of dots and spaces as `..` or as nothing, and `.git.` as `.git`: each is refused by what it would be read as.
        if (names.Any(name => name.Trim('.', ' ').Length == 0)) return (null, "steps out of the checkout, and `..` is never followed");
        return names.Any(name => string.Equals(name.TrimEnd('.', ' '), ".git", StringComparison.OrdinalIgnoreCase))
            ? (null, "is git's own folder, which is never read: `status` and `branches` say how the checkout stands")
            : (names, null);
    }

    /// <summary>
    /// The names walked from <paramref name="root"/>, each as the disk spells it, and the file or folder found; none found where a name is
    /// not there; a refusal where a name is git's own folder or a link whose final target lies outside the checkout.
    /// </summary>
    public static (FileSystemInfo? Found, IReadOnlyList<string> Names, string? Refusal) Walk(string root, IReadOnlyList<string> names)
    {
        var top = Path.GetFullPath(root);
        var real = Real(new DirectoryInfo(top)) ?? top;
        FileSystemInfo current = new DirectoryInfo(top);
        var walked = new List<string>(names.Count);
        foreach (var name in names)
        {
            if (current is not DirectoryInfo folder) return (null, walked, null);
            var found = folder.EnumerateFileSystemInfos(name, Exact)
                .OrderByDescending(each => string.Equals(each.Name, name, StringComparison.Ordinal)).FirstOrDefault();
            if (found is null) return (null, walked, null);
            if (string.Equals(found.Name, ".git", StringComparison.OrdinalIgnoreCase)) return (null, walked, "is git's own folder, which is never read");
            if (found.LinkTarget is not null && (Real(found) is not { } target || !(AcrossRules.Holds(top, target) || AcrossRules.Holds(real, target))))
            {
                return (null, walked, "leads out of the checkout through a link, which is never followed out");
            }

            walked.Add(found.Name);
            current = found;
        }

        return (current, walked, null);
    }

    /// <summary>Where a link finally leads, or the path itself where it is none; null where its chain cannot be read.</summary>
    private static string? Real(FileSystemInfo info)
    {
        try
        {
            return info.LinkTarget is null ? info.FullName : info.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
