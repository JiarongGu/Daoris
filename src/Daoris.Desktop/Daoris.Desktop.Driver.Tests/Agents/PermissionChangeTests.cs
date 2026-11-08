using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>Every writer of the rule file reads, decides and saves under its one lock, so no writer's save undoes another's.</summary>
public sealed class PermissionChangeTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-rule-change-" + Guid.NewGuid().ToString("N")[..8]);

    public PermissionChangeTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Start() => PermissionRules.Save(_home, PermissionRules.Add(
        PermissionRules.Add(PermissionFile.Empty, RuleScope.Machine, null, RuleList.Allow, "Bash(a:*)"), RuleScope.Machine, null, RuleList.Ask, "Bash(b:*)"));

    [Fact]
    public void A_removal_waiting_on_the_lock_lands_after_a_change_that_read_the_rule_and_neither_undoes_the_other()
    {
        Start();
        Task? removal = null;

        PermissionChange.Change(
            _home,
            rules => PermissionRules.Add(rules, RuleScope.Machine, null, RuleList.Allow, "Bash(b:*)"),
            read: () =>
            {
                // The person's removal, started once the settlement has read the rules and held off by the lock until it has written.
                removal = Task.Run(() => PermissionChange.Change(_home, rules => PermissionRules.Remove(rules, RuleScope.Machine, null, "Bash(a:*)")));
                Thread.Sleep(300);
            });
        removal!.Wait();

        var after = PermissionRules.Load(_home);
        Assert.Equal(["Bash(b:*)"], after.Machine.Allow);
        Assert.Empty(after.Machine.Ask);
    }

    [Fact]
    public void A_refused_edit_writes_nothing_and_lets_the_lock_go()
    {
        Start();
        var before = File.ReadAllText(PermissionRules.PathOf(_home));

        Assert.Throws<DriverException>(() => PermissionChange.Change(_home, _ => throw new DriverException("no")));

        Assert.Equal(before, File.ReadAllText(PermissionRules.PathOf(_home)));
        PermissionChange.Change(_home, rules => PermissionRules.Remove(rules, RuleScope.Machine, null, "Bash(a:*)"));
        Assert.Empty(PermissionRules.Load(_home).Machine.Allow);
    }

    [Fact]
    public void A_lock_held_past_the_wait_refuses_the_change_and_writes_nothing()
    {
        Start();
        var before = File.ReadAllText(PermissionRules.PathOf(_home));

        using (KeepLock.Take(PermissionRules.PathOf(_home)))
        {
            Assert.Throws<IOException>(() => PermissionChange.Change(
                _home, rules => PermissionRules.Remove(rules, RuleScope.Machine, null, "Bash(a:*)"), within: TimeSpan.FromMilliseconds(100)));
        }

        Assert.Equal(before, File.ReadAllText(PermissionRules.PathOf(_home)));
    }
}
