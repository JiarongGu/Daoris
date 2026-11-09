namespace Daoris.Driver;

/// <summary>
/// <c>daoris files &lt;repository&gt;</c>: each path git does not manage with its effective rule, the layer that decided and what
/// the layers below said, at its line's tip. Nothing in a tree is written; the three writes are the person's, refused from a session.
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
            var commit = await FilesLooks.CommitAsync(root, repository, DriverConfig.Load(DriverConfig.ResolvePath()), ct).ConfigureAwait(false);
            switch (verb)
            {
                case null:
                    break;
                case "read":
                    var written = await FilesLooks.ReadAgainAsync(home, root, repository, commit, ct).ConfigureAwait(false);
                    output.WriteLine($"daoris: `{repository}` read again at {Short(commit)}: {written} found rules written.");
                    break;
                case "set" when words.Count >= 2 && FileKinds.Of(words[0]) is not null:
                    var copy = words.Remove("--copy") ? true : words.Remove("--no-copy") ? false : (bool?)null;
                    var kind = FilesEdits.Set(home, repository, machine, words[0], [.. words.Skip(1)], copy);
                    output.WriteLine($"daoris: {(machine ? "this machine's" : $"`{repository}`'s")} rules end with {kind} {string.Join(", ", words.Skip(1))}.");
                    break;
                case "remove" when words.Count >= 1:
                    var removed = FilesEdits.Remove(home, repository, machine, words);
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
        var gathered = await FilesLooks.GatherAsync(home, root, repository, commit, ct).ConfigureAwait(false);
        var (found, reading, own, person) = (gathered.Found, gathered.Reading, gathered.Own, gathered.Person);
        var rules = gathered.Rules(repository);
        var paths = gathered.Paths(rules);

        List<string> said =
        [
            found.Reading is { } last
                ? $"`{repository}` at {Short(commit)}, its found layer read at {Short(last.Commit)}:"
                : $"`{repository}` at {Short(commit)}, never read: the found rules below are this reading's, unwritten (`daoris files {repository} read`):",
        ];
        said.AddRange(rules.Over(paths, reading.Floor).Select(Line));
        if (person.Unread is { } why) said.Add($"Your {DeclarationNames.PersonRules} does not read ({why}): every run, copy and share is held as an ask until it does.");
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
        if (file.Rule?.Evidence is { Length: > 0 } evidence) parts.Add(string.Join("; ", evidence));
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
