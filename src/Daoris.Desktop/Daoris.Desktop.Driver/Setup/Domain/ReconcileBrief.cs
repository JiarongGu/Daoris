using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The reconcile quest's words (the context-one design §3 B): <see cref="SetupQuests.Reconcile"/> and a body that asks
/// an adopted repository's own session to run the doctrine tool's brief and decide each item it lists, with the person's
/// standing answer for it on this machine among the items.
/// </summary>
/// <remarks>
/// <para>🔴 <b>In the canon's words</b>, as <see cref="SetupBrief"/>'s are: no decision number, no path of Daoris's own, no
/// machine path. The items themselves are the tool's to list (<c>daoris reconcile</c>): the driver keeps no copy of its
/// table, and adds only what the tool cannot read, the machine's items.</para>
///
/// <para><b>The machine's items are the standing answer's lines</b>, each quoted, for the session to judge: a line that states
/// a rule about the repository's own development moves into its records, and one that is the person's choice for this
/// machine stays theirs.</para>
/// </remarks>
public static class ReconcileBrief
{
    /// <summary>The title and the body for an adopted repository asked to reconcile.</summary>
    /// <exception cref="ArgumentException">Any other case.</exception>
    public static (string Title, string Body) Compose(SetupBriefInput input)
    {
        if (input.Case != SetupCase.Reconcile) throw new ArgumentException("the reconcile quest is for an adopted repository", nameof(input));

        var body = new StringBuilder();
        body.Append("**What is asked, and whose it is.** Reconcile this repository with the shared doctrine: where the canon ")
            .Append("this machine's doctrine tool carries differs from what this repository holds, decide each difference here, ")
            .Append("adopting the canon's text, keeping this repository's own with its reason, or proposing that text upstream, ")
            .Append("so that what every session here reads is what this repository chose. This is this repository's own change, ")
            .Append("made by its own session on its own branch: nothing was written here from outside, and nothing outside this ")
            .Append("tree is this session's to change.\n\n");

        var facts = input.Facts;
        body.Append("**What was read here**, from its line `").Append(facts.Line).Append("` at commit `")
            .Append(SetupBrief.Short(facts.Commit)).Append("`: the doctrine is taken up and synced. ")
            .Append(input.Doctrine is { } doctrine
                ? $"This machine's doctrine tool, asked `daoris status --json` before this request, said {StatusSaid(doctrine)} The brief is still the "
                  + "tool's answer to read: "
                : "Whether the canon has moved since is the doctrine tool's answer, which this request did not read first: ")
            .Append("if `daoris reconcile` finds nothing, and no line below is this repository's to keep, close it `done`, saying so.\n\n");

        var lines = StandingLines(input.Standing);
        string[] steps =
        [
            SetupBrief.Tool(input.Version),
            "**The checks first.** Run this repository's own checks, where they are a session's to run, before the first "
            + "change, `daoris check` among them, and note which were green.",
            "**The brief.** Run `daoris reconcile`, and read every item it lists: what a sync would change or add, the copies "
            + "edited here, a keep the canon moved past, and a gap in this repository's set-up. It lists only differences, and "
            + "says for each where the canon's text is and the line that keeps it.",
            "**Decide each item**, as the brief says.\n"
            + "   - **Adopt**: the canon's text. Once every item is decided, `daoris sync` writes each adopted file and passes over "
            + "every kept one; `daoris sync --force` only where an adopted file's edit here is to be discarded, a deliberate "
            + "answer, never a way past a question.\n"
            + "   - **Keep, with its reason**: the item's line in `daoris.json`'s `kept`, the reason in this repository's words.\n"
            + "   - **Upstream**: `daoris upstream --all` composes the request's words for every edited file, and writes nothing "
            + "outside this tree; your close carries the words for the ones proposed, and each is kept meanwhile, with that "
            + "reason.",
            .. lines.Count == 0 ? Array.Empty<string>() : [MachineItems(lines)],
            "**Verify.** Run `daoris sync`, `daoris check` and `daoris status --json`, then this repository's own checks: green "
            + "where they were green before. One green before and red after means this does not close `done`; where its fix is "
            + "outside the items, stop and say so.",
            SetupBrief.Commit(),
        ];

        body.Append("**The steps**, in order.\n\n");
        for (var i = 0; i < steps.Length; i++) body.Append(i + 1).Append(". ").Append(steps[i]).Append('\n');
        body.Append('\n');

        body.Append("**What this writes, and what it never does.** Write only in this tree, on this branch: `daoris.json`'s ")
            .Append("`kept`, and `daoris.lock` and what `daoris sync` writes; the brief, a knowledge document or a record that ")
            .Append("takes a line of the machine's answer; and what an item needs in this repository's own documents to keep its ")
            .Append("checks green. Never push, merge or open a pull request; never write outside this tree (another repository's ")
            .Append("checkout you may read is read only); never publish a request to another repository, since a proposal ")
            .Append("upstream is words in your close for the person to send; never run `daoris connect`; never set `remote.join` ")
            .Append("or `remote.knowledge` in `daoris.json`; and never change a source, build or CI file: anything that needs one ")
            .Append("is named in your close, as work for a request the person may publish.\n\n");

        body.Append("**How to close it.** Close it `done`, saying: the tool's version; the checks before and after; each item ")
            .Append("`daoris reconcile` listed and what was decided, adopted, kept with its reason, or proposed upstream with the ")
            .Append("request's words")
            .Append(lines.Count == 0 ? "" : "; each line of the machine's answer, and the record it went to or that it stays the person's")
            .Append("; and what you found that needs a request elsewhere. Or decline, with the reason, *the doctrine command could ")
            .Append("not run here* among them.\n");
        return (SetupQuests.Title(SetupQuests.Reconcile, input.Day), body.ToString().TrimEnd() + "\n");
    }

    /// <summary>The status's own verdicts in a sentence: how the canons stand, what a sync would do, and the keeps the canon moved past.</summary>
    private static string StatusSaid(DoctrineStatus status)
    {
        var said = new List<string>
        {
            status.Update switch
            {
                DoctrineUpdates.Behind => $"its canon, `{status.Carried}`, is newer than the `{status.Locked}` this repository synced from",
                DoctrineUpdates.Differs => $"its canon differs from the one this repository synced from at the same version, `{status.Carried}`, "
                    + "so which is newer only the text can say",
                _ => "the canon has not moved since this repository synced",
            },
        };

        var moves = new[] { ("change", status.Changed), ("add", status.Added), ("retire", status.Retired) }.Where(each => each.Item2 > 0).ToList();
        if (moves.Count > 0)
        {
            var words = moves.Select((each, i) => i == 0 ? $"{each.Item1} {each.Item2} {(each.Item2 == 1 ? "file" : "files")}" : $"{each.Item1} {each.Item2}").ToList();
            said.Add("a sync would " + (words.Count == 1 ? words[0] : $"{string.Join(", ", words.SkipLast(1))} and {words[^1]}"));
        }

        if (status.Reopened > 0)
        {
            said.Add($"{status.Reopened} {(status.Reopened == 1 ? "keep was" : "keeps were")} judged against a canon text that has moved since");
        }

        return string.Join("; ", said) + ".";
    }

    /// <summary>The standing answer's lines, trimmed, the blank ones left out.</summary>
    private static List<string> StandingLines(StandingAnswer? standing) =>
        standing is null
            ? []
            : [.. standing.Says.ReplaceLineEndings("\n").Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0)];

    private static string MachineItems(IReadOnlyList<string> lines) =>
        "**The machine's items.** The person told this machine these lines hold for every quest here, its standing answer, "
        + "in their words. A line that states a rule about this repository's own development, how to verify, how long a "
        + "session runs, what a change needs before it is done, moves into this repository's records, where every developer "
        + "and agent reads it: write it into the brief or a knowledge document, in this repository's words, and name it in "
        + "your close, so the person can take it out of the machine's answer. A line that is the person's choice for this "
        + "machine, which environment to test in, which writes are allowed, stays theirs: say so.\n"
        + string.Join("\n", lines.Select(line => TextBounds.Quote(line, indent: "   ")));
}
