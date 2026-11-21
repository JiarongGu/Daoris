using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.AcpBackgroundTests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A subagent's permission request rises with its subagent (SESSIONVIEW1f2): one its own session asks on the wire is held tagged
/// with that session, the id the record keeps its lifetime under; the session's own agent's is held with none. No process and no
/// model, so the fast half.
/// </summary>
public sealed class AcpPermissionChildTests
{
    [Theory]
    [InlineData("child-7", "child-7")]
    [InlineData("s-1", null)]
    public async Task A_request_asked_on_a_subagents_own_session_is_held_tagged_with_it(string asker, string? child)
    {
        var registry = new HeldPermissions();
        var agent = new Agent((frame, self) =>
        {
            if (!frame.TryGetProperty("method", out var method))
            {
                if (frame.TryGetProperty("id", out var id) && id.GetRawText() == "900")
                {
                    self.Push(Update("""{"sessionUpdate":"tool_call_update","toolCallId":"c9","status":"failed"}"""));
                    self.Push(Answer(self.PromptIds[^1], "end_turn"));
                }

                return null;
            }

            switch (method.GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1","modes":{"currentModeId":"default","availableModes":[{"id":"default"},{"id":"acceptEdits"},{"id":"bypassPermissions"}]}}""");
                case "session/prompt":
                    self.Push(Update("""{"sessionUpdate":"tool_call","toolCallId":"c9","title":"npm start","kind":"execute","status":"pending"}"""));
                    self.Push($$$"""{"jsonrpc":"2.0","id":900,"method":"session/request_permission","params":{"sessionId":"{{{asker}}}","toolCall":{"toolCallId":"c9","title":"npm start","kind":"execute"},"options":[{"optionId":"yes","name":"Allow","kind":"allow_once"},{"optionId":"no","name":"Reject","kind":"reject_once"}]}}""");
                    return null;
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var asking = DrivenAccess.For(
            AccessRules.ResolveLevels(new AccessSetting { Pauses = new Dictionary<string, string> { [AccessRules.Permissions] = AccessRules.Asking } }, null, null),
            "engine", BuiltDoors.Of("claude-code-acp"));
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, closeTimeout: null, asking.Posture)
        {
            Unoffered = asking.Unoffered,
            Permissions = registry.For("d1"),
        };

        var run = session.RunAsync("X:/daoris/engine", "start the server", CancellationToken.None);
        HeldPermission? held = null;
        await Poll.Until(() => (held = registry.Held("d1").FirstOrDefault()) is not null, () => "nothing was held for `d1`");

        Assert.Equal(("c9", child), (held!.Call, held.Child));
        Assert.True(registry.Answer("d1", "900", "no").Answered);
        await run.WaitAsync(Poll.Patience);
    }
}
