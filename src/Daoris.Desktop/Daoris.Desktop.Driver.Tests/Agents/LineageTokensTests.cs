using System.Diagnostics;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COWORKVIEW1a (the design §3.6, *who holds the token*): the lineage tokens one Daoris process issues, each held from its issue,
/// watched once handed its process, kept unwatched where it cannot be, and gone once its process's exit is seen or no process ever ran
/// under it, each move told; and the issuer each start names, which a reader compares with itself.
/// </summary>
public sealed class LineageTokensTests
{
    private static readonly DateTime Started = new(2026, 10, 31, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_token_is_sixteen_hex_characters_shaped_as_an_id_and_held_unwatched_from_its_issue()
    {
        var tokens = new LineageTokens(4242, Started);

        var first = tokens.Issue();
        var second = tokens.Issue();

        Assert.Matches(new Regex("^[0-9a-f]{16}$"), first);
        Assert.NotEqual(first, second);
        Assert.Equal(TokenReading.Unwatched, tokens.Reading(first));
        Assert.Equal(TokenReading.Gone, tokens.Reading("0123456789abcdef"));
    }

    [Fact]
    public void Handed_a_live_process_it_is_watched_and_gone_at_the_exit_loop_read_each_move_told_once()
    {
        var tokens = new LineageTokens(4242, Started);
        var moved = new List<string>();
        tokens.Moved += moved.Add;
        var token = tokens.Issue();
        using var alive = Process.GetCurrentProcess();

        tokens.Hand(token, alive);
        Assert.Equal(TokenReading.Watched, tokens.Reading(token));
        tokens.Exited(token);
        tokens.Exited(token);

        Assert.Equal(TokenReading.Gone, tokens.Reading(token));
        Assert.Equal([token, token], moved);
    }

    [Fact]
    public void A_process_it_cannot_watch_keeps_its_token_unwatched_until_loop_tells_the_exit()
    {
        var tokens = new LineageTokens(4242, Started);
        var moved = new List<string>();
        tokens.Moved += moved.Add;
        var token = tokens.Issue();

        tokens.Hand(token, null);

        Assert.Equal(TokenReading.Unwatched, tokens.Reading(token));
        Assert.Equal([token], moved);
        tokens.Exited(token);
        Assert.Equal(TokenReading.Gone, tokens.Reading(token));
    }

    [Fact]
    public void A_spawn_that_made_no_process_or_a_run_put_back_lets_its_token_go_once()
    {
        var tokens = new LineageTokens(4242, Started);
        var moved = new List<string>();
        tokens.Moved += moved.Add;
        var first = tokens.Issue();
        var second = tokens.Issue();

        tokens.Unspawned(first);
        tokens.Unspawned(first);

        Assert.Equal(TokenReading.Gone, tokens.Reading(first));
        Assert.Equal(TokenReading.Unwatched, tokens.Reading(second));
        Assert.Equal([first], moved);
    }

    [Fact]
    public void A_start_names_this_process_by_its_id_and_start_time_and_a_reader_knows_only_itself()
    {
        var tokens = new LineageTokens(4242, Started);

        Assert.Equal($"4242 {Started.Ticks}", tokens.Issuer);
        Assert.True(tokens.Issued(tokens.Issuer));
        Assert.True(tokens.Issued($"4242 {Started.AddMilliseconds(400).Ticks}"));
        Assert.False(tokens.Issued($"4242 {Started.AddSeconds(5).Ticks}"));
        Assert.False(tokens.Issued($"4243 {Started.Ticks}"));
        Assert.False(tokens.Issued("4242"));
        Assert.False(tokens.Issued("not an issuer"));
        Assert.False(tokens.Issued(null));
    }

    [Fact]
    public void A_flow_handed_a_set_of_its_own_issues_from_it_and_the_process_s_set_is_one()
    {
        var own = new LineageTokens(4242, Started);

        using (LineageTokens.Within(own)) Assert.Same(own, LineageTokens.Current);

        Assert.Same(LineageTokens.Shared, LineageTokens.Current);
        Assert.True(LineageTokens.Shared.Issued(LineageTokens.Shared.Issuer));
    }
}

/// <summary>COWORKVIEW1a: a watched token leaves at its process's real exit, seen through the set's own handle, told once.</summary>
[Trait(Category.Name, Category.Process)]
public sealed class LineageTokensProcessTests
{
    [Fact]
    public async Task A_watched_token_leaves_at_its_process_s_exit_with_no_word_from_loop()
    {
        var tokens = new LineageTokens(4242, DateTime.UtcNow);
        var gone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var token = tokens.Issue();
        tokens.Moved += moved =>
        {
            if (moved == token && tokens.Reading(token) == TokenReading.Gone) gone.TrySetResult();
        };
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe")) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "/d", "/c", "ping -n 2 127.0.0.1 >nul" }) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;

        tokens.Hand(token, process);
        Assert.Equal(TokenReading.Watched, tokens.Reading(token));
        await gone.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(process.HasExited);
        Assert.Equal(TokenReading.Gone, tokens.Reading(token));
    }

    [Fact]
    public void A_process_already_exited_when_handed_lets_its_token_go()
    {
        var tokens = new LineageTokens(4242, DateTime.UtcNow);
        var token = tokens.Issue();
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe")) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "/d", "/c", "exit 0" }) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        process.WaitForExit();

        tokens.Hand(token, process);

        Assert.Equal(TokenReading.Gone, tokens.Reading(token));
    }
}
