using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// SUPERVISOR1a-ii over the real pipe (D208 point 2; the supervisor design §2.2 and §11 row 1a-ii): the hello draws a nonce for its
/// connection; a <c>Role</c> route the judge's role method covers runs with no card, its handler reading the role's door; one it does
/// not cover is refused, never asked, with no press granted as with one; and a proof missing, wrong, over another connection's nonce,
/// replayed or over an altered form is refused <c>not-holder</c>. The key never crosses the pipe: the client sends a proof alone.
/// </summary>
public sealed partial class PipeStandingTests
{
    /// <summary>A driver module that says what door and role each request ran under.</summary>
    private sealed class RoleModule : ModuleBase
    {
        public List<RoleJudgement?> Seen { get; } = [];

        public override string ModuleName => DriverModule.Name;

        protected override Task<object?> RouteMessageAsync(IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
        {
            lock (Seen) Seen.Add(PipeDoor.Role);
            return Task.FromResult<object?>(new { Kept = true, Standing = PipeDoor.Standing });
        }
    }

    private (InstancePipe Pipe, StandingConfirmation Judge, string Key, string Generation) Appointed(IMessageDispatcher dispatcher, MachineLog log)
    {
        var standing = new StandingConfirmation(Home, log, new StandingConfirmationTests.Placed(StandingPlace.Outside), offered: true, appointments: true)
        {
            Drives = _ => true,
        };
        var pipe = new InstancePipe(dispatcher, Bus, Home, log, version: "9.9.9-test", standing: standing);
        Assert.NotNull(pipe.Serve());
        string? handed = null;
        Assert.True(standing.Appoint(Roles.Supervisor, "engine", RoleHolders.Terminal, true, key => (handed = key) is not null).Answer.Done);
        return (pipe, standing, handed!, standing.Admit("engine").Generation!);
    }

    /// <summary>An answer proved by <paramref name="key"/> over <paramref name="nonce"/>, as the verb builds it.</summary>
    private static object Proved(string key, string? nonce, string generation, string choice = "Monthly")
    {
        SupervisorReason[] reasons = [new("The ticket says monthly.", "the quest's line 2")];
        var proof = nonce is null ? new string('0', 64) : SupervisorProof.Prove(key, nonce, SupervisorProof.Canonical("s1", "q-1", generation, choice, reasons))!;
        return new SupervisorForm("s1", "q-1", generation, proof, choice, reasons).Payload();
    }

    private static string? NonceOf(JsonElement hello) =>
        hello.GetProperty("data").TryGetProperty("nonce", out var nonce) ? nonce.GetString() : null;

    private static string? CodeOf(JsonElement answer) =>
        answer.TryGetProperty("error", out var error) ? error.GetProperty("code").GetString() : null;

    [Fact]
    public async Task A_covered_role_act_runs_with_no_card_under_the_role_door_and_never_the_presses_standing()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new RoleModule();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, _, key, generation) = Appointed(dispatcher, log);
        using var served = pipe;
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        var nonce = NonceOf(await client.HelloAsync(HomeId));
        Assert.Equal(64, nonce!.Length);

        // No press is granted: the role is judged on its own, never open.
        Assert.False(pipe.StandingConfirmation.Now().On);
        var id = await client.SendAsync(DriverModule.Name, SupervisorRoutes.Answer, Proved(key, nonce, generation));
        var answer = await client.AnswerAsync(id);

        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        Assert.False(answer.GetProperty("data").GetProperty("standing").GetBoolean());
        var role = Assert.Single(driver.Seen);
        Assert.Equal(("engine", generation), (role!.Repository, role.Generation));
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.DoesNotContain(Each(client.Told), each => Is(each, InstancePipe.Confirming));
    }

    public static TheoryData<string> Wrong => ["none", "key", "other-nonce", "replayed", "altered"];

    [Theory]
    [MemberData(nameof(Wrong))]
    public async Task A_proof_that_is_not_the_holders_for_this_connection_and_form_is_refused_never_asked(string wrong)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new RoleModule();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, _, key, generation) = Appointed(dispatcher, log);
        using var served = pipe;
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        var nonce = NonceOf(await client.HelloAsync(HomeId))!;
        await using var other = await PipeClient.ConnectAsync(pipe.Name!);
        var elsewhere = NonceOf(await other.HelloAsync(HomeId))!;

        object payload = wrong switch
        {
            "none" => Proved(key, null, generation),
            "key" => Proved(HolderKey.Draw(), nonce, generation),
            "other-nonce" => Proved(key, elsewhere, generation),
            _ => Proved(key, nonce, generation),
        };
        if (wrong == "altered")
        {
            var text = JsonSerializer.Serialize(payload).Replace("\"Monthly\"", "\"Weekly\"", StringComparison.Ordinal);
            payload = JsonDocument.Parse(text).RootElement.Clone();
        }

        if (wrong == "replayed")
        {
            // The first use of the nonce is the holder's own; the same proof sent again finds it used.
            Assert.True((await client.AnswerAsync(await client.SendAsync(DriverModule.Name, SupervisorRoutes.Answer, payload))).GetProperty("success").GetBoolean());
        }

        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, SupervisorRoutes.Answer, payload));
        Assert.False(answer.GetProperty("success").GetBoolean());
        Assert.Equal(SupervisorWhy.NotHolder, CodeOf(answer));
        Assert.Equal(wrong == "replayed" ? 1 : 0, driver.Seen.Count);
        Assert.Empty(pipe.Confirmations.Now().Waiting);
        Assert.Contains(Lines("supervisor.refused"), line => line.GetProperty("data").GetProperty("why").GetString() == SupervisorWhy.NotHolder);
    }

    /// <summary>A caller in an agent's job is refused before its proof is read, and a role act in a build that does not offer it is refused.</summary>
    [Fact]
    public async Task A_contained_caller_and_a_dark_build_are_refused_before_any_proof()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var log = new MachineLog(Home, "desktop");
        var driver = new RoleModule();
        var (dispatcher, services) = Compose(log, driver);
        using var composed = services;
        var (pipe, judge, key, generation) = Appointed(dispatcher, log);
        using var served = pipe;
        ((StandingConfirmationTests.Placed)judge.Caller).Answer = StandingPlace.InAgentJob;
        await using var client = await PipeClient.ConnectAsync(pipe.Name!);
        var nonce = NonceOf(await client.HelloAsync(HomeId))!;
        var refused = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, SupervisorRoutes.Answer, Proved(key, nonce, generation)));
        Assert.Equal(StandingWhy.InAgentJob, CodeOf(refused));
        Assert.Empty(driver.Seen);

        // This build's own judge: appointments are not offered, so nothing is covered whatever is sent.
        using var darkLog = new MachineLog(Home, "dark");
        var dark = new InstancePipe(dispatcher, Bus, Path.Combine(Home, "dark-home"), darkLog, version: "9.9.9-test");
        Directory.CreateDirectory(Path.Combine(Home, "dark-home"));
        if (dark.Serve() is { } name)
        {
            using var darkServed = dark;
            await using var darkClient = await PipeClient.ConnectAsync(name);
            var darkNonce = NonceOf(await darkClient.HelloAsync(HomeIdentity.Read(Path.Combine(Home, "dark-home"))!))!;
            var answer = await darkClient.AnswerAsync(await darkClient.SendAsync(DriverModule.Name, SupervisorRoutes.Answer, Proved(key, darkNonce, generation)));
            Assert.Equal(SupervisorWhy.NotOffered, CodeOf(answer));
            Assert.Empty(driver.Seen);
        }
    }
}
