using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The index quest's words (ORIENT2c; the orientation design §2, D151 point 5): <see cref="SetupQuests.Index"/> and a body
/// that asks a set-up repository's own session for a generated index of where things are, kept true by its own check.
/// </summary>
/// <remarks>
/// <para>🔴 <b>In the canon's words</b>, as <see cref="SetupBrief"/>'s are: read in a repository that may know nothing of
/// Daoris, so no decision number, no row of a backlog, no path of Daoris's own and no machine path. <c>SetupBriefTests</c>
/// holds it with the other cases.</para>
///
/// <para><b>Inventory, generator, check, marks, declaration, and nothing else</b> (the design §2.2's bounds): an index the
/// repository already keeps stays whole and where it is (§2.3), and the generator is the one the press named, at its path,
/// with exactly the two commands it grants (<see cref="SetupPressRules.IndexRules"/>).</para>
///
/// <para><b>What driven sessions read most is not carried</b>: the design's §2.2 has the press count it from their typed
/// events where there are any; this body leaves the tables to the session's reading of the code, which §2.2 allows.</para>
/// </remarks>
public static class IndexBrief
{
    /// <summary>The title and the body for a repository set up and naming no index.</summary>
    /// <exception cref="ArgumentException">Any other case, or no generator named.</exception>
    public static (string Title, string Body) Compose(SetupBriefInput input)
    {
        if (input.Case != SetupCase.Index) throw new ArgumentException("the index quest is for a repository set up and naming no index", nameof(input));
        var form = input.Index ?? throw new ArgumentException("the index quest names its generator", nameof(input));

        var body = new StringBuilder();
        body.Append("**What is asked, and whose it is.** Give this repository an index of where things are: a folder, ")
            .Append("generated and committed, whose rows each name a file and its lines for something a session looks for ")
            .Append("here, so a session opens a row and reads the lines it names rather than searching and reading files whole. ")
            .Append("It is written and checked by a generator in this repository's own toolchain, so a clone with nothing ")
            .Append("else installed reads it, keeps it true and passes its checks. This is this repository's own change, made ")
            .Append("by its own session on its own branch: nothing was written here from outside, and nothing outside this ")
            .Append("tree is this session's to change.\n\n");

        Read(body, input, form);
        Steps(body, input, form);
        Bounds(body, form);
        body.Append("**How to close it.** Close it `done`, saying: the tool's version; each index this repository already ")
            .Append("kept, what it answers, and how the generated one differs from it; the generator's path, its two commands ")
            .Append("and where its check runs; the folder, each file in it and what it answers; this repository's checks before ")
            .Append("and after, with the index's own check among them; and what you found that needs a request elsewhere. Or ")
            .Append("decline, with the reason, *the doctrine command could not run here* or *the generator's toolchain cannot ")
            .Append("run here* among them.\n");
        return (SetupQuests.Title(SetupQuests.Index, input.Day), body.ToString().TrimEnd() + "\n");
    }

    private static void Read(StringBuilder body, SetupBriefInput input, IndexForm form)
    {
        var facts = input.Facts;
        body.Append("**What was read here**, from its line `").Append(facts.Line).Append("` at commit `")
            .Append(SetupBrief.Short(facts.Commit)).Append("`:\n\n");
        body.Append("- `daoris.json`: the doctrine is taken up on the agents layout, it declares what this repository owns, ")
            .Append("and its `documents` name no `index`.\n");
        body.Append("- Its router: ").Append(facts.DeclaredRouter is { } router
            ? $"`{router}`, declared as `documents.router`."
            : "none declared.").Append('\n');
        body.Append("- Files whose name says they are an index: ").Append(facts.IndexNamed.Count == 0
            ? "none."
            : SetupBrief.Names(facts.IndexNamed) + ".").Append('\n');
        body.Append("- `.agents/skills/`: ").Append(facts.AgentsSkills.Count == 0
            ? "nothing."
            : $"{SetupBrief.Count(facts.AgentsSkills.Count, "skill")}, {SetupBrief.Names(facts.AgentsSkills)}: one of this "
              + "repository's own may route a session to where things are.").Append('\n');
        if (input.Description is { Stack.Count: > 0 } said)
        {
            body.Append("- What it is built with: ").Append(string.Join(", ", said.Stack)).Append(".\n");
        }

        body.Append("- The generator this asks for: `").Append(form.Generator).Append("`, in ").Append(form.Stack)
            .Append(", which is not on the line yet.\n\n");
    }

    private static void Steps(StringBuilder body, SetupBriefInput input, IndexForm form)
    {
        var dotnet = form.Stack == ".NET"
            ? " It is a file-based program, which needs no project file and a .NET SDK that runs one (10 or later): where the "
              + "SDK here cannot, stop and decline: *the generator's toolchain cannot run here*, with what it printed."
            : "";
        string[] steps =
        [
            SetupBrief.Tool(input.Version),
            "**The inventory first.** List every index this repository already keeps, written by hand or generated: its "
            + "router, files named as indexes, a routing skill of its own, a code map, an architecture page, each with what "
            + "it answers. Keep each as and where it is: nothing is moved, rewritten or deleted. One that is generated and "
            + "checked already is the index: declare it (below), write no generator, and say in your close what it lacks. One "
            + "that is generated with no check stays as it is, with no second index beside it, and your close says that "
            + "nothing keeps it true. Then run this repository's own checks, where they are a session's to run, before the "
            + "first change, and note which were green.",
            $"**The generator**, at `{form.Generator}`, in {form.Stack}, from the index template beside the "
            + "`set-up-documents` skill where it is there. Its two commands are exactly these: `" + form.Write + "` writes "
            + "the folder, and `" + form.Check + "` builds it in memory and fails, naming each missing, stale or stray file "
            + "and the command that writes it again. It needs nothing this repository does not already use, reads the files "
            + "git would stage (tracked, and untracked but not ignored) with LF endings, sorts by code unit and never by "
            + "locale, makes no network call and finishes in seconds, so one tree writes the same bytes on every machine."
            + dotnet + "\n"
            + "   - **The folder** is this repository's choice, beside its other documents, and never one that holds files "
            + "already.\n"
            + "   - **Its README** says what each file answers and the two commands; lists first, under *Kept by hand*, each "
            + "index the inventory found, with what it answers and none of its rows restated; links the knowledge's own "
            + "index without copying it; and lists each file over the outline threshold, with its size and lines.\n"
            + "   - **An outline** of each file too long to read whole (a threshold of this repository's choosing, about 40 "
            + "KB): its declarations or headings, each with its lines.\n"
            + "   - **A digest of the decisions**, where this repository keeps a decisions record: each decision's title and "
            + "lines, then each dated amendment's label and lines.\n"
            + "   - **Up to four tables** of what this repository is reached through: entry points (commands, routes, "
            + "handlers, jobs), names that cross files (configuration keys, message types, catalogue keys), and test "
            + "fixtures and helpers, each row a file and a line. Status, prose and a tour are not rows.",
            "**The check**, inside the command that means done here: a test file the test runner already discovers, where "
            + "it can, so no build or CI file changes; otherwise its one entry in that command.",
            "**The marks.** In `.gitattributes`, the folder marked `-merge`, so git never merges it as text, and generated "
            + "for review where the code host reads such a mark (`linguist-generated`). The README's first line says that a "
            + "conflict in the folder is resolved by running `" + form.Write + "` again, never by hand.",
            "**The declaration.** In `daoris.json`'s `documents`, `index` names the README's path. Then run `daoris sync`, "
            + "which renders its row in the brief's *Where things are*, and keeps the rest of `AGENTS.md` as it is.",
            "**Verify.** Run `" + form.Check + "`, then `daoris check` and `daoris status --json`, then this repository's own "
            + "checks: green where they were green before, the index's own check among them.",
            SetupBrief.Commit(),
        ];

        body.Append("**The steps**, in order.\n\n");
        for (var i = 0; i < steps.Length; i++) body.Append(i + 1).Append(". ").Append(steps[i]).Append('\n');
        body.Append('\n');
    }

    private static void Bounds(StringBuilder body, IndexForm form) =>
        body.Append("**What this writes, and what it never does.** Write only in this tree, on this branch: the generator at `")
            .Append(form.Generator).Append("`; the index's folder; the check's test file, or its one entry in the command ")
            .Append("that means done; the folder's lines in `.gitattributes`; `documents.index` in `daoris.json`; and what ")
            .Append("`daoris sync` writes. Never move, rewrite or delete an index this repository already keeps, or a routing ")
            .Append("skill of its own; never change another source, build or CI file, or add frontmatter to a file; never push, ")
            .Append("merge or open a pull request; never write outside this tree (another repository's checkout you may read is ")
            .Append("read only); never publish a request to another repository; and never run `daoris connect` or `daoris ")
            .Append("upstream`. Anything that needs more is named in your close, as work for a request the person may publish.\n\n");
}
