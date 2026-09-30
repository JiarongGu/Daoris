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
        Repositories: ["engine", "game"], Workspaces: ["default", "work"], Agents: ["claude-code", "claude-code-acp"]);

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
/// <see cref="IHelpDoors"/>, each kind's test file adds the doors of its kind (MOD6).
/// </summary>
public sealed partial class HelpStandInDoors : IHelpDoors
{
    public List<string> Calls { get; } = [];
}
