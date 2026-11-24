using System.Collections.Concurrent;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A write to an account's stored sign-in that empties its tokens marks the account signed out at once, and one that fills them marks it
/// signed in, whatever the last look read: on 2026-11-24 at 05:10:15Z the watch logged account-1's store renamed with both tokens empty and
/// the Agents page and <c>daoris agent list</c> read it signed in for hours. The mark says who wrote it where the watch can tell.
/// </summary>
public sealed class SignOutSeenTests : IDisposable
{
    private static readonly DateTimeOffset Ten = new(2026, 11, 24, 5, 10, 15, TimeSpan.Zero);

    private const string Emptied = """{"claudeAiOauth":{"accessToken":"","refreshToken":"","expiresAt":0}}""";
    private const string Filled = """{"claudeAiOauth":{"accessToken":"a-value","refreshToken":"r-value","expiresAt":1}}""";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-sign-out-seen-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _account;
    private readonly HarnessRoster _roster;

    public SignOutSeenTests()
    {
        Directory.CreateDirectory(_home);
        _account = HarnessSettings.ProfileHome(_home, "claude-code", "account-1");
        Directory.CreateDirectory(_account);
        new HarnessSettings().WithDefault("claude-code", "account-1").Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
        _roster = new HarnessRoster(ProviderRegistry.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => Ten };
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private AccountRead? Read() => AccountReads.Of(_home, "claude-code").Accounts.GetValueOrDefault("account-1");

    private void SignedInHoursAgo()
    {
        File.WriteAllText(Path.Combine(_account, ".credentials.json"), Filled);
        AccountReads.Keep(_home, "claude-code", "account-1", LoginState.In, Ten - TimeSpan.FromHours(3));
    }

    private async Task Written(string json)
    {
        // The agent's write: a temp file, then a rename over the store.
        var temp = Path.Combine(_account, ".credentials.json.tmp.4242");
        await File.WriteAllTextAsync(temp, json);
        File.Move(temp, Path.Combine(_account, ".credentials.json"), overwrite: true);
    }

    private static object? Value(AccountLine line, string key) => line.Data.First(each => each.Key == key).Value;

    /// <summary>The 2026-11-24 case: a store emptied by a write reads signed out in the roster at the next look, with its cause, none having asked.</summary>
    [Fact]
    public async Task A_store_emptied_by_a_write_reads_signed_out_at_once_with_its_cause()
    {
        SignedInHoursAgo();
        var lines = new ConcurrentQueue<AccountLine>();
        using var writes = new CredentialWrites(_roster, lines.Enqueue);
        writes.Follow();

        await Written(Emptied);
        await Poll.Until(() => Read() is { Login: LoginState.Out }, () => $"read {Read()?.Login}");

        var state = SignInStates.Of(Read());
        Assert.Equal(SignInStanding.Out, state.Standing);
        Assert.True(state.NotByPerson);
        Assert.Equal(SignInCause.Outside, state.By);

        // The roster composes its report from the same reading; the notice for a sign-out nobody at Daoris made is there too.
        Assert.Single(SignOutNotices.Occurrences(_home, Ten));
        await Poll.Until(
            () => lines.Any(line => line.Event == "credentials.written" && Value(line, "marked") is "out"),
            () => string.Join("; ", lines.Select(line => line.Event)));
        var said = lines.First(line => line.Event == "credentials.written" && Value(line, "marked") is "out");
        Assert.Equal(SignInCause.Outside, Value(said, "by"));
    }

    /// <summary>A write while a session of Daoris's runs on the account says session; with a probe's lock held, probe; during Daoris's own sign-in, sign-in.</summary>
    [Theory]
    [InlineData("session", SignInCause.Session)]
    [InlineData("probe", SignInCause.Probe)]
    [InlineData("sign-in", SignInCause.SignIn)]
    public async Task A_sign_out_says_who_was_at_the_account(string at, string expected)
    {
        SignedInHoursAgo();
        switch (at)
        {
            case "session":
                _roster._looked = [("claude-code", new SessionStarted("claude-code", "account-1", null, Running: true) { Id = "s-running" })];
                break;
            case "probe":
                var lockFile = ProbeLock.PathOf(_home, "claude-code", "account-1");
                Directory.CreateDirectory(Path.GetDirectoryName(lockFile)!);
                File.WriteAllText(lockFile, "{}");
                File.SetLastWriteTimeUtc(lockFile, Ten.UtcDateTime);
                break;
        }

        using var signing = at == "sign-in" ? SignInMarks.Begin(_account) : null;
        if (at == "sign-in") File.SetLastWriteTimeUtc(SignInMarks.PathOf(_home, "claude-code", "account-1"), Ten.UtcDateTime);
        Assert.Equal(at == "sign-in", SignInMarks.Active(_home, "claude-code", "account-1", Ten));
        Assert.False(SignInMarks.Active(_home, "claude-code", "account-1", Ten.Add(SignInMarks.Stale).AddMinutes(1)));
        using var writes = new CredentialWrites(_roster, _ => { });
        writes.Follow();
        await Written(Emptied);
        await Poll.Until(() => Read() is { Login: LoginState.Out }, () => $"read {Read()?.Login}");

        Assert.Equal(expected, Read()!.By);
    }

    /// <summary>The cause is kept with the reading: it survives the file and shows after a restart, and a reading made without one has none.</summary>
    [Fact]
    public void The_cause_is_kept_in_the_reading_and_an_older_reading_has_none()
    {
        AccountReads.Keep(_home, "claude-code", "account-1", LoginState.In, Ten - TimeSpan.FromHours(3));
        AccountReads.Keep(_home, "claude-code", "account-2", LoginState.Out, Ten, by: SignInCause.Session);
        AccountReads.Keep(_home, "claude-code", "account-3", LoginState.Out, Ten);

        var reads = AccountReads.Of(_home, "claude-code").Accounts;
        Assert.Equal((SignInCause.Session, null), (reads["account-2"].By, reads["account-3"].By));
        Assert.Contains("\"by\": \"session\"", File.ReadAllText(AccountReads.PathOf(_home)).Replace("\":\"", "\": \""));
        Assert.Null(reads["account-1"].By);
    }

    /// <summary>A write that fills a store this watch saw cleared marks the account signed in; the same store written again changes nothing.</summary>
    [Fact]
    public async Task A_store_filled_by_a_write_reads_signed_in_at_once()
    {
        File.WriteAllText(Path.Combine(_account, ".credentials.json"), Emptied);
        AccountReads.Keep(_home, "claude-code", "account-1", LoginState.Out, Ten - TimeSpan.FromHours(1), by: SignInCause.Outside);
        using var writes = new CredentialWrites(_roster, _ => { });
        writes.Follow();

        await Written(Filled);
        await Poll.Until(() => Read() is { Login: LoginState.In }, () => $"read {Read()?.Login}");

        Assert.Null(Read()!.By);
    }

    /// <summary>An account its agent's provider refused reads signed out while its store still holds tokens; a refresh's write of that store is no sign-in.</summary>
    [Fact]
    public async Task A_write_that_leaves_tokens_in_a_store_does_not_undo_a_refusal()
    {
        File.WriteAllText(Path.Combine(_account, ".credentials.json"), Filled);
        AccountReads.Keep(_home, "claude-code", "account-1", LoginState.Out, Ten - TimeSpan.FromHours(1), by: SignInCause.Session);
        var lines = new ConcurrentQueue<AccountLine>();
        using var writes = new CredentialWrites(_roster, lines.Enqueue);
        writes.Follow();

        await Written(Filled);
        await Poll.Until(() => lines.Any(line => line.Event == "credentials.written" && Value(line, "file") is "store"), () => "no store line");

        Assert.Equal(LoginState.Out, Read()!.Login);
        Assert.Null(Value(lines.First(line => line.Event == "credentials.written" && Value(line, "file") is "store"), "marked"));
    }

    /// <summary>A store with no bytes is a write half made: it marks nothing, and the one after it that empties the tokens does.</summary>
    [Fact]
    public async Task A_store_with_no_bytes_marks_nothing()
    {
        SignedInHoursAgo();
        var lines = new ConcurrentQueue<AccountLine>();
        using var writes = new CredentialWrites(_roster, lines.Enqueue);
        writes.Follow();

        await File.WriteAllTextAsync(Path.Combine(_account, ".credentials.json"), "");
        await Poll.Until(
            () => lines.Any(line => line.Event == "credentials.written" && Value(line, "file") is "store" && Value(line, "empty") is true),
            () => "no empty store line");
        Assert.Equal(LoginState.In, Read()!.Login);

        await Written(Emptied);
        await Poll.Until(() => Read() is { Login: LoginState.Out }, () => $"read {Read()?.Login}");
    }

    /// <summary>The look's own read of a cleared store, which a stopped watch leaves to it, names no cause: nobody saw the write.</summary>
    [Fact]
    public void A_look_reading_a_cleared_store_names_no_writer()
    {
        SignedInHoursAgo();
        File.WriteAllText(Path.Combine(_account, ".credentials.json"), Emptied);

        Assert.Equal(1, StoredSignIns.Read(_roster));

        Assert.Equal((LoginState.Out, null), (Read()!.Login, Read()!.By));
        Assert.DoesNotContain("by", JsonDocument.Parse(File.ReadAllText(AccountReads.PathOf(_home))).RootElement
            .GetProperty("claude-code").GetProperty("accounts").GetProperty("account-1").EnumerateObject().Select(each => each.Name));
    }
}
