using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// METHODS1 b: the desk's design pass. A design pass's packet is its purpose brief alone, so the desk refuses a source beside it
/// and any pass but <c>design</c>, and writes the pass where the driver reads it.
/// </summary>
public sealed class BrainstormDesignDeskTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "brainstorm-design-" + Guid.NewGuid().ToString("N")[..8]);

    public BrainstormDesignDeskTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void A_design_pass_is_written_with_its_pass_and_asked_again_in_the_same_words_is_the_same()
    {
        var desk = new BrainstormDesk(_home);

        var asked = desk.Ask("A tool that runs a company of agents.", "daoris", ["a", "b"], At, "d1", pass: "design");

        Assert.Equal(BrainstormRefusal.None, asked.Refusal);
        using var request = JsonDocument.Parse(File.ReadAllText(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Brainstorms, "d1"), "request.json")));
        Assert.Equal("design", request.RootElement.GetProperty("pass").GetString());
        Assert.Equal(BrainstormRefusal.None, desk.Ask("A tool that runs a company of agents.", "daoris", ["a", "b"], At, "d1", pass: "design").Refusal);
        Assert.Equal(BrainstormRefusal.State, desk.Ask("A tool that runs a company of agents.", "daoris", ["a", "b"], At, "d1").Refusal);
    }

    [Fact]
    public void A_design_pass_with_a_source_or_another_pass_is_refused_with_nothing_written()
    {
        var desk = new BrainstormDesk(_home);

        var sourced = desk.Ask("The purpose.", "daoris", null, At, "d2", ["docs/a.md"], "design");
        var other = desk.Ask("The purpose.", "daoris", null, At, "d3", pass: "review");

        Assert.Equal(BrainstormRefusal.BadShape, sourced.Refusal);
        Assert.Contains("purpose brief alone", sourced.Message, StringComparison.Ordinal);
        Assert.Equal(BrainstormRefusal.BadShape, other.Refusal);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Brainstorms, "d2")));
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Brainstorms, "d3")));
    }
}
