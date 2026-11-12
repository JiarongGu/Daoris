namespace Daoris.Driver;

/// <summary>
/// The room's one rule for a run's steps and the person's words asking for delivery, by mode: Ask Daoris puts the
/// person's requirements and through words on an ask when it makes it; where it asks on the person's card it never presses a run's step,
/// and where it acts as the person it presses an Accept or the pass of an unchecked done only where the person asked in words for the
/// work to be delivered and the run still waits on it. The section and the doors table both say it from here, so the room never says two.
/// </summary>
internal static class HelpDoorThrough
{
    private const string Carry =
        "Where the person asks for work to be delivered without their press, put their words on the ask when you make it:\n"
        + "`daoris ask --to <repo> … --require \"<their words>\" \"<the check>\"` and `--through \"<their words>\"`, or\n"
        + "`daoris ask through <id> \"<their words>\"` on one already made, each copied verbatim from their messages here; each run\n"
        + "of it then goes through its review to its pull request on them, and a departure from a requirement still waits for them.\n";

    /// <summary>The rule as the section says it where each press waits on the person's card.</summary>
    public const string Carded =
        "A run's step (its Accept, its changes, a pass or a stop, a task, an approval, a fix) is the person's alone: you never press it.\n"
        + Carry + "\n";

    /// <summary>The rule as the section says it where Ask Daoris acts as the person.</summary>
    public const string AsThePerson =
        Carry
        + "Where a run still waits on an Accept or an unchecked done the person asked in words to have delivered, you may press it as\n"
        + "theirs (`daoris workflow accept`, or `daoris workflow skip` of the unchecked hold), said once; no other step of a run is yours.\n\n";

    /// <summary>The doors table's word on a run's Accept.</summary>
    public const string Accept =
        "it is yours alone; where Ask Daoris acts as you, it presses it only where you asked in words for the work to be delivered and "
        + "the run still waits on it, said once and listed in Notifications";

    /// <summary>The doors table's word on a run's pass and stop.</summary>
    public const string Pass =
        "it is yours alone; where Ask Daoris acts as you, it passes only an unchecked done you asked in words to have delivered, said "
        + "once and listed in Notifications, and never stops a step";
}
