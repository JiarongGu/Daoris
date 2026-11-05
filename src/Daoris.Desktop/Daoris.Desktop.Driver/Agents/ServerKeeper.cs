namespace Daoris.Driver;

/// <summary>
/// Keeps a browser server's memory bounded (MCPMEM1): the server a session drives Daoris's browser with is started under a small
/// node script that starts it afresh once its working set passes <see cref="CeilingMb"/> and no call is in flight. The tabs are
/// Daoris's browser's, so the new server finds them where the old left them; only the snapshot references it had handed out are gone.
/// </summary>
/// <remarks>
/// <para><b>Why a restart.</b> Playwright MCP 0.0.82 keeps, per tab, every request the page made since its last navigation, and has
/// no option that bounds that: a page that polls grew the server by about 50 KB a request, 490 MB in 40 seconds, and a chat's
/// ordinary navigation and snapshot calls by about 1 MB a call, shed in part by the garbage collector. Its idle timeout closes only a
/// browser it launched itself, which this one never does.</para>
///
/// <para><b>Only where the state is the browser's.</b> A server that attaches to Daoris's browser holds nothing the chat needs after
/// a restart; any other server may, and is started as it was declared.</para>
/// </remarks>
public static class ServerKeeper
{
    public const string ScriptName = "server-keeper.mjs";

    public const int CeilingMb = 512;

    /// <summary>How often the working set is read, in milliseconds.</summary>
    public const int EveryMs = 15_000;

    /// <summary>The script as shipped, from the assembly, with LF line endings.</summary>
    public static string Source { get; } = ReadSource();

    /// <summary>The script under the home, written if absent or not as shipped; answers its path.</summary>
    public static string Install(string home)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Hooks);
        var path = Path.Combine(folder, ScriptName);
        if (File.Exists(path) && AtomicFile.ReadText(path) == Source) return path;

        Directory.CreateDirectory(folder);
        AtomicFile.WriteText(path, Source);
        return path;
    }

    /// <summary>
    /// <paramref name="server"/>, started by node as <c>node &lt;script&gt; …</c>, made the keeper's with that start after its limits.
    /// </summary>
    public static AcpMcpServer Wrap(string home, AcpMcpServer server, int ceilingMb = CeilingMb, int everyMs = EveryMs) =>
        server with { Arguments = [Install(home), ceilingMb.ToString(), everyMs.ToString(), .. server.Arguments] };

    private static string ReadSource()
    {
        using var stream = typeof(ServerKeeper).Assembly.GetManifestResourceStream("Daoris.Driver.server-keeper.mjs")
            ?? throw new InvalidOperationException("the server keeper's script is missing from the build");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }
}
