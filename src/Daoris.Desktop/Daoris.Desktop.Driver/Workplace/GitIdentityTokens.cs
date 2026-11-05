namespace Daoris.Driver;

/// <summary>
/// An identity's tokens set and cleared (D241 point 6), called by each door of the identity (the terminal's <c>git identity token</c>,
/// the bridge's <c>GIT_IDENTITY</c>, the page): the identity's <c>api</c> token, which a hosted service's act is handed, on any
/// identity; and an HTTPS identity's <c>https</c> token, replaced when the host rotates it. The value goes in and never comes out:
/// the answer is whether one is set (<see cref="Has"/>).
/// </summary>
public static class GitIdentityTokens
{
    /// <summary>Keep a token of a kind for an identity named by id or label.</summary>
    /// <exception cref="DriverException">No such identity, a kind the identity's way does not keep, or a token that does not read.</exception>
    public static GitIdentity Set(string home, string name, string kind, string token)
    {
        var identity = Checked(home, name, kind);
        GitTokens.Set(home, identity.Id, kind, token);
        return identity;
    }

    /// <summary>Clear a token of a kind; an HTTPS identity's <c>https</c> token is cleared by removing the identity, not alone.</summary>
    /// <exception cref="DriverException">No such identity, or a kind the identity's way does not keep.</exception>
    public static GitIdentity Clear(string home, string name, string kind)
    {
        var identity = Checked(home, name, kind);
        if (kind == GitTokens.Https)
            throw new DriverException($"git: {identity.Label}'s HTTPS token is replaced with a new one, or the identity is removed (token-required)");
        GitTokens.Clear(home, identity.Id, kind);
        return identity;
    }

    /// <summary>Whether the identity holds a token of that kind: all a page or a verb is told.</summary>
    public static bool Has(string home, GitIdentity identity, string kind) => GitTokens.Has(home, identity.Id, kind);

    private static GitIdentity Checked(string home, string name, string kind)
    {
        if (!GitTokens.IsKind(kind)) throw new DriverException($"git: a token is {GitTokens.Https} or {GitTokens.Api} (kind)");
        var identity = GitIdentityActs.Find(home, name);
        if (kind == GitTokens.Https && identity.Way != GitIdentity.Https)
            throw new DriverException($"git: {identity.Label} is an SSH identity: it keeps an {GitTokens.Api} token, and its key carries git (kind)");
        return identity;
    }
}
