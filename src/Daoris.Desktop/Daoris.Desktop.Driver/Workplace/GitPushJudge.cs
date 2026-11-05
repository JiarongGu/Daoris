namespace Daoris.Driver;

/// <summary>A push batch's answer (D241 point 3): whether every update may pass, the code, the one line a refusal says, and whether the grant was used.</summary>
/// <param name="Allowed">Whether the whole batch may pass; one refused update refuses it all, before any ref moves.</param>
/// <param name="Code"><c>access</c>, <c>granted</c>, or a refusal's code: <c>grant-bound</c>, <c>dangerous-denied</c>.</param>
/// <param name="Line">The one line a refusal writes where git prints it; null where the batch passes.</param>
/// <param name="UsedGrant">Whether the grant carries an update that passed, so the door takes it once.</param>
public sealed record GitBatchVerdict(bool Allowed, string Code, string? Line, bool UsedGrant = false);

/// <summary>
/// The judge of a push's updates (D241 point 3): each update by its kind, and the batch as one. No person is waited on. The Write a grant
/// alone allowed reaches only the updates the grant names (<see cref="GitGrantRef.Names"/>); a Dangerous update the grant carries passes
/// (<see cref="GitGrantRef.Carries"/>), and every other, a tag the grant names included, passes only where the place's Dangerous value is
/// <c>allow</c>: <c>deny</c>, the default and a stale <c>ask</c> refuse it.
/// </summary>
/// <remarks>Pure: the doors compile it in (<c>Daoris.Desktop.GitDoor</c>) and hand it what they read.</remarks>
public static class GitPushJudge
{
    /// <summary>The line a refusal says where Write passed on a grant alone and the grant does not carry <paramref name="reference"/>.</summary>
    public static string NotCarriedLine(string reference) =>
        $"daoris: the grant for this push does not carry {reference}: it names the refs of its own act and no others.";

    /// <summary>The line a refusal says where the grant was taken by another push first.</summary>
    public const string TakenLine = "daoris: the grant for this push was already used by another push.";

    /// <summary>
    /// Judge the batch. <paramref name="writeByGrant"/> is whether the act's Write passed on the grant alone; <paramref name="grant"/> is
    /// what the grant carries, null where there is none; <paramref name="dangerous"/> is the place's Dangerous value.
    /// </summary>
    public static GitBatchVerdict Judge(
        IReadOnlyList<GitPushUpdate> updates, IReadOnlyList<GitPushKind> kinds, IReadOnlyList<GitGrantRef>? grant, bool writeByGrant, string dangerous)
    {
        var used = false;
        for (var index = 0; index < updates.Count; index++)
        {
            var update = updates[index];
            var kind = kinds[index];
            var named = grant?.Any(each => each.Names(update)) == true;
            var carried = grant?.Any(each => each.Carries(update, kind)) == true;
            if (writeByGrant && !named) return new(false, "grant-bound", NotCarriedLine(update.Ref));
            if (kind != GitPushKind.Plain && !carried && dangerous != GitAccessKeys.Allow)
                return new(false, "dangerous-denied", GitPushUpdates.DeniedLine(kind));
            used |= (named && writeByGrant) || (carried && kind != GitPushKind.Plain);
        }

        return new(true, writeByGrant || used ? "granted" : "access", null, used);
    }
}
