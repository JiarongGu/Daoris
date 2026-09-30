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
        + "landing, across, intake, helper, strikes, timeout, notify, cap, adapter. An `across` write-to is the "
        + "person's standing say-so for one repository's sessions to write into another. Never for a push, a merge, "
        + "a discard, a sign-in or a key: those stay the person's own presses.")]
    public string ProposeSetting(
        [Description("The door, as `daoris driver` spells it: drive, undrive, hold, resume, trees, line, landing, across, intake, helper, strikes, timeout, notify, cap or adapter.")]
        string door,
        [Description("Why: what the person asked, and what the change would do. The person decides on this.")]
        string why,
        [Description("The repository, for drive, undrive, hold, resume, trees, across, and a line or a landing set for one repository.")]
        string? target = null,
        [Description("The workspace, for a line, a landing or `across … read` set for every repository in it. Name this or target, not both.")]
        string? workspace = null,
        [Description("What it is set to, as the CLI takes it: `on`/`off` (trees, notify), a branch or `--clear` (line), `merge`, `branch <pattern>` with `--tidy` and `--plugin <id>` if wanted, or `--clear` (landing), `read on|off|--clear` or `write-to <other> [--clear]` (across), an agent or `off` (intake, helper), an agent (adapter), a number (strikes, timeout minutes, cap).")]
        string? value = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeSetting(new SettingChange(door, target, workspace, value), why, intake?.Session, DateTimeOffset.UtcNow).Message;
    }
}
