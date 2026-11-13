using Daoris.Contracts.Asks;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What a shown plan's evidence holds on this machine's line, read from git objects: the blob of each cited file, and nothing for what cannot be read.</summary>
[Trait(Category.Parallel, Category.Git)]
public sealed class PlanBlobsTests : IAsyncLifetime
{
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "plan-blobs", Guid.NewGuid().ToString("N")[..8]);
    private string _blob = "";

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "src"));
        await GitFixture.GitAsync(_folder, "init", "-q", "-b", "main");
        await GitFixture.GitAsync(_folder, "config", "user.email", "test@example.com");
        await GitFixture.GitAsync(_folder, "config", "user.name", "Test");
        await File.WriteAllTextAsync(Path.Combine(_folder, "src", "client.ts"), "export const header = 1;\n");
        await GitFixture.GitAsync(_folder, "add", "-A");
        await GitFixture.GitAsync(_folder, "commit", "-q", "-m", "first");
        _blob = (await GitFixture.GitAsync(_folder, "rev-parse", "HEAD:src/client.ts")).Trim();
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        return Task.CompletedTask;
    }

    private static AskPlanResponse Plan(params AskPlanReleaseResponse[] releases) =>
        new("a1", [new(null, 1, 1, "why", [], [], [], [], releases, [], [])], [], []);

    private static AskPlanReleaseResponse Order(string repository, string file) =>
        new("frontend/web-app", "backend/api", "calls it", "code-read", new AskPlanEvidenceResponse(repository, file, "1-9", "old"));

    [Fact]
    public async Task The_blob_each_cited_file_holds_on_the_line_is_keyed_by_its_repository_and_path()
    {
        var registry = new[] { new RepoView("backend", true, _folder) };

        var seen = await PlanBlobs.SeenAsync(Plan(Order("backend", "src/client.ts"), Order("backend", "src/gone.ts"), Order("elsewhere", "x.ts")), registry, DriverConfigReader.Parse("{}"));

        Assert.Equal(new Dictionary<string, string> { ["backend:src/client.ts"] = _blob }, seen);
    }

    [Fact]
    public async Task A_plan_citing_nothing_reads_nothing()
    {
        var seen = await PlanBlobs.SeenAsync(Plan(), [new RepoView("backend", true, _folder)], DriverConfigReader.Parse("{}"));

        Assert.Empty(seen);
    }
}
