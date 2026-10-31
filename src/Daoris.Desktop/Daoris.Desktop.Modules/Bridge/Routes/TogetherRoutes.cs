using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>Bringing sessions together, the page's `bridge/sessions/together.ts` (LINESTOGETHER1d): the operation a session is in, and the presses on it.</summary>
public sealed class TogetherRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>
    /// The operation bringing sessions together a session is in (LINESTOGETHER1d, the review-flow design §4), its open one first, else
    /// its newest: the phase on its head's page and each member's, and where it was brought into. None where it is in none.
    /// </summary>
    [DriverRoute("TOGETHER")]
    [ByEvent("TOGETHER", Untold = "the operations file, which the look rewrites at each look and the presses rewrite")]
    private async Task<object?> TogetherAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        await Task.CompletedTask;
        try
        {
            var held = new TogetherOperations(_loop.Home).All().Where(each => each.Holds(session)).ToList();
            var operation = held.LastOrDefault(each => each.IsOpen) ?? held.LastOrDefault();
            return new { Session = session, Operation = operation is null ? null : TogetherWire.Of(operation, session) };
        }
        catch (IOException error)
        {
            return new { Session = session, Operation = (object?)null, Unread = error.Message };
        }
    }

    // LINESTOGETHER1d: Bring them together… on a head, with the sessions the person ticked; the press judges each again under the
    // door's locks and says the fixed words to the head. A refusal is answered as data, with its sentence.
    [DriverRoute("TOGETHER_PRESS")]
    [Publishes("TOGETHER_PRESS", StateScopes.Sessions, Session = "session")]
    private async Task<object?> TogetherPressAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        var members = request.Payload is { } payload && payload.TryGetProperty("members", out var named) && named.ValueKind == JsonValueKind.Array
            ? [.. named.EnumerateArray().Where(id => id.ValueKind == JsonValueKind.String).Select(id => id.GetString()!).Distinct(StringComparer.Ordinal)]
            : Array.Empty<string>();
        var service = _loop.Service ?? throw NotReady();
        var records = await service.SessionRecordsAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
        var config = DriverConfig.Load(_loop.ConfigPath);
        var plugins = WorkflowCurrent.PluginsOf(PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names));
        string? WorkspaceOf(string repository) => snapshot.Repositories
            .FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Workspace;
        var sources = new WorkflowRunSources(service, _loop.Home, config, plugins, WorkspaceOf);
        async Task<bool> StepsOnAsync(string member, CancellationToken token) =>
            await WorkflowRunReader.ReadAsync(new WorkflowRunAsk(Session: member), sources, token).ConfigureAwait(false) is { Problem: null } read
            && read.Runs.Any(run => TogetherPress.StepsOn(run.Steps));
        var (head, candidates, refused) = await TogetherPress.CandidatesAsync(records, session, members, cancellationToken, StepsOnAsync).ConfigureAwait(false);
        if (head is null) return new { Operation = (string?)null, Refused = refused };
        var pressed = await Press(service).PressAsync(head, candidates, cancellationToken).ConfigureAwait(false);
        _loop.Nudge();
        return new { Operation = pressed.Operation?.Id, Refused = pressed.Refused };
    }

    // LINESTOGETHER1d: Ask again after a failed check, the words said once more under a key of their own.
    [DriverRoute("TOGETHER_ASK_AGAIN")]
    [Publishes("TOGETHER_ASK_AGAIN", StateScopes.Sessions, Session = "session")]
    private async Task<object?> TogetherAskAgainAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        var service = _loop.Service ?? throw NotReady();
        var refused = await Press(service).AskHeadAgainAsync(session, cancellationToken).ConfigureAwait(false);
        _loop.Nudge();
        return new { Done = refused is null, Refused = refused };
    }

    // LINESTOGETHER1d: Let them go before the landing; what the head made stays.
    [DriverRoute("TOGETHER_LET_GO")]
    [Publishes("TOGETHER_LET_GO", StateScopes.Sessions, Session = "session")]
    private async Task<object?> TogetherLetGoAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var session = PayloadHelper.GetRequiredValue<string>(request.Payload, "session");
        var refused = await Press(_loop.Service ?? throw NotReady()).LetGoAsync(session, cancellationToken).ConfigureAwait(false);
        _loop.Nudge();
        return new { Done = refused is null, Refused = refused };
    }

    /// <summary>The press, saying its words through the say door to the line's head, in Daoris's own words.</summary>
    private TogetherPress Press(ServiceClient service) => new(_loop.Home,
        async (at, text, key, token) => (await WordsHead.FollowAsync(
            at, named => service.SayAsync(named, text, null, token, from: DaorisWords.From, key: key)).ConfigureAwait(false)).Said,
        () => DateTimeOffset.UtcNow);
}
