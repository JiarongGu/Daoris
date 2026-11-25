using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A new session tree's machine-local inputs: the files under <c>local/</c> its repository's gates read, which
/// git ignores and only the checkout holds, copied into the tree read-only as it opens, so its gates run the checks they run in the
/// checkout. A sensitive scan that finds no private pattern list in a fresh tree falls back to its built-in patterns and passes.
/// </summary>
/// <remarks>
/// <para><b>What is declared</b> is this machine's (the home's <c>localInputs</c> and <c>patternsFile</c>), else the checkout's
/// <c>daoris.gates.json</c> as an older repository holds them, never the tree's: what leaves the checkout's private files is the owner's say,
/// never a branch's. The patterns file is one input; naming none implies the devkit's default, copied where the checkout holds it and
/// unsaid where it does not. No gates file and nothing kept here, or a gates file that does not read, declares nothing.</para>
///
/// <para><b>The gate</b>, for each: a file under <c>local/</c>, no part of its path empty, <c>.</c> or <c>..</c>; the checkout holds
/// it; git ignores it in the tree, so the copy never reads as the tree's work; and no link stands on its way into the tree. A miss
/// copies nothing of it and the opening says which and why, except the implied default the checkout lacks.</para>
///
/// <para><b>The copy</b> is made under a name of its own beside where it goes, marked read-only and renamed into place, while the
/// tree is still locked as opening: a cut-off open takes back what it copied, and its undo takes the tree. Copied, never linked:
/// a link's write would land in the checkout, and a hard link shares the read-only mark with it.</para>
/// </remarks>
internal static class TreeLocalInputs
{
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
    internal static IReadOnlyList<LocalInput> Declared(string checkout, RepositoryBudgets? kept = null)
    {
        var file = FromGatesFile(checkout);
        var found = new List<LocalInput>();
        var inputs = kept?.LocalInputs ?? file?.Inputs;
        var patterns = kept?.PatternsFile ?? file?.Patterns;
        if (file is null && kept?.LocalInputs is null && patterns is null) return found;
        foreach (var path in inputs ?? []) Add(found, new(Spelled(path), Declared: true));
        Add(found, patterns is not null ? new(Spelled(patterns), Declared: true) : new(DefaultPatternsFile, Declared: false));
        return found;
    }

    /// <summary>The private patterns file this machine keeps, else the gates file names, else the default, in git's spelling; null where neither is read or the path is no input's.</summary>
    internal static string? PatternsFile(string checkout, RepositoryBudgets? kept = null)
    {
        var file = FromGatesFile(checkout);
        if (file is null && kept?.PatternsFile is null) return null;
        var path = Spelled(kept?.PatternsFile ?? file?.Patterns ?? DefaultPatternsFile);
        return Within(path) ? path : null;
    }

    private sealed record GatesFileInputs(IReadOnlyList<string> Inputs, string? Patterns);

    private static GatesFileInputs? FromGatesFile(string checkout)
    {
        try
        {
            var file = Path.Combine(checkout, DeclarationNames.Gates);
            if (!File.Exists(file)) return null;
            using var document = JsonDocument.Parse(File.ReadAllBytes(file));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var inputs = new List<string>();
            if (root.TryGetProperty("localInputs", out var named) && named.ValueKind == JsonValueKind.Array)
            {
                inputs.AddRange(named.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()).OfType<string>().Where(path => path.Length > 0));
            }

            var patterns = root.TryGetProperty("sensitive", out var sensitive) && sensitive.ValueKind == JsonValueKind.Object
                && sensitive.TryGetProperty("patternsFile", out var declared) && declared.ValueKind == JsonValueKind.String
                && declared.GetString() is { Length: > 0 } path ? path : null;
            return new(inputs, patterns);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The declared inputs copied into <paramref name="tree"/>, a tree of <paramref name="root"/> git has just checked out:
    /// git says which it ignores there, and <see cref="Copy"/> does the rest. A cancel stops it and takes back what it copied.
    /// </summary>
    internal static async Task<LocalCopy> CopyAsync(string root, string tree, Func<string, FileVerdict> permit, CancellationToken ct, RepositoryBudgets? kept = null)
    {
        var declared = Declared(root, kept);
        if (declared.Count == 0) return LocalCopy.None;
        var asked = declared.Where(input => Candidate(root, input)).Select(input => input.Path).ToList();
        if (asked.Count == 0) return Copy(root, tree, declared, _ => false, permit, null, ct);

        // 🔴 git takes `-z` only with `--stdin`: the paths go in there.
        var (code, said, err) = await WorkingTree.GitAsync(tree, ["check-ignore", "--stdin", "-z"], string.Concat(asked.Select(path => path + "\0")), ct)
            .ConfigureAwait(false);
        if (code is not (0 or 1))
        {
            return Copy(root, tree, declared, _ => false, permit, $"git could not say whether it ignores it: {err.Trim().Split('\n')[0].Trim()}", ct);
        }

        var ignored = said.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToHashSet(PathComparer);
        return await Task.Run(() => Copy(root, tree, declared, ignored.Contains, permit, null, ct), ct).ConfigureAwait(false);
    }

    /// <summary>The gate and the copy, git's word on what it ignores in the tree handed in; the person's permissions are not asked.</summary>
    internal static LocalCopy Reuse(string root, string tree, Func<string, bool> ignored, CancellationToken ct, RepositoryBudgets? kept = null) =>
        Copy(root, tree, Declared(root, kept), ignored, _ => FileVerdict.Made, null, ct);

    /// <summary>
    /// Each input judged and copied, or why not; where git could not say what it ignores (<paramref name="unsaid"/>), each the
    /// checkout holds copies nothing and says git's words.
    /// </summary>
    private static LocalCopy Copy(
        string root, string tree, IReadOnlyList<LocalInput> declared, Func<string, bool> ignored, Func<string, FileVerdict> permit, string? unsaid, CancellationToken ct)
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

                if (permit(input.Path) is var verdict and not FileVerdict.Made)
                {
                    declined.Add(new(input.Path, verdict == FileVerdict.Denied ? LocalUncopiedWhy.Denied : LocalUncopiedWhy.Held));
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
        var under = Path.Combine(Path.GetDirectoryName(final)!, DependencyStore.MakingPrefix + Guid.NewGuid().ToString("N")[..8]);
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

    /// <summary>A path under <c>local/</c> in git's spelling, every part of it a name: the rule the owner's door judges a binding by.</summary>
    internal static bool Within(string path) => CheckBudgets.UnderLocal(path);

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

/// <summary>A file a repository's gates read that only its checkout holds.</summary>
/// <param name="Path">Relative to the repository, in git's spelling.</param>
/// <param name="Declared">Named by the gates file; false for the devkit's default patterns file, implied where it names none.</param>
internal sealed record LocalInput(string Path, bool Declared);

/// <summary>What a tree's open copied of its checkout's machine-local inputs, and why it copied none of the rest.</summary>
/// <param name="Copied">The files copied, relative to the repository in git's spelling.</param>
/// <param name="Declined">Each declared file not copied, and why.</param>
internal sealed record LocalCopy(IReadOnlyList<string> Copied, IReadOnlyList<LocalUncopied> Declined)
{
    /// <summary>A repository that declares none: nothing copied, and nothing to say.</summary>
    public static LocalCopy None { get; } = new([], []);

    /// <summary>The files copied, each in code, as a sentence lists them.</summary>
    public string Listed => string.Join(", ", Copied.Select(path => $"`{path}`"));

    /// <summary>Its line of the opening where anything is copied, with its code; null where nothing is.</summary>
    public Noted? Line() => Copied.Count == 0
        ? null
        : Noted.Of(
            StartNoteCodes.StartedLocalCopied,
            $"The machine-local files the repository's gates read were copied in from your checkout, read-only: {Listed}.",
            ("inputs", (IReadOnlyList<string>)[.. Copied]));

    /// <summary>
    /// What the session's agent is told, where anything is copied: what the files are, and that they are the checkout's to keep,
    /// so a gate's finding is fixed in the work and never in the list it is found with.
    /// </summary>
    public string? Told => Copied.Count == 0
        ? null
        : $"Daoris copied in from the repository's checkout, read-only, the machine-local files its gates read: {Listed}. They are the "
          + "checkout's, not this tree's work: leave them as they are, and fix what a gate finds with them in your own changes.";
}

/// <summary>A declared machine-local input not copied into a new tree, and why: a line of the tree's opening.</summary>
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
                StartNoteCodes.StartedLocalMissing, $"`{Input}` was not copied: the repository's gates read it, and your checkout does not hold it.", input),
            LocalUncopiedWhy.Refused => Noted.Of(
                StartNoteCodes.StartedLocalRefused,
                $"`{Input}` was not copied: Daoris copies into a tree only a file under `local/` that git ignores there, and this is not one.", input),
            LocalUncopiedWhy.Held => Noted.Of(
                StartNoteCodes.StartedLocalHeld,
                $"`{Input}` was not copied: an `ask` in your permissions holds this copy for an allow, which the next start reads.", input),
            LocalUncopiedWhy.Denied => Noted.Of(
                StartNoteCodes.StartedLocalDenied, $"`{Input}` was not copied: your permissions deny this copy.", input),
            _ when Detail is { Length: > 0 } said => Noted.Of(
                    StartNoteCodes.StartedLocalFailed, $"`{Input}` was not copied from your checkout: the copy failed ({said}).", input)
                .Also(NotePart.Said(said, NoteBy.Program)),
            _ => Noted.Of(StartNoteCodes.StartedLocalFailed, $"`{Input}` was not copied from your checkout: the copy failed.", input),
        };
    }
}

/// <summary>Why a declared machine-local input is not copied into a new tree: each its own line's code.</summary>
internal static class LocalUncopiedWhy
{
    /// <summary>The checkout does not hold it.</summary>
    public const string Missing = "missing";

    /// <summary>Not a file under <c>local/</c> that git ignores in the tree, or a link stands on its way there.</summary>
    public const string Refused = "refused";

    /// <summary>The copy failed, or git could not say whether it ignores it.</summary>
    public const string Failed = "failed";

    /// <summary>An <c>ask</c> in the person's permissions holds it for an allow.</summary>
    public const string Held = "held";

    /// <summary>A <c>deny</c> in the person's permissions matches it.</summary>
    public const string Denied = "denied";
}
