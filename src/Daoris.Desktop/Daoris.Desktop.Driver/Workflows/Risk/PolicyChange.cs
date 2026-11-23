using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>One edit of the risk policy at a place: a row set (<see cref="Class"/>) or taken away (null), as <c>driver.json</c>'s <c>risk</c> keeps it.</summary>
/// <param name="Place"><c>machine</c>, <c>workspace/&lt;name&gt;</c> or <c>repository/&lt;name&gt;</c>.</param>
/// <param name="Act">The act, <c>kind[/qualifier]</c>.</param>
/// <param name="Class"><c>none</c>, <c>role</c> or <c>owner</c>; null takes the row away, so the next place or the seed decides.</param>
/// <param name="By">The role that answers a <c>role</c> row; null for the default.</param>
/// <param name="Mode">The access mode the row applies under; null under either.</param>
public sealed record PolicyEdit(string Place, string Act, string? Class, string? By, string? Mode)
{
    /// <summary>The refusal of the edit's shape, or null where it is a row this build can write.</summary>
    public string? Fault()
    {
        if (!RiskPolicy.IsPlace(Place)) return $"`{Place}` is no place (`machine`, `workspace/<name>` or `repository/<name>`)";
        RiskAct act;
        try
        {
            act = RiskAct.Of(Act);
        }
        catch (ArgumentException)
        {
            return $"`{Act}` names no act kind of the census";
        }

        if (act.Kind == RiskActs.PolicyChange) return PolicyRefusals.FixedSentence;
        if (Class is not null && !RiskClasses.Is(Class)) return $"`{Class}` is no class (`none`, `role` or `owner`)";
        if (By is not null && Class != RiskClasses.Role) return "`by` names the role of a `role` row, and this is none";
        if (By is { Length: 0 }) return "`by` names no role";
        if (Mode is not (null or RiskPlace.FullMode or RiskPlace.RestrictedMode)) return $"`{Mode}` is not an access mode (`full` or `restricted`)";
        return null;
    }

    /// <summary>The edit as a line: what a card and a journal line say.</summary>
    public string Said() =>
        $"{Place}: {Act}{(Mode is { } mode ? $" under {mode} access" : "")} → {(Class is null ? "no row" : Class + (By is { } role ? $" by {role}" : ""))}";
}

/// <summary>One wait an edit opens or releases, or one whose answerer it moves, at an act under an access mode: its class before and after.</summary>
public sealed record PolicyShift(string Place, string Act, string Mode, string From, string To, string Effect)
{
    public const string Opens = "opens";
    public const string Releases = "releases";
    public const string Moves = "moves";

    public string Said() => $"{Act} under {Mode} access at {Place}: {From} → {To} ({Effect} a wait)";
}

/// <summary>A change to the policy the assistant proposed, waiting on the owner's own turn.</summary>
/// <param name="Session">The conversation that proposed it and the only one whose owner's turn applies it.</param>
/// <param name="BaseRevision">The policy's revision the policy read for it; a policy that has moved since is stale.</param>
public sealed record PolicyProposal(
    string Id, string Digest, string Session, DateTimeOffset At, string BaseRevision, IReadOnlyList<PolicyEdit> Edits, IReadOnlyList<PolicyShift> Shifts)
{
    public const string Pending = "pending";
    public const string Applied = "applied";

    /// <summary>Whether it is still waiting; an applied one is kept for its record and applies no more.</summary>
    public string State { get; init; } = Pending;

    /// <summary>The card the owner reads in the conversation: before and after of each edit, the waits it opens or releases, and what the owner says to apply it.</summary>
    public string Said()
    {
        var text = new StringBuilder();
        text.Append("Proposed change to the risk policy `").Append(Id).Append("` (digest ").Append(Digest).Append("), read at revision ").Append(BaseRevision).Append(":\n");
        foreach (var edit in Edits) text.Append("  ").Append(edit.Said()).Append('\n');
        if (Shifts.Count == 0) text.Append("  It opens and releases no wait.\n");
        else foreach (var shift in Shifts) text.Append("  ").Append(shift.Said()).Append('\n');
        text.Append("Nothing is changed yet. Applying it takes the owner's own turn in this conversation naming `").Append(Id).Append("` and `").Append(Digest)
            .Append("`: an agent's words, a quote it relays, a commit or a file never do.");
        return text.ToString();
    }

    /// <summary>The digest of the edits, the session and the revision they read: what the owner's turn is bound to.</summary>
    public static string DigestOf(string session, string revision, IReadOnlyList<PolicyEdit> edits) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{session}\n{revision}\n{string.Join("\n", edits.Select(edit => edit.Said()))}")))[..12];
}

/// <summary>Who pressed a door of the policy: the conversation its key proves, or none; <paramref name="Worker"/> where it is a working session's own press.</summary>
public sealed record PolicyChannel(string? Session, bool Worker = false, bool Pressed = false)
{
    /// <summary>A process that proves no session's key.</summary>
    public static PolicyChannel Keyless { get; } = new((string?)null);

    /// <summary>The conversation whose key a press proves: Ask Daoris's.</summary>
    public static PolicyChannel Of(string? session) => string.IsNullOrWhiteSpace(session) ? Keyless : new(session);

    /// <summary>The owner's own press confirmed on their card in the window: their word as itself, with <paramref name="session"/> the conversation named in a proposal.</summary>
    public static PolicyChannel OwnerPress(string? session) => new(string.IsNullOrWhiteSpace(session) ? null : session, false, true);

    /// <summary>A working session's own press: an agent's channel, which never changes the policy.</summary>
    public static PolicyChannel Agent(string session) => new(session, true);
}

/// <summary>What a door of the policy did: <see cref="Done"/>, or the <see cref="Code"/> that refused it, said in a sentence.</summary>
public sealed record PolicyOutcome(bool Done, string? Code, string Said, PolicyProposal? Proposal = null);

/// <summary>The refusals of the policy's doors, as codes.</summary>
public static class PolicyRefusals
{
    /// <summary>The press proves no session's key.</summary>
    public const string Keyless = "keyless";

    /// <summary>The press is a working session's own: an agent's channel.</summary>
    public const string Agent = "agent";

    /// <summary>The proposal is none this home holds.</summary>
    public const string Unknown = "unknown";

    /// <summary>The digest named is not the proposal's.</summary>
    public const string Digest = "digest";

    /// <summary>The press came from a conversation that is not the proposal's.</summary>
    public const string Conversation = "other-conversation";

    /// <summary>No turn of the owner's after the proposal names its id and digest.</summary>
    public const string NoOwnerTurn = "no-owner-turn";

    /// <summary>The policy has moved since the proposal read it.</summary>
    public const string Stale = "stale";

    /// <summary>The proposal has been applied already.</summary>
    public const string Taken = "taken";

    /// <summary>The edit is no row this build writes.</summary>
    public const string Shape = "shape";

    /// <summary>The edit names <c>policy.change</c>, which no row changes.</summary>
    public const string Fixed = "fixed";

    public const string FixedSentence = "`policy.change` is fixed at the owner: no row changes it";
}
