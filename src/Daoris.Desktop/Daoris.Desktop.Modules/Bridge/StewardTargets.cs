using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The conditions of the steward's quest and tree acts that read a dependency, each judged again under the gates of the
/// act's target: a quest that exists, a session's tree this home opened, and a landing that pushes nothing.
/// </summary>
internal static class StewardTargets
{
    /// <summary>A quest the service holds.</summary>
    public static async Task<string?> QuestExistsAsync(RoleActRun run, string quest, CancellationToken ct) =>
        run.World is { } world && await world.QuestStatusAsync(quest, ct).ConfigureAwait(false) is null ? StewardWhy.NoQuest : null;

    /// <summary>A quest's own session whose working tree is one this home opened: never the person's checkout.</summary>
    public static async Task<string?> OwnTreeAsync(RoleActRun run, string session, CancellationToken ct)
    {
        if (run.World is not { } world) return null;
        if (await world.SessionAsync(session, ct).ConfigureAwait(false) is not { } record || !StewardActRows.QuestSession(record)) return StewardWhy.NotQuestSession;
        return record.Tree is null ? StewardWhy.NoTree : world.OwnTree(record.Tree) ? null : StewardWhy.NotOwn;
    }

    /// <summary>
    /// A tree's landing, which never pushes to a remote: its plan, read through the landing's own open route, names no plugin that
    /// pushes the branch once it is made. A plan that cannot be read is no plan that pushes nothing, so the act is refused.
    /// </summary>
    public static async Task<string?> LandsWithoutPushingAsync(RoleActRun run, string session, CancellationToken ct)
    {
        if (await OwnTreeAsync(run, session, ct).ConfigureAwait(false) is { } refused) return refused;
        if (run.Route is not { } dispatch) return StewardWhy.NoRoute;
        var plan = await dispatch("LANDING", JsonSerializer.SerializeToElement(new { id = session }), ct).ConfigureAwait(false);
        if (!plan.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True) return StewardWhy.Refused;
        return plan.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
               && data.TryGetProperty("plugin", out var plugin) && plugin.ValueKind != JsonValueKind.Null
            ? StewardWhy.LandingPushes
            : null;
    }
}
