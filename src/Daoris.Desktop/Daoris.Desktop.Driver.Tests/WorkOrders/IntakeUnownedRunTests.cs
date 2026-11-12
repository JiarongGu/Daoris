using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An intake that says no department owns the ask, run through the driver by a real stub process over a stand-in for the service's
/// doors an intake crosses. The ask is held with the sentence, nothing is published, no second intake serves it, and an ask a
/// department owns is routed to it with no change.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Network)]
public sealed class IntakeUnownedRunTests : IDisposable
{
    private const string Why = "a billing system, and no repository here declares billing";

    private readonly string _home = Path.Combine(
        TestHomes.Root, "daoris-intake-unowned-" + Guid.NewGuid().ToString("N")[..8]);

    public IntakeUnownedRunTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly IReadOnlyList<DeclarationView> Circle =
    [
        new("media-api", Adopted: true, Registered: true, "The media service and its config.",
            ["media config", "video and image field names"], ["a media bug"], Root: "/work/media-api"),
        new("storefront", Adopted: true, Registered: true, "The storefront.", ["product pages"], ["a UI bug"], Root: null),
    ];

    private DriverConfig IntakeOn() =>
        (StaffedRoles.Config("stub") with
        {
            TestBound = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Agent()] },
        }).StaffIntake("stub");

    private Daoris.Driver.Driver Driver(Service service)
    {
        var adapters = ProviderRegistry.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), IntakeOn(), adapters, _home,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>
    /// The stub intake: it ends its last message with the verdict line for a billing system, with a verdict that names no work for a
    /// malformed one, and otherwise publishes to the repository that owns the work.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "intake-agent.mjs");
        File.WriteAllText(script, """
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }

            const ask = process.env.DAORIS_ASK_ID;
            const session = process.env.DAORIS_SESSION_ID;
            const target = process.env.DAORIS_TARGET ?? '';
            console.log('stub: intake for ask ' + ask + ' as session ' + session);

            if (/billing system/.test(target)) {
              console.log('I read every declaration and none covers it.');
              console.log('');
              console.log('no-department-owns-this: a billing system, and no repository here declares billing');
              process.exit(0);
            }

            if (/malformed verdict/.test(target)) {
              console.log('no-department-owns-this:');
              process.exit(0);
            }

            const answer = await fetch(process.env.DAORIS_SERVICE_URL + '/api/asks/' + ask + '/publish', {
              method: 'POST',
              headers: { 'content-type': 'application/json' },
              body: JSON.stringify({ to: 'media-api', title: 'Read the field name', body: 'The ticket names it.', session }),
            });
            console.log('stub: publish answered ' + answer.status);
            if (!answer.ok) process.exit(1);
            """);
        return script;
    }

    [Fact]
    public async Task An_ask_no_department_owns_is_held_for_ask_daoris_with_the_sentence_and_nothing_is_published()
    {
        await using var service = Service.Start(Circle, "build a billing system");
        var driver = Driver(service);

        var first = await driver.RunOnceAsync();

        Assert.Empty(service.Published);
        Assert.Equal("awaiting-person", service.Session["state"]!.GetValue<string>());
        Assert.Equal(
            "no department owns ask `#a1b2c3`: nothing in this workspace's declarations covers the work. It is held for Ask Daoris — "
            + "ask it to open a department or a room for it (`daoris repository create`, or `daoris place <repository> --room <path>`) — "
            + "or route it yourself with `daoris ask publish a1b2c3 --to <repository>`, or end it with `daoris ask close a1b2c3 "
            + "--reason \"…\"`. The intake said: " + Why,
            service.Session["note"]!.GetValue<string>());
        Assert.Contains(first.Events, line => line.StartsWith("awaiting-person  intake i1", StringComparison.Ordinal) && line.Contains("no department owns ask"));

        // Listed with the sessions that wait on the person, the ask itself neither closed nor routed.
        Assert.Equal("i1", Assert.Single(service.Waiting)["id"]!.GetValue<string>());
        Assert.Equal("Proposed", service.Ask["state"]!.GetValue<string>());

        // No second intake serves it, and the person's own publish ends the parked record as it ends any park.
        await driver.RunOnceAsync();
        Assert.Single(service.Intakes);
        service.Answer("q9");
        await driver.RunOnceAsync();
        Assert.Equal("completed", service.Session["state"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_verdict_with_no_work_named_is_the_ordinary_park_for_the_person()
    {
        await using var service = Service.Start(Circle, "a malformed verdict");

        await Driver(service).RunOnceAsync();

        Assert.Empty(service.Published);
        Assert.Equal("awaiting-person", service.Session["state"]!.GetValue<string>());
        Assert.StartsWith("published nothing: the declarations did not settle ask `#a1b2c3`", service.Session["note"]!.GetValue<string>());
    }

    [Fact]
    public async Task An_ask_a_department_owns_is_routed_to_it_and_completes_with_no_verdict_read()
    {
        await using var service = Service.Start(Circle, "use the media config for the video field name");

        await Driver(service).RunOnceAsync();

        Assert.Equal("media-api", Assert.Single(service.Published)["to"]!.GetValue<string>());
        Assert.Equal("completed", service.Session["state"]!.GetValue<string>());
    }

    /// <summary>One ask and the one session its intake opens, kept as the desk and the ledger keep them, over a real loopback listener.</summary>
    private sealed class Service : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly IReadOnlyList<DeclarationView> _registry;
        private readonly object _gate = new();
        private JsonObject? _session;

        public string Url { get; }

        public JsonObject Ask { get; }

        public List<JsonObject> Intakes { get; } = [];

        public List<JsonObject> Published { get; } = [];

        public JsonObject Session
        {
            get { lock (_gate) return _session!; }
        }

        /// <summary>The sessions the service lists by default: those that have not ended.</summary>
        public IReadOnlyList<JsonObject> Waiting
        {
            get { lock (_gate) return [.. Listed().Where(s => s["state"]!.GetValue<string>() == "awaiting-person")]; }
        }

        private List<JsonObject> Listed() =>
            _session is { } held && held["state"]!.GetValue<string>() is "queued" or "starting" or "working" or "awaiting-person" ? [held] : [];

        private Service(HttpListener listener, string url, IReadOnlyList<DeclarationView> registry, string sentence)
        {
            _listener = listener;
            Url = url.TrimEnd('/');
            _registry = registry;
            Ask = new JsonObject
            {
                ["id"] = "a1b2c3", ["workspace"] = "work", ["sentence"] = sentence, ["state"] = "Proposed", ["tier"] = "declarations",
                ["links"] = new JsonArray(), ["attachments"] = new JsonArray(), ["proposal"] = new JsonArray(),
                ["quests"] = new JsonArray(), ["intake"] = null,
            };
            _serving = ServeAsync();
        }

        public static Service Start(IReadOnlyList<DeclarationView> registry, string sentence)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new Service(listener, url, registry, sentence);
        }

        /// <summary>The person publishes the ask themselves.</summary>
        public void Answer(string quest)
        {
            lock (_gate)
            {
                Ask["state"] = "Published";
                Ask["quests"]!.AsArray().Add(quest);
            }
        }

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }

                var (status, body) = Respond(context.Request);
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        private (int, string) Respond(HttpListenerRequest request)
        {
            var path = request.Url!.AbsolutePath;
            JsonObject Body()
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                return JsonNode.Parse(reader.ReadToEnd())!.AsObject();
            }

            lock (_gate)
            {
                switch (request.HttpMethod, path)
                {
                    case ("GET", "/api/quests"):
                        return (200, "[]");

                    case ("GET", "/api/registry"):
                        return (200, new JsonArray([.. _registry.Select(r => (JsonNode)new JsonObject
                        {
                            ["repository"] = r.Repository, ["adopted"] = r.Adopted, ["registered"] = r.Registered,
                            ["summary"] = r.Summary, ["owns"] = new JsonArray([.. r.Owns.Select(o => (JsonNode)o)]),
                            ["accepts"] = new JsonArray([.. r.Accepts.Select(a => (JsonNode)a)]),
                            ["root"] = r.Root, ["workspace"] = "work",
                        })]).ToJsonString());

                    case ("GET", "/api/sessions"):
                        return (200, new JsonArray([.. Listed().Select(s => s.DeepClone())]).ToJsonString());

                    case ("GET", "/api/asks"):
                        return (200, new JsonArray([Ask.DeepClone()]).ToJsonString());

                    case ("GET", "/api/asks/a1b2c3"):
                        return (200, Ask.ToJsonString());

                    case ("POST", "/api/sessions/intake"):
                    {
                        Intakes.Add(Body());
                        if (Ask["intake"] is not null) return (409, """{"error":"already served"}""");
                        _session = new JsonObject
                        {
                            ["id"] = "i1", ["repository"] = "ask #a1b2c3", ["state"] = "queued", ["kind"] = "chat", ["ask"] = "a1b2c3",
                            ["workspace"] = "work",
                        };
                        Ask["intake"] = "i1";
                        return (200, new JsonObject { ["session"] = _session.DeepClone(), ["message"] = "opened" }.ToJsonString());
                    }

                    case ("POST", "/api/sessions/i1/state"):
                    {
                        var body = Body();
                        _session!["state"] = body["state"]!.GetValue<string>();
                        if (body["note"] is { } note) _session["note"] = note.GetValue<string>();
                        return (200, new JsonObject { ["session"] = _session.DeepClone(), ["message"] = "moved" }.ToJsonString());
                    }

                    case ("POST", "/api/asks/a1b2c3/publish"):
                    {
                        var body = Body();
                        Published.Add(body);
                        Ask["quests"]!.AsArray().Add($"q{Published.Count}");
                        Ask["state"] = "Published";
                        if (body["session"]?.GetValue<string>() is "i1") Ask["tier"] = "intake";
                        return (200, new JsonObject
                        {
                            ["ask"] = Ask.DeepClone(), ["message"] = $"Published quest `#q{Published.Count}`.",
                            ["quest"] = new JsonObject { ["id"] = $"q{Published.Count}" },
                        }.ToJsonString());
                    }

                    default:
                        return (404, $$"""{"error":"the stand-in has no {{request.HttpMethod}} {{path}}"}""");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            _listener.Close();
            try { await _serving; } catch (ObjectDisposedException) { }
        }
    }
}
