using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// A conversation's Accept: its submission, then the same keep as every Accept. It reserves the conversation in this window's runner,
/// captures one tip, submits it as the run's next candidate, reads the gate keyed by the run at that tip with the press's answer to what it
/// showed, keeps the review's Accept on the candidate's record, and waits for what its run's <c>land</c> came to
/// (<see cref="ChatPresses.PressAsync"/>); work no run's <c>land</c> performs is held.
/// </summary>
internal sealed class ChatAccepts(DriverLoop loop)
{
    /// <summary>What the press came to, the gate that judged it, and the code the page words a refusal by.</summary>
    internal sealed record Pressed(TreeLanding Landed, LandingGate Gate, string? Code);

    /// <summary>The newest record of a chat run's line in its tree, which its press reserves; null where the run is no conversation's.</summary>
    internal static string? SessionOf(string run, string recordsJson) =>
        ChatRuns.RecordOf(run) is { } first && ChatRuns.PressOf(first, recordsJson).Line is { Count: > 0 } line ? line[^1] : null;

    /// <summary>Binds the chat run before its press is gated: null where it is bound or bound now, else why the home could not keep it.</summary>
    internal string? BindFailed(string run, string tree) => ChatRuns.BindFirst(
        DriverConfig.Load(loop.ConfigPath), loop.Home, run, tree,
        WorkflowSteps.PluginsOf(PluginCatalog.Load(loop.Home, ProviderRegistry.Built().Names)), DateTimeOffset.UtcNow).Failed;

    /// <summary>The node door's Accept on a chat run: the conversation's press, answered as the node door answers, with its <c>landing</c> and <c>press</c>.</summary>
    public async Task<object> NodeAsync(WorkflowNodePress press, CancellationToken ct)
    {
        var service = loop.Service ?? throw DriverArguments.NotReady();
        var recordsJson = await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var session = SessionOf(press.Run, recordsJson);
        var tree = session is null ? null
            : SessionRecords.Parse(recordsJson).FirstOrDefault(record => string.Equals(record.Id, session, StringComparison.OrdinalIgnoreCase))?.Tree;
        var refused = session is null || string.IsNullOrWhiteSpace(tree)
            ? $"run `{press.Run}` names no conversation with a working tree on this machine, so nothing was accepted."
            : BindFailed(press.Run, tree) is { } failed ? $"run `{press.Run}` could not be bound here, so nothing was accepted: {failed}" : null;
        if (refused is not null)
        {
            return new { press.Run, press.Node, press.Act, Done = false, Refused = RunNodeWords.NotRun, Press = (string?)null, Landing = (object?)null, Message = refused };
        }

        var pressed = await PressAsync(session!, tree!, press.Run, recordsJson, press.Answers, ct).ConfigureAwait(false);
        var landed = pressed.Landed;
        return new
        {
            press.Run, press.Node, press.Act, Done = landed.Landed || landed.Refusal is LandingCode.CarriedOn, Refused = landed.Landed ? null : landed.Refusal,
            Press = pressed.Code, Landing = new { landed.Landed, landed.Message, Code = landed.Refusal }, landed.Message,
        };
    }

    /// <summary>The press on the conversation's work in <paramref name="tree"/>, its record <paramref name="id"/> the line's newest.</summary>
    public async Task<Pressed> PressAsync(string id, string tree, string chatRun, string recordsJson, string? answers, CancellationToken ct)
    {
        var service = loop.Service ?? throw DriverArguments.NotReady();
        var trees = new SessionTrees(loop.Home);
        var (workspace, repository) = trees.Owner(tree);
        var (line, live) = ChatRuns.PressOf(id, recordsJson);
        var world = new ServiceReviewWorld(service);
        var presses = new OpinionPresses(loop.Home, service) { Holder = loop.Chat };
        var accepted = false;
        var kept = false;
        PressSteps.Reach(PressSteps.Gate);
        var pressed = await ChatPresses.PressAsync(
            loop.Home, new ChatPressAsk(id, tree, chatRun, repository, workspace, live), loop.Chat,
            // The gate is the run's, its graph part judged by the pass this press asks after the candidate it submitted, and the press's
            // Accept the review's word, kept on the way to it.
            async (tip, token) => await GraphPasses.PressAsync(
                async held => await presses.PressedAsync(
                    id, await trees.GateAsync(tree, null, world, id, held, tip: tip, run: chatRun), answers, tree, null, held, chatRun),
                held => trees.GateAsync(tree, null, world, id, held, tip: tip, run: chatRun), loop.PassWholeAsync, token,
                (review, _) => Task.FromResult(accepted = RunNodeWords.Word(new NodeRecordFiles(loop.Home), review.Run, review.Node!, NodeWordActs.Accept, null).Done),
                (graph, _) => Task.FromResult(kept = EarlyAccepts.Keep(new NodeRecordFiles(loop.Home), graph.Run, graph.Commit).Done)),
            async (gate, token) => gate.Process is WorkflowProcess { LandsByRuntime: true } performed
                ? kept && !gate.LetsGo && LandPresses.Kept(gate) is { } early ? early : await LandingAsync(id, gate, performed, accepted, token)
                : RunLandHolds.Of(gate),
            () => ChatPresses.TurnsOf(loop.Events, line), ct).ConfigureAwait(false);

        var landed = pressed.Landed;
        var judged = pressed.Gate ?? LandingGate.Open(repository);
        if (landed.Landed) loop.Events.Keep(id, LandingNotes.Note(landed), said => loop.Output.Append(id, said));
        if (loop.Log is { } log && LandingGateWords.Held(id, (workspace, repository), judged, landed, ReviewDoors.Screen) is { } held)
        {
            SessionLog.WriteLanding(log, held);
        }

        loop.Nudge();
        return new Pressed(landed, judged, pressed.Code);
    }

    /// <summary>What the run's <c>land</c> came to at the gate's tip, waited for within the bound; a gate held only by this press's Accept holds nothing.</summary>
    private async Task<TreeLanding> LandingAsync(string id, LandingGate gate, WorkflowProcess process, bool accepted, CancellationToken ct)
    {
        if (gate.Refusal is { } held && !LandPresses.HeldByOwnAccept(gate, accepted)) return held;
        if ((gate.Tip ?? gate.GraphPart()?.Commit) is not { } tip) return new TreeLanding(false, TreesWords.NotJudged(id).Error.FirstOrDefault() ?? "nothing was judged.");

        return PerformedAnswers.Of(await PerformedWaits.AwaitAsync(loop, id, RunUnits.IdOf(process.BoundRun!), tip, ct).ConfigureAwait(false));
    }
}
