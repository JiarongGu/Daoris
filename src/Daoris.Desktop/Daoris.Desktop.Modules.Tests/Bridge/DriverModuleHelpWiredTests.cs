using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// HELPWIRED1 over the bridge (D183 point 4, `HelpRoutes.cs`): <c>HELP_WIRED</c> answers what each start of one Ask Daoris
/// conversation handed its agent, newest first, as the start kept it beside its other files; a conversation from before answers
/// none recorded. A read, answered from this machine's home alone: no service is asked, and nothing is spawned.
/// </summary>
public sealed class DriverModuleHelpWiredTests : DriverModuleBridge
{
    private static HelpReceipt Receipt(string at, string mode, string start) => new()
    {
        At = DateTimeOffset.Parse(at), Start = start, Mode = mode, Harness = "claude-code-acp", Version = null, Door = HelpWired.Acp,
        Posture = mode == HelpModes.Full ? "bypassPermissions" : HelpRoom.Posture, Folder = "room",
        Servers = [new HelpWiredServer("tickets", "acme.tickets", HelpWired.Handed, ["search"])],
        Rules = new HelpWiredRules(["mcp__tickets__search"], [], []),
    };

    [Fact]
    public async Task HELP_WIRED_answers_a_conversation_s_receipts_newest_first_with_its_nulls()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        var wired = new HelpWired(loop.Home);
        wired.Keep("h1", Receipt("2026-10-19T09:00:00Z", HelpModes.Restricted, HelpWired.New));
        wired.Keep("h1", Receipt("2026-10-19T10:00:00Z", HelpModes.Full, HelpWired.GoingOn));

        var answer = await AnswerAsync(module, "HELP_WIRED", new { session = "h1" });

        Assert.Equal("h1", answer.GetProperty("session").GetString());
        Assert.True(answer.GetProperty("recorded").GetBoolean());
        var receipts = answer.GetProperty("receipts").EnumerateArray().ToList();
        Assert.Equal([HelpModes.Full, HelpModes.Restricted], receipts.Select(receipt => receipt.GetProperty("mode").GetString()));
        Assert.Equal([HelpWired.GoingOn, HelpWired.New], receipts.Select(receipt => receipt.GetProperty("start").GetString()));
        // A fact not known is answered as null, never left out.
        Assert.Equal(JsonValueKind.Null, receipts[0].GetProperty("version").ValueKind);
        Assert.Equal(JsonValueKind.Null, receipts[0].GetProperty("account").ValueKind);
        var server = Assert.Single(receipts[0].GetProperty("servers").EnumerateArray());
        Assert.Equal(("tickets", "handed"), (server.GetProperty("name").GetString(), server.GetProperty("state").GetString()));
        Assert.Equal(["mcp__tickets__search"], receipts[0].GetProperty("rules").GetProperty("allow").EnumerateArray().Select(rule => rule.GetString()));
    }

    [Fact]
    public async Task A_conversation_started_before_receipts_answers_not_recorded()
    {
        var module = Module();

        var answer = await AnswerAsync(module, "HELP_WIRED", new { session = "h9" });

        Assert.False(answer.GetProperty("recorded").GetBoolean());
        Assert.Empty(answer.GetProperty("receipts").EnumerateArray());
    }

    [Fact]
    public async Task A_field_the_file_holds_that_a_receipt_has_no_place_for_is_never_answered()
    {
        var loop = Loop();
        var wired = new HelpWired(loop.Home);
        Directory.CreateDirectory(Path.GetDirectoryName(wired.PathOf("h2"))!);
        File.WriteAllText(wired.PathOf("h2"), """
            { "receipts": [ { "at": "2026-10-19T09:00:00Z", "start": "new", "mode": "restricted", "harness": "claude-code",
              "door": "pipe", "folder": "room", "token": "sk-hostile-file",
              "servers": [ { "name": "tickets", "state": "handed", "env": { "TICKETS_TOKEN": "sk-hostile-env" }, "args": ["--key", "sk-hostile-arg"] } ] } ] }
            """);

        var answer = await AnswerAsync(new DriverModule(Bus, loop), "HELP_WIRED", new { session = "h2" });

        Assert.True(answer.GetProperty("recorded").GetBoolean());
        Assert.Contains("tickets", answer.GetRawText());
        Assert.DoesNotContain("sk-hostile", answer.GetRawText());
    }
}
