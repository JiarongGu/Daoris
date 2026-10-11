using System.Net;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HEAD2e2 (D162's HEAD2e note, D50): an ask's short title at a terminal, the twin of the composer's field and of the one Ask Daoris's
/// ask card says. <c>daoris-driver ask … --short "…"</c> sends it as typed; the service judges it, so a title too long reaches the door
/// unchanged and the refusal is the service's sentence, printed verbatim. A service stood in: the fast half (MOD8).
/// </summary>
public sealed class AskShortCommandTests
{
    private const string Refusal = "A short title is at most 40 characters on one line. Nothing was asked.";

    /// <summary>The ask's door hears the title as typed, and nothing where none was given; a too-long one is the service's to refuse.</summary>
    [Fact]
    public async Task A_new_ask_carries_the_short_title_to_the_asks_door_unjudged()
    {
        var tooLong = new string('x', 41);
        var (service, heard) = ReviewChoiceStandIn.Of((method, path, body) => (method, path) is ("POST", "/api/asks")
            ? body.Contains(tooLong, StringComparison.Ordinal)
                ? (HttpStatusCode.BadRequest, JsonSerializer.Serialize(new { error = Refusal }))
                : (HttpStatusCode.OK, """{"ask":{"id":"a1b2c3"},"message":"Asked as `#a1b2c3` in `default`."}""")
            : null);

        Assert.True((await service.AskAsync("default", "fix the stall", [], [], null, shortTitle: "Cold-cache stall")).Ok);
        await service.AskAsync("default", "fix the stall", [], [], null);
        var refused = await service.AskAsync("default", "fix the stall", [], [], null, shortTitle: tooLong);

        using var titled = JsonDocument.Parse(heard[0].Body);
        Assert.Equal("Cold-cache stall", titled.RootElement.GetProperty("short").GetString());
        using var none = JsonDocument.Parse(heard[1].Body);
        Assert.False(none.RootElement.TryGetProperty("short", out _));
        using var long41 = JsonDocument.Parse(heard[2].Body);
        Assert.Equal(tooLong, long41.RootElement.GetProperty("short").GetString());
        Assert.False(refused.Ok);
        Assert.Equal(Refusal, refused.Message);
    }

    [Fact]
    public void The_usage_names_it()
    {
        Assert.Contains("\n  ask … --short \"…\"\n", DriverCommand.Usage.ReplaceLineEndings("\n"));
    }

    /// <summary>The console reads the flag, hands it to the door, refuses it beside another verb, and names it in its usage: read as text.</summary>
    [Fact]
    public void The_console_reads_it()
    {
        var console = File.ReadAllText(Path.Combine(
            Daoris.Driver.Tests.HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host", "AskConsole.cs"));
        Assert.Contains("case \"--short\": shortTitle = Value(); break;", console);
        Assert.Contains("shortTitle: shortTitle", console);
        Assert.Contains("--short goes with a new ask's words", console);
        Assert.Contains("daoris-driver ask … --short \\\"…\\\"", console);
    }
}
