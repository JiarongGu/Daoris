using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// HELPMODE1a1 (D183 points 1, 3): what a start of Ask Daoris gets on its harness under the mode the person chose. Restricted
/// keeps each harness as it ran; full runs each in its own mode that asks nothing, a flag on the native door and a required mode
/// on the protocol door; a harness with no such mode is refused, naming it. No process, so the fast half.
/// </summary>
public sealed class HelpAccessTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-access-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpAccessTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static ISessionAdapter Adapter(string name) => AdapterSet.Built().Resolve(name);

    /// <summary>Restricted is Ask Daoris as it ran: the agent's asking mode asked for on the protocol door, no flag on the native one.</summary>
    [Theory]
    [InlineData("claude-code")]
    [InlineData("claude-code-acp")]
    [InlineData("codex-acp")]
    [InlineData("dsh")]
    public void Restricted_keeps_each_harness_as_it_ran(string name)
    {
        var access = HelpAccess.For(HelpModes.Restricted, Adapter(name));

        Assert.False(access.Full);
        Assert.Null(access.Refusal);
        Assert.Equal((HelpRoom.Posture, (string?)null), (access.Posture, access.PermissionMode));
        Assert.Contains("restricted", access.Said);
        Assert.Contains($"`{name}`", access.Said);
    }

    /// <summary>
    /// Full runs each harness in its mode that asks nothing: Claude Code's flag on the native door, a mode the protocol door must set
    /// or fail on the others. What its conversation is told names the mode and the harness's own word for it.
    /// </summary>
    [Theory]
    [InlineData("claude-code", null, "bypassPermissions")]
    [InlineData("claude-code-acp", "!bypassPermissions", null)]
    [InlineData("codex-acp", "!agent-full-access", null)]
    public void Full_runs_each_harness_in_its_mode_that_asks_nothing(string name, string? posture, string? permissionMode)
    {
        var access = HelpAccess.For(HelpModes.Full, Adapter(name));

        Assert.True(access.Full);
        Assert.Null(access.Refusal);
        Assert.Equal((posture, permissionMode), (access.Posture, access.PermissionMode));
        Assert.Contains("full access", access.Said);
        Assert.Contains($"`{Adapter(name).FullPosture}`", access.Said);
    }

    /// <summary>A harness with no mode that asks nothing mapped is refused in full access, naming it, and the person's two ways on.</summary>
    [Theory]
    [InlineData("dsh")]
    [InlineData("acp-stub")]
    public void Full_on_a_harness_with_no_such_mode_is_refused_naming_it(string name)
    {
        var refusal = HelpAccess.For(HelpModes.Full, Adapter(name)).Refusal;

        Assert.NotNull(refusal);
        Assert.Contains($"`{name}`", refusal);
        Assert.Contains("help mode restricted", refusal);
        Assert.Contains("another agent", refusal);
    }

    /// <summary>
    /// Its place under full access: no allow-list and nothing asked, its harness's mode that asks nothing, every plugin server and no
    /// tree guard, since its tree is the room and its housekeeping is outside it; the person's own denies still go, the shipped
    /// no-push among them. Restricted leaves the place exactly as HELP1a made it.
    /// </summary>
    [Fact]
    public void Its_place_under_full_access_asks_nothing_and_keeps_the_persons_denies()
    {
        var place = ChatRunner.HelpPlaceIn(_home, HelpRoom.PathOf(_home), [new("console-ui", "work", "/work/console-ui")]);
        var file = PermissionFile.Empty with
        {
            Machine = new RuleLists(["Bash(npm test:*)"], ["Bash(git commit:*)"], ["WebFetch(domain:secrets.example)"]),
        };

        Assert.Same(place, ChatRunner.HelpUnder(place, HelpAccess.For(HelpModes.Restricted, Adapter("claude-code-acp"))));
        Assert.Equal((true, false, (string?)null), (place.Guarded, place.EveryServer, place.PermissionMode));

        var full = ChatRunner.HelpUnder(place, HelpAccess.For(HelpModes.Full, Adapter("claude-code-acp")));
        var rules = full.Rules(file, "s1");

        Assert.Empty(rules.Allow);
        Assert.Empty(rules.Ask);
        Assert.Contains("WebFetch(domain:secrets.example)", rules.Deny);
        Assert.Contains("Bash(git push:*)", rules.Deny);
        Assert.Equal("!bypassPermissions", full.Posture);
        Assert.Equal((false, true), (full.Guarded, full.EveryServer));
        Assert.Equal((HelpRoom.Repository, PluginServerTo.Help, true), (full.Name, full.ServersTo, full.Keeps));

        var native = ChatRunner.HelpUnder(place, HelpAccess.For(HelpModes.Full, Adapter("claude-code")));
        Assert.Equal("bypassPermissions", native.PermissionMode);
    }

    /// <summary>
    /// A start's place is put under the mode in the driver's file at that start, with what it got for its transcript's first line
    /// (D183 point 3), kept apart from the mode chosen; a repository's conversation is left as it is and says nothing; and full access
    /// on an agent with none such ends the start, saying so.
    /// </summary>
    [Fact]
    public void A_start_takes_the_mode_in_the_file_and_says_what_it_got_first()
    {
        var help = ChatRunner.HelpPlaceIn(_home, HelpRoom.PathOf(_home), []);
        var repository = help with { Name = "engine" };
        var full = DriverConfig.Empty.WithHelpMode(HelpModes.Full);

        Assert.Equal((repository, (string?)null, (string?)null), ChatRunner.UnderHelpMode(repository, Adapter("claude-code-acp"), full));

        var (restricted, saidRestricted, restrictedMode) = ChatRunner.UnderHelpMode(help, Adapter("claude-code-acp"), DriverConfig.Empty);
        Assert.Same(help, restricted);
        Assert.StartsWith("— Ask Daoris runs restricted on `claude-code-acp`", saidRestricted);
        Assert.Equal(HelpModes.Restricted, restrictedMode);

        var (fully, saidFull, fullMode) = ChatRunner.UnderHelpMode(help, Adapter("codex-acp"), full);
        Assert.Equal("!agent-full-access", fully.Posture);
        Assert.Contains("full access", saidFull);
        Assert.Contains("takes no rules", saidFull);
        Assert.Equal(HelpModes.Full, fullMode);

        var refused = Assert.Throws<DriverException>(() => ChatRunner.UnderHelpMode(help, Adapter("dsh"), full));
        Assert.Contains("`dsh`", refused.Message);
    }

    /// <summary>
    /// 🔴 HELPMODE1a2 (D183 point 3): a conversation going on under another mode than it last ran under says the change first, then
    /// what it runs under now, in one line; one going on under the same mode says what it runs under alone. A record from before the
    /// modes ran restricted.
    /// </summary>
    [Fact]
    public void A_conversation_going_on_under_another_mode_says_the_change_first()
    {
        var help = ChatRunner.HelpPlaceIn(_home, HelpRoom.PathOf(_home), []);
        var full = DriverConfig.Empty.WithHelpMode(HelpModes.Full);

        var (_, widened, _) = ChatRunner.UnderHelpMode(help, Adapter("claude-code-acp"), full, ran: HelpModes.Restricted);
        var (_, narrowed, _) = ChatRunner.UnderHelpMode(help, Adapter("claude-code-acp"), DriverConfig.Empty, ran: HelpModes.Full);
        var (_, same, _) = ChatRunner.UnderHelpMode(help, Adapter("claude-code-acp"), full, ran: HelpModes.Full);
        var (_, before, _) = ChatRunner.UnderHelpMode(help, Adapter("claude-code-acp"), full, ran: HelpModes.Recorded(null));

        Assert.StartsWith(
            "— Ask Daoris's mode changed since this conversation last ran: it ran restricted, and goes on with full access. Ask Daoris "
            + "runs with full access on `claude-code-acp`", widened);
        Assert.StartsWith(
            "— Ask Daoris's mode changed since this conversation last ran: it ran with full access, and goes on restricted. Ask Daoris "
            + "runs restricted", narrowed);
        Assert.StartsWith("— Ask Daoris runs with full access", same);
        Assert.Equal(widened, before);
        Assert.DoesNotContain('\n', widened!);
    }

    /// <summary>
    /// ACCESSSWAP1 (D183 point 2, replacing HELPMODE1a2's next start): a change under a running conversation is said as what changed,
    /// its mode and its housekeeping grant each, and when it applies: its agent ends at its turn's end, never mid-turn, or now where it
    /// is idle, and its next words resume the same conversation under the new access. Under the access chosen there is nothing to say.
    /// HELPMODE1c3: the chosen settings are compared and said the same way.
    /// </summary>
    [Fact]
    public void A_change_while_a_conversation_runs_says_what_changed_and_that_its_next_turn_takes_it()
    {
        static ConfiguredAccess Of(string[] housekeeping, params string[] settings) => new(housekeeping) { Settings = settings };
        var none = ConfiguredAccess.None;
        var widened = HelpSwap.Changed(HelpModes.Restricted, none, HelpModes.Full, Of([ConfiguredAccess.Inspect, ConfiguredAccess.CleanUp]));

        Assert.Equal("its mode is now full access, and its housekeeping grants `inspect`, `clean-up` now", widened);
        Assert.Equal("its mode is now restricted", HelpSwap.Changed(HelpModes.Full, none, HelpModes.Restricted, none));
        Assert.Equal("its housekeeping grants nothing now", HelpSwap.Changed(HelpModes.Restricted, Of([ConfiguredAccess.Inspect]), "RESTRICTED", none));
        Assert.Null(HelpSwap.Changed(HelpModes.Full, Of([ConfiguredAccess.Inspect]), HelpModes.Full, Of([ConfiguredAccess.Inspect])));
        Assert.Equal("its chosen settings are `notify`, `cap` now",
            HelpSwap.Changed(HelpModes.Restricted, Of([ConfiguredAccess.Inspect]), HelpModes.Restricted,
                Of([ConfiguredAccess.Inspect], ConfiguredAccess.Notify, ConfiguredAccess.Cap)));
        Assert.Equal("its chosen settings are none now", HelpSwap.Changed(HelpModes.Restricted, Of([], ConfiguredAccess.Cap), HelpModes.Restricted, none));
        Assert.Null(HelpSwap.Changed(HelpModes.Restricted, Of([], ConfiguredAccess.Cap), HelpModes.Restricted, Of([], ConfiguredAccess.Cap)));

        var turning = HelpSwap.Said(widened!, idle: false);
        Assert.StartsWith("— Ask Daoris's access changed under this conversation: its mode is now full access", turning);
        Assert.Contains("Its agent ends at its turn's end, never mid-turn, and your next words resume this conversation", turning);
        Assert.Contains("Its agent ends now, and", HelpSwap.Said(widened!, idle: true));
        Assert.DoesNotContain("next start", turning);
        Assert.DoesNotContain('\n', turning);
    }

    /// <summary>A record's mode as read: as kept, any case; none, or one this build does not know, is restricted, what it ran before the modes.</summary>
    [Theory]
    [InlineData(null, "restricted")]
    [InlineData("full", "full")]
    [InlineData("FULL", "full")]
    [InlineData("configured", "restricted")]
    public void A_records_mode_reads_restricted_unless_it_says_full(string? kept, string read) =>
        Assert.Equal(read, HelpModes.Recorded(kept));
}
