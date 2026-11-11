using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// On the native door a working session's or a conversation's settings file carries the permission ask beside the tree guard and the
/// lineage hook, last, handed the requests box, its session, the file itself and the hold's bound; a session handed none carries none.
/// A permit the script writes is held for the person in the registry the card and the allow supervisor read, and answered beside it.
/// </summary>
public sealed class ClaudeAskHookTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-ask-" + Guid.NewGuid().ToString("N")[..8]);

    public ClaudeAskHookTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public void The_rules_file_carries_the_ask_last_on_every_tool_with_its_box_session_file_and_bound()
    {
        var node = Path.Combine(_home, "stand-node.exe");
        AtomicFile.WriteText(node, "stand-in, never executed");
        Tools.UseFile(_home, "node", node);
        var guard = TreeGuard.For(_home, _home, []);

        var file = ClaudeRules.Language.Write(_home, "s1", new HandedRules(RuleLists.Empty, [], guard, ChildLineage.PathOf(_home, "s1")) { Asked = true });

        using var document = JsonDocument.Parse(File.ReadAllText(file));
        var before = document.RootElement.GetProperty("hooks").GetProperty("PreToolUse").EnumerateArray().ToList();
        Assert.Equal([TreeGuardHook.Matcher, LineageHook.Matcher, AskHook.Matcher], before.Select(entry => entry.GetProperty("matcher").GetString()));
        var ask = Assert.Single(before[2].GetProperty("hooks").EnumerateArray());
        var script = Path.Combine(DaorisHome.Entry(_home, HomeEntry.Hooks), ClaudeAskHook.ScriptName);
        Assert.Equal(node, ask.GetProperty("command").GetString());
        Assert.Equal(
            [script, DaorisHome.Entry(_home, HomeEntry.Requests), "s1", file, "900"],
            ask.GetProperty("args").EnumerateArray().Select(arg => arg.GetString()));
        Assert.Equal(960, ask.GetProperty("timeout").GetInt32());
        Assert.Equal(ClaudeAskHook.Source, File.ReadAllText(script));
    }

    [Fact]
    public void A_session_not_asked_is_handed_no_ask()
    {
        var file = ClaudeRules.Language.Write(_home, "s1", new HandedRules(RuleLists.Empty, [], null, ChildLineage.PathOf(_home, "s1")));

        using var document = JsonDocument.Parse(File.ReadAllText(file));
        Assert.DoesNotContain(
            document.RootElement.GetProperty("hooks").GetProperty("PreToolUse").EnumerateArray(),
            entry => entry.GetProperty("matcher").GetString() == AskHook.Matcher);
    }

    [Theory]
    [InlineData("allow", true, PermissionWhy.Answered)]
    [InlineData(null, false, PermissionWhy.Answered)]
    public async Task A_permit_is_held_for_the_person_and_answered_beside_it(string? option, bool allowed, string why)
    {
        var requests = new SessionRequests(_home);
        var registry = new HeldPermissions();
        var asked = new SessionRequest("s1a2b3c4", SessionMove.Permit, DateTimeOffset.UtcNow)
        {
            Key = "a1b2c3d4e5f6", Operation = "Bash", Text = "Bash: npm test", By = "hook", Call = "toolu_01",
        };

        var holding = NativePermits.HoldAsync(requests, registry, asked);
        var held = Assert.Single(registry.Held("s1a2b3c4"));
        Assert.Equal(("a1b2c3d4e5f6", "Bash: npm test", "execute", "toolu_01"), (held.Request, held.Title, held.Kind, held.Call));
        Assert.Equal(["allow", "reject"], held.Choices.Select(choice => choice.Id));
        Assert.True(registry.Answer("s1a2b3c4", "a1b2c3d4e5f6", option).Answered);
        await holding;

        var answer = JsonDocument.Parse(File.ReadAllText(Path.Combine(requests.Folder, "s1a2b3c4.a1b2c3d4e5f6.answer.json"))).RootElement;
        Assert.Equal((allowed, why), (answer.GetProperty("done").GetBoolean(), answer.GetProperty("code").GetString()));
        Assert.Empty(registry.Held("s1a2b3c4"));
    }
}
