namespace Daoris.Driver;

/// <summary>The rule file's layers in the order they are read, a match in a later one winning; the floor is read after all of them.</summary>
internal enum FileLayer
{
    Found,
    Repository,
    Machine,
    Person,
    Floor,
}

/// <summary>A rule as one layer said it.</summary>
internal sealed record LayerSaid(FileLayer Layer, FileRule Rule);

/// <summary>
/// A path's effective rule: its kind (null where git tracks it) and the layer that decided, the rule with the fields it left out taken
/// from its signature, and every earlier match below it. <see cref="Floor"/> says where the floor decided, <see cref="Copy"/> whether
/// a copy is made, and <see cref="Idle"/> why a dependency acts on nothing.
/// </summary>
internal sealed record EffectiveFile(string Path, FileKind? Kind, FileLayer? Layer, FileRule? Rule, IReadOnlyList<LayerSaid> Below)
{
    internal string? Floor { get; init; }

    internal bool Copy { get; init; }

    internal string? Idle { get; init; }
}

/// <summary>
/// The rule file read whole for one repository (the tree-pool design §29.2): the found layer, the repository's own, the person's
/// machine rules and then their repository's, each read in order with the last match deciding, a later layer's match winning over
/// every earlier one, and the floor after all of them. The person's permissions of both scopes answer Daoris's acts; a person's file
/// that does not read holds every act as an ask no allow answers. Nothing here acts: it says what the rules say.
/// </summary>
internal sealed class FileRules
{
    private readonly IReadOnlyList<LayerSaid> _rules;
    private readonly FilePermissions _permissions;
    private readonly Func<string, FileKind, FileRule?>? _signature;

    private FileRules(IReadOnlyList<LayerSaid> rules, FilePermissions permissions, string? unread, Func<string, FileKind, FileRule?>? signature)
    {
        _rules = rules;
        _permissions = permissions;
        Unread = unread;
        _signature = signature;
    }

    /// <summary>Why the person's file does not read, or null.</summary>
    internal string? Unread { get; }

    internal IReadOnlyList<LayerSaid> Rules => _rules;

    /// <param name="signature">A toolchain signature's rule for a path and kind, whose fields fill what the winning rule leaves out.</param>
    internal static FileRules Of(
        IReadOnlyList<FileRule> found, IReadOnlyList<FileRule> repository, PersonFilesRead person, string name,
        Func<string, FileKind, FileRule?>? signature = null)
    {
        var own = person.For(name);
        IReadOnlyList<LayerSaid> rules =
        [
            .. found.Select(rule => new LayerSaid(FileLayer.Found, rule)),
            .. repository.Select(rule => new LayerSaid(FileLayer.Repository, rule)),
            .. person.Machine.Rules.Select(rule => new LayerSaid(FileLayer.Machine, rule)),
            .. own.Rules.Select(rule => new LayerSaid(FileLayer.Person, rule)),
        ];
        return new FileRules(rules, person.Machine.Permissions.With(own.Permissions), person.Unread, signature);
    }

    /// <summary>What the rules and the floor say of <paramref name="path"/>, an ignored top or a path a rule names.</summary>
    internal EffectiveFile For(string path, FileFloorFacts floor)
    {
        var said = _rules.Where(each => each.Rule.Matches(path)).ToList();
        if (FileFloor.TrackedIn(path, floor.Tracked) is not null) return new(path, null, FileLayer.Floor, null, said) { Floor = FileFloor.Tracked };
        if (FileFloor.Held(path, floor.Repositories)) return new(path, FileKind.Repository, FileLayer.Floor, null, said) { Floor = FileFloor.Repository };
        if (said.Count == 0) return new(path, FileKind.Unknown, null, null, []);

        var winner = said[^1];
        var rule = winner.Rule.Filled(_signature?.Invoke(path, winner.Rule.Kind));
        var copy = rule.Kind == FileKind.Local && rule.Copy == true;
        var settings = copy && FileFloor.IsSettings(path);
        return new(path, rule.Kind, winner.Layer, rule, said[..^1])
        {
            Copy = copy && !settings,
            Floor = settings ? FileFloor.NeverCopied : null,
            Idle = rule.Kind == FileKind.Dependency && (rule.Install is null || rule.Key is null)
                ? "it names and inherits no install and no key, so it acts on nothing"
                : null,
        };
    }

    internal IReadOnlyList<EffectiveFile> Over(IEnumerable<string> paths, FileFloorFacts floor) => [.. paths.Select(path => For(path, floor))];

    /// <summary>The person's answer for one act (<see cref="FileActs"/>); every act is held while their file does not read.</summary>
    internal FileVerdict Judge(string act) => Unread is not null ? FileVerdict.Held : _permissions.Judge(act);

    /// <summary>Whether the ask that holds <paramref name="act"/> names the person (<c>run(…)@person</c>): no supervisor's allow answers it.</summary>
    internal bool NamesPerson(string act) => _permissions.AskNamesPerson(act);
}
