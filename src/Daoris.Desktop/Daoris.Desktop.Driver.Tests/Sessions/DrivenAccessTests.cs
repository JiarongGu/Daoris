using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AUTONOMY1b (D184 points 2, 3 and 5): what a driven start gets on its harness under its repository's permission prompts. Auto keeps
/// each harness as it ran (D81); asking runs it in its asking mode and unrestricted in its mode that asks nothing, a flag on the native
/// door and a required mode on the protocol door; a harness with no such mode is refused, naming it. The person's denies and the tree
/// guard go in every mode, and a session going on under another setup says the change. No process, so the fast half.
/// </summary>
/// <remarks>
/// The start itself runs a real process, which only the <c>Process</c> half reaches (<see cref="AnswerContinuesTickTests"/>); these
/// hold its seams: the access each start gets, the spawn's rules, the note it keeps and the record read back.
/// </remarks>
public sealed class DrivenAccessTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-driven-access-" + Guid.NewGuid().ToString("N")[..8]);

    public DrivenAccessTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static ISessionAdapter Adapter(string name) => AdapterSet.Built().Resolve(name);

    /// <summary>A repository's setup with its permission prompts set to <paramref name="value"/> at the repository, the rest today's.</summary>
    private static ResolvedAccess Permissions(string value) => AccessRules.ResolveLevels(
        new AccessSetting { Pauses = new Dictionary<string, string> { [AccessRules.Permissions] = value } }, null, null);

    private static SessionTarget Target(string root) => new(
        QuestId: "q1", Title: "Serve the report", Body: "The report needs a port.", Asker: "ask #a1", Repository: "engine", Root: root,
        ServiceUrl: "http://127.0.0.1:9");

    /// <summary>
    /// 🔴 Per harness, the mode each value starts it in: auto keeps the adapter's own (D81), asking its asking mode, unrestricted its
    /// mode that asks nothing (D183's full postures); a flag on the native door, a mode the protocol door must set or fail on.
    /// </summary>
    [Theory]
    [InlineData("claude-code", AccessRules.Auto, null, null)]
    [InlineData("claude-code", AccessRules.Asking, null, "default")]
    [InlineData("claude-code", AccessRules.Unrestricted, null, "bypassPermissions")]
    [InlineData("claude-code-acp", AccessRules.Auto, null, null)]
    // AUTONOMY1b3a: the asking mode's posture is marked too, so the protocol door holds each request for the person.
    [InlineData("claude-code-acp", AccessRules.Asking, "!?default", null)]
    [InlineData("claude-code-acp", AccessRules.Unrestricted, "!bypassPermissions", null)]
    [InlineData("codex-acp", AccessRules.Auto, null, null)]
    [InlineData("codex-acp", AccessRules.Asking, "!?read-only", null)]
    [InlineData("codex-acp", AccessRules.Unrestricted, "!agent-full-access", null)]
    public void Each_harness_starts_in_the_mode_its_permission_prompts_name(string name, string permissions, string? posture, string? permissionMode)
    {
        var got = DrivenAccess.For(Permissions(permissions), "engine", Adapter(name));

        Assert.Null(got.Refusal);
        Assert.Equal(permissions, got.Permissions);
        Assert.Equal((posture, permissionMode), (got.Posture, got.PermissionMode));
        Assert.StartsWith($"Permission prompts: {permissions} on `{name}`", got.Said);
    }

    /// <summary>Auto is today's on every harness, one with no mode mapped among them: nothing refused, nothing set.</summary>
    [Theory]
    [InlineData("stub")]
    [InlineData("acp-stub")]
    [InlineData("dsh")]
    public void Auto_runs_every_harness_as_it_ran(string name)
    {
        var got = DrivenAccess.For(AccessRules.ResolveLevels(null, null, null), "engine", Adapter(name));

        Assert.Null(got.Refusal);
        Assert.Equal((AccessRules.Auto, (string?)null, (string?)null), (got.Permissions, got.Posture, got.PermissionMode));
    }

    /// <summary>
    /// 🔴 A harness with no mode for the value is refused plainly, naming it, the value and where it was set, and the person's two ways
    /// on: never run in its own mode under that label (D183 point 1).
    /// </summary>
    [Theory]
    [InlineData("stub", AccessRules.Unrestricted, "asks nothing")]
    [InlineData("acp-stub", AccessRules.Unrestricted, "asks nothing")]
    [InlineData("dsh", AccessRules.Unrestricted, "asks nothing")]
    [InlineData("stub", AccessRules.Asking, "asking mode")]
    [InlineData("dsh", AccessRules.Asking, "asking mode")]
    public void A_value_the_harness_has_no_mode_for_refuses_the_start_naming_it(string name, string permissions, string kind)
    {
        var got = DrivenAccess.For(Permissions(permissions), "engine", Adapter(name));

        Assert.NotNull(got.Refusal);
        Assert.Contains($"`{name}`", got.Refusal);
        Assert.Contains($"`{permissions}`", got.Refusal);
        Assert.Contains(kind, got.Refusal);
        Assert.Contains("set for its repository", got.Refusal);
        Assert.Contains("`daoris-driver access engine permissions=auto`", got.Refusal);
        Assert.Contains("another agent", got.Refusal);
        Assert.Equal(((string?)null, (string?)null), (got.Posture, got.PermissionMode));
    }

    /// <summary>
    /// What a start is told of its mode names the harness's own word for it, and whether the person's denies and the tree guard reach
    /// it: Codex takes no rules, so they do not, and says so.
    /// </summary>
    [Fact]
    public void What_a_start_says_names_the_harness_s_own_mode_and_whether_the_denies_reach_it()
    {
        var unrestricted = DrivenAccess.For(Permissions(AccessRules.Unrestricted), "engine", Adapter("claude-code-acp")).Said;
        Assert.Contains("`bypassPermissions`", unrestricted);
        Assert.Contains("asks nothing", unrestricted);
        Assert.Contains("the person's own denies", unrestricted);
        Assert.Contains("tree guard", unrestricted);

        var asking = DrivenAccess.For(Permissions(AccessRules.Asking), "engine", Adapter("claude-code")).Said;
        Assert.Contains("`default`", asking);
        Assert.Contains("only what its rules allow", asking);
        Assert.Contains("refused", asking);

        var codex = DrivenAccess.For(Permissions(AccessRules.Unrestricted), "engine", Adapter("codex-acp")).Said;
        Assert.Contains("`agent-full-access`", codex);
        Assert.Contains("takes no rules", codex);

        var auto = DrivenAccess.For(Permissions(AccessRules.Auto), "engine", Adapter("claude-code-acp")).Said;
        Assert.Contains("`auto` or `acceptEdits`", auto);
        Assert.DoesNotContain('\n', unrestricted + asking + codex + auto);
    }

    /// <summary>
    /// AUTONOMY1b3a (D184 point 3): under asking, the protocol door holds each request for the person to allow once or refuse, and one
    /// nobody answers is refused at its bound; the native door has no wire to hold one on, and says so plainly.
    /// </summary>
    [Fact]
    public void Asking_says_the_protocol_door_holds_each_request_for_the_person_and_the_native_door_cannot()
    {
        var acp = DrivenAccess.For(Permissions(AccessRules.Asking), "engine", Adapter("claude-code-acp")).Said;
        Assert.Contains("waits for the person", acp);
        Assert.Contains($"{(int)HeldPermissions.Bound.TotalMinutes} minutes", acp);
        Assert.DoesNotContain("refuses every", acp);

        var codex = DrivenAccess.For(Permissions(AccessRules.Asking), "engine", Adapter("codex-acp")).Said;
        Assert.Contains("waits for the person", codex);
        Assert.Contains("takes no rules", codex);
        Assert.DoesNotContain("refuses every", codex);

        var native = DrivenAccess.For(Permissions(AccessRules.Asking), "engine", Adapter("claude-code")).Said;
        Assert.Contains("cannot hold", native);
        Assert.Contains("only what its rules allow", native);
        Assert.DoesNotContain('\n', acp + codex + native);
    }

    /// <summary>
    /// 🔴 The person's denies hold in every mode (D184 point 5): the spawn's rules, composed beside the mode and never from it, carry
    /// their deny and the tree guard on both Claude doors under each value, the native door by its flag, the protocol door on its meta.
    /// </summary>
    [Theory]
    [InlineData("claude-code")]
    [InlineData("claude-code-acp")]
    public void The_persons_denies_and_the_tree_guard_go_in_every_mode(string name)
    {
        PermissionRules.Save(
            _home, PermissionRules.Add(PermissionFile.Empty, RuleScope.Machine, null, RuleList.Deny, "Read(//c/secrets/**)"));
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(new ServiceClient("http://127.0.0.1:9", null), DriverConfig.Empty, adapters, _home);
        var adapter = adapters.Resolve(name);
        var tree = Path.Combine(_home, "engine");

        foreach (var permissions in new[] { AccessRules.Asking, AccessRules.Auto, AccessRules.Unrestricted })
        {
            var info = adapter.Prepare(Target(tree) with { Access = DrivenAccess.For(Permissions(permissions), "engine", adapter) }, ["claude"]);

            var (file, meta, _) = driver.HandRules(adapter, info, $"s-{permissions}", null, "engine", tree, kept: null);

            Assert.NotNull(file);
            var handed = JsonNode.Parse(File.ReadAllText(file!))!;
            Assert.Contains("Read(//c/secrets/**)", handed["permissions"]!["deny"]!.AsArray().Select(rule => rule!.GetValue<string>()));
            var hook = handed["hooks"]!["PreToolUse"]![0]!["hooks"]![0]!;
            Assert.Equal(tree, hook["args"]![1]!.GetValue<string>());
            if (adapter.Wire == SessionWire.Pipe) Assert.Equal(file, info.ArgumentList[info.ArgumentList.IndexOf("--settings") + 1]);
            else Assert.NotNull(meta);
        }
    }

    /// <summary>
    /// The start note names the mode the start got, after its setup's line (D183's HELPMODE1a1 note): a start nobody changed notes
    /// nothing, as before, and any other says its setup and then its mode on its harness.
    /// </summary>
    [Fact]
    public void The_start_note_names_the_mode_the_start_got()
    {
        var today = AccessRules.ResolveLevels(null, null, null);
        Assert.Null(DrivenAccess.StartNote(today, DrivenAccess.For(today, "engine", Adapter("claude-code-acp")), prior: null));

        var full = AccessRules.ResolveLevels(AccessSetting.FullAccess, null, null);
        var note = DrivenAccess.StartNote(full, DrivenAccess.For(full, "engine", Adapter("claude-code-acp")), prior: null);

        Assert.NotNull(note);
        Assert.StartsWith("Access setup: full access, set for its repository", note);
        Assert.Contains("Permission prompts: unrestricted on `claude-code-acp`", note);
        Assert.Contains("`bypassPermissions`", note);
        Assert.DoesNotContain('\n', note);
    }

    /// <summary>
    /// 🔴 A session going on under another setup runs under the one standing now and says the change first (D183 point 3, as HELPMODE1a2
    /// says Ask Daoris's): a carry-on's new record, and a resume of the same record. A record from before the setting ran today's, auto,
    /// and a value this build does not know reads as auto too.
    /// </summary>
    [Fact]
    public void A_session_going_on_under_another_setup_says_the_change_first()
    {
        var today = AccessRules.ResolveLevels(null, null, null);
        var auto = DrivenAccess.For(today, "engine", Adapter("claude-code-acp"));
        var unrestricted = DrivenAccess.For(Permissions(AccessRules.Unrestricted), "engine", Adapter("claude-code-acp"));
        PriorSession Ran(string? permissions) => new("s1", null)
        {
            Access = permissions is null ? null : new Dictionary<string, string> { [AccessRules.Permissions] = permissions },
        };

        var carried = DrivenAccess.StartNote(today, auto, Ran(AccessRules.Unrestricted));
        Assert.NotNull(carried);
        Assert.StartsWith("Permission prompts changed since `s1` started: it started under `unrestricted`, and this run goes on under `auto`.", carried);
        Assert.Contains("Permission prompts: auto on `claude-code-acp`", carried);

        var resumed = DrivenAccess.GoingOnNote(Ran(AccessRules.Asking), unrestricted);
        Assert.NotNull(resumed);
        Assert.StartsWith("Permission prompts changed since `s1` started: it started under `asking`, and this run goes on under `unrestricted`.", resumed);
        Assert.Contains("`bypassPermissions`", resumed);

        Assert.Null(DrivenAccess.GoingOnNote(Ran(null), auto));
        Assert.Null(DrivenAccess.GoingOnNote(Ran("sometimes"), auto));
        Assert.Null(DrivenAccess.GoingOnNote(Ran(AccessRules.Unrestricted), unrestricted));
        Assert.Null(DrivenAccess.StartNote(today, auto, Ran(AccessRules.Auto)));
    }

    /// <summary>
    /// 🔴 AUTONOMY1b2 (D184 point 1): a resume under a changed setup tells the service's door the setup it now runs under, each pause with
    /// its value, so the record names it; one under the same setup, or from a record before the setting that ran auto, calls nothing.
    /// </summary>
    [Fact]
    public async Task A_resume_under_a_changed_setup_keeps_the_new_one_on_its_record_and_the_same_keeps_nothing()
    {
        using var listener = new System.Net.HttpListener();
        var port = System.Net.Sockets.TcpListener.Create(0);
        port.Start();
        var number = ((System.Net.IPEndPoint)port.LocalEndpoint).Port;
        port.Stop();
        listener.Prefixes.Add($"http://127.0.0.1:{number}/");
        listener.Start();
        var heard = new List<(string Path, string Body)>();
        var serving = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                System.Net.HttpListenerContext context;
                try { context = await listener.GetContextAsync(); } catch (Exception) { return; }
                using var reader = new StreamReader(context.Request.InputStream);
                var said = await reader.ReadToEndAsync();
                lock (heard) heard.Add((context.Request.Url!.AbsolutePath, said));
                var bytes = System.Text.Encoding.UTF8.GetBytes("""{"session":{"id":"s1"},"message":"kept"}""");
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        });
        var service = new ServiceClient($"http://127.0.0.1:{number}", null);
        var adapter = Adapter("claude-code-acp");
        var now = Permissions(AccessRules.Unrestricted);
        var got = DrivenAccess.For(now, "engine", adapter);
        PriorSession Ran(string? permissions) => new("s1", null)
        {
            Access = permissions is null ? null : new Dictionary<string, string> { [AccessRules.Permissions] = permissions },
        };

        await DrivenAccess.KeepAsync(service, Ran(AccessRules.Unrestricted), got, now, default);
        await DrivenAccess.KeepAsync(service, Ran(null), DrivenAccess.For(AccessRules.ResolveLevels(null, null, null), "engine", adapter),
            AccessRules.ResolveLevels(null, null, null), default);
        lock (heard) Assert.Empty(heard);

        await DrivenAccess.KeepAsync(service, Ran(AccessRules.Asking), got, now, default);

        listener.Stop();
        await serving;
        var (path, body) = Assert.Single(heard);
        Assert.Equal("/api/sessions/s1/access", path);
        var sent = System.Text.Json.Nodes.JsonNode.Parse(body)!["access"]!.AsObject();
        Assert.Equal(now.Values.ToDictionary(p => p.Key, p => p.Value), sent.ToDictionary(p => p.Key, p => p.Value!.GetValue<string>()));
        Assert.Equal(AccessRules.Unrestricted, sent[AccessRules.Permissions]!.GetValue<string>());
    }

    /// <summary>The record's setup read back as the service answers it on loopback (AUTONOMY1a), and none from a record before it.</summary>
    [Fact]
    public void A_record_s_setup_is_read_back_with_it()
    {
        var json = """
            [{"id":"s1","state":"awaiting-person","access":{"permissions":"asking","routing":"person","landing":"person","goAheads":"named","questions":"claude-code"}},
             {"id":"s2","state":"failed"}]
            """;

        Assert.Equal(AccessRules.Asking, ServiceClient.ReadRecord(json, "s1")!.Access![AccessRules.Permissions]);
        Assert.Null(ServiceClient.ReadRecord(json, "s2")!.Access);
    }
}
