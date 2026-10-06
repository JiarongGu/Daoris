using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>setting_propose</c> (HELP1c): the <c>setting</c> kind's tool, written by <see cref="HelpProposalBox.ProposeSetting"/>.</summary>
public sealed partial class KnowledgeTools
{
    [McpServerTool(Name = "setting_propose")]
    [Description(
        "Ask Daoris only: propose a change to how this machine drives its repositories, for the person to "
        + "apply. It becomes a card saying what it changes and the terminal command that does the same, with "
        + "Apply and Not now; nothing changes until the person presses Apply, and their answer comes back as "
        + "their next message. Each door is a `daoris driver` verb: drive, undrive, hold, resume, trees, line, "
        + "landing, across, standing, language, intake, helper, strikes, retry, timeout, notify, cap, adapter. An `across` "
        + "write-to is the person's standing say-so for one repository's sessions to write into another; `standing` keeps what "
        + "the person says holds for every session in one repository (which writes are allowed, where to test), in their words; "
        + "`language` sets the language a repository's or a workspace's sessions write to the person in, the work's and never "
        + "the window's, which stays the viewer's own in Settings → Appearance; "
        + "`retry` takes a quest parked by its failed sessions, or held by the person's stop, as the room lists them; "
        + "a `landing` on a branch may add `--auto-accept`: a quest's done then lands its work with no press, and the rule's "
        + "plugin pushes it and opens a pull request without asking each time, which is the person's standing say-so for that "
        + "push, so propose it only when they ask for it, and never on a merge. Never "
        + "for a push, a merge, a discard, a sign-in or a key: those "
        + "stay the person's own presses.")]
    public string ProposeSetting(
        [Description("The door, as `daoris driver` spells it: drive, undrive, hold, resume, trees, line, landing, across, standing, language, intake, helper, strikes, retry, timeout, notify, cap or adapter.")]
        string door,
        [Description("Why: what the person asked, and what the change would do. The person decides on this.")]
        string why,
        [Description("The repository, for drive, undrive, hold, resume, trees, across, standing, and a line, a landing or a language set for one repository; for retry, the quest's id, as the room lists the parked and the held ones.")]
        string? target = null,
        [Description("The workspace, for a line, a landing, a language or `across … read` set for every repository in it. Name this or target, not both.")]
        string? workspace = null,
        [Description("What it is set to, as the CLI takes it: `on`/`off` (trees, notify), a branch or `--clear` (line), `merge`, `branch <pattern>` with `--tidy`, `--plugin <id>` and `--auto-accept` if wanted, or `--clear` (landing), `read on|off|--clear` or `write-to <other> [--clear]` (across), the person's own words or `--clear` (standing), `en` or `zh`, or `--clear` (language), an agent or `off` (intake, helper), an agent (adapter), a number (strikes, timeout minutes, cap).")]
        string? value = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeSetting(new SettingChange(door, target, workspace, value), why, intake?.Session, DateTimeOffset.UtcNow).Message;
    }
}
