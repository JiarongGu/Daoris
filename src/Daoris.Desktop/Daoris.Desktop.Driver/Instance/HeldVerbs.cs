using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The verbs that open or take a conversation the running Daoris runs and hold it:
/// <c>daoris chat --repository</c> and <c>--session</c>, <c>session start-from</c> and
/// <c>session hand-over</c>. Each sends the route the window presses, with <c>hold: true</c> where it opens one, or <c>HOLD</c> where it
/// takes one already open, then keeps the connection as the conversation (<see cref="HeldChat"/>). With no Daoris running it runs nothing.
/// </summary>
public static class HeldVerbs
{
    /// <summary>The fields <c>chat --repository</c> asks a start with, as the window's <c>START_CHAT</c> names them.</summary>
    public sealed record ChatStart(string Repository, string? Agent, string? Account, bool OwnTree);

    /// <summary><c>daoris chat --repository &lt;name&gt;</c>: a conversation started in Daoris, with no agent named Chat's order walked there, and held.</summary>
    public static Task<int> StartAsync(InstanceWorld world, ChatStart start, Func<Action, IDisposable>? interrupts = null, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?> { ["repository"] = start.Repository };
        if (!string.IsNullOrWhiteSpace(start.Agent)) payload["door"] = start.Agent;
        if (!string.IsNullOrWhiteSpace(start.Account)) payload["profile"] = start.Account;
        if (start.OwnTree) payload["ownTree"] = true;
        return HoldAsync(world, TreesRoutes.DriverModule, "START_CHAT", payload, hold: true, "chat", interrupts, ct);
    }

    /// <summary><c>daoris chat --session &lt;id&gt;</c>: a conversation Daoris already has, held once the person confirms it.</summary>
    public static Task<int> HoldSessionAsync(InstanceWorld world, string session, Func<Action, IDisposable>? interrupts = null, CancellationToken ct = default) =>
        HoldAsync(world, InstanceClient.PipeModule, "HOLD", new { session }, hold: false, "chat", interrupts, ct);

    /// <summary><c>daoris session start-from &lt;id&gt;</c>: the words a session could not go on with open a conversation in Daoris, which is held.</summary>
    public static Task<int> StartFromAsync(InstanceWorld world, string session, Func<Action, IDisposable>? interrupts = null, CancellationToken ct = default) =>
        HoldAsync(world, TreesRoutes.DriverModule, "SESSION_START_FROM", new { id = session }, hold: true, "sessions", interrupts, ct);

    /// <summary>
    /// <c>daoris session hand-over &lt;id&gt; --agent …</c>: the conversation goes on with another agent in a new one, which is held; where the
    /// answer names none (a quest's own session, an ask kept for a turn's end) its sentence is all.
    /// </summary>
    public static Task<int> HandOverAsync(
        InstanceWorld world, string session, string agent, string? account, Func<Action, IDisposable>? interrupts = null, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?> { ["id"] = session, ["agent"] = agent };
        if (!string.IsNullOrWhiteSpace(account)) payload["account"] = account;
        return HoldAsync(world, TreesRoutes.DriverModule, "SESSION_HAND_OVER", payload, hold: true, "sessions", interrupts, ct);
    }

    private static async Task<int> HoldAsync(
        InstanceWorld world, string module, string type, object payload, bool hold, string verb, Func<Action, IDisposable>? interrupts, CancellationToken ct)
    {
        var chat = new HeldChat(world);
        var opened = await HeldDoor.OpenAsync(world, module, type, payload, hold, chat.Hear, ct).ConfigureAwait(false);
        if (opened.Held is not { } held) return opened.Exit;

        await using (held.ConfigureAwait(false))
        {
            var data = opened.Data;
            var session = InstanceClient.Text(data, "sessionId") ?? InstanceClient.Text(data, "session");
            // What it came to, in the sentence of the route's own answer, as the verb said it when it ran the conversation itself.
            if (InstanceClient.Text(data, "message") is { } message) await world.Error.WriteLineAsync($"{verb}: {message}").ConfigureAwait(false);
            if (session is null)
            {
                var kept = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("kept", out var did) && did.ValueKind == JsonValueKind.True;
                return type == "SESSION_HAND_OVER" && kept ? 0 : 1;
            }

            using var _ = interrupts?.Invoke(chat.Interrupt);
            return await chat.RunAsync(held, session, ct).ConfigureAwait(false);
        }
    }
}
