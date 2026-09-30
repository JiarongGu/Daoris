using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A driver error a person can act on. Exit code 2 territory: tool error, not policy.</summary>
public sealed class DriverException(string message) : Exception(message);

/// <param name="QuestId">The quest the session exists to serve.</param>
/// <param name="Title">One line: what is wanted.</param>
/// <param name="Body">Why, and the evidence — the asker's words, verbatim.</param>
/// <param name="Asker">Who asked.</param>
/// <param name="Repository">Whose agent the session is.</param>
/// <param name="Root">The working tree it runs in.</param>
/// <param name="ServiceUrl">Where the service is, so the session can claim and close its own quest.</param>
public sealed record SessionTarget(
    string QuestId,
    string Title,
    string Body,
    string Asker,
    string Repository,
    string Root,
    string ServiceUrl)
{
    /// <summary>Addresses the quest carries (D65 §2), handed over as the asker gave them.</summary>
    public IReadOnlyList<string> Links { get; init; } = [];

    /// <summary>Files the quest carries, with where this machine keeps each — or null where it does not.</summary>
    public IReadOnlyList<QuestFileView> Attachments { get; init; } = [];

    /// <summary>What the service publishes when this closes done (D65 §4) — told, so the session knows.</summary>
    public IReadOnlyList<QuestStepView> Then { get; init; } = [];

    /// <summary>The quest this one follows, when it is a step of a chain.</summary>
    public string? Parent { get; init; }

    /// <summary>
    /// The ask this session answers — an INTAKE (D65 §1b), which serves no quest: its spawn carries the
    /// ask and its own session instead, and no quest variable at all. Null for a quest's session.
    /// </summary>
    public string? Ask { get; init; }

    /// <summary>
    /// The session's own record — handed to every session, whose connector names it when it publishes
    /// (an intake's, D65 §1b; a quest's, SESS1).
    /// </summary>
    public string? Session { get; init; }

    /// <summary>
    /// The instruction, when it is not a quest's — an intake's job (<see cref="IntakePrompt"/>). Null
    /// composes the claiming instruction, as every target always has.
    /// </summary>
    public string? Prompt { get; init; }

    /// <summary>
    /// The code map the session's tree keeps, repository-relative, or null when it keeps none. Where
    /// there is one, the session is asked to keep it current as it works (MAP3d, the agent producer).
    /// </summary>
    public string? CodeMap { get; init; }

    /// <summary>
    /// For a session that RESUMES a waiting quest (D79): the question an earlier session asked another
    /// repository, now closed, with its answer. Null composes the first start's claiming instruction.
    /// </summary>
    public QuestView? Answered { get; init; }

    /// <summary>
    /// For a session CARRYING ON a quest a cut-off left taken (D80): what the cut-off session's record
    /// said about its end. Null for every other session.
    /// </summary>
    public string? CutOff { get; init; }

    /// <summary>What the tree holds uncommitted when a session carries on — git's short lines (D80).</summary>
    public IReadOnlyList<string> InFlight { get; init; } = [];

    /// <summary>
    /// What the person answered, when the session before parked to ask them (STANDDOWN2) — the carry-on
    /// is handed it in their words. Null for a carry-on after a cut-off.
    /// </summary>
    public string? PersonSaid { get; init; }

    /// <summary>
    /// The branch this session's tree grew from when it is a chain's next step in its parent's
    /// repository (CHAIN2) — the parent's unmerged work is in the tree. Null for the canonical line.
    /// </summary>
    public string? GrewFrom { get; init; }

    /// <summary>
    /// How this session's work will land, when it runs in a tree of its own (WSR1, D87) — so a session
    /// whose work goes through review knows not to merge or push it. Null, or a merge, says nothing,
    /// and the instruction reads as it always has.
    /// </summary>
    public LandingPlan? LandsOn { get; init; }

    /// <summary>
    /// The other checkouts this session may read (D107), the declared write targets among them. Empty — as
    /// when reading across is off everywhere — and the instruction reads as it always has.
    /// </summary>
    public IReadOnlyList<AcrossCheckout> ReadsAcross { get; init; } = [];

    /// <summary>The checkouts the person declared this repository may also write into (D107). Empty is none.</summary>
    public IReadOnlyList<AcrossCheckout> WritesAcross { get; init; } = [];

    /// <summary>
    /// The target a quest's session is handed: the quest as the service answered it, run in
    /// <paramref name="workTree"/> — the repository's own tree where it opted in (D51), its root
    /// otherwise — naming the code map that tree keeps.
    /// </summary>
    public static SessionTarget ForQuest(QuestView quest, string workTree, string serviceUrl) =>
        new(quest.Id, quest.Title, quest.Body, quest.From, quest.To, workTree, serviceUrl)
        {
            Links = quest.Links,
            Attachments = quest.Attachments,
            Then = quest.Then,
            Parent = quest.Parent,
            CodeMap = CodeMapFile.Find(workTree),
        };

    /// <summary>
    /// The directory the session is handed as <c>DAORIS_QUEST_ATTACHMENTS</c> — the one the service
    /// keeps this quest's files in — or null when none of them is on this machine.
    /// </summary>
    public string? AttachmentsDirectory => Attachments
        .Select(file => file.Path)
        .OfType<string>()
        .Select(System.IO.Path.GetDirectoryName)
        .FirstOrDefault(directory => !string.IsNullOrEmpty(directory));
}

/// <summary>
/// The claiming instruction (D46 §3), composed once and delivered per harness. Project-agnostic on
/// purpose: it travels to repositories that know nothing of this one's decision numbering, so it
/// speaks in the canon's names, never in D-numbers.
/// </summary>
public static class TargetPrompt
{
    /// <summary>
    /// The instruction a session is handed — an intake's own where the target carries one, so every
    /// door that delivers a target (the pipe's argument, the protocol's prompt, <c>DAORIS_TARGET</c>)
    /// delivers the same words without knowing which kind of session it is.
    /// </summary>
    public static string Compose(SessionTarget target) =>
        target.Prompt
        ?? (target.Answered is { } answered ? Resuming(target, answered)
            : target.CutOff is { } cutOff ? CarryingOn(target, cutOff)
            : Claiming(target));

    private static string Claiming(SessionTarget target) =>
        $"""
        You are the agent for `{target.Repository}`, working inside its own repository and nowhere else.

        Your target is quest `#{target.QuestId}`, asked by `{target.Asker}`:

        # {target.Title}

        {target.Body}
        {Carried(target)}
        First take the quest (respond to `#{target.QuestId}` with `take`), then do the work inside this
        repository under its own doctrine and gates, then close it: `done` when it has landed, or
        `decline` with the reason — the reason is the part the asker can act on. If the quest is already
        taken or closed, stand down and finish without changing anything.
        {Mapped(target)}{Landing(target)}{Reading(target)}
        {Asking(target)}

        {Proposing}

        {Boundary(target)}
        """;

    /// <summary>
    /// A session resuming a quest its own earlier session took and waited on (D79): the quest again,
    /// what was asked and what came back, and — the part a claiming instruction would get wrong — that
    /// the quest is already this session's, so taking it again is refused and standing down is wrong.
    /// </summary>
    private static string Resuming(SessionTarget target, QuestView answered) =>
        $"""
        You are the agent for `{target.Repository}`, working inside its own repository and nowhere else.

        You are resuming quest `#{target.QuestId}`, asked by `{target.Asker}`. It is already taken, and it
        is yours: do not take it again, and do not stand down.

        # {target.Title}

        {target.Body}
        {Carried(target)}
        An earlier session on this quest needed something only `{answered.To}` could answer, asked it, and
        waited. What it did is in this tree — read its commits before you go on. The question was quest
        `#{answered.Id}`, "{answered.Title}", and {Answer(answered)}

        Carry on from there, inside this repository under its own doctrine and gates, then close
        `#{target.QuestId}`: `done` when it has landed, or `decline` with the reason — the reason is the part
        the asker can act on.
        {Mapped(target)}{Landing(target)}{Reading(target)}
        {Asking(target)}

        {Proposing}

        {Boundary(target)}
        """;

    /// <summary>
    /// A session carrying on a quest its own earlier session took and was cut off from (D80): the
    /// quest again, what cut the last one off, and what it left in the tree — so it finishes the work
    /// rather than starting it again, and does not take or stand down from a quest that is its own.
    /// </summary>
    private static string CarryingOn(SessionTarget target, string cutOff) =>
        $"""
        You are the agent for `{target.Repository}`, working inside its own repository and nowhere else.

        You are carrying on quest `#{target.QuestId}`, asked by `{target.Asker}`. It is already taken, and
        it is yours: do not take it again, and do not stand down.

        # {target.Title}

        {target.Body}
        {Carried(target)}
        {Before(target, cutOff)} What it did is in this tree — any commits it made are on this branch,
        and {InFlight(target)}

        Finish from there rather than starting again, inside this repository under its own doctrine and
        gates, then close `#{target.QuestId}`: `done` when it has landed, or `decline` with the reason —
        the reason is the part the asker can act on. Commit as you go, so a second cut-off loses less.
        {Mapped(target)}{Landing(target)}{Reading(target)}
        {Asking(target)}

        {Proposing}

        {Boundary(target)}
        """;

    /// <summary>
    /// Why the session before did not finish: cut off (D80), or stopped to ask the person, who has
    /// answered (STANDDOWN2) — in their words, which are the reason this session exists.
    /// </summary>
    private static string Before(SessionTarget target, string record) => target.PersonSaid is { } said
        ? $"An earlier session on this quest stopped to ask the person, and they answered:\n\n> "
          + said.ReplaceLineEndings("\n> ") + $"\n\nIts record reads: {record}"
        : $"An earlier session on this quest was cut off before it closed it: {record}";

    /// <summary>The tree's uncommitted changes as the driver read them, or that there were none.</summary>
    private static string InFlight(SessionTarget target) => target.InFlight.Count == 0
        ? "it left nothing uncommitted."
        : "these are the changes it had not committed yet:\n\n"
          + string.Join("\n", target.InFlight.Select(line => $"    {line}"));

    /// <summary>What came back, in the answerer's own words where it gave any.</summary>
    private static string Answer(QuestView answered)
    {
        var closed = string.Equals(answered.Status, "Declined", StringComparison.OrdinalIgnoreCase)
            ? $"`{answered.To}` declined it"
            : $"`{answered.To}` closed it done";
        return answered.Note is { Length: > 0 } note
            ? $"{closed}, saying:\n\n> {note.ReplaceLineEndings("\n> ")}"
            : $"{closed} without a note — read that quest, and what `{answered.To}` landed, for the answer.";
    }

    /// <summary>
    /// Ask and wait (D79): what another repository knows is asked of it, never guessed — and, where the
    /// session may not read across (D107), never read out of it. The session publishes, parks its quest on
    /// the question, and ends — the quest stays its own, and the driver resumes it here once it closes.
    /// </summary>
    private static string Asking(SessionTarget target) =>
        $"""
        {Needs(target)} Publish a quest to it saying
        what you need and why, commit what you have so far, then respond to `#{target.QuestId}` with `wait`
        on that new quest's id, and end your turn. The quest stays yours, and you are started again here,
        in this tree, with its answer.

        If it needs something only the person can give — a sign-in, a go-ahead for an act outside this
        repository, a choice between options that is theirs — say exactly what and why in your last
        message, commit what you have, and end your turn with the quest still taken, rather than declining.
        The person answers, and you are started again here, in this tree, with their words.
        """;

    private const string Proposing =
        """
        If a command the work genuinely needs is refused, and your connector offers `permission_propose`,
        propose the narrowest rule that would allow it, with the reason. A rule that lets agents do more
        waits for the person, so do not wait on it: finish what you can, or decline and say what was
        refused.
        """;

    /// <summary>
    /// What to ask another repository for (D79). Where the session may read across (D107), reading is no
    /// longer forbidden: what stays asked is a change, and what its code cannot tell.
    /// </summary>
    private static string Needs(SessionTarget target) => target.ReadsAcross.Count == 0
        ? """
          If the work needs something only another repository knows or can change — its contract, its data,
          a change in its code — do not read into it and do not guess: ask it.
          """
        : """
          If the work needs something another repository's code cannot tell you, or a change in it — what it
          promises, why it is the way it is — do not guess: ask it.
          """;

    /// <summary>
    /// The other checkouts this session may read and not write (D107), each by name and path, and how: their
    /// files where they lie, and two read-only git commands. Empty when there are none, so the target reads
    /// exactly as it did before.
    /// </summary>
    private static string Reading(SessionTarget target)
    {
        var readOnly = target.ReadsAcross
            .Where(read => !target.WritesAcross.Any(write => string.Equals(write.Repository, read.Repository, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return readOnly.Count == 0
            ? ""
            : $"""

              You may read these other repositories' checkouts on this machine, and change nothing in them:

              {Listed(readOnly)}

              Read their files where they lie, and see how each stands with `git -C <path> status` and
              `git -C <path> branch --list`, the path written as above.

              """;
    }

    /// <summary>
    /// Never write outside this repository — except, where the person declared it (D107), in the checkouts
    /// this repository may also change, which the boundary names with how to commit there.
    /// </summary>
    private static string Boundary(SessionTarget target) => target.WritesAcross.Count == 0
        ? """
          Never write outside this repository. Work another repository needs is a quest published to it,
          never an edit — that is the rule the whole arrangement rests on. Anything that cannot be taken
          back or that leaves the repository — a push, a publish, a release — is not yours to do; surface
          it and finish.
          """
        : $"""
          Never write outside this repository, except in these, which the person has declared this one may change:

          {Listed(target.WritesAcross)}

          There, change only what this quest needs, follow that repository's own doctrine, and commit what you
          change in it with `git -C <path> add` and `git -C <path> commit`.
          Any other work another repository needs is a quest published to it, never an edit — that is the rule
          the whole arrangement rests on. Anything that cannot be taken back or that leaves the repository — a
          push, a publish, a release — is not yours to do; surface it and finish.
          """;

    /// <summary>Each checkout on a line of its own, its path as git and its rule spell it.</summary>
    private static string Listed(IEnumerable<AcrossCheckout> checkouts) =>
        string.Join("\n", checkouts.Select(checkout => $"- `{checkout.Repository}` — `{AcrossRules.GitPath(checkout.Path)}`"));

    /// <summary>
    /// The agent producer (MAP3d): a repository that keeps a code map is asked to keep it moving with
    /// its code — only where one exists, because a map nobody started is not this session's to invent.
    /// It says "the repository's own tool where it has one" because nothing here can tell which
    /// producer wrote the file, and the file itself does not say. Empty when there is no map, so the
    /// target reads exactly as it did before.
    /// </summary>
    private static string Mapped(SessionTarget target) => target.CodeMap is not { } map
        ? ""
        : $"""

          This repository keeps a code map in `{map}`: its modules, a line on what each is for, and which
          depends on which. If your work adds, removes, moves or rewires a module, bring the map up to date
          in the same change: with the repository's own tool for it where it has one, otherwise by hand in
          the same shape — each module's `id` unique, its `path` relative to the repository, its `summary`
          one line, and each dependency naming two modules by `id`. A map that breaks that shape is shown as
          nothing at all, so leave it whole.

          """;

    /// <summary>
    /// How the work lands, said only where it goes through review: the branch the person will push and
    /// open a pull request from, and that the session makes neither move itself (D87).
    /// </summary>
    private static string Landing(SessionTarget target) => target.LandsOn is not { Form: LandingForm.Branch } plan
        ? ""
        : $"""

          This work goes through review. {Lands(plan)} Commit your work on this branch as you go —
          do not merge it, push it, or open a pull request yourself.

          """;

    /// <summary>Who takes the branch on from there: the person, or the plugin the rule names (D100) once the person accepts it.</summary>
    private static string Lands(LandingPlan plan) => plan.Plugin is null
        ? $"When it is done, the person puts this tree's branch on `{plan.Target}` and opens a pull request from it."
        : $"When it is done and the person accepts it, this tree's branch is put on `{plan.Target}`, pushed, and a pull request opened from it.";

    /// <summary>
    /// What the asker gave beside their words, said plainly — each link, each file where it lies, and a
    /// file this machine does not hold said to be elsewhere rather than listed as if a path would open
    /// it. Empty when the quest carries nothing, so an ordinary target reads exactly as it always has.
    /// </summary>
    private static string Carried(SessionTarget target)
    {
        if (target.Links.Count == 0 && target.Attachments.Count == 0 && target.Parent is null && target.Then.Count == 0)
        {
            return "";
        }

        var text = new StringBuilder();

        // Where it sits in a chain the asker composed: what it follows, and what its close publishes —
        // so a verifying session knows whose work it checks, and a developing one that a check comes.
        if (target.Parent is { } parent)
        {
            text.AppendLine().AppendLine(
                $"It follows quest `#{parent}`, which is done — read that quest for the work this one builds on."
                + (target.GrewFrom is { } branch
                    ? $" That work is in this tree: it grew from `{branch}`, the branch `#{parent}` landed on, "
                      + "which is not merged yet — so there is no merge to wait for or to make."
                    : ""));
        }

        if (target.Then.Count > 0)
        {
            var next = target.Then[0];
            text.AppendLine().AppendLine(
                $"When you close it `done`, the asker's next step is published to `{next.To}`: \"{next.Title}\""
                + (target.Then.Count > 1 ? $", with {target.Then.Count - 1} more after it." : ".")
                + " Close it `done` only when that step can start from what you landed.");
        }

        if (target.Links.Count > 0)
        {
            text.AppendLine().AppendLine("Links the asker gave with it — read them; they are part of the ask:");
            foreach (var link in target.Links) text.AppendLine($"- {link}");
        }

        if (target.Attachments.Count > 0)
        {
            text.AppendLine().AppendLine(target.AttachmentsDirectory is { } directory
                ? $"Files the asker attached, kept for you in `{directory}` (also `DAORIS_QUEST_ATTACHMENTS`) — read them, never edit them:"
                : "Files the asker attached:");
            foreach (var file in target.Attachments)
            {
                text.AppendLine(file.Path is { } path
                    ? $"- `{file.Name}` — {path}"
                    : $"- `{file.Name}` — not on this machine: it stayed where the quest was published. Ask for "
                      + "what it shows if the work needs it.");
            }
        }

        return text.ToString();
    }
}

/// <param name="Repository">Whose agent the conversation is.</param>
/// <param name="Root">The working tree it runs in.</param>
/// <param name="ServiceUrl">Where the service is, so the session can take or publish quests itself.</param>
/// <remarks>
/// A chat carries no quest (D49 §3) — that is the whole point: it is for work not yet shaped as an
/// ask. It may take one mid-conversation through its own connector, exactly as a driven session does.
/// </remarks>
public sealed record ChatTarget(string Repository, string Root, string ServiceUrl);

/// <summary>How the driver talks to the spawned process once it is running (D53).</summary>
public enum SessionWire
{
    /// <summary>
    /// The harness takes its whole target at once and prints text; the driver reads the text, keeps
    /// it as the transcript, and observes the exit. The original door, and still the default.
    /// </summary>
    Pipe,

    /// <summary>
    /// The harness speaks the **Agent Client Protocol** on stdio: JSON-RPC frames, a session created
    /// on the working tree, the target delivered as a prompt, and tool boundaries, turn boundaries
    /// and thoughts arriving as structured updates.
    /// </summary>
    /// <remarks>
    /// The gain over the pipe is a timeline that needs nothing parsed out of another program's
    /// stdout — the coupling D23/D24 exist to prevent, and which D52 rejected by name. What does NOT
    /// change is where a session record comes from: the wire's own stop reason flattens an aborted,
    /// blocked or errored turn to `end_turn`, so the record still moves on the exit code and the
    /// quest's state (D46 §4) and the wire only enriches the console and the transcript.
    /// </remarks>
    Acp,
}

/// <summary>
/// One harness adapter: how a session is spawned, and how the target reaches it. An adapter names a
/// harness, never a model (D24) — which model answers is that harness's own configuration in that
/// repository.
/// </summary>
public interface ISessionAdapter
{
    string Name { get; }

    /// <summary>
    /// Which door the driver holds this harness's session over (D53). Default <see cref="SessionWire.Pipe"/>,
    /// so an adapter that says nothing behaves exactly as every adapter did before the door existed —
    /// the same silence-preserves rule the toolchain and the session trees follow.
    /// </summary>
    SessionWire Wire => SessionWire.Pipe;

    /// <summary>
    /// The permission posture D37 sanctions, in <b>this harness's own vocabulary</b>, for adapters
    /// whose wire carries one (ACP3/D53).
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>The posture lives in three different places across three harnesses</b>, which is
    /// why this is a property of the adapter and not a constant in the session. Claude Code names it
    /// <c>acceptEdits</c> and Codex names it <c>agent</c> — both ACP modes, both observed rather than
    /// guessed (`docs/2026-09-22-acp3-probe-evidence.md`). dsh's <c>session/new</c> carries no modes
    /// at all, so its posture is an environment variable set at spawn instead.</para>
    ///
    /// <para><b>Null means this wire carries no posture</b>, and the session then asks for none —
    /// leaving the agent at its own default. That is the safe direction: every default observed is
    /// equal to or stricter than the one Daoris would set, so a forgotten posture stalls a session
    /// rather than widening it. It is never a licence to guess a neighbouring mode.</para>
    /// </remarks>
    string? AcpPosture => null;

    /// <summary>
    /// The process that would be the session: spawned in the root, target delivered, output
    /// redirected so the driver can keep the transcript. Preparation only — the driver owns the
    /// process lifetime, because observing it is the driver's half of the contract (D46 §5).
    /// </summary>
    ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command);

    /// <summary>
    /// Whether this harness can be wired for turn-taking (D49 §3) — stdin carrying the person's
    /// messages, stdout streaming the harness's.
    /// </summary>
    /// <remarks>
    /// Declared honestly and opted into, never assumed: an adapter that has not been wired for a
    /// conversation says so, and asking for one errors naming what the harness is — the same rule
    /// that governs an unknown adapter name (D23). Default false, so a new adapter is non-interactive
    /// until someone has actually done the work.
    /// </remarks>
    bool Interactive => false;

    /// <summary>
    /// Hand a pipe-door session the MCP servers Daoris offers it (D65 §1f), written to a file under
    /// Daoris's home — the harness's own way of taking servers at spawn, where it has one. The
    /// protocol door carries them on the wire instead (ACP4) and never comes here.
    /// </summary>
    /// <remarks>
    /// Default: nothing — a harness with no such flag is handed nothing, and says nothing, which
    /// is the silence-preserves rule every adapter default follows. An adapter opts in when the
    /// flag is real and verified against the binary, like every other claim about somebody else's
    /// tool.
    /// </remarks>
    void HandServers(ProcessStartInfo info, string configFile) { }

    /// <summary>
    /// Whether this harness takes the permission rules Daoris composes for a session (PERM1, D72) — a
    /// settings file under the home, in the harness's own rule language.
    /// </summary>
    /// <remarks>
    /// Default false, and then nothing is composed or handed: a Claude Code rule means nothing to
    /// Codex's approval policy or dsh's permission mode, and translating one is a claim about somebody
    /// else's program, made when asked for and measured when made. Silence preserves how the adapter
    /// spawned before the rules existed.
    /// </remarks>
    bool TakesSettings => false;

    /// <summary>Hand a pipe-door spawn its rules file, as the harness's own flag. Default: nothing.</summary>
    void HandSettings(ProcessStartInfo info, string settingsFile) { }

    /// <summary>
    /// What a protocol-door session carries on <c>session/new</c> to take its rules file, in the adapter's
    /// own <c>_meta</c> vocabulary — or null for none, and then no <c>_meta</c> is sent at all.
    /// </summary>
    object? AcpSessionMeta(string settingsFile) => null;

    /// <summary>
    /// What a spawn of this harness carries so a session's tools load with its first request, rather than
    /// behind a search step the agent must take first (HELP5). Empty where the harness has no such switch,
    /// or nobody has read one in its binary.
    /// </summary>
    /// <remarks>
    /// <para>Asked for by a conversation whose few tools are needed at once, which is Ask Daoris's: every
    /// answer it gives reads the family. A repository's sessions never ask, and keep the harness's own
    /// default, which is that harness's to change.</para>
    ///
    /// <para>Default empty, the silence-preserves rule every adapter default follows: an adapter opts in
    /// when the switch is read in the binary it runs, like every other claim about somebody else's tool.</para>
    /// </remarks>
    IReadOnlyDictionary<string, string> ToolsUpFront => ReadOnlyDictionary<string, string>.Empty;

    /// <summary>The process that would be a CHAT: the same spawn, with stdin open.</summary>
    ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
        throw new DriverException(
            $"the `{Name}` adapter cannot hold a conversation — it spawns an agent that takes its "
            + "target once and runs to completion. Chat with an adapter that declares `interactive`.");

    /// <summary>
    /// This harness AS A TOOL (D49 §4): where its binary is, how it reports its version, which
    /// environment variable names its configuration home, and how to run its own install, update and
    /// login flows.
    /// </summary>
    /// <remarks>
    /// Null — the default — is an adapter Daoris manages nothing about. It spawns exactly as it did
    /// before the toolchain existed, which is what lets a new adapter arrive without first answering
    /// questions about an installer it may not have. An adapter opts in when the answers are real:
    /// every field here is a claim about somebody else's tool, and a guessed one is worse than none.
    /// </remarks>
    HarnessToolchain? Toolchain => null;

    /// <summary>
    /// A reader of this harness's own structured stdout on the pipe door (D76 §1), new per session — or
    /// null when the door carries text, which is every adapter until its harness's wire is checked.
    /// </summary>
    /// <remarks>
    /// The protocol door reads its structure in <see cref="AcpSession"/>; this is the native door's twin,
    /// and it is the ADAPTER's, because a wire belongs to a harness (D23). Nothing here is parsed out of
    /// prose: a mapper reads a documented format, checked against the binary before it was written.
    /// </remarks>
    IStreamMapper? StructuredOutput() => null;

    /// <summary>
    /// A person's message as this harness reads it on a conversation's stdin — the text as it is, unless
    /// the harness takes structured input.
    /// </summary>
    string FrameMessage(string text) => text;

    /// <summary>
    /// The line that stops a conversation's turn on this harness's stdin and keeps its session (CONV4a) —
    /// null where the harness has none, and a turn there cannot be stopped short of ending the session.
    /// </summary>
    string? FrameInterrupt() => null;
}

/// <summary>What every adapter shares: the process shell, and the target riding in the environment.</summary>
internal static class Spawning
{
    /// <summary>
    /// A redirected process in the repository root. The environment, not arguments, carries the
    /// target's pieces: any script shape can read it without parsing, and nothing quest-sized ever
    /// hits a shell's quoting rules.
    /// </summary>
    /// <param name="redirectInput">
    /// Open stdin. A pipe-door session is given its whole target at once and has nobody to take turns
    /// with, so it gets none; a protocol-door session needs one, because the driver writes frames
    /// into it (D53).
    /// </param>
    public static ProcessStartInfo InRoot(
        SessionTarget target, string fileName, IEnumerable<string> arguments, bool redirectInput = false)
    {
        var info = Shell(target.Root, fileName, arguments, target.Repository, target.ServiceUrl);
        if (redirectInput) info.RedirectStandardInput = true;

        // An intake (D65 §1b) serves an ask, not a quest: the ask and its own session, and the quest
        // variables absent rather than blank — the chat rule, for the same reason.
        if (target.Ask is { } ask)
        {
            info.Environment[IntakeRoom.AskVariable] = ask;
            if (target.Session is { } session) info.Environment[IntakeRoom.SessionVariable] = session;
            info.Environment["DAORIS_TARGET"] = TargetPrompt.Compose(target);
            return info;
        }

        info.Environment["DAORIS_QUEST_ID"] = target.QuestId;
        info.Environment["DAORIS_QUEST_TITLE"] = target.Title;
        info.Environment["DAORIS_QUEST_BODY"] = target.Body;
        info.Environment["DAORIS_QUEST_ASKER"] = target.Asker;
        info.Environment["DAORIS_TARGET"] = TargetPrompt.Compose(target);
        // Its own record, as an intake's is named (SESS1): a connector the harness starts itself on the
        // pipe door inherits it, and says which session published a quest.
        if (target.Session is { } own) info.Environment[IntakeRoom.SessionVariable] = own;

        // The quest's files (D65 §2), when this machine holds any — absent rather than empty when it
        // does not, because a blank directory would read to a session as one that was emptied.
        if (target.AttachmentsDirectory is { } attachments)
        {
            info.Environment["DAORIS_QUEST_ATTACHMENTS"] = attachments;
        }

        return info;
    }

    /// <summary>
    /// The same shell for a CHAT (D49 §3), with stdin open so the person's messages reach the harness.
    /// </summary>
    /// <remarks>
    /// The quest variables are absent rather than empty: a conversation serves no quest, and a blank
    /// `DAORIS_QUEST_ID` would read to a session as an id it failed to parse. What it gets is what is
    /// true — which repository it is the agent for, and where to reach the service if the conversation
    /// turns into a quest worth taking or publishing.
    /// </remarks>
    public static ProcessStartInfo ChatInRoot(
        ChatTarget target, string fileName, IEnumerable<string> arguments)
    {
        var info = Shell(target.Root, fileName, arguments, target.Repository, target.ServiceUrl);
        info.RedirectStandardInput = true;
        return info;
    }

    private static ProcessStartInfo Shell(
        string root, string fileName, IEnumerable<string> arguments, string repository, string serviceUrl)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,

            // 🔴 The shell is a window, not a console. A console child of a GUI process is given a
            // console of its own unless this says otherwise — and nothing said otherwise, so every
            // git, every probe and every session flashed a terminal onto the desktop. Held by a source scan over every
            // spawn the desktop makes, because the next spawn site will forget it too.
            CreateNoWindow = true,

            // 🔴 A transcript is read as UTF-8 or it is not the transcript. .NET defaults a
            // redirected stream to the CONSOLE's codepage; on this machine that is CP936, and the
            // first real deployment recorded an em-dash (`e2 80 94`) as `e9 88 a5 3f` — decoded as
            // GBK and re-encoded. The result is still valid UTF-8, so nothing downstream can tell it
            // was ever wrong, and a platform that speaks 简体中文 loses every Chinese character in a
            // session record. Set here, once, because every adapter goes through this shell and the
            // protocol door parses JSON-RPC off the same stream.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);

        info.Environment["DAORIS_REPOSITORY"] = repository;
        info.Environment["DAORIS_SERVICE_URL"] = serviceUrl;

        return info;
    }
}

/// <summary>
/// The stub: runs whatever command the configuration names, in the repository root, with the target
/// in the environment. A test double with real mechanics — real spawn, real cwd, real delivery, real
/// exit — which is what lets the family rehearsal gate the whole loop with no model in it (D46 §8).
/// </summary>
public sealed class StubAdapter : ISessionAdapter
{
    public string Name => "stub";

    /// <summary>
    /// Interactive, so the whole conversation loop can be gated with no model in it (D46 §8's argument,
    /// one layer on): a scripted exchange is a real spawn, a real stdin, a real stream and a real exit.
    /// </summary>
    public bool Interactive => true;

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
        Spawning.InRoot(target, Command(command)[0], Command(command).Skip(1));

    public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
        Spawning.ChatInRoot(target, Command(command)[0], Command(command).Skip(1));

    /// <summary>
    /// A toolchain with real mechanics and no tool behind it — the same trick as the adapter itself
    /// (D46 §8). There is nothing to install or update (the "binary" is whatever the config names),
    /// but the <b>version, the environment seam and the login question are genuine</b>, which is what
    /// lets the family rehearsal gate profile selection and the logged-out refusal with no account,
    /// no credential and no model anywhere in it.
    /// </summary>
    public HarnessToolchain? Toolchain => new(
        Binary: [],
        VersionArguments: ["--version"],
        ProfileVariable: "DAORIS_STUB_CONFIG_DIR",
        // A login flow too, so signing in to another account (D66 §3) can be gated with no account:
        // whatever the configured command does with `--login` is the stub's sign-in.
        LoginArguments: ["--login"],
        LoginCheck: new LoginQuestion(
            ["--login-state"], LoggedIn: @"logged-in", LoggedOut: @"logged-out",
            Account: @"logged-in as (\S+)"),
        // Claude Code's own words for a refused credential (AGT3b), mirrored so the rehearsal can
        // gate a refused account with no account behind it.
        Refused: "API Error: 401");

    private static IReadOnlyList<string> Command(IReadOnlyList<string>? command) =>
        command is { Count: > 0 }
            ? command
            : throw new DriverException(
                "the stub adapter needs a command — name one in driver.json: "
                + """{ "commands": { "stub": ["node", "path/to/agent.mjs"] } }""");
}

/// <summary>
/// The stub's protocol twin (D53/ACP1): the same fake-binary trick, one door over.
/// </summary>
/// <remarks>
/// <para>It exists for the reason <see cref="StubAdapter"/> exists — so the whole door can be gated
/// with no model, no account and no credential anywhere in it (D46 §8). The command it runs is
/// whatever the configuration names, and the rehearsal names a small ACP agent that speaks the wire
/// and nothing else.</para>
///
/// <para><b>Stdin is open, and that is the difference that matters.</b> A pipe-door session is handed
/// its target once; a protocol-door session is written to, frame by frame, for as long as it lives.</para>
/// </remarks>
public sealed class AcpStubAdapter : ISessionAdapter
{
    public string Name => "acp-stub";

    public SessionWire Wire => SessionWire.Acp;

    /// <summary>
    /// Interactive, so a conversation on this door can be gated with no model in it (CONV3b) — the
    /// argument that made <see cref="StubAdapter"/> interactive, one door over.
    /// </summary>
    public bool Interactive => true;

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
        Spawning.InRoot(target, Resolve(command)[0], Resolve(command).Skip(1), redirectInput: true);

    public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
        Spawning.ChatInRoot(target, Resolve(command)[0], Resolve(command).Skip(1));

    private static IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) =>
        command is { Count: > 0 }
            ? command
            : throw new DriverException(
                "the acp-stub adapter needs a command — name one in driver.json: "
                + """{ "commands": { "acp-stub": ["node", "path/to/acp-agent.mjs"] } }""");
}

/// <summary>
/// The supported harness over the <b>protocol door</b> (ACP2/D53): Claude Code through the ACP
/// project's adapter, which runs the Agent SDK.
/// </summary>
/// <remarks>
/// <para><b>Two seams, both established keylessly</b> in the dsh evaluation's §1a.
/// <c>CLAUDE_CODE_EXECUTABLE</c> points the SDK at a <c>claude</c> of Daoris's choosing — which is
/// what makes probe 3's "native binary not found" the useful part of that probe, since the adapter
/// need not carry a second copy of a tool the toolchain already manages. <c>CLAUDE_CONFIG_DIR</c> is
/// the same account seam the pipe door uses, applied by the same one line in the driver.</para>
///
/// <para><b>Nothing about the target or the posture is a command-line argument here.</b> The target
/// arrives as <c>session/prompt</c> and the posture is a <b>mode</b> on the wire — an adapter that
/// also passed <c>-p</c> would send the work twice, and one that passed <c>--permission-mode</c>
/// would be stating the posture in the other door's vocabulary.</para>
///
/// <para><b>It is a separate harness to the toolchain</b>, with its own package and its own pin. The
/// ACP adapter and <c>claude</c> are different programs at different versions, and one pin for both
/// would install the wrong thing under a name somebody trusted.</para>
///
/// <para><b>No model is named</b> (D24): which model answers is the harness's own configuration, and
/// the ACP wire's model catalogue is deliberately not read.</para>
/// </remarks>
public sealed class ClaudeAcpAdapter : ISessionAdapter
{
    public string Name => "claude-code-acp";

    public SessionWire Wire => SessionWire.Acp;

    /// <summary>
    /// <c>auto</c> where the adapter offers it — Claude Code's own mode, in which the harness judges
    /// each action — and <c>acceptEdits</c>, the posture observed in the evaluation's §1a, from one that
    /// does not (D81: a session does as much as the harness would on its own). Never <c>bypassPermissions</c>, which judges nothing, however available the wire
    /// makes it. The pipe door still passes <c>--permission-mode acceptEdits</c>: <c>auto</c> is measured
    /// on this wire and not yet on that one.
    /// </summary>
    public string? AcpPosture => "auto|acceptEdits";

    /// <summary>
    /// The adapter takes turns on its own wire, which is all this seam asks of an interactive
    /// harness — a conversation over ACP is the same session entity by a different door (D49 §3).
    /// </summary>
    public bool Interactive => true;

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        return Spawning.InRoot(target, resolved[0], resolved.Skip(1), redirectInput: true);
    }

    public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command)
    {
        // A chat already redirects stdin — it is the person's channel over the pipe door, and the
        // driver's frames over this one.
        var resolved = Resolve(command);
        return Spawning.ChatInRoot(target, resolved[0], resolved.Skip(1));
    }

    /// <summary>The rules Daoris composed (PERM1, D72), taken on the wire rather than as an argument.</summary>
    public bool TakesSettings => true;

    /// <summary>
    /// 🔴 Read from the adapter's own source at 0.79.0 (`dist/acp-agent.js`): <c>session/new</c>'s
    /// <c>_meta.claudeCode.options</c> is spread into the Agent SDK's options (l. 5934–6045), and a
    /// <c>settings</c> string is a file read against the session's cwd (l. 6003–6010) — an absolute path
    /// resolving to itself. The SDK's programmatic tier: the one <c>--settings</c> fills on the pipe door.
    /// </summary>
    public object? AcpSessionMeta(string settingsFile) =>
        new { claudeCode = new { options = new { settings = settingsFile } } };

    /// <summary>
    /// The pipe door's switch (HELP5), on this door's spawn: the Agent SDK runs <c>claude</c> with this
    /// process's environment, which is how the pin's own switch reaches it too (AGT2).
    /// </summary>
    public IReadOnlyDictionary<string, string> ToolsUpFront => ClaudeCodeAdapter.LoadToolsUpFront;

    /// <summary>
    /// The adapter's own mechanisms. Pinned EXACT by default (D53's note on a harness that moved
    /// 0.79 in the week it was evaluated): a protocol door whose adapter changes under a running
    /// loop is the moving target D49 §4 already refuses for harnesses.
    /// </summary>
    public HarnessToolchain? Toolchain => new(
        Product: "Claude Code",
        Maker: "Anthropic",
        // 🔴 The BINARY is `claude-agent-acp`, not this adapter's Daoris name — verified against
        // the installed package, after a guess was caught by `harness list` reporting a pin that
        // was there as NOT INSTALLED. Every field here is a claim about somebody else's program.
        Binary: ["claude-agent-acp"],
        VersionArguments: ["--version"],
        // The same account seam the pipe door uses — one variable, applied by one line in the driver,
        // so neither door can run as an account the other would not have chosen.
        ProfileVariable: "CLAUDE_CONFIG_DIR",
        Install: ["npm", "install", "-g", "@agentclientprotocol/claude-agent-acp"],
        // No login flow and no login question of its own: the ACCOUNT belongs to `claude`, which the
        // profile directory carries. `daoris agent login claude-code` is still the verb, and this
        // adapter reads the home it produced. Unknown is permissive, by SES3's rule.
        Package: "@agentclientprotocol/claude-agent-acp",
        // No login of its own: it runs `claude` and reads the home `claude` logged into.
        AccountOf: "claude-code",
        // The same harness underneath, so the same trust record governs this door too.
        TrustFile: ClaudeTrust.FileName);

    private IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) =>
        command is { Count: > 0 } ? command : Toolchain!.Binary;
}

/// <summary>
/// <b>dsh over the protocol door</b> (ACP3/D53): a configuration of the door, not a second seam.
/// </summary>
/// <remarks>
/// <para><b>A profile IS a home here.</b> <c>dsh --profile &lt;name&gt;</c> boots a directory under
/// <c>$DSH_HOME/profiles</c>, so the one variable isolates credentials, settings and sessions
/// together — which is why <c>DSH_HOME</c> is the account seam and why what Daoris writes for this
/// harness goes inside a directory it created rather than beside one it did not.</para>
///
/// <para>🔴 <b>Its wire carries no posture.</b> Observed at 0.1.6-alpha.2: <c>session/new</c> answers
/// with <c>sessionId</c> and <c>configOptions</c> and no <c>modes</c> key — and the one config option
/// is the model catalogue, which Daoris never turns itself (D24; since D98 a person may, for one
/// conversation). So the wire carries no posture to set. It is set in the environment instead, below.</para>
///
/// <para><b>No login question</b> (SES3): dsh has no account to be logged out of, and only a definite
/// *out* refuses — so `unknown` is permissive and a session starts.</para>
///
/// <para><b>No model is named</b> (D24): which model answers is the profile's own `settings.yaml`.</para>
/// </remarks>
public sealed class DshAdapter : ISessionAdapter
{
    public string Name => "dsh";

    public SessionWire Wire => SessionWire.Acp;

    /// <summary>
    /// The profile dsh boots for the automation surface. Its ACP server is <b>automation-only by
    /// their own decision</b> — they removed it as an editor UI — which is exactly the half Daoris
    /// wants and none of the product half D53 declined.
    /// </summary>
    public const string Profile = "acp";

    /// <summary>
    /// dsh's permission posture, as an environment variable read at boot.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>`workspace-write` is D37 in dsh's vocabulary</b>, read from its own shipped bundle: it
    /// feeds both the sandbox policy's mode and the approval policy, which derives to <c>ask</c> for
    /// every value except <c>danger-full-access</c>, where it becomes <c>never</c>. Writes inside the
    /// workspace are sandbox-legal and proceed without asking; anything escalating past the sandbox
    /// asks, which over ACP arrives as <c>session/request_permission</c> and is refused by
    /// construction (D52). Failing closed on escalation is the behaviour, not a limitation.
    ///
    /// <para>It is stated even though it is <b>also dsh's default</b>. A posture that happens to match
    /// somebody else's default is not one Daoris has set, and the default is theirs to change.</para>
    /// </remarks>
    public const string PermissionVariable = "DSH_PERMISSION_MODE";

    /// <summary>The value that expresses D37 here. Never <c>danger-full-access</c>, which is approvals off.</summary>
    public const string Posture = "workspace-write";

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        var info = Spawning.InRoot(
            target, resolved[0], resolved.Skip(1).Concat(["--profile", Profile]), redirectInput: true);

        // Set HERE rather than in the driver's one line, because this posture is genuinely this
        // harness's own mechanism — the other two protocol adapters express the same boundary as a
        // mode on the wire, and a driver that applied one mechanism to all three would set nothing
        // for two of them and report success.
        info.Environment[PermissionVariable] = Posture;

        return info;
    }

    /// <summary>
    /// Pinned exact, and <b>vendored nowhere</b>: 561 MB per machine, and a harness's own packaging is
    /// its own problem — but the version the toolchain installs is asserted, not assumed (D53).
    /// </summary>
    public HarnessToolchain? Toolchain => new(
        Product: "dsh",
        Maker: "DeepSeek",
        Binary: ["dsh"],
        // `-V, --version` — verified against the installed CLI, which printed its exact version.
        VersionArguments: ["--version"],
        ProfileVariable: "DSH_HOME",
        Install: ["npm", "install", "-g", "@deepseek-ai/dsh"],
        // No login flow and no login question: there is no account here to be out of.
        Package: "@deepseek-ai/dsh");

    private IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) =>
        command is { Count: > 0 } ? command : Toolchain!.Binary;
}

/// <summary>
/// <b>Codex over the protocol door</b> (ACP3/D53, closing HARNESS2): the ACP project's Codex adapter,
/// which drives the machine's `codex` exactly as the Claude one drives `claude`.
/// </summary>
/// <remarks>
/// <para>🔴 <b>The posture is `agent`, and it was established rather than guessed</b> — the one thing
/// HARNESS2 said the ACP route does not settle for free. Read from codex-acp@1.12.0's own bundle, the
/// wire offers three modes: <c>read-only</c> (asks for everything, so a driven session stalls on its
/// first edit), <c>agent</c> (workspace-write sandbox, approvals on request) and
/// <c>agent-full-access</c> (<c>dangerFullAccess</c> with approvals <c>never</c>, which is precisely
/// what D37 forbids). There is no third reading.</para>
///
/// <para>🔴 <b>It is stricter than `acceptEdits`, not equivalent.</b> `agent` runs with
/// <c>networkAccess: false</c>, which Claude Code's posture does not — and a session that cannot
/// reach the network fails in ways that look like something else entirely. Recorded here because the
/// surprise belongs next to the constant.</para>
///
/// <para>🔴 <b>`CODEX_HOME` must already exist</b>, where the Claude adapter creates its own config
/// directory. Pointed at a path that is not there, this adapter exits 1 before <c>initialize</c>
/// completes, naming the directory — which is why the toolchain carries <c>ProfileMustExist</c>.</para>
///
/// <para><b>No model is named</b> (D24), and the account belongs to `codex`: this adapter runs the
/// harness and reads the home the harness logged into, so it has no login flow of its own.</para>
/// </remarks>
public sealed class CodexAcpAdapter : ISessionAdapter
{
    public string Name => "codex-acp";

    public SessionWire Wire => SessionWire.Acp;

    public bool Interactive => true;

    /// <summary>D37 in Codex's vocabulary. Never <c>agent-full-access</c>, which is approvals off.</summary>
    public string? AcpPosture => "agent";

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        return Spawning.InRoot(target, resolved[0], resolved.Skip(1), redirectInput: true);
    }

    public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        return Spawning.ChatInRoot(target, resolved[0], resolved.Skip(1));
    }

    public HarnessToolchain? Toolchain => new(
        Product: "Codex",
        Maker: "OpenAI",
        // 🔴 The BINARY is `codex-acp` — the adapter's own bin, not this adapter's Daoris name and
        // not `codex`. Verified against the installed package's `bin` map, the same check that
        // caught the `claude-agent-acp` guess.
        Binary: ["codex-acp"],
        VersionArguments: ["--version"],
        ProfileVariable: "CODEX_HOME",
        Install: ["npm", "install", "-g", "@agentclientprotocol/codex-acp"],
        Package: "@agentclientprotocol/codex-acp",
        // No login of its own: it runs `codex` and reads the home `codex` logged into.
        AccountOf: "codex",
        ProfileMustExist: true);

    private IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) =>
        command is { Count: > 0 } ? command : Toolchain!.Binary;
}

/// <summary>The seam that points the Agent SDK at a <c>claude</c> Daoris chose (ACP2, §1a).</summary>
public static class ClaudeAcp
{
    /// <summary>The environment variable the ACP adapter's SDK reads to find its CLI.</summary>
    public const string ExecutableVariable = "CLAUDE_CODE_EXECUTABLE";

    /// <summary>
    /// Point this spawn at a managed <c>claude</c>.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Null leaves the variable unset</b>, so the SDK finds its CLI the way it always did.
    /// Setting it to an empty string — or to a path that is not there — would break a machine that
    /// works today, which is the additive rule every part of the toolchain holds (D48 §2a).
    /// </remarks>
    public static void PointAtClaude(ProcessStartInfo info, string? managedClaude)
    {
        if (managedClaude is not { Length: > 0 }) return;

        info.Environment[ExecutableVariable] = managedClaude;
        // 🔴 The SDK runs that `claude` with this process's environment, so the pin's own switch
        // travels here (AGT2) — the same one the pipe door's spawn of the same binary carries.
        foreach (var (name, value) in ClaudeCodeAdapter.StayPinned) info.Environment[name] = value;
    }
}

/// <summary>
/// The supported harness (D46 §5): Claude Code in its non-interactive mode, in the repository root,
/// with the composed target as the prompt.
/// </summary>
/// <remarks>
/// <para><b>The permission mapping is the D37 boundary, stated in the harness's own vocabulary.</b>
/// Edits auto-accept — reversible, in-repository, the automated middle — and every other tool runs
/// under the repository's own checked-in permission configuration, exactly as an interactive session
/// there would. Nothing at the outward boundary is auto-approved by the driver, ever: a session that
/// cannot proceed ends, and the observation concludes it honestly.</para>
///
/// <para><b>The connector is the session's voice.</b> An adopted repository's own `.mcp.json` wires
/// the knowledge tools the prompt tells the session to claim and close its quest with — that wiring
/// is the connector's job at adoption, not something the driver may reach in and write (that would be
/// the very edit this whole system exists to prevent).</para>
///
/// <para><b>No model is ever named</b> (D24): which model answers is the harness's own configuration
/// in that repository. The default command is `claude` off the PATH; a machine whose shim needs a
/// path names it in `commands` like any other adapter command.</para>
/// </remarks>
public sealed class ClaudeCodeAdapter : ISessionAdapter
{
    public string Name => "claude-code";

    /// <summary>
    /// The supported harness holds a conversation (D49 §3): run without a one-shot prompt it takes
    /// turns on its own streams, which is all this seam asks of it.
    /// </summary>
    public bool Interactive => true;

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        var arguments = resolved.Skip(1)
            .Concat(["-p", TargetPrompt.Compose(target), "--permission-mode", "acceptEdits"])
            .Concat(StreamJsonOut);

        return Spawning.InRoot(target, resolved[0], arguments);
    }

    /// <summary>
    /// The harness's own structured output (D76, CONV3), checked against the binary before it was
    /// written (docs/2026-09-25-stream-json-evidence.md): `stream-json` needs `--verbose` beside it, and
    /// partial messages are what let the words stream as they are written.
    /// </summary>
    private static readonly string[] StreamJsonOut =
        ["--output-format", "stream-json", "--verbose", "--include-partial-messages"];

    public IStreamMapper StructuredOutput() => new ClaudeStreamJson();

    /// <summary>
    /// A person's message as one `stream-json` user line — the shape the binary took on stdin, one turn
    /// per line, in the probe the evidence records.
    /// </summary>
    public string FrameMessage(string text) => JsonSerializer.Serialize(new
    {
        type = "user",
        message = new { role = "user", content = new[] { new { type = "text", text } } },
    });

    /// <summary>
    /// The turn's stop as a <c>control_request</c> of subtype <c>interrupt</c> — answered by the binary in
    /// the probe with a <c>control_response</c>, the turn ending <c>aborted_streaming</c> or
    /// <c>aborted_tools</c>, and the session taking the next turn (stream-json evidence, § Stopping a turn).
    /// </summary>
    public string FrameInterrupt() => JsonSerializer.Serialize(new
    {
        type = "control_request",
        request_id = $"daoris-{Guid.NewGuid():N}",
        request = new { subtype = "interrupt" },
    });

    /// <summary>
    /// A conversation: no target prompt, because the person supplies the first message.
    /// </summary>
    /// <remarks>
    /// <para>The permission posture is the SAME as a driven session's and for the same reason — the
    /// repository's own checked-in configuration governs, and nothing at the outward boundary is ever
    /// auto-approved (D46 §5, obligation 3). A chat does not get more latitude because a person is
    /// watching; the person being present is why a dirty tree is allowed, not why a push would be.</para>
    ///
    /// <para>No model is named here either (D24): which model answers is the harness's own
    /// configuration in that repository.</para>
    /// </remarks>
    public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        // A conversation on the harness's structured wire (CONV3): each message a `stream-json` line on
        // stdin, one turn each, until input ends — the same two endings the chat door keeps.
        return Spawning.ChatInRoot(
            target, resolved[0],
            resolved.Skip(1)
                .Concat(["-p", "--input-format", "stream-json", "--permission-mode", "acceptEdits"])
                .Concat(StreamJsonOut));
    }

    /// <summary>
    /// `--mcp-config &lt;file&gt;` — verified on the binary (`claude --help`, 2026-09-23: *"Load MCP
    /// servers from JSON files"*). The file is Daoris's, under its home; the repository's own
    /// `.mcp.json` is untouched and still governs its own servers. What the session may CALL stays
    /// the repository's allow-list (D37, D46 §5) — a server offered is a tool available, not a tool
    /// approved.
    /// </summary>
    public void HandServers(ProcessStartInfo info, string configFile)
    {
        info.ArgumentList.Add("--mcp-config");
        info.ArgumentList.Add(configFile);
    }

    /// <summary>The rules Daoris composed (PERM1, D72), as this harness's settings flag.</summary>
    public bool TakesSettings => true;

    /// <summary>
    /// `--settings &lt;file&gt;` — *"load additional settings from"* (`claude --help`, 2.1.280, HELP3's
    /// evidence): the command-line tier, which the harness merges with the person's own settings and the
    /// repository's, `deny` beating `allow`. The posture stays `acceptEdits`: rules are a scope, not a mode.
    /// </summary>
    public void HandSettings(ProcessStartInfo info, string settingsFile)
    {
        info.ArgumentList.Add("--settings");
        info.ArgumentList.Add(settingsFile);
    }

    /// <summary>
    /// The harness's own mechanisms (D49 §4), <b>verified against the real binary</b> before they were
    /// written down — every line of this is a claim about somebody else's tool, and a guessed one
    /// fails at the worst moment, in a person's terminal, saying something that is not true.
    /// </summary>
    /// <remarks>
    /// <para>Install is a whole command because a machine without the harness cannot run it; update
    /// and login are the harness's own subcommands, because a present harness updates and authenticates
    /// itself.</para>
    ///
    /// <para><b>The login question answers JSON with a boolean, and the binary exits 0 either way</b> —
    /// so the output is the answer and the exit code is deliberately not consulted. It also volunteers
    /// an email, an organisation and a subscription tier; Daoris reads the boolean and the email —
    /// who signed in, which is what a person names an account by (D66 §3) — and keeps neither the
    /// organisation nor the tier.</para>
    ///
    /// <para><c>CLAUDE_CONFIG_DIR</c> is the environment seam: a spawn under it is genuinely a separate
    /// account — a fresh directory reports logged out while the machine's own home reports logged in.</para>
    /// </remarks>
    public HarnessToolchain? Toolchain => new(
        Product: "Claude Code",
        Maker: "Anthropic",
        Binary: ["claude"],
        VersionArguments: ["--version"],
        ProfileVariable: "CLAUDE_CONFIG_DIR",
        Install: ["npm", "install", "-g", "@anthropic-ai/claude-code"],
        UpdateArguments: ["update"],
        LoginArguments: ["auth", "login"],
        LoginCheck: new LoginQuestion(
            ["auth", "status"],
            LoggedIn: @"""loggedIn""\s*:\s*true",
            LoggedOut: @"""loggedIn""\s*:\s*false",
            Account: @"""email""\s*:\s*""([^""]+)"""),
        // A pin comes from the release bucket, against its SIGNED manifest (AGT2b) — the npm package
        // installs the same native binary, with nothing but npm's own integrity check behind it.
        Channel: ClaudeReleases.Channel,
        // 🔴 Where it records the workspaces a person has accepted (DEPLOY1). Read before every
        // driven spawn, because an untrusted tree makes the repository's own allow-list inert and
        // the session cannot then take or close its quest — nine minutes and a real login, three
        // times over, before this was measured rather than assumed.
        TrustFile: ClaudeTrust.FileName,
        // Its user-tier settings, `model` and `effortLevel` among them (AGT6): read from its ACP adapter's
        // own settings reader and its SDK's settings schema, never guessed. The ACP door's are this one's.
        SettingsFile: AgentSettings.FileName,
        PinnedEnvironment: StayPinned,
        // An account that is an API key (AGT3). Measured on 2.1.280 with an invalid key: `auth
        // status` reads it (api_key, no email) and a `-p` run takes it with no prompt.
        KeyVariable: "ANTHROPIC_API_KEY",
        // What it prints when its provider refuses the credential (AGT3b) — measured on 2.1.280: a
        // `-p` run with an invalid key was silent for 189 s of retries, then printed "Failed to
        // authenticate. API Error: 401 API key is invalid." and exited 1.
        Refused: "API Error: 401");

    /// <summary>
    /// What a pinned <c>claude</c> runs with so it stays the version pinned (AGT2). 🔴 Measured on a
    /// pinned 2.1.270 with no login: its own <c>claude doctor</c> read <i>Auto-updates: enabled</i> and
    /// called a copy in Daoris's folder an npm-global install; with <c>DISABLE_UPDATES=1</c> it read
    /// disabled, refused <c>claude update</c>, and stayed 2.1.270. <c>DISABLE_AUTOUPDATER</c> stops
    /// only the background check, and a pin is every path.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> StayPinned =
        new Dictionary<string, string> { ["DISABLE_UPDATES"] = "1" };

    public IReadOnlyDictionary<string, string> ToolsUpFront => LoadToolsUpFront;

    /// <summary>
    /// 🔴 Read in the managed binary (HELP5, Claude Code 2.1.274): with <c>ENABLE_TOOL_SEARCH</c> unset, every
    /// MCP tool waits behind a <c>ToolSearch</c> step, a whole model round trip before the agent's first real
    /// move; a value its parser reads as false loads every tool with the first request instead.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> LoadToolsUpFront =
        new Dictionary<string, string> { ["ENABLE_TOOL_SEARCH"] = "false" };

    private IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) =>
        command is { Count: > 0 } ? command : Toolchain!.Binary;
}

/// <summary>
/// The adapters that exist, by name. D23 one layer up: `claude-code` first and supported, `codex`
/// second and explicit — and an unknown name is an error naming what exists, never a silent fallback,
/// because a driver that quietly spawned a different harness than the person configured is the same
/// failure as a repository that asked for one layout and received another.
/// </summary>
public sealed class AdapterSet(IReadOnlyDictionary<string, ISessionAdapter> adapters)
{
    /// <summary>Every adapter this build has, in a stable order — what a roster enumerates.</summary>
    public IReadOnlyList<string> Names => [.. adapters.Keys.OrderBy(k => k, StringComparer.Ordinal)];

    /// <summary>The plugin a harness came from (D64), or null for one this build carries.</summary>
    public string? DeclaredBy(string name) =>
        adapters.TryGetValue(name, out var adapter) && adapter is DeclaredAcpAdapter declared
            ? declared.Plugin
            : null;

    /// <summary>
    /// This set plus every harness the catalogue's contributing plugins declare (D64 §3). The
    /// catalogue has already refused any name this set carries, so nothing here can be replaced —
    /// a plugin adds, and the built-in set is exactly what it was.
    /// </summary>
    public AdapterSet WithPlugins(PluginCatalog catalog)
    {
        var joined = new Dictionary<string, ISessionAdapter>(adapters, StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in catalog.Contributing)
        {
            foreach (var harness in plugin.Manifest.Harnesses)
            {
                joined.TryAdd(harness.Name, new DeclaredAcpAdapter(harness, plugin.Manifest.Id));
            }
        }

        return new AdapterSet(joined);
    }

    public ISessionAdapter Resolve(string name)
    {
        if (adapters.TryGetValue(name, out var adapter)) return adapter;

        throw new DriverException(
            $"unknown adapter '{name}' — one of: {string.Join(", ", adapters.Keys.OrderBy(k => k, StringComparer.Ordinal))}. "
            + "An adapter is added deliberately, never guessed.");
    }

    public static AdapterSet Built() => new(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
    {
        // The stub gate-verifies the mechanism with no model in it (D46 §8); `claude-code` is the
        // supported harness on top of that proven loop. `codex` arrives explicitly, later — and until
        // it does, asking for it errors naming these three, which is the honest answer.
        ["stub"] = new StubAdapter(),
        // The same trick one door over (D53/ACP1): it proves the PROTOCOL with no model in it, and a
        // real harness rides that proven door in ACP2 rather than being the thing that proves it.
        ["acp-stub"] = new AcpStubAdapter(),
        ["claude-code"] = new ClaudeCodeAdapter(),
        // The supported harness over the protocol door (ACP2/D53). It arrives BESIDE the pipe door
        // rather than replacing it: D23's "on proof" means a real driven run over ACP, and until
        // that has happened `claude-code` remains what a machine drives with unless it says otherwise.
        ["claude-code-acp"] = new ClaudeAcpAdapter(),
        // Two more CONFIGURATIONS of the same door (ACP3/D53) — not two more seams, which is the
        // whole argument for adopting a protocol rather than a product. Each states its own posture
        // in its own vocabulary, and each arrives beside what was already here.
        ["dsh"] = new DshAdapter(),
        ["codex-acp"] = new CodexAcpAdapter(),
    });
}
