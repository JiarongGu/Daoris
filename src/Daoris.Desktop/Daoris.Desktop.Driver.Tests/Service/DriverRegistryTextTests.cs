using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The port keeps today's text (ONEWAY1u, D194 point 6): each case of <c>daoris connect</c>, <c>retire</c>, <c>wire</c> and <c>import</c>,
/// as the CLI's TypeScript verbs said it before they retire (<c>fixtures/driver-registry-text.json</c>), said again by
/// <see cref="ServiceClient"/>'s operations and <see cref="RegistryWords"/> over a stand-in service: the same lines on each stream, the
/// same exit, and the same requests sent. A row of the <c>command</c> door is the verb's own grammar, which the command tree's table reads.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class DriverRegistryTextTests
{
    private static readonly Lazy<JsonElement> Read = new(() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "driver-registry-text.json"))).RootElement.Clone());

    public static TheoryData<string> Names()
    {
        var names = new TheoryData<string>();
        foreach (var row in Read.Value.GetProperty("rows").EnumerateArray())
        {
            if (row.GetProperty("door").GetString() == "operation") names.Add(row.GetProperty("name").GetString()!);
        }

        return names;
    }

    [Fact]
    public void The_table_holds_every_operation_the_verbs_make()
    {
        var rows = Read.Value.GetProperty("rows").EnumerateArray().ToList();
        Assert.True(rows.Count >= 35, $"only {rows.Count} rows, so this proves little");
        var ops = rows.Where(row => row.TryGetProperty("op", out _)).Select(row => row.GetProperty("op").GetString()).ToHashSet();
        Assert.All((string[])["connect", "retire", "wire", "import"], op => Assert.Contains(op, ops));
        Assert.Contains(rows, row => row.GetProperty("door").GetString() == "command");
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task Each_case_says_today_s_text_and_sends_the_same_requests(string name)
    {
        var row = Read.Value.GetProperty("rows").EnumerateArray().First(each => each.GetProperty("name").GetString() == name);
        var folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "driver-registry-text", Guid.NewGuid().ToString("N")[..8]);
        var root = Path.Combine(folder, "engine");
        Directory.CreateDirectory(root);
        try
        {
            foreach (var file in row.GetProperty("before").EnumerateObject())
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, file.Name))!);
                File.WriteAllText(Path.Combine(root, file.Name), file.Value.GetString());
            }

            var reply = row.TryGetProperty("reply", out var given)
                ? new ServiceReply(given.GetProperty("status").GetInt32(), given.GetProperty("payload").GetRawText())
                : new ServiceReply(200, "{}");
            var doors = new Doors(row.GetProperty("service").GetString()!, reply);
            using var client = new ServiceClient(doors);
            var args = row.TryGetProperty("args", out var each) ? each : default;
            string? Arg(string field) => args.ValueKind == JsonValueKind.Object && args.TryGetProperty(field, out var value) ? value.GetString() : null;
            var dry = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("dry", out var flag) && flag.GetBoolean();

            var said = await SayAsync(row.GetProperty("op").GetString()!, client, root, dry, Arg);

            string Spelled(string text) => text
                .Replace(JsonEncodedText.Encode(root).ToString(), "<root>", StringComparison.Ordinal)
                .Replace(root, "<root>", StringComparison.Ordinal).Replace(root.Replace('\\', '/'), "<root>", StringComparison.Ordinal);
            AssertLines(Expected(row, "out"), said.Out.Select(Spelled).ToList());
            AssertLines(Expected(row, "err"), said.Err.Select(Spelled).ToList());
            Assert.Equal(row.GetProperty("exit").GetInt32(), said.Exit);

            var asked = doors.Asked.Select(call => new JsonObject
            {
                ["method"] = call.Verb.ToString().ToUpperInvariant(),
                ["path"] = call.Path,
                ["body"] = call.Body is null ? null : JsonNode.Parse(Spelled(call.Body)),
            }).ToList();
            var sent = row.GetProperty("calls").EnumerateArray().Select(call => JsonNode.Parse(call.GetRawText())!).ToList();
            Assert.Equal(sent.Count, asked.Count);
            for (var at = 0; at < sent.Count; at++)
            {
                Assert.True(JsonNode.DeepEquals(sent[at], asked[at]), $"request {at} of `{name}`:\n{asked[at]}\nexpected:\n{sent[at]}");
            }
        }
        finally
        {
            GitSeed.Discard(folder);
        }
    }

    /// <summary>What the command makes of one case: the operation, and the words over its result.</summary>
    private static async Task<FileSaid> SayAsync(string op, ServiceClient client, string root, bool dry, Func<string, string?> arg)
    {
        try
        {
            switch (op)
            {
                case "connect" when dry:
                    return FileSaid.Said(RegistryWords.ConnectDry(RegistryConnect.Plan(root, arg("workspace"), client.BaseUrl)));
                case "connect":
                    return FileSaid.Said(RegistryWords.Connected(await client.ConnectAsync(root, arg("workspace"))));
                case "retire" when dry:
                    return FileSaid.Said([RegistryWords.RetireDry(arg("repository")!)]);
                case "retire":
                    return FileSaid.Said([RegistryWords.Retired(await client.RetireAsync(arg("repository")!))]);
                case "wire" when dry:
                    return FileSaid.Said([RegistryWords.WireDry(arg("repository")!, arg("workspace")!)]);
                case "wire":
                {
                    var (ok, message) = await client.WireRepositoryAsync(arg("repository")!, arg("workspace")!);
                    return ok ? FileSaid.Said([$"daoris: {message}"]) : FileSaid.Refused(message, 1);
                }

                case "import" when dry:
                    return FileSaid.Said([RegistryWords.ImportDry(arg("folder"), arg("workspace"))]);
                case "import":
                    return FileSaid.Said([RegistryWords.Imported(await client.ImportAsync(arg("folder"), arg("workspace")))]);
                default:
                    throw new InvalidOperationException($"the table names an operation this test does not make: {op}");
            }
        }
        catch (RegistryRefusal refused)
        {
            return FileSaid.Refused(refused.Message, refused.Exit);
        }
    }

    /// <summary>The lines are equal, a payload the verb printed being equal as JSON.</summary>
    private static void AssertLines(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var at = 0; at < expected.Count; at++)
        {
            if (expected[at].StartsWith('{')) Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected[at]), JsonNode.Parse(actual[at])), $"{actual[at]}\nexpected:\n{expected[at]}");
            else Assert.Equal(expected[at], actual[at]);
        }
    }

    private static IReadOnlyList<string> Expected(JsonElement row, string stream) =>
        [.. row.GetProperty(stream).EnumerateArray().Select(each => each.GetString()!)];

    /// <summary>The host as the client knocks on it: each request kept, each answer the row's.</summary>
    private sealed class Doors(string baseUrl, ServiceReply reply) : IServiceDoors
    {
        public List<(ServiceVerb Verb, string Path, string? Body)> Asked { get; } = [];

        public string BaseUrl => baseUrl;

        public Task<string> GetAsync(string path, CancellationToken ct) => throw new NotSupportedException();

        public Task<string> PostAsync(string path, string json, CancellationToken ct) => throw new NotSupportedException();

        public Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct)
        {
            Asked.Add((verb, path, json));
            return Task.FromResult(reply);
        }

        public void Dispose()
        {
        }
    }
}
