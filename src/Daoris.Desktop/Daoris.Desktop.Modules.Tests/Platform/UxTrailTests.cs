using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The UX trail: kept on the desktop only, off until the person turns it on, and never more than the pages opened and the
/// presses made. Whatever else a window sends (a field's value, typing, a field this build does not name) is not kept.
/// </summary>
public sealed class UxTrailTests : IDisposable
{
    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "_fixtures", "ux-trail", Guid.NewGuid().ToString("N")[..8]);
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 51, 0, TimeSpan.Zero);

    public void Dispose()
    {
        GitSeed.Discard(_home);
    }

    /// <summary>Entries as the bridge reads a window's: what it does not name is not read.</summary>
    private static UxTrailEntry[] Entries(string json) => JsonSerializer.Deserialize<UxTrailEntry[]>(json, Shenora.Core.Ipc.IpcJson.Options)!;

    [Fact]
    public void Off_by_default_it_keeps_nothing_and_writes_no_folder()
    {
        var trail = new UxTrail(_home);

        Assert.False(trail.On);
        Assert.Equal(0, trail.Append(Entries("""[{"kind":"page","page":"/sessions"}]"""), Now));
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.UxTrail)));
    }

    [Fact]
    public void On_it_keeps_pages_presses_and_waits_by_day_and_never_a_value_or_typing()
    {
        var trail = new UxTrail(_home);
        trail.Set(true);

        var kept = trail.Append(Entries("""
            [{"kind":"wait","row":"step:q7/sign","since":"2026-11-16T09:50:00Z","at":"2026-11-16T09:50:01Z"},
             {"kind":"page","page":"/sessions","from":"/overview","at":"2026-11-16T09:50:30Z"},
             {"kind":"press","press":"Approve…","row":"step:q7/sign","page":"/sessions","value":"my secret","at":"2026-11-16T09:51:00Z"},
             {"kind":"typing","text":"hello"}]
            """), Now);

        Assert.Equal(3, kept);
        var lines = File.ReadAllLines(DaorisHome.Entry(_home, HomeEntry.UxTrail, "2026-11-16.jsonl"));
        Assert.Equal(3, lines.Length);
        Assert.DoesNotContain(lines, line => line.Contains("secret", StringComparison.Ordinal) || line.Contains("hello", StringComparison.Ordinal));
        Assert.Equal("Approve…", JsonDocument.Parse(lines[2]).RootElement.GetProperty("press").GetString());

        trail.Set(false);
        Assert.False(trail.On);
        Assert.Equal(0, trail.Append(Entries("""[{"kind":"page","page":"/agents"}]"""), Now));
    }

    [Fact]
    public void On_it_keeps_a_row_whose_question_changed_and_a_row_that_cleared()
    {
        var trail = new UxTrail(_home);
        trail.Set(true);

        var kept = trail.Append(Entries("""
            [{"kind":"changed","row":"go-ahead:a2","at":"2026-11-16T09:50:00Z"},
             {"kind":"cleared","row":"go-ahead:a2","at":"2026-11-16T09:51:00Z"}]
            """), Now);

        Assert.Equal(2, kept);
    }
}
