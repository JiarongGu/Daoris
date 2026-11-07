namespace Daoris.Driver;

/// <summary>What <c>trees land</c> on a chat run's work reads and writes through: the service, the home, its trees and the instance.</summary>
/// <param name="Instance">The running Daoris this terminal may reach; null where it reaches none, so a live conversation is refused.</param>
public sealed record ChatLandWorld(ServiceClient Service, string Home, SessionTrees Trees, InstanceWorld? Instance, TextWriter Output, TextWriter Error)
{
    /// <summary>This host's machine log: a landing the gate held is written there.</summary>
    public MachineLog? Log { get; init; }
}

/// <summary>
/// <c>daoris tree land &lt;session&gt;</c> on a chat run's work: bound first, as the window's press binds it, then the driver's one press operation
/// (<see cref="ChatPresses.PressAsync"/>). A live conversation is held by the running Daoris's runner, so the press is sent there (the connected
/// terminal), confirmed in its window and reserved there; with none to reach, it is refused, never landed beside a turn this terminal cannot
/// see. An ended one no runner holds, and this terminal is its door (the standalone terminal), which lands nothing: the
/// run's <c>land</c> lands it at Daoris's next pass, so the press keeps the review's word, or says what holds the work. Its words are the
/// press's, exit 1 where refused.
/// </summary>
public static class ChatLandCommand
{
    /// <summary>The press, by the session's record as the service answered it and its chat run.</summary>
    /// <param name="entered">The branch name the person entered, which the running Daoris's press is sent with.</param>
    public static async Task<int> RunAsync(
        ChatLandWorld world, string session, string tree, string chatRun, string recordsJson, string? entered, CancellationToken ct = default)
    {
        var bound = WorkplaceWorkflows.Current.BindChatFirst(DriverConfig.Load(DriverConfig.ResolvePath(world.Home)), world.Home, chatRun, tree,
            WorkplacePlugins.Current.Load(world.Home), DateTimeOffset.UtcNow);
        if (bound is { } failed) return Print(world, TreesWords.BindFailed(session, failed));

        var (line, live) = WorkplaceWorkflows.Current.ChatPressOf(session, recordsJson);
        if (live)
        {
            var payload = new { id = session, branch = entered, said = true, follow = true };
            Task<int> Elsewhere() =>
                Task.FromResult(Print(world, TreesWords.Land(new TreeLanding(false, ChatPresses.Elsewhere) { Refusal = LandingCode.Refused }, [])));
            return world.Instance is null
                ? await Elsewhere().ConfigureAwait(false)
                : await InstanceDoor.PressAsync(TreesRoutes.Land, payload, Elsewhere, answer => TreesSaid.ReadWords(world.Output, world.Error)(answer) ?? 2,
                    world.Instance, ct).ConfigureAwait(false);
        }

        var service = world.Service;
        var landing = world.Trees;
        var (workspace, repository) = landing.Owner(tree);
        var events = WorkplaceEventRecords.Current.Open(DaorisHome.Entry(world.Home, HomeEntry.Sessions));
        var reviewWorld = new ServiceReviewWorld(service);
        var presses = new OpinionPresses(world.Home, service);
        TreesSaid? kept = null;
        var pressed = await ChatPresses.PressAsync(
            world.Home, new ChatPressAsk(session, tree, chatRun, repository, workspace, Live: false), holder: null,
            async (tip, token) => await presses.PressedAsync(
                session, await landing.GateAsync(tree, null, reviewWorld, session, token, tip: tip, run: chatRun).ConfigureAwait(false), null, tree, null,
                token, chatRun, ReviewDoors.Terminal).ConfigureAwait(false),
            (gate, _) =>
            {
                // A run whose `land` the runtime performs keeps the word for its next pass; any other is held, and nothing lands it here.
                if (gate.Process is not { LandsByRuntime: true }) return Task.FromResult(RunLandHolds.Of(gate));
                kept = TreesWords.AcceptedForPass(world.Home, gate);
                return Task.FromResult(new TreeLanding(false, "accepted; Daoris lands it at its next pass."));
            },
            () => ChatPresses.TurnsOf(events, line), ct).ConfigureAwait(false);

        if (kept is not null) return Print(world, kept);

        WorkplacePressTelling.Current.Tell(world.Home, TreesMarks.Land, session, DateTimeOffset.UtcNow);
        var said = TreesWords.Land(pressed.Landed, []);
        said.Print(world.Output, world.Error);
        if (world.Log is { } log && pressed.Gate is { } judged
            && LandingGateWords.Held(session, (workspace, repository), judged, pressed.Landed, ReviewDoors.Terminal) is { } held)
        {
            SessionLog.WriteLanding(log, held);
        }

        return said.Code;
    }

    private static int Print(ChatLandWorld world, TreesSaid said)
    {
        said.Print(world.Output, world.Error);
        return said.Code;
    }
}
