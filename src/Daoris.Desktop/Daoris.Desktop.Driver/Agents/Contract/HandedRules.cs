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
    /// That a call its harness would ask about is asked of the person through Daoris, where its door has no wire to hold one on (D253):
    /// a working session's or a conversation's on the native door. A provider whose door holds on its wire hands nothing for it.
    /// </summary>
    public bool Asked { get; init; }
}
