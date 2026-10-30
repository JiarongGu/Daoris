using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BRAINSTORM1c (D176 point 7): each pass takes its account by the walk, as a driven start's is, on each real door, and a maker with no ready
/// account is <c>unavailable</c> with the walk's own code: <c>cooling</c> where its accounts wait for a cool-off, <c>signed-out</c> where
/// none is signed in, <c>unavailable</c> where nothing of it is installed, <c>held</c> otherwise. The walk never runs on the other door or
/// another maker in its place.
/// </summary>
/// <remarks>
/// Nothing here starts a process, and no account is asked: each door is the real adapter's own name, wire and toolchain with its binary a
/// file this test made, present by a file look, and its sign-in question and version question taken out, so the roster asks no `claude` or
/// `codex` anything.
/// </remarks>
public sealed class BrainstormWalkTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Now = new(DateTime.UtcNow.Date.AddHours(12), TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "brainstorm1c-walk-" + Guid.NewGuid().ToString("N")[..8]);

    public BrainstormWalkTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private string Command => Path.Combine(_home, "agent-here");

    /// <summary>A real door's identity over a toolchain that is present by its file and asks nothing.</summary>
    private sealed class Door(ISessionAdapter real, HarnessToolchain toolchain) : ISessionAdapter
    {
        public string Name => real.Name;

        public SessionWire Wire => real.Wire;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns here");
    }

    private HarnessToolchain Quiet(HarnessToolchain real) =>
        real with { Binary = [Command], VersionArguments = [], LoginCheck = null, ProbeByPresence = true, Install = null, Channel = null };

    /// <summary>The real doors and the agents whose accounts they run as, quiet; <paramref name="installed"/> says whether the binary is there.</summary>
    private HarnessRoster Roster(bool installed = true)
    {
        if (installed) File.WriteAllText(Command, "");
        var built = AdapterSet.Built();
        var adapters = new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "claude-code", "claude-code-acp", "codex-acp" })
        {
            adapters[name] = new Door(built.Resolve(name), Quiet(built.Resolve(name).Toolchain!));
        }

        var owners = new Dictionary<string, HarnessToolchain>(StringComparer.OrdinalIgnoreCase) { [CodexAccounts.Agent] = Quiet(CodexAccounts.Toolchain) };
        return new HarnessRoster(new AdapterSet(adapters, owners), Settings) { Clock = () => Now, Zone = Zone };
    }

    /// <summary>The agent whose accounts the door runs as, and its account folders, with the first the default.</summary>
    private static string Owner(string door) => door == "codex-acp" ? CodexAccounts.Agent : "claude-code";

    private void Accounts(string door, params string[] ids)
    {
        var owner = Owner(door);
        foreach (var id in ids) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, owner, id));
        new HarnessSettings().WithDefault(owner, ids[0]).Save(Settings);
    }

    private static BrainstormTurn Turn(string door) =>
        new("b1", "g1", door, "room", "Answer it.", "answer.json", 5, new ActorEffective(door, null, null, "before"));

    private static Task<HarnessSelection> Walk(string door, HarnessRoster roster) =>
        DriverBrainstorms.VoiceAsync(Turn(door), DriverConfig.Empty, roster, CancellationToken.None);

    /// <summary>A door with a ready account is placed on it, counted as a start, and its code is none.</summary>
    [Theory]
    [InlineData("claude-code-acp")]
    [InlineData("codex-acp")]
    public async Task A_door_with_a_ready_account_is_placed_on_it(string door)
    {
        Accounts(door, "account-1", "account-2");

        var placed = await Walk(door, Roster());

        Assert.True(placed.Allowed, placed.Refusal);
        Assert.Equal("account-1", placed.Profile);
    }

    /// <summary>
    /// 🔴 Where every account of the door's agent waits for a cool-off, the pass is `cooling`, and the walk does not move to the other door
    /// of the same maker's account or to another maker's: its own entry's walk alone is asked.
    /// </summary>
    [Theory]
    [InlineData("claude-code-acp")]
    [InlineData("codex-acp")]
    public async Task A_door_whose_accounts_all_cool_is_cooling(string door)
    {
        Accounts(door, "account-1");
        AccountCooling.Cool(_home, new CoolingEntry(Owner(door), "account-1", Now.AddHours(3), true, "weekly", Now, "s0"), Now);

        var held = await Walk(door, Roster());

        Assert.False(held.Allowed);
        Assert.Equal("cooling", DriverBrainstorms.PassCode(held));
    }

    /// <summary>🔴 A door whose accounts are not signed in is `signed-out`, never the code of an agent nothing of which is installed.</summary>
    [Theory]
    [InlineData("claude-code-acp")]
    [InlineData("codex-acp")]
    public async Task A_door_with_no_account_signed_in_is_signed_out(string door)
    {
        Accounts(door, "account-1");
        var roster = Roster();
        roster.SignedOut(door, "account-1");

        var held = await Walk(door, roster);

        Assert.False(held.Allowed);
        Assert.Equal("signed-out", DriverBrainstorms.PassCode(held));
    }

    /// <summary>A door whose binary is not installed is `unavailable`: nothing of it can run here at all.</summary>
    [Theory]
    [InlineData("claude-code-acp")]
    [InlineData("codex-acp")]
    public async Task A_door_that_is_not_installed_is_unavailable(string door)
    {
        Accounts(door, "account-1");

        var held = await Walk(door, Roster(installed: false));

        Assert.False(held.Allowed);
        Assert.Equal("unavailable", DriverBrainstorms.PassCode(held));
    }

    /// <summary>
    /// The code is read from the walk's facts and never its words, and the model a voice's accounts did not offer keeps the code the pass's
    /// restart reads (D205 §4.4).
    /// </summary>
    [Fact]
    public void The_code_is_the_walks_own_and_a_refusal_with_no_fact_is_held()
    {
        Assert.Equal("held", DriverBrainstorms.PassCode(new HarnessSelection("a provider's refusal, in words")));
        Assert.Equal(AgentOffers.NotOffered, DriverBrainstorms.PassCode(new HarnessSelection("refused") { NotOffered = ["account-1"] }));
        Assert.Equal("unavailable", DriverBrainstorms.PassCode(new HarnessSelection("no agent") { Absent = AgentAbsence.NotInstalled }));
    }
}
