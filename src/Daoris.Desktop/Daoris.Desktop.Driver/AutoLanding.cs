using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Who accepted a landing (LAND2b, D145 point 6): the person's press, or the rule's switch at the quest's done.</summary>
public static class AcceptedBy
{
    /// <summary>The person pressed Accept: the review's, a terminal's <c>trees land</c>, or an Ask Daoris card.</summary>
    public const string Person = "person";

    /// <summary>The rule accepts automatically (<c>autoAccept</c>), and the quest's done landed it at a look.</summary>
    public const string Auto = "auto";
}

/// <summary>The landing rule a landing was made under, as it stood then (LAND2b, D145 point 6, design §8).</summary>
/// <param name="Plugin">The plugin the rule named, whether or not it pushed; null where the person pushes.</param>
/// <param name="Source">Where the rule came from, one of <see cref="LandingSource"/>.</param>
public sealed record LandedRule(string? Plugin, bool AutoAccept, string Source);

/// <summary>
/// What a try to land a due session came to (LAND2b, design §8): the codes its due entry keeps and the machine log writes.
/// The first eight are the design's; the rest name why an entry closed without a landing, so nothing closes unsaid (D143
/// point 1).
/// </summary>
public static class AutoLandingCode
{
    /// <summary>The branch was made and recorded, and the rule's plugin pushed it where it names one.</summary>
    public const string Landed = "landed";

    /// <summary>A later step's done moved the chain's branch on (LAND2c). Declared for it; nothing writes it yet.</summary>
    public const string Advanced = "advanced";

    /// <summary>The done made no commits, so there was nothing to land.</summary>
    public const string Nothing = "nothing";

    /// <summary>The quest is done and held (D133's departure, D144's evidence): it lands at the look after the release.</summary>
    public const string Held = "held";

    /// <summary>The tree holds uncommitted work, which a branch would leave behind.</summary>
    public const string Uncommitted = "uncommitted";

    /// <summary>The pattern names a branch that stands, and Daoris does not move one (D87).</summary>
    public const string Exists = "exists";

    /// <summary>The branch was made and recorded; the rule's plugin cannot land work here, so its push was not tried (D145 point 2).</summary>
    public const string PluginUnready = "plugin-unready";

    /// <summary>The branch was made and recorded; the rule's plugin failed, or answered that it did not push (D100).</summary>
    public const string PluginFailed = "plugin-failed";

    /// <summary>Its work reached a branch of the person's another way: their press, or a push of their own.</summary>
    public const string Already = "already";

    /// <summary>A newer session went on in its tree, and stands for it now (<c>ReviewableTree</c>'s rule).</summary>
    public const string Superseded = "superseded";

    /// <summary>Its tree was discarded before it landed.</summary>
    public const string Gone = "gone";

    /// <summary>Its quest is no longer done: the person turned a departure down, or the service no longer holds it.</summary>
    public const string Undone = "undone";

    /// <summary>Its repository's rule no longer accepts automatically: the person's press is the way again.</summary>
    public const string Off = "off";

    /// <summary>The landing was refused for another reason, git's or the pattern's, in its own sentence.</summary>
    public const string Refused = "refused";

    /// <summary>Whether a try with this code closes its entry; the rest wait for a change, a release or the person's press.</summary>
    public static bool Closes(string code) => code is not (Held or Uncommitted or Exists or Refused);

    /// <summary>Whether a try with this code is tried again only once its tree's tip or status moves (design §2).</summary>
    public static bool OnChange(string code) => code is Uncommitted or Exists or Refused;
}

/// <summary>One try to land a due session, and the facts it was made at (design §8): never a sentence, a path or a word of anyone's.</summary>
/// <param name="Code">One of <see cref="AutoLandingCode"/>.</param>
public sealed record AutoTry(DateTimeOffset At, string Code)
{
    /// <summary>The tree's HEAD when it was tried, or null where none was read.</summary>
    public string? Tip { get; init; }

    /// <summary>The tree's status when it was tried (<see cref="AutoLandingRules.Fingerprint"/>), or null where none was read.</summary>
    public string? Status { get; init; }

    /// <summary>How many paths were uncommitted, where that is what held it.</summary>
    public int? Uncommitted { get; init; }

    /// <summary>The branch it made, or the one it found standing.</summary>
    public string? Branch { get; init; }

    /// <summary>How many commits it carried, where it landed.</summary>
    public int? Commits { get; init; }
}

/// <summary>
/// A session due to land automatically (LAND2b, D145 point 2): its quest, where it ran, the tree it ran in, when it became
/// due, each try, and when the entry closed.
/// </summary>
/// <param name="Tree">The tree its record names, under this home's trees (D51): machine-local, like the file that keeps it.</param>
public sealed record AutoLanding(string Session, string Quest, string Repository, string Workspace, string Tree, DateTimeOffset DueAt)
{
    public IReadOnlyList<AutoTry> Tries { get; init; } = [];

    /// <summary>When a try closed it (<see cref="AutoLandingCode.Closes"/>), or null while it waits.</summary>
    public DateTimeOffset? Closed { get; init; }

    /// <summary>Its last try, or null before the first.</summary>
    public AutoTry? Last => Tries.Count == 0 ? null : Tries[^1];
}

/// <summary>How a concluded record stands to the switch (<see cref="AutoLandingRules.Concluded"/>).</summary>
public static class AutoConcluded
{
    /// <summary>A done, in a tree of its own: due to land.</summary>
    public const string Due = "due";

    /// <summary>A done in the repository's own checkout: its work is already there, and nothing lands (design §2).</summary>
    public const string NoTree = "no-tree";

    /// <summary>An end that is not a done — declined, failed, stood down, stopped: it keeps today's review (design §2).</summary>
    public const string NotDone = "not-done";
}

/// <summary>The due list's rules (LAND2b): pure, so the tables hold them without a process.</summary>
public static class AutoLandingRules
{
    /// <summary>The ends a record may conclude in that are not a done, and are said under the switch.</summary>
    private static readonly HashSet<string> Ends = new(StringComparer.Ordinal) { "declined", "failed", "stood-down", "stopped" };

    /// <summary>
    /// How a driven record that just concluded stands to its repository's rule (design §2): due, a done in no tree of its own,
    /// an end that is not a done, or null where the rule does not accept automatically, or the record is not an end.
    /// </summary>
    /// <param name="status">Its quest's status as the conclusion read it.</param>
    /// <param name="ownTree">Whether it ran in a tree this home opened (D51), the only kind a landing reaches.</param>
    /// <param name="state">The state its record concluded in.</param>
    public static string? Concluded(LandingRule rule, string? status, bool ownTree, string state)
    {
        // A merge never accepts automatically (D145 point 1): the file's reader drops one that says so, and so does this.
        if (rule is not { Form: LandingForm.Branch, AutoAccept: true } || SessionStates.IsParked(state)) return null;
        if (status == "Done") return ownTree ? AutoConcluded.Due : AutoConcluded.NoTree;
        return Ends.Contains(state) ? AutoConcluded.NotDone : null;
    }

    /// <summary>
    /// Whether a due entry is worth a try now (design §2): its first, and a hold's at every look; a refusal only once its
    /// tree's tip or status moved since it, so a look never repeats one that nothing changed.
    /// </summary>
    public static bool ShouldTry(AutoLanding entry, string? tip, string status) => entry.Last switch
    {
        null => true,
        { Code: var code } last when AutoLandingCode.OnChange(code) =>
            !string.Equals(last.Tip, tip, StringComparison.Ordinal) || !string.Equals(last.Status, status, StringComparison.Ordinal),
        { Code: AutoLandingCode.Held } => true,
        _ => false,
    };

    /// <summary>
    /// A tree's <c>git status --porcelain</c> as a fact a try is kept with: <c>clean</c>, or a fingerprint of what git listed,
    /// so a later look can tell whether it moved without keeping a path.
    /// </summary>
    public static string Fingerprint(string porcelain)
    {
        var lines = porcelain.ReplaceLineEndings("\n").Split('\n').Select(line => line.TrimEnd()).Where(line => line.Length > 0).ToList();
        if (lines.Count == 0) return "clean";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines)));
        return "sha256:" + Convert.ToHexStringLower(hash)[..16];
    }

    /// <summary>What a landing came to, by its code (design §8).</summary>
    public static string CodeOf(TreeLanding landed)
    {
        if (!landed.Landed) return landed.Refusal ?? AutoLandingCode.Refused;
        if (landed.Unready is not null) return AutoLandingCode.PluginUnready;
        return landed.Plugin is { Failed: true } or { Pushed: false } ? AutoLandingCode.PluginFailed : AutoLandingCode.Landed;
    }
}

/// <summary>
/// The sessions due to land automatically (LAND2b, D145 point 2): <c>&lt;home&gt;/sessions/auto-landings.json</c>. Written when a
/// record concludes due, read and tried at each look, and kept once closed, so what each try came to can still be read by
/// the trace and a terminal (D143).
/// </summary>
/// <remarks>
/// <para><b>Machine-local, under the home (D63)</b>, beside the conversations it is about: which sessions this machine lands is
/// a fact about this machine, like its landings.</para>
///
/// <para><b>A file that does not read is no due list, never a failure.</b> A landing missed that way waits for the person's
/// press, as it did before the switch: the safe side.</para>
///
/// <para><b>Bounded</b>: the newest <see cref="ClosedKept"/> closed entries are kept, an open one always.</para>
/// </remarks>
public sealed class AutoLandings(string home)
{
    public const string FileName = "auto-landings.json";

    /// <summary>How many closed entries are kept, the newest by when they closed; an open entry is never dropped.</summary>
    public const int ClosedKept = 200;

    // One writer at a time in this process: the look's pass and a conclusion may write at once.
    private static readonly object Gate = new();

    public string FilePath => Path.Combine(home, "sessions", FileName);

    /// <summary>Every entry, open and closed, in the order they became due.</summary>
    public IReadOnlyList<AutoLanding> All()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (!document.RootElement.TryGetProperty("due", out var due) || due.ValueKind != JsonValueKind.Array) return [];
            return [.. due.EnumerateArray().Select(Read).OfType<AutoLanding>()];
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>The entries still waiting to land, in the order they became due.</summary>
    public IReadOnlyList<AutoLanding> Open() => [.. All().Where(entry => entry.Closed is null)];

    /// <summary>One session's entry, open or closed, or null.</summary>
    public AutoLanding? Of(string session) =>
        All().LastOrDefault(entry => string.Equals(entry.Session, session, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Make a session due. One due before, open or closed, is opened again with its tries kept: a session words went on with
    /// after its landing (D137) concludes again, and what it came to the first time stays a fact.
    /// </summary>
    public void Due(AutoLanding entry) => Edit(all =>
    {
        var before = all.FirstOrDefault(each => Same(each, entry.Session));
        var opened = entry with { Tries = before?.Tries ?? entry.Tries, Closed = null };
        return [.. all.Where(each => !Same(each, entry.Session)), opened];
    });

    /// <summary>Keep one try on a session's entry, and close it where the try ends it.</summary>
    public void Tried(string session, AutoTry tried, bool close) => Edit(all =>
        [.. all.Select(each => Same(each, session) ? each with { Tries = [.. each.Tries, tried], Closed = close ? tried.At : each.Closed } : each)]);

    private static bool Same(AutoLanding entry, string session) => string.Equals(entry.Session, session, StringComparison.OrdinalIgnoreCase);

    private void Edit(Func<IReadOnlyList<AutoLanding>, IReadOnlyList<AutoLanding>> change)
    {
        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            AtomicFile.WriteText(FilePath, ToJson(Bounded(change(All()))));
        }
    }

    /// <summary>The entries with the closed ones beyond the newest <see cref="ClosedKept"/> dropped, the rest in their order.</summary>
    private static IReadOnlyList<AutoLanding> Bounded(IReadOnlyList<AutoLanding> entries)
    {
        var dropped = entries
            .Select((entry, at) => (Entry: entry, At: at))
            .Where(each => each.Entry.Closed is not null)
            .OrderByDescending(each => each.Entry.Closed).ThenByDescending(each => each.At)
            .Skip(ClosedKept)
            .Select(each => each.At)
            .ToHashSet();
        return dropped.Count == 0 ? entries : [.. entries.Where((_, at) => !dropped.Contains(at))];
    }

    /// <summary>Written by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives.</summary>
    private static string ToJson(IReadOnlyList<AutoLanding> entries)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("due");
            foreach (var entry in entries)
            {
                writer.WriteStartObject();
                writer.WriteString("session", entry.Session);
                writer.WriteString("quest", entry.Quest);
                writer.WriteString("repository", entry.Repository);
                writer.WriteString("workspace", entry.Workspace);
                writer.WriteString("tree", entry.Tree);
                writer.WriteString("dueAt", Stamp(entry.DueAt));
                if (entry.Closed is { } closed) writer.WriteString("closed", Stamp(closed));
                writer.WriteStartArray("tries");
                foreach (var tried in entry.Tries)
                {
                    writer.WriteStartObject();
                    writer.WriteString("at", Stamp(tried.At));
                    writer.WriteString("code", tried.Code);
                    if (tried.Tip is not null) writer.WriteString("tip", tried.Tip);
                    if (tried.Status is not null) writer.WriteString("status", tried.Status);
                    if (tried.Uncommitted is { } uncommitted) writer.WriteNumber("uncommitted", uncommitted);
                    if (tried.Branch is not null) writer.WriteString("branch", tried.Branch);
                    if (tried.Commits is { } commits) writer.WriteNumber("commits", commits);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n";
    }

    /// <summary>One entry, or null where it lacks what makes it one: a session, a quest, a repository, a tree and when it became due.</summary>
    private static AutoLanding? Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (Text(element, "session") is not { } session || Text(element, "quest") is not { } quest
            || Text(element, "repository") is not { } repository || Text(element, "tree") is not { } tree
            || When(Text(element, "dueAt")) is not { } dueAt)
        {
            return null;
        }

        var tries = element.TryGetProperty("tries", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray()
                .Where(each => each.ValueKind == JsonValueKind.Object && When(Text(each, "at")) is not null && Text(each, "code") is not null)
                .Select(each => new AutoTry(When(Text(each, "at"))!.Value, Text(each, "code")!)
                {
                    Tip = Text(each, "tip"),
                    Status = Text(each, "status"),
                    Uncommitted = Number(each, "uncommitted"),
                    Branch = Text(each, "branch"),
                    Commits = Number(each, "commits"),
                })
                .ToList()
            : [];
        return new AutoLanding(session, quest, repository, Text(element, "workspace") ?? RemoteTarget.DefaultWorkspace, tree, dueAt)
        {
            Tries = tries,
            Closed = When(Text(element, "closed")),
        };
    }

    private static string Stamp(DateTimeOffset at) => at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset? When(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null;

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) ? n : null;
}

/// <summary>
/// What the conversation's record keeps of each try it is told of (LAND2b, design §2, D100): a note whose lead-in is a code
/// (LANG1a), so the page words it in the reader's language, and the landing's own sentence beneath it as a part of its own.
/// </summary>
public static class AutoLandingNotes
{
    /// <summary>Whether a try with this code is said in the conversation; the closings without a landing stay the due list's and the log's.</summary>
    public static bool Says(string code) => code is AutoLandingCode.Landed or AutoLandingCode.PluginUnready or AutoLandingCode.PluginFailed
        or AutoLandingCode.Nothing or AutoLandingCode.Held or AutoLandingCode.Uncommitted or AutoLandingCode.Exists or AutoLandingCode.Refused;

    /// <summary>
    /// The note for one try: its lead-in by its code, then the landing's sentence where there was a landing to say it.
    /// </summary>
    /// <param name="landed">What the landing said, or null where the try never reached one (a hold, uncommitted work, no commits).</param>
    /// <param name="plugin">The rule's plugin, where the lead-in names it.</param>
    /// <param name="uncommitted">How many paths were uncommitted, where that held it.</param>
    /// <param name="branch">The branch the pattern names, where a refusal names one and the landing did not.</param>
    public static SessionEvent Of(string code, TreeLanding? landed, string? plugin = null, int? uncommitted = null, string? branch = null)
    {
        var named = landed?.Branch ?? branch ?? "";
        var lead = code switch
        {
            AutoLandingCode.Landed => Noted.Of(NoteCodes.LandingAccepted,
                $"{LandingRules.AutoAccepted} its work is on `{named}`.", ("branch", named)),
            AutoLandingCode.PluginUnready => Noted.Of(NoteCodes.LandingUnready,
                $"{LandingRules.AutoAccepted} its work is on `{named}`, and `{plugin}` cannot land work here, so nothing was pushed; "
                + "`trees hand` gives it the branch once it can.",
                ("branch", named), ("plugin", plugin)),
            AutoLandingCode.PluginFailed => Noted.Of(NoteCodes.LandingPluginFailed,
                $"{LandingRules.AutoAccepted} its work is on `{named}`, and `{plugin}` did not push it.", ("branch", named), ("plugin", plugin)),
            AutoLandingCode.Nothing => Noted.Of(NoteCodes.LandingNothing, "its quest was done and its work made no commits, so nothing was landed."),
            AutoLandingCode.Held => Noted.Of(NoteCodes.LandingHeld,
                "its quest is done and held for your yes, so its work lands at the look after you release it."),
            AutoLandingCode.Uncommitted => Noted.Of(NoteCodes.LandingUncommitted,
                $"not accepted automatically: its tree has {uncommitted ?? 0} uncommitted path(s), which a branch would leave behind. "
                + "Commit them in the tree or tell the session, then Accept lands it.",
                ("paths", uncommitted ?? 0)),
            AutoLandingCode.Exists => Noted.Of(NoteCodes.LandingExists,
                $"not accepted automatically: `{named}` is already a branch, and Daoris does not move it. It waits for your review.",
                ("branch", named)),
            _ => Noted.Of(NoteCodes.LandingRefused, "not accepted automatically: the landing was refused, as follows. It waits for your review."),
        };

        // The landing's own sentence beneath, as written: it carries git's and the plugin's words beside Daoris's.
        var said = landed is { Message.Length: > 0 } ? lead.Then(" ", Noted.Said(landed.Message, NoteBy.Program)) : lead;
        return new SessionEvent { Kind = SessionEventKind.Note, Text = said.Note, Parts = said.Parts };
    }

    /// <summary>What a record that concluded under the switch and lands nothing says (design §2).</summary>
    /// <param name="concluded"><see cref="AutoConcluded.NoTree"/> or <see cref="AutoConcluded.NotDone"/>.</param>
    public static SessionEvent Concluded(string concluded)
    {
        var said = concluded == AutoConcluded.NoTree
            ? Noted.Of(NoteCodes.LandingNoTree,
                "its quest was done, and it worked in the repository's own checkout, so its work is already there: nothing lands automatically.")
            : Noted.Of(NoteCodes.LandingNotDone, "it did not end on a done, so nothing lands automatically: its work waits for your review.");
        return new SessionEvent { Kind = SessionEventKind.Note, Text = said.Note, Parts = said.Parts };
    }
}

/// <summary>
/// The machine log's landing line (LAND2b, design §8, D94): <c>landing.auto</c>, one per try, with codes and counts and the
/// plugin's id, never a sentence, a branch or a path.
/// </summary>
public sealed record LandingLine(string Event, IReadOnlyList<(string Key, object? Value)> Data)
{
    /// <summary>What a plugin's id may be in a line: the catalogue's own shape, never a phrase.</summary>
    private static readonly System.Text.RegularExpressions.Regex PluginId =
        new("^[a-z0-9][a-z0-9.-]{0,119}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>One try: whose, where, its code, what it carried or what held it, and whether the plugin pushed.</summary>
    public static LandingLine Auto(
        string session, string repository, string workspace, string code, int? commits, int? uncommitted, string? plugin, bool? pushed) =>
        new("landing.auto",
        [
            ("session", session), ("repository", repository), ("workspace", workspace), ("code", code), ("commits", commits),
            ("uncommitted", uncommitted), ("plugin", plugin is not null && PluginId.IsMatch(plugin) ? plugin : null), ("pushed", pushed),
        ]);
}
