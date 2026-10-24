namespace Daoris.Driver;

/// <summary>What <c>trees land</c> on a chat run's work reads and writes through (CHATFLOW1d): the service, the home, its trees and the instance.</summary>
/// <param name="Instance">The running Daoris this terminal may reach; null where it reaches none, so a live conversation is refused.</param>
public sealed record ChatLandWorld(ServiceClient Service, string Home, SessionTrees Trees, InstanceWorld? Instance, TextWriter Output, TextWriter Error)
{
    /// <summary>This host's machine log: a landing the gate held is written there.</summary>
    public MachineLog? Log { get; init; }
}

/// <summary>
/// <c>daoris-driver trees land &lt;session&gt; [--without-look "…"]</c> on a chat run's work (CHATFLOW1d, CHATFLOW1d2; the chat-flow design §5.2
/// point 1, §5.3, Codex's finding 11): bound first, as the window's press binds it, then the driver's one press operation
/// (<see cref="ChatPresses.PressAsync"/>). A live conversation is held by the running Daoris's runner, so the press is sent there (the connected
/// terminal), confirmed in its window and reserved there; with none to reach, it is refused, never landed beside a turn this terminal cannot
/// see. An ended one no runner holds, and this terminal is its door (the standalone terminal). Its words are the press's, exit 1 where refused.
/// </summary>
public static class ChatLandCommand
{
    /// <summary>The press, by the session's record as the service answered it and its chat run.</summary>
    /// <param name="entered">The branch name the person entered, which lands as entered.</param>
    /// <param name="withoutLook">The person's way past the look and their words (CHATFLOW1d2); null for a plain Accept.</param>
    public static async Task<int> RunAsync(
        ChatLandWorld world, string session, string tree, string chatRun, string recordsJson, string? entered, ChatLookPress? withoutLook,
        CancellationToken ct = default)
    {
        var bound = ChatRuns.BindFirst(DriverConfig.Load(DriverConfig.ResolvePath(world.Home)), world.Home, chatRun, tree,
            WorkflowCurrent.PluginsOf(PluginCatalog.Load(world.Home, AdapterSet.Built().Names)), DateTimeOffset.UtcNow);
        if (bound.Failed is { } failed) return Print(world, TreesWords.BindFailed(session, failed));

        var (line, live) = ChatRuns.PressOf(session, recordsJson);
        if (live)
        {
            var route = withoutLook is null ? TreesRoutes.Land : TreesRoutes.LandWithoutLook;
            object payload = withoutLook is null
                ? new { id = session, branch = entered, said = true, follow = true }
                : new { id = session, words = withoutLook.Words, said = true, follow = true };
            Task<int> Elsewhere() =>
                Task.FromResult(Print(world, TreesWords.Land(new TreeLanding(false, ChatPresses.Elsewhere) { Refusal = AutoLandingCode.Refused }, [])));
            return world.Instance is null
                ? await Elsewhere().ConfigureAwait(false)
                : await InstanceDoor.PressAsync(route, payload, Elsewhere, answer => TreesSaid.ReadWords(world.Output, world.Error)(answer) ?? 2,
                    world.Instance, ct).ConfigureAwait(false);
        }

        var service = world.Service;
        var landing = world.Trees;
        var (workspace, repository) = landing.Owner(tree);
        var events = new SessionEvents(DaorisHome.Entry(world.Home, HomeEntry.Sessions));
        var reviewWorld = new ServiceReviewWorld(service);
        var presses = new OpinionPresses(world.Home, service);
        var quests = new PressQuests(service.FindQuestAsync);
        var records = SessionRecords.Parse(recordsJson);
        var pressed = await ChatPresses.PressAsync(
            world.Home, new ChatPressAsk(session, tree, chatRun, repository, workspace, Live: false) { WithoutLook = withoutLook }, holder: null,
            async (tip, token) => await presses.PressedAsync(
                session, await landing.GateAsync(tree, null, reviewWorld, session, token, tip: tip, run: chatRun).ConfigureAwait(false), null, tree, null,
                token, chatRun, ReviewDoors.Terminal).ConfigureAwait(false),
            async (gate, token) =>
            {
                var subject = await landing.OwnSubjectAsync(
                    tree, session, null, records, quest => quests.FindAsync(quest, token), named => events.Openings([named]).GetValueOrDefault(named),
                    gate.Process, token).ConfigureAwait(false) with { Entered = entered };
                return await landing.LandAsync(tree, subject, token, inUse: async held => (await service.ActiveSessionsAsync(held).ConfigureAwait(false))
                    .Select(each => each.Tree).OfType<string>().Where(each => each.Length > 0)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase), gate: gate, joins: subject.Joins).ConfigureAwait(false);
            },
            () => ChatPresses.TurnsOf(events, line), ct).ConfigureAwait(false);

        new PressesTold(world.Home).Tell(new PressTold(withoutLook is null ? PressesTold.Land : PressesTold.LandWithoutLook, session, DateTimeOffset.UtcNow));
        var said = TreesWords.Land(pressed.Landed, []);
        said.Print(world.Output, world.Error);
        if (world.Log is { } log && pressed.Gate is { } judged
            && LandingGateWords.Held(session, (workspace, repository), judged, pressed.Landed, ReviewDoors.Terminal) is { } held)
        {
            SessionLog.WriteLanding(log, held);
        }

        if (pressed.Landed.Landed)
        {
            events.Keep(session, LandingRules.Note(pressed.Landed), each => world.Error.WriteLine($"trees: {each}"));
            var followed = await TreesWords.FollowMovedAsync(service, world.Home, () => DriverConfig.Load(DriverConfig.ResolvePath(world.Home)), world.Log, ct)
                .ConfigureAwait(false);
            foreach (var each in followed.Out) world.Output.WriteLine(each);
            foreach (var each in followed.Error) world.Error.WriteLine(each);
        }

        return said.Code;
    }

    private static int Print(ChatLandWorld world, TreesSaid said)
    {
        said.Print(world.Output, world.Error);
        return said.Code;
    }
}
