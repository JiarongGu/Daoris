namespace Daoris.Driver;

/// <summary>Every layer read for one repository, before any is composed: the found layer's last reading, a fresh reading, the repository's own layer and the person's.</summary>
internal sealed record FilesGathered(FoundFilesRead Found, FileReading Reading, RepositoryFilesRead Own, PersonFilesRead Person)
{
    internal FileRules Rules(string repository) =>
        FileRules.Of(Found.Reading?.Rules ?? Reading.Found.Rules, Own.Rules, Person, repository, Reading.Signature);

    /// <summary>The ignored tops and every path an entry names alone (<see cref="GitPattern.LiteralOf(FileRule, string)"/>), in order.</summary>
    internal IEnumerable<string> Paths(FileRules rules) =>
        Reading.Tops.Concat(rules.Rules.SelectMany(each => each.Rule.Paths.Select(path => GitPattern.LiteralOf(each.Rule, path))).OfType<string>())
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
}

/// <summary>
/// The rule file read for the page (row 6b) and the terminal's verb alike: <see cref="GatherAsync"/> reads the layers at a
/// commit, <see cref="Compose"/> says what they say. Only the person's layer and the found layer are written, and only by their own writers.
/// </summary>
public static class FilesLooks
{
    /// <summary>The commit the checkout's line stands at, the one the repository's own layer is read at.</summary>
    public static async Task<string> CommitAsync(string root, string repository, DriverConfig config, CancellationToken ct)
    {
        var line = await CanonicalLine.ResolveAsync(root, repository, null, config, ct).ConfigureAwait(false);
        return await FileGit.CommitAsync(root, line.Branch ?? "HEAD", ct).ConfigureAwait(false);
    }

    internal static async Task<FilesGathered> GatherAsync(string home, string root, string repository, string commit, CancellationToken ct)
    {
        var reading = await FileAnalysis.ReadAsync(root, commit, FileSignatures.Shipped, [], ct).ConfigureAwait(false);
        var own = await RepositoryFilesReader.AtAsync(root, commit, ct).ConfigureAwait(false);
        return new FilesGathered(FoundFiles.Read(home, repository), reading, own, PersonFiles.Read(home));
    }

    /// <summary>The repository read at <paramref name="commit"/> and composed for the page.</summary>
    public static async Task<FilesLook> ReadAsync(string home, string root, string repository, string commit, CancellationToken ct) =>
        Compose(repository, commit, await GatherAsync(home, root, repository, commit, ct).ConfigureAwait(false));

    /// <summary>The repository read again: the found layer replaced whole, as the analysis writes it; how many found rules it wrote.</summary>
    public static async Task<int> ReadAgainAsync(string home, string root, string repository, string commit, CancellationToken ct)
    {
        var reading = await FileAnalysis.ReadAsync(root, commit, FileSignatures.Shipped, [], ct).ConfigureAwait(false);
        FoundFiles.Write(home, repository, reading.Found);
        return reading.Found.Rules.Count;
    }

    internal static FilesLook Compose(string repository, string commit, FilesGathered gathered)
    {
        var rules = gathered.Rules(repository);
        var files = rules.Over(gathered.Paths(rules), gathered.Reading.Floor);
        var person = gathered.Person;
        var last = gathered.Found.Reading;
        return new FilesLook(
            repository, Short(commit), last is null ? null : Short(last.Commit), last?.At,
            [.. files.Select(File)],
            [.. files.SelectMany(file => Acts(rules, file))],
            [Scope("machine", person.Machine), Scope("repository", person.For(repository))],
            [.. Own("machine", person.Machine), .. Own("repository", person.For(repository))],
            person.Unread,
            [.. gathered.Found.Problems.Concat(gathered.Own.Problems).Concat(person.Problems)])
        {
            Kept = [.. Kept(person)],
            FirstForm = gathered.Own.FirstForm,
        };
    }

    // The analysis counted the path, so its size and its evidence stay beside a later layer's rule that took the path over.
    private static FilesLookFile File(EffectiveFile file)
    {
        var said = file.Below.Select(each => each.Rule).Reverse().Prepend(file.Rule).OfType<FileRule>().ToList();
        var size = said.Select(rule => rule.Size).FirstOrDefault(found => found is not null);
        var evidence = said.Select(rule => rule.Evidence).FirstOrDefault(found => found.Count > 0) ?? [];
        return new FilesLookFile(
            file.Path, file.Kind?.Word(), file.Layer is { } layer ? Word(layer) : null, FloorCode(file.Floor), file.Copy, file.Idle is null ? null : "no-install",
            size?.Files, size?.Bytes, size?.More ?? false, evidence,
            [.. file.Below.Select(each => new FilesLookBelow(Word(each.Layer), each.Rule.Kind.Word()))]);
    }

    // The floor's sentences are the terminal's; the page words each by its code.
    private static string? FloorCode(string? floor) => floor switch
    {
        FileFloor.Tracked => "tracked",
        FileFloor.Repository => "repository",
        FileFloor.NeverCopied => "never-copied",
        _ => null,
    };

    private static IEnumerable<FilesLookAct> Acts(FileRules rules, EffectiveFile file)
    {
        if (file.Rule is not { } rule) yield break;
        if (file.Kind == FileKind.Dependency && file.Idle is null && rule.Install is { } install) yield return Act(rules, FileActs.Run(install), file.Path);
        if (file.Copy) yield return Act(rules, FileActs.Copy(file.Path), file.Path);
        if (file.Kind == FileKind.Dependency && rule.Across == true) yield return Act(rules, FileActs.Share(file.Path), file.Path);
    }

    private static FilesLookAct Act(FileRules rules, string act, string path) => new(act, path, rules.Judge(act) switch
    {
        FileVerdict.Denied => "denied",
        FileVerdict.Held => "held",
        _ => "made",
    });

    private static FilesLookPermissions Scope(string name, FileScope scope) => new(
        name, scope.Permissions.Deny, scope.Permissions.Ask,
        [.. scope.Permissions.Allow.Select(rule => new FilesLookAllow(rule, scope.Permissions.Makers.GetValueOrDefault(rule)))]);

    private static IEnumerable<FilesLookRule> Own(string name, FileScope scope) =>
        scope.Rules.Where(rule => !rule.FirstForm).Select(rule => new FilesLookRule(name, rule.Kind.Word(), rule.Paths, rule.Reach is null ? null : FileKindSets.Copies(rule)));

    /// <summary>Every path of the person's file read as written, in every scope, so any repository's page settles them all in one write.</summary>
    private static IEnumerable<FilesLookKept> Kept(PersonFilesRead person) =>
        new[] { ("machine", person.Machine) }.Concat(person.Repositories.Select(pair => (pair.Key, pair.Value)))
            .SelectMany(scope => scope.Item2.Rules.Where(rule => rule.FirstForm).SelectMany(rule => rule.Paths.Where(RuleFileFirstForm.Keeps).Select(path => Kept(scope.Item1, rule, path))));

    private static FilesLookKept Kept(string scope, FileRule rule, string path)
    {
        var (whole, files) = RuleFileFirstForm.Wordings(path);
        return new FilesLookKept(scope, rule.Kind.Word(), path, whole, files);
    }

    private static string Word(FileLayer layer) => layer.ToString().ToLowerInvariant();

    private static string Short(string commit) => commit.Length > 10 ? commit[..10] : commit;
}
