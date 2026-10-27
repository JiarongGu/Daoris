using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEEPSIZE1b2 (D195's KEEPSIZE1b notes; the keep-size design §2.2 and §3.3): what KEEPSIZE1b left of its row, the two proofs that read no
/// destructive path. The driver's own client posts a finished tree's record to the service's own host, started in a process of its own over
/// a scratch home (<see cref="RealServiceHost"/>), after the look's real move of a real tree over real git: so the tree path posted is the
/// one the move leaves in the manifest, not one a test wrote, and the sessions are the service's own rows, opened through the client as the
/// driver opens them. The service's store-level cases (<c>WorkRecordStoreTests</c>, <c>WorkRecordHostTests</c>) write the paths themselves.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class KeepSizeServiceProcessTests : KeepSizeFixture, IAsyncLifetime
{
    private RealServiceHost? _host;
    private RecordingDoors? _doors;
    private ServiceClient? _client;

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>The host is stopped before the scratch folder goes (the base's disposal comes after this): it holds its store open.</summary>
    public Task DisposeAsync()
    {
        _client?.Dispose();
        _host?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>The host over a scratch home of its own, <c>engine</c> registered at <paramref name="root"/>, and a client on it.</summary>
    private async Task<ServiceClient> ServiceAsync(string root)
    {
        _host = await RealServiceHost.StartAsync(Path.Combine(_scratch, "service"));
        _doors = new RecordingDoors(new ServiceHttp(_host.Url, key: null));
        _client = new ServiceClient(_doors);
        var (ok, message) = await _client.RegisterAsync(new JsonObject
        {
            ["repository"] = "engine",
            ["root"] = root,
            ["packs"] = new JsonArray(),
            ["domain"] = new JsonObject
            {
                ["summary"] = "An engine.",
                ["owns"] = new JsonArray("its own tree"),
                ["accepts"] = new JsonArray("a quest"),
            },
        });
        Assert.True(ok, message);
        return _client;
    }

    /// <summary>A conversation that ran in <paramref name="tree"/> from <paramref name="baseCommit"/> and ended, as the driver opens and ends one.</summary>
    private async Task<string> RanAsync(string tree, string baseCommit)
    {
        var (id, message) = await _client!.OpenChatAsync("engine", "stub", tree: tree, baseCommit: baseCommit);
        Assert.True(id is not null, message);
        foreach (var state in new[] { "starting", "working", "completed" }) await _client.AdvanceAsync(id!, state);
        return id!;
    }

    /// <summary>The record pass at the real clock, through the driver's client: what the Keeping pass does of a deleted folder.</summary>
    private async Task RecordThroughServiceAsync(SessionTrees trees, string root, DateTimeOffset now) =>
        await trees.RecordSetAsideAsync(Roots(root), _client!.PostWorkRecordAsync, () => now, CancellationToken.None);

    private static async Task<string> HeadOfAsync(string tree) => (await GitAsync(tree, "rev-parse", "HEAD")).Trim();

    /// <summary>
    /// 🔴 A chain's two sessions in one tree, each its own conversation based where the last left the tree (Opus finding 10, Codex finding 6):
    /// the look moves the real tree, the driver's client posts the record with the tree path the move left in the manifest, and the service
    /// names both sessions and gives each the commits made after its own base. <c>WorkRecordStoreTests</c> proves the attribution with the
    /// tree paths it writes; this holds the path the move posts to the path the sessions were opened at.
    /// </summary>
    [Fact]
    public async Task A_chains_two_sessions_are_attributed_through_a_real_moves_posted_tree_path()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await ServiceAsync(root);
        var tree = await trees.OpenAsync(root, "engine", "default");
        var first = await RanAsync(tree.Path, await HeadOfAsync(tree.Path));
        await CommitAsync(tree.Path, "one.txt", "one\n", "the first session's work");
        var firstTip = await HeadOfAsync(tree.Path);
        var second = await RanAsync(tree.Path, firstTip);
        await CommitAsync(tree.Path, "two.txt", "two\n", "the second session's work");
        await CommitAsync(tree.Path, "three.txt", "three\n", "and more of it");
        await GitAsync(root, "merge", "--no-ff", "--no-edit", tree.Branch);

        var aside = await SetAsideAsync(trees, root);
        await RecordThroughServiceAsync(trees, root, DateTimeOffset.UtcNow);

        Assert.Equal(SlimStates.Recorded, Manifest(aside).State);
        var posted = Assert.Single(_doors!.Records);
        var answer = Assert.Single(_doors.Answers);
        Assert.True(answer.GetProperty("kept").GetBoolean());
        var sessions = answer.GetProperty("sessions").EnumerateArray().ToDictionary(
            each => each.GetProperty("session").GetString()!,
            each => each.GetProperty("commits").ValueKind == JsonValueKind.Null
                ? null
                : each.GetProperty("commits").EnumerateArray().Select(commit => commit.GetString()!).ToArray());
        var commits = posted["commits"]!.AsArray().Select(commit => commit!["id"]!.GetValue<string>()).ToArray();
        Assert.Equal(["the first session's work", "the second session's work", "and more of it"], posted["commits"]!.AsArray().Select(commit => commit!["subject"]!.GetValue<string>()));
        Assert.Equal(new HashSet<string> { first, second }, sessions.Keys.ToHashSet());
        Assert.Equal([commits[0]], sessions[first]);
        Assert.Equal([commits[1], commits[2]], sessions[second]);
        // The path the move posts is the one the sessions were opened at: the service reads a session's commits only where the two name one tree.
        Assert.Equal(tree.Path, posted["tree"]!.GetValue<string>());
    }

    /// <summary>
    /// 🔴 <c>whole</c> set in <c>driver.json</c> when the tree moves is the manifest's, and stays its own after the person unsets it (the
    /// keep-size design §3.4): the record is posted and kept, and a purge under the policy the file now gives takes nothing by the hour, the
    /// folder's days from its move instead. The policy at the purge alone (<c>KeepSizeGitTests</c>) never read the manifest's flag.
    /// </summary>
    [Fact]
    public async Task A_whole_set_at_the_move_keeps_the_folder_its_days_after_it_is_unset()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await ServiceAsync(root);
        Keep(whole: true);
        await LandedTreeAsync(trees, root);

        var aside = await SetAsideAsync(trees, root);
        Keep(whole: false);
        var now = DateTimeOffset.UtcNow;
        await RecordThroughServiceAsync(trees, root, now);

        Assert.True(Manifest(aside).Finished!.Whole);
        Assert.Equal(SlimStates.Recorded, Manifest(aside).State);
        Assert.True(Assert.Single(_doors!.Records)["whole"]!.GetValue<bool>());
        Assert.True(Assert.Single(_doors.Answers).GetProperty("kept").GetBoolean());
        var policy = KeepPolicy.Of(_home);
        Assert.False(policy.SetAside.Whole);
        var judged = SetAsideEligibility.Of(aside, now, policy.SetAside.Days, policy.SetAside.Whole);
        Assert.Equal((SetAsideClass.Recorded, false), (judged.Class, judged.Hour));
        Assert.Empty(await PurgeAsync(trees, now.AddHours(2), root, policy));
        Assert.True(Directory.Exists(aside));
        Assert.Equal(SlimStates.Recorded, Manifest(aside).State);
    }

    /// <summary>The person's choice in the scratch home's <c>driver.json</c>, as <c>daoris driver keep</c> writes it.</summary>
    private void Keep(bool whole) =>
        DriverConfig.Load(DriverConfig.ResolvePath(_home)).WithKeep(new KeepSet("set-aside", Whole: whole)).Save(DriverConfig.ResolvePath(_home));

    /// <summary>The real doors, with each record post and the service's answer to it kept for the test to read.</summary>
    private sealed class RecordingDoors(IServiceDoors inner) : IServiceDoors
    {
        private readonly object _gate = new();
        private readonly List<JsonObject> _records = [];
        private readonly List<JsonElement> _answers = [];

        public string BaseUrl => inner.BaseUrl;

        /// <summary>Each body posted to the record door, in order.</summary>
        public IReadOnlyList<JsonObject> Records
        {
            get { lock (_gate) return [.. _records]; }
        }

        /// <summary>Each answer the record door gave, in order.</summary>
        public IReadOnlyList<JsonElement> Answers
        {
            get { lock (_gate) return [.. _answers]; }
        }

        public Task<string> GetAsync(string path, CancellationToken ct) => inner.GetAsync(path, ct);

        public async Task<string> PostAsync(string path, string json, CancellationToken ct)
        {
            var answer = await inner.PostAsync(path, json, ct);
            Keep(path, json, answer);
            return answer;
        }

        public async Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct)
        {
            var reply = await inner.SendAsync(verb, path, json, ct);
            if (verb == ServiceVerb.Post && json is not null) Keep(path, json, reply.Payload);
            return reply;
        }

        private void Keep(string path, string json, string answer)
        {
            if (path != "/api/trees/record") return;
            lock (_gate)
            {
                _records.Add(JsonNode.Parse(json)!.AsObject());
                _answers.Add(JsonDocument.Parse(answer).RootElement.Clone());
            }
        }

        public void Dispose() => inner.Dispose();
    }
}
