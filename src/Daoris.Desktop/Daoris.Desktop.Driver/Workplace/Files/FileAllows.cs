namespace Daoris.Driver;

/// <summary>What the start's sweep found of one supervisor's allow: the decision, and whether its allow stands in a layer of the person's file.</summary>
public sealed record FileAllowSwept(string Decision, string Text, bool Made);

/// <summary>
/// A supervisor's allow of an act an <c>ask</c> holds: judged against the person's file as it stands, written as the held act's own text
/// into each repository's layer with the role that gave it and the decision that kept it, and settled at the next start from what the
/// file holds. The write is the person's own (<see cref="PersonPermissions"/>), under the file's lock.
/// </summary>
public static class FileAllows
{
    /// <summary>Whether the person's permissions still hold <paramref name="ask"/>'s act for its repository, an allow of the act answering it no more.</summary>
    public static bool StillHeld(string home, FileAsk ask) =>
        FileRules.Of([], [], PersonFiles.Read(home), ask.Repository).Judge(ask.Text) == FileVerdict.Held;

    /// <summary>
    /// The allow written into each of <paramref name="ask"/>'s repositories' own layers, never the machine's, and never the <c>ask</c>'s
    /// pattern; how many layers took it (one already holding it took none).
    /// </summary>
    /// <exception cref="DriverException">The person's file stands and does not read.</exception>
    public static int Give(string home, FileAsk ask, string role, string decision) =>
        ask.Repositories.Count(repository => PersonPermissions.Add(home, repository, "allow", ask.Text, role, decision));

    /// <summary>
    /// Every ask a decision has begun on, settled from the file: made where an allow stands under that decision in one of its repositories'
    /// layers, else the decision lapses and the ask waits again.
    /// </summary>
    public static IReadOnlyList<FileAllowSwept> Sweep(string home)
    {
        var swept = new List<FileAllowSwept>();
        var person = PersonFiles.Read(home);
        foreach (var ask in FileAsks.Read(home).Where(each => each.Decision is not null))
        {
            var made = person.Unread is null && ask.Repositories.Any(repository => person.For(repository).Permissions.Decisions.GetValueOrDefault(ask.Text) == ask.Decision);
            FileAsks.Settle(home, ask.Decision!, made);
            swept.Add(new FileAllowSwept(ask.Decision!, ask.Text, made));
        }

        return swept;
    }
}
