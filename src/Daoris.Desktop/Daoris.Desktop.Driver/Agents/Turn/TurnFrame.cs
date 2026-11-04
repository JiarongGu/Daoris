using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One frame the agent sent, as the session's dispatch hands it to the stages of its phase.</summary>
/// <param name="Method">The protocol method it names; null where the wire named none in a string.</param>
/// <param name="Frame">The whole frame.</param>
/// <param name="Id">A request's id as the wire said it, which its answer carries back; default for an update.</param>
/// <param name="Update">An update's <c>params.update</c>; default for a request.</param>
internal sealed record TurnFrame(string? Method, JsonElement Frame, JsonElement Id, JsonElement Update)
{
    /// <summary>The session an update names in its params, or null where it names none.</summary>
    public string? From =>
        Frame.TryGetProperty("params", out var parameters) ? AcpRender.StringField(parameters, "sessionId") : null;

    /// <summary>Whether the update is the session's own: it names no session, or names <paramref name="sessionId"/>.</summary>
    public bool IsOwn(string? sessionId) => From is not { } from || from == sessionId;
}
