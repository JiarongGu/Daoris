using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOW1i-b (the workflow design §4.3, D157's WORKFLOW1i-a note): the intake's room says, per repository, which kinds its
/// workspace declares, the workflow each follows there and which lower the person's part, and its instruction says it may
/// propose a kind and never set one. Runs no process: the fast half (MOD8).
/// </summary>
public sealed class IntakeKindTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-intake-kinds-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly IReadOnlyList<DeclarationView> Circle =
    [
        new("engine", Adopted: true, Registered: true, "The engine.", ["the core"], [], Root: null),
        new("tools", Adopted: true, Registered: true, "The tools.", ["scripts"], [], Root: null),
    ];

    private DriverConfig Config()
    {
        WorkflowStore.Add(_home, new WorkflowVersionAdded("docs-fast", "Documentation, fast", "terminal", "2026-10-09T09:00:00Z",
            JsonNode.Parse("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"automatic","pattern":"docs/{quest}"}]""")), kept: []);
        WorkflowStore.Add(_home, new WorkflowVersionAdded("careful", "Careful", "terminal", "2026-10-09T09:00:00Z",
            JsonNode.Parse("""[{"id":"work","kind":"work"},{"id":"look","kind":"look"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""")), kept: []);
        var config = WorkflowSelection.Apply(DriverConfig.Empty, new WorkflowDeclare("docs", "work", "Documentation", ["docs/**"]));
        config = WorkflowSelection.Apply(config, new WorkflowDeclare("feature", "work", "Feature", []));
        config = WorkflowSelection.Apply(config, new WorkflowUse("docs-fast", null, "work", "docs"));
        // A repository that chooses any workflow replaces its workspace's whole (§4.1), so the kind no longer reaches it.
        return WorkflowSelection.Apply(config, new WorkflowUse("careful", "tools", null, null));
    }

    [Fact]
    public void Each_repository_reads_the_workspaces_kinds_against_what_it_follows_with_none()
    {
        var config = Config();
        var read = IntakeKinds.Read(_home, config, "work", ["engine", "tools"]);

        var engine = read[0];
        Assert.Equal(("engine", "Current"), (engine.Repository, engine.Otherwise));
        var docs = engine.Kinds.Single(kind => kind.Kind == "docs");
        Assert.Equal(("Documentation", "`docs-fast` v1"), (docs.Label, docs.Workflow));
        Assert.Equal(["docs/**"], docs.Paths);
        var steps = WorkflowStore.Load(_home, "docs-fast")!.Read.Versions.Last().Steps!;
        var current = Involvement.StepsOf(WorkflowCurrent.Derive(config, "engine", "work", []));
        Assert.Equal(Involvement.Lowers(steps, current), docs.Lowers);
        // A kind the workspace maps nowhere follows what the repository does with none: it lowers nothing.
        Assert.Empty(engine.Kinds.Single(kind => kind.Kind == "feature").Lowers!);

        var tools = read[1];
        Assert.Equal("`careful` v1", tools.Otherwise);
        Assert.All(tools.Kinds, kind => Assert.Empty(kind.Lowers!));
    }

    [Fact]
    public void A_workspace_that_declares_no_kind_reads_none()
    {
        Assert.Empty(IntakeKinds.Read(_home, DriverConfig.Empty, "work", ["engine"]));
    }

    [Fact]
    public void A_kinds_workflow_that_is_not_saved_here_reads_as_unread_lowering()
    {
        var config = WorkflowSelection.Apply(DriverConfig.Empty, new WorkflowDeclare("docs", "work", "Documentation", []));
        config = WorkflowSelection.Apply(config, new WorkflowUse("gone-flow", null, "work", "docs"));

        var docs = Assert.Single(Assert.Single(IntakeKinds.Read(_home, config, "work", ["engine"])).Kinds);

        Assert.Equal(("`gone-flow`", null), (docs.Workflow, docs.Lowers));
    }

    [Fact]
    public void The_room_lists_the_kinds_per_repository_and_which_lower_the_persons_part()
    {
        var kinds = IntakeKinds.Read(_home, Config(), "work", ["engine", "tools"]);

        var room = IntakeRoom.Render("work", Circle, kinds: kinds);

        Assert.Contains("## Kinds of work\n", room);
        Assert.Contains("`workflowProposal`", room);
        Assert.Contains("propose", room);
        Assert.Contains("### `engine` — with no kind it follows Current\n", room);
        Assert.Contains("- `docs` (Documentation), paths `docs/**`: `docs-fast` v1 — lowers your part: ", room);
        Assert.Contains("- `feature` (Feature): Current — keeps your part\n", room);
        Assert.Contains("### `tools` — with no kind it follows `careful` v1\n", room);
        Assert.Contains("- `docs` (Documentation), paths `docs/**`: `careful` v1 — keeps your part\n", room);
    }

    [Fact]
    public void A_room_with_no_kinds_says_nothing_of_them()
    {
        Assert.DoesNotContain("Kinds of work", IntakeRoom.Render("work", Circle));
        Assert.DoesNotContain("Kinds of work", IntakeRoom.Render("work", Circle, kinds: []));
    }

    [Fact]
    public void Prepare_writes_the_kinds_into_the_room()
    {
        var room = IntakeRoom.Prepare(_home, "work", Circle, IntakeKinds.Read(_home, Config(), "work", ["engine"]));

        Assert.Contains("## Kinds of work", File.ReadAllText(Path.Combine(room, "AGENTS.md")));
    }

    [Fact]
    public void The_intake_may_propose_a_kind_never_set_one_and_lowers_only_on_the_persons_words()
    {
        var flat = string.Join(' ', IntakePrompt.Compose(IntakePromptGoldenTests.Full).Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries));

        Assert.Contains("`workflowProposal`", flat);
        Assert.Contains("a `kind` its workspace declares, and its `reason` the person's own words, copied exactly", flat);
        Assert.Contains("never set a kind", flat);
        Assert.Contains("a kind the room says lowers the person's part only where the person's words say so", flat);
        Assert.Contains("the person's press applies a proposal", flat);
    }
}
