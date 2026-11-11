using Daoris.Desktop.Driver.Tests;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>What every kind's proposal tests stand on: a home, the file as the box writes it, the machine, and an Apply through <see cref="HelpStandInDoors"/>.</summary>
public abstract class HelpProposalsFixture : IDisposable
{
    protected readonly string _home = Path.Combine(TestHomes.Root, "daoris-help-plans-" + Guid.NewGuid().ToString("N")[..8]);

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

    protected string Beside(string suffix)
    {
        var folder = _home + suffix;
        _scratch.Add(folder);
        return folder;
    }

    protected static readonly HelpMachineFacts Facts = new(
        Repositories: ["engine", "game"], Workspaces: ["default", "work"], Agents: ["claude-code", "claude-code-acp"])
    {
        Parked = [new ParkedQuest("q1a2b3c4", "engine")],
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

    /// <summary>The machine as the Agents screen, the quest drawer and the ask's record read it, each record with its <c>deletable</c>.</summary>
    protected static readonly HelpMachineFacts Machine = Facts with
    {
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
    /// This machine's session records, as a go naming a session is judged against them.
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
    };

    protected static HelpProposal Of(string kind, string door, string? target = null, string? value = null) =>
        new("p6", kind, door, target, null, value, null, "the person asked", "h1", "proposed");

    /// <summary>A plugin installed under the test's home, as `daoris plugin add` leaves one.</summary>
    protected HelpMachineFacts WithPlugin(string id, string points = "\"work/land\"", bool enabled = true)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins,id);
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
/// The doors a proposal is applied through, each a stand-in that records the call. Like
/// <see cref="IHelpDoors"/>, its doors are grouped by kind, in the order of the per-kind interfaces: one
/// class, because a test sets a kind's answer in an object initializer (<c>new HelpStandInDoors { Deleted = … }</c>).
/// </summary>
public sealed class HelpStandInDoors : IHelpDoors
{
    public List<string> Calls { get; } = [];

    /// <summary>What the ask door answers (the ids it names are what the conversation's list keeps); by default an ask and no quest.</summary>
    public AskAnswer Asked { get; set; } = new(true, "Asked.", "a1", null);

    public Task<AskAnswer> AskAsync(string workspace, string sentence, string? review, string? reviewWords, CancellationToken ct, string? shortTitle = null)
    {
        var chosen = review is null ? "" : $" [review {review}{(reviewWords is null ? "" : $": {reviewWords}")}]";
        Calls.Add($"ask {workspace} {sentence}{chosen}{(shortTitle is null ? "" : $" [short {shortTitle}]")}");
        return Task.FromResult(Asked);
    }

    public DriverConfig Config { get; private set; } = DriverConfig.Empty;

    public void Change(Func<DriverConfig, DriverConfig> edit)
    {
        Config = edit(Config);
        Calls.Add("change");
    }

    public void SetActor(string actor, IReadOnlyList<ActorChoice>? choices)
    {
        Config = ActorChoices.With(Config, actor, choices);
        Calls.Add("change");
    }
}
