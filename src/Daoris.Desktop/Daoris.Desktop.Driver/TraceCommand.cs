namespace Daoris.Driver;

/// <summary>The kinds of thing a trace starts from (TRACE1, D143).</summary>
public static class TraceEntry
{
    public const string Commit = "commit";
    public const string Session = "session";
    public const string Quest = "quest";

    /// <summary>Every kind, as the terminal names one before an id.</summary>
    public static IReadOnlyList<string> All { get; } = [Commit, Session, Quest];
}

/// <summary>What <c>daoris-driver trace</c> was asked: an id, and the kind it names where the person said which.</summary>
/// <param name="Id">A commit (at least <see cref="Trace.CommitDigits"/> hexadecimal digits), a session's id or a quest's, as typed.</param>
/// <param name="Kind">One of <see cref="TraceEntry"/>, or null: whatever the id names here, refused where it names several.</param>
public sealed record TraceAsk(string Id, string? Kind = null);

/// <summary>Where a trace reads (D143): the service's doors, the home's files and the person's choices. It writes to none.</summary>
public sealed record TraceSources(ServiceClient Service, string Home, DriverConfig Config);

/// <summary>An ask a chain names, as the service answered it: the ask, or not found, or why it could not be read.</summary>
internal sealed record TraceAskRead(string Id, AskView? Ask, string? Unread);

/// <summary>
/// <c>daoris-driver trace &lt;commit|session|quest&gt;</c> (TRACE1, D143): one read from a commit, a session or a quest back to the
/// ask it served, each link printed from the store that keeps it and a link nothing keeps said missing. In the library, so
/// its words are held by a test.
/// </summary>
/// <remarks>
/// <para><b>Reads only.</b> Every request to the service is a <c>GET</c>, every file is opened to read, and the host routes the
/// verb before it opens its machine log, whose open prunes old files: the trace writes nothing anywhere.</para>
///
/// <para>Exit codes are the family's: 0 the chain read, its missing links said · 1 nothing here names the id, or it names
/// several · 2 could not: the usage, or a store the answer needed did not answer.</para>
/// </remarks>
public static class TraceCommand
{
    public const string Usage =
        """
        usage: daoris-driver trace <commit|session|quest>  ·  trace commit|session|quest <id>
        """;

    /// <summary>What the words ask, or null with what is wrong with them.</summary>
    public static TraceAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        switch (args)
        {
            case [var kind, var id] when TraceEntry.All.Contains(kind) && id.Trim().Length > 0:
                if (kind == TraceEntry.Commit && !Trace.IsCommit(id.Trim().TrimStart('#')))
                {
                    problem = $"a commit is named by at least {Trace.CommitDigits} of its hexadecimal digits, not `{id}`.";
                    return null;
                }

                return new TraceAsk(id.Trim(), kind);
            // A flag is no id: `trace --help` reads the usage rather than searching every store for `--help`.
            case [var id] when id.Trim().Length > 0 && !id.StartsWith('-') && !TraceEntry.All.Contains(id):
                return new TraceAsk(id.Trim());
            default:
                problem = "`trace` reads one commit, session or quest: name it, or its kind and then it (`trace session <id>`).";
                return null;
        }
    }

    /// <summary>Read the stores, find what the id names, read the asks the chain names, and print the chain.</summary>
    public static async Task<int> RunAsync(TraceAsk asked, TraceSources sources, TextWriter output, CancellationToken ct = default)
    {
        var facts = await Trace.ReadAsync(sources, ct).ConfigureAwait(false);
        var unread = TraceWords.Unread(facts);
        var (found, problem) = Trace.Resolve(asked, facts);
        if (found is null)
        {
            output.WriteLine($"trace: {problem}");
            foreach (var line in unread) output.WriteLine($"  {line}");
            return unread.Count > 0 ? 2 : 1;
        }

        var asks = new Dictionary<string, TraceAskRead>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in TraceWords.AsksNamed(found, facts))
        {
            asks[id] = await ReadAskAsync(sources.Service, id, ct).ConfigureAwait(false);
        }

        output.Write(TraceWords.Say(found, facts, asks, sources, unread));
        return 0;
    }

    /// <summary>One ask, whole, from its door (<c>GET /api/asks/{id}</c>): never a failure of the trace.</summary>
    private static async Task<TraceAskRead> ReadAskAsync(ServiceClient service, string id, CancellationToken ct)
    {
        try
        {
            return new TraceAskRead(id, await service.FindAskAsync(id, ct).ConfigureAwait(false), null);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                          or InvalidOperationException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return new TraceAskRead(id, null, error.Message);
        }
    }
}
