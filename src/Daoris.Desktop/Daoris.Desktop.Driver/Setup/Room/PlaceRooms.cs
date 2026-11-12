namespace Daoris.Driver;

/// <summary>
/// The room part of a place's plan (<c>place &lt;repository&gt; --room &lt;path&gt; --purpose "…"</c>): a room is a folder inside a repository whose
/// <c>AGENTS.md</c> and declaration are that repository's own files, so Daoris writes none of it and the plan's whole is one quest to the
/// repository's own session (<see cref="RoomBrief"/>). The plan refuses what a quest could not do, every refusal at once in a code a screen keys
/// on, and writes and asks nothing.
/// </summary>
/// <remarks>
/// <para><b>One door.</b> A place's set-up is one door (<c>PLACE_APPLY</c>), so a room is a part of it and not a verb of its own. It rides with no
/// other part: the others change configuration, a room changes none, and an ask that mixed them would have two receipts.</para>
/// <para><b>One judge.</b> <see cref="JudgeAsync"/> is what the plan refuses by and what the apply judges again before it asks, so a room that
/// appeared between the two is never asked for a second time.</para>
/// </remarks>
public static class PlaceRooms
{
    /// <summary>The longest reason a room carries: one line, which is the quest's quoted request.</summary>
    public const int PurposeLength = 1000;

    /// <summary>What is said where general driving stays off: the room's quest is admitted alone.</summary>
    private const string Alone = "its room quest alone";

    /// <summary>The ask as the plan keeps it: repositories, path and reason said as the repository and the quest keep them, each as typed where it is refused.</summary>
    public static PlaceAsk Normalized(PlaceAsk ask)
    {
        var names = ask.Repositories.Select(each => each.Trim()).Where(each => each.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var purpose = ask.Purpose is { } typed ? string.Join(' ', typed.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) : null;
        return ask with { Repositories = names, Room = PlaceRoomPath.Read(ask.Room).Path ?? ask.Room, Purpose = purpose };
    }

    /// <summary>The ask's own refusals, said before any repository is read: one repository, one reason, a path inside it, and no other part.</summary>
    public static IReadOnlyList<PlaceRefusal> AskRefusals(PlaceAsk ask)
    {
        var refusals = new List<PlaceRefusal>();
        void Refuse(string code, string sentence) => refusals.Add(new(null, PlacePlans.Room, code, sentence));

        var parts = new[]
        {
            ask.Preset.Length > 0 ? "a delivery" : null, ask.Opinion ? "a second opinion" : null, ask.Look ? "your look" : null,
            ask.Adoption ? "adoption" : null, ask.Builds is not null ? "what it builds" : null, ask.Drive ? "general driving" : null,
            ask.AtOnce is not null || ask.Pilot is not null ? "a pace" : null,
        }.OfType<string>().ToList();
        if (parts.Count > 0)
        {
            Refuse(PlaceRoomRefusals.Alone,
                $"a room is asked of its repository's own session and changes no configuration, so it rides with no other part ({string.Join(", ", parts)}): "
                + "ask each of those in a place of its own, and the room by itself.");
        }

        if (ask.Repositories.Count == 0) Refuse(PlaceRoomRefusals.One, "name the repository the room is for.");
        else if (ask.Repositories.Count > 1) Refuse(PlaceRoomRefusals.One, "a room is asked of one repository, whose own session makes it: name one.");

        if (ask.Purpose is not { Length: > 0 })
        {
            Refuse(PlaceRoomRefusals.Purpose, "say why the room is wanted, as `--purpose \"…\"`: it is the request its repository's session reads.");
        }
        else if (ask.Purpose.Length > PurposeLength)
        {
            Refuse(PlaceRoomRefusals.Purpose, $"the reason runs to {ask.Purpose.Length} characters: say it in {PurposeLength} or fewer, since it is one line of the quest.");
        }

        if (PlaceRoomPath.Read(ask.Room).Problem is { } problem) Refuse(PlaceRoomRefusals.Path, problem);
        return refusals;
    }

    /// <summary>
    /// What the repository's row, its configuration, its quests and its line say of the room, every refusal in the press's order: not adopted, held, no
    /// checkout, a set-up open beside it, a line that cannot be read, a path through a link or onto a file, and a room already there. Reads the
    /// room only once the path is one and the row can answer for it; asks no quest.
    /// </summary>
    public static async Task<(IReadOnlyList<PlaceRefusal> Refusals, PlaceRoomRead? Read)> JudgeAsync(
        IPlaceWorld world, DriverConfig config, bool unadopted, RepoView row, string path, IReadOnlyList<QuestView> quests, CancellationToken ct)
    {
        var name = row.Repository;
        var refusals = new List<PlaceRefusal>();
        void Refuse(string code, string sentence) => refusals.Add(new(name, PlacePlans.Room, code, sentence));

        if (!row.Adopted)
        {
            Refuse(SetupRefusals.NotAdopted, NotAdopted(name, "has not taken up the shared doctrine"));
            return (refusals, null);
        }

        if (config.Holds.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            Refuse(PlaceRoomRefusals.Held, $"`{name}` is held, so it starts nothing new, a room's quest included: resume it first, with `daoris place resume {name}`.");
        }

        // Only its checkout's absence: an adopted repository's sessions work apart or in place as its workflow says, and a room is any quest's own work.
        foreach (var refusal in SetupPress.ConfigurationRefusals(config, unadopted, row, apart: true)) Refuse(refusal.Code, refusal.Sentence);
        if (SetupPress.OpenRefusal(quests, name) is { } open) Refuse(open.Code, open.Sentence + " Its own rooms are that set-up's to propose: ask for the room once it is done.");
        if (refusals.Count > 0) return (refusals, null);

        var read = await world.RoomAsync(row, config, path, ct).ConfigureAwait(false);
        if (read.Problem is { } problem)
        {
            Refuse(PlaceApplyRefusals.Unread, problem);
            return (refusals, read);
        }

        if (!read.Adopted) Refuse(SetupRefusals.NotAdopted, NotAdopted(name, $"holds no `{DeclarationNames.Manifest}` on its line `{read.Line}`"));
        if (read.Through is { } through)
        {
            Refuse(PlaceRoomRefusals.Path, $"`{path}` goes through `{through}`, which is a link, a submodule or a file on `{name}`'s line, so the room would not be a folder of this repository.");
        }
        else if (read.IsFile)
        {
            Refuse(PlaceRoomRefusals.Path, $"`{path}` is a file on `{name}`'s line, not a folder to make a room in.");
        }

        if (ExistsAt(read, path) is { } held)
        {
            Refuse(PlaceRoomRefusals.Exists, $"`{name}` already has a room at `{path}` ({held}): nothing is asked, since its own session keeps its text.");
        }

        return (refusals, read);
    }

    /// <summary>The declaration or file that makes a room of <paramref name="path"/> on the line, or null where none does.</summary>
    public static string? ExistsAt(PlaceRoomRead read, string path) =>
        read.Declared.Any(each => PlaceRoomPath.Same(path, each)) ? $"`{DeclarationNames.Manifest}`'s `rooms`"
        : read.Folders.Any(each => PlaceRoomPath.Same(path, each)) ? "a folder with its own `AGENTS.md`"
        : read.Lanes.Any(each => string.Equals(each, path, StringComparison.OrdinalIgnoreCase)) ? $"a room `{DeclarationNames.Lanes}` declares"
        : null;

    private static string NotAdopted(string name, string why) =>
        $"`{name}` {why}, so there is no room to add: its set-up comes first, *{SetupQuests.SetUp}*.";

    /// <summary>
    /// The plan of a room ask: the ask's refusals, then each repository's, and for the one that is asked its one change, the room's quest. Writes and
    /// asks nothing: the registry and the quests, and the line's declaration as git objects, are all it reads.
    /// </summary>
    public static async Task<PlacePlanned> PlanAsync(
        IPlaceWorld world, DriverConfig config, string home, bool unadopted, PlaceAsk ask, CancellationToken ct)
    {
        ask = Normalized(ask);
        var refusals = AskRefusals(ask).ToList();
        var path = PlaceRoomPath.Read(ask.Room).Path;
        var registry = await world.RegistryAsync(ct).ConfigureAwait(false);
        var quests = await world.QuestsAsync(ct).ConfigureAwait(false);
        var store = WorkflowPorts.Store(home);
        var rows = new List<PlaceRepository>();
        string? growth = null;
        var made = new List<(PlaceChange Change, string Repository)>();
        foreach (var name in ask.Repositories)
        {
            if (registry.FirstOrDefault(each => string.Equals(each.Repository, name, StringComparison.OrdinalIgnoreCase)) is not { } row)
            {
                var missing = SetupPress.NotOnRegistry(name);
                refusals.Add(new(name, PlacePlans.Room, missing.Code, missing.Sentence));
                continue;
            }

            growth = GrowthRules.Resolve(config, row.Repository, RemoteTarget.Workspace(row.Workspace)).Value;
            if (growth == GrowthKey.Off) refusals.Add(new(row.Repository, PlacePlans.Room, PlaceRoomRefusals.Growth, GrowthRules.OffRoom(row.Repository)));
            IReadOnlyList<PlaceRefusal> judged = [];
            PlaceRoomRead? read = null;
            if (path is not null) (judged, read) = await JudgeAsync(world, config, unadopted, row, path, quests, ct).ConfigureAwait(false);
            refusals.AddRange(judged);
            var workspace = RemoteTarget.Workspace(row.Workspace);
            var side = PlacePlans.SideOf(config, row.Repository, workspace, store);
            var changes = new List<PlaceChange>();
            if (path is not null && refusals.Count == 0)
            {
                changes.Add(new(PlacePlans.RoomPressKey, PlacePlans.Room, $"a room at `{path}` asked of its repository's own session", "no room", "asked"));
                // Not driven, the press admits its quest alone, and the queued work stays held.
                if (!config.Drivable.Contains(row.Repository, StringComparer.OrdinalIgnoreCase)) changes.Add(new(PlacePlans.RoomPressKey, PlacePlans.Room, PlacePlans.AloneSaid, "nothing", Alone));
            }

            rows.Add(new PlaceRepository(
                row.Repository, workspace, side, side, changes, [], [], [], null, read?.Problem,
                new PlaceSuggestion(SetupPresets.Keep, false, SuggestionWhy.Of(SuggestionWhy.Kept))));
            made.AddRange(changes.Select(change => (change, row.Repository)));
        }

        var said = made.GroupBy(each => each.Change).Select(group => new PlaceChanged(group.Key, [.. group.Select(each => each.Repository)])).ToList();
        return new PlacePlanned(ask, rows, refusals.Count == 0 ? said : [], refusals, PlacePlans.Terminal(ask)) { Growth = growth };
    }

    /// <summary>The terminal's lines of a room's plan: the room and its reason, the change it makes, and each refusal.</summary>
    public static IReadOnlyList<string> Lines(PlacePlanned plan, bool applying)
    {
        var ask = plan.Asked;
        var lines = new List<string>
        {
            $"place: {string.Join(", ", ask.Repositories.Select(name => $"`{name}`"))} — a room at `{ask.Room}`: {ask.Purpose}",
        };
        if (plan.Changes.Count == 0)
        {
            lines.Add("changes: none, no room is asked.");
        }
        else
        {
            lines.Add("changes, each said once:");
            lines.AddRange(plan.Changes.Select(each =>
                $"  {string.Join(", ", each.Repositories.Select(name => $"`{name}`"))}: {each.Change.Said}, {each.Change.Before} → {each.Change.After}"));
        }

        if (plan.Refusals.Count > 0)
        {
            lines.Add("refused:");
            lines.AddRange(plan.Refusals.Select(refusal => $"  - {refusal.Sentence}"));
        }

        if (!applying) lines.Add("--plan: nothing was written, and nothing was asked.");
        return lines;
    }
}
