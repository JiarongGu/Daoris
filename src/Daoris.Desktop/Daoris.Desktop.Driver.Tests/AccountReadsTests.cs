using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ROSTER1 (D150 §5.3): <c>reads.json</c> under the home keeps what was last read of each account's sign-in, and of the tool's
/// own, with when, so a restart starts from what was last read rather than from a probe. Nothing here starts a process.
/// </summary>
public sealed class AccountReadsTests : IDisposable
{
    private static readonly DateTimeOffset Ten = new(2026, 10, 4, 10, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-account-reads-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void Nothing_read_is_nothing_known()
    {
        var reads = AccountReads.Of(_home, "claude-code");

        Assert.Null(reads.Own);
        Assert.Empty(reads.Accounts);
        Assert.False(File.Exists(AccountReads.PathOf(_home)));
    }

    [Fact]
    public void Each_account_and_the_tools_own_sign_in_keep_their_word_and_when()
    {
        AccountReads.Keep(_home, "claude-code", "account-1", LoginState.Out, Ten);
        AccountReads.Keep(_home, "claude-code", null, LoginState.In, Ten.AddMinutes(1));
        AccountReads.Keep(_home, "codex", "work", LoginState.Unknown, Ten.AddMinutes(2));

        var claude = AccountReads.Of(_home, "claude-code");
        Assert.Equal(new AccountRead(LoginState.Out, Ten), claude.Accounts["account-1"]);
        Assert.Equal(new AccountRead(LoginState.In, Ten.AddMinutes(1)), claude.Own);
        Assert.Equal(new AccountRead(LoginState.Unknown, Ten.AddMinutes(2)), AccountReads.Of(_home, "codex").Accounts["work"]);
    }

    /// <summary>Names compare without case, as the wiring compares them.</summary>
    [Fact]
    public void An_account_is_found_by_its_name_whatever_its_case()
    {
        AccountReads.Keep(_home, "Claude-Code", "Account-1", LoginState.In, Ten);

        Assert.Equal(LoginState.In, AccountReads.Of(_home, "claude-code").Accounts["account-1"].Login);
    }

    /// <summary>Two reads finishing out of order: the later reading stands, never an older one written after it.</summary>
    [Fact]
    public void An_older_reading_never_replaces_a_newer_one()
    {
        AccountReads.Keep(_home, "claude-code", "account-1", LoginState.In, Ten.AddMinutes(5));
        AccountReads.Keep(_home, "claude-code", "account-1", LoginState.Out, Ten);

        Assert.Equal(new AccountRead(LoginState.In, Ten.AddMinutes(5)), AccountReads.Of(_home, "claude-code").Accounts["account-1"]);
    }

    /// <summary>A removed account is forgotten, so one made later under its name starts never read.</summary>
    [Fact]
    public void A_removed_account_is_forgotten_and_the_rest_stand()
    {
        AccountReads.Keep(_home, "claude-code", "account-1", LoginState.In, Ten);
        AccountReads.Keep(_home, "claude-code", "account-2", LoginState.Out, Ten);

        AccountReads.Forget(_home, "claude-code", "account-1");

        var reads = AccountReads.Of(_home, "claude-code");
        Assert.False(reads.Accounts.ContainsKey("account-1"));
        Assert.Equal(LoginState.Out, reads.Accounts["account-2"].Login);
    }

    /// <summary>
    /// The file holds a word and a time per account, never who signed in (D66 §3: read fresh, written nowhere), and keeps what
    /// it has no field for, as every file the home holds does.
    /// </summary>
    [Fact]
    public void The_file_holds_a_word_and_a_time_never_who_and_keeps_what_it_does_not_know()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(AccountReads.PathOf(_home), """{ "claude-code": { "later": 1 } }""");

        AccountReads.Keep(_home, "claude-code", "account-1", LoginState.In, Ten);

        var text = File.ReadAllText(AccountReads.PathOf(_home));
        var root = JsonNode.Parse(text)!.AsObject();
        Assert.Equal(1, (int)root["claude-code"]!["later"]!);
        Assert.Equal("in", (string?)root["claude-code"]!["accounts"]!["account-1"]!["login"]);
        Assert.Equal("2026-10-04T10:00:00Z", (string?)root["claude-code"]!["accounts"]!["account-1"]!["read"]);
        Assert.DoesNotContain("@", text);
        Assert.DoesNotContain("\r", text);
    }

    /// <summary>An unreadable file, or an entry this build cannot read, is nothing known: never a guess of signed in or out.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "claude-code": { "accounts": { "account-1": { "login": "maybe", "read": "2026-10-04T10:00:00Z" } } } }""")]
    [InlineData("""{ "claude-code": { "accounts": { "account-1": { "login": "in", "read": "yesterday" } } } }""")]
    [InlineData("""{ "claude-code": { "accounts": { "account-1": "in" } } }""")]
    public void What_does_not_read_is_nothing_known(string file)
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(AccountReads.PathOf(_home), file);

        Assert.Empty(AccountReads.Of(_home, "claude-code").Accounts);
    }
}
