using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONTEXT1: the account is kept beside the instruction, on the same event of the session's record (D76), and says what was
/// handed beside the instruction too: the permission rules a session's agent takes. In-process: the structured capture is
/// driven over readers, as <see cref="ClaudeStreamJsonTests"/> drives it, so this is the fast half (MOD8).
/// </summary>
public sealed class HandedRecordTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-handed-" + Guid.NewGuid().ToString("N")[..8]);

    public HandedRecordTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static InstructionAccount Account() => TargetPrompt.Composed(TargetPromptGoldenTests.Full).Account;

    [Fact]
    public void The_opening_keeps_the_account_on_the_instruction_and_not_on_the_persons_answer()
    {
        var account = Account();

        var alone = Assert.Single(Daoris.Driver.Driver.Opening("the target", null, account));
        Assert.Same(account, alone.Account);

        var answered = Daoris.Driver.Driver.Opening("the target", "go ahead with the PUT", account);
        Assert.Equal([("target", true), ("person", false)], answered.Select(e => (e.Origin!, e.Account is not null)));
        Assert.Null(Assert.Single(Daoris.Driver.Driver.Opening("the target", null)).Account);
    }

    /// <summary>The record writes the account with the event and reads it back whole, beside the text.</summary>
    [Fact]
    public void The_record_keeps_the_account_beside_the_instruction_and_reads_it_back()
    {
        var events = new SessionEvents(_home);
        var account = Account().Beside(new HandedSection("rules", "permissions", "the permission rules: 9 handed beside it") { Shown = 9 });

        events.Append("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "the target", Account = account });

        var read = Assert.Single(events.Page("s1").Events);
        Assert.Equal(TargetPromptGoldenTests.Written(account), TargetPromptGoldenTests.Written(read.Account!));
        Assert.Contains("\"account\":{\"chars\":", File.ReadAllText(events.PathOf("s1")));
    }

    /// <summary>The native door's structured capture opens the record with the instruction it was handed, and its account.</summary>
    [Fact]
    public async Task The_structured_capture_opens_its_record_with_the_instruction_and_its_account()
    {
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        var account = Account();

        await Daoris.Driver.Driver.CaptureStructuredAsync(
            new StringReader(""), new StringReader(""), Path.Combine(_home, "s1.log"), "s1", output: null, events,
            new ClaudeStreamJson(), prompt: "take quest #q1", CancellationToken.None, account: account);

        var opening = Assert.Single(events.Page("s1").Events);
        Assert.Equal(("target", "take quest #q1"), (opening.Origin, opening.Text));
        Assert.Equal(account.Chars, opening.Account!.Chars);
    }

    [Fact]
    public void The_rules_handed_are_counted_by_list_with_the_scopes_they_were_composed_for()
    {
        var file = PermissionRules.Load(_home);
        var composed = PermissionRules.Compose(file, "work", "reports");

        var rules = Daoris.Driver.Driver.RulesHanded(takes: true, file, composed, "work", "reports");

        Assert.Equal(("rules", "permissions", null), (rules.Name, rules.Source, rules.Chars));
        Assert.Equal(composed.Allow.Count + composed.Ask.Count + composed.Deny.Count, rules.Shown);
        Assert.Null(rules.None);
        Assert.Equal(
            $"the permission rules: {rules.Shown} handed beside it ({composed.Allow.Count} allow, 0 ask, {composed.Deny.Count} deny), "
            + "composed from this machine's rules for workspace `work` and repository `reports`; 1 hard denial and the tree guard",
            rules.Said);
    }

    [Fact]
    public void An_agent_that_takes_no_rules_is_handed_none_and_says_why()
    {
        var file = PermissionRules.Load(_home);

        var rules = Daoris.Driver.Driver.RulesHanded(takes: false, file, RuleLists.Empty, "work", "reports");

        Assert.Equal(("agent", 0), (rules.None, rules.Shown));
        Assert.Equal("the permission rules: none handed, since its agent takes no rules from Daoris", rules.Said);
    }

    [Fact]
    public void A_rules_file_that_does_not_read_is_said_and_the_defaults_still_counted()
    {
        File.WriteAllText(PermissionRules.PathOf(_home), "{ not json");
        var file = PermissionRules.Load(_home);
        var composed = PermissionRules.Compose(file, null, null);

        var rules = Daoris.Driver.Driver.RulesHanded(takes: true, file, composed, null, null);

        var cut = Assert.Single(rules.Cuts!);
        Assert.Equal("rules-unread", cut.Code);
        Assert.Equal("this machine's rules file could not be read, so only the defaults were handed", cut.Said);
        Assert.True(rules.Shown > 0);
        Assert.Contains("for workspace `default`", rules.Said);
    }
}
