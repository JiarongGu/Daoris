namespace Daoris.Driver;

/// <summary>What a session's rule language is handed: everything the start composed, in no provider's words.</summary>
/// <param name="Rules">The composed allow, ask and deny lists.</param>
/// <param name="HardDeny">What the classifier is told as hard denials, or empty for none; required, so no start forgets to say.</param>
/// <param name="Guard">The tree guard for this session, or null where it is switched off.</param>
/// <param name="Lineage">
/// The file the session's children are appended to, or null for none: a provider whose agent says its children by a hook writes one
/// there; one whose calls carry their thread needs none.
/// </param>
public sealed record HandedRules(RuleLists Rules, IReadOnlyList<string> HardDeny, TreeGuardHook? Guard, string? Lineage)
{
    /// <summary>
    /// That a call its harness would ask about is asked of the person through Daoris, where its door has no wire to hold one on.
    /// </summary>
    public bool Asked { get; init; }

    /// <summary>The person's own tool home and the families they leave on, whose settings a language merges beneath these; null for none.</summary>
    public OwnTools? Own { get; init; }

    /// <summary>The folder the session works in, whose own settings outrank the person's; null where it has none.</summary>
    public string? Folder { get; init; }

    /// <summary>A session shaped for one job, which takes nothing of the person's settings that would widen it.</summary>
    public bool Shaped { get; init; }
}
