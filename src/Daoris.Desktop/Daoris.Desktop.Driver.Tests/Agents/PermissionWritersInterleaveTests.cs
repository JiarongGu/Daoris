using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's removal of an allow lands while another writer of the rule file has read the rules and not yet saved: whichever order the
/// lock gives, the allow stays removed and the other writer's change stands. Each case runs the removal from the other writer's own read seam.
/// </summary>
public sealed class PermissionWritersInterleaveTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-08T10:00:00Z");

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-interleave-" + Guid.NewGuid().ToString("N")[..8]);

    public PermissionWritersInterleaveTests()
    {
        Directory.CreateDirectory(_home);
        PermissionRules.Save(_home, PermissionRules.Add(PermissionRules.Add(PermissionFile.Empty, RuleScope.Machine, null, RuleList.Allow, "Bash(a:*)"),
            RuleScope.Machine, null, RuleList.Ask, "Bash(b:*)"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private Task? _removal;

    /// <summary>The person's removal of A, started when the next change has read the rules and held off by the lock until it has written.</summary>
    private void RemovalDuringNextRead(RuleScope scope = RuleScope.Machine, string? name = null) => PermissionChange.AfterRead.Value = () =>
    {
        _removal = Task.Run(() => PermissionChange.Change(_home, rules => PermissionRules.Remove(rules, scope, name, "Bash(a:*)")));
        Thread.Sleep(300);
    };

    private void Propose(string id, string action, string list, string rule)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.RuleProposals);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, id + ".json"), new JsonObject
        {
            ["id"] = id, ["proposed"] = At.ToString("O"), ["by"] = new JsonObject { ["session"] = "s1a2b3c4" },
            ["change"] = new JsonObject { ["action"] = action, ["scope"] = "machine", ["list"] = list, ["rule"] = rule },
            ["why"] = "Needed.", ["state"] = action == "add" && list == "allow" ? "waiting" : "proposed",
        }.ToJsonString());
    }

    [Fact]
    public void A_supervisors_yes_moving_B_to_allow_and_the_persons_removal_of_A_leave_A_removed_and_B_allowed()
    {
        Propose("p1", "add", "allow", "Bash(b:*)");
        RemovalDuringNextRead();

        var settled = ProposalSettlement.Settle(_home, "p1", RuleSettler.Supervisor("keeper", "d1"), yes: true, null, At);
        _removal!.Wait();

        Assert.Equal(SettleOutcome.Settled, settled.Outcome);
        var rules = PermissionRules.Load(_home).Machine;
        Assert.Equal(["Bash(b:*)"], rules.Allow);
        Assert.Empty(rules.Ask);
    }

    [Fact]
    public void The_ticks_narrowing_and_the_persons_removal_of_A_leave_A_removed_and_the_deny_written()
    {
        // A deny of B moves B out of the allow list, which the narrowing writes whole.
        PermissionChange.Change(_home, rules => PermissionRules.Add(rules, RuleScope.Machine, null, RuleList.Allow, "Bash(c:*)"));
        Propose("p2", "add", "deny", "Bash(c:*)");
        RemovalDuringNextRead();

        RuleProposals.Settle(_home, At);
        _removal!.Wait();

        var rules = PermissionRules.Load(_home).Machine;
        Assert.Empty(rules.Allow);
        Assert.Equal(["Bash(c:*)"], rules.Deny);
    }

    [Fact]
    public void A_setups_allows_and_the_persons_removal_of_A_leave_A_removed_and_the_setups_rules_added()
    {
        PermissionChange.Change(_home, rules => PermissionRules.Add(rules, RuleScope.Workspace, "games", RuleList.Allow, "Bash(a:*)"));
        RemovalDuringNextRead(RuleScope.Workspace, "games");

        WorkspaceSetup.Write(_home, "games", ["engine"], atOnce: 1, pilot: 0, At);
        _removal!.Wait();

        var allowed = PermissionRules.Load(_home).Workspaces["games"].Allow;
        Assert.DoesNotContain("Bash(a:*)", allowed);
        Assert.NotEmpty(allowed);
    }
}
