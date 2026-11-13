using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using static Daoris.Knowledge.Mcp.WorkspaceScope;

namespace Daoris.Knowledge.Mcp;

/// <summary>The quest tools: <c>quest_publish</c>, <c>quest_list</c> and <c>quest_respond</c>, and the records an agent writes them with; their words are said on <see cref="KnowledgeServer"/>.</summary>
/// <param name="quests">What <c>quest_list</c> reads.</param>
/// <param name="exchange">What judges a publish and an answer, shared with the HTTP host.</param>
/// <param name="circle">Which workspace <c>quest_list</c> answers from.</param>
/// <param name="asks">The asks, for the one session that publishes as one — an intake.</param>
/// <param name="intake">Which ask this connector speaks for (an intake), and which session, when the driver started it: a quest's publisher and a take's taker.</param>
/// <param name="ledger">The ledger, for the one write a session's connector makes on its own record: that it took its quest.</param>
/// <param name="engines">Each repository's memory, which a listed quest's memory proposals are settled against.</param>
[McpServerToolType]
public sealed class QuestTools(
    QuestStore quests, QuestExchange exchange, WorkspaceScope circle,
    AskDesk? asks = null, IntakeScope? intake = null, SessionLedger? ledger = null, IMemoryEngines? engines = null)
{
    [McpServerTool(Name = "quest_publish")]
    [Description(
        "Ask ANOTHER repository in this family to do something: never edit a sibling or read into it, publish a quest "
        + "and its own agent takes it. Say what is needed and why, with the evidence, not the change you would make. "
        + "If your work depends on the answer, wait on it with quest_respond (action wait) and end your turn.")]
    public async Task<string> PublishQuestAsync(
        [Description("The repository you are working in. An intake publishes as its ask, whatever this says.")]
        string from,
        [Description(
            "The repository asked; it must be addressable in the registry. `repository:lane` asks one lane, "
            + "`repository:lane+lane` several (your own repository's lanes too).")]
        string to,
        [Description("One line: what is wanted.")] string title,
        [Description("Why, and the evidence.")]
        string body,
        [Description("Absolute http or https addresses.")]
        string[]? links = null,
        [Description("Files by path on this machine (relative: in your repository). A remote learns only their names.")]
        string[]? attachments = null,
        [Description("Steps asked in order once this quest is DONE; a decline stops the chain. {parent} in a title or body is the previous quest's id.")]
        ChainStep[]? then = null,
        [Description(
            "For an ask: what the person requires, each in their own words quoted exactly, with the check that proves it. "
            + "A quote they never said is refused; your reading goes in the body. Steps inherit them.")]
        Requirement[]? requirements = null,
        [Description("The few words that tell this quest apart in a list, at most 40 characters. Omit and it is named from its words.")]
        string? shortTitle = null,
        [Description("`design` for design or decision work; omit for ordinary work.")]
        string? weight = null,
        [Description("Repository-relative globs of the files the work will change (`src/frame/**`); they add to the person's paths.")]
        string[]? paths = null,
        [Description(
            "For an ask: whether the person looks at the work running first: off, on, or an environment's name, only on their own "
            + "words, quoted exactly. Without them use reviewProposal. Your off is kept as a proposal: only their press turns a review off.")]
        ReviewChoice? review = null,
        [Description("For an intake where the person said nothing on it: the review you propose and why. Only their press applies it; no set-up step for a chain you propose off.")]
        ReviewProposal? reviewProposal = null,
        [Description(
            "For an intake: the kind of work you read the ask to be, with the person's own words quoted exactly "
            + "(at most 300 characters) as the reason. It is kept beside their choice and only their press applies it; you never set a kind or workflow.")]
        WorkflowProposal? workflowProposal = null,
        CancellationToken ct = default)
    {
        // A path becomes bytes at the door, on the machine that has the file; the exchange judges and keeps them.
        var uploads = new List<QuestUpload>();
        foreach (var path in attachments ?? [])
        {
            var (upload, refusal) = await QuestFiles.ReadAsync(path, Directory.GetCurrentDirectory(), ct)
                .ConfigureAwait(false);
            if (refusal is not null) return refusal + " Nothing was published.";
            uploads.Add(upload!);
        }

        // A step or a requirement sent as null arrives blank, refused by the exchange naming its number.
        var steps = (then ?? []).Select(step => new QuestStep(step?.To ?? "", step?.Title ?? "", step?.Body ?? "") { SetUpIn = step?.SetUpIn }).ToList();
        // A choice left out arrives blank and is refused by the exchange naming why, and so does a proposal.
        var chosen = review is null ? null : new QuestReview(review.Choice ?? "", review.Words);
        // A half left out arrives blank and is refused by the exchange naming which; so does an evidence item
        // naming both or neither.
        var required = (requirements ?? [])
            .Select(r => new QuestRequirement(r?.Quote ?? "", r?.Check ?? "")
            {
                Evidence = [.. (r?.Evidence ?? []).Select(item => new QuestEvidence(item?.Path, item?.Gate))],
            })
            .ToList();

        // An intake publishes AS ITS ASK: the room is no repository, so `from` could name
        // nothing addressable — the ask is the asker, in its own circle, carrying its own links and
        // files beside these. The desk judges through the same exchange, so nothing else differs.
        // A connector is always an agent's, whether or not the driver named its session.
        if (intake is { Active: true, Ask: { } askId } && asks is not null)
        {
            var answered = await asks.PublishAsync(
                    askId, to, DateTimeOffset.UtcNow, ct,
                    new AskDraft(title, body)
                    {
                        Links = links ?? [], Uploads = uploads, Then = steps, Requirements = required, Short = shortTitle,
                        Weight = weight,
                        Paths = paths ?? [],
                        Review = chosen,
                        ReviewProposal = reviewProposal is null ? null : new ReviewProposed(reviewProposal.Choice, reviewProposal.Reason),
                        WorkflowProposal = workflowProposal is null ? null : new WorkflowProposed(workflowProposal.Kind, workflowProposal.Reason),
                    },
                    intake.Session, byAgent: true)
                .ConfigureAwait(false);
            return answered.Refusal == AskRefusal.None
                ? $"As ask `#{askId}`: {answered.Message}{LeftUnsaid(answered.Quest)}"
                : answered.Message;
        }

        // A proposal is an intake's reading, kept on its ask: a session on no ask's intake has none to keep it on.
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
                    Weight = weight,
                    Paths = paths ?? [],
                    Review = chosen,
                    // Which session asked, as the driver named it on this connector.
                    PublishedBy = intake?.Session,
                    // An agent's authority, named session or not: the session only credits it.
                    ByAgent = true,
                },
                DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false);
        return outcome.Refusal == QuestPublishRefusal.None
            ? outcome.Message + LeftUnsaid(outcome.Quest)
            : outcome.Message;
    }

    /// <summary>
    /// What a publish left for Daoris to fill, said beneath its answer: with no short title, the name a list will call it, read from its
    /// words. An ask's quest with no requirements is said so by the ask's own desk, at every door.</summary>
    private static string LeftUnsaid(Quest? quest) =>
        quest is { Short: null }
            ? $"\n\nIt has no short title, so a list will call it \"{quest.Name}\", a name read from its own words."
            : "";

    /// <summary>The memory proposals each listed quest carries, settled first against its quest and its target; none without a ledger.</summary>
    private async Task<ILookup<string, MemoryOp>> ProposalsAsync(IReadOnlyList<Quest> listed, CancellationToken ct)
    {
        if (ledger is null) return Array.Empty<MemoryOp>().ToLookup(op => op.Carried!);
        await new MemoryProposals(ledger.Memory, quests, engines: engines).SettleAsync(DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        return (await ledger.Memory.CarriedAsync([.. listed.Select(quest => quest.Id)], ct).ConfigureAwait(false)).ToLookup(op => op.Carried!);
    }

    private static string Standing(MemoryOp op) => op.State switch
    {
        MemoryOpState.Awaiting => "open: decide it through your own memory operations",
        MemoryOpState.Stale => "stale: the entry moved past the revision it read before it was decided",
        MemoryOpState.Declined => "declined",
        MemoryOpState.Answered => "answered by the quest's close, which applied nothing",
        _ => op.State,
    };

    [McpServerTool(Name = "quest_list")]
    [Description(
        "Quests across the family: what has been asked of whom, and what is outstanding.")]
    public async Task<string> ListQuestsAsync(
        [Description("Only quests addressed to this repository.")]
        string? repository = null,
        [Description("Include finished and declined ones.")]
        bool includeClosed = false,
        [Description(WorkspaceArgument)] string? workspace = null,
        [Description("The page, from 1; each page names the next.")]
        int page = 1,
        CancellationToken ct = default)
    {
        var scope = await circle.ResolveAsync(workspace, ct).ConfigureAwait(false);
        var found = await quests.ListAsync(repository, includeClosed, scope, ct).ConfigureAwait(false);
        CallFound.Say(found.Count);
        if (found.Count == 0)
        {
            return repository is null
                ? $"No open quests{Scoped(scope)}."
                : $"Nothing asked of `{repository}`{Scoped(scope)}.";
        }

        var proposals = await ProposalsAsync(found, ct).ConfigureAwait(false);
        var blocks = new List<QuestListPage.Block>();
        foreach (var group in found.GroupBy(q => q.To).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            foreach (var quest in group)
            {
                var text = new StringBuilder();
                text.AppendLine($"- `#{quest.Id}` **{quest.Title}** — {quest.Status}, from `{quest.From}`");
                // Its publisher's short title, where one is given: the words a list shows for it.
                if (quest.Short is { } shortTitle) text.AppendLine($"  short title: {shortTitle}");
                text.AppendLine($"  {Text.Excerpt(quest.Body, null, 200)}");
                // The lanes of the repository it asks; none is the whole repository.
                if (quest.Lanes.Count > 0) text.AppendLine($"  lanes: {string.Join(" · ", quest.Lanes.Select(lane => $"`{lane}`"))}");
                // The paths it declares: a start meeting them is said, and may wait.
                if (quest.Paths.Count > 0) text.AppendLine($"  paths: {string.Join(" · ", quest.Paths.Select(path => $"`{path}`"))}");
                // A workflow's request: the run that waits on it, its node and its number.
                if (quest.Request is { } request) text.AppendLine($"  asked by a workflow's request `{request}`: its run waits on this quest's done");
                if (quest.Links.Count > 0) text.AppendLine($"  links: {string.Join(" · ", quest.Links)}");
                if (quest.Attachments.Count > 0)
                {
                    // Names only: whoever takes the quest on the machine that keeps them is handed them.
                    text.AppendLine($"  files: {string.Join(" · ", quest.Attachments.Select(a => a.Name))}");
                }

                // The chain around it: what this one follows, and what its close publishes.
                if (quest.Parent is { } parent) text.AppendLine($"  follows `#{parent}`");
                // Ask and wait: what its taker waits on.
                if (quest.Awaits is { } awaits) text.AppendLine($"  waits on `#{awaits}`");
                foreach (var step in quest.Then)
                {
                    text.AppendLine($"  then → `{step.To}`: {step.Title}" + (step.SetUpIn is { } shownIn ? $" (set-up step, in `{shownIn}`)" : ""));
                }

                // The review on the record: the chain's choice, a set-up step's environment, what it showed, and
                // the person's verdicts, so the session working a step reads what it set up and what they said of it.
                if (quest.Review is { } chosen)
                {
                    text.AppendLine($"  review: `{chosen.Choice}`" + (chosen.Words is { } said ? $", on the person's words \"{said}\"" : ""));
                }

                if (quest.SetUpIn is { } environment) text.AppendLine($"  set-up step: shows the work it follows in `{environment}` for the person's review");

                // The person's kind and workflow on a quest no ask published: the latest stands, and the driver that
                // starts it reads it against its workspace's kinds. Only the person sets it.
                if (quest.WorkflowChoices.LastOrDefault() is { } workflow && (workflow.Kind ?? workflow.Workflow) is not null)
                {
                    text.AppendLine("  workflow: "
                                    + string.Join(", ", new[] { workflow.Kind is { } kind ? $"kind `{kind}`" : null, workflow.Workflow is { } named ? $"`{named}`" : null }
                                        .OfType<string>())
                                    + ", the person's choice");
                }

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
                // What the person requires: their words as quoted, and the check, whole.
                foreach (var requirement in quest.Requirements)
                {
                    text.AppendLine($"  requires \"{requirement.Quote}\" — check: {requirement.Check}");
                    // The facts its check turns on that Daoris reads itself.
                    if (requirement.Evidence.Count > 0)
                    {
                        text.AppendLine($"    evidence: {string.Join(" · ", requirement.Evidence.Select(item => $"{item.Kind} `{item.Named}`"))}");
                    }
                }

                if (quest.Note is { Length: > 0 }) text.AppendLine($"  _{quest.Note}_");

                // The memory proposals it carries, each its own decision, with its standing.
                if (proposals[quest.Id].ToList() is { Count: > 0 } carried)
                {
                    text.AppendLine("  memory proposals it carries, each its own decision:");
                    foreach (var (op, number) in carried.Select((op, i) => (op, i + 1)))
                    {
                        text.AppendLine($"  {number}. `{op.Id}` {op.Change.Relation} `{op.Repository}:{op.Change.Target}` at revision {op.Change.Revision}, "
                            + $"from {(op.By is { } by ? MemoryProposer.Words(quest.From, by) : "its proposer")}: {op.Change.Text} ({Standing(op)})");
                    }
                }

                // A quest its graph closes, and its taker's result while it stays taken.
                if (quest is { Closure: not null, Status: QuestStatus.Taken })
                {
                    text.AppendLine(quest.Submission is { } submitted
                        ? "  submitted " + submitted.At.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture)
                          + (submitted.Note is { Length: > 0 } said ? $": _{said}_" : "")
                          + ". It stays taken until its graph closes it at the work's end, after its holds and gates."
                        // A reopened submission, with the person's words, until the next ready answers it.
                        : quest.Reopened is { } reopen
                        ? "  the person reopened its submission " + reopen.At.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture)
                          + (reopen.Note is { Length: > 0 } words ? $": _{words}_" : "")
                          + ". Work on what they said, then say `ready` again, never `done`."
                        : "  its graph closes it: its taker says `ready`, never `done`, when the work is done.");
                }

                // How its done answered each, and whether a departure holds it for the person's yes.
                foreach (var answer in quest.Answers)
                {
                    text.AppendLine(answer.IsDeparture
                        ? $"  requirement {answer.Requirement} departed: {answer.Departed} Quoting \"{answer.Quote}\"."
                        : $"  requirement {answer.Requirement} met: {answer.Met}");
                }

                // What Daoris last read of its evidence: the commit, and each item's code. A commit read alone is the
                // submission's own, and no evidence is read with it.
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
                        // Said so a session on it stops, and one looking for work leaves it.
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

                // Never the plain done: what it departed from is still wanted, accepted or not.
                if (quest.Left)
                {
                    text.AppendLine("  unfinished: it departed from "
                        + $"{string.Join(", ", quest.Answers.Where(answer => answer.IsDeparture).Select(answer => $"requirement {answer.Requirement}"))}, "
                        + "so part of the work is left, and the ask is not done until it is finished or the person closes the ask.");
                }

                blocks.Add(new(quest.To, text.ToString()));
            }
        }

        return QuestListPage.Of(blocks, page, Scoped(scope));
    }

    [McpServerTool(Name = "quest_respond")]
    [Description(
        "Answer a quest addressed to your repository: take it, finish it, or decline it with a reason. Needing another "
        + "repository's change or fact: quest_publish the question, then wait on its id here (action wait) and end your turn; "
        + "you resume with the answer. Where quest_list says its graph closes it, finish with ready, not done. Your reason is "
        + "the handback, the record later readers see: one that only points elsewhere is refused.")]
    public async Task<string> RespondToQuestAsync(
        [Description("The quest id, from quest_list.")] string id,
        [Description("take, done, ready, decline, or wait.")] string action,
        [Description(
            "Your handback, required to decline: what changed, where (the commits), what you proved, and what you did not, "
            + "in the note itself.")]
        string? reason = null,
        [Description("For wait: the id of the question you published.")] string? on = null,
        [Description(
            "For done or ready on a quest with the person's requirements: one answer for each, by number: met with how, or "
            + "departed with the reason and the person's words (quote). One left unanswered is refused. A met answer that names "
            + "evidence is read in your last commit when you end, so commit it before you finish.")]
        RequirementAnswer[]? answers = null,
        CancellationToken ct = default)
    {
        // A number left out arrives as 0, and a half left out blank: the exchange refuses each, naming which.
        var answered = (answers ?? [])
            .Select(answer => new QuestAnswer(answer.Requirement ?? 0, answer.Met, answer.Departed, answer.Quote))
            .ToList();
        // The session the driver started, as its connector is spawned naming it: a take binds the graph named for that
        // session alone. Its close's note is its handback: refused where it only points elsewhere, and kept naming the session.
        var outcome = await exchange.RespondAsync(
                id, action, reason, DateTimeOffset.UtcNow, ct, on: on, answers: answered, session: intake?.Session, handback: true)
            .ConfigureAwait(false);

        if (outcome.Refusal == QuestRespondRefusal.AlreadyTaken
            && string.Equals(action, "take", StringComparison.OrdinalIgnoreCase)
            && intake?.Session is { } retrying && ledger is not null
            && await new QuestRetakes(quests, ledger).AdoptAsync(id, retrying, ct).ConfigureAwait(false) is { } adopted)
        {
            return adopted;
        }

        // A take by a session the driver started is written on its record: how its end is
        // told apart from a stand-down, which the quest's state alone cannot say. A chat's too:
        // how a delete knows the chat served that quest.
        if (outcome.Refusal == QuestRespondRefusal.None
            && string.Equals(action, "take", StringComparison.OrdinalIgnoreCase)
            && intake?.Session is { } session && ledger is not null)
        {
            await ledger.MarkTookAsync(session, id, ct).ConfigureAwait(false);
        }

        return outcome.Message;
    }
}

/// <summary>
/// One step of a chain as an agent writes it — the door's own shape, so each field can
/// describe itself to the model that fills it. Nullable because an agent may leave one out, and the
/// exchange refuses a step without its words naming which step it is.
/// </summary>
public sealed record ChainStep(
    [property: Description("The repository asked at this step.")]
    string? To,
    [property: Description("One line: what is wanted.")]
    string? Title,
    [property: Description("Why, and how to tell it is done.")]
    string? Body,
    [property: Description(
        "Only for a set-up step that shows the work for the person's review: the review environment, one the repository declares, "
        + "never production. It asks the previous step's repository, and its done waits for the person's review.")]
    string? SetUpIn = null);

/// <summary>
/// One requirement as an agent writes it — nullable for the chain step's reason: the exchange
/// refuses one missing a half, naming which, rather than the door dropping it.
/// </summary>
public sealed record Requirement(
    [property: Description("The person's own words, copied exactly, never reworded.")]
    string? Quote,
    [property: Description("How to tell the work meets them.")]
    string? Check,
    [property: Description("Optional, at most 5: what Daoris reads to tell the check was met; a path only where the work plainly leaves one.")]
    RequirementEvidence[]? Evidence = null);

/// <summary>
/// One fact a requirement names as its evidence, as an agent writes it — nullable for the chain step's reason:
/// the exchange refuses an item that names both or neither, or a path that is not one, naming which.
/// </summary>
public sealed record RequirementEvidence(
    [property: Description("A file or folder the last commit must hold: forward slashes, relative to the receiving repository's root.")]
    string? Path,
    [property: Description("A gate the receiving repository declares. Not read yet: refused for now.")]
    string? Gate);

/// <summary>
/// How a done answers one requirement, as an agent writes it — nullable for the chain step's reason: the
/// exchange refuses an answer that names no requirement, or says both or neither, naming which.
/// </summary>
public sealed record RequirementAnswer(
    [property: Description("The requirement's number, from 1, in the order quest_list lists them.")]
    int? Requirement,
    [property: Description("How its check was met. Leave empty when the work departed from it.")]
    string? Met,
    [property: Description("Why the work departed from it, instead of meeting it. Leave empty when it was met.")]
    string? Departed,
    [property: Description("With departed only: the person's own words the departure turns on, copied exactly.")]
    string? Quote);

/// <summary>
/// An intake's kind proposal as it writes it (the workflow design §4.3): a kind and the person's words it rests on,
/// for the person's press. A kind only, never a workflow: the workflow a kind gives is the driving machine's.
/// </summary>
public sealed record WorkflowProposal(
    [property: Description("A kind's id of the workspace, such as docs.")]
    string? Kind,
    [property: Description("The person's own words that say why, copied exactly, at most 300 characters; anything else is refused.")]
    string? Reason);

/// <summary>
/// A chain's review choice as an agent writes it: nullable for the chain step's reason, the exchange refusing one
/// that is not one, naming why.
/// </summary>
public sealed record ReviewChoice(
    [property: Description("off, on , or an environment's name.")]
    string? Choice,
    [property: Description("The person's own words you set it on, copied exactly.")]
    string? Words);

/// <summary>An intake's review proposal as it writes it: a choice and its reason, for the person's press.</summary>
public sealed record ReviewProposal(
    [property: Description("off, on, or an environment's name.")]
    string? Choice,
    [property: Description("Why, in at most 300 characters.")]
    string? Reason);
