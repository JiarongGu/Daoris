namespace Daoris.Driver;

/// <summary>
/// An account made before it is signed in to, both doors' (<c>daoris agent account add</c>, the bridge's <c>profile-add</c>): a folder
/// under a drawn id (<see cref="AccountNames.NewId"/>), and the word the person gave it as its name, never as its id.
/// </summary>
public static class AccountAdd
{
    /// <summary>
    /// The account <paramref name="name"/> names, made where none does: one already called so, by its name or its id, is the answer, as
    /// asking for one that is there always was. The name is judged before anything is made (<see cref="AccountNames.Problem"/>).
    /// </summary>
    /// <returns>The account's id.</returns>
    /// <exception cref="AccountNameException">The name is refused; nothing was made.</exception>
    public static string Make(string home, string agent, string? name)
    {
        var accounts = HarnessSettings.Profiles(home, agent);
        var names = AccountNames.Of(home, agent);
        if (name?.Trim() is { Length: > 0 } wanted && AccountNames.Resolve(accounts, names, wanted) is { } there) return there;

        var id = AccountNames.NewId(home, agent);
        IReadOnlyCollection<string> after = [.. accounts, id];
        if (name is not null && AccountNames.Problem(after, names, id, name) is { } problem)
        {
            throw new AccountNameException(problem, AccountNames.Sentence(agent, problem, id, name, accounts, names));
        }

        Directory.CreateDirectory(HarnessSettings.ProfileHome(home, agent, id));
        if (name is not null) AccountNames.Rename(home, agent, after, id, name);
        return id;
    }
}
