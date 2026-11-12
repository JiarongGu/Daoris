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

    /// <summary>
    /// The one rule for what Ask Daoris finishes itself and what it sends on, said by the section in each mode: a simple act on this
    /// machine is its own where its mode lets it press, and work that changes a repository's content goes to that repository.
    /// </summary>
    private const string SendOn =
        "Work that changes a repository's content goes to that repository as a quest (`daoris ask --to <repo> \"…\"`) or a session.\n";

    /// <summary>The rule as the section says it where each press waits on the person's card.</summary>
    public const string Carded =
        "A run's step (its Accept, its changes, a pass or a stop, a task, an approval, a fix) is the person's to press on a card here: you\n"
        + "propose it and never press it.\n"
        + Carry
        + "Do it, or send it on: a simple act on this machine (a push they asked for, withdrawing a branch, passing over or stopping a\n"
        + "step, a plugin's command, an acceptance, a setting) you put on their card for their yes. " + SendOn + "\n";

    /// <summary>The rule as the section says it where Ask Daoris acts as the person.</summary>
    public const string AsThePerson =
        Carry
        + "Every press of a run's step (its Accept, its changes, a pass over or a stop, a task, an approval, a fix) is yours to press as\n"
        + "the person, said once and listed in Notifications (`daoris workflow accept|changes|skip|stop|done|approve|reject|fix`).\n"
        + "Do it, or send it on: a simple act on this machine (a push they asked for, withdrawing a branch, passing over or stopping a\n"
        + "step, a plugin's command, an acceptance, a setting) you finish yourself, never handing it back as still theirs. " + SendOn
        + "If something is refused, say which rule refused it; only a sign-in or a credential's value stays theirs.\n\n";
}
