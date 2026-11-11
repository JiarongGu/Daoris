using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris repository excludes</c> at the driver: a folder's globs edited (each trimmed and kept once, a removal before an addition),
/// sent whole to the service's excludes door, refused in its own words where the name is no registered folder, and the folder's manifest
/// read again with the new list where this machine's service holds its root.
/// </summary>
public sealed class FolderExcludesPressTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "folder-excludes-press", Guid.NewGuid().ToString("N")[..8]);
    private readonly string _folder;
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public FolderExcludesPressTests()
    {
        _folder = Path.Combine(_home, "scratch");
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "notes.txt"), "kept");
        File.WriteAllText(Path.Combine(_folder, "debug.log"), "left out");
    }

    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public void An_edit_removes_before_it_adds_and_keeps_each_glob_once_in_order()
    {
        Assert.Equal(["a", "c", "d"], ExcludesEdit.Apply(["a", "b", " c ", "a"], [" d ", "c", ""], ["b"]));
        Assert.Empty(ExcludesEdit.Apply(["a"], [], ["a"]));
    }

    [Fact]
    public async Task The_whole_new_list_goes_to_the_service_and_the_manifest_is_read_again_with_it()
    {
        using var service = Client(Registry("folder", ["build/**"]), out var doors);

        var exit = await RepositoryPresses.ExcludesAsync("Scratch", ["*.log"], ["build/**"], _home, service, _out, _error);

        Assert.Equal(0, exit);
        var (verb, path, body) = Assert.Single(doors.Asked, call => call.Verb == ServiceVerb.Post);
        Assert.Equal("/api/registry/scratch/excludes", path);
        Assert.Equal(["*.log"], JsonNode.Parse(body!)!["excludes"]!.AsArray().Select(glob => glob!.GetValue<string>()));
        Assert.Contains("Its manifest was read again with them.", _out.ToString());
        var kept = new ManifestStore(_home, "default", "scratch").Last();
        Assert.Equal(["*.log"], kept!.Excludes);
        Assert.Null(kept.Listed("debug.log"));
        Assert.NotNull(kept.Listed("notes.txt"));
    }

    [Theory]
    [InlineData("git", "is not a folder")]
    [InlineData(null, "is not registered on this machine")]
    public async Task A_name_that_is_no_registered_folder_is_refused_and_nothing_is_sent(string? kind, string words)
    {
        using var service = Client(kind is null ? "[]" : Registry(kind, []), out var doors);

        var exit = await RepositoryPresses.ExcludesAsync("scratch", ["*.log"], [], _home, service, _out, _error);

        Assert.Equal(1, exit);
        Assert.Contains(words, _error.ToString());
        Assert.DoesNotContain(doors.Asked, call => call.Verb == ServiceVerb.Post);
    }

    [Fact]
    public async Task The_service_s_refusal_is_said_as_it_was_and_the_manifest_is_left_alone()
    {
        using var service = Client(Registry("folder", []), out var doors, refuse: true);

        var exit = await RepositoryPresses.ExcludesAsync("scratch", ["*.log"], [], _home, service, _out, _error);

        Assert.Equal(1, exit);
        Assert.Contains("excludes are a folder department's", _error.ToString());
        Assert.Null(new ManifestStore(_home, "default", "scratch").Last());
    }

    [Fact]
    public async Task With_no_edit_the_list_is_said_beside_the_defaults()
    {
        using var service = Client(Registry("folder", ["build/**"]), out _);

        Assert.Equal(0, await RepositoryPresses.ExcludesListAsync("scratch", service, _out, _error));

        Assert.Contains("`scratch` leaves out build/**.", _out.ToString());
        Assert.Contains("Beside the defaults: ~$*", _out.ToString());
    }

    private string Registry(string kind, string[] excludes) =>
        new JsonArray(new JsonObject
        {
            ["repository"] = "scratch", ["root"] = _folder, ["kind"] = kind, ["workspace"] = "default",
            ["excludes"] = new JsonArray([.. excludes.Select(glob => (JsonNode)JsonValue.Create(glob))]),
        }).ToJsonString();

    private static ServiceClient Client(string registry, out Doors doors, bool refuse = false)
    {
        doors = new Doors(registry, refuse);
        return new ServiceClient(doors);
    }

    /// <summary>A local service as the client knocks on it: the registry as given, and the excludes door taking or refusing the list.</summary>
    private sealed class Doors(string registry, bool refuse) : IServiceDoors
    {
        public List<(ServiceVerb Verb, string Path, string? Body)> Asked { get; } = [];

        public string BaseUrl => "http://localhost:5177";

        public Task<string> GetAsync(string path, CancellationToken ct) => throw new NotSupportedException();

        public Task<string> PostAsync(string path, string json, CancellationToken ct) => throw new NotSupportedException();

        public Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct)
        {
            Asked.Add((verb, path, json));
            return Task.FromResult(verb == ServiceVerb.Post
                ? refuse
                    ? new ServiceReply(409, """{"error":"`scratch` is not a folder: excludes are a folder department's, for the files its manifest passes by."}""")
                    : new ServiceReply(200, """{"repository":"scratch","workspace":"default"}""")
                : new ServiceReply(200, registry));
        }

        public void Dispose()
        {
        }
    }
}
