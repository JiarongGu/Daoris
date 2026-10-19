namespace Daoris.Driver;

/// <summary>
/// What a driven start gets on its harness under its repository's permission prompts (AUTONOMY1b, D184 points 2, 3 and 5): the
/// posture its protocol door asks, or the permission mode its native door names; or why it cannot start under that value. Resolved at
/// each start and each resume from the setup standing then (<see cref="AccessRules.Resolve"/>), as Ask Daoris's mode is
/// (<see cref="HelpAccess"/>).
/// </summary>
/// <remarks>
/// <para><b>Auto</b> is today's (D81): the adapter's own posture, nothing named. <b>Asking</b> is the harness's asking mode
/// (<see cref="ISessionAdapter.AskingPosture"/>), and <b>unrestricted</b> its mode that asks nothing (<see cref="ISessionAdapter.FullPosture"/>),
/// each required on the protocol door, so an agent that does not offer it fails the open rather than running in its own mode under
/// that label (D183 point 3). A harness that names none is refused before any record opens, naming it (D183 point 1).</para>
///
/// <para><b>What every value keeps</b> (D184 point 5): the rules the spawn is handed are composed beside the mode, never from it, so
/// the person's denies, the tree guard and the classifier's hard denials go in every one. Only a harness that takes no rules (Codex)
/// is beyond them, in every mode, and its sentence says so.</para>
/// </remarks>
/// <param name="Permissions">The value it runs under, one of the permission prompts' (<see cref="AccessRules.Permissions"/>).</param>
/// <param name="Said">What its start note says of the mode it got, the harness's own word for it, in one line.</param>
public sealed record DrivenAccess(string Permissions, string Said)
{
    /// <summary>The protocol door's posture: null keeps the adapter's own (auto), the asking or unrestricted mode marked required.</summary>
    public string? Posture { get; init; }

    /// <summary>The native door's <c>--permission-mode</c>: null keeps the adapter's own (auto).</summary>
    public string? PermissionMode { get; init; }

    /// <summary>Why it does not start under this value on this harness, said plainly; null where it does.</summary>
    public string? Refusal { get; init; }

    /// <summary>The repository whose setup it is, which a refusal's way on names.</summary>
    public string Repository { get; init; } = "";

    /// <summary>
    /// The access a driven start of <paramref name="repository"/> gets on <paramref name="adapter"/> under <paramref name="access"/>'s
    /// permission prompts: auto as it always ran; asking or unrestricted in the harness's mode for it, or refused where it names none.
    /// </summary>
    public static DrivenAccess For(ResolvedAccess access, string repository, ISessionAdapter adapter)
    {
        var permissions = access.Value(AccessRules.Permissions);
        var keeps = adapter.TakesSettings
            ? "the person's own denies, and the tree guard where it is on, still hold"
            : "this agent takes no rules, so the person's own denies and the tree guard do not reach it";
        if (permissions is not (AccessRules.Asking or AccessRules.Unrestricted))
        {
            var own = adapter.Wire == SessionWire.Pipe ? "the permission mode driven sessions always ran in there"
                : adapter.AcpPosture is { Length: > 0 } posture ? $"it asks for the agent's {Spelled(posture)} mode, the first it offers"
                : "the agent's own mode, which Daoris does not set";
            return new(AccessRules.Auto, $"Permission prompts: auto on `{adapter.Name}`, {own} (D81); {keeps}.") { Repository = repository };
        }

        var unrestricted = permissions == AccessRules.Unrestricted;
        var kind = unrestricted ? "mode that asks nothing" : "asking mode";
        if ((unrestricted ? adapter.FullPosture : adapter.AskingPosture) is not { Length: > 0 } mode)
        {
            var refusal = $"`{repository}`'s permission prompts are `{permissions}`, set for "
                + $"{AccessRules.Whose(access.Source(AccessRules.Permissions))}, and Daoris knows no {kind} of `{adapter.Name}`'s, so it does "
                + $"not start there in another mode under that name. {WayOn(repository)}";
            return new(permissions, refusal) { Refusal = refusal, Repository = repository };
        }

        var does = unrestricted
            ? "which asks nothing"
            : !adapter.TakesSettings
                ? "which asks before acting; Daoris refuses every ask (D52), and this agent takes no rules, so it does only what that mode "
                  + "allows unasked"
                : adapter.Wire == SessionWire.Pipe
                    ? "where a tool its rules do not allow is refused, since nothing answers an ask while it runs, so it uses only what its "
                      + "rules allow"
                    : "which asks before any tool its rules do not allow; Daoris refuses every ask (D52), so it uses only what its rules allow";
        var said = adapter.Wire == SessionWire.Pipe
            ? $"Permission prompts: {permissions} on `{adapter.Name}`, the `{mode}` permission mode, {does}; {keeps}."
            : $"Permission prompts: {permissions} on `{adapter.Name}`, the agent's `{mode}` mode, {does}; {keeps}.";
        return adapter.Wire == SessionWire.Pipe
            ? new(permissions, said) { PermissionMode = mode, Repository = repository }
            : new(permissions, said) { Posture = $"{HelpModes.Required}{mode}", Repository = repository };
    }

    /// <summary>
    /// What a protocol door's open says where the agent does not offer this start's required posture (<c>AcpSession.Unoffered</c>):
    /// the mode asked, what the agent offers, the repository's setup, and the person's two ways on. Never Ask Daoris's words.
    /// </summary>
    /// <param name="named">The mode asked, without its mark.</param>
    /// <param name="offers">What the agent offers, said: <c>it offers `a`, `b`</c>, or <c>it offers no modes</c>.</param>
    public string Unoffered(string named, string offers) =>
        $"the agent does not offer `{named}`, the {(Permissions == AccessRules.Unrestricted ? "mode that asks nothing" : "asking mode")} "
        + $"`{Repository}`'s permission prompts (`{Permissions}`) need ({offers}), so it does not start in another mode under that name. "
        + WayOn(Repository);

    /// <summary>The person's two ways on from a value the harness cannot run: auto for the repository, or another agent.</summary>
    private static string WayOn(string repository) =>
        $"Set its permission prompts to `auto` (`daoris-driver access {repository} permissions=auto`), or choose another agent for it.";

    /// <summary>A posture list as a sentence says it: <c>auto|acceptEdits</c> is <c>`auto` or `acceptEdits`</c>.</summary>
    private static string Spelled(string posture) => string.Join(
        " or ",
        posture.TrimStart(HelpModes.Required).Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(mode => $"`{mode}`"));

    /// <summary>
    /// The permission prompts a record says it started under (AUTONOMY1a's <c>access</c>), read as a stored shape: a record from before
    /// the setting ran today's, auto, and a value this build does not know reads as auto too, as the driver's file reads one.
    /// </summary>
    public static string Ran(IReadOnlyDictionary<string, string>? kept) =>
        kept is not null && kept.TryGetValue(AccessRules.Permissions, out var value)
                         && AccessRules.ValueNamed(AccessRules.Pause(AccessRules.Permissions), value) is { } named
            ? named
            : AccessRules.Auto;

    /// <summary>
    /// What a run going on from <paramref name="prior"/> says first where its permission prompts changed since that record started (D183
    /// point 3, as HELPMODE1a2 says Ask Daoris's); null where they are the same.
    /// </summary>
    public static string? ChangedSince(PriorSession prior, string now) => Ran(prior.Access) == now
        ? null
        : $"Permission prompts changed since `{prior.Session}` started: it started under `{Ran(prior.Access)}`, and this run goes on under `{now}`.";

    /// <summary>
    /// What a driven start's record notes of its setup once it opens: the change from the session it goes on from, where there is one,
    /// then its setup's line (<see cref="AccessRules.StartNote"/>) and the mode it got. A start nobody changed, going on from none that
    /// ran otherwise, notes nothing, so its transcript reads as it always did.
    /// </summary>
    /// <param name="prior">The session this start goes on from, a carry-on's or a resume's; null for a first start.</param>
    public static string? StartNote(ResolvedAccess access, DrivenAccess got, PriorSession? prior)
    {
        var changed = prior is null ? null : ChangedSince(prior, got.Permissions);
        var setup = AccessRules.StartNote(access);
        return changed is null && setup is null ? null : string.Join(" ", new[] { changed, setup, got.Said }.OfType<string>());
    }

    /// <summary>
    /// What a resumed record notes as it goes on (AUTONOMY1b): the change and the mode it got, where its permission prompts changed
    /// since it started; null otherwise. Its record keeps the setup it opened under.
    /// </summary>
    public static string? GoingOnNote(PriorSession prior, DrivenAccess got) =>
        ChangedSince(prior, got.Permissions) is { } changed ? $"{changed} {got.Said}" : null;
}
