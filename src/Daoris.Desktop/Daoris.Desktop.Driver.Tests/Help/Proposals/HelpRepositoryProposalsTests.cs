using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's <c>repository</c> proposal (ENTRY1d1, D161's ENTRY1d note): a registered repository moved to a workspace on
/// this machine, judged against the registry as the service answers it and applied through the local host's own
/// <c>POST /api/registry/{repository}/workspace</c>, the door the Manage drawer's *Move to workspace* uses. A move needs no
/// path (D48 §7): it edits one field of the registry's row.
/// </summary>
public sealed class HelpRepositoryProposalsTests : HelpProposalsFixture
{
    /// <summary>The registry as the driver reads it: <c>engine</c> in <c>work</c>, <c>game</c> in the default workspace.</summary>
    private static readonly HelpMachineFacts Registry = Facts with
    {
        Registered = [("engine", "work", "/checkouts/engine"), ("game", null, null)],
    };

    private static HelpProposal Move(string? repository, string? workspace) => Of("repository", "wire", repository, workspace);

    [Fact]
    public void A_move_to_another_workspace_is_proposed_and_says_what_changes()
    {
        var plan = HelpProposals.Plan(Move("engine", "default"), DriverConfig.Empty, Registry);

        Assert.Null(plan.Refusal);
        Assert.Equal(
            "Move `engine` from workspace `work` to workspace `default` on this machine: one row of the registry changes, at "
            + "once and only here, and no file is touched.",
            plan.Describe);
        Assert.Equal(new HelpRepositoryMove("engine", "work", "default"), plan.Move);
    }

    /// <summary>A name no repository is in yet is a workspace the move starts, as the drawer's free text allows.</summary>
    [Fact]
    public void A_move_to_a_new_workspace_is_allowed_and_the_card_says_it_starts_it()
    {
        var plan = HelpProposals.Plan(Move("game", "  studio "), DriverConfig.Empty, Registry);

        Assert.Null(plan.Refusal);
        Assert.Contains("Move `game` from workspace `default` to workspace `studio` on this machine", plan.Describe);
        Assert.Contains("No repository is in `studio` yet, so the move starts it here.", plan.Describe);
        Assert.Equal(new HelpRepositoryMove("game", "default", "studio"), plan.Move);
    }

    /// <summary>The registry's own spelling of the name is what the route is sent, whatever case the helper wrote.</summary>
    [Fact]
    public void The_repository_is_named_as_the_registry_spells_it()
    {
        var plan = HelpProposals.Plan(Move("Engine", "default"), DriverConfig.Empty, Registry);

        Assert.Null(plan.Refusal);
        Assert.Equal("engine", plan.Move!.Repository);
    }

    [Theory]
    [InlineData("engine", "work", "`engine` is already in workspace `work`, so there is nothing to move.")]
    [InlineData("engine", " WORK ", "`engine` is already in workspace `work`, so there is nothing to move.")]
    [InlineData("game", "default", "`game` is already in workspace `default`, so there is nothing to move.")]
    [InlineData("atelier", "work", "`atelier` is not registered on this machine — use a repository's name as Repositories lists it.")]
    [InlineData("", "work", "a move names the repository, as Repositories lists it.")]
    [InlineData("engine", " ", "a move names the workspace to move `engine` to.")]
    public void A_move_the_route_would_not_make_is_refused_in_its_words(string repository, string workspace, string refusal)
    {
        var plan = HelpProposals.Plan(Move(repository, workspace), DriverConfig.Empty, Registry);

        Assert.Equal(refusal, plan.Refusal);
        Assert.Null(plan.Move);
    }

    [Fact]
    public void A_door_that_is_not_a_move_is_refused()
    {
        var plan = HelpProposals.Plan(Of("repository", "add", "engine", "work"), DriverConfig.Empty, Registry);

        Assert.Equal("`add` is not a repository's change — `wire`, a move to a workspace.", plan.Refusal);
    }

    [Fact]
    public async Task A_move_goes_through_the_hosts_own_workspace_door_and_its_refusal_is_said_in_the_services_words()
    {
        var (applied, doors, _) = await ApplyAsync(Move("Engine", "studio"), facts: Registry);
        var refusing = new HelpStandInDoors { Wired = (false, "`engine` belongs to workspace `aurora`, and this host serves `tools`.") };
        var (refused, _, _) = await ApplyAsync(Move("engine", "default") with { Id = "p9" }, refusing, Registry);

        Assert.True(applied.Applied);
        Assert.Equal(["POST /api/registry/engine/workspace studio"], doors.Calls);
        Assert.Equal("Applied: `#p6` — `engine` is now in workspace `studio`.", applied.Told);
        Assert.Equal("applied", HelpProposals.Find(_home, "p6")!.State);
        Assert.False(refused.Applied);
        Assert.Equal("Not applied: `#p9` — `engine` belongs to workspace `aurora`, and this host serves `tools`.", refused.Told);
        Assert.Equal("refused", HelpProposals.Find(_home, "p9")!.State);
    }

    /// <summary>
    /// The driver's client sends the drawer's own request, and says the host's answer: the workspace it took, or its refusal
    /// verbatim; a host older than the door is said to be, never read as a move.
    /// </summary>
    [Fact]
    public async Task The_client_posts_the_drawers_own_request_and_reads_the_hosts_answer()
    {
        var host = new RegistryHost();
        using var client = new ServiceClient("http://stand-in", null, new HttpClient(host));

        var moved = await client.WireRepositoryAsync("engine", "studio", CancellationToken.None);
        var unknown = await client.WireRepositoryAsync("atelier", "studio", CancellationToken.None);
        var older = await client.WireRepositoryAsync("old host", "studio", CancellationToken.None);

        Assert.Equal((true, "`engine` is now in workspace `studio`."), moved);
        Assert.Equal((false, "`atelier` is not registered here — `daoris connect` from inside it, or add it from Repositories."), unknown);
        Assert.False(older.Ok);
        Assert.Contains("has no workspace door for a repository (405)", older.Message);
        Assert.Equal(("/api/registry/engine/workspace", "studio"), host.Heard[0]);
        Assert.Equal("/api/registry/old%20host/workspace", host.Heard[2].Path);
    }

    /// <summary>The local host's re-wiring door, standing in: a move it takes, a name it does not hold, and a host from before the door.</summary>
    private sealed class RegistryHost : HttpMessageHandler
    {
        public List<(string Path, string? Workspace)> Heard { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var workspace = body.RootElement.GetProperty("workspace").GetString();
            Heard.Add((request.RequestUri.AbsolutePath, workspace));
            var (status, answer) = path switch
            {
                "/api/registry/engine/workspace" when request.Method == HttpMethod.Post => (HttpStatusCode.OK,
                    JsonSerializer.Serialize(new { repository = "engine", at = "2026-10-11T10:00:00Z", workspace })),
                "/api/registry/atelier/workspace" => (HttpStatusCode.NotFound, JsonSerializer.Serialize(new
                {
                    error = "`atelier` is not registered here — `daoris connect` from inside it, or add it from Repositories.",
                })),
                _ => (HttpStatusCode.MethodNotAllowed, ""),
            };
            return new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }
}

public sealed partial class HelpStandInDoors
{
    public (bool Ok, string Message) Wired { get; set; } = (true, "`engine` is now in workspace `studio`.");

    public Task<(bool Ok, string Message)> WireRepositoryAsync(string repository, string workspace, CancellationToken ct)
    {
        Calls.Add($"POST /api/registry/{repository}/workspace {workspace}");
        return Task.FromResult(Wired);
    }
}
