namespace Daoris.Driver;

/// <summary>
/// One ref a grant carries (D241 point 3): the ref's name, the commit the push moves it to, and where the person's act knows it, the commit
/// the ref stands at. A grant is bound to its act, so it carries no ref but the ones its act names.
/// </summary>
/// <param name="Ref">The full ref name, <c>refs/heads/work/fix</c>.</param>
/// <param name="New">The commit the push puts the ref at.</param>
/// <param name="Old">
/// The commit the ref stands at on the remote, for an act that knows it (a press). Null for the landing, which knows only the commit it
/// pushes: the grant then carries a creation or a fast-forward to <paramref name="New"/> and never a force, a delete or any other
/// Dangerous kind. With it, the grant carries that exact update whatever its kind.
/// </param>
/// <remarks>The base library alone: the doors compile it in (<c>Daoris.Desktop.GitDoor</c>).</remarks>
public sealed record GitGrantRef(string Ref, string New, string? Old = null)
{
    /// <summary>Whether this ref carries <paramref name="update"/>, judged to be of <paramref name="kind"/>.</summary>
    public bool Carries(GitPushUpdate update, GitPushKind kind) =>
        string.Equals(Ref, update.Ref, StringComparison.Ordinal)
        && string.Equals(New, update.New, StringComparison.OrdinalIgnoreCase)
        && (Old is null ? kind == GitPushKind.Plain : string.Equals(Old, update.Old, StringComparison.OrdinalIgnoreCase));
}
