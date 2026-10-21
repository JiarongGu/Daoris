using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Daoris.Knowledge.Mcp;

/// <summary>Ask Daoris's <c>setting_propose</c> (HELP1c): the <c>setting</c> kind's tool, written by <see cref="HelpProposalBox.ProposeSetting"/>.</summary>
[McpServerToolType]
public sealed class HelpSettingTool(IntakeScope? intake = null, HelpProposalBox? help = null)
{
    /// <summary>
    /// What the landing's gate does with work that waits for the person's review, the sentence every other door says after a
    /// review rule's own (REVIEWENV1c3): the shared table's <c>gate</c> rows,
    /// <c>src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/review-rules.json</c>, which the driver's
    /// <c>ReviewRules.Waiting</c> and the CLI's <c>REVIEW_WAITING</c> are held to. Copied, since the service references
    /// neither, and held to the table by <c>HelpSettingProposalTests</c>, which reads its <c>gate</c> rows (REVIEWENV1c4): the
    /// doors' words change together, this one with them.
    /// </summary>
    private const string ReviewWaiting = "Where work here waits for your review, it lands only once you say it is reviewed, "
        + "`daoris-driver quest review <quest> reviewed`, or skip the review, `daoris-driver quest review <quest> skip`. "
        + "No set-up step is composed for you yet.";

    /// <summary>
    /// What the gate does with work that waits for another agent's reading, the sentences every other door says after a
    /// second-opinion rule's own (XAGENT1f4): at landing, before a chain's next step under <c>steps</c>, and with <c>verify</c>.
    /// The shared table's <c>gate</c> rows, <c>src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/opinion-rules.json</c>,
    /// which the driver's <c>OpinionRules.Gate</c> and the CLI's <c>opinionGate</c> are held to. Copied, since the service
    /// references neither, and held to the table by <c>HelpSettingProposalTests</c>, which reads its <c>gate</c> rows.
    /// </summary>
    private const string OpinionWaiting = "Where work here waits for another agent's reading, it lands only once that reading is "
        + "settled, or once you go on without it, `daoris-driver opinion anyway <session>`, or say you looked yourself, "
        + "`daoris-driver opinion myself <session>`.";

    /// <summary>Said after a rule reading before each next step (XAGENT1f4): the twins' <c>StepWaiting</c>, by the same table.</summary>
    private const string OpinionStepWaiting = "Where a chain's next step here waits for another agent's reading of the step before "
        + "it, it starts only once that reading is settled.";

    /// <summary>Said after a rule letting a reviewer run what is declared safe (XAGENT1f4): the twins' <c>SafeNotHanded</c>.</summary>
    private const string OpinionSafeNotHanded = "What this repository declares safe is not handed to a reviewer yet.";

    [McpServerTool(Name = "setting_propose")]
    [Description(
        "Ask Daoris only: propose a change to how this machine drives its repositories, for the person to "
        + "apply. It becomes a card saying what it changes and the terminal command that does the same, with "
        + "Apply and Not now; nothing changes until the person presses Apply, and their answer comes back as "
        + "their next message. Each door is a `daoris driver` verb: drive, undrive, hold, resume, trees, line, "
        + "landing, across, standing, language, review, opinion, helpers, commits, intake, helper, strikes, retry, timeout, notify, cap, "
        + "adapter, cooloff, keep. `keep` sets how long this machine keeps one kind of thing Daoris writes, its words a kind and "
        + "its rule: `set-aside <days>|whole|slim|default` (trees the look moved aside), `downloads <days>|never|default` "
        + "(versions nothing uses), `logs <days>|default` (the machine log), `history <days>|never|default` (finished work's "
        + "records cleared that many days after their last move, every refusal holding; `never`, the default, leaves them to the "
        + "person's clear), or `floor <n>GB|<n>%|off|default` (the free space below which Daoris frees early what it can make again "
        + "and says the rest), proposed only where the person asked for it. "
        + "An `across`"
        + "write-to is the person's standing say-so for one repository's sessions to write into another; `standing` keeps what "
        + "the person says holds for every session in one repository (which writes are allowed, where to test), in their words; "
        + "`language` sets the language a repository's or a workspace's sessions write to the person in, the work's and never "
        + "the window's, which stays the viewer's own in Settings → Appearance; "
        + "`review` declares where a repository's or a workspace's work is shown to the person before it is offered to land: an "
        + "environment, `local` (shown in Daoris's browser at the address where the app runs) or `deployed`, named by the "
        + "repository's own procedure document, never production; `--required` is the person's say-so that work waits for "
        + "their look, so propose it only when they ask; the door then tells the person what the landing does with work "
        + "that waits: \"" + ReviewWaiting + "\"; "
        + "`opinion` declares which other agent reads a repository's or a workspace's work before it lands, in a copy of its own "
        + "that nothing is taken back from: `--reviewers` names adapters in the order they are tried, only the agents the person "
        + "names and never one you chose, ideally another maker's than the one doing the work; `--required` (work waits for "
        + "them when no reviewer can read it) and `--verify` (a reviewer may run what the repository declares safe) are their "
        + "say-so, so propose them only when they ask; the door then tells the person what the gate does with work that waits: "
        + "at landing, \"" + OpinionWaiting + "\"; with `--on steps`, \"" + OpinionStepWaiting + "\"; with `--verify`, \""
        + OpinionSafeNotHanded + "\"; those two presses are the person's own, never yours to propose; "
        + "`retry` takes a quest parked by its failed sessions, or held by the person's stop, as the room lists them; "
        + "`helpers` sets what a helper an agent asks for does in a repository or a workspace: `--mode ask` (the default: each "
        + "waits for the person's yes), `on` (it starts at once, within the limits) or `off` (none is offered), and `--agents` the "
        + "adapters it may ask, in the order they are tried, only the agents the person names; "
        + "`commits` sets how a repository's or a workspace's work is committed: `--mode as-you-go` (the default: each piece that "
        + "passes its checks), `ask` (each commit waits for the person's yes) or `at-end` (one commit when the work is done), and "
        + "`--convention` the person's words on how a commit message is written, one paragraph of at most 400 characters; "
        + "you may propose either rule, and a landing's branch pattern, from the repository's own instructions (its branch names, "
        + "its commit rule), quoting them as written, for the person to apply: that text is never the process until the person "
        + "applies it; "
        + "a `landing` on a branch may add `--auto-accept`: a quest's done then lands its work with no press, and the rule's "
        + "plugin pushes it and opens a pull request without asking each time, which is the person's standing say-so for that "
        + "push, so propose it only when they ask for it, and never on a merge; a branch's pattern may say `{ticket}`, a ticket "
        + "found in the title or the request by `--key <shape>` (a regular expression; `--no-key` reads the default shape), and "
        + "`--squash` makes the pull request's branch one commit of the session's work; a landing proposed keeps the key and the "
        + "squash already set unless it says `--no-key` or `--no-squash`. Never "
        + "for a push, a merge, a discard, a sign-in or an account's key: those "
        + "stay the person's own presses.")]
    public string ProposeSetting(
        [Description("The door, as `daoris driver` spells it: drive, undrive, hold, resume, trees, line, landing, across, standing, language, review, opinion, helpers, commits, intake, helper, strikes, retry, timeout, notify, cap, adapter, cooloff or keep.")]
        string door,
        [Description("Why: what the person asked, and what the change would do. The person decides on this.")]
        string why,
        [Description("The repository, for drive, undrive, hold, resume, trees, across, standing, and a line, a landing, a language, a review, a second opinion, a helpers rule or a commit rule set for one repository; for retry, the quest's id, as the room lists the parked and the held ones.")]
        string? target = null,
        [Description("The workspace, for a line, a landing, a language, a review, a second opinion, a helpers rule or a commit rule, or `across … read`, set for every repository in it. Name this or target, not both.")]
        string? workspace = null,
        [Description("What it is set to, as the CLI takes it: `on`/`off` (trees, notify), a branch or `--clear` (line), `merge`, `branch <pattern>` with `--tidy`, `--plugin <id>` and `--auto-accept` if wanted, `--key <shape>` or `--no-key` where the pattern says `{ticket}`, and `--squash` or `--no-squash`, or `--clear` (landing), `read on|off|--clear` or `write-to <other> [--clear]` (across), the person's own words or `--clear` (standing), `en` or `zh`, or `--clear` (language), `<environment> --kind local|deployed --procedure <path> [--address <url>] [--run \"<command>\"] [--required|--not-required]`, `none`, `--drop <environment>`, `--required|--not-required` or `--clear` (review), `--reviewers <adapter,adapter> [--on landing,steps] [--required|--not-required] [--verify|--no-verify] [--minutes <n>] [--recheck|--no-recheck]`, `none` or `--clear` (opinion), `--mode ask|on|off` and/or `--agents <adapter,adapter>`, or `--clear` (helpers), `--mode as-you-go|ask|at-end` and/or `--convention \"<words>\"` or `--no-convention`, or `--clear` (commits), an agent or `off` (intake, helper), an agent (adapter), a number (strikes, timeout minutes, cap, cooloff minutes), a kind and its rule (keep: `set-aside 30`, `downloads never`, `logs default`, `history 365`).")]
        string? value = null)
    {
        var box = help ?? HelpProposalBox.FromEnvironment();
        return box.ProposeSetting(new SettingChange(door, target, workspace, value), why, intake?.Session, DateTimeOffset.UtcNow).Message;
    }
}
