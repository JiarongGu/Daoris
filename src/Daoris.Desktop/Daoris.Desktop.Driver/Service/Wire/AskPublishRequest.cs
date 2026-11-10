using Daoris.Contracts.Asks;
using Daoris.Contracts.Quests;

namespace Daoris.Driver;

/// <summary>
/// A terminal's publish of an ask: its receiver, the session publishing where the terminal is a session's (<c>DAORIS_SESSION_ID</c>), so
/// the quest names its publisher, and the person's requirements it carries, each their words quoted with its check. A bare receiver
/// carries none, and the service's answer says its done is then checked against nothing.
/// </summary>
public static class AskPublishRequest
{
    /// <summary>The request the service's publish door takes.</summary>
    public static PublishAsk Of(string id, string to, string? session, IReadOnlyList<QuestRequirementWire> requirements) =>
        new(id.Trim().TrimStart('#'), to.Trim(),
            Session: string.IsNullOrWhiteSpace(session) ? null : session.Trim(),
            Requirements: requirements.Count == 0 ? null : [.. requirements]);
}
