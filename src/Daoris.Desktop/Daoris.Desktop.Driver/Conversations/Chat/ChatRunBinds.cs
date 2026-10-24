using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// A conversation's chat run bound (CHATFLOW1c; the chat-flow design §4.3): when its tree opens, beside where it is told its process, and
/// at a turn's end for a chat whose tree grew before this build or whose bind failed, by the same code. Bound to Current or a steps
/// version only; a graph version binds nothing before CHATFLOW1f.
/// </summary>
/// <remarks>
/// <para><b>Said once.</b> Under a named workflow, the conversation's first Daoris line says which and who chose it, as the look says a
/// start's; under Current nothing is said. A bind the home could not keep is said once a run, and the next turn's end tries again: the
/// conversation goes on, and only the landing waits.</para>
///
/// <para><b>Nothing of its checks</b> is told here: they are not enforced on its tree before CHATFLOW1h (§4.4).</para>
/// </remarks>
internal sealed class ChatRunBinds(string home, ServiceClient service, SessionEvents events)
{
    // Each conversation's run once its key was read, and each run whose failed bind was said, so a turn's end asks the service once.
    private readonly ConcurrentDictionary<string, string> _runs = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, bool> _failedSaid = new(StringComparer.Ordinal);

    /// <summary>
    /// Bind the run of a conversation whose tree it just grew, <c>chat-&lt;session&gt;</c>: the record is the line's first in that tree.
    /// The process its told landing is planned with comes back: its binding's, else the rules live.
    /// </summary>
    public WorkflowProcess Opened(string session, string repository, string? workspace, DriverConfig config)
    {
        var run = ChatRuns.Prefix + session;
        _runs[session] = run;
        var bound = Bind(session, run, repository, workspace, config);
        return WorkflowProcesses.Bound(home, config, repository, workspace, run, bound.Binding);
    }

    /// <summary>
    /// At a turn's end: bind the conversation's run where it is not yet (a chat whose tree grew before this build, a hand-over's next
    /// record of one, a bind that failed). Its key is read once from the service's records; a record that continues a quest's work, or
    /// does not stand in a tree this home opened, binds nothing. A service that does not answer leaves it for the next turn's end.
    /// </summary>
    public async Task TurnEndedAsync(string session, string tree, DriverConfig config)
    {
        try
        {
            if (_runs.TryGetValue(session, out var known) && WorkflowRunBindings.Bound(home, known)) return;
            var trees = new SessionTrees(home);
            if (!trees.Holds(tree)) return;
            var sessions = Trace.ParseSessions(await service.SessionRecordsJsonAsync(CancellationToken.None).ConfigureAwait(false));
            if (sessions.FirstOrDefault(each => string.Equals(each.Id, session, StringComparison.OrdinalIgnoreCase)) is not { } record
                || ChatRuns.ChatOf(record, sessions, trees.Holds) is not { } run)
            {
                return;
            }

            _runs[session] = run;
            if (WorkflowRunBindings.Bound(home, run)) return;
            var registry = await service.RegistryAsync(CancellationToken.None).ConfigureAwait(false);
            var workspace = registry.FirstOrDefault(each => string.Equals(each.Repository, record.Repository, StringComparison.OrdinalIgnoreCase))?.Workspace;
            Bind(session, run, record.Repository, workspace, config);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException
                                          or UnauthorizedAccessException or OperationCanceledException or InvalidOperationException)
        {
            // Unread: the next turn's end tries again, and a press on the unbound run binds first.
        }
    }

    /// <summary>Bind <paramref name="run"/> and say what the conversation is told of it, once.</summary>
    private ChatRunBound Bind(string session, string run, string repository, string? workspace, DriverConfig config)
    {
        var plugins = WorkflowCurrent.PluginsOf(PluginCatalog.Load(home, AdapterSet.Built().Names));
        var bound = ChatRuns.Bind(config, home, run, repository, workspace, plugins, DateTimeOffset.UtcNow);
        var line = bound.Failed is { } why
            ? _failedSaid.TryAdd(run, true) ? ChatRuns.FailedLine(why) : null
            : bound.Wrote && bound.Binding is { } binding ? ChatRuns.Line(binding) : null;
        if (line is not null)
        {
            events.Keep(session, new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts }, say: null);
        }

        return bound;
    }
}
