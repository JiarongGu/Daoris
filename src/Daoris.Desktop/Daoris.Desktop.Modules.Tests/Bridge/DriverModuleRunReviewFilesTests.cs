using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <c>RUN_REVIEW</c>'s <c>files</c> over the bridge: a branch's or a checkout's record lists the files between its commits, read from the
/// checkout the registry names; any other kind carries none, and a branch with no checkout here lists none rather than failing.
/// </summary>
/// <remarks>The cases that run git are <c>Process</c>.</remarks>
public sealed class DriverModuleRunReviewFilesTests : DriverModuleBridge
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-12T09:00:00Z");

    private const string Graph = """
        {"schemaVersion":2,"entries":["work"],"end":"review",
         "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},{"id":"review","kind":"review","version":1,"reach":"pressed"}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"review"}]}
        """;

    private void Recorded(ReviewRecord record)
    {
        StoredSteps.Add(Home, new WorkflowVersionAdded("docs", "Docs", "terminal", T.ToString("O"), null, System.Text.Json.Nodes.JsonNode.Parse(Graph)), kept: []);
        Assert.True(WorkflowRunBindings.Bind(Home, WorkflowRunBindings.Plan(DriverConfig.Empty, Home, "q1", "engine", "aurora", null, new WorkflowTaskChoice(null, "docs"), [], T)));
        var unit = RunUnits.IdOf("q1");
        new NodeRecordFiles(Home, () => true).Write(new NodeRecord(unit)
        {
            Statuses = new SortedDictionary<string, NodeStatus>(StringComparer.Ordinal) { ["review"] = new(unit, "review", NodeStates.Active) { Attempt = 1, Since = T } },
        });
        ReviewRecords.Keep(Home, record);
    }

    private async Task<DriverModule> UpAsync(string? root)
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandIn(root))));
        return new DriverModule(Bus, loop);
    }

    private static async Task<string> GitAsync(string root, params string[] arguments)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git")
        { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(TestGit.Past(info))!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(output, error);
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await error);
        return (await output).Trim();
    }

    private static async Task<string> CommitAsync(string root, string file, string text)
    {
        await File.WriteAllTextAsync(Path.Combine(root, file), text);
        await GitAsync(root, "add", file);
        await GitAsync(root, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.test", "commit", "--quiet", "-m", text.Trim());
        return await GitAsync(root, "rev-parse", "HEAD");
    }

    [Fact]
    public async Task A_record_of_another_kind_carries_no_files()
    {
        Recorded(new ReviewRecord("q1", "review", 1, ReviewKinds.Answer, "aaaaaaa1", T) { Place = "engine" });
        var module = await UpAsync(null);

        var review = await AnswerAsync(module, "RUN_REVIEW", new { run = "q1", attempt = 0 });

        Assert.True(!review.TryGetProperty("files", out var files) || files.ValueKind == JsonValueKind.Null);
    }

    [Fact]
    [Trait(Category.Name, Category.Process)]
    public async Task A_branchs_files_are_listed_from_the_checkout_the_registry_names()
    {
        var root = Path.Combine(Home, "checkouts", "engine");
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await CommitAsync(root, "notes.md", "one\n");
        await GitAsync(root, "checkout", "--quiet", "-b", "work");
        await CommitAsync(root, "notes.md", "two\n");
        var tip = await CommitAsync(root, "added.md", "new\n");
        Recorded(new ReviewRecord("q1", "review", 1, ReviewKinds.Branch, "bbbbbbb1", T) { Place = "engine", Reference = new() { Branch = "work", Line = "main", Tip = tip } });
        var module = await UpAsync(root);

        var review = await AnswerAsync(module, "RUN_REVIEW", new { run = "q1", attempt = 0 });

        var files = review.GetProperty("files").EnumerateArray().ToDictionary(row => row.GetProperty("path").GetString()!, row => row.GetProperty("change").GetString());
        Assert.Equal(new Dictionary<string, string?> { ["added.md"] = "added", ["notes.md"] = "modified" }, files);
    }

    [Fact]
    public async Task A_branch_with_no_checkout_here_lists_no_files_and_still_answers()
    {
        Recorded(new ReviewRecord("q1", "review", 1, ReviewKinds.Branch, "bbbbbbb1", T) { Place = "engine", Reference = new() { Branch = "work", Line = "main", Tip = "abc" } });
        var module = await UpAsync(null);

        var review = await AnswerAsync(module, "RUN_REVIEW", new { run = "q1", attempt = 0 });

        Assert.Equal("q1", review.GetProperty("run").GetString());
        Assert.True(!review.TryGetProperty("files", out var files) || files.ValueKind == JsonValueKind.Null);
    }

    /// <summary>The service: the registry with <c>engine</c>'s checkout at <paramref name="root"/> (none where null), and nothing else.</summary>
    private sealed class StandIn(string? root) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            object body = request.RequestUri!.AbsolutePath == "/api/registry" && root is not null
                ? new object[] { new { repository = "engine", workspace = "aurora", root } }
                : Array.Empty<object>();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            });
        }
    }
}
