using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The intake's room (D65 §1b): a directory under Daoris's home, one per circle, seeded with the
/// circle's declarations — the working tree an intake session runs in, because it answers for a
/// WORKSPACE and no repository is that.
/// </summary>
/// <remarks>
/// <para><b>Daoris's own directory, so Daoris writes it</b> — the one tree the driver may seed, since
/// nobody else owns it (D32 is about repositories). Re-rendered at every open: the declarations are
/// the registry's and change, and an intake deciding from a stale room decides wrong.</para>
///
/// <para><b>Never a session tree</b> (§1b's trap): `SessionTrees` lays out
/// <c>&lt;workspace&gt;/&lt;repository&gt;</c> worktrees of a checkout, and the room is neither.</para>
///
/// <para><b>Never asked about by git.</b> It is no repository, and git asked about it walks UP — under
/// a home inside a checkout (a rehearsal's scratch) it would answer for that checkout. So the intake
/// records no base commit and no evidence of commits.</para>
///
/// <para><b>A top file, not the Domain's (FLAT1b-ii).</b> Seeding the room makes its folder and writes three files, so the
/// type holds I/O; what the room says is <see cref="Render"/>, still a pure function of its arguments. The declarations
/// and the kinds it reads are the records in <c>Domain/Intake.cs</c>.</para>
/// </remarks>
public static class IntakeRoom
{
    /// <summary>Which ask an intake answers — on its spawn, and on the connector it is offered.</summary>
    public const string AskVariable = "DAORIS_ASK_ID";

    /// <summary>Which session is answering — what the ask checks before it says a harness decided.</summary>
    public const string SessionVariable = "DAORIS_SESSION_ID";

    /// <summary>What an intake's spawn and its connector carry beyond a session's usual environment.</summary>
    public static IReadOnlyDictionary<string, string> Scope(string ask, string session) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AskVariable] = ask,
            [SessionVariable] = session,
        };

    /// <summary>
    /// The tools the room allows without asking. The intake's job is to read the family and publish
    /// — and over the protocol door a permission request is refused by construction (D52), so a tool
    /// left off this list is a session that stalls on its first call. Nothing here writes anywhere
    /// but the service, and nothing runs a command: reading a ticket is `WebFetch`, the design's own
    /// "its own web tools".
    /// </summary>
    internal static readonly IReadOnlyList<string> Allowed =
    [
        $"mcp__{KnowledgeConnector.ServerName}__registry",
        $"mcp__{KnowledgeConnector.ServerName}__knowledge_search",
        $"mcp__{KnowledgeConnector.ServerName}__knowledge_get",
        $"mcp__{KnowledgeConnector.ServerName}__knowledge_repositories",
        $"mcp__{KnowledgeConnector.ServerName}__quest_list",
        $"mcp__{KnowledgeConnector.ServerName}__quest_publish",
        "WebFetch",
    ];

    /// <summary>Where a circle's room is — the home's <see cref="HomeEntry.Intake"/>, one per circle, whatever it is called.</summary>
    public static string PathOf(string home, string workspace) =>
        DaorisHome.Entry(home, HomeEntry.Intake, SafeName(workspace));

    /// <summary>Seed the room with the circle's declarations as they stand now, and answer where it is.</summary>
    /// <param name="kinds">The kinds its workspace declares, per repository (WORKFLOW1i-b); null or empty where it declares none.</param>
    public static string Prepare(
        string home, string workspace, IReadOnlyList<DeclarationView> declarations, IReadOnlyList<IntakeRepositoryKinds>? kinds = null)
    {
        var room = PathOf(home, workspace);
        Directory.CreateDirectory(Path.Combine(room, ".claude"));

        // What each repository that declared nothing says about itself (D77), read from its own files
        // at every open — a README changes as a declaration does — and never written into.
        var described = new Dictionary<string, RepositoryDescription>(StringComparer.OrdinalIgnoreCase);
        foreach (var repository in declarations.Where(d => !(d.Adopted && d.Registered) && d.Root is { Length: > 0 }))
        {
            if (SelfDescriptionFiles.Read(repository.Root!) is { } description) described[repository.Repository] = description;
        }

        Write(Path.Combine(room, "AGENTS.md"), Render(workspace, declarations, described, kinds));
        // Two harnesses read AGENTS.md; Claude Code reads CLAUDE.md — the canon's own shape (D59).
        Write(Path.Combine(room, "CLAUDE.md"), "@AGENTS.md\n");
        Write(Path.Combine(room, ".claude", "settings.json"), Settings());
        return room;
    }

    /// <summary>Beside, then renamed — a harness starting in the room never reads half a file.</summary>
    private static void Write(string path, string content)
    {
        AtomicFile.WriteText(path, content);
    }

    /// <summary>
    /// The room's AGENTS.md: who is in the circle, what each declares it owns and accepts, where it is —
    /// and, for one that declared nothing, what its own files say about it (D77).
    /// </summary>
    public static string Render(
        string workspace, IReadOnlyList<DeclarationView> declarations,
        IReadOnlyDictionary<string, RepositoryDescription>? described = null,
        IReadOnlyList<IntakeRepositoryKinds>? kinds = null)
    {
        described ??= new Dictionary<string, RepositoryDescription>();
        var text = new StringBuilder();
        text.Append($"# Intake — workspace `{workspace}`\n\n");
        text.Append("This room is Daoris's, not any repository's. A session here answers ONE ask made at the\n");
        text.Append("workspace: it decides which repository owns the work — from what each repository declares\n");
        text.Append("below, or what one that declares nothing says about itself — and publishes the quest to it.\n");
        text.Append("It never edits a repository: work a repository needs is a quest its own agent takes. Written\n");
        text.Append("by Daoris at every intake; an edit here is overwritten.\n\n");

        var declared = declarations.Where(d => d.Adopted && d.Registered).ToList();
        var silent = declarations.Where(d => d.Adopted && !d.Registered).ToList();
        var outside = declarations.Where(d => !d.Adopted).ToList();
        var askable = outside.Where(d => d.Root is { Length: > 0 }).ToList();

        text.Append("## Who owns what\n\n");
        if (declared.Count == 0)
        {
            text.Append(silent.Count + askable.Count > 0
                ? "_No repository in this workspace has declared what it owns. Decide from what each says about\n"
                  + "itself, below — and where that does not settle it, say so and ask the person._\n\n"
                : "_No repository in this workspace has declared what it owns — nothing here can be decided from\n"
                  + "declarations. Say so, and ask the person._\n\n");
        }

        foreach (var repository in declared)
        {
            text.Append($"### `{repository.Repository}`\n\n");
            if (repository.Summary is { Length: > 0 } summary) text.Append($"{summary.Trim()}\n\n");
            if (repository.Owns.Count > 0) text.Append($"- **owns:** {string.Join("; ", repository.Owns)}\n");
            if (repository.Accepts.Count > 0) text.Append($"- **accepts:** {string.Join("; ", repository.Accepts)}\n");
            if (repository.Root is { Length: > 0 } root) text.Append($"- **where:** {root}\n");
            if (repository.Lanes.Count > 0)
            {
                // Its lanes (D115 §2.2), each by the address that asks it, so an intake can send work
                // that is plainly one lane's to that lane.
                text.Append("- **lanes** — a quest to one is work for that lane's session; to the repository alone, for the whole of it:\n");
                foreach (var lane in repository.Lanes)
                {
                    var words = lane.Summary.Length > 0 ? $"{lane.Title}: {lane.Summary}" : lane.Title;
                    text.Append($"  - `{repository.Repository}:{lane.Id}`{(words.Length > 0 ? $" — {words}" : "")}"
                                + $"{(lane.Steward ? " (the steward's: it keeps the records)" : "")}\n");
                }
            }

            text.Append('\n');
        }

        // Said, never hidden: "who cannot be decided" and "who cannot be asked" are part of the same
        // question, and silence reads as the repository not existing.
        if (silent.Count > 0)
        {
            text.Append("## Adopted, and declared nothing\n\n");
            foreach (var repository in silent)
            {
                text.Append($"- `{repository.Repository}` — can be asked, but declared nothing it owns");
                text.Append(described.TryGetValue(repository.Repository, out var own) ? $"; {Describe(own)}\n" : ".\n");
            }

            text.Append('\n');
        }

        // Registered with a root is addressable (D70): a quest there is answered by a protocol-door
        // session. It declares nothing, so what its own files say is what the intake has (D77) —
        // evidence, labelled as the repository's word, outranked by any declaration.
        if (outside.Count > 0)
        {
            text.Append("## Not adopted — what each says about itself\n\n");
            if (askable.Count > 0)
            {
                text.Append("None of these declares what it owns. Each with a checkout here can be asked a quest, and\n");
                text.Append("beside it is what its OWN files say — its README or package, and what it is built with. That\n");
                text.Append("is evidence, not a declaration: a name and a stack often settle a screen against a service,\n");
                text.Append("and where they do not, say so.\n\n");
            }

            foreach (var repository in outside)
            {
                text.Append(repository.Root is { Length: > 0 }
                    ? described.TryGetValue(repository.Repository, out var own)
                        ? $"- `{repository.Repository}` — {Describe(own)}\n"
                        : $"- `{repository.Repository}` — says nothing about itself: only its name.\n"
                    : $"- `{repository.Repository}` — not adopted, and no root is known for it on this machine, so it\n"
                      + "  cannot be asked a quest.\n");
            }

            text.Append('\n');
        }

        // WORKFLOW1i-b (the workflow design §4.3): the kinds the workspace declares, per repository with the workflow each follows
        // there and whether it lowers the person's part, so a proposal is made knowing it. The intake only proposes (WORKFLOW1i).
        if (kinds is { Count: > 0 })
        {
            text.Append("## Kinds of work\n\n");
            text.Append("This workspace declares kinds of work. You may PROPOSE one for the ask — `workflowProposal` on\n");
            text.Append("`quest_publish` — and never set one: the person's press does. Each repository follows a workflow for a\n");
            text.Append("kind, listed below; a kind that \"lowers your part\" asks the person to do less there (a look, a second\n");
            text.Append("opinion or a press dropped), and is proposed only on the person's words saying so.\n\n");
            foreach (var repository in kinds)
            {
                text.Append($"### `{repository.Repository}` — with no kind it follows {repository.Otherwise}\n\n");
                foreach (var kind in repository.Kinds)
                {
                    var paths = kind.Paths.Count == 0 ? "" : ", paths " + string.Join(", ", kind.Paths.Select(path => $"`{path}`"));
                    var part = kind.Lowers switch
                    {
                        null => "its steps could not be read here, so take it as lowering your part",
                        { Count: 0 } => "keeps your part",
                        var lowers => $"lowers your part: {string.Join("; ", lowers)}",
                    };
                    text.Append($"- `{kind.Kind}`{(kind.Label is { Length: > 0 } label ? $" ({label})" : "")}{paths}: {kind.Workflow} — {part}\n");
                }

                text.Append('\n');
            }
        }

        return text.ToString();
    }

    /// <summary>One line of what a repository says about itself: what it is built with, then its own words.</summary>
    private static string Describe(RepositoryDescription description)
    {
        var parts = new List<string>();
        if (description.Stack.Count > 0) parts.Add(string.Join(", ", description.Stack));
        if (description.Summary is { } summary) parts.Add($"its {description.Source} says: {summary}");
        return string.Join(" · ", parts);
    }

    /// <summary>The allow-list, as the harness reads a project's settings.</summary>
    private static string Settings() => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteStartObject("permissions");
        writer.WriteStartArray("allow");
        foreach (var rule in Allowed) writer.WriteStringValue(rule);
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    });

    /// <summary>
    /// A circle's name as one folder: ASCII letters, digits, `-` and `_`, lower case — and, when that
    /// lost anything, a short hash of the exact name beside it, so two circles are never one room.
    /// A workspace plan's file is named by the same rule (WSSETUP6), so two circles never share one.
    /// </summary>
    /// <remarks>
    /// 🔴 Every name with no ASCII letter in it used to fold to <c>default</c>, and `my circle` and
    /// `my-circle` to one folder (REV3). The room is the intake's lock and holds its circle's
    /// declarations, so two circles in one room blocked each other and read each other's family.
    /// A name that is already a folder name stays exactly that, so no existing room moves.
    /// </remarks>
    internal static string SafeName(string workspace)
    {
        var name = workspace.Trim().ToLowerInvariant();
        if (name.Length == 0) name = "default";   // silence is the default circle, as everywhere (D48)
        var safe = new string(name
            .Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray()).Trim('-');
        if (safe == name && safe.Length > 0) return safe;

        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..8].ToLowerInvariant();
        return safe.Length == 0 ? $"workspace-{hash}" : $"{safe}-{hash}";
    }
}
