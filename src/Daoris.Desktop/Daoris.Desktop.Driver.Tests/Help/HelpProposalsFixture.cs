using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// What every kind's proposal tests stand on (MOD6): a home of the test's own with the proposals folder, the
/// file as the service's box writes it, the machine a proposal is judged against, and an Apply through
/// <see cref="HelpStandInDoors"/>, whose doors each kind's test file adds.
/// </summary>
public abstract class HelpProposalsFixture : IDisposable
{
    protected readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-plans-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly HashSet<string> _scratch = [];

    protected HelpProposalsFixture() => Directory.CreateDirectory(HelpProposals.FolderOf(_home));

    public void Dispose()
    {
        foreach (var folder in _scratch.Prepend(_home))
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>A folder beside the home, never inside it, removed with the home when the test ends.</summary>
    protected string Beside(string suffix)
    {
        var folder = _home + suffix;
        _scratch.Add(folder);
        return folder;
    }

    protected static readonly HelpMachineFacts Facts = new(
        Repositories: ["engine", "game"], Workspaces: ["default", "work"], Agents: ["claude-code", "claude-code-acp"])
    {
        // HELP10: what the loop's last tick parked by its strikes, the verdict the quest drawer shows Retry by.
        Parked = [new ParkedQuest("q1a2b3c4", "engine")],
        // SESSUX1b: what its last look held by the person's stop, and the session each was stopped in.
        Held = [new HeldQuest("q2taken0", "engine", "s7a8b9c0")],
    };

    /// <summary>A file as the service's `HelpProposalBox` writes it — the twin's shape.</summary>
    protected string File(string id, string kind, string door, string? target = null, string? workspace = null,
        string? value = null, string? sentence = null, string session = "h1", string state = "proposed")
    {
        var node = new JsonObject
        {
            ["id"] = id, ["proposed"] = "2026-09-29T10:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = session },
            ["kind"] = kind, ["door"] = door, ["target"] = target, ["workspace"] = workspace, ["value"] = value,
            ["sentence"] = sentence, ["why"] = "the person asked", ["state"] = state, ["note"] = null,
        };
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), $"{id}.json"), node.ToJsonString());
        return id;
    }

    protected static HelpProposal Setting(string door, string? target = null, string? workspace = null, string? value = null) =>
        new("p1", "setting", door, target, workspace, value, null, "why", "h1", "proposed");

    /// <summary>
    /// The machine as the Agents screen, the quest drawer and the ask's record read it: each door's roster
    /// row, and each record with the service's own <c>deletable</c>.
    /// </summary>
    protected static readonly HelpMachineFacts Machine = Facts with
    {
        Doors =
        [
            new HelpDoorFacts("claude-code")
            {
                Present = true, Updates = "tool", Channel = "claude-code-releases", Owner = "claude-code",
                Accounts = ["work"], SettingsKnown = true, Product = "Claude Code",
            },
            new HelpDoorFacts("claude-code-acp")
            {
                Present = true, Updates = "pin", Pinned = "0.84.0", Package = "@agentclientprotocol/claude-agent-acp",
                Owner = "claude-code", Accounts = ["work"], SettingsKnown = true,
            },
            new HelpDoorFacts("dsh") { Present = true, Owner = "dsh", Product = "DeepSeek" },
            new HelpDoorFacts("codex-acp") { Present = false, Updates = "tool", Package = "@zed-industries/codex-acp", Owner = "codex-acp" },
        ],
        Quests =
        [
            new HelpQuestFacts("q1a2b3c4", "Cap the chunk budget", "engine", "Open", Deletable: true),
            new HelpQuestFacts("q2taken0", "Stream the tiles", "engine", "Taken", Deletable: false),
            new HelpQuestFacts("q3done00", "Fix the stall", "game", "Done", Deletable: false),
            new HelpQuestFacts("q4declin", "Rewrite it all", "game", "Declined", Deletable: false),
            new HelpQuestFacts("q5start0", "Verify the fix", "game", "Open", Deletable: false),
        ],
        Asks =
        [
            new HelpAskFacts("a1b2c3d4", "fix the chunk streamer's stall", "work", "Published", ["q1a2b3c4"], Deletable: true),
            new HelpAskFacts("a2none00", "a test ask", "work", "Open", [], Deletable: true),
            new HelpAskFacts("a3kept00", "stream the tiles", "work", "Published", ["q2taken0", "q1a2b3c4"], Deletable: false),
        ],
    };

    /// <summary>
    /// This machine's sessions as Sessions' list reads them (SESSUX1h2b): each record, where the list places it with its
    /// archive mark, the one a tree or a landing keeps, and the ledger's judgement of a delete one names.
    /// </summary>
    protected static readonly HelpMachineFacts SessionsHere = Machine with
    {
        Sessions =
        [
            new SessionRecord("s1ended0", "engine", "completed") { Quest = "q3done00" },
            new SessionRecord("s2ended0", "game", "failed") { Quest = "q3done00" },
            new SessionRecord("s3live00", "engine", "working") { Quest = "q2taken0" },
            new SessionRecord("s4parked", "engine", "awaiting-person") { Quest = "q1a2b3c4" },
            new SessionRecord("s5review", "game", "completed") { Quest = "q5start0", Tree = "/trees/s5review" },
            new SessionRecord("s6archiv", "engine", "completed") { Quest = "q4declin" },
            new SessionRecord("h1helper", HelpRoom.Repository, "completed") { Kind = "chat", Deletable = true },
            new SessionRecord("north/s7team0", "engine", "completed") { Kind = "chat" },
            new SessionRecord("c1chat00", "engine", "completed") { Kind = "chat", Deletable = true },
            new SessionRecord("c2tree00", "engine", "completed") { Kind = "chat", Deletable = true, Tree = "/trees/c2tree00" },
            new SessionRecord("c3named0", "engine", "completed") { Kind = "chat" },
            new SessionRecord("c4remote", "game", "stopped") { Kind = "chat" },
        ],
        Placed =
        [
            new SessionGrouping("s4parked", SessionGroup.You, "parked"),
            new SessionGrouping("s5review", SessionGroup.Review, "completed"),
            new SessionGrouping("s3live00", SessionGroup.Working, "working"),
            new SessionGrouping("s1ended0", SessionGroup.Ended, "completed"),
            new SessionGrouping("s2ended0", SessionGroup.Ended, "failed"),
            new SessionGrouping("h1helper", SessionGroup.Ended, "completed"),
            new SessionGrouping("north/s7team0", SessionGroup.Ended, "completed") { Teammate = true },
            new SessionGrouping("c1chat00", SessionGroup.Ended, "completed"),
            new SessionGrouping("c2tree00", SessionGroup.Ended, "completed"),
            new SessionGrouping("c3named0", SessionGroup.Ended, "completed"),
            new SessionGrouping("c4remote", SessionGroup.Ended, "stopped"),
            new SessionGrouping("s6archiv", SessionGroup.Archived, "completed") { Archived = true },
        ],
        Kept = new HashSet<string>(["c2tree00"], StringComparer.Ordinal),
        SessionDeletes = new Dictionary<string, SessionDeleteAnswer>(StringComparer.Ordinal)
        {
            ["c1chat00"] = new(true, "Session `c1chat00` may be deleted."),
            ["c3named0"] = new(false, "Session `c3named0` is named by ask `#a1b2c3d4` as its intake.") { Refusal = "named", Ask = "a1b2c3d4" },
        },
    };

    protected static HelpProposal Of(string kind, string door, string? target = null, string? value = null) =>
        new("p6", kind, door, target, null, value, null, "the person asked", "h1", "proposed");

    /// <summary>A plugin installed under the test's home, as `daoris plugin add` leaves one (D64).</summary>
    protected HelpMachineFacts WithPlugin(string id, string points = "\"work/land\"", bool enabled = true)
    {
        var folder = Path.Combine(_home, PluginCatalog.Folder, id);
        Directory.CreateDirectory(folder);
        System.IO.File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName),
            $$"""{ "id": "{{id}}", "hooks": { "command": ["node", "${plugin}/land.mjs"], "points": [{{points}}] } }""");
        if (!enabled) PluginState.Disable(_home, id);
        return Facts with { Plugins = PluginCatalog.Load(_home) };
    }

    /// <summary>The proposal's file written, judged against the machine, and applied through stand-in doors that record each call.</summary>
    protected async Task<(HelpApplied Applied, HelpStandInDoors Doors, List<string> Later)> ApplyAsync(
        HelpProposal proposal, HelpStandInDoors? doors = null, HelpMachineFacts? facts = null)
    {
        File(proposal.Id, proposal.Kind, proposal.Door, proposal.Target, proposal.Workspace, proposal.Value, proposal.Sentence);
        doors ??= new HelpStandInDoors();
        var later = new List<string>();
        var plan = HelpProposals.Plan(proposal, doors.Config, facts ?? Machine);
        var applied = await HelpProposals.ApplyAsync(_home, proposal, plan, doors, later.Add, CancellationToken.None);
        return (applied, doors, later);
    }
}

/// <summary>
/// The doors a proposal is applied through, each a stand-in that records the call (HELP6). Like
/// <see cref="IHelpDoors"/>, its doors are grouped by kind, in the order of the per-kind interfaces (MOD6, PART5): one
/// class, because a test sets a kind's answer in an object initializer (<c>new HelpStandInDoors { Deleted = … }</c>).
/// </summary>
public sealed class HelpStandInDoors : IHelpDoors
{
    public List<string> Calls { get; } = [];

    // IHelpAcceptDoors
    public (bool Ok, string Message) Accepted { get; set; } =
        (true, "Accepted the departure on quest `#q3done00`: what it held goes on.");

    public Task<(bool Ok, string Message)> AcceptDepartureAsync(string id, CancellationToken ct)
    {
        Calls.Add($"POST /api/quests/{id}/accept");
        return Task.FromResult(Accepted);
    }

    // IHelpAccountDoors
    public AgentSettingsRead SetAgentSettings(string harness, string account, AgentSettingEdit? model, AgentSettingEdit? effort)
    {
        string Said(AgentSettingEdit? edit) => edit is null ? "-" : edit.Value ?? "clear";
        Calls.Add($"SET_AGENT_SETTINGS {harness} {account} {Said(model)} {Said(effort)}");
        return new AgentSettingsRead(model?.Value, effort?.Value, [], null);
    }

    // IHelpAgentDoors
    public Action<int, string?>? Ended { get; private set; }

    /// <summary>An action that ends before its start is answered: a pin already installed does.</summary>
    public bool EndsAtOnce { get; init; }

    public Task StartAgentActionAsync(string harness, string action, string? version, Action<int, string?> ended, CancellationToken ct)
    {
        Calls.Add($"HARNESS_ACTION {harness} {action} {version}".TrimEnd());
        Ended = ended;
        if (EndsAtOnce) ended(0, null);
        return Task.CompletedTask;
    }

    /// <summary>What the default's door refuses with, as `HARNESS_ACTION` would; null to make it.</summary>
    public string? DefaultRefusal { get; init; }

    public Task SetDefaultAccountAsync(string harness, string account, string? workspace, CancellationToken ct)
    {
        if (DefaultRefusal is { } refused) throw new DriverException(refused);
        Calls.Add($"HARNESS_ACTION {harness} profile-default {account} {workspace}".TrimEnd());
        return Task.CompletedTask;
    }

    /// <summary>What the use's door refuses with, as `ACCOUNT_USE` would; null to make it (TOOL4g).</summary>
    public string? UseRefusal { get; init; }

    public Task SetAccountUseAsync(string harness, UseChange change, string? workspace, CancellationToken ct)
    {
        if (UseRefusal is { } refused) throw new DriverException(refused);
        // SWITCH1d: the switch and each window of the curve, as `session=0:90;weekly=0:30,100:100`.
        var curve = change.Curve is { } set
            ? string.Join(";", UseCurve.Windows.Where(window => set[window] is not null)
                .Select(window => $"{window}={string.Join(",", set[window]!.Select(point => $"{point.Elapsed}:{point.Used}"))}"))
            : "";
        Calls.Add($"ACCOUNT_USE {harness} use={change.Use} keep={change.Keep} noKeep={change.NoKeep} early={change.Early} near={change.Near} "
            + $"switch={change.Switch} curve={curve} noCurve={change.NoCurve} {workspace}".TrimEnd());
        return Task.CompletedTask;
    }

    /// <summary>What the order's door refuses with, as `ACCOUNT_USE` would; null to make it (TOOL4l-b).</summary>
    public string? OrderRefusal { get; init; }

    /// <summary>Whether the account was still cooling when the ready's door ended it (TOOL4l-b).</summary>
    public bool ReadyEnded { get; init; } = true;

    public Task<bool> ReadyAccountAsync(string harness, string? account, CancellationToken ct)
    {
        Calls.Add($"ACCOUNT_USE {harness} ready {account ?? "own"}");
        return Task.FromResult(ReadyEnded);
    }

    public Task SetAccountOrderAsync(string harness, IReadOnlyList<string> accounts, bool clear, string? workspace, CancellationToken ct)
    {
        if (OrderRefusal is { } refused) throw new DriverException(refused);
        Calls.Add($"ACCOUNT_USE {harness} order {string.Join(",", accounts)} clear={clear} {workspace}".TrimEnd());
        return Task.CompletedTask;
    }

    // IHelpAskDoors
    public Task<AskAnswer> AskAsync(string workspace, string sentence, string? review, string? reviewWords, CancellationToken ct, string? shortTitle = null)
    {
        var chosen = review is null ? "" : $" [review {review}{(reviewWords is null ? "" : $": {reviewWords}")}]";
        Calls.Add($"ask {workspace} {sentence}{chosen}{(shortTitle is null ? "" : $" [short {shortTitle}]")}");
        return Task.FromResult(new AskAnswer(true, "Asked.", "a1", null));
    }

    // IHelpBrowserDoors
    /// <summary>What the browser's door refuses with, as the Browser screen's route would; null to make the change.</summary>
    public string? BrowserRefusal { get; init; }

    public void ChangeBrowser(string setting, string value, string? address, string? title)
    {
        if (BrowserRefusal is { } refused) throw new DriverException(refused);
        Calls.Add($"BROWSER {setting} {value} {address} {title}".TrimEnd());
    }

    // IHelpClearDoors
    /// <summary>What the screen's clear answers; by default every unit sent was listed and none went.</summary>
    public HistoryOutcome? HistoryCleared { get; set; }

    public Task<HistoryOutcome> ClearHistoryAsync(HistoryScope scope, string id, IReadOnlyList<HistoryUnitName> units, CancellationToken ct)
    {
        Calls.Add($"HISTORY_CLEAR {HistoryClearing.Word(scope)} {id} {string.Join(",", units.Select(unit => $"{unit.Kind}:{unit.Id}"))}");
        return Task.FromResult(HistoryCleared ?? new HistoryOutcome(scope, id) { Listed = units.Count });
    }

    // IHelpDeleteDoors
    public (bool Ok, string Message) Deleted { get; set; } = (true, "Deleted it.");

    public Task<(bool Ok, string Message)> DeleteQuestAsync(string id, CancellationToken ct)
    {
        Calls.Add($"DELETE /api/quests/{id}");
        return Task.FromResult(Deleted);
    }

    public Task<(bool Ok, string Message)> DeleteAskAsync(string id, CancellationToken ct)
    {
        Calls.Add($"DELETE /api/asks/{id}");
        return Task.FromResult(Deleted);
    }

    public Task<(bool Ok, string Message)> DeleteSessionAsync(string id, CancellationToken ct)
    {
        Calls.Add($"SESSION_DELETE {id}");
        return Task.FromResult(Deleted);
    }

    // IHelpHandDoors
    /// <summary>What the hand-off's door answers (WSR5b): pushed by default, or its refusal in its own words.</summary>
    public TreeHand? HandAnswer { get; init; }

    public Task<TreeHand> HandAsync(string repository, string branch, string? plugin, CancellationToken ct)
    {
        Calls.Add($"HANDOFF {repository} {branch} {plugin ?? "(the rule's)"}".TrimEnd());
        return Task.FromResult(HandAnswer ?? new TreeHand(true, $"handed `{branch}` to plugin `example.lands`. Plugin `example.lands`: pushed it.", branch));
    }

    // IHelpPauseDoors
    public (bool Ok, string Message) WorkPaused { get; set; } = (true, "Paused it.");

    public (bool Ok, string Message) WorkResumed { get; set; } = (true, "Resumed it.");

    public Task<(bool Ok, string Message)> PauseWorkAsync(WorkScope scope, string id, CancellationToken ct)
    {
        Calls.Add($"WORK_PAUSE {WorkPausing.Word(scope)} {id}");
        return Task.FromResult(WorkPaused);
    }

    public Task<(bool Ok, string Message)> ResumeWorkAsync(WorkScope scope, string id, CancellationToken ct)
    {
        Calls.Add($"WORK_RESUME {WorkPausing.Word(scope)} {id}");
        return Task.FromResult(WorkResumed);
    }

    // IHelpPluginDoors
    /// <summary>What the plugin door refuses in its own words, for a plugin that moved since the card was drawn.</summary>
    public string? PluginRefusal { get; init; }

    public void AddPlugin(string folder)
    {
        Calls.Add($"PLUGIN_ADD {folder}");
        if (PluginRefusal is { } refused) throw new DriverException(refused);
    }

    public void SwitchPlugin(string id, bool on) => Calls.Add($"PLUGIN_ACTION {(on ? "enable" : "disable")} {id}");

    public void AddOffer(string id)
    {
        Calls.Add($"PLUGIN_INSTALL {id}");
        if (PluginRefusal is { } refused) throw new DriverException(refused);
    }

    public Task UpdatePluginAsync(string id, CancellationToken ct)
    {
        Calls.Add($"PLUGIN_UPDATE {id}");
        return PluginRefusal is { } refused ? Task.FromException(new DriverException(refused)) : Task.CompletedTask;
    }

    // IHelpRepositoryDoors
    public (bool Ok, string Message) Wired { get; set; } = (true, "`engine` is now in workspace `studio`.");

    public Task<(bool Ok, string Message)> WireRepositoryAsync(string repository, string workspace, CancellationToken ct)
    {
        Calls.Add($"POST /api/registry/{repository}/workspace {workspace}");
        return Task.FromResult(Wired);
    }

    // IHelpSessionDoors
    public (bool Ok, string Message) Archived { get; set; } = (true, "Archived it.");

    public Task<(bool Ok, string Message)> ArchiveSessionsAsync(IReadOnlyList<string> ids, bool archived, CancellationToken ct)
    {
        Calls.Add($"SESSION_ARCHIVE {(archived ? "archive" : "unarchive")} {string.Join(",", ids)}");
        return Task.FromResult(Archived);
    }

    // IHelpSettingDoors
    public DriverConfig Config { get; private set; } = DriverConfig.Empty;

    public void Change(Func<DriverConfig, DriverConfig> edit)
    {
        Config = edit(Config);
        Calls.Add("change");
    }

    // IHelpSyncDoors
    /// <summary>What the look lists, as <c>TREES_SYNC_PLAN</c> would.</summary>
    public SyncPlan Listed { get; init; } = new([], [], []);

    /// <summary>What the look's door refuses with; null to list.</summary>
    public string? SyncRefusal { get; init; }

    /// <summary>A replay the press stops at a conflict, in the tree layer's words; null for none.</summary>
    public string? Conflict { get; init; }

    public Task<SyncPlan> SyncPlanAsync(string? repository, string? workspace, CancellationToken ct)
    {
        if (SyncRefusal is { } refused) throw new DriverException(refused);
        // BRSCOPE1b: the workspace is recorded only where one is named, so a call without one reads as it always did.
        Calls.Add($"TREES_SYNC_PLAN {repository}".TrimEnd() + (workspace is null ? "" : $" in:{workspace}"));
        return Task.FromResult(Listed);
    }

    public Task<SyncDone> SyncAsync(string? repository, string? workspace, IReadOnlySet<string> only, CancellationToken ct)
    {
        Calls.Add($"TREES_SYNC {repository} {string.Join(' ', only.Order(StringComparer.Ordinal))}" + (workspace is null ? "" : $" in:{workspace}"));
        return Task.FromResult(new SyncDone(
            [.. Listed.Lines.Where(pull => only.Contains($"{pull.Repository}:{pull.Line}")).Select(pull => new PullResult(pull, pull.Moves, "moved"))],
            [.. Listed.Rebases.Where(item => only.Contains($"{item.Repository}:{item.Branch}"))
                .Select(item => Conflict is { } conflict ? new RebaseResult(item, false, conflict) : new RebaseResult(item, item.Replays, "replayed"))],
            [.. Listed.Deletes.Where(item => only.Contains($"{item.Repository}:{item.Branch}")).Select(item => new LandedResult(item, item.Removable, "removed"))]));
    }
}
