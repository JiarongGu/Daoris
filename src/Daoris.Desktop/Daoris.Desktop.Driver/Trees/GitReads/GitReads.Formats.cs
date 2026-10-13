using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The git calls behind a page and the pure readers of what git answers (GIT1b, D147 §4.1): each call asks a machine format,
/// never a human one, and each format has a reader here with its own tests, apart from any process.
/// </summary>
/// <remarks>
/// <para><b>What a person's settings would change in the answer is said on the call.</b> <c>--no-color</c>,
/// <c>--no-show-signature</c> (<c>log.showSignature</c> writes gpg's lines into the answer) and <c>--encoding=UTF-8</c>
/// (<c>i18n.logOutputEncoding</c> would re-encode every message). Names are the commit's own (<c>%an</c>, never the mailmap's
/// <c>%aN</c>), since a <c>.mailmap</c> in the checkout would change an answer named only by ids.</para>
///
/// <para><b>A record is fields ended by NULs.</b> <c>-z</c> ends each commit with a NUL, so a list of commits splits into
/// fields of a known count. No field the formats ask holds a NUL: an id, a name, an address, a time, or a message.</para>
///
/// <para><b>Each call ends its revisions with <c>--</c></b>, so nothing it names is ever read as a path, and a path after it
/// is never read as an option.</para>
/// </remarks>
public static class GitFormats
{
    /// <summary>The fields of a commit as a list draws it: its id, parents, author, address, authored and committed times, and subject.</summary>
    internal const string Listed = "%H%x00%P%x00%an%x00%ae%x00%aI%x00%cI%x00%s";

    private const int ListedFields = 7;

    /// <summary>A commit's own fields for its page: <see cref="Listed"/>'s and the committer's, then its whole message.</summary>
    private const string Detailed = "%H%x00%P%x00%an%x00%ae%x00%aI%x00%cn%x00%ce%x00%cI%x00%s%x00%B";

    private const int DetailedFields = 10;

    // Marks the start of each commit in a file's history (`%x01`), where the paths that changed follow its fields.
    private const char Start = '\u0001';

    private static readonly string[] Log = ["log", "--no-color", "--no-show-signature", "--encoding=UTF-8", "-z"];

    /// <summary>
    /// A page of history from <paramref name="tips"/>: <see cref="GitReads.GraphPage"/> commits in date order and one more,
    /// which says whether another page follows. <paramref name="sinceParted"/> walks two tips since they parted (<c>A...B</c>),
    /// each commit marked with the side that holds it.
    /// </summary>
    public static IReadOnlyList<string> GraphArguments(IReadOnlyList<string> tips, bool sinceParted, int page)
    {
        List<string> arguments = [.. Log, "--date-order"];
        if (sinceParted) arguments.Add("--left-right");
        arguments.Add("--format=" + (sinceParted ? "%m%x00" : "") + Listed);
        arguments.Add($"--skip={page * GitReads.GraphPage}");
        arguments.Add($"--max-count={GitReads.GraphPage + 1}");
        if (sinceParted) arguments.Add($"{tips[0]}...{tips[1]}");
        else arguments.AddRange(tips);
        arguments.Add("--");
        return arguments;
    }

    /// <summary>One commit's fields and whole message.</summary>
    public static IReadOnlyList<string> CommitArguments(string commit) => [.. Log, "--max-count=1", "--format=" + Detailed, commit, "--"];

    /// <summary>
    /// The commits that changed <paramref name="path"/>, following renames, each with the path as it changed there. Up to the
    /// end of page <paramref name="page"/> and one more: <c>--follow</c> ignores <c>--skip</c> (Git 2.53 printed nothing for
    /// <c>--skip=1</c>), so a page is cut from the whole walk to its end. Literal, so a <c>*</c> in a name is that name.
    /// </summary>
    public static IReadOnlyList<string> HistoryArguments(string commit, string path, int page) =>
    [
        "--literal-pathspecs", .. Log, "--follow", "--name-status", "--format=%x01" + Listed,
        $"--max-count={((page + 1) * GitReads.HistoryPage) + 1}", commit, "--", path,
    ];

    /// <summary>The file as <paramref name="commit"/> holds it, each line with the commit that last changed it, in git's porcelain form.</summary>
    public static IReadOnlyList<string> BlameArguments(string commit, string path) =>
        ["-c", "core.quotePath=false", "blame", "--porcelain", "--encoding=UTF-8", commit, "--", path];

    /// <summary>The commits only one side holds, each marked with its side, up to <see cref="GitReads.CompareCommits"/> and one more.</summary>
    public static IReadOnlyList<string> CompareArguments(string left, string right) =>
    [
        .. Log, "--date-order", "--left-right", "--format=%m%x00" + Listed, $"--max-count={GitReads.CompareCommits + 1}", $"{left}...{right}", "--",
    ];

    /// <summary>The local branches and origin's whose history holds <paramref name="commit"/>, each with the ref a symbolic one points to.</summary>
    public static IReadOnlyList<string> HoldingArguments(string commit) =>
        ["for-each-ref", "--contains", commit, "--format=%(refname)%00%(symref)%00", GitRefs.Heads.TrimEnd('/'), GitRefs.Origin.TrimEnd('/')];

    /// <summary>The commit <paramref name="revision"/> names now, in full, or nothing where it names none.</summary>
    public static IReadOnlyList<string> ResolveArguments(string revision) =>
        ["rev-parse", "--verify", "--quiet", "--end-of-options", revision + "^{commit}"];

    /// <summary>
    /// Every commit a list's call answered, in git's order; <paramref name="marked"/> where each starts with its side's mark.
    /// A record cut short, or one naming no commit, is passed over rather than read askew.
    /// </summary>
    public static IReadOnlyList<GitLogCommit> ParseListed(string stdout, bool marked)
    {
        var fields = stdout.Split('\0');
        var width = ListedFields + (marked ? 1 : 0);
        var commits = new List<GitLogCommit>();
        for (var at = 0; at + width <= fields.Length; at += width)
        {
            var side = marked ? SideOf(fields[at].TrimStart('\n')) : null;
            if (Commit(fields, marked ? at + 1 : at) is { } commit) commits.Add(commit with { Side = side });
        }

        return commits;
    }

    /// <summary>One commit's fields and message, or null where git's answer was not one whole commit.</summary>
    public static GitCommitDetail? ParseCommit(string stdout)
    {
        var fields = stdout.Split('\0');
        if (fields.Length < DetailedFields + 1) return null;
        var commit = fields[0].TrimStart('\n');
        if (!GitReads.IsWholeId(commit)) return null;

        return new GitCommitDetail(commit, Parents(fields[1]), fields[8], fields[9].TrimEnd('\n'))
        {
            Author = fields[2],
            AuthorEmail = fields[3],
            AuthoredAt = Time(fields[4]),
            Committer = fields[5],
            CommitterEmail = fields[6],
            CommittedAt = Time(fields[7]),
        };
    }

    /// <summary>
    /// A file's history as <c>log --follow --name-status -z</c> answers it: each commit's fields after its start mark, then
    /// the status and the path as it changed there, two paths for a rename or a copy. A commit with no status is listed with
    /// no path; a status beyond the first is passed over, since <c>--follow</c> limits each to the one path.
    /// </summary>
    public static IReadOnlyList<GitFileChange> ParseHistory(string stdout)
    {
        var tokens = stdout.Split('\0');
        var changes = new List<GitFileChange>();
        var at = 0;
        while (at < tokens.Length)
        {
            var token = tokens[at].TrimStart('\n');
            if (token.Length == 0 || token[0] != Start || at + ListedFields > tokens.Length)
            {
                at++;
                continue;
            }

            var fields = tokens[at..(at + ListedFields)];
            fields[0] = token[1..];
            at += ListedFields;
            if (Commit(fields, 0) is not { } commit) continue;

            var change = new GitFileChange(commit, "modified", "");
            if (at < tokens.Length && tokens[at].TrimStart('\n') is [var letter and >= 'A' and <= 'Z', ..])
            {
                var two = letter is 'R' or 'C';
                if (at + (two ? 2 : 1) < tokens.Length)
                {
                    change = two
                        ? change with { Status = StatusOf(letter), Path = tokens[at + 2], From = tokens[at + 1] }
                        : change with { Status = StatusOf(letter), Path = tokens[at + 1] };
                }

                at += two ? 3 : 2;
            }

            changes.Add(change);
        }

        return changes;
    }

    /// <summary>A whole blame answer read at once, as <see cref="GitBlameSplit"/> reads it a piece at a time.</summary>
    public static GitBlame ParseBlame(string stdout, string commit, string path)
    {
        var split = new GitBlameSplit();
        split.Take(stdout);
        return split.End(commit, path);
    }

    /// <summary>The whole names of the branches holding a commit, origin's <c>HEAD</c> and any other symbolic ref left out.</summary>
    public static IReadOnlyList<string> ParseHolding(string stdout)
    {
        var fields = stdout.Split('\0');
        var names = new List<string>();
        for (var at = 0; at + 2 <= fields.Length; at += 2)
        {
            var name = fields[at].TrimStart('\n', '\r');
            if (name.StartsWith("refs/", StringComparison.Ordinal) && fields[at + 1].Length == 0) names.Add(name);
        }

        return names;
    }

    /// <summary>The words a status letter is said in, as the review says them (D113).</summary>
    internal static string StatusOf(char letter) => letter switch
    {
        'A' => "added",
        'D' => "deleted",
        'R' => "renamed",
        'C' => "copied",
        _ => "modified",
    };

    /// <summary>
    /// A path as git quotes it under <c>core.quotePath=false</c> (<c>quote_c_style</c>), read back as itself: in double
    /// quotes where it holds a double quote, a backslash or a control character, each escaped, an octal escape a byte.
    /// </summary>
    public static string Unquoted(string text)
    {
        if (text.Length < 2 || text[0] != '"' || text[^1] != '"') return text;

        var bytes = new List<byte>(text.Length);
        var plain = new StringBuilder();
        void Flush()
        {
            if (plain.Length == 0) return;
            bytes.AddRange(Encoding.UTF8.GetBytes(plain.ToString()));
            plain.Clear();
        }

        for (var i = 1; i < text.Length - 1; i++)
        {
            var c = text[i];
            if (c != '\\' || i + 1 >= text.Length - 1)
            {
                plain.Append(c);
                continue;
            }

            var next = text[++i];
            if (next is >= '0' and <= '7' && i + 2 < text.Length - 1 && IsOctal(text[i + 1]) && IsOctal(text[i + 2]))
            {
                Flush();
                bytes.Add((byte)(((next - '0') << 6) | ((text[i + 1] - '0') << 3) | (text[i + 2] - '0')));
                i += 2;
                continue;
            }

            plain.Append(next switch
            {
                'a' => '\a',
                'b' => '\b',
                't' => '\t',
                'n' => '\n',
                'v' => '\v',
                'f' => '\f',
                'r' => '\r',
                _ => next,
            });
        }

        Flush();
        return Encoding.UTF8.GetString([.. bytes]);
    }

    /// <summary>A page of history as a person types it, the graph drawn: <c>git log --graph --date-order</c> from the same tips.</summary>
    public static string GraphCommand(IReadOnlyList<string> tips, bool sinceParted) =>
        GitRefs.Command(sinceParted
            ? ["log", "--graph", "--date-order", "--left-right", $"{tips[0]}...{tips[1]}"]
            : ["log", "--graph", "--date-order", .. tips]);

    /// <summary>A commit as a person types it: its fields, then its changes against <paramref name="against"/>, or a root commit's own.</summary>
    public static IReadOnlyList<string> CommitCommands(string commit, string? against) =>
    [
        GitRefs.Command(["show", "-s", commit]),
        against is null ? GitRefs.Command(["show", "--format=", commit]) : GitRefs.Command(["diff", against, commit]),
    ];

    /// <summary>A file's history as a person types it; literal where the path holds what git would read as a pattern.</summary>
    public static string HistoryCommand(string commit, string path) =>
        GitRefs.Command(Magic(path) ? ["--literal-pathspecs", "log", "--follow", commit, "--", path] : ["log", "--follow", commit, "--", path]);

    public static string BlameCommand(string commit, string path) => GitRefs.Command(["blame", commit, "--", path]);

    /// <summary>A compare as a person types it: the commits only on each side, then the changes, since the two parted or everything.</summary>
    public static IReadOnlyList<string> CompareCommands(string left, string right, bool everything) =>
    [
        GitRefs.Command(["log", "--left-right", "--date-order", $"{left}...{right}"]),
        GitRefs.Command(["diff", everything ? $"{left}..{right}" : $"{left}...{right}"]),
    ];

    public static string HoldingCommand(string commit) =>
        GitRefs.Command(["for-each-ref", "--contains", commit, GitRefs.Heads.TrimEnd('/'), GitRefs.Origin.TrimEnd('/')]);

    public static string ResolveCommand(string revision) => GitRefs.Command(["rev-parse", "--verify", revision + "^{commit}"]);

    /// <summary>A commit id as a sentence names it.</summary>
    internal static string Short(string commit) => commit.Length > 8 ? commit[..8] : commit;

    /// <summary>One commit's seven fields from <paramref name="at"/>, or null where they name no commit.</summary>
    private static GitLogCommit? Commit(string[] fields, int at)
    {
        var commit = fields[at].TrimStart('\n');
        if (!GitReads.IsWholeId(commit)) return null;

        return new GitLogCommit(commit, Parents(fields[at + 1]), fields[at + 2], fields[at + 3], fields[at + 6])
        {
            AuthoredAt = Time(fields[at + 4]),
            CommittedAt = Time(fields[at + 5]),
        };
    }

    private static IReadOnlyList<string> Parents(string field) =>
        [.. field.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(GitReads.IsWholeId)];

    private static DateTimeOffset? Time(string field) =>
        DateTimeOffset.TryParse(field, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when) ? when : null;

    private static string? SideOf(string mark) => mark switch
    {
        "<" => GitSide.Left,
        ">" => GitSide.Right,
        _ => null,
    };

    private static bool IsOctal(char c) => c is >= '0' and <= '7';

    /// <summary>Whether git would read anything in the path as a pattern or as magic, were it not literal.</summary>
    private static bool Magic(string path) => path.IndexOfAny(['*', '?', '[', ':', '\\']) >= 0;
}
