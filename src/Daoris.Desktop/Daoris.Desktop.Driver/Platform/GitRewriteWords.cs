namespace Daoris.Driver;

/// <summary>
/// What a session is told about the <c>daoris::</c> rewrite in its Git section (GITAUTH1b, the git-auth design §4.4 and §5.3): what
/// <c>git remote -v</c> shows for an HTTP or HTTPS remote under Daoris's doors, what to name a tool that reads remotes with, and that
/// <c>gh</c> and <c>az</c> act as the person's own sign-ins (§10). Words alone: it reads no address, and is the one file besides the
/// reader that names that command (<c>GitRemotesTests</c>' scan excuses it for that).
/// </summary>
public static class GitRewriteWords
{
    /// <summary>The words, with <paramref name="origin"/>'s address as the example where it is an HTTP or HTTPS one.</summary>
    public static string Told(string? origin)
    {
        var address = GitWords.Redact(origin?.Trim());
        var http = address is not null && (address.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || address.StartsWith("http://", StringComparison.OrdinalIgnoreCase));
        var example = http
            ? $" `origin` shows as `{GitRouting.Prefix}{address}`, and its address is `{address}`."
            : "";
        return $"Daoris's git door stands in front of this repository's HTTP and HTTPS remotes, so `git remote -v` shows each as "
            + $"`{GitRouting.Prefix}` followed by its address.{example} `{GitRouting.Prefix}` is the door, not part of the address: a "
            + "tool that reads remotes cannot parse it, so name the repository to it (`gh --repo <owner>/<name>`; `az repos` with "
            + "`--organization`, `--project`, `--repository` and `--detect false`) rather than letting it read the remote, and never "
            + "rewrite the remote to take the prefix off. `gh` and `az` here act as the person's own sign-ins and are not Daoris's git: "
            + "do not use them to push or to change the remote.";
    }
}
