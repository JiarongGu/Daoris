using System.Text;

namespace Daoris.Driver;

/// <summary>An ask as the local service answered it (D65 §1a) — enough to open an intake and observe it.</summary>
/// <param name="State">`Open`, `Proposed`, `Published` or `Closed`, as the record spells it.</param>
/// <param name="Tier">Which tier answered — `declarations`, `named` or `intake` — never a model (D24).</param>
public sealed record AskView(string Id, string Workspace, string Sentence, string State, string Tier)
{
    public string? Asker { get; init; }

    /// <summary>Why it was closed, when it was.</summary>
    public string? Note { get; init; }

    /// <summary>The intake session that served it, once one opened.</summary>
    public string? Intake { get; init; }

    public IReadOnlyList<string> Links { get; init; } = [];

    /// <summary>Its files, each with where THIS machine keeps it — the service's answer, never derived here.</summary>
    public IReadOnlyList<QuestFileView> Attachments { get; init; } = [];

    /// <summary>The quests it became, in order.</summary>
    public IReadOnlyList<string> Quests { get; init; } = [];

    /// <summary>What the declarations tier proposed, best first — a word match, offered as one.</summary>
    public IReadOnlyList<string> Proposed { get; init; } = [];
}

/// <summary>A repository's declaration as the registry answered it — what the intake decides from (D34).</summary>
/// <param name="Registered">Whether it declared a domain at all — adopted and silent is not the same as owning nothing.</param>
/// <param name="Root">Where it is on this machine, answered only to this machine; null elsewhere.</param>
public sealed record DeclarationView(
    string Repository, bool Adopted, bool Registered, string? Summary,
    IReadOnlyList<string> Owns, IReadOnlyList<string> Accepts, string? Root);

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
/// </remarks>
public static class IntakeRoom
{
    /// <summary>The folder under the home that holds a room per circle.</summary>
    public const string Folder = "intake";

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

    /// <summary>Where a circle's room is — one folder under the home, whatever the circle is called.</summary>
    public static string PathOf(string home, string workspace) =>
        Path.Combine(home, Folder, SafeName(workspace));

    /// <summary>Seed the room with the circle's declarations as they stand now, and answer where it is.</summary>
    public static string Prepare(string home, string workspace, IReadOnlyList<DeclarationView> declarations)
    {
        var room = PathOf(home, workspace);
        Directory.CreateDirectory(Path.Combine(room, ".claude"));

        Write(Path.Combine(room, "AGENTS.md"), Render(workspace, declarations));
        // Two harnesses read AGENTS.md; Claude Code reads CLAUDE.md — the canon's own shape (D59).
        Write(Path.Combine(room, "CLAUDE.md"), "@AGENTS.md\n");
        Write(Path.Combine(room, ".claude", "settings.json"), Settings());
        return room;
    }

    /// <summary>The room's AGENTS.md: who is in the circle, what each owns and accepts, and where it is.</summary>
    public static string Render(string workspace, IReadOnlyList<DeclarationView> declarations)
    {
        var text = new StringBuilder();
        text.Append($"# Intake — workspace `{workspace}`\n\n");
        text.Append("This room is Daoris's, not any repository's. A session here answers ONE ask made at the\n");
        text.Append("workspace: it decides which repository owns the work — from what each repository declares\n");
        text.Append("below — and publishes the quest to it. It never edits a repository: work a repository needs\n");
        text.Append("is a quest its own agent takes. Written by Daoris at every intake; an edit here is overwritten.\n\n");

        var declared = declarations.Where(d => d.Adopted && d.Registered).ToList();
        var silent = declarations.Where(d => d.Adopted && !d.Registered).ToList();
        var outside = declarations.Where(d => !d.Adopted).ToList();

        text.Append("## Who owns what\n\n");
        if (declared.Count == 0)
        {
            text.Append("_No repository in this circle has declared what it owns — nothing here can be decided from\n");
            text.Append("declarations. Say so, and ask the person._\n\n");
        }

        foreach (var repository in declared)
        {
            text.Append($"### `{repository.Repository}`\n\n");
            if (repository.Summary is { Length: > 0 } summary) text.Append($"{summary.Trim()}\n\n");
            if (repository.Owns.Count > 0) text.Append($"- **owns:** {string.Join("; ", repository.Owns)}\n");
            if (repository.Accepts.Count > 0) text.Append($"- **accepts:** {string.Join("; ", repository.Accepts)}\n");
            if (repository.Root is { Length: > 0 } root) text.Append($"- **where:** {root}\n");
            text.Append('\n');
        }

        // Said, never hidden: "who cannot be decided" and "who cannot be asked" are part of the same
        // question, and silence reads as the repository not existing.
        if (silent.Count > 0)
        {
            text.Append("## Adopted, and declared nothing\n\n");
            foreach (var repository in silent)
            {
                text.Append($"- `{repository.Repository}` — can be asked, but has declared nothing it owns, so no\n");
                text.Append("  declaration can make it the owner. Only the person can.\n");
            }

            text.Append('\n');
        }

        if (outside.Count > 0)
        {
            text.Append("## Not addressable\n\n");
            foreach (var repository in outside)
            {
                text.Append($"- `{repository.Repository}` — not adopted: nothing there can see a quest.\n");
            }

            text.Append('\n');
        }

        return text.ToString();
    }

    /// <summary>The allow-list, as the harness reads a project's settings.</summary>
    private static string Settings()
    {
        using var stream = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("permissions");
            writer.WriteStartArray("allow");
            foreach (var rule in Allowed) writer.WriteStringValue(rule);
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n";
    }

    /// <summary>A circle's name as one folder: letters, digits, `-` and `_`, lower case. Anything else is a `-`.</summary>
    private static string SafeName(string workspace)
    {
        var safe = new string(workspace.Trim().ToLowerInvariant()
            .Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray()).Trim('-');
        return safe.Length == 0 ? "default" : safe;
    }

    /// <summary>Beside, then renamed — a harness starting in the room never reads half a file.</summary>
    private static void Write(string path, string content)
    {
        var beside = path + ".writing";
        File.WriteAllText(beside, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(beside, path, overwrite: true);
    }
}

/// <summary>
/// The intake's instruction (D65 §1b), composed once and delivered by the harness's own door — the
/// claiming instruction's sibling. It names no model (D24) and no repository layout: which harness
/// reads it is the machine's choice, and the circle is whatever the room says.
/// </summary>
public static class IntakePrompt
{
    public static string Compose(AskView ask)
    {
        var text = new StringBuilder();
        text.Append($"You are the intake for workspace `{ask.Workspace}`. You are not any repository's agent: this\n");
        text.Append("room is Daoris's, and your one job is to decide who owns the work a person asked for, and to\n");
        text.Append("ask them for it.\n\n");

        text.Append($"The person asked, as ask `#{ask.Id}`:\n\n");
        foreach (var line in ask.Sentence.Split('\n')) text.Append($"> {line.TrimEnd('\r')}\n");
        text.Append('\n');

        if (ask.Links.Count > 0)
        {
            text.Append("Links they gave — read them; a ticket usually says more than the sentence does:\n");
            foreach (var link in ask.Links) text.Append($"- {link}\n");
            text.Append('\n');
        }

        if (ask.Attachments.Count > 0)
        {
            text.Append("Files they attached — read them, never edit them. Every quest you publish carries them:\n");
            foreach (var file in ask.Attachments)
            {
                text.Append(file.Path is { } path
                    ? $"- `{file.Name}` — {path}\n"
                    : $"- `{file.Name}` — not on this machine.\n");
            }

            text.Append('\n');
        }

        text.Append("`AGENTS.md` in this room lists every repository in the circle — what each owns, what it\n");
        text.Append("accepts, and where it is. Decide from those declarations; the `registry` tool answers the same,\n");
        text.Append("live.");
        if (ask.Proposed.Count > 0)
        {
            text.Append(" By words alone the declarations proposed ");
            text.Append(string.Join(", ", ask.Proposed.Select(repository => $"`{repository}`")));
            text.Append(" — a word match, not a decision.");
        }

        text.Append("\n\n");
        text.Append("When the declarations settle it, publish the work with `quest_publish`: to the repository that\n");
        text.Append("owns it, a title that says what is wanted, and a body with the why and the evidence — what the\n");
        text.Append("ticket says, not the change you would make. The quest is asked by the ask itself and carries its\n");
        text.Append("links and files. Work that needs two repositories is a quest to each; a check that should follow\n");
        text.Append("the work — in a browser, say — is a `then` step on the quest it follows.\n\n");

        text.Append("When they do not settle it — nobody declares it, or more than one could — do not guess, and\n");
        text.Append("publish nothing. End by saying plainly what you would need to know; the person decides.\n\n");

        text.Append("Never edit a repository, and never write outside this room: publishing is the whole of your work.\n");
        return text.ToString();
    }
}

/// <summary>
/// An intake's end, concluded from what the driver can see (D46 §4): the exit code, and what became
/// of the ASK — never from what the session said about itself.
/// </summary>
public static class IntakeObservation
{
    /// <param name="before">How many quests the ask had when the intake opened.</param>
    /// <param name="after">The ask as it stands now — null when the service no longer has it.</param>
    public static SessionConclusion Conclude(int exitCode, int before, AskView? after)
    {
        var exit = exitCode == 0 ? "" : $" (exit {exitCode})";
        if (after is null)
        {
            return new("failed", $"the ask it answered is gone{exit}.");
        }

        var gained = after.Quests.Skip(before).ToList();

        // The ask becoming quests BY ITS INTAKE is the work — it outranks a messy exit, as a quest
        // reaching done does for a driven session.
        if (gained.Count > 0 && after.Tier == ByIntake)
        {
            return new("completed", $"published {Quests(gained)} onto ask `#{after.Id}`{exit}.");
        }

        // Somebody else answered it while this ran — the person, publishing or closing. The ask was
        // theirs to settle, which is what standing down has always meant.
        if (after.State == "Closed")
        {
            return new("stood-down", $"ask `#{after.Id}` was closed while it ran ({after.Note}).");
        }

        if (gained.Count > 0)
        {
            return new("stood-down", $"ask `#{after.Id}` was answered while it ran — it became {Quests(gained)}.");
        }

        // A clean exit that published nothing is the intake ASKING: the declarations did not settle it
        // and it said so rather than guess. The question is its own last words, on its transcript.
        return exitCode == 0
            ? new("awaiting-person",
                $"published nothing: the declarations did not settle ask `#{after.Id}`, so it asks you rather "
                + "than guess — its question ends its transcript. `daoris-driver ask --publish "
                + $"{after.Id} --to <repository>` answers it; `daoris-driver ask --close {after.Id} --reason \"…\"` "
                + "ends it.")
            : new("failed", $"exit {exitCode} before publishing anything onto ask `#{after.Id}`.");
    }

    /// <summary>
    /// A PARKED intake's end: the person answered its ask. It has no process left to observe — it asked
    /// and ended — so this is the only way its record ever closes. Null while the ask still waits.
    /// </summary>
    public static SessionConclusion? Answered(AskView ask) => ask.State switch
    {
        "Published" => new("completed", $"the person answered ask `#{ask.Id}` — it became {Quests(ask.Quests)}."),
        "Closed" => new("stopped", $"the person closed ask `#{ask.Id}`: {ask.Note}"),
        _ => null,
    };

    /// <summary>The ask's own tier word for an intake's publish — the service's spelling.</summary>
    public const string ByIntake = "intake";

    private static string Quests(IEnumerable<string> ids) => string.Join(", ", ids.Select(id => $"`#{id}`"));
}
