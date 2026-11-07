namespace Daoris.Driver;

/// <summary>
/// <c>daoris files &lt;repository&gt;</c>: what the rule file's layers say of each path git does not manage in a repository's
/// checkout, each top's effective rule, the layer that decided and what the layers below it said, read at its line's tip.
/// <c>read</c> reads the repository again and writes its found layer; <c>set</c> and <c>remove</c> edit the person's layer. Nothing
/// here copies, installs, clears or stores anything, and nothing in a tree is written. The three writes are the person's, refused
/// from a session's terminal.
/// </summary>
[CommandGroup("files", Whole = true)]
public static class FilesCommand
{
    public const string Usage =
        "usage: daoris files <repository> [read | set <kind> <path>… [--copy|--no-copy] [--machine] | remove <path>… [--machine]]";

    /// <summary>
    /// The words after <c>files</c>, the repository named first, over its checkout <paramref name="root"/>, read at the tip of the line
    /// the driver's settings or the checkout name.
    /// </summary>
    /// <param name="session">Whether this terminal is a session's, whose writes are refused.</param>
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args, string home, string root, bool session, TextWriter output, TextWriter error, CancellationToken ct)
    {
        if (args.Count == 0 || args[0].StartsWith('-'))
        {
            error.WriteLine(Usage);
            return 2;
        }

        var (repository, verb) = (args[0], args.Count > 1 ? args[1] : null);
        var words = args.Skip(2).ToList();
        if (verb is "read" or "set" or "remove" && session)
        {
            error.WriteLine($"daoris: {DriverChoicesCommand.SessionsTerminal}");
            return 1;
        }

        var machine = words.Remove("--machine");
        try
        {
            var line = await CanonicalLine.ResolveAsync(root, repository, null, DriverConfig.Load(DriverConfig.ResolvePath()), ct).ConfigureAwait(false);
            var commit = await FileGit.CommitAsync(root, line.Branch ?? "HEAD", ct).ConfigureAwait(false);
            switch (verb)
            {
                case null:
                    break;
                case "read":
                    var reading = await FileAnalysis.ReadAsync(root, commit, FileSignatures.Shipped, [], ct).ConfigureAwait(false);
                    FoundFiles.Write(home, repository, reading.Found);
                    output.WriteLine($"daoris: `{repository}` read again at {Short(commit)}: {reading.Found.Rules.Count} found rules written.");
                    break;
                case "set" when words.Count >= 2 && FileKinds.Of(words[0]) is { } kind:
                    var copy = words.Remove("--copy") ? true : words.Remove("--no-copy") ? false : (bool?)null;
                    var rule = new FileRule(kind, [.. words.Skip(1)]) { Copy = copy };
                    if (rule.Paths.Count == 0) throw new DriverException("`set` takes a kind and at least one path.");
                    if (rule.Paths.Select(RoomPaths.Unfit).FirstOrDefault(why => why is not null) is { } unfit) throw new DriverException($"a path {unfit}.");
                    if (copy is not null && kind != FileKind.Local) throw new DriverException("only a `local` rule takes --copy or --no-copy.");
                    PersonFiles.Set(home, machine ? null : repository, rule);
                    output.WriteLine($"daoris: {(machine ? "this machine's" : $"`{repository}`'s")} rules end with {kind.Word()} {string.Join(", ", rule.Paths)}.");
                    break;
                case "remove" when words.Count >= 1:
                    var removed = words.Sum(path => PersonFiles.Remove(home, machine ? null : repository, path));
                    output.WriteLine($"daoris: {removed} of your rules named {string.Join(", ", words)}; {(removed == 0 ? "nothing was written" : "each lost it")}.");
                    break;
                default:
                    error.WriteLine(Usage);
                    return 2;
            }

            foreach (var said in await SaidAsync(home, root, repository, commit, ct).ConfigureAwait(false)) output.WriteLine(said);
            return 0;
        }
        catch (DriverException refused)
        {
            error.WriteLine($"daoris: {refused.Message}");
            return 1;
        }
    }

    /// <summary>Every layer read for <paramref name="repository"/>, and each path's effective rule in a line of its own.</summary>
    internal static async Task<IReadOnlyList<string>> SaidAsync(string home, string root, string repository, string commit, CancellationToken ct)
    {
        var reading = await FileAnalysis.ReadAsync(root, commit, FileSignatures.Shipped, [], ct).ConfigureAwait(false);
        var found = FoundFiles.Read(home, repository);
        var own = await RepositoryFilesReader.AtAsync(root, commit, ct).ConfigureAwait(false);
        var person = PersonFiles.Read(home);
        var rules = FileRules.Of(found.Reading?.Rules ?? reading.Found.Rules, own.Rules, person, repository, reading.Signature);
        var paths = reading.Tops.Concat(rules.Rules.SelectMany(each => each.Rule.Paths).Where(path => path.IndexOfAny(['*', '?', '{']) < 0))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

        List<string> said =
        [
            found.Reading is { } last
                ? $"`{repository}` at {Short(commit)}, its found layer read at {Short(last.Commit)}:"
                : $"`{repository}` at {Short(commit)}, never read: the found rules below are this reading's, unwritten (`daoris files {repository} read`):",
        ];
        said.AddRange(rules.Over(paths, reading.Floor).Select(Line));
        if (person.Unread is { } why) said.Add($"Your {PersonFiles.FileName} does not read ({why}): every run, copy and share is held as an ask until it does.");
        said.AddRange(found.Problems.Concat(own.Problems).Concat(person.Problems).Select(problem => $"Refused: {problem}"));
        return said;
    }

    private static string Line(EffectiveFile file)
    {
        var parts = new List<string> { file.Kind?.Word() ?? "git's", file.Layer is { } layer ? $"by {Layer(layer)}" : "named by no rule" };
        if (file.Floor is { } floor) parts.Add(floor);
        if (file.Copy) parts.Add("copied into each tree");
        if (file.Idle is { } idle) parts.Add(idle);
        if (file.Rule?.Size is { } size) parts.Add($"{(size.More ? "more than " : "")}{size.Files} files, {size.Bytes / 1024} KB");
        if (file.Rule?.Evidence is { Count: > 0 } evidence) parts.Add(string.Join("; ", evidence));
        if (file.Below.Count > 0) parts.Add("below: " + string.Join(", ", file.Below.Select(each => $"{Layer(each.Layer)} {each.Rule.Kind.Word()}")));
        return $"  {file.Path}  {string.Join(" · ", parts)}";
    }

    private static string Layer(FileLayer layer) => layer switch
    {
        FileLayer.Found => "the analysis",
        FileLayer.Repository => "the repository",
        FileLayer.Machine => "your machine's rules",
        FileLayer.Person => "your rules",
        _ => "the floor",
    };

    private static string Short(string commit) => commit.Length > 10 ? commit[..10] : commit;
}
