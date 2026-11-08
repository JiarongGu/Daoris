namespace Daoris.Driver;

/// <summary>
/// One rule of the rule file, in every layer's one format: its kind, the paths it covers in the gates' path grammar, and the fields
/// its kind takes, each null where the rule leaves it out. The found layer adds what the analysis saw: its evidence, the kind another
/// source said (<see cref="Other"/>) and the size the bounded walk read.
/// </summary>
/// <param name="Paths">Repository-relative, <c>/</c>, globs, a folder ending in <c>/</c>.</param>
internal sealed record FileRule(FileKind Kind, IReadOnlyList<string> Paths)
{
    /// <summary>A dependency's install, run in the folder of the manifest beside its first path.</summary>
    internal string? Install { get; init; }

    /// <summary>The repository's own commands a dependency's install leaves out, run after the installs in an unworked tree.</summary>
    internal IReadOnlyList<string>? Setup { get; init; }

    /// <summary>How often <see cref="Setup"/> runs: <c>repository</c> once per recorded key, <c>tree</c> in each tree.</summary>
    internal string? Per { get; init; }

    /// <summary>The tracked paths and globs that key a dependency; an empty list keys it by the commit's tree.</summary>
    internal IReadOnlyList<string>? Key { get; init; }

    /// <summary>The commands whose output keys a dependency, or a tree's build state for an intermediate or a cache.</summary>
    internal IReadOnlyList<string>? Toolchain { get; init; }

    /// <summary>The names of the variables a dependency's install is given, never their values.</summary>
    internal IReadOnlyList<string>? Env { get; init; }

    /// <summary>The names of <see cref="Env"/> whose values are also scanned for in what the install wrote.</summary>
    internal IReadOnlyList<string>? Secret { get; init; }

    /// <summary>Folders inside a dependency that its tools write and check.</summary>
    internal IReadOnlyList<string>? Caches { get; init; }

    /// <summary>A dependency installed in each tree and never stored.</summary>
    internal bool? Bound { get; init; }

    /// <summary>A dependency's store entry shared across repositories.</summary>
    internal bool? Share { get; init; }

    /// <summary>A local file copied into each tree read-only from the checkout.</summary>
    internal bool? Copy { get; init; }

    internal IReadOnlyList<string> Evidence { get; init; } = [];

    /// <summary>The kind a weaker source said, shown beside the one written.</summary>
    internal FileKind? Other { get; init; }

    internal FileSize? Size { get; init; }

    internal bool Matches(string path) => Paths.Any(each => RoomPaths.Declared(each).IsMatch(path));

    /// <summary>Each field this rule leaves out taken from <paramref name="signature"/>, a toolchain signature's rule of the same kind.</summary>
    internal FileRule Filled(FileRule? signature) => signature is null || signature.Kind != Kind ? this : this with
    {
        Install = Install ?? signature.Install,
        Setup = Setup ?? signature.Setup,
        Per = Per ?? signature.Per,
        Key = Key ?? signature.Key,
        Toolchain = Toolchain ?? signature.Toolchain,
        Env = Env ?? signature.Env,
        Secret = Secret ?? signature.Secret,
        Caches = Caches ?? signature.Caches,
        Bound = Bound ?? signature.Bound,
        Share = Share ?? signature.Share,
        Copy = Copy ?? signature.Copy,
    };
}

/// <summary>What a bounded walk counted under a path: its files and bytes, and whether it stopped at its bound first.</summary>
internal sealed record FileSize(long Files, long Bytes, bool More);
