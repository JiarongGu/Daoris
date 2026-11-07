using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The permission posture Daoris drives a session under, expressed as the wire's own mode: read from the adapter's posture
/// string, and set on the session once it answers its open.
/// </summary>
/// <remarks>
/// <para><b>A session does as much as the harness would on its
/// own</b>, so an adapter may name <c>auto</c>, the mode
/// in which the harness judges each action itself, ahead of <c>acceptEdits</c>. What the doctrine
/// keeps is kept by the rules handed at spawn (no push, the tree guard), which hold in every mode.
/// This door sets a mode, which is what Claude Code offers on
/// `session/new`.</para>
///
/// <para>🔴 <b>Never a mode the adapter did not name, and never one the agent did not offer.</b> The
/// wire also offers <c>bypassPermissions</c>; a driver that reached for a neighbouring mode when
/// its own is missing is how a permission boundary widens without a decision. An adapter names
/// its postures in order, and the first the agent offers is set. An agent that offers no modes is
/// left exactly alone — its permissions are its own business, which is the stub's shape and any
/// future harness's right.</para>
///
/// <para>🔴 <b>A posture marked required is set, or the session does not open</b>: Ask Daoris's
/// full access, a <see cref="PostureAsked.Required"/> posture of the adapter's <see cref="AgentDoor.FullPosture"/>, and a driven
/// session's asking or unrestricted permission prompts (<see cref="DrivenAccess"/>). Left alone, an agent that does
/// not offer it would run in its own mode under that label; it fails plainly instead, naming what the agent offers and the
/// person's two ways on (<see cref="AcpSession.Unoffered"/>'s words where a driven start gave them). Every other posture keeps the leniency
/// above.</para>
/// </remarks>
internal sealed class AcpPosture(
    string? posture,
    Func<Func<string, string, string>?> unoffered,
    Func<string, object, CancellationToken, Task<JsonElement>> request,
    Func<string?> sessionId)
{
    /// <param name="created">The answer to the open: the modes it offers.</param>
    /// <param name="again">Whether a value is set on the session since it answered, which may have moved its mode: set it whatever it said.</param>
    public async Task SetAsync(JsonElement created, CancellationToken ct, bool again = false)
    {
        // An adapter that names no posture asks for none — dsh, whose wire carries no modes, and the
        // stub, whose permissions are its own business.
        if (posture is not { Length: > 0 }) return;

        var (named, required, _) = PostureAsked.Parse(posture);
        DriverException Refused(IReadOnlyCollection<string> offers)
        {
            var offered = offers.Count > 0
                ? $"it offers {string.Join(", ", offers.Order(StringComparer.Ordinal).Select(mode => $"`{mode}`"))}"
                : "it offers no modes";
            // A driven start's required posture is its repository's permission prompts', said in its setup's words.
            return new(unoffered()?.Invoke(named, offered)
                ?? $"the agent does not offer `{named}`, the mode that asks nothing which full access needs ({offered}), so it does not start in "
                + "a narrower one under that name. Choose restricted access for Ask Daoris (`daoris ask-daoris mode restricted`), or another "
                + "agent for it.");
        }

        // An answer that is no object names no modes (HELPMODE1a4): a resume's may be null, and a required posture fails on it too.
        if (created.ValueKind != JsonValueKind.Object
            || !created.TryGetProperty("modes", out var modes)
            || !modes.TryGetProperty("availableModes", out var available)
            || available.ValueKind != JsonValueKind.Array)
        {
            if (required) throw Refused([]);
            return;
        }

        var offered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mode in available.EnumerateArray())
        {
            if (mode.TryGetProperty("id", out var id) && id.GetString() is { } name) offered.Add(name);
        }

        // The adapter names its postures in order of preference, `|`-separated: the first the
        // agent offers is the one. None offered is left alone, exactly as a single posture is.
        var chosen = named.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(offered.Contains);
        if (chosen is null)
        {
            if (required) throw Refused(offered);
            return;
        }

        // Already there is nothing to ask for — and an agent that started in the posture is a fact
        // worth not overwriting with an identical call.
        if (!again && modes.TryGetProperty("currentModeId", out var current) && current.GetString() == chosen)
        {
            return;
        }

        await request("session/set_mode", new { sessionId = sessionId(), modeId = chosen }, ct).ConfigureAwait(false);
    }
}
