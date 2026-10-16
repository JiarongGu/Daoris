namespace Daoris.Driver;

/// <summary>
/// The name of the pipe the running Daoris answers on (RPC1a, D171 point 3, the terminal RPC design §4.1):
/// <c>daoris-&lt;the home's id&gt;-&lt;the Windows session's id&gt;</c>, so two homes' instances, and one user's two sessions,
/// never meet. The desktop serves it and writes it into <c>running.json</c> as <c>pipe</c> (<see cref="RunningAddress"/>); a
/// reader takes a name only where it is the pipe of the home the file names.
/// </summary>
/// <remarks>
/// A twin of the CLI's <c>PIPE_PREFIX</c> and <c>isPipeOf</c> (<c>service.ts</c>), which hold the same rule and share no code
/// (<c>twins.md</c>). The id is never a path, so the name may travel where a path may not.
/// </remarks>
public static class PipeName
{
    /// <summary>What every Daoris pipe's name begins with. The CLI's <c>PIPE_PREFIX</c> is its copy.</summary>
    public const string Prefix = "daoris-";

    /// <summary>The pipe of the home whose id is <paramref name="homeId"/>, in the Windows session <paramref name="session"/>.</summary>
    public static string For(string homeId, int session) =>
        $"{Prefix}{homeId}-{session.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Whether <paramref name="name"/> is the pipe of the home whose id is <paramref name="homeId"/>: the prefix, that id, and a
    /// session's number, nothing else. False where either is missing or no id.
    /// </summary>
    public static bool IsOf(string? name, string? homeId)
    {
        if (name is null || !HomeIdentity.IsId(homeId)) return false;
        var start = Prefix + homeId + "-";
        if (!name.StartsWith(start, StringComparison.Ordinal)) return false;
        var session = name.AsSpan(start.Length);
        if (session.Length is 0 or > 10) return false;
        foreach (var c in session)
        {
            if (c is < '0' or > '9') return false;
        }

        return true;
    }
}
