namespace Daoris.Driver;

/// <summary>A reason the files stage says of a dependency: its code (<see cref="DepsWhy"/>), its English clause, and the values its entry says.</summary>
internal sealed record DepsReason(string Code, string Text, IReadOnlyList<(string Name, object? Value)> Values)
{
    internal static DepsReason Of(string code, string text, params (string Name, object? Value)[] values) => new(code, text, values);

    /// <summary>The line's values: the reason's code as its <c>why</c>, then the reason's own.</summary>
    internal (string Name, object? Value)[] With(params (string Name, object? Value)[] line) => [.. line, ("why", Code), .. Values];
}

/// <summary>
/// The reasons a <c>started.deps-*</c> line names (<see cref="NoteCodes.Deps"/>), each worded in both catalogues as
/// <c>note.deps.why.*</c>, so the clause is the page's language and not the driver's.
/// </summary>
internal static class DepsWhy
{
    internal const string Changed = "changed";
    internal const string Companion = "companion";
    internal const string Unplaced = "unplaced";
    internal const string KeyMoved = "key-moved";
    internal const string BeforeOwn = "before-own";
    internal const string InputChanged = "input-changed";
    internal const string Gone = "gone";
    internal const string Grouped = "grouped";
    internal const string Denied = "denied";
    internal const string Held = "held";
    internal const string Failed = "failed";
    internal const string Tracked = "tracked";
    internal const string Bound = "bound";
    internal const string NamesTree = "names-tree";
    internal const string Secret = "secret";
    internal const string Beside = "beside";
    internal const string LinkOut = "link-out";
    internal const string HeldBefore = "held-ignored";
    internal const string Group = "group";
    internal const string Unsealed = "unsealed";

    private const string Yours = ", and this tree is its line's to install in";

    internal static DepsReason ChangedIt() => DepsReason.Of(Changed, "its work changed it");

    internal static DepsReason CompanionChanged(string file) => DepsReason.Of(Companion, $"`{file}`, which its install wrote beside it, changed", ("file", file));

    internal static DepsReason NotPlaced() => DepsReason.Of(Unplaced, "Daoris did not place it");

    internal static DepsReason KeyMovedSince() => DepsReason.Of(KeyMoved, "the files that key it moved since it was installed" + Yours);

    internal static DepsReason BeforeIsOwn(string before) => DepsReason.Of(BeforeOwn, $"it was installed against `{before}`, which is this line's own here", ("before", before));

    internal static DepsReason InputChangedHere() => DepsReason.Of(InputChanged, "this line changed a file that keys it" + Yours);

    internal static DepsReason FolderGone() => DepsReason.Of(Gone, "its folder is gone" + Yours);

    internal static DepsReason GroupedWith(string with) => DepsReason.Of(Grouped, $"it was installed as one group with `{with}`, which no longer holds as one" + Yours, ("with", with));

    internal static DepsReason DeniedBy(string install) => DepsReason.Of(Denied, $"a restriction set for it denies `{install}`", ("install", install));

    internal static DepsReason HeldFor(string install) => DepsReason.Of(Held, $"`{install}` waits for an allow", ("install", install));

    internal static DepsReason FailedSaying(string install, string tail) => DepsReason.Of(Failed, $"`{install}` failed: {tail}", ("install", install), ("tail", tail));

    internal static DepsReason TrackedPutBack(IReadOnlyList<string> paths) => DepsReason.Of(
        Tracked, $"its install changed {string.Join(", ", paths.Select(path => $"`{path}`"))}, which git tracks or does not ignore, so Daoris put it back", ("file", paths[0]));

    internal static DepsReason BoundToTree() => DepsReason.Of(Bound, "its rule says it is bound to its tree");

    internal static DepsReason NamesItsTree() => DepsReason.Of(NamesTree, "it names the folder it was installed in");

    internal static DepsReason HoldsSecret() => DepsReason.Of(Secret, "it holds a value its rule marks secret");

    internal static DepsReason WroteBeside(string file) => DepsReason.Of(Beside, $"its install wrote `{file}` outside its folder", ("file", file));

    internal static DepsReason HeldIgnored(string file) => DepsReason.Of(HeldBefore, $"the tree held `{file}`, which git ignores, as its install began", ("file", file));

    internal static DepsReason LinksOut(string link) => DepsReason.Of(LinkOut, $"its link `{link}` leads outside the tree", ("link", link));

    internal static DepsReason SealFailed() => DepsReason.Of(Unsealed, "the store could not take a copy of it, so the next tree installs it again");

    internal static DepsReason GroupFormed(string with) => DepsReason.Of(Group, $"its install changed `{with}`, which another dependency placed, so the two are kept as one group", ("with", with));
}
