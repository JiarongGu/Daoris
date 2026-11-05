namespace Daoris.Driver;

/// <summary>
/// One ref a grant carries (D241 point 3): the ref's name, the commit the push moves it to, and where the person's act knows it, the commit
/// the ref stands at. A grant is bound to its act, so it carries no ref but the ones its act names.
/// </summary>
/// <param name="Ref">The full ref name, <c>refs/heads/work/fix</c>.</param>
/// <param name="New">The commit the push puts the ref at.</param>
/// <param name="Old">
/// The commit the ref stands at on the remote, for an act that knows it (the push act, <c>GitPushActs</c>), the zero id where the
/// remote has no such ref. Null for the landing, which knows only the commit it pushes: the grant then carries a creation or a fast-forward
/// to <paramref name="New"/> and never a force, a delete or any other Dangerous kind. With it, the grant names that exact update alone.
/// </param>
/// <remarks>The base library alone: the doors compile it in (<c>Daoris.Desktop.GitDoor</c>).</remarks>
public sealed record GitGrantRef(string Ref, string New, string? Old = null)
{
    /// <summary>Whether this ref names <paramref name="update"/>: its ref and new commit, and its old commit where bound.</summary>
    public bool Names(GitPushUpdate update) =>
        string.Equals(Ref, update.Ref, StringComparison.Ordinal)
        && string.Equals(New, update.New, StringComparison.OrdinalIgnoreCase)
        && (Old is null || string.Equals(Old, update.Old, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether this ref carries <paramref name="update"/>, judged to be of <paramref name="kind"/>, past the place's Dangerous value: a plain
    /// update it names, or with the old commit bound any kind it names but a tag, which the place's Dangerous value always decides.
    /// </summary>
    public bool Carries(GitPushUpdate update, GitPushKind kind) =>
        Names(update) && kind != GitPushKind.Tag && (Old is not null || kind == GitPushKind.Plain);
}
