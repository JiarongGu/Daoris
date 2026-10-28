using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A new session tree's machine-local inputs (TREELOCAL1, D216): the files under <c>local/</c> its repository's gates read, which
/// git ignores and only the checkout holds, copied into the tree read-only as it opens, so its gates run the checks they run in the
/// checkout. A sensitive scan that finds no private pattern list in a fresh tree falls back to its built-in patterns and passes.
/// </summary>
/// <remarks>
/// <para><b>What is declared</b> is read from the checkout's <c>daoris.gates.json</c>, never the tree's: what leaves the checkout's
/// private files is the checkout's say, never a branch's, and a chain's step grows from a branch a session wrote. Its
/// <c>localInputs</c> names them, and its <c>sensitive.patternsFile</c> is one, as the merge tool's gate tree takes it (GATEWT1b);
/// a gates file naming no patterns file implies the devkit's default, copied where the checkout holds it and unsaid where it does
/// not, since a repository that never wrote one may scan with its built-in patterns on purpose. No gates file, or one that does not
/// read, declares nothing: the repository's own gates refuse that one in its own words.</para>
///
/// <para><b>The gate</b>, for each: a file under <c>local/</c>, no part of its path empty, <c>.</c> or <c>..</c>; the checkout holds
/// it; git ignores it in the tree, so the copy never reads as the tree's work; and no link stands on its way into the tree. A miss
/// copies nothing of it and the opening says which and why, except the implied default the checkout lacks.</para>
///
/// <para><b>The copy</b> is made under a name of its own beside where it goes, marked read-only and renamed into place, while the
/// tree is still locked as opening: a cut-off open takes back what it copied, and its undo takes the tree. Copied, never linked:
/// a link's write would land in the checkout, and a hard link shares the read-only mark with it (D216).</para>
/// </remarks>
internal static class TreeLocalInputs
{
    /// <summary>The folder every input is under: the family's machine-local, ignored one, as the merge tool's gate tree takes it.</summary>
    private const string Local = "local/";

    /// <summary>The devkit's private pattern list where the gates file names none (<c>SensitiveOptions</c>' default).</summary>
    internal const string DefaultPatternsFile = "local/sensitive-patterns.txt";

    /// <summary>
    /// The test seam: run with each file's path in the checkout before it is copied, where a test cuts the open off. Production
    /// sets none.
    /// </summary>
    internal static readonly AsyncLocal<Action<string>?> Copying = new();

    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// What <paramref name="checkout"/>'s gates file declares its gates read from <c>local/</c>, each once in its first spelling, its
    /// slashes git's: <c>localInputs</c> in order, then <c>sensitive.patternsFile</c>, or the devkit's default, implied.
    /// </summary>
    internal static IReadOnlyList<LocalInput> Declared(string checkout)
    {
        var file = Path.Combine(checkout, GeneratedWriters.DeclarationFile);
        var found = new List<LocalInput>();
        try
        {
            if (!File.Exists(file)) return found;
            using var document = JsonDocument.Parse(File.ReadAllBytes(file));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return found;
            if (root.TryGetProperty("localInputs", out var inputs) && inputs.ValueKind == JsonValueKind.Array)
            {
                foreach (var each in inputs.EnumerateArray())
                {
                    if (each.ValueKind == JsonValueKind.String && each.GetString() is { Length: > 0 } path) Add(found, new(Spelled(path), Declared: true));
                }
            }

            Add(found, root.TryGetProperty("sensitive", out var sensitive) && sensitive.ValueKind == JsonValueKind.Object
                && sensitive.TryGetProperty("patternsFile", out var patterns) && patterns.ValueKind == JsonValueKind.String
                && patterns.GetString() is { Length: > 0 } named
                    ? new(Spelled(named), Declared: true)
                    : new(DefaultPatternsFile, Declared: false));
            return found;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// The checkout's declared inputs copied into <paramref name="tree"/>, a tree of <paramref name="root"/> git has just checked out:
    /// git says which it ignores there, and <see cref="Copy"/> does the rest. A cancel stops it and takes back what it copied.
    /// </summary>
    internal static async Task<LocalCopy> CopyAsync(string root, string tree, CancellationToken ct)
    {
        var declared = Declared(root);
        if (declared.Count == 0) return LocalCopy.None;
        var asked = declared.Where(input => Candidate(root, input)).Select(input => input.Path).ToList();
        if (asked.Count == 0) return Copy(root, tree, declared, _ => false, null, ct);

        // 🔴 git takes `-z` only with `--stdin` (TREEDEPS1's finding): the paths go in there.
        var (code, said, err) = await WorkingTree.GitAsync(tree, ["check-ignore", "--stdin", "-z"], string.Concat(asked.Select(path => path + "\0")), ct)
            .ConfigureAwait(false);
        if (code is not (0 or 1))
        {
            return Copy(root, tree, declared, _ => false, $"git could not say whether it ignores it: {err.Trim().Split('\n')[0].Trim()}", ct);
        }

        var ignored = said.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToHashSet(PathComparer);
        return await Task.Run(() => Copy(root, tree, declared, ignored.Contains, null, ct), ct).ConfigureAwait(false);
    }

    /// <summary>The gate and the copy, git's word on what it ignores in the tree handed in.</summary>
    internal static LocalCopy Reuse(string root, string tree, Func<string, bool> ignored, CancellationToken ct) =>
        Copy(root, tree, Declared(root), ignored, null, ct);

    /// <summary>
    /// Each input judged and copied, or why not; where git could not say what it ignores (<paramref name="unsaid"/>), each the
    /// checkout holds copies nothing and says git's words.
    /// </summary>
    private static LocalCopy Copy(
        string root, string tree, IReadOnlyList<LocalInput> declared, Func<string, bool> ignored, string? unsaid, CancellationToken ct)
    {
        if (declared.Count == 0) return LocalCopy.None;
        root = Path.GetFullPath(root);
        tree = Path.GetFullPath(tree);
        var copied = new List<string>();
        var made = new List<string>();
        var declined = new List<LocalUncopied>();
        try
        {
            foreach (var input in declared)
            {
                var source = Path.Combine(root, input.Path);
                if (!Within(input.Path) || Directory.Exists(source))
                {
                    declined.Add(new(input.Path, LocalUncopiedWhy.Refused));
                    continue;
                }

                if (!File.Exists(source))
                {
                    if (input.Declared) declined.Add(new(input.Path, LocalUncopiedWhy.Missing));
                    continue;
                }

                if (unsaid is not null)
                {
                    declined.Add(new(input.Path, LocalUncopiedWhy.Failed, unsaid));
                    continue;
                }

                var final = Path.Combine(tree, input.Path);
                if (!ignored(input.Path) || LinkOnTheWay(tree, final))
                {
                    declined.Add(new(input.Path, LocalUncopiedWhy.Refused));
                    continue;
                }

                Copying.Value?.Invoke(source);
                ct.ThrowIfCancellationRequested();
                if (CopyOne(source, final, made) is { } failed)
                {
                    declined.Add(new(input.Path, LocalUncopiedWhy.Failed, failed));
                    continue;
                }

                copied.Add(input.Path);
            }
        }
        catch (OperationCanceledException)
        {
            Discard(made);
            throw;
        }

        return copied.Count == 0 && declined.Count == 0 ? LocalCopy.None : new(copied, declined);
    }

    /// <summary>
    /// One file copied under a name of its own beside <paramref name="final"/>, marked read-only, and renamed into place, each made
    /// path in <paramref name="made"/>. Null once there, or what failed, in the program's words, with nothing of it left.
    /// </summary>
    private static string? CopyOne(string source, string final, List<string> made)
    {
        var under = Path.Combine(Path.GetDirectoryName(final)!, TreeDependencies.CopyingPrefix + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(final)!);
            made.Add(under);
            File.Copy(source, under);
            File.SetAttributes(under, File.GetAttributes(under) | FileAttributes.ReadOnly);
            File.Move(under, final);
            made.Remove(under);
            made.Add(final);
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Discard([under]);
            made.Remove(under);
            return error.Message.Trim();
        }
    }

    /// <summary>Whether a checkout's file is one to look at: its path one an input may name, and the checkout holding it.</summary>
    private static bool Candidate(string root, LocalInput input) => Within(input.Path) && File.Exists(Path.Combine(root, input.Path));

    /// <summary>A path under <c>local/</c> in git's spelling, every part of it a name: none empty, <c>.</c>, <c>..</c>, or holding a colon.</summary>
    internal static bool Within(string path) =>
        path.StartsWith(Local, StringComparison.Ordinal)
        && path.Split('/').All(part => part.Length > 0 && part is not ("." or "..") && !part.Contains(':'));

    /// <summary>Whether a folder that stands between <paramref name="tree"/> and <paramref name="final"/> is a link, or the file's place is taken.</summary>
    private static bool LinkOnTheWay(string tree, string final)
    {
        if (Path.Exists(final)) return true;
        for (var folder = Path.GetDirectoryName(final); folder is not null && folder.Length > tree.Length; folder = Path.GetDirectoryName(folder))
        {
            if (Path.Exists(folder) && File.GetAttributes(folder).HasFlag(FileAttributes.ReparsePoint)) return true;
        }

        return false;
    }

    /// <summary>What a copy made, read-only marks taken off and deleted, as far as it goes.</summary>
    private static void Discard(IEnumerable<string> made)
    {
        foreach (var each in made.Where(File.Exists).ToList())
        {
            try
            {
                File.SetAttributes(each, FileAttributes.Normal);
                File.Delete(each);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Inside the tree, under its ignored `local/`, which an open cut off removes whole.
            }
        }
    }

    /// <summary>A declared path in git's spelling: forward slashes.</summary>
    private static string Spelled(string path) => path.Replace('\\', '/');

    private static void Add(List<LocalInput> found, LocalInput input)
    {
        var at = found.FindIndex(each => PathComparer.Equals(each.Path, input.Path));
        if (at < 0) found.Add(input);
        else if (input.Declared && !found[at].Declared) found[at] = input;
    }
}

/// <summary>A file a repository's gates read that only its checkout holds (TREELOCAL1).</summary>
/// <param name="Path">Relative to the repository, in git's spelling.</param>
/// <param name="Declared">Named by the gates file; false for the devkit's default patterns file, implied where it names none.</param>
internal sealed record LocalInput(string Path, bool Declared);

/// <summary>What a tree's open copied of its checkout's machine-local inputs (TREELOCAL1), and why it copied none of the rest.</summary>
/// <param name="Copied">The files copied, relative to the repository in git's spelling.</param>
/// <param name="Declined">Each declared file not copied, and why.</param>
internal sealed record LocalCopy(IReadOnlyList<string> Copied, IReadOnlyList<LocalUncopied> Declined)
{
    /// <summary>A repository that declares none: nothing copied, and nothing to say.</summary>
    public static LocalCopy None { get; } = new([], []);

    /// <summary>The files copied, each in code, as a sentence lists them.</summary>
    public string Listed => string.Join(", ", Copied.Select(path => $"`{path}`"));

    /// <summary>Its line of the opening where anything was copied, with its code; null where nothing was.</summary>
    public Noted? Line() => Copied.Count == 0
        ? null
        : Noted.Of(
            NoteCodes.StartedLocalCopied,
            $"The machine-local files the repository's gates read were copied in from your checkout, read-only: {Listed}.",
            ("inputs", (IReadOnlyList<string>)[.. Copied]));

    /// <summary>
    /// What the session's agent is told, where anything was copied: what the files are, and that they are the checkout's to keep,
    /// so a gate's finding is fixed in the work and never in the list it was found with.
    /// </summary>
    public string? Told => Copied.Count == 0
        ? null
        : $"Daoris copied in from the repository's checkout, read-only, the machine-local files its gates read: {Listed}. They are the "
          + "checkout's, not this tree's work: leave them as they are, and fix what a gate finds with them in your own changes.";
}

/// <summary>A declared machine-local input not copied into a new tree (TREELOCAL1), and why: a line of the tree's opening.</summary>
/// <param name="Input">The file, relative to the repository in git's spelling, as declared.</param>
/// <param name="Why">Why (<see cref="LocalUncopiedWhy"/>).</param>
/// <param name="Detail">What failed, in the program's words, where the copy did.</param>
internal sealed record LocalUncopied(string Input, string Why, string? Detail = null)
{
    /// <summary>Its line of the opening, with its code.</summary>
    public Noted Line()
    {
        var input = ("input", (object?)Input);
        return Why switch
        {
            LocalUncopiedWhy.Missing => Noted.Of(
                NoteCodes.StartedLocalMissing, $"`{Input}` was not copied: the repository's gates read it, and your checkout does not hold it.", input),
            LocalUncopiedWhy.Refused => Noted.Of(
                NoteCodes.StartedLocalRefused,
                $"`{Input}` was not copied: Daoris copies into a tree only a file under `local/` that git ignores there, and this is not one.", input),
            _ when Detail is { Length: > 0 } said => Noted.Of(
                    NoteCodes.StartedLocalFailed, $"`{Input}` was not copied from your checkout: the copy failed ({said}).", input)
                .Also(NotePart.Said(said, NoteBy.Program)),
            _ => Noted.Of(NoteCodes.StartedLocalFailed, $"`{Input}` was not copied from your checkout: the copy failed.", input),
        };
    }
}

/// <summary>Why a declared machine-local input was not copied into a new tree (TREELOCAL1): each its own line's code.</summary>
internal static class LocalUncopiedWhy
{
    /// <summary>The checkout does not hold it.</summary>
    public const string Missing = "missing";

    /// <summary>Not a file under <c>local/</c> that git ignores in the tree, or a link stands on its way there.</summary>
    public const string Refused = "refused";

    /// <summary>The copy failed, or git could not say whether it ignores it.</summary>
    public const string Failed = "failed";
}
