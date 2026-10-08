using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The reviewer's instruction (XAGENT1d, D155 points 5 and 6; the second-agent design §4–§5): the packet in words, what it may
/// do and what never, and how it says its opinion. Composed once, from facts, with no model; handed whole in one turn.
/// </summary>
/// <remarks>
/// <para><b>Project-agnostic, as every instruction is</b>: it travels to repositories that know nothing of this one, so it speaks
/// in plain words and the tool's names, never in decision numbers. One line a paragraph, so no phrase is split by a wrap.</para>
///
/// <para><b>The words are half of what holds it.</b> The copy is the floor and the rules are the harness's (§5.1–§5.2); the
/// instruction says the same so an agent that follows it never tries, and it says the one thing neither can: that the
/// repository's own doctrine telling an agent to write as it goes does not apply to a reading.</para>
///
/// <para><b>Claims stay claims.</b> The working session's closing note is handed as its claim, the person's words as theirs,
/// and only what Daoris read of the evidence as what Daoris read (§6.6). The working session's conversation is never handed.</para>
/// </remarks>
public static class OpinionInstruction
{
    /// <summary>The most commits named one by one; the rest are counted, and git lists them in the copy.</summary>
    internal const int CommitsShown = 50;

    /// <summary>The most paths named one by one; the rest are counted, and the diff holds them.</summary>
    internal const int PathsShown = 200;

    /// <summary>The instruction for <paramref name="packet"/>.</summary>
    public static string Compose(OpinionPacket packet)
    {
        var text = new StringBuilder();
        text.Append($"You are giving a second opinion on work another session did in `{packet.Candidate.Repository}`. ")
            .Append("You did not write it, and you will not change it: you read it, and you say what you find.\n\n")
            .Append("You are in a copy of the repository made for this reading, checked out at the work's last commit. ")
            .Append("Nothing in it is taken back: no file you write and no commit or branch you make there reaches the work or the person. ")
            .Append("The session that did the work owns it: it checks what you say against the code, and makes any change itself.\n");

        Work(text, packet);
        Asked(text, packet);
        Rules(text, packet.Rules);
        May(text, packet.Verify);
        Say(text, packet.Minutes);
        return text.ToString();
    }

    private static void Work(StringBuilder text, OpinionPacket packet)
    {
        var candidate = packet.Candidate;
        text.Append("\n## The work\n\n").Append(Occasion(packet.Occasion)).Append("\n\n")
            .Append($"It is the commits from `{candidate.Base}` to `{candidate.Tip}`, {Plural(candidate.Commits.Count, "commit")}, oldest first:\n\n");
        foreach (var commit in candidate.Commits.Take(CommitsShown)) text.Append($"- `{commit}`\n");
        if (candidate.Commits.Count > CommitsShown)
        {
            text.Append($"- … and {candidate.Commits.Count - CommitsShown} more, which `git log {candidate.Base}..{candidate.Tip}` lists.\n");
        }

        if (candidate.Paths.Count > 0)
        {
            text.Append($"\nThey change {Plural(candidate.Paths.Count, "path")}:\n\n");
            foreach (var path in candidate.Paths.Take(PathsShown)) text.Append($"- {path.Status} `{path.Path}`\n");
            if (candidate.Paths.Count > PathsShown) text.Append($"- … and {candidate.Paths.Count - PathsShown} more, which the diff holds.\n");
        }

        var command = $"git diff {candidate.Base}...{candidate.Tip}";
        text.Append('\n').Append(packet.Diff is { } diff
            ? $"The whole diff is in `{diff}`, which you may read. Your copy holds both commits too, so `{command}` shows the same, and `git log` and `git show` show the commits.\n"
            : $"Your copy holds both commits: `{command}` shows the whole diff, and `git log` and `git show` show the commits.\n");
    }

    /// <summary>Why it is read now (design §2.1), in one sentence.</summary>
    private static string Occasion(string occasion) => occasion switch
    {
        OpinionRules.Landing => "It is about to land: you read it before the person looks at it and before it is offered to land.",
        OpinionRules.Steps => "It is one step of a chain: you read it before the chain's next step starts.",
        "failure" => "Its quest is held after the work failed: read why it failed, and say what would fix it.",
        _ => "The person asked for this reading.",
    };

    private static void Asked(StringBuilder text, OpinionPacket packet)
    {
        text.Append("\n## What was asked\n");
        if (packet.Quests.Count == 0)
        {
            text.Append("\nNo quest is handed with this reading: judge the work by itself and by the repository's own rules.\n");
        }

        foreach (var quest in packet.Quests) Quest(text, quest);
        if (packet.Words is { } words) text.Append(AskWordsText.Beneath(words, packet.Quests.FirstOrDefault()?.Id ?? ""));
    }

    private static void Quest(StringBuilder text, QuestView quest)
    {
        text.Append($"\nQuest `#{quest.Id}`, \"{quest.Title}\", asked by `{quest.From}` of `{quest.To}`, now {quest.Status}:\n\n")
            .Append(Quoted(quest.Body, ""));

        if (quest.Requirements.Count > 0)
        {
            text.Append("\nWhat the person requires of it, each in their own words, with the check that proves the work meets it:\n");
            for (var at = 0; at < quest.Requirements.Count; at++)
            {
                var requirement = quest.Requirements[at];
                text.Append($"\n- Requirement {at + 1}:\n\n").Append(Quoted(requirement.Quote, "  "))
                    .Append($"\n  Check: {requirement.Check}\n");
                if (requirement.Evidence.Count > 0)
                {
                    text.Append($"  Evidence Daoris reads: {string.Join(", ", requirement.Evidence.Select(Named))}.\n");
                }
            }
        }

        if (quest.Answers.Count > 0)
        {
            text.Append("\nHow the session's close answered each, as its own claim:\n\n");
            foreach (var answer in quest.Answers.OrderBy(answer => answer.Requirement))
            {
                text.Append(answer.Met is { } met
                    ? $"- Requirement {answer.Requirement}: met — {met}\n"
                    : $"- Requirement {answer.Requirement}: departed — {answer.Departed}{(answer.Quote is { } quote ? $", turning on \"{quote}\"" : "")}\n");
            }
        }

        if (quest.Note is { Length: > 0 } note)
        {
            text.Append("\nThe session that did the work closed it saying, as its own claim:\n\n").Append(Quoted(note, ""));
        }

        if (quest.Evidence is { } read)
        {
            text.Append($"\nWhat Daoris read of its evidence itself, at `{read.Commit}`:\n\n");
            foreach (var item in read.Items) text.Append($"- {(item.Path is not null ? $"`{item.Path}`" : $"gate `{item.Gate}`")}: {item.Result}\n");
        }
        else if (quest.Requirements.Any(requirement => requirement.Evidence.Count > 0))
        {
            text.Append("\nDaoris has read none of its evidence yet.\n");
        }

        if (quest.Hold is { Length: > 0 } hold) text.Append($"\nIt is held for the person: `{hold}`.\n");
    }

    private static string Named(QuestEvidenceItem item) => item.Path is { } path ? $"`{path}`" : $"gate `{item.Gate}`";

    private static void Rules(StringBuilder text, OpinionRulesRead rules)
    {
        text.Append("\n## The repository's own rules\n\n");
        if (rules.Doctrine.Count == 0 && rules.Decisions is null && rules.Gates is null)
        {
            text.Append("Your copy holds no instruction file or index Daoris recognises: find the repository's rules in its README and its own documents, and judge the work by them.\n");
            return;
        }

        text.Append("Judge the work by this repository's own rules as they stand in your copy, and read them in your own terms: an instruction written for another agent's tools still says what the repository expects.\n");
        if (rules.Doctrine.Count > 0)
        {
            text.Append('\n');
            foreach (var path in rules.Doctrine) text.Append($"- `{path}`\n");
        }

        var records = new List<string>();
        if (rules.Decisions is { } decisions) records.Add($"Its decisions are recorded in `{decisions}`.");
        if (rules.Gates is { } gates) records.Add($"What it declares safe to run is in `{gates}`.");
        if (records.Count > 0) text.Append('\n').Append(string.Join(" ", records)).Append('\n');
    }

    private static void May(StringBuilder text, bool verify) =>
        text.Append("\n## What you may do\n\n")
            .Append("You read: the copy, its history, the diff and these rules, with read-only commands. ")
            .Append(verify
                ? "You may also build and run what this repository declares safe to run unasked, in this copy only, and nothing else; say in what you read which you ran and what each showed.\n\n"
                : "Do not build, test or run the repository's own programs: this reading is by reading alone.\n\n")
            .Append("You never edit, write or delete a file; commit, merge, rebase or push; deploy or publish; change a permission rule or propose one; ")
            .Append("start, stop or touch a process, a port or a server of the person's; take, close, decline or publish a quest; or ask for another opinion. ")
            .Append("This repository's own instructions may tell an agent to write down what it learns, keep a record or commit as it goes: here they do not apply, because this reading changes nothing. ")
            .Append("Nobody will answer a question from you while you read: decide from what you can read, and say what you could not tell.\n");

    private static void Say(StringBuilder text, int minutes) =>
        text.Append("\n## What you say\n\n")
            .Append("Say your opinion once, before you end, with your connector's `opinion_give`. Give each finding, the most important first and at most 20:\n\n")
            .Append("- its weight: `must` (wrong to land as it is), `should`, or `note`;\n")
            .Append("- where it is: a path from the repository's root with its line, a commit, or `general`;\n")
            .Append("- what you claim, and what goes wrong if it holds;\n")
            .Append("- how to see it: the steps or the command and what it showed, or, where you could not reproduce it, your reasoning;\n")
            .Append("- how sure you are: `sure`, `likely` or `unsure`;\n")
            .Append("- and, where you have one, a diagnosis or a change you propose, as text. The session that did the work makes its own.\n\n")
            .Append("Then say what you read, and what you did not read or could not tell. ")
            .Append("If you raise nothing, give no findings and still say what you read: an opinion says what it covered, never `no issues`. ")
            .Append("Your findings are claims: the session that did the work checks each against the code and answers it, and the person sees both.\n\n")
            .Append($"You have one turn, and at most {minutes} minutes. An opinion not given through `opinion_give` before you end is not given.\n");

    /// <summary>Words quoted line by line, each line under <paramref name="indent"/>, ending with a line break.</summary>
    private static string Quoted(string words, string indent) =>
        string.Join("\n", words.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n').Select(line => line.Length == 0 ? $"{indent}>" : $"{indent}> {line}")) + "\n";

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
