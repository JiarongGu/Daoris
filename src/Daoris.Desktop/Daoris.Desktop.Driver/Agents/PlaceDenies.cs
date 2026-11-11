namespace Daoris.Driver;

/// <summary>
/// What a session working in its place is handed where its start found uncommitted paths that are not its own: a deny for each
/// spelling of the git verbs that would take them with its own (adding everything, committing everything, stashing), as
/// <c>no-push</c> takes push's, the option-first forms among them. A harness that takes no rules is told only (<see cref="PlacePrompt"/>).
/// </summary>
internal static class PlaceDenies
{
    public static IReadOnlyList<string> Rules { get; } =
    [
        "Bash(git add -A)", "Bash(git add -A:*)", "Bash(git add --all)", "Bash(git add --all:*)", "Bash(git add .)", "Bash(git add . *)",
        "Bash(git commit -a)", "Bash(git commit -a:*)", "Bash(git commit -am:*)", "Bash(git commit --all)", "Bash(git commit --all:*)",
        "Bash(git stash)", "Bash(git stash:*)",
        "Bash(git -* add -A)", "Bash(git -* add -A *)", "Bash(git -* add --all)", "Bash(git -* add --all *)", "Bash(git -* add .)",
        "Bash(git -* add . *)", "Bash(git -* commit -a *)", "Bash(git -* commit -am *)", "Bash(git -* commit --all *)",
        "Bash(git -* stash)", "Bash(git -* stash *)",
    ];

    /// <summary><paramref name="composed"/> with the denies beside its own where <paramref name="others"/> names any path.</summary>
    public static RuleLists Compose(RuleLists composed, IReadOnlyList<string> others) =>
        others.Count == 0 ? composed : composed with { Deny = [.. composed.Deny.Union(Rules, StringComparer.Ordinal)] };
}
