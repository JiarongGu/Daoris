using System.Net;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An account of an agent that signs nobody in and is held by an API key alone (dsh): ready by its key's presence and, where its
/// provider answers one, a cheap authenticated read of that key. Nothing is spent: the agent is a stand-in script and the provider's
/// API a stand-in handler.
/// </summary>
public sealed class KeyOnlyAccountTests : IDisposable
{
    private const string Key = "sk-keyonly-9999-abcd";

    private readonly string _home = Path.Combine(
        TestHomes.Root, "daoris-key-only-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private sealed class Answering(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Seen { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private sealed class Failing : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("stand-in: no network");
    }

    private HarnessRoster Roster(IKeyCheck check)
    {
        Directory.CreateDirectory(_home);
        var script = Path.Combine(_home, "dsh.mjs");
        File.WriteAllText(script, "if (process.argv[2] === '--version') { console.log('dsh 1.0'); process.exit(0); }\n");
        var toolchain = new TestAgent(["node", script], ["--version"], ProfileVariable: "DSH_HOME", KeyVariable: "DEEPSEEK_API_KEY", KeyCheck: check);
        return new HarnessRoster(TestDoors.Registry([TestDoors.Door("fake", toolchain)]), DaorisHome.Entry(_home, HomeEntry.Harnesses));
    }

    private static DriverConfig Config() => DriverConfig.Empty with { Actors = StaffedRoles.On("fake") };

    [Theory]
    [InlineData(HttpStatusCode.OK, LoginState.In)]
    [InlineData(HttpStatusCode.Unauthorized, LoginState.Out)]
    [InlineData(HttpStatusCode.Forbidden, LoginState.Out)]
    [InlineData(HttpStatusCode.InternalServerError, LoginState.Unknown)]
    public async Task The_balance_read_says_a_key_accepted_refused_or_unknown(HttpStatusCode status, LoginState expected)
    {
        var handler = new Answering(status);

        var state = await new DeepSeekBalance(handler).CheckAsync(Key, CancellationToken.None);

        Assert.Equal(expected, state);
        Assert.Equal("/user/balance", handler.Seen!.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", handler.Seen.Headers.Authorization!.Scheme);
        Assert.Equal(Key, handler.Seen.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task A_balance_read_that_cannot_reach_the_provider_is_no_refusal()
    {
        Assert.Equal(LoginState.Unknown, await new DeepSeekBalance(new Failing()).CheckAsync(Key, CancellationToken.None));
    }

    [Fact]
    public async Task An_account_with_a_key_the_provider_accepts_reads_signed_in_and_one_without_a_key_reads_no_key()
    {
        if (!OperatingSystem.IsWindows()) return;
        var roster = Roster(new DeepSeekBalance(new Answering(HttpStatusCode.OK)));
        var keyed = HarnessKeys.Add(_home, "fake", Key);
        var bare = "acct-00000000";
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", bare));
        HarnessSettings.Placed(HarnessSettings.ResolvePath(_home), "fake", bare);

        var report = (await roster.ReportAsync("fake", Config(), refresh: true))!;

        Assert.Equal(LoginState.In, report.Profiles.Single(p => p.Name == keyed).Login);
        Assert.Equal(LoginState.Out, report.Profiles.Single(p => p.Name == bare).Login);
    }

    [Fact]
    public async Task A_key_the_provider_refuses_reads_signed_out()
    {
        if (!OperatingSystem.IsWindows()) return;
        var roster = Roster(new DeepSeekBalance(new Answering(HttpStatusCode.Unauthorized)));
        var keyed = HarnessKeys.Add(_home, "fake", Key);

        var report = (await roster.ReportAsync("fake", Config(), refresh: true))!;

        Assert.Equal(LoginState.Out, report.Profiles.Single(p => p.Name == keyed).Login);
    }

    [Fact]
    public async Task The_walk_chooses_the_account_that_has_a_key_over_the_one_that_has_none()
    {
        if (!OperatingSystem.IsWindows()) return;
        var roster = Roster(new DeepSeekBalance(new Answering(HttpStatusCode.OK)));
        var bare = "acct-00000000";
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", bare));
        HarnessSettings.Placed(HarnessSettings.ResolvePath(_home), "fake", bare);
        var keyed = HarnessKeys.Add(_home, "fake", Key);
        HarnessSettings.Change(DaorisHome.Entry(_home, HomeEntry.Harnesses), read => read.WithRotation("fake", [bare, keyed]));

        var selection = await roster.SelectAsync("fake", Config(), null, null);

        Assert.True(selection.Allowed, selection.Refusal);
        Assert.Equal(keyed, selection.Profile);
        Assert.Equal(Key, selection.Environment!["DEEPSEEK_API_KEY"]);
    }

    [Fact]
    public async Task The_walk_holds_a_start_when_the_only_key_is_refused()
    {
        if (!OperatingSystem.IsWindows()) return;
        var roster = Roster(new DeepSeekBalance(new Answering(HttpStatusCode.Unauthorized)));
        var keyed = HarnessKeys.Add(_home, "fake", Key);
        HarnessSettings.Change(DaorisHome.Entry(_home, HomeEntry.Harnesses), read => read.WithRotation("fake", [keyed]));

        var selection = await roster.SelectAsync("fake", Config(), null, null);

        Assert.False(selection.Allowed);
        // The way on is a key, so the note is the key's, not the sign-in's the catalogue words as `agent login`.
        Assert.Contains(selection.RefusalNote!.Parts, part => part.Code == "held.key-missing");
        Assert.DoesNotContain(selection.RefusalNote.Parts, part => part.Code == "held.signed-out");
    }

    [Fact]
    public async Task A_key_only_agent_with_no_account_is_held_for_a_key_and_names_the_key_verb()
    {
        if (!OperatingSystem.IsWindows()) return;
        var roster = Roster(new DeepSeekBalance(new Answering(HttpStatusCode.OK)));

        var selection = await roster.SelectAsync("fake", Config(), null, null);

        Assert.False(selection.Allowed);
        Assert.Equal("held.no-key", Assert.Single(selection.RefusalNote!.Parts).Code);
        Assert.Contains("daoris agent key fake", selection.Refusal);
        Assert.DoesNotContain("agent login", selection.Refusal);
    }

    [Fact]
    public async Task A_list_of_accounts_with_no_key_is_held_with_the_key_verb_for_each()
    {
        if (!OperatingSystem.IsWindows()) return;
        var roster = Roster(new DeepSeekBalance(new Answering(HttpStatusCode.OK)));
        string[] bare = ["acct-00000001", "acct-00000002"];
        foreach (var account in bare)
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", account));
            HarnessSettings.Placed(HarnessSettings.ResolvePath(_home), "fake", account);
        }

        HarnessSettings.Change(DaorisHome.Entry(_home, HomeEntry.Harnesses), read => read.WithRotation("fake", [.. bare]));

        var selection = await roster.SelectAsync("fake", Config(), null, null);

        Assert.False(selection.Allowed);
        Assert.Contains(selection.RefusalNote!.Parts, part => part.Code == "held.key");
        Assert.Contains("daoris agent key fake --account acct-00000001", selection.Refusal);
        Assert.DoesNotContain("agent login", selection.Refusal);
    }

    [Fact]
    public void Dsh_declares_the_variable_its_provider_reads_and_a_balance_read()
    {
        var dsh = ProviderRegistry.Built().AgentNamed("dsh")!;

        Assert.Equal("DEEPSEEK_API_KEY", dsh.Accounts.KeyVariable);
        Assert.NotNull(dsh.Accounts.KeyCheck);
    }
}
