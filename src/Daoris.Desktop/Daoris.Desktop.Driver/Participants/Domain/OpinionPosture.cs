namespace Daoris.Driver;

/// <summary>
/// What held a reviewer read-only, as the opinion's record says it: the posture is the
/// adapter's, in its own words, and only where measured. A mode's name is never relied on: one called <c>read-only</c>
/// runs a workspace-write sandbox (as <c>codex-acp</c> does).
/// </summary>
public static class OpinionPosture
{
    /// <summary>The agent takes Daoris's rules (Claude Code, by either door), so the opinion's rules held it, beside its copy.</summary>
    public const string RulesAndCopy = "rules-and-copy";

    /// <summary>Its copy alone held it: an agent whose posture as a reviewer nobody has measured is held by the floor only.</summary>
    public const string CopyAlone = "copy-alone";

    /// <summary>A packet's reader whose agent's rules turned every tool off: it read the packet in its prompt, in an empty folder of its own.</summary>
    public const string PacketOnly = "packet-only";

    /// <summary>A packet's reader whose agent takes no rules that turn its tools off: the walk passes it, never starting it holding them.</summary>
    public const string NotEnforced = "not-enforced";

    /// <summary>What held a reviewer on <paramref name="adapter"/>: its rules where it takes them, else its copy alone.</summary>
    public static string Of(AgentDoor adapter) => adapter.Can.TakesSettings ? RulesAndCopy : CopyAlone;

    /// <summary>The posture as the record says it.</summary>
    public static string Said(string posture) => posture switch
    {
        RulesAndCopy => "read-only by its agent's rules and its copy",
        PacketOnly => "packet-only: its agent's rules turned its tools off",
        NotEnforced => "not enforced: its agent takes no rules that turn its tools off",
        _ => "read-only by its copy alone",
    };
}
