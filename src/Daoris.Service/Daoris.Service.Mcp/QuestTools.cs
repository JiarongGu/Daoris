using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using static Daoris.Knowledge.Mcp.WorkspaceScope;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// The quest tools (PART10a): <c>quest_publish</c>, <c>quest_list</c> and <c>quest_respond</c>, and the records an agent
/// writes them with. How every tool's words are written is said on <see cref="KnowledgeServer"/>.
/// </summary>
/// <param name="quests">What <c>quest_list</c> reads.</param>
/// <param name="exchange">What judges a publish and an answer, shared with the HTTP host.</param>
/// <param name="circle">Which workspace <c>quest_list</c> answers from.</param>
/// <param name="asks">The asks, for the one session that publishes as one — an intake (D65 §1b).</param>
/// <param name="intake">
/// Which ask this connector speaks for, when the session is an intake; and which session it is, when the driver started
/// it — what a quest records as its publisher (SESS1) and a take as its taker (STANDDOWN2).
/// </param>
/// <param name="ledger">The ledger, for the one write a session's connector makes on its own record: that it took its quest.</param>
[McpServerToolType]
public sealed class QuestTools(
    QuestStore quests, QuestExchange exchange, WorkspaceScope circle,
    AskDesk? asks = null, IntakeScope? intake = null, SessionLedger? ledger = null)
{
    [McpServerTool(Name = "quest_publish")]
    [Description(
        "Ask ANOTHER repository in this family to do something. Repositories here are not developed "
        + "across: you never edit a sibling, you publish a quest and its own agent takes it. Say what "
        + "is needed and why, with the evidence — not the change you would make. Use before touching "
        + "any repository that is not the one you are working in, and before reading into one: what it "
        + "owns and why its code is shaped that way is its agent's to say. If your work depends on the "
        + "answer, wait on this quest with quest_respond (action wait) and end your turn.")]
    public async Task<string> PublishQuestAsync(
        [Description("The repository asking — the one you are working in. An intake publishes as its ask, whatever this says.")]
        string from,
        [Description(
            "The repository being asked. It must be in the registry as addressable, or nobody there can see it. "
            + "Where the registry lists its lanes, `repository:lane` asks one of them, and `repository:lane+lane` "
            + "several — your own repository's lanes included, which is the one way to ask your own repository.")]
        string to,
        [Description("One line: what is wanted.")] string title,
        [Description("Why, and the evidence. Whoever works there may see a better answer than you did.")]
        string body,
        [Description("Addresses the quest carries — a ticket, a page, a document. Absolute http or https only.")]
        string[]? links = null,
        [Description(
            "Files the quest carries, by their path on this machine — a screenshot, a log, a document. "
            + "Relative paths are the repository you are working in. They are kept on this machine and "
            + "handed to whoever takes the quest here; a remote learns only their names.")]
        string[]? attachments = null,
        [Description(
            "What to ask next, in order, once this quest is DONE — develop, then verify, then report. "
            + "Each step is published automatically when the one before it closes done, on behalf of "
            + "the same asker; a decline stops the chain. Write {parent} in a step's title or body for "
            + "the id of the quest it follows.")]
        ChainStep[]? then = null,
        [Description(
            "What the person requires, for a quest an ask asks: each one their own words, quoted exactly as they "
            + "gave them in the ask or said on it since, with the check that proves the work meets them. A quote "
            + "they never said is refused, naming it; your reading of their words belongs in the body. Every step "
            + "of the chain inherits them. A quest one repository asks of another carries none. A requirement may "
            + "name its evidence: a path the done's commit must hold, which Daoris reads itself before what follows goes on.")]
        Requirement[]? requirements = null,
        [Description(
            "A short title: the few words that tell this quest apart in a list, at most 40 characters, about 20 in Chinese. "
            + "Your own words, never the title cut short. Omit it and the quest is named from its own words.")]
        string? shortTitle = null,
        [Description(
            "Whether the person looks at this chain's work running before it lands, for a quest an ask asks: off, on (the "
            + "repository's default environment) or an environment's name, set only on the person's own words, which you quote "
            + "exactly. Every step inherits it. Without their words, propose one with reviewProposal instead. Your off, even on "
            + "their words, is kept as a proposal: only their own press turns a review off.")]
        ReviewChoice? review = null,
        [Description(
            "For an intake, where the person said nothing on it: the review you propose for this chain and your reason, kept on "
            + "the ask beside their choice. Only their press applies it; compose no set-up step for a chain you propose off.")]
        ReviewProposal? reviewProposal = null,
        [Description(
            "For an intake: the kind of work you read this ask to be, proposed on the person's own words, which you quote exactly "
            + "(at most 300 characters) as the reason. It is kept on the ask beside their choice and only their press applies it; "
            + "you never set a kind or a workflow, and the workspace's own kinds decide which are real.")]
        WorkflowProposal? workflowProposal = null,
        CancellationToken ct = default)
    {
        // A path becomes bytes at the door, on the machine that has the file (D65 §2): the exchange
        // judges and keeps them, exactly as it does for the HTTP host's uploads.
        var uploads = new List<QuestUpload>();
        foreach (var path in attachments ?? [])
        {
            var (upload, refusal) = await QuestFiles.ReadAsync(path, Directory.GetCurrentDirectory(), ct)
                .ConfigureAwait(false);
            if (refusal is not null) return refusal + " Nothing was published.";
            uploads.Add(upload!);
        }

        // A step or a requirement sent as null arrives blank, refused by the exchange naming its number (REVIEWENV1b3).
        var steps = (then ?? []).Select(step => new QuestStep(step?.To ?? "", step?.Title ?? "", step?.Body ?? "") { SetUpIn = step?.SetUpIn }).ToList();
        // A choice left out arrives blank and is refused by the exchange naming why (REVIEWENV1b), and so does a proposal.
        var chosen = review is null ? null : new QuestReview(review.Choice ?? "", review.Words);
        // A half left out arrives blank and is refused by the exchange naming which (DRIFT1c); so does an evidence item
        // naming both or neither (EVID1a).
        var required = (requirements ?? [])
            .Select(r => new QuestRequirement(r?.Quote ?? "", r?.Check ?? "")
            {
                Evidence = [.. (r?.Evidence ?? []).Select(item => new QuestEvidence(item?.Path, item?.Gate))],
            })
            .ToList();

        // An intake publishes AS ITS ASK (D65 §1b): the room is no repository, so `from` could name
        // nothing addressable — the ask is the asker, in its own circle, carrying its own links and
        // files beside these. The desk judges through the same exchange, so nothing else differs.
        // A connector is always an agent's, whether or not the driver named its session (REVIEWENV1b3).
        if (intake is { Active: true, Ask: { } askId } && asks is not null)
        {
            var answered = await asks.PublishAsync(
                    askId, to, DateTimeOffset.UtcNow, ct,
                    new AskDraft(title, body)
                    {
                        Links = links ?? [], Uploads = uploads, Then = steps, Requirements = required, Short = shortTitle,
                        Review = chosen,
                        ReviewProposal = reviewProposal is null ? null : new ReviewProposed(reviewProposal.Choice, reviewProposal.Reason),
                        WorkflowProposal = workflowProposal is null ? null : new WorkflowProposed(workflowProposal.Kind, workflowProposal.Reason),
                    },
                    intake.Session, byAgent: true)
                .ConfigureAwait(false);
            return answered.Refusal == AskRefusal.None
                ? $"As ask `#{askId}`: {answered.Message}{LeftUnsaid(answered.Quest, onAnAsk: true)}"
                : answered.Message;
        }

        // A proposal is an intake's reading, kept on its ask (REVIEWENV1b): a session on no ask's intake has none to keep it on.
        if (reviewProposal is not null)
        {
            return "A review proposal is an intake's, kept on its ask for the person's press, and this connector speaks for no "
                   + "intake. Say the review you would propose in the body. Nothing was published.";
        }

        if (workflowProposal is not null)
        {
            return "A kind proposal is an intake's, kept on its ask for the person's press, and this connector speaks for no "
                   + "intake. Say the kind you would propose in the body. Nothing was published.";
        }

        // The judgement — who may be addressed, what a refusal says — lives in the exchange, shared
        // with the HTTP host so the same ask cannot be deliverable through one door and refused at
        // the other.
        var outcome = await exchange.PublishAsync(
                new QuestAsk(from, to, title, body)
                {
                    Links = links ?? [],
                    Uploads = uploads,
                    Then = steps,
                    Requirements = required,
                    Short = shortTitle,
                    Review = chosen,
                    // Which session asked (SESS1), as the driver named it on this connector (PERM2).
                    PublishedBy = intake?.Session,
                    // An agent's authority, named session or not: the session only credits it (REVIEWENV1b3).
                    ByAgent = true,
                },
                DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false);
        return outcome.Refusal == QuestPublishRefusal.None
            ? outcome.Message + LeftUnsaid(outcome.Quest, onAnAsk: false)
            : outcome.Message;
    }

    /// <summary>
    /// What a publish left for Daoris to fill, said beneath its answer (SHORTFIT1), of the quest as it stands: with no short
    /// title, the name a list will call it, read from its words (SESSUX1j); and on an ask's quest with no requirements, that
    /// nothing of the person's words will be checked, since a done answers only those it carries (DRIFT1c, D133 §3). A
    /// repository's quest carries none by design, so it is not told so.
    /// </summary>
    private static string LeftUnsaid(Quest? quest, bool onAnAsk)
    {
        if (quest is null) return "";

        var said = new List<string>();
        if (quest.Short is null)
        {
            said.Add($"It has no short title, so a list will call it \"{quest.Name}\", a name read from its own words.");
        }

        if (onAnAsk && quest.Requirements.Count == 0)
        {
            said.Add("It carries no requirements, so nothing of the person's words will be checked when it is done.");
        }

        return said.Count == 0 ? "" : "\n\n" + string.Join(" ", said);
    }

    [McpServerTool(Name = "quest_list")]
    [Description(
        "Quests across the family: what has been asked of whom, and what is still outstanding. Give a "
        + "repository name to see only what it owes.")]
    public async Task<string> ListQuestsAsync(
        [Description("Only quests addressed to this repository. Omit for the whole family.")]
        string? repository = null,
        [Description("Include finished and declined ones. Default false — outstanding work is the question.")]
        bool includeClosed = false,
        [Description(WorkspaceArgument)] string? workspace = null,
        CancellationToken ct = default)
    {
        if (circle.Refused(workspace) is { } outside) return outside;
        var scope = await circle.ResolveAsync(workspace, ct).ConfigureAwait(false);
        var found = await quests.ListAsync(repository, includeClosed, scope, ct).ConfigureAwait(false);
        if (found.Count == 0)
        {
            return repository is null
                ? $"No open quests{Scoped(scope)}."
                : $"Nothing asked of `{repository}`{Scoped(scope)}.";
        }

        var text = new StringBuilder($"{found.Count} quest(s){Scoped(scope)}:\n\n");
        foreach (var group in found.GroupBy(q => q.To).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            text.AppendLine($"## `{group.Key}` — {group.Count()}");
            foreach (var quest in group)
            {
                text.AppendLine($"- `#{quest.Id}` **{quest.Title}** — {quest.Status}, from `{quest.From}`");
                // Its publisher's short title (SESSUX1j), where one was given: the words a list shows for it.
                if (quest.Short is { } shortTitle) text.AppendLine($"  short title: {shortTitle}");
                text.AppendLine($"  {Text.Excerpt(quest.Body, null, 200)}");
                // The lanes of the repository it asks (D115 §2.2); none is the whole repository.
                if (quest.Lanes.Count > 0) text.AppendLine($"  lanes: {string.Join(" · ", quest.Lanes.Select(lane => $"`{lane}`"))}");
                if (quest.Links.Count > 0) text.AppendLine($"  links: {string.Join(" · ", quest.Links)}");
                if (quest.Attachments.Count > 0)
                {
                    // Names only: whoever takes the quest on the machine that keeps them is handed them.
                    text.AppendLine($"  files: {string.Join(" · ", quest.Attachments.Select(a => a.Name))}");
                }

                // The chain around it (D65 §4): what this one follows, and what its close publishes.
                if (quest.Parent is { } parent) text.AppendLine($"  follows `#{parent}`");
                // Ask and wait (D79): what its taker waits on.
                if (quest.Awaits is { } awaits) text.AppendLine($"  waits on `#{awaits}`");
                foreach (var step in quest.Then)
                {
                    text.AppendLine($"  then → `{step.To}`: {step.Title}" + (step.SetUpIn is { } shownIn ? $" (set-up step, in `{shownIn}`)" : ""));
                }

                // The review on the record (REVIEWENV1b): the chain's choice, a set-up step's environment, what it showed, and
                // the person's verdicts, so the session working a step reads what it set up and what they said of it.
                if (quest.Review is { } chosen)
                {
                    text.AppendLine($"  review: `{chosen.Choice}`" + (chosen.Words is { } said ? $", on the person's words \"{said}\"" : ""));
                }

                if (quest.SetUpIn is { } environment) text.AppendLine($"  set-up step: shows the work it follows in `{environment}` for the person's review");
                foreach (var setUp in quest.SetUps)
                {
                    text.AppendLine($"  set up at `{Clipped.ShortCommit(setUp.Commit, 7)}`"
                        + (setUp.Look is { } look ? $": <{look}>" : setUp.Local ? ", locally on the machine that showed it" : "")
                        + (setUp.Shows is { } shows ? $" — {shows}" : "")
                        + (setUp.Session is null ? " (the person's own)" : ""));
                }

                foreach (var verdict in quest.Verdicts)
                {
                    text.AppendLine($"  the person said {verdict.Said}" + (verdict.Commit is { } at ? $" of the set-up at `{Clipped.ShortCommit(at, 7)}`" : "")
                        + (verdict.Words is { } words ? $": \"{words}\"" : ""));
                }
                // What the person requires (DRIFT1c): their words as quoted, and the check, whole.
                foreach (var requirement in quest.Requirements)
                {
                    text.AppendLine($"  requires \"{requirement.Quote}\" — check: {requirement.Check}");
                    // The facts its check turns on that Daoris reads itself (EVID1a).
                    if (requirement.Evidence.Count > 0)
                    {
                        text.AppendLine($"    evidence: {string.Join(" · ", requirement.Evidence.Select(item => $"{item.Kind} `{item.Named}`"))}");
                    }
                }

                if (quest.Note is { Length: > 0 }) text.AppendLine($"  _{quest.Note}_");

                // FABRIC-CLOSE1a (§E5): a quest its graph closes, and its taker's result while it stays taken.
                if (quest is { Closure: not null, Status: QuestStatus.Taken })
                {
                    text.AppendLine(quest.Submission is { } submitted
                        ? "  submitted " + submitted.At.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture)
                          + (submitted.Note is { Length: > 0 } said ? $": _{said}_" : "")
                          + ". It stays taken until its graph closes it at the work's end, after its holds and gates."
                        // FABRIC-CLOSE1c4: a reopened submission, with the person's words, until the next ready answers it.
                        : quest.Reopened is { } reopen
                        ? "  the person reopened its submission " + reopen.At.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture)
                          + (reopen.Note is { Length: > 0 } words ? $": _{words}_" : "")
                          + ". Work on what they said, then say `ready` again, never `done`."
                        : "  its graph closes it: its taker says `ready`, never `done`, when the work is done.");
                }

                // How its done answered each (DRIFT1d), and whether a departure holds it for the person's yes.
                foreach (var answer in quest.Answers)
                {
                    text.AppendLine(answer.IsDeparture
                        ? $"  requirement {answer.Requirement} departed: {answer.Departed} Quoting \"{answer.Quote}\"."
                        : $"  requirement {answer.Requirement} met: {answer.Met}");
                }

                // What Daoris last read of its evidence (EVID1a): the commit, and each item's code. A commit read alone is the
                // submission's own (FABRIC-CLOSE1c6), and no evidence was read with it.
                if (quest.Evidence is { Items.Count: 0 } alone)
                {
                    text.AppendLine($"  its submission's commit, read at the end of its session: `{Clipped.ShortCommit(alone.Commit, 7)}`");
                }
                else if (quest.Evidence is { } read)
                {
                    text.AppendLine($"  evidence read at `{Clipped.ShortCommit(read.Commit, 7)}` ({read.How}): "
                        + string.Join(" · ", read.Items.Select(item => $"requirement {item.Requirement} `{item.Path ?? item.Gate}` {item.Result}")));
                }

                if (quest.Hold is { } hold)
                {
                    text.AppendLine(hold switch
                    {
                        QuestHold.Departed => "  ⚠ held for the person's yes: what follows it waits until they accept the departure.",
                        QuestHold.EvidenceUnread =>
                            "  ⚠ held until Daoris reads its evidence: the driver reads it when the session that closed it ends, and "
                            + "what follows it waits until it is found or the person accepts the done as it stands.",
                        QuestHold.EvidenceMissing =>
                            "  ⚠ held for the person: its evidence was not found in the commit read, and what follows it waits until "
                            + "a later commit holds it or they accept the done as it stands.",
                        // FABRIC-CLOSE1c: said so a session on it stops, and one looking for work leaves it.
                        QuestHold.SourceReopened when quest.Status == QuestStatus.Open =>
                            $"  ⚠ withdrawn: the person reopened the work it grew from, `#{quest.Parent}`, so nobody takes it until that "
                            + "work is ready again.",
                        QuestHold.SourceReopened =>
                            $"  ⚠ held for the person: they reopened the work it grew from, `#{quest.Parent}`, so it goes no further "
                            + "until they choose to go on with it on the new work or to abandon it. Stop here.",
                        _ =>
                            $"  ⚠ held for the person's review in `{quest.SetUpIn}`: what follows it waits until they say the newest "
                            + "set-up is reviewed, or skip the review.",
                    });
                }
                if (quest.Accepted is { } accepted)
                {
                    text.AppendLine((quest.Answers.Any(answer => answer.IsDeparture) ? "  the person accepted the departure " : "  the person accepted the done as it stood ")
                        + accepted.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            text.AppendLine();
        }

        return text.ToString();
    }

    [McpServerTool(Name = "quest_respond")]
    [Description(
        "Answer a quest addressed to the repository you are working in: take it, finish it, or decline "
        + "it. Declining is a real answer and often the right one — it needs a reason, because a bare "
        + "refusal gives the asker nothing to act on. Finishing a quest that carries the person's requirements "
        + "answers each one (answers). When your work needs something another repository "
        + "owns — a change there, or a fact about its code — publish the question to it with "
        + "quest_publish, then WAIT on that question here (action wait, on the question's id) and end "
        + "your turn: your quest stays yours, and the driver resumes you in the same tree with the answer. "
        + "Where quest_list says its graph closes it, finish with ready instead of done: the same reason and answers, kept as "
        + "your submission, and Daoris closes the quest at the work's end.")]
    public async Task<string> RespondToQuestAsync(
        [Description("The quest id, from quest_list.")] string id,
        [Description("take, done, ready, decline, or wait. ready finishes a quest its graph closes.")] string action,
        [Description("Required to decline; worth giving when finishing.")] string? reason = null,
        [Description("For wait: the id of the question you published to another repository.")] string? on = null,
        [Description(
            "For done or ready, on a quest that carries the person's requirements: one answer for each, by its number. Met, saying "
            + "how its check was met; or departed, with the reason and the person's own words it turns on (quote), "
            + "copied exactly. A done that leaves one unanswered is refused. A departure is shown to the person, and "
            + "what follows the quest waits for their yes. Where a met requirement names evidence, Daoris reads it in your "
            + "last commit when you end, and a met answer without it holds the quest for the person, so commit it before "
            + "you finish. A quest with no requirements takes none.")]
        RequirementAnswer[]? answers = null,
        CancellationToken ct = default)
    {
        // A number left out arrives as 0, and a half left out blank: the exchange refuses each, naming which (DRIFT1d).
        var answered = (answers ?? [])
            .Select(answer => new QuestAnswer(answer.Requirement ?? 0, answer.Met, answer.Departed, answer.Quote))
            .ToList();
        // The session the driver started, as its connector was spawned naming it (FABRIC-CLOSE1o): a take binds the graph named for that
        // session alone.
        var outcome = await exchange.RespondAsync(
                id, action, reason, DateTimeOffset.UtcNow, ct, on: on, answers: answered, session: intake?.Session)
            .ConfigureAwait(false);

        // A take by a session the driver started is written on its record (STANDDOWN2): how its end is
        // told apart from a stand-down, which the quest's state alone cannot say. A chat's too (CHATTAKE1):
        // how a delete knows the chat served that quest.
        if (outcome.Refusal == QuestRespondRefusal.None
            && string.Equals(action, "take", StringComparison.OrdinalIgnoreCase)
            && intake?.Session is { } session && ledger is not null)
        {
            await ledger.MarkTookAsync(session, id, outcome.TakeBind, ct).ConfigureAwait(false);
        }

        return outcome.Message;
    }
}

/// <summary>
/// One step of a chain as an agent writes it (D65 §4) — the door's own shape, so each field can
/// describe itself to the model that fills it. Nullable because an agent may leave one out, and the
/// exchange refuses a step without its words naming which step it was.
/// </summary>
public sealed record ChainStep(
    [property: Description("The repository asked at this step. Every step is asked on behalf of the chain's asker.")]
    string? To,
    [property: Description("One line: what is wanted. {parent} becomes the id of the quest this step follows.")]
    string? Title,
    [property: Description("Why, and how to tell it is done — e.g. where to look in the browser. {parent} works here too.")]
    string? Body,
    [property: Description(
        "Only for a set-up step, which shows the work for the person's review: the review environment it is shown in, one the "
        + "repository declares, never production. It asks the same repository as the step before it, after that repository's "
        + "last step, and its done waits for the person's review.")]
    string? SetUpIn = null);

/// <summary>
/// One requirement as an agent writes it (DRIFT1c) — nullable for the chain step's reason: the exchange
/// refuses one missing a half, naming which, rather than the door dropping it.
/// </summary>
public sealed record Requirement(
    [property: Description("The person's own words, copied exactly from what they asked or said since — never reworded.")]
    string? Quote,
    [property: Description("How to tell the work meets them, in the terms of the repository asked.")]
    string? Check,
    [property: Description(
        "Optional: what Daoris reads itself to tell the check was met, at most 5 items. Name a path only where the work "
        + "plainly leaves a file or folder the person can name.")]
    RequirementEvidence[]? Evidence = null);

/// <summary>
/// One fact a requirement names as its evidence, as an agent writes it (EVID1a) — nullable for the chain step's reason:
/// the exchange refuses an item that names both or neither, or a path that is not one, naming which.
/// </summary>
public sealed record RequirementEvidence(
    [property: Description(
        "A file or folder the work's last commit must hold: forward slashes, relative to the receiving repository's root, "
        + "e.g. docs/report.md.")]
    string? Path,
    [property: Description(
        "A gate the receiving repository declares. Not read yet: a gate is refused until its landing queue reads gates.")]
    string? Gate);

/// <summary>
/// How a done answers one requirement, as an agent writes it (DRIFT1d) — nullable for the chain step's reason: the
/// exchange refuses an answer that names no requirement, or says both or neither, naming which.
/// </summary>
public sealed record RequirementAnswer(
    [property: Description("The requirement's number, from 1, in the order quest_list lists them.")]
    int? Requirement,
    [property: Description("How its check was met. Leave empty when the work departed from it.")]
    string? Met,
    [property: Description("Why the work departed from it, instead of meeting it. Leave empty when it was met.")]
    string? Departed,
    [property: Description(
        "With departed only: the person's own words the departure turns on, copied exactly from what they asked or "
        + "said since. Your reading of their words is the reason, never their words.")]
    string? Quote);

/// <summary>
/// An intake's kind proposal as it writes it (WORKFLOW1i, the workflow design §4.3): a kind and the person's words it rests on,
/// for the person's press. A kind only, never a workflow: the workflow a kind gives is the driving machine's.
/// </summary>
public sealed record WorkflowProposal(
    [property: Description("The kind of work the ask is, a kind's id of the workspace such as docs: lower-case letters, digits and dashes.")]
    string? Kind,
    [property: Description(
        "The person's own words that say why, copied exactly from what they asked or said since, in at most 300 characters. "
        + "A reason that is not their words is refused.")]
    string? Reason);

/// <summary>
/// A chain's review choice as an agent writes it (REVIEWENV1b): nullable for the chain step's reason, the exchange refusing one
/// that is not one, naming why.
/// </summary>
public sealed record ReviewChoice(
    [property: Description("off, on (the repository's default environment), or an environment's name.")]
    string? Choice,
    [property: Description(
        "The person's own words you set it on, copied exactly from what they asked or said since. Without them, propose one "
        + "instead.")]
    string? Words);

/// <summary>An intake's review proposal as it writes it (REVIEWENV1b): a choice and its reason, for the person's press.</summary>
public sealed record ReviewProposal(
    [property: Description("off, on (the repository's default environment), or an environment's name.")]
    string? Choice,
    [property: Description("Why, in at most 300 characters, such as that the work changes only a document.")]
    string? Reason);
