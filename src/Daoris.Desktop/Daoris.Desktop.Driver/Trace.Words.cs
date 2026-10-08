using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What a trace prints (TRACE1, D143): the ask, its quests and their sessions, each section naming the store it was read
/// from, and every link nothing keeps said missing where it would have been. The driver's own sentences, in English, as the
/// headless host's every line is.
/// </summary>
/// <remarks>
/// <para><b>It words the chain, and reads nothing itself</b> (TRACE1b): <see cref="TraceChains"/> reads every store and
/// compares every moment, and the screen words the same chain from its codes (D50). What a failed read said is the
/// terminal's alone, since it may name a file under the home.</para>
///
/// <para><b>Facts, and moments compared.</b> What stood when a session started is read by comparing the moments each store
/// kept with the moment the session opened, never from what stands now: a standing answer set after a start says that what
/// stood before is not kept, since <c>driver.json</c> keeps the latest; a go-ahead answered after says the same, since an
/// answer replaces the one before.</para>
///
/// <para><b>An instruction by its event and size, never its words</b>: what a session was handed is transcript-class and stays
/// in its record (D76, D47 §4), where the session's page shows it. Its sections are read from the account kept beside it
/// (CONTEXT1): sizes, sources and what each bound left out, in the driver's words.</para>
/// </remarks>
internal static class TraceWords
{
    /// <summary>Each store that did not answer, said once at the top: its links below are then said not read.</summary>
    internal static IReadOnlyList<string> Unread(IReadOnlyList<TraceUnread> unread) =>
    [
        .. unread.Select(store => store.Store switch
        {
            TraceStores.Sessions => $"the service's session records could not be read: {store.Problem}",
            TraceStores.Quests => $"the service's quests could not be read: {store.Problem}",
            _ => $"{LandedBranches.FileName} under the home does not read, so no landing is read from it: {store.Problem}",
        }),
    ];

    /// <summary>The chain, whole: the heading and how the id was found, then each ask, its quests and their sessions.</summary>
    internal static string Say(TraceChain chain)
    {
        var text = new StringBuilder();
        var shown = chain.Kind == TraceEntry.Quest ? $"#{chain.Id}" : chain.Id;
        text.Append($"trace: {chain.Kind} {shown} — each link read from the store that keeps it; a link nothing keeps is said missing.\n");
        foreach (var line in chain.Found.Select(Found).Concat(Unread(chain.Unread))) text.Append($"  {line}\n");

        foreach (var link in chain.Links)
        {
            text.Append('\n');
            switch (link)
            {
                case { Ask: { } ask }: Ask(text, ask); break;
                case { Quest: { } quest }: Quest(text, quest); break;
                case { Session: { } session }: Session(text, session); break;
                case { Unrecorded: { } unrecorded }: Unrecorded(text, unrecorded); break;
            }
        }

        return text.ToString();
    }

    private static string Found(TraceFoundBy by) => by.How switch
    {
        TraceFoundBy.Evidence => $"found in session {by.Session}'s evidence: {by.Line}",
        TraceFoundBy.Tip => $"found as the tip of branch {by.Branch}, which session {by.Session}'s landing made",
        _ => $"found as the commit a plugin pushed of branch {by.Branch}, which session {by.Session}'s landing made",
    };

    /// <summary>The ask, from its door: the person's words verbatim with how and when each was given, and its go-aheads.</summary>
    private static void Ask(StringBuilder text, TraceAskLink ask)
    {
        if (ask.Missing == TraceLinkGaps.Unread)
        {
            text.Append($"ask #{ask.Id} · could not be read: {ask.Problem}\n");
            return;
        }

        if (ask.Missing == TraceLinkGaps.NotFound)
        {
            text.Append($"ask #{ask.Id} · not found: the service holds no ask by that id, which {NamedBy(ask.NamedBy)} names\n");
            return;
        }

        text.Append($"ask #{ask.Id} · the service's ask record (GET /api/asks/{ask.Id})\n");
        text.Append($"  workspace {ask.Workspace} · {ask.State} · answered by the {ask.Tier} tier\n");
        if (ask.Intake is { } intake)
        {
            text.Append(intake.State is not { } state
                ? $"  intake session {intake.Session} · the service holds no record of it\n"
                : $"  intake session {intake.Session} · {StateOf(state)} · {Agent(intake.Agent!)}\n");
        }

        if (ask.Quests.Count > 0) text.Append($"  quests it became: {string.Join(", ", ask.Quests.Select(id => $"#{id}"))}\n");
        if (ask.Note is { } note) text.Append($"  its close said: {note}\n");

        if (ask.Words is not { } words)
        {
            text.Append("  the person's words: this service answers none, a host from before they were kept, which is not the same as none said\n");
            text.Append("  its sentence:\n").Append(Quoted(ask.Sentence ?? "", "    "));
        }
        else
        {
            text.Append($"  the person's words ({words.Count}), oldest first:\n");
            foreach (var word in words)
            {
                text.Append($"    {When(word.At)} · {Given(word)}\n").Append(Quoted(word.Text, "      "));
            }

            if (ask.WordsKeptFrom is { } from)
            {
                text.Append($"  their words are kept from {When(from)}: what they said before then is in its sessions' records\n");
            }
        }

        if (ask.GoAheads is not { } goAheads)
        {
            text.Append("  go-aheads: this service answers none, a host from before them\n");
        }
        else if (goAheads.Count == 0)
        {
            text.Append("  go-aheads: none asked\n");
        }
        else
        {
            text.Append($"  go-aheads ({goAheads.Count}):\n");
            foreach (var goAhead in goAheads)
            {
                var asked = goAhead.FirstAskedAt is { } at ? $" · first asked by session {goAhead.FirstAskedBy}, {When(at)}" : "";
                var near = goAhead.Near is { } other ? $" · asked again beside go-ahead {other}, whose words it shared" : "";
                text.Append($"    {goAhead.Number} · {goAhead.Named} · {Answered(goAhead.Answer)}{asked}{near}\n");
            }
        }
    }

    /// <summary>The quest as its operations replay, from its door: where it came from, its requirements and answers, its records.</summary>
    private static void Quest(StringBuilder text, TraceQuestLink quest)
    {
        if (quest.Missing is { } missing)
        {
            text.Append(missing == TraceLinkGaps.Unread
                ? $"quest #{quest.Id} · could not be read: the service's quests did not answer\n"
                : $"quest #{quest.Id} · not found: the service holds no quest by that id, which {NamedBy(quest.NamedBy)} names\n");
            Records(text, quest);
            return;
        }

        text.Append($"quest #{quest.Id} → {quest.Address} · the service's quest, as its operations replay; ")
            .Append("no door on a local host reads the operations themselves\n");
        text.Append($"  \"{quest.Title}\"\n");
        text.Append($"  {quest.Status} · filed {WhenOr(quest.Filed)} · last moved {WhenOr(quest.Moved)}\n");
        text.Append(quest.Ask is { } askId
            ? $"  asked by ask #{askId}\n"
            : $"  on no ask: quest #{quest.Id} was asked by `{quest.From}` (its sender), so no person's words, go-aheads or requirements on an ask hold it\n");
        if (quest.Parent is { } parent) text.Append($"  a step of quest #{parent}, published when that one closed\n");
        if (quest.PublishedBy is { } by) text.Append($"  published by session {by}\n");
        if (quest.Awaits is { } awaits) text.Append($"  waits on quest #{awaits}, asked of the repository that knows\n");
        if (quest.Held) text.Append(Held(quest));
        if (quest.Evidence is { } read)
        {
            text.Append($"  evidence read at `{Short(read.Commit)}` ({read.How}), ")
                .Append(read.Session is { } session ? $"at the end of session {session}" : "from a terminal")
                .Append(read.Machine is { } machine ? $" on {machine}" : "")
                .Append($", {WhenOr(read.At)}: {read.Found} of {read.Items} found\n");
        }

        if (quest.Accepted is { } accepted) text.Append($"  accepted {When(accepted)}: your yes to its departure\n");
        if (quest.Note is { } note) text.Append($"  its close said: {OneLine(note)}\n");

        if (quest.Requirements.Count > 0)
        {
            text.Append($"  requirements ({quest.Requirements.Count}), each the person's words and its check:\n");
            foreach (var requirement in quest.Requirements)
            {
                text.Append($"    {requirement.Number} · \"{requirement.Quote}\" · check: {requirement.Check}\n");
                text.Append(requirement.Answer switch
                {
                    TraceAnswers.Met => $"        met: {requirement.Met}\n",
                    TraceAnswers.Departed => $"        departed: {requirement.Departed}, on their words \"{requirement.On}\"\n",
                    TraceAnswers.Unanswered => "        not answered: its done carries no answer to it\n",
                    _ => "        not answered yet: a done answers it\n",
                });
                if (requirement.Evidence.Count > 0)
                {
                    text.Append($"        evidence: {string.Join(" · ", requirement.Evidence.Select(EvidenceItem))}\n");
                }
            }
        }

        if (quest.Then.Count > 0)
        {
            text.Append($"  then ({quest.Then.Count}): {string.Join("; ", quest.Then.Select(step => $"{step.To} · \"{step.Title}\""))}\n");
        }

        Records(text, quest);
    }

    /// <summary>
    /// Why a held done waits, and its doors (DRIFT1d; EVID1b, D144 §6): a departure waits for the person's yes; evidence nobody
    /// read waits for the session's end or the terminal's check; evidence read and not found waits for a later commit or the
    /// yes. A host before evidence names no cause, and its one hold was a departure.
    /// </summary>
    private static string Held(TraceQuestLink quest) => quest.Hold switch
    {
        EvidenceCodes.Unread =>
            $"  held: its evidence is not read yet: the driver reads it when the session that closed it ends, or "
            + $"`daoris-driver quest check {quest.Id}` reads it; your yes takes the done as it stands (`daoris-driver quest accept {quest.Id}`)\n",
        EvidenceCodes.MissingHold =>
            $"  held: its evidence was not found in the commit read, and waits for you: a later commit that holds it "
            + $"(`daoris-driver quest check {quest.Id} --commit <sha>`), or your yes to the done as it stands (`daoris-driver quest accept {quest.Id}`)\n",
        // A set-up step's review (REVIEWENV1c, D154 point 9): the person's look lets it go, which a yes to a departure does not.
        EvidenceCodes.Unreviewed =>
            $"  held: it waits for your review of what it showed (`daoris-driver quest review {quest.Id} reviewed`, or "
            + $"`daoris-driver quest review {quest.Id} not-yet \"…\"`), or your skip of it (`daoris-driver quest review {quest.Id} skip`); "
            + "a yes to a departure does not let it go\n",
        _ => $"  held: its done departed from what you required, and waits for your yes (`daoris-driver quest accept {quest.Id}`)\n",
    };

    /// <summary>One evidence item and what was last read of it, as the record's bundle says it; or that it is not read yet.</summary>
    private static string EvidenceItem(TraceEvidenceItem item)
    {
        var named = item.Kind == "gate" ? $"gate `{item.Named}`" : $"`{item.Named}`";
        return item.Result is not { } result
            ? $"{named} not read yet"
            : $"{named} {EvidenceCheck.Said(new EvidenceRead(0, item.Kind == "path" ? item.Named : null, item.Kind == "gate" ? item.Named : null, result) { Changed = item.Changed, Spelled = item.Spelled })}";
    }

    /// <summary>The records that name a quest, oldest first, by id and state: each one's own trace is a word away.</summary>
    private static void Records(StringBuilder text, TraceQuestLink quest) =>
        text.Append(quest.Records switch
        {
            null => "  its session records could not be read\n",
            { Count: 0 } => "  no session record names it\n",
            var on => $"  session records on it, oldest first: {string.Join(", ", on.Select(session => $"{session.Id} {session.State}"))}\n",
        });

    /// <summary>One session: its record's facts, then this machine's (its events, its rules, its landing), then what stood when it started.</summary>
    private static void Session(StringBuilder text, TraceSessionLink session)
    {
        text.Append($"session {session.Id} · {session.Kind} · {StateOf(session.State)} · the service's session record\n");
        text.Append($"  opened {WhenOr(session.Opened)} · last moved {WhenOr(session.Moved)}\n");
        text.Append($"  {Agent(session.Agent)}\n");
        if (session.Teammate)
        {
            text.Append("  a teammate's record: it ran on another machine, which keeps its account, tree, events and rules\n");
        }
        else
        {
            text.Append($"  {(session.TreePath is { } tree ? $"tree {tree}" : "tree not recorded")} · ")
                .Append($"{(session.BaseCommit is { } commit ? $"grew from commit {commit}" : "base commit not recorded")}\n");
        }

        if (session.Quest is null && session.Ask is null) text.Append("  a conversation on no quest: no quest and no ask to read\n");
        Before(text, session.Before);
        if (session.Took) text.Append("  it took the quest through its own connector\n");
        if (session.Note is { } note) text.Append($"  its record's note: {OneLine(note)}\n");
        if (session.Answer is { } answer) text.Append("  the person's answer to its park:\n").Append(Quoted(answer, "    "));
        Evidence(text, session.Evidence);
        if (session.Events is { } events) Events(text, session, events);
        if (session.Rules is { } rules) Rules(text, session, rules);
        if (session.Landing is { } landing) Landing(text, landing);
        if (session.Stood is { } stood) Stood(text, session, stood);
    }

    /// <summary>The record before it on its quest, by when each opened: its ending, and whether it ran in the same tree.</summary>
    private static void Before(StringBuilder text, TraceBefore? before)
    {
        if (before is null) return;
        if (before.First)
        {
            text.Append("  the first session record on its quest\n");
            return;
        }

        var tree = before.Tree switch
        {
            TraceTrees.Same => "in the same tree",
            TraceTrees.Other => "in another tree",
            _ => "its tree not recorded",
        };
        text.Append(before.Ended
            ? $"  before it on this quest: session {before.Session}, ended {before.State} {WhenOr(before.At)}, {tree}\n"
            : $"  before it on this quest: session {before.Session}, still {before.State}, {tree}\n");
    }

    /// <summary>The commits the driver read off its tree at its end, as its record keeps them.</summary>
    private static void Evidence(StringBuilder text, TraceEvidence? evidence)
    {
        if (evidence is null)
        {
            text.Append("  evidence: none on its record\n");
            return;
        }

        text.Append($"  evidence: {evidence.Said}\n");
        foreach (var line in evidence.Lines) text.Append($"    {line}\n");
    }

    /// <summary>
    /// This machine's record of it (D76): the driver's notes before its instruction, which name the account a start chose and
    /// why; each instruction it was handed, by its event and size; and each acceptance of its work a landing's press kept.
    /// </summary>
    private static void Events(StringBuilder text, TraceSessionLink session, TraceEvents events)
    {
        switch (events.Missing)
        {
            case TraceEventGaps.NotAnId:
                text.Append("  events: its id names no record on this machine\n");
                return;
            case TraceEventGaps.NoneHere:
                text.Append("  events: none on this machine for it\n");
                return;
            case TraceEventGaps.Unread:
                text.Append($"  events: the record here does not read: {events.Problem}\n");
                return;
        }

        text.Append($"  events (sessions/{session.Id}.events.jsonl, this machine's):\n");
        foreach (var note in events.Starts) text.Append($"    its start, in the driver's words: {OneLine(note)}\n");

        if (events.Instructions.Count > 0)
        {
            text.Append($"    instruction handed: {string.Join("; then ", events.Instructions.Select(Size))} (its words stay in the record and are not printed)\n");
            foreach (var instruction in events.Instructions) Sections(text, instruction);
        }
        else
        {
            text.Append(session.Kind == "chat"
                ? "    instruction: none; a conversation opens on the person's own words\n"
                : "    instruction handed: no instruction event in its record\n");
        }

        foreach (var note in events.Accepted) text.Append($"    {When(note.At)} · {OneLine(note.Said)}\n");
    }

    /// <summary>
    /// What an instruction was composed of (CONTEXT1, D143 point 1), from the account kept beside it on its event: each section
    /// in the driver's words with what its bound left out, what was handed beside it, then what could have been handed and was
    /// not. An instruction from before the account was kept says so (point 3), and nothing is rebuilt from what stands now.
    /// </summary>
    private static void Sections(StringBuilder text, TraceInstruction instruction)
    {
        if (instruction.Account is not { } account)
        {
            text.Append("    its sections: not kept, since it was handed before the driver kept an account of them\n");
            return;
        }

        text.Append($"    its sections, as the driver composed them (event {instruction.Seq}, {Count(account.Chars)} characters):\n");
        void Section(HandedSection section, string indent)
        {
            text.Append($"{indent}{OneLine(section.Said)}\n");
            foreach (var cut in section.Cuts ?? []) text.Append($"{indent}  left out: {OneLine(cut.Said)}\n");
        }

        foreach (var section in account.Sections.Where(section => section.None is null)) Section(section, "      ");
        var absent = account.Sections.Where(section => section.None is not null).ToList();
        if (absent.Count == 0) return;
        text.Append("      not handed:\n");
        foreach (var section in absent) Section(section, "        ");
    }

    /// <summary>One instruction by its event and its size: the length written, or, where the record cut it, the length it said.</summary>
    private static string Size(TraceInstruction instruction) => instruction.Kept is { } kept
        ? $"event {instruction.Seq}, {Count(instruction.Chars)} characters, of which the record keeps the first {Count(kept)}"
        : $"event {instruction.Seq}, {Count(instruction.Chars)} characters";

    /// <summary>
    /// The rules it was handed (PERM1, D72), from the file the driver wrote under the home for it, while that file stands: it is
    /// the session's own and goes when its run ends.
    /// </summary>
    private static void Rules(StringBuilder text, TraceSessionLink session, TraceRules rules)
    {
        switch (rules.Missing)
        {
            case TraceRuleGaps.NoneWhileRunning:
                text.Append("  rules handed: no file under the home while it runs: its agent takes no rules file, or there was nothing to hand\n");
                return;
            case TraceRuleGaps.GoneWithRun:
                text.Append("  rules handed: not on this machine: the file goes when its session ends, and a parked session's when its run does\n");
                return;
            case TraceRuleGaps.Unread:
                text.Append($"  rules handed: the file under the home does not read: {rules.Problem}\n");
                return;
        }

        text.Append($"  rules handed ({SpawnServers.Folder}/{session.Id}.settings.json, kept while it runs): ")
            .Append($"{rules.Allowed} allowed, {rules.Asked} asked, {rules.Denied} denied")
            .Append(rules.Hard == 0 ? "" : $"; {rules.Hard} hard denial{(rules.Hard == 1 ? "" : "s")} beside the harness's own")
            .Append(rules.Guarded ? "; the tree guard holds it to its tree" : "")
            .Append('\n');
    }

    /// <summary>
    /// Where its work landed, from <c>landings.json</c> (WSR5): each branch a landing made of it, standing or gone. A landing
    /// into the line records no branch, so where none is, the acceptance its record keeps is pointed to, or said absent.
    /// </summary>
    private static void Landing(StringBuilder text, TraceLanding landing)
    {
        foreach (var branch in landing.Branches) text.Append($"  landing: {Landed(branch)}\n");
        if (landing.Due is { } due) DueToLand(text, due);
        text.Append(landing.Missing switch
        {
            null => "",
            TraceLandingGaps.Unread => $"  landing: {LandedBranches.FileName} does not read, so no branch landing of it is read\n",
            TraceLandingGaps.Unknown => "  landing: no branch landing on this machine names it, and no record of it here could say whether its work was accepted\n",
            TraceLandingGaps.None => "  landing: none: no branch landing on this machine names it, and its record keeps no acceptance of its work\n",
            _ => "  landing: no branch landing on this machine names it; its record keeps the acceptance above, which is all a "
                 + "landing into the line (merge) keeps, without the merge's own commit\n",
        });
    }

    /// <summary>A landing whose session the service holds no record of: what the landing alone says.</summary>
    private static void Unrecorded(StringBuilder text, TraceUnrecordedLink unrecorded)
    {
        text.Append($"session {unrecorded.Session} · its landing names it, and the service holds no record of it\n");
        foreach (var branch in unrecorded.Branches) text.Append($"  landing: {Landed(branch)}\n");
    }

    private static string Landed(TraceBranch landing)
    {
        var said = new StringBuilder($"branch {landing.Branch} at {landing.Tip}, ");
        said.Append(landing.Line is { } line ? $"from line {line}" : "from a line git named none of").Append($", {When(landing.At)}");
        if (landing.From is { } from) said.Append($"; grew from {from}");
        if (landing.Plugin is { } plugin)
        {
            said.Append(landing.Pushed ? $"; pushed by plugin {plugin}" : $"; handed to plugin {plugin}, which did not push it");
            if (landing.PullRequest is { } pull) said.Append($", pull request {pull}");
        }

        // What its plugin last answered about its pull request, read as kept and never asked (PLUGHOOK1c, D148 point 6).
        said.Append(PullRequestWords.Row(landing.PullRequestState, null, landing.PullRequestAskFailed, null));

        if (landing.Gone is { } gone)
        {
            said.Append($"; gone since {When(gone)}");
            if (landing.RemovedAs is { } kind) said.Append($", removed as {kind}{(landing.RemovedOn is { } on ? $" on {on}" : "")}");
        }

        // Who accepted it, and the rule it was made under (LAND2b, D145 point 6): kept since then, and said missing before (D143).
        said.Append(landing.AcceptedBy switch
        {
            AcceptedBy.Auto => "; accepted automatically when its quest was done",
            AcceptedBy.Person => "; accepted by the person's press",
            _ => "; who accepted it is not kept: it landed before landings kept it",
        });
        if (landing.Rule is { } rule)
        {
            said.Append($"; under the {rule.Source}'s rule, ")
                .Append(rule.Plugin is { } named ? $"naming plugin {named}" : "naming no plugin")
                .Append(rule.AutoAccept ? ", accepting automatically" : ", accepting at a press");
        }

        // The review that let it go (REVIEWENV1c, design §3.5): the person's reviewed, with its environment and set-up's commit, or
        // their skip. Nothing for a landing no review was asked of, and for one recorded before.
        said.Append(landing.Review switch
        {
            { Said: ReviewVerdicts.Reviewed } review =>
                $"; reviewed by you in `{review.Environment}`"
                + (review.Quest is { } step ? $" on set-up step #{step}" : "")
                + (review.Commit is { } commit ? $" at {Short(commit)}" : "")
                + (review.At is { } at ? $", {When(at)}" : ""),
            { Said: ReviewVerdicts.Skipped } review =>
                $"; landed without a review, which you skipped{(review.Quest is { } on ? $" on quest #{on}" : "")}"
                + (review.Words is { Length: > 0 } words ? $", saying: \"{OneLine(words)}\"" : ""),
            _ => "",
        });

        return said.ToString();
    }

    /// <summary>
    /// Its entry on the due list (LAND2b, design §8), where it has one: when it became due, and each try by its code, with the
    /// branch it made or met. A session never due has none, and says nothing here.
    /// </summary>
    private static void DueToLand(StringBuilder text, TraceDue due)
    {
        text.Append($"  due to land automatically since {When(due.Since)} ({AutoLandings.FileName}, this machine's)")
            .Append(due.Closed is { } closed ? $", closed {When(closed)}\n" : ", still waiting\n");
        if (due.Tries.Count == 0) text.Append("    not tried yet\n");
        foreach (var tried in due.Tries)
        {
            text.Append($"    {When(tried.At)} · {tried.Code}")
                .Append(tried.Branch is { } branch ? $" · branch {branch}" : "")
                .Append(tried.Commits is { } commits ? $" · {commits} commit(s)" : "")
                .Append(tried.Uncommitted is { } paths ? $" · {paths} uncommitted path(s)" : "")
                .Append(tried.Tip is { } tip ? $" · at {tip[..Math.Min(12, tip.Length)]}" : "")
                .Append('\n');
        }
    }

    /// <summary>
    /// What stood when a driven session on a quest started: the standing answer for its repository, each go-ahead on its ask,
    /// and how many of the person's words were said before it, each by the moments kept and nothing else.
    /// </summary>
    private static void Stood(StringBuilder text, TraceSessionLink session, TraceStood stood)
    {
        if (stood.Missing == TraceStoodGaps.NoMoment)
        {
            text.Append("  what stood when it started: its record says no moment it opened, so nothing is read against one\n");
            return;
        }

        text.Append("  what stood when it started:\n");
        if (stood.Standing is { } standing)
        {
            var named = $"    standing answer for {standing.Repository}";
            text.Append(standing.State switch
            {
                TraceStandings.None => $"{named}: none set now, and one cleared since is not kept\n",
                TraceStandings.MomentUnknown => $"{named}: set at a moment driver.json does not say, so whether it stood then is not known\n",
                TraceStandings.Before => $"{named}: set {When(standing.At!.Value)}, before it started:\n{Quoted(standing.Says ?? "", "      ")}",
                _ => $"{named}: set {When(standing.At!.Value)}, after it started; what stood before is not kept: driver.json keeps the latest\n",
            });
        }

        foreach (var goAhead in stood.GoAheads)
        {
            var mine = goAhead.Mine ? ", by this session" : "";
            text.Append($"    go-ahead {goAhead.Number}: ").Append(goAhead.State switch
            {
                TraceGoAheadStands.AskedAfter => $"first asked after it started{mine}",
                TraceGoAheadStands.Waiting => "waiting on the person when it started, and still",
                TraceGoAheadStands.ApprovedBefore => $"approved {When(goAhead.At!.Value)}, before it started",
                TraceGoAheadStands.RefusedBefore => $"refused {When(goAhead.At!.Value)}, before it started",
                _ => $"answered {When(goAhead.At!.Value)}, after it started; whether an earlier answer stood then is not kept: an answer replaces the one before",
            }).Append('\n');
        }

        if (stood.Words is { } words)
        {
            text.Append($"    the person's words on ask #{words.Ask}: {words.Before} of {words.Of} said before it started\n");
        }
    }

    /// <summary>What names a link nothing holds, as the sentence says it.</summary>
    private static string NamedBy(TraceNamedBy? by) => by switch
    {
        { Kind: TraceNamers.Quest } => $"quest #{by.Id}'s sender",
        { Kind: TraceNamers.Intake } => $"intake session {by.Id}'s record",
        { Kind: TraceNamers.Session } => $"session {by.Id}'s record",
        { Kind: TraceNamers.Landing } => $"the landing of session {by.Id}",
        _ => "the trace",
    };

    /// <summary>A record's state, with what its flags add to it.</summary>
    private static string StateOf(TraceState state) => state switch
    {
        { Limit: true } => "failed, by an account's limit",
        { Interrupted: true } => "stopped, not by the person: the driver closed, or the sweep found it orphaned",
        { Answered: true } => "awaiting-person, answered: it goes on at the driver's next look",
        { State: "" } => "state not recorded",
        _ => state.State,
    };

    /// <summary>What ran it, as its record names it: the adapter, the harness's version, the account; each said missing where unsaid.</summary>
    private static string Agent(TraceAgent agent)
    {
        var said = $"agent {agent.Adapter ?? "not recorded"} · {(agent.Harness is { } version ? $"harness {version}" : "harness not recorded")}";
        if (agent.Teammate) return said;
        return said + (agent.Account is { } account ? $" · account {account}" : " · account none named: the tool's own sign-in");
    }

    /// <summary>How a word was given (DRIFT1a's kinds), and to which session on which quest.</summary>
    private static string Given(AskWordView word)
    {
        var on = word.Quest is { Length: > 0 } quest ? $", on quest #{quest}" : "";
        var to = word.Session is { Length: > 0 } session ? session : "a session";
        return word.Kind switch
        {
            AskWordView.Asked => "asked",
            AskWordView.Answered => $"answered session {to}{on}",
            AskWordView.Added => $"added while session {to} ran{on}",
            AskWordView.Reopened => $"said to session {to} after it ended{on}",
            // A review's verdict (REVIEWENV1c): said to no session, and worded as the verdict it came with.
            AskWordView.Reviewed => $"reviewed what a set-up step showed{on}",
            AskWordView.NotYet => $"said not yet to what a set-up step showed{on}",
            AskWordView.Skipped => $"skipped the review of the work{on}",
            var kind => $"{kind}, to session {to}{on}",
        };
    }

    private static string Answered(GoAheadAnswerView? answer) => answer switch
    {
        null => "waiting on the person",
        _ => $"{(answer.Approved ? "approved" : "refused")} {When(answer.At)}{(answer.Words is { Length: > 0 } words ? $", saying: \"{words}\"" : "")}",
    };

    /// <summary>Someone's words, line by line beneath their item, as the instructions quote them.</summary>
    private static string Quoted(string words, string indent) =>
        string.Join("\n", words.ReplaceLineEndings("\n").Split('\n').Select(line => line.Length == 0 ? $"{indent}>" : $"{indent}> {line}")) + "\n";

    private static string OneLine(string text) => text.ReplaceLineEndings(" ").Trim();

    /// <summary>A moment as the instructions say it: to the minute, in UTC, the same on every machine.</summary>
    private static string When(DateTimeOffset at) => at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string WhenOr(DateTimeOffset? at) => at is { } moment ? When(moment) : "at a moment not recorded";

    private static string Short(string commit) => commit.Length > 8 ? commit[..8] : commit;

    private static string Count(int number) => number.ToString("N0", CultureInfo.InvariantCulture);
}
