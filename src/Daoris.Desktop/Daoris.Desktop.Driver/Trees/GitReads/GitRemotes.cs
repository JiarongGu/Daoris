namespace Daoris.Driver;

/// <summary>
/// The one reader of a remote's address (GITAUTH1b, D198 point 2; the git-auth design §4.4). Under Daoris's wiring git applies the
/// rewrite wherever it hands out an address, so <c>git remote get-url</c> answers <c>daoris::https://…</c> for every HTTP and HTTPS
/// remote; this asks git for the effective address and takes Daoris's own prefix off it, exactly once and nothing else, so the person's
/// own rewrites stay applied. Every address Daoris keeps, says or judges goes through it: <c>GitRemotesTests</c> holds that no
/// other code reads <c>remote get-url</c> or <c>remote -v</c>.
/// </summary>
public static class GitRemotes
{
    /// <summary>
    /// The effective address of <paramref name="remote"/> in the checkout at <paramref name="root"/>, its push address where
    /// <paramref name="push"/> asks it; null where git names no such remote, or there is no checkout there.
    /// </summary>
    public static async Task<string?> AddressOf(string root, string remote, bool push = false, CancellationToken ct = default)
    {
        if (!Directory.Exists(root)) return null;
        string[] arguments = push ? ["remote", "get-url", "--push", remote] : ["remote", "get-url", remote];
        var (code, said, _) = await WorkingTree.GitAsync(root, arguments, ct).ConfigureAwait(false);
        return code == 0 ? Unprefixed(said.Trim()) : null;
    }

    /// <summary>An address with Daoris's own prefix taken off, once; any other address as it is.</summary>
    public static string Unprefixed(string address) =>
        address.StartsWith(GitRouting.Prefix, StringComparison.Ordinal) ? address[GitRouting.Prefix.Length..] : address;
}
