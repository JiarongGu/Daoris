using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The supervisor's rules as the room says them: each place's set, so a line that waits
/// for the person is explained from their own rules: a rule reads <i>when the count reaches n here, do this</i>, and a line holds until its own
/// door releases it. The room says a supervisor rule where it must tell it from a doctrine's or an access rule, a count by its code, and a hold as
/// <i>waits for you</i>. Written only where the home holds a rule; a rule is proposed with `role_propose` and is the person's Apply alone,
/// whatever access the agent has.
/// </summary>
internal sealed class HelpRoomRules : IHelpRoomSection
{
    public string Render(HelpMachine machine)
    {
        if (machine.Rules.Count == 0) return "";
        var text = new StringBuilder();
        text.Append("### The supervisor's rules\n\n");
        text.Append("A supervisor rule is the person's: *when this count reaches n at this place, do this*. The counts are `answers-alone` (answers alone in a\n");
        text.Append("row), `failures` (failed sessions on a quest), `tries` (the service did not answer), `moves` (moves to another choice a day) and the\n");
        text.Append("rest of the terminal's list. A narrower place's rules on a count replace a wider place's; a rule acts between turns only, never ends a\n");
        text.Append("running or quiet session and never answers a question. A line it holds *waits for you* until its own door releases it (*Go on with this\n");
        text.Append("answer*, *Try again* or *Go on now*). Answer *why is this waiting?* from these lines and the line's own note; propose a change with\n");
        text.Append("`role_propose` naming `supervisor`, with the place and its rules at the revision shown here; it is the person's Apply alone.\n\n");
        foreach (var line in machine.Rules) text.Append($"- {line}\n");
        text.Append('\n');
        return text.ToString();
    }
}
