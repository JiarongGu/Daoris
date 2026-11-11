using System.Net;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The routes a terminal's person-only verbs press (<c>quest accept|done|delete|reopen|go-on|review|workflow</c>,
/// <c>ask workflow|review</c>, <c>repository retire|wire|import</c>): each runs the verb's own path in this Daoris, whose service client
/// holds the person key, so every call it makes to the service presents that key and the terminal is told the verb's own words.
/// </summary>
public sealed class PersonVerbRoutesTests : DriverModuleBridge, IDisposable
{
    private const string Key = "person-key-under-test";

    private string Checkout => Path.Combine(Home, "..", "checkout-" + Path.GetFileName(Home));

    // Re-implemented so the checkout beside the home, which the base knows nothing of, goes with it.
    public new void Dispose()
    {
        try { Directory.Delete(Checkout, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        base.Dispose();
    }

    public static TheoryData<string, object, int> Presses => new()
    {
        { "QUEST_ACCEPT", new { quest = "q1", said = true }, 0 },
        { "QUEST_DONE", new { quest = "q1", note = "by hand", said = true }, 0 },
        { "QUEST_DELETE", new { quest = "q1", said = true }, 0 },
        { "ASK_DELETE", new { ask = "a1", said = true }, 0 },
        { "ASK_NEW", new { workspace = "default", sentence = "fix the stall", to = "engine", said = true }, 0 },
        { "QUEST_GO_ON", new { quest = "q1", said = true }, 0 },
        // The submission a reopen names is read first, and no quest is held here.
        { "QUEST_REOPEN", new { quest = "q1", said = true }, 1 },
        { "QUEST_REVIEW", new { quest = "q1", verdict = "skipped", said = true }, 1 },
        { "QUEST_WORKFLOW", new { quest = "q1", words = "no longer", said = true }, 0 },
        { "ASK_WORKFLOW", new { ask = "a1", words = "no longer", said = true }, 0 },
        { "ASK_REVIEW", new { ask = "a1", choice = "on", said = true }, 0 },
        { "REGISTRY_RETIRE", new { repository = "engine", said = true }, 0 },
        { "REGISTRY_REWIRE", new { repository = "engine", workspace = "work", said = true }, 0 },
        { "REGISTRY_IMPORT", new { said = true }, 0 },
    };

    /// <summary>A folder's excludes press reads the registry, then sends the whole new list to the excludes door with the person key.</summary>
    [Fact]
    public async Task A_folder_excludes_press_sends_the_new_list_to_the_service_with_the_person_key()
    {
        var standIn = new KeyRecording(request => request.Method == HttpMethod.Get
            ? """[{"repository":"scratch","kind":"folder","excludes":["build/**"],"workspace":"default"}]"""
            : """{"repository":"scratch","workspace":"default"}""");
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://127.0.0.1:1", null, new HttpClient(standIn), personKey: Key));

        var answer = await AnswerAsync(new DriverModule(Bus, loop), "REGISTRY_EXCLUDES", new { repository = "scratch", add = new[] { "*.log" }, said = true });

        Assert.Equal(0, answer.GetProperty("said").GetProperty("code").GetInt32());
        Assert.Equal(("/api/registry/scratch/excludes", Key), Assert.Single(standIn.Heard));
    }

    /// <summary>
    /// A press reaches the service with the person key on every request it makes, and answers the lines the verb said with its exit code;
    /// the key is the loop's client's, never a field of the press.
    /// </summary>
    [Theory]
    [MemberData(nameof(Presses))]
    public async Task A_press_calls_the_service_with_the_person_key_and_answers_the_verbs_words(string route, object payload, int exit)
    {
        var standIn = new KeyRecording(request => request.Method == HttpMethod.Get ? "[]" : """{"message":"Done as the person's.","repository":"engine","workspace":"work"}""");
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://127.0.0.1:1", null, new HttpClient(standIn), personKey: Key));
        var module = new DriverModule(Bus, loop);

        var answer = await AnswerAsync(module, route, payload);

        var said = answer.GetProperty("said");
        Assert.Equal(exit, said.GetProperty("code").GetInt32());
        Assert.NotEmpty(said.GetProperty("out").EnumerateArray().Concat(said.GetProperty("error").EnumerateArray()));
        Assert.NotEmpty(standIn.Heard);
        Assert.All(standIn.Heard, heard => Assert.Equal(Key, heard.Key));
    }

    /// <summary>Not asked for its words, a press answers whether the verb succeeded and every line it said, never the terminal's shape.</summary>
    [Fact]
    public async Task A_press_not_asked_for_words_answers_done_and_the_message()
    {
        var standIn = new KeyRecording(_ => """{"message":"Accepted."}""");
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://127.0.0.1:1", null, new HttpClient(standIn), personKey: Key));

        var answer = await AnswerAsync(new DriverModule(Bus, loop), "QUEST_ACCEPT", new { quest = "#q1" });

        Assert.True(answer.GetProperty("done").GetBoolean());
        Assert.Contains("Accepted.", answer.GetProperty("message").GetString());
        Assert.False(answer.TryGetProperty("said", out _));
        Assert.Equal(Key, Assert.Single(standIn.Heard).Key);
    }

    /// <summary>An installed plugin is replaced wholesale by its own route, the window's add refusing it.</summary>
    [Fact]
    public async Task A_replace_of_an_installed_plugin_is_its_own_route_and_the_add_still_refuses_it()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        var folder = Path.Combine(Checkout, "acme.quiet");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "plugin.json"), """{ "id": "acme.quiet", "name": "Quiet", "version": "1.0.0" }""");

        var added = await AnswerAsync(module, "PLUGIN_ADD", new { folder });
        Assert.Equal("acme.quiet", added.GetProperty("id").GetString());
        Assert.Contains("already installed", await RefusalAsync(module, "PLUGIN_ADD", new { folder }));

        await File.WriteAllTextAsync(Path.Combine(folder, "plugin.json"), """{ "id": "acme.quiet", "name": "Quiet", "version": "1.1.0" }""");
        var replaced = await AnswerAsync(module, "PLUGIN_REPLACE", new { folder });

        Assert.Equal("1.1.0", replaced.GetProperty("version").GetString());
    }

    /// <summary>A service standing in: the person key each request carried is kept, and its answer is the test's.</summary>
    private sealed class KeyRecording(Func<HttpRequestMessage, string> answer) : HttpMessageHandler
    {
        private readonly List<(string Path, string? Key)> _heard = [];

        public IReadOnlyList<(string Path, string? Key)> Heard { get { lock (_heard) return [.. _heard]; } }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            // The registry is the come-up's own read, never the route's.
            if (request.RequestUri!.AbsolutePath != "/api/registry")
            {
                var key = request.Headers.TryGetValues(PersonHeader.Name, out var values) ? values.Single() : null;
                lock (_heard) _heard.Add((request.RequestUri.AbsolutePath, key));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer(request), Encoding.UTF8, "application/json") });
        }
    }
}
