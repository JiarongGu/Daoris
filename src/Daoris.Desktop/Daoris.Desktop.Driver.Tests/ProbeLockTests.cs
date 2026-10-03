using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL6g: one status question per configuration home at a time, across processes (<see cref="ProbeLock"/>). The CLI's
/// <c>probelock.test.ts</c> holds the same rows: where each lock lives, and when one is taken for a holder that died.
/// </summary>
public sealed class ProbeLockTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-probe-lock-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Where each lock lives, relative to the home: the twin's <c>PATH_ROWS</c>, which reads these rows, cell for cell.</summary>
    [Theory]
    [InlineData("claude-code", "account-1", "harnesses/.probing/claude-code/account-1.lock")]
    [InlineData("claude-code", "work.account", "harnesses/.probing/claude-code/work.account.lock")]
    [InlineData("claude-code", null, "harnesses/.probing/claude-code.lock")]
    [InlineData("codex", "account-2", "harnesses/.probing/codex/account-2.lock")]
    public void Each_lock_lives_beside_the_accounts_never_inside_one(string owner, string? profile, string relative)
    {
        var path = ProbeLock.PathOf(_home, owner, profile);

        Assert.Equal(relative, Path.GetRelativePath(_home, path).Replace('\\', '/'));
        Assert.False(path.StartsWith(Path.Combine(_home, "harnesses", owner) + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    /// <summary>Where a sign-in or a key into an account is marked: the twin's <c>MARK_ROWS</c> reads these.</summary>
    [Theory]
    [InlineData("claude-code", "account-1", "harnesses/.probing/claude-code/account-1.signed-in")]
    [InlineData("codex", "work", "harnesses/.probing/codex/work.signed-in")]
    public void A_sign_in_is_marked_beside_the_account_s_lock(string owner, string profile, string relative) =>
        Assert.Equal(relative, Path.GetRelativePath(_home, ProbeLock.SignedInPathOf(_home, owner, profile)).Replace('\\', '/'));

    [Fact]
    public void A_sign_in_into_an_account_of_the_layout_is_marked_and_one_elsewhere_is_not()
    {
        Assert.Null(ProbeLock.SignedIn(_home, "claude-code", "account-1"));

        ProbeLock.MarkSignedIn(HarnessSettings.ProfileHome(_home, "claude-code", "account-1"), DateTimeOffset.UtcNow);
        ProbeLock.MarkSignedIn(Path.Combine(_home, "elsewhere", "account-2"), DateTimeOffset.UtcNow);

        Assert.NotNull(ProbeLock.SignedIn(_home, "claude-code", "account-1"));
        Assert.Null(ProbeLock.SignedIn(_home, "claude-code", "account-2"));
        Assert.False(Directory.Exists(Path.Combine(_home, "elsewhere", "harnesses")));
    }

    /// <summary>When a lock file is taken for a holder that died, by its age in seconds: the twin's <c>STALE_ROWS</c> reads these.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(59, false)]
    [InlineData(61, true)]
    [InlineData(3600, true)]
    public void A_lock_older_than_a_minute_is_a_holder_that_died(int seconds, bool stale)
    {
        var path = ProbeLock.PathOf(_home, "claude-code", "account-1");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}\n");
        var now = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(path, now.AddSeconds(-seconds));

        Assert.Equal(stale, ProbeLock.IsStale(path, now));
    }

    [Fact]
    public void A_lock_that_is_not_there_is_not_stale() =>
        Assert.False(ProbeLock.IsStale(ProbeLock.PathOf(_home, "claude-code", "account-1"), DateTime.UtcNow));

    [Fact]
    public async Task A_lock_taken_is_a_file_until_it_is_let_go()
    {
        var path = ProbeLock.PathOf(_home, "claude-code", "account-1");

        var held = await ProbeLock.TakeAsync(path);

        Assert.NotNull(held);
        Assert.True(File.Exists(path));
        using (var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)))
        {
            Assert.Contains("\"pid\":", reader.ReadToEnd());
        }
        await held!.DisposeAsync();
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task A_second_taker_in_this_process_waits_for_the_first()
    {
        var path = ProbeLock.PathOf(_home, "claude-code", "account-1");
        var first = await ProbeLock.TakeAsync(path);

        var second = await ProbeLock.TakeAsync(path, TimeSpan.FromMilliseconds(300));

        Assert.Null(second);
        await first!.DisposeAsync();
        var third = await ProbeLock.TakeAsync(path, TimeSpan.FromSeconds(5));
        Assert.NotNull(third);
        await third!.DisposeAsync();
    }

    [Fact]
    public async Task A_lock_another_process_holds_keeps_a_taker_out_until_its_patience_ends()
    {
        var path = ProbeLock.PathOf(_home, "claude-code", "account-1");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"pid\":1}\n");

        var held = await ProbeLock.TakeAsync(path, TimeSpan.FromMilliseconds(300));

        Assert.Null(held);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task A_lock_a_dead_holder_left_is_taken()
    {
        var path = ProbeLock.PathOf(_home, "claude-code", "account-1");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"pid\":1}\n");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-5));

        var held = await ProbeLock.TakeAsync(path, TimeSpan.FromSeconds(5));

        Assert.NotNull(held);
        await held!.DisposeAsync();
    }

    [Fact]
    public async Task Two_accounts_are_two_locks()
    {
        var one = await ProbeLock.TakeAsync(ProbeLock.PathOf(_home, "claude-code", "account-1"));
        var two = await ProbeLock.TakeAsync(ProbeLock.PathOf(_home, "claude-code", "account-2"), TimeSpan.FromMilliseconds(300));

        Assert.NotNull(one);
        Assert.NotNull(two);
        await one!.DisposeAsync();
        await two!.DisposeAsync();
    }
}
