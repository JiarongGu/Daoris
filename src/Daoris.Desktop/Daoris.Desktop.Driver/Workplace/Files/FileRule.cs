using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>
/// One entry of the rule file, in every layer's one format: its kind, the patterns it covers in git's ignore syntax, and the rule
/// set its kind names (<see cref="FileKindSets"/>), each field null where the entry leaves it out. The found layer adds what the
/// analysis saw: its evidence, the kind another source said (<see cref="Other"/>) and the size the bounded walk read.
/// </summary>
/// <remarks>Its lists are frozen, since a parse is kept by its content and shared; a default array, which throws when enumerated, is refused.</remarks>
/// <param name="Paths">Git's ignore patterns, read by <see cref="GitPattern"/>; a first-form entry's paths as that form wrote them.</param>
internal sealed record FileRule(FileKind Kind, ImmutableArray<string> Paths)
{
    private readonly ImmutableArray<string> _paths = Made(Paths, nameof(Paths));
    private readonly ImmutableArray<string>? _setup;
    private readonly ImmutableArray<string>? _key;
    private readonly ImmutableArray<string>? _toolchain;
    private readonly ImmutableArray<string>? _env;
    private readonly ImmutableArray<string>? _secret;
    private readonly ImmutableArray<string>? _caches;
    private readonly ImmutableArray<string>? _refuses;
    private readonly ImmutableArray<string> _evidence = [];

    public ImmutableArray<string> Paths { get => _paths; init => _paths = Made(value, nameof(Paths)); }

    /// <summary>Whether the paths are first-form paths no git pattern states, read by the first form's own matcher.</summary>
    internal bool FirstForm { get; init; }

    /// <summary>How the paths reach a tree: <c>tree</c>, or <c>copy</c> from the checkout, read-only.</summary>
    internal string? Reach { get; init; }

    /// <summary>What a take does with the paths (<see cref="FileKindSets.Takes"/>).</summary>
    internal string? Take { get; init; }

    internal string? Finish { get; init; }

    /// <summary>A dependency's install, run in the folder of the manifest beside its first path.</summary>
    internal string? Install { get; init; }

    /// <summary>The repository's own commands a dependency's install leaves out, run after the installs in an unworked tree.</summary>
    internal ImmutableArray<string>? Setup { get => _setup; init => _setup = Made(value, nameof(Setup)); }

    /// <summary>How often <see cref="Setup"/> runs: <c>repository</c> once per recorded key, <c>tree</c> in each tree.</summary>
    internal string? Per { get; init; }

    /// <summary>The tracked paths and globs that key a dependency; an empty list keys it by the commit's tree.</summary>
    internal ImmutableArray<string>? Key { get => _key; init => _key = Made(value, nameof(Key)); }

    /// <summary>The commands whose output keys a dependency, or a tree's build state for an intermediate or a cache.</summary>
    internal ImmutableArray<string>? Toolchain { get => _toolchain; init => _toolchain = Made(value, nameof(Toolchain)); }

    /// <summary>The names of the variables a dependency's install is given, never their values.</summary>
    internal ImmutableArray<string>? Env { get => _env; init => _env = Made(value, nameof(Env)); }

    /// <summary>The names of <see cref="Env"/> whose values are also scanned for in what the install wrote.</summary>
    internal ImmutableArray<string>? Secret { get => _secret; init => _secret = Made(value, nameof(Secret)); }

    /// <summary>Folders inside a dependency that its tools write and check.</summary>
    internal ImmutableArray<string>? Caches { get => _caches; init => _caches = Made(value, nameof(Caches)); }

    /// <summary>A dependency installed in each tree and never stored.</summary>
    internal bool? Bound { get; init; }

    /// <summary>A dependency's store entry shared across repositories.</summary>
    internal bool? Across { get; init; }

    /// <summary>An output's one build command, run by the door in its project's folder for each configuration it names as <c>{configuration}</c>.</summary>
    internal string? Builds { get; init; }

    /// <summary>The runners the repository integrated with a base's guard, named in the start's words; nothing here enforces them.</summary>
    internal ImmutableArray<string>? Refuses { get => _refuses; init => _refuses = Made(value, nameof(Refuses)); }

    internal ImmutableArray<string> Evidence { get => _evidence; init => _evidence = Made(value, nameof(Evidence)); }

    /// <summary>The kind a weaker source said, shown beside the one written.</summary>
    internal FileKind? Other { get; init; }

    internal FileSize? Size { get; init; }

    internal bool Matches(string path, bool ignoreCase = false) => GitPattern.Covers(this, path, ignoreCase);

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
        Across = Across ?? signature.Across,
        Reach = Reach ?? signature.Reach,
        Take = Take ?? signature.Take,
        Finish = Finish ?? signature.Finish,
        Builds = Builds ?? signature.Builds,
        Refuses = Refuses ?? signature.Refuses,
    };

    private static ImmutableArray<string> Made(ImmutableArray<string> list, string field) =>
        list.IsDefault ? throw new ArgumentException($"a rule's {field} is a default array, which holds nothing to read", field) : list;

    private static ImmutableArray<string>? Made(ImmutableArray<string>? list, string field) => list is { } held ? Made(held, field) : null;
}

/// <summary>What a bounded walk counted under a path: its files and bytes, and whether it stopped at its bound first.</summary>
internal sealed record FileSize(long Files, long Bytes, bool More);
