using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSSETUP5's Process-half case (D124 §8): registration follows the line against real git. A scratch repository's line
/// gains a manifest and a lanes file by a <c>merge</c> landing; the checkout then moves to another branch, where the
/// working files say something else. The look after the landing registers what the LINE declares, adopted and declared,
/// for the checkout's root, through the registry door, and asks for the refresh once.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class RegistrationLineProcessTests : IDisposable
{
    private readonly string _scratch = Path.Combine(RepoRoot(), "_fixtures", "registration-line", Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_scratch, "daoris-home");

    public RegistrationLineProcessTests() => Directory.CreateDirectory(Home);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* a straggling git handle */ }
        catch (UnauthorizedAccessException) { /* read-only pack files under .git */ }
    }

    [Fact]
    public async Task A_manifest_a_merge_landing_put_on_the_line_is_registered_while_the_checkout_is_on_another_branch()
    {
        var root = Path.Combine(_scratch, "engine");
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# engine\n");
        await GitAsync(root, "add", ".");
        await GitAsync(root, "commit", "--quiet", "-m", "first");

        // A set-up's session, in a tree of its own, writes the declaration; the press lands it under `merge`.
        var trees = new SessionTrees(Home);
        var opened = await trees.OpenAsync(root, "engine", "default");
        await File.WriteAllTextAsync(Path.Combine(opened.Path, "daoris.json"),
            """{"source":"daoris@0.0.1","packs":[],"domain":{"summary":"The engine","owns":["render"],"accepts":["bugs"]}}""" + "\n");
        await File.WriteAllTextAsync(Path.Combine(opened.Path, "daoris.lanes.json"),
            """{"lanes":[{"id":"core","title":"Core","paths":["src/**"]}]}""" + "\n");
        await GitAsync(opened.Path, "add", "-A");
        await GitAsync(opened.Path, "commit", "--quiet", "-m", "set up");
        var merged = await trees.MergeAsync(opened.Path);
        Assert.True(merged.Merged, merged.Message);
        Assert.Equal(["engine"], RegistryFollowing.Due(Home));

        // The person moves the checkout on, with edits in flight that say something else: neither is the line.
        await GitAsync(root, "checkout", "--quiet", "-b", "elsewhere");
        await File.WriteAllTextAsync(Path.Combine(root, "daoris.json"), """{"source":"s","domain":{"summary":"Not on the line","owns":[],"accepts":[]}}""");
        File.Delete(Path.Combine(root, "daoris.lanes.json"));

        var registry = new StandInRegistry(root);
        using var service = new ServiceClient("http://localhost:5177", null, new HttpClient(registry));
        var report = await RegistrationFollow.FollowAsync(new RegistrationWorld(service, Home, DriverConfig.Empty), RegistryFollowing.Due(Home));

        var followed = Assert.Single(report.Followed);
        Assert.Equal(RegistryOutcome.Registered, followed.Outcome);
        Assert.Equal("main", followed.Line);
        var sent = Assert.Single(registry.Sent);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse($$"""{"repository":"engine","packs":[],"domain":{"summary":"The engine","owns":["render"],"accepts":["bugs"]},"join":false,"shareKnowledge":false,"lanes":[{"id":"core","title":"Core","summary":"","steward":false}],"root":{{System.Text.Json.JsonSerializer.Serialize(root)}}}"""),
            sent), sent.ToJsonString());
        Assert.Equal(1, registry.Refreshes);
        Assert.Empty(RegistryFollowing.Due(Home));
        Assert.Equal(RegistryOutcome.Registered, RegistryFollowing.Read(Home)["engine"].Outcome);
    }

    /// <summary>The registry door, standing in: one unadopted row at the checkout's root, and what was sent to it.</summary>
    private sealed class StandInRegistry(string root) : HttpMessageHandler
    {
        public List<JsonObject> Sent { get; } = [];

        public int Refreshes { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == "/api/registry")
            {
                return Answer(new JsonArray(new JsonObject
                {
                    ["repository"] = "engine", ["workspace"] = "default", ["adopted"] = false, ["root"] = root,
                }));
            }

            if (request.Method == HttpMethod.Post && path == "/api/registry")
            {
                Sent.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!.AsObject());
                return Answer(new JsonObject { ["repository"] = "engine", ["workspace"] = "default" });
            }

            if (request.Method == HttpMethod.Post && path == "/api/refresh")
            {
                Refreshes++;
                return Answer(new JsonObject { ["entries"] = 0 });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Answer(JsonNode payload) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
