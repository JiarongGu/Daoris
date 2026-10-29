using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1j part ii (the foundation design §2.4, D212's FOUNDATION1j notes): a second opinion's, a helper's and a partner's reviewer is
/// walked on the one start plan. The plan's loop walks the reviewer's order by maker and skips an agent held as a whole, the reviewer's own
/// rule over that loop; each entry's accounts are the plan's one ask of D197's walk, so a start takes exactly one reservation, at its
/// selection; and each entry it passes is said in the plan's one vocabulary (<see cref="StartPasses"/>).
/// </summary>
/// <remarks>
/// Nothing here starts a process: each agent is present by a file look on a command this test writes, its family copied from the
/// declaration this build carries, as <c>ReviewerChoiceTests</c>' are. The clock is the day the test runs, which names the machine log's file.
/// </remarks>
public sealed class ReviewerPlanTests : IDisposable
{
    private const string Nowhere = "daoris-foundation1j-no-such-command";

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Now = new(DateTime.UtcNow.Date.AddHours(12), TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "foundation1j-reviewer-" + Guid.NewGuid().ToString("N")[..8]);

    public ReviewerPlanTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
        // Codex's and dsh's one account each, so a start chosen on one is counted (an agent on its own sign-in holds no count).
        foreach (var owner in new[] { "codex", "dsh" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, owner, "account-1"));
        new HarnessSettings().WithDefault("codex", "account-1").WithDefault("dsh", "account-1").Save(Settings);
    }

    public void Dispose() => GitSeed.Discard(_home);

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private string Command => Path.Combine(_home, "agent-here");

    private sealed class Adapter(string name, HarnessToolchain? toolchain) : ISessionAdapter
    {
        public string Name => name;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns here");
    }

    private static readonly string[] Agents = ["claude-code", "claude-code-acp", "codex-acp", "dsh"];

    /// <summary>Each of <see cref="Agents"/> with its declared family, present by a file look, or installed nowhere where <paramref name="absent"/> names it.</summary>
    private HarnessRoster Roster(string[]? absent = null)
    {
        var adapters = new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in Agents)
        {
            var real = AdapterSet.Built().Resolve(name).Toolchain!;
            adapters[name] = new Adapter(name, new HarnessToolchain(
                Binary: [absent?.Contains(name) == true ? Nowhere : Command], VersionArguments: [], ProfileVariable: real.ProfileVariable,
                AccountOf: real.AccountOf, ProbeByPresence: true, Product: real.Product, Maker: real.Maker, RunsInOwnHome: true));
        }

        return new HarnessRoster(new AdapterSet(adapters), Settings) { Clock = () => Now, Zone = Zone };
    }

    private void Cool(string agent) =>
        AccountCooling.Cool(_home, new CoolingEntry(agent, "account-1", Now.AddHours(30), true, "weekly", Now.AddHours(-1), "s1"), Now);

    private static DriverConfig Config => DriverConfig.Empty with { Adapter = "claude-code" };

    private static OpinionRule Rule(string reviewers) => new([.. reviewers.Split(',')], [OpinionRules.Landing]);

    /// <summary>
    /// 🔴 Exactly one reservation per start: the reviewer's order by maker walked on the plan's loop, another maker's agents first, each
    /// one asked of the plan's account walk once; the one cooling passed, the next taken and counted, and the same agent the rule lists first
    /// never asked. The choice carries the plan's settled selection, whose reservation is the start's one count.
    /// </summary>
    [Fact]
    public async Task The_reviewer_walks_its_order_by_maker_on_the_plan_and_takes_one_reservation_at_its_selection()
    {
        Cool("codex");
        var roster = Roster();
        var (counted, walks) = (roster.Mark(), roster.Selected);

        var choice = await ReviewerChoice.ChooseAsync(roster, Rule("claude-code-acp,codex-acp,dsh"), ["claude-code"], Config, null);

        Assert.Equal(("dsh", (string?)null), (choice.Reviewer, choice.Code));
        Assert.Equal((counted + 1, walks + 2), (roster.Mark(), roster.Selected));
        var settled = choice.Settled!;
        Assert.Equal(("opinion", roster.Mark(), "dsh"), (settled.Role, settled.Reservation, settled.Chosen!.Agent));
        Assert.Equal(["rule-default:codex-acp:cooling"], choice.Passes.Select(pass => pass.Word));
        Assert.Equal(choice.Passes, settled.Passes);
    }

    /// <summary>
    /// The reviewer's own rule over the plan's loop (D205 §4.1, MODELROLE1e): an agent held as a whole, by its cool-off, holds each of its
    /// entries alike, so its later entry is neither asked nor passed, and a helper's walk is the plan of the helper's role.
    /// </summary>
    [Fact]
    public async Task A_held_agent_s_later_entries_are_neither_asked_nor_passed()
    {
        Cool("codex");
        var config = ActorChoices.With(Config, "helper", [new ActorChoice("codex-acp", "opus"), new ActorChoice("codex-acp", "sonnet")]);
        var roster = Roster();
        var walks = roster.Selected;

        var choice = await ReviewerChoice.ChooseAsync(roster, Rule("codex-acp,dsh"), ["claude-code"], config, null, actor: "helper");

        Assert.Equal("dsh", choice.Reviewer);
        Assert.Equal(walks + 2, roster.Selected);
        Assert.Equal(["1:codex-acp:cooling"], choice.Passes.Select(pass => pass.Word));
        Assert.Equal("helper", choice.Settled!.Role);
    }

    /// <summary>The reviewer's codes, which its sentence and its own <c>Why</c> read, are said in the plan's one vocabulary.</summary>
    [Theory]
    [InlineData(ReviewerHeld.NotInstalled, StartPasses.Unavailable)]
    [InlineData(ReviewerHeld.PinNotInstalled, StartPasses.Unavailable)]
    [InlineData(ReviewerHeld.NotOffered, StartPasses.NotOffered)]
    [InlineData(ReviewerHeld.Cooling, StartPasses.Cooling)]
    [InlineData(ReviewerHeld.SignedOut, StartPasses.SignedOut)]
    [InlineData(ReviewerHeld.Refused, StartPasses.Held)]
    public void Each_reviewer_code_is_said_as_the_plan_s(string held, string pass)
    {
        Assert.Equal(pass, ReviewerHeld.Pass(held));
        Assert.Contains(pass, StartPasses.Codes);
    }

    /// <summary>
    /// A reviewer not installed and a name no agent of this machine answers are each passed <c>unavailable</c>, its own sentence kept, and the
    /// helper's record's machine log line names them by the plan's codes (D205 §4.6 item 3), never the reviewer's.
    /// </summary>
    [Fact]
    public async Task What_the_walk_passed_is_logged_in_the_plan_s_words()
    {
        var roster = Roster(absent: ["codex-acp"]);

        var choice = await ReviewerChoice.ChooseAsync(roster, Rule("codex-acp,nobody-here,claude-code-acp"), ["claude-code"], Config, null, actor: "helper");

        Assert.Equal("claude-code-acp", choice.Reviewer);
        Assert.Equal([ReviewerHeld.NotInstalled, ReviewerHeld.NotInstalled], choice.Tried.Select(tried => tried.Held));
        Assert.Equal(["rule-default:codex-acp:unavailable", "rule-default:nobody-here:unavailable"], choice.Passes.Select(pass => pass.Word));

        using (var log = new MachineLog(_home, "driver", () => Now))
        {
            OneTurnReask.Say(new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), "s1", "helper", choice, log);
        }

        var day = Now.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var chose = StubFile.Lines(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Logs), $"{day}.driver.jsonl"))
            .Select(line => JsonDocument.Parse(line).RootElement).Single(line => line.GetProperty("event").GetString() == "actor.chose");
        Assert.Equal(
            ["rule-default:codex-acp:unavailable", "rule-default:nobody-here:unavailable"],
            chose.GetProperty("data").GetProperty("passed").EnumerateArray().Select(word => word.GetString()));
    }
}
