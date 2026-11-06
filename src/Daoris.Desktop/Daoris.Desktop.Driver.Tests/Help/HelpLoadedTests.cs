using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HELPWIRED1b (the Ask Daoris modes design §6): what the door said it loaded, read from its recorded init frame, kept on the receipt of
/// the start it said it for and answered beside what that start handed.
/// </summary>
public sealed class HelpLoadedTests : IDisposable
{
    private const string Init = """
        {"type":"system","subtype":"init","session_id":"c1","tools":["Read","mcp__daoris-knowledge__registry"],
         "mcp_servers":[{"name":"daoris-knowledge","status":"connected"},{"name":"tickets","status":"failed"}],
         "skills":["triage"],"plugins":[{"name":"acme.tickets","path":"x"}]}
        """;

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "help-loaded", Guid.NewGuid().ToString("N")[..8]);

    public HelpLoadedTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    private static HelpReceipt Receipt(string at) =>
        new() { At = DateTimeOffset.Parse(at), Harness = "claude-code", Folder = "room" };

    [Fact]
    public void An_init_frame_is_read_as_what_the_door_says_it_loaded_and_no_other_frame_is()
    {
        var loaded = NativeInit.Read(NativeFrame.Of(Init))!;

        Assert.Equal(["Read", "mcp__daoris-knowledge__registry"], loaded.Tools);
        Assert.Equal([new WireServer("daoris-knowledge", "connected"), new WireServer("tickets", "failed")], loaded.Servers);
        Assert.Equal(["triage"], loaded.Skills);
        Assert.Equal(["acme.tickets"], loaded.Plugins);
        Assert.Null(NativeInit.Read(NativeFrame.Of("""{"type":"system","subtype":"status"}""")));
        Assert.Null(NativeInit.Read(NativeFrame.Of("not json")));
    }

    [Fact]
    public void The_first_init_frame_of_a_start_is_kept_on_its_newest_receipt_beside_what_was_handed_and_answered()
    {
        var wired = new HelpWired(_home);
        wired.Keep("help0001", Receipt("2026-11-06T09:00:00Z"));
        wired.Keep("help0001", Receipt("2026-11-06T10:00:00Z"));
        var part = HelpLoaded.Part(_home, "help0001");
        var loaded = NativeInit.Read(NativeFrame.Of(Init))!;

        part.Read(new WireReading.Loaded(loaded));
        part.Read(new WireReading.Loaded(loaded with { Skills = [] }));
        part.Read(new WireReading.Loaded(loaded) { Replayed = true });

        var receipts = wired.Read("help0001");
        Assert.Equal(loaded, receipts[0].Loaded);
        Assert.Null(receipts[1].Loaded);
        var answer = HelpWired.Answer("help0001", receipts).GetProperty("receipts")[0].GetProperty("loaded");
        Assert.Equal("failed", answer.GetProperty("servers")[1].GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, HelpWired.Answer("help0001", receipts).GetProperty("receipts")[1].GetProperty("loaded").ValueKind);
    }

    [Fact]
    public void A_conversation_with_no_receipt_keeps_nothing_of_what_its_door_said()
    {
        var wired = new HelpWired(_home);

        HelpLoaded.Part(_home, "chat0001").Read(new WireReading.Loaded(NativeInit.Read(NativeFrame.Of(Init))!));

        Assert.False(File.Exists(wired.PathOf("chat0001")));
    }
}
