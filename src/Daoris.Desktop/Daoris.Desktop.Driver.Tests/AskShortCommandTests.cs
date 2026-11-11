using System.Net;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An ask's short title at a terminal, the twin of the composer's field and of the one Ask Daoris's
/// ask card says. <c>daoris ask … --short "…"</c> sends it as typed; the service judges it, so a title too long reaches the door
/// unchanged and the refusal is the service's sentence, printed verbatim. A service stood in: the fast half.
/// </summary>
public sealed class AskShortCommandTests
{
    private const string Refusal = "A short title is at most 40 characters on one line. Nothing was asked.";

    /// <summary>The ask's door hears the title as typed, and nothing where none is given; a too-long one is the service's to refuse.</summary>
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

    /// <summary>The verb's words read the flag, refuse it beside another act and name it in the usage; the new ask hands it to the door.</summary>
    [Fact]
    public void The_verb_reads_it()
    {
        Assert.Equal("Stall", AskArguments.Read(["--short", "Stall", "fix it"], out _)!.Short);
        Assert.Null(AskArguments.Read(["--short", "Stall", "--close", "a1"], out var problem));
        Assert.Contains("--short goes with a new ask's words", problem);
        Assert.Contains("daoris ask … --short \"…\"", AskArguments.Usage);
        var made = File.ReadAllText(Path.Combine(
            Daoris.Desktop.Driver.Tests.WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver", "WorkOrders", "AskNewCommand.cs"));
        Assert.Contains("shortTitle: ask.Short", made);
    }
}
