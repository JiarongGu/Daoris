using System.Text;

namespace Daoris.Driver;

/// <summary>Which set-up a repository's line calls for (D117 §6.1, D124 §2.1).</summary>
public enum SetupCase
{
    /// <summary>Not adopted on its line: the doctrine taken up whole, with the knowledge (<see cref="SetupQuests.SetUp"/>).</summary>
    Whole,

    /// <summary>Adopted on the older layout, or on the agents layout with the move unfinished (<see cref="SetupQuests.Move"/>).</summary>
    Move,

    /// <summary>Adopted on the agents layout and clean, declaring nothing: the knowledge step alone (<see cref="SetupQuests.Declare"/>).</summary>
    Declare,

    /// <summary>Adopted on the agents layout, clean and declaring: already set up, refused.</summary>
    Done,
}

/// <summary>What the quest is composed from.</summary>
/// <param name="Day">The day of the press, which the title carries.</param>
/// <param name="Repository">The repository it is for, by its registered name.</param>
/// <param name="Facts">What its line holds (<see cref="LayoutReader"/>).</param>
/// <param name="Version">What the <c>daoris</c> a child of Daoris would find answered for its version.</param>
/// <param name="Description">What its README says of it, as the intake reads it (D77); null when it says nothing.</param>
/// <param name="Entries">The path of each entry the workspace's index holds for it, once per entry.</param>
/// <param name="Neighbours">The names the workspace knows its other repositories by.</param>
public sealed record SetupBriefInput(
    SetupCase Case, DateOnly Day, string Repository, LayoutFacts Facts, string Version,
    RepositoryDescription? Description, IReadOnlyList<string> Entries, IReadOnlyList<string> Neighbours);

/// <summary>
/// The set-up quest's words (LAYOUT7; D117 §6.2–§6.3 as D124 §2.2–§2.7 amends them): a title from
/// <see cref="SetupQuests"/> and a body that IS the adoption playbook, for a session that cannot read it.
/// </summary>
/// <remarks>
/// <para>🔴 <b>In the canon's words.</b> The body is read in a repository that may know nothing of Daoris: no decision
/// number, no row of its backlog, no path of Daoris's own, no machine path, only the layout's own file names and the
/// doctrine tool's verbs. <c>SetupBriefTests</c> holds each.</para>
///
/// <para>🔴 <b>A twin</b> (<c>.claude/knowledge/twins.md</c>, *the set-up brief and the adoption playbook*): the
/// playbook (<c>.claude/knowledge/adoption.md</c>) is Daoris's own document, and the body is it in other words.
/// <c>SetupBriefTests</c> reads the playbook's steps and holds that the whole set-up's body names each, so a step
/// added there fails until it is said here.</para>
///
/// <para><b>Every verb it asks for is one the press allows</b> (<see cref="SetupPress.Verbs"/>), exactly, since a
/// session on the protocol door cannot ask for another (D52).</para>
/// </remarks>
public static class SetupBrief
{
    /// <summary>How many names a list in the body shows before it says how many more.</summary>
    private const int Shown = 12;

    /// <summary>The title and the body, for the case the press found.</summary>
    /// <exception cref="ArgumentException">A repository already set up: there is nothing to ask.</exception>
    public static (string Title, string Body) Compose(SetupBriefInput input)
    {
        var stem = input.Case switch
        {
            SetupCase.Whole => SetupQuests.SetUp,
            SetupCase.Move => SetupQuests.Move,
            SetupCase.Declare => SetupQuests.Declare,
            _ => throw new ArgumentException("a repository already set up has no set-up to ask for", nameof(input)),
        };

        var body = new StringBuilder();
        Asked(body, input);
        Read(body, input);
        Steps(body, input);
        Bounds(body);
        Close(body, input);
        return (SetupQuests.Title(stem, input.Day), body.ToString().TrimEnd() + "\n");
    }

    private static void Asked(StringBuilder body, SetupBriefInput input)
    {
        var what = input.Case switch
        {
            SetupCase.Whole =>
                "Take up the shared doctrine in the layout every agent reads, and write down what this repository owns "
                + "and promises, so that a session here, or in a neighbouring repository, finds its answers in files "
                + "rather than asking a person.",
            SetupCase.Move =>
                "Move this repository's doctrine to the layout every agent reads"
                + (input.Facts.Declares ? "" : ", and write down what this repository owns and promises")
                + ", so that a session here, or in a neighbouring repository, finds its answers in files rather than "
                + "asking a person.",
            _ =>
                "Write down what this repository owns and promises, so that a session in a neighbouring repository finds "
                + "its answers here rather than asking a person. The doctrine is already taken up here, on the layout "
                + "every agent reads: this is the knowledge step alone.",
        };

        body.Append("**What is asked, and whose it is.** ").Append(what)
            .Append(" This is this repository's own change, made by its own session on its own branch: nothing was ")
            .Append("written here from outside, and nothing outside this tree is this session's to change.\n\n");
    }

    private static void Read(StringBuilder body, SetupBriefInput input)
    {
        var facts = input.Facts;
        body.Append("**What was read here**, from its line `").Append(facts.Line).Append("` at commit `")
            .Append(Short(facts.Commit)).Append("`:\n\n");

        body.Append("- `daoris.json`: ").Append(Manifest(facts)).Append('\n');
        body.Append("- `AGENTS.md`: ").Append(Instruction(facts.Agents, facts, "the doctrine's region")).Append('\n');
        body.Append("- `CLAUDE.md`: ").Append(Instruction(facts.Claude, facts, region: null)).Append('\n');
        body.Append("- `.claude/rules/`: ").Append(facts.Rules.Count == 0
            ? "nothing."
            : $"{Count(facts.Rules.Count, "file")}, {Names(facts.Rules)}: rules one agent alone reads.").Append('\n');
        body.Append("- `.claude/skills/`: ").Append(ClaudeSkills(facts)).Append('\n');
        if (facts.ClaudeKnowledge.Count > 0)
        {
            body.Append("- `.claude/knowledge/`: ").Append(Count(facts.ClaudeKnowledge.Count, "file")).Append(", ")
                .Append(Names(facts.ClaudeKnowledge)).Append(".\n");
        }

        body.Append("- `.agents/skills/`: ").Append(facts.AgentsSkills.Count == 0
            ? "nothing."
            : $"{Count(facts.AgentsSkills.Count, "skill")}, {Names(facts.AgentsSkills)}.").Append('\n');
        if (facts.AgentsKnowledge.Count > 0)
        {
            body.Append("- `.agents/knowledge/`: ").Append(Count(facts.AgentsKnowledge.Count, "file")).Append(", ")
                .Append(Names(facts.AgentsKnowledge)).Append(".\n");
        }

        body.Append("- Folders with an `AGENTS.md` of their own: ")
            .Append(facts.Rooms.Count == 0 ? "none." : Names(facts.Rooms) + ".").Append('\n');

        if (input.Description is { } said)
        {
            if (said.Summary is { } summary)
            {
                body.Append("- What it says of itself (`").Append(said.Source ?? "README.md")
                    .Append("`, as this machine's checkout holds it): ").Append(summary).Append('\n');
            }

            if (said.Stack.Count > 0) body.Append("- What it is built with: ").Append(string.Join(", ", said.Stack)).Append(".\n");
        }
        else
        {
            body.Append("- It says nothing of itself in a README or a package description.\n");
        }

        body.Append("- ").Append(Index(input.Entries)).Append('\n');
        body.Append("- ").Append(input.Neighbours.Count == 0
            ? "This workspace holds no other repository."
            : $"The workspace's other repositories, by the names it knows them by: {Names(input.Neighbours, everyOne: true)}.").Append("\n\n");
    }

    private static void Steps(StringBuilder body, SetupBriefInput input)
    {
        var facts = input.Facts;
        var steps = new List<string> { Tool(input.Version) };
        if (input.Case == SetupCase.Whole) steps.Add(TakeUp(facts));
        if (input.Case == SetupCase.Move) steps.Add(MoveLayout(facts));
        if (input.Case is SetupCase.Whole or SetupCase.Move)
        {
            steps.Add(Collisions(facts));
            steps.Add(Mechanism());
            steps.Add(Twins());
        }

        if (input.Case != SetupCase.Move || !facts.Declares) steps.Add(Knowledge(input));
        if (input.Case is SetupCase.Whole or SetupCase.Move)
        {
            steps.Add(Brief(facts));
            steps.Add(Documents(facts));
            steps.Add(SafeWork());
        }

        steps.Add(input.Case == SetupCase.Declare ? VerifyDeclared() : Verify());
        steps.Add(Commit());

        body.Append("**The steps**, in order.\n\n");
        for (var i = 0; i < steps.Count; i++) body.Append(i + 1).Append(". ").Append(steps[i]).Append('\n');
        body.Append('\n');
    }

    private static string Tool(string version) =>
        $"**The tool.** Run `daoris --version`. It prints `{version}`. If it prints anything else, or `daoris` is not "
        + "found, stop and decline: *the doctrine command could not run here*, with what it printed.";

    private static string TakeUp(LayoutFacts facts) =>
        "**Take up the doctrine.** Run `daoris init --harness agents`, and read what it prints: the packs, this "
        + "repository's own documents, and the records it seems to keep by role (`daoris analyze --json` says the same "
        + "for a machine to read). " + Moves(facts);

    private static string MoveLayout(LayoutFacts facts) =>
        "**Move to the agents layout.** In `daoris.json`, set `\"harness\": \"agents\"` and `\"target\": \".agents\"` "
        + "(`daoris analyze --json` says what the tool reads here). " + Moves(facts);

    /// <summary>The repository's own documents out of the old tiers, and what reads them by path.</summary>
    private static string Moves(LayoutFacts facts)
    {
        var left = facts.ClaudeKnowledge.Select(file => $".claude/knowledge/{file}")
            .Concat(facts.ClaudeSkills.Except(facts.Mirrors, StringComparer.Ordinal).Select(skill => $".claude/skills/{skill}/"))
            .ToList();
        return "Move this repository's own documents out of `.claude/knowledge/` and `.claude/skills/` with `git mv`, "
            + "into `.agents/knowledge/` and `.agents/skills/`"
            + (left.Count == 0 ? "" : $" (on the line: {Names(left)})")
            + ": the tool refuses while one is left there, since its index would stop listing it. Search the repository "
            + "for anything that reads `.claude/skills/` or `.claude/knowledge/` by path, a CI step, a hook, a script, and "
            + "name each in your close; changing one is not this set-up's.";
    }

    private static string Collisions(LayoutFacts facts)
    {
        var links = new List<string>();
        if (facts.Claude.IsLinkLike || facts.ClaudeSkillsRoot.State != InstructionState.Absent || facts.Agents.IsLinkLike)
        {
            var named = new List<string>();
            if (facts.Agents.IsLinkLike) named.Add("`AGENTS.md`");
            if (facts.Claude.IsLinkLike) named.Add("`CLAUDE.md`");
            if (facts.ClaudeSkillsRoot.State != InstructionState.Absent) named.Add("`.claude/skills`");
            links.Add($" On this line {string.Join(" and ", named)} {(named.Count == 1 ? "is" : "are")} not what the tool "
                + "writes: it never writes through a link or a link checked out as text. A real file holding `@AGENTS.md`, "
                + "and a folder of the copies the tool writes, work on every checkout; the choice is this repository's, and "
                + "your close says what you chose.");
        }

        return "**Read the collisions.** Run `daoris sync --dry-run`, and read every `COLLIDES`, `DRIFTED`, `LEFT BEHIND` "
            + "and `LINK` line. A collision is a file this repository wrote itself, before it took up the doctrine: "
            + "nothing is overwritten." + string.Concat(links);
    }

    private static string Mechanism() =>
        "**Keep the mechanism.** For each collision, keep its mechanism, the commands, paths, guards and policies "
        + "particular to here, in a knowledge document of this repository's own before taking the shared principle. "
        + "Then run `daoris sync`, and `daoris sync --force` only where you decided to take the shared version: a "
        + "deliberate answer to a question the tool asked, never a way past one. The core skill `set-up-documents` and "
        + "the knowledge `development-documents` are now in this tree: read both, since the steps after this follow them.";

    private static string Twins() =>
        "**Hunt renamed twins.** Read the generated index in `AGENTS.md` end to end for two rows that say the same thing: "
        + "a rule or document of this repository's own that a shared one now says under another name. `daoris doctor` "
        + "narrows the search and does not finish it. Retire this repository's copy only when every line of it is "
        + "somewhere else, and say where each went.";

    private static string Knowledge(SetupBriefInput input)
    {
        var own = input.Entries.Count == 0
            ? ""
            : $" The workspace's index already holds {Count(input.Entries.Count, "entry")} from this repository (above).";
        return "**Initialise the knowledge.** Write what a session in another repository would need from this one and "
            + "could not find: what it owns and where, what it promises and the shape of its data, and how the figures "
            + "others rely on are computed, each fact with the place in the code that holds it. Say which facts the code "
            + "did not confirm.\n"
            + "   - **The domain**, in `daoris.json`: `summary`, one line for someone who has never opened this repository; "
            + "`owns`, the areas where a change belongs here rather than anywhere else; `accepts`, the kinds of work worth "
            + "asking of it; `uses`, the repositories of this workspace its code depends on, by the names listed above, "
            + "where the code shows it (a client, a package reference, an address).\n"
            + "   - **Knowledge documents** of this repository's own, in `.agents/knowledge/`, from the knowledge template "
            + "beside the `set-up-documents` skill, one per area a neighbour would ask about: what it owns, and where (each "
            + "area of `owns`, and the folders and entry points that hold it); its contracts and data (what it exposes, "
            + "interfaces, endpoints, messages, files it writes, tables it owns; what it takes from whom; their shapes; and "
            + "which are promised and which incidental); and the computations others depend on (each figure, status or "
            + "rule another repository or a person reads from it, how it is derived, from which inputs, and where in the "
            + "code). In each, where every fact lives, a path and a symbol, so a reader can check it and a later session "
            + "can keep it true. Each document's `applies_when` names the question a neighbour would be asking.\n"
            + "   - **Not** a tour of the folders, anything a reader sees at a glance, the build commands (the brief and the "
            + "gates declaration hold them), or this repository's history (its decisions record, if it keeps one).\n"
            + "   - **Truth before coverage.** State a fact confirmed in the code, with its place. Write one that could not "
            + "be confirmed as not confirmed, with where it would be settled. Name the neighbour for one only a neighbour "
            + "knows. Publish no request to another repository from this set-up.\n"
            + "   - **Its own documents first.**" + own + " Read what this repository already keeps before writing: a "
            + "document that already answers is named in your close and not rewritten, and a new one says where the "
            + "older one is.";
    }

    private static string Brief(LayoutFacts facts)
    {
        var claude = facts.Claude is { State: InstructionState.File, OwnLines: > 0 } own
            ? $" `CLAUDE.md` holds {Count(own.OwnLines, "line")} of its own (above)."
            : "";
        var rules = facts.Rules.Count == 0
            ? ""
            : " `.claude/rules/` holds rules one agent alone reads: moving their text into `AGENTS.md`'s own part is "
              + "this repository's call, and your close says what was done.";
        return "**Write the brief**, this repository's own part of `AGENTS.md`, above the region, from the brief template "
            + "beside the `set-up-documents` skill. Move into it what every agent needs from `CLAUDE.md`'s own lines: some "
            + "agents read only `AGENTS.md`, and only so many bytes of it, so a rule left in `CLAUDE.md` never reaches "
            + "them. Leave `CLAUDE.md` holding the import and what only the agent that reads it needs." + claude + rules
            + " A line goes in the brief only if nearly every task here needs it and nothing in the code would tell a "
            + "reader. Moving is not trimming: every line that leaves the brief has a new home, and your close names it.";
    }

    private static string Documents(LayoutFacts facts) =>
        "**Declare the documents and the rooms.** Each record this repository keeps, by role (router, decisions, "
        + "backlog, archive, fixes, changelog, glossary, gates), with its path, in `daoris.json`'s `documents`. A folder "
        + "whose conventions, traps or checks differ from the rest gets an `AGENTS.md` of its own, from the room template, "
        + "and is listed in `daoris.json`'s `rooms`"
        + (facts.Rooms.Count == 0 ? "" : $"; these folders hold one already: {Names(facts.Rooms)}")
        + ". The knowledge documents need no declaring: the index lists them from the files.";

    private static string SafeWork() =>
        "**Declare the safe work**: what this repository's sessions may run without asking, the checks that are a "
        + "session's to run, the build and test commands, the install from the lockfile. Exact commands, one per entry, "
        + "in `daoris.gates.json`'s `safe`, beside `gates`, and the file where there is none. Never in the brief's prose: "
        + "a sentence shapes what an agent tries, not what it is allowed. Nothing is widened by writing it: the person "
        + "accepts it once, after it lands, and your close says it waits for them.";

    private static string Verify() =>
        "**Verify.** Run `daoris sync`, `daoris check` and `daoris status --json`, then this repository's own build and "
        + "tests where they are a session's to run. `daoris check` reports the always-loaded budget and never fails on "
        + "it: trim a long rule of this repository's own that repeats a shared one, or raise the budget to the true "
        + "number, and say which.";

    private static string VerifyDeclared() =>
        "**Verify.** Run `daoris check` and `daoris status --json`, then this repository's own build and tests where "
        + "they are a session's to run.";

    private static string Commit() =>
        "**Commit** on this branch as each step lands: the branch is what is reviewed, and this session never pushes it.";

    private static void Bounds(StringBuilder body) =>
        body.Append("**What this writes, and what it never does.** Write only in this tree, on this branch: `daoris.json` ")
            .Append("and `daoris.lock`; `AGENTS.md` and `CLAUDE.md` (the region, the brief, the import); `.agents/`, with the ")
            .Append("shared documents and this repository's own knowledge; the copies under `.claude/skills/` the tool ")
            .Append("writes; each room's `AGENTS.md`; the `safe` section of `daoris.gates.json`; and a document that keeps a ")
            .Append("collision's mechanism. Never push, merge or open a pull request; never write outside this tree (another ")
            .Append("repository's checkout you may read is read only); never publish a request to another repository; never ")
            .Append("run `daoris connect` or `daoris upstream`; never set `remote.join` or `remote.knowledge` in `daoris.json`, ")
            .Append("since what may leave this machine is the person's call and silence keeps it here; and never change a ")
            .Append("source, build or CI file: anything that needs one is named in your close, as work for a request the ")
            .Append("person may publish.\n\n");

    private static void Close(StringBuilder body, SetupBriefInput input)
    {
        var items = input.Case == SetupCase.Declare
            ? "the tool's version; the domain, in its words; each knowledge document, the question it answers, and each fact "
              + "marked not confirmed; and what you found that needs a request elsewhere"
            : "the tool's version; each collision and how it was resolved; each twin retired and where its lines went; "
              + (input.Case != SetupCase.Move || !input.Facts.Declares
                  ? "the domain, in its words; each knowledge document, the question it answers, and each fact marked not confirmed; "
                  : "")
              + "what the brief took in and where each line it let go now lives; the documents, rooms and safe work declared, "
              + "and that the safe work waits for the person's yes; the budget `daoris check` reports and the root `AGENTS.md`'s "
              + "bytes; what was chosen about links; and what you found that needs a request elsewhere";
        body.Append("**How to close it.** Close it `done`, saying: ").Append(items)
            .Append(". Or decline, with the reason, *the doctrine command could not run here* among them.\n");
    }

    private static string Manifest(LayoutFacts facts)
    {
        if (!facts.Adopted) return "absent, so the doctrine is not taken up here.";
        if (!facts.ManifestReads) return "present, and it does not read as JSON.";

        var layout = facts.ManifestHarness == AgentLayout.Agents ? "the agents layout" : "the older `.claude` layout";
        var locked = !facts.Locked
            ? "; no `daoris.lock`, so the tool has not synced here"
            : facts.LockHarness is { } written && written != facts.ManifestHarness
                ? $"; its lock is still on `{written}`, so the files have not moved"
                : "";
        return $"on {layout}{locked}; it {(facts.Declares ? "declares what this repository owns" : "declares nothing yet")}.";
    }

    private static string Instruction(InstructionFile file, LayoutFacts facts, string? region) => file.State switch
    {
        InstructionState.Absent => "absent.",
        InstructionState.Link => $"a link to `{file.Target}`, and "
            + (facts.LinksHeldAsText
                ? "this checkout holds links as text (`core.symlinks=false`), so an agent here reads the path, not instructions"
                : "this checkout holds links as links, so an agent here reads what it points at; on a checkout without links it would read the path")
            + "; a real file holding `@AGENTS.md` works on every checkout, and the choice is this repository's.",
        InstructionState.HeldAsText => $"a file holding only the path `{file.Target}`: a link checked out as text and "
            + "committed so, which an agent reads as a path, not instructions; a real file holding `@AGENTS.md` works on "
            + "every checkout, and the choice is this repository's.",
        _ when region is not null => file.OwnLines == 0
            ? file.Region ? $"{region} alone." : "present, with nothing written in it."
            : $"{Count(file.OwnLines, "line")} of its own{(file.Region ? $", and {region}" : "")}.",
        _ => (file.OwnLines, file.Imports) switch
        {
            (0, true) => "the import of `AGENTS.md` alone.",
            (0, false) => "present, with nothing written in it.",
            (var lines, true) => $"{Count(lines, "line")} of its own, and it imports `AGENTS.md`.",
            (var lines, false) => $"{Count(lines, "line")} of its own, and no import of `AGENTS.md`: an agent that reads "
                + "only `AGENTS.md` never sees them.",
        },
    };

    private static string ClaudeSkills(LayoutFacts facts)
    {
        var root = facts.ClaudeSkillsRoot;
        if (root.State == InstructionState.Link)
        {
            return $"a link to `{root.Target}`"
                + (facts.LinksHeldAsText ? ", which this checkout holds as text (`core.symlinks=false`)." : ".");
        }

        if (root.State == InstructionState.HeldAsText) return $"a file holding only the path `{root.Target}`, where a folder must go.";
        if (root.State == InstructionState.File) return "a file, where a folder must go.";
        if (facts.ClaudeSkills.Count == 0) return "nothing.";

        var mirrors = facts.Mirrors.Count == 0 ? "" : $"; {Names(facts.Mirrors)} {(facts.Mirrors.Count == 1 ? "is a copy" : "are copies")} the lock records";
        return $"{Count(facts.ClaudeSkills.Count, "skill")}, {Names(facts.ClaudeSkills)}{mirrors}.";
    }

    /// <summary>The index's entries for it, counted by the file each came from, most first.</summary>
    private static string Index(IReadOnlyList<string> entries)
    {
        if (entries.Count == 0) return "The workspace's index holds nothing for it yet.";
        var files = entries.GroupBy(path => path, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"`{group.Key}` ({group.Count()})")
            .ToList();
        var shown = files.Count > Shown ? string.Join(", ", files.Take(Shown)) + $", and {files.Count - Shown} more" : string.Join(", ", files);
        return $"The workspace's index holds {Count(entries.Count, "entry")} for it, from {Count(files.Count, "file")}: {shown}.";
    }

    /// <summary>Names in code type, at most <see cref="Shown"/> of them unless every one is asked for.</summary>
    private static string Names(IReadOnlyList<string> names, bool everyOne = false)
    {
        var quoted = names.Select(name => $"`{name}`").ToList();
        return everyOne || quoted.Count <= Shown
            ? string.Join(", ", quoted)
            : string.Join(", ", quoted.Take(Shown)) + $", and {quoted.Count - Shown} more";
    }

    private static string Count(int count, string noun) =>
        count == 1 ? $"1 {noun}" : $"{count} {(noun.EndsWith('y') ? noun[..^1] + "ies" : noun + "s")}";

    private static string Short(string commit) => commit.Length > 12 ? commit[..12] : commit;
}
