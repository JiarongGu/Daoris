using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Each working role's last seven days (MODELROLE1f, D205): starts, on which choice, how many fell to a later one and why, summed from the
/// machine log's <c>actor.chose</c> lines. A role the log has no start of is absent, never zero.
/// </summary>
public sealed class RoleUseTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "role-use-" + Guid.NewGuid().ToString("N")[..8]);

    public RoleUseTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Write(string day, params string[] lines)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Logs);
        Directory.CreateDirectory(folder);
        File.AppendAllLines(Path.Combine(folder, $"{day}.driver.jsonl"), lines);
    }

    private static string Chose(string day, string actor, string choice, params string[] passed) =>
        "{\"time\":\"" + day + "T09:00:00.000Z\",\"source\":\"driver\",\"level\":\"info\",\"event\":\"actor.chose\",\"data\":{\"actor\":\"" + actor
        + "\",\"choice\":\"" + choice + "\",\"agent\":\"claude\",\"model\":null,\"passed\":[" + string.Join(",", passed.Select(word => "\"" + word + "\"")) + "]}}";

    /// <summary>The sum's table: starts per role, per choice, and the later ones with their codes.</summary>
    [Fact]
    public void The_sum_of_a_week_of_starts_per_role()
    {
        Write("2026-10-30",
            Chose("2026-10-30", "build", "1"),
            Chose("2026-10-30", "build", "2", "1:codex:cooling"),
            Chose("2026-10-30", "build", "3", "1:codex:cooling", "2:claude:signed-out"),
            Chose("2026-10-30", "chat", "before"));
        Write("2026-10-25", Chose("2026-10-25", "build", "1"));

        var read = RoleUse.Of(_home, Now);

        var build = Assert.Single(read.Roles, each => each.Role == "build");
        Assert.Equal(4, build.Starts);
        Assert.Equal([("1", 2), ("2", 1), ("3", 1)], build.Choices.Select(each => (each.Choice, each.Starts)));
        Assert.Equal(2, build.Fell);
        Assert.Equal([("cooling", 2), ("signed-out", 1)], build.Why.Select(each => (each.Code, each.Starts)));
        var chat = Assert.Single(read.Roles, each => each.Role == "chat");
        Assert.Equal((1, 0), (chat.Starts, chat.Fell));
        Assert.Equal(7, read.Days);
    }

    /// <summary>A role with no start in the week is not there: absent stays absent.</summary>
    [Fact]
    public void A_role_without_a_start_this_week_is_absent_and_no_history_is_no_roles()
    {
        Assert.Empty(RoleUse.Of(_home, Now).Roles);

        Write("2026-10-20", Chose("2026-10-20", "build", "1"));
        Write("2026-10-31", """{"time":"2026-10-31T09:00:00.000Z","source":"driver","level":"info","event":"app.started","data":{}}""", "torn");

        Assert.Empty(RoleUse.Of(_home, Now).Roles);
    }

    /// <summary>A log that keeps fewer than seven days says the span it read.</summary>
    [Fact]
    public void A_log_of_fewer_days_says_its_span()
    {
        Write("2026-10-29", Chose("2026-10-29", "build", "1"));

        Assert.Equal(3, RoleUse.Of(_home, Now).Days);
    }
}
