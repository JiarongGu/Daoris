namespace Daoris.Driver;

/// <summary>
/// The door's third name, <c>git-credential-daoris</c>: git's credential helper for an identity's
/// HTTPS act, which the remote helper names for its inner transport alone. On <c>get</c> it answers the token of the identity the
/// helper named (<see cref="GitRouting.IdentityVariable"/>) and only for that identity's own host; any other host, no identity, no
/// token or a way that is not HTTPS gets nothing, said by an empty answer and exit 0 as git's helpers say it. <c>store</c> and
/// <c>erase</c> change nothing: Daoris keeps a token only through its own doors. The base library alone: the door compiles it in.
/// </summary>
public static class GitCredentialDoor
{
    /// <summary>The helper's name git runs it by (<c>credential.helper=daoris</c>).</summary>
    public const string Name = "daoris";

    /// <summary>The file the door is laid under, beside the other two.</summary>
    public const string Exe ="git-credential-daoris.exe";

    /// <summary>The folder the credential door lies in: beside the program running (the install's <c>app/bin/</c>), else where git's <c>PATH</c> finds it; null where it is not laid.</summary>
    public static string? FolderOf(Func<string, string?> variable)
    {
        var own = Path.GetDirectoryName(Environment.ProcessPath);
        if (own is not null && System.IO.File.Exists(Path.Combine(own, Exe))) return own;
        return GitDoors.Find("git-credential-daoris", variable("PATH")) is { } found ? Path.GetDirectoryName(found) : null;
    }

    /// <summary>Answer one call of git's credential protocol: <paramref name="arguments"/> is the verb.</summary>
    public static int Run(IReadOnlyList<string> arguments, Func<string, string?> variable, TextReader input, TextWriter output, TextWriter error)
    {
        if (arguments.Count == 0)
        {
            error.WriteLine("git-credential-daoris: takes a credential verb, as git passes it.");
            return 128;
        }

        if (arguments.Count != 1 || arguments[0] != "get") return 0;
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        while (input.ReadLine() is { Length: > 0 } line)
        {
            var at = line.IndexOf('=');
            if (at > 0) fields[line[..at]] = line[(at + 1)..];
        }

        if (Answer(variable, fields) is { } answer)
        {
            output.Write($"username={answer.User}\npassword={answer.Token}\n");
            output.Flush();
        }

        return 0;
    }

    /// <summary>The user and token for the request's own host, or null.</summary>
    public static (string User, string Token)? Answer(Func<string, string?> variable, IReadOnlyDictionary<string, string> fields)
    {
        if (variable(GitRouting.HomeVariable) is not { Length: > 0 } home || variable(GitRouting.IdentityVariable) is not { Length: > 0 } id) return null;
        if (!fields.TryGetValue("protocol", out var protocol) || protocol is not ("https" or "http")) return null;
        if (!fields.TryGetValue("host", out var host) || GitHttps.HostOf($"{protocol}://{host}") is not { } requested) return null;
        GitIdentity? identity;
        try
        {
            identity = GitIdentities.Find(home, id);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return null;
        }

        if (identity is null || identity.Way != GitIdentity.Https) return null;
        // The identity's host as it is kept: a scheme's default port is dropped there, as it is from the request's.
        if (!string.Equals(GitHttps.HostOf($"{protocol}://{identity.Host}"), requested, StringComparison.Ordinal)) return null;
        if (GitTokens.Read(home, id, GitTokens.Https) is not { } token) return null;
        fields.TryGetValue("username", out var asked);
        return (identity.User ?? (string.IsNullOrEmpty(asked) ? "daoris" : asked), token);
    }
}
