using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The landing rules (WSR1, D87): which applies to a repository, whether a pattern can name a branch,
/// and what it names for one session.
/// </summary>
public static class LandingRules
{
    /// <summary>
    /// What a pattern may say. One of the first three is required, or every session's work would go to one branch: a ticket names
    /// a branch as the quest does (PROCESS1b-i), two sessions on one ticket meeting the second's <c>exists</c> refusal.
    /// </summary>
    public static readonly IReadOnlyList<string> Placeholders = LandingKey.Placeholders;

    /// <summary>
    /// The shape of a ticket's key where the rule names none (PROCESS1b-i, D164's amendment point 2): a project's capitals, a dash
    /// and its number, as <c>TK-2202</c>.
    /// </summary>
    public const string DefaultKey = LandingKey.DefaultKey;

    /// <summary>How long a key may be: a shape, not a program. The CLI's twin holds the same bound.</summary>
    public const int KeyLimit = LandingKey.KeyLimit;

    /// <summary>
    /// The letters a key may escape (PROCESS1b-i): the classes and controls .NET and JavaScript read alike. <c>\x</c>, <c>\u</c>,
    /// <c>\c</c>, <c>\p</c> and the rest differ, or are refused by one engine. The CLI's <c>SHARED_ESCAPES</c> is the same list.
    /// </summary>
    public const string SharedEscapes = LandingKey.SharedEscapes;

    /// <summary>How long one search for a key may run before its words are passed over: a key is the person's, and need not be quick.</summary>
    private static readonly TimeSpan KeyTimeout = LandingKey.KeyTimeout;

    /// <summary>A web address in a title (LANDNAME2): never a branch's words, as <c>feature/https-…-browse-a267d192</c> was.</summary>
    private static readonly Regex WebAddress = new(@"\b(?:https?://|www\.)\S*", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// What is wrong with a rule's shape, in a sentence, or null when it can land work. Whether its plugin
    /// is on this machine is <see cref="PluginProblem"/>'s question: a file stays readable wherever it is (the settings'
    /// <see cref="LandingKey.Problem(LandingRule)"/>).
    /// </summary>
    public static string? Problem(LandingRule rule) => LandingKey.Problem(rule);

    /// <summary>
    /// Why the plugin a rule names cannot land work on this machine, in a sentence, or null when it can
    /// (D100): not installed, switched off, refused by the catalogue, or speaking on no
    /// <see cref="HookPoints.Land"/> point. Asked when the rule is set and again at the press, so a plugin
    /// gone since is refused before anything is made.
    /// </summary>
    public static string? PluginProblem(string plugin, IWorkplacePluginCatalog catalog)
    {
        var entry = catalog.Find(plugin);
        if (entry is null)
        {
            return $"the landing rule names plugin `{plugin}`, which is not installed on this machine — "
                + "`daoris plugin add <folder>` installs one, and `daoris plugin list` shows what there is.";
        }

        if (!entry.Enabled)
        {
            return $"plugin `{plugin}` is switched off on this machine — `daoris plugin enable {entry.Id}` switches it on.";
        }

        if (entry.Problem is { } problem) return $"plugin `{plugin}` contributes nothing: {problem}";

        return entry.Points.Contains(HookPoints.Land, StringComparer.Ordinal)
            ? null
            : $"plugin `{plugin}` does not land work — its manifest speaks on no `{HookPoints.Land}` point.";
    }

    /// <summary>
    /// What a door says as <i>Accept automatically</i> is set (LAND2a, D145 point 5, design §6): the switch is the
    /// person's standing say-so for a push with no press, so it is said where it is given. With no plugin, the warning
    /// that nothing leaves the machine. <c>daoris place landing</c> says these words; the page says them in its own catalogue.
    /// </summary>
    public static string AutoAcceptSays(string? plugin, bool merge = false) => merge
        ? "when a quest here is done, its work is merged into its line with no press. Switch it off to accept each one yourself."
        : plugin is { } named
        ? $"when a quest here is done, its work is put on its branch, and `{named}` pushes it and opens a pull request without "
          + "asking you each time. The pull request is where it is judged. Switch it off to accept each one yourself."
        : "when a quest here is done, its work is put on its branch, and nothing leaves this machine: no plugin opens a pull "
          + "request, so each done's branch waits here for you to push it. Switch it off to accept each one yourself.";

    /// <summary>
    /// What the conversation's record keeps of a landing (D100): the whole sentence the press said, the
    /// plugin's part in it. Machine-local, like every note there — a plugin's word never rides the
    /// session record, which travels (D64 §4).
    /// </summary>
    public static SessionEvent Note(TreeLanding landed) => Press(ChatNoteCodes.LandingPersonAccepted, PersonAccepted, landed.Message);

    /// <summary>
    /// A press's note by its code (CONVNOTE2c): the lead is Daoris's, its sentence the program's words beneath, as written, for
    /// they carry git's and the plugin's words beside Daoris's. The English is the lead, a space and the sentence, as before.
    /// </summary>
    private static SessionEvent Press(NoteCode code, string lead, string message)
    {
        var said = Noted.Of(code, lead).Then(" ", Noted.Said(message, NoteBy.Program));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = said.Note, Parts = said.Parts };
    }

    /// <summary>
    /// What a part's conversation keeps where the person left it out of its entry's Accept (ACCEPTALL1c, D167's 2026-10-15 amendment
    /// point 3): the branch its line of work landed on, or the line it merged into, so its review says so and leads with Discard.
    /// Machine-local, as every note there is; landing it later is a press of its own.
    /// </summary>
    public static SessionEvent LeftOutNote(string branch)
    {
        var said = Noted.Of(ChatNoteCodes.LandingLeftOut,
            $"the person left this part out when its line of work landed on `{branch}`: its work did not land there.", ("branch", branch));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = said.Note, Parts = said.Parts };
    }

    /// <summary>
    /// What a member's conversation keeps where a combined Accept brought its work into the head's landing (LINESTOGETHER1d2): where it
    /// went and which operation did it, so the conversation says it is not landed itself.
    /// </summary>
    public static SessionEvent BroughtIntoNote(TogetherBrought brought)
    {
        var (code, where) = brought.Line ? (ChatNoteCodes.LandingBroughtIntoLine, $"the line `{brought.Into}`") : (ChatNoteCodes.LandingBroughtInto, $"`{brought.Into}`");
        var said = Noted.Of(code, $"its work was brought into {where} by another session's Accept (operation {brought.Operation}), so it lands no second time.",
            ("branch", brought.Into), ("operation", brought.Operation));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = said.Note, Parts = said.Parts };
    }

    /// <summary>
    /// Where a part's review reads it left out (ACCEPTALL1c): the branch the newest <see cref="LeftOutNote"/> names, while no acceptance
    /// (<see cref="IsAcceptance"/>, a press of its own or a landing at done) came after it; null where none says it.
    /// </summary>
    public static string? LeftOutOf(IReadOnlyList<SessionEvent> events)
    {
        for (var at = events.Count - 1; at >= 0; at--)
        {
            if (events[at] is not { Kind: SessionEventKind.Note } note) continue;
            if (note.Text is { } text && IsAcceptance(text)) return null;
            if (note.Parts?.FirstOrDefault(part => part.Code == ChatNoteCodes.LandingLeftOut.Code)?.Value("branch") is string branch) return branch;
        }

        return null;
    }

    /// <summary>How the conversation's note of a press opens: what the trace knows an acceptance by (TRACE1).</summary>
    public const string PersonAccepted = "the person accepted this work:";

    /// <summary>
    /// How the conversation's note of a landing at done opens (LAND2b, D145 point 4): who accepted it, which the review's note
    /// and the trace name. The rest of the note is its code's (<see cref="AutoLandingNotes"/>).
    /// </summary>
    public const string AutoAccepted = "accepted automatically when its quest was done:";

    /// <summary>Whether a conversation's note is an acceptance of the session's work: a press's, or a landing at done.</summary>
    public static bool IsAcceptance(string text) =>
        text.StartsWith(PersonAccepted, StringComparison.Ordinal) || text.StartsWith(AutoAccepted, StringComparison.Ordinal);

    /// <summary>
    /// What the conversation's record keeps of a hand-off after the landing (WSR5b): the whole sentence, the
    /// plugin's part in it — kept where the landing's own note is, and for the same reason (D100).
    /// </summary>
    public static SessionEvent HandNote(TreeHand handed) =>
        Press(ChatNoteCodes.LandingPersonHanded, "the person handed this work on:", handed.Message);

    /// <summary>What is wrong with a branch pattern, in a sentence, or null when it names one branch per session (the settings' <see cref="LandingKey.Problem(string)"/>).</summary>
    public static string? Problem(string pattern) => LandingKey.Problem(pattern);

    /// <summary>What is wrong with a ticket's key, in a sentence, or null where both readers of a rule take it the same way (the settings' <see cref="LandingKey.KeyProblem(string)"/>).</summary>
    public static string? KeyProblem(string key) => LandingKey.KeyProblem(key);

    /// <summary>A pattern filled with one session's names (the settings' <see cref="LandingKey.Expand"/>).</summary>
    public static string Expand(string pattern, LandingNames names) => LandingKey.Expand(pattern, names);

    /// <summary>
    /// A title in the words git takes: lower-case letters and digits, joined by hyphens, whole words up to 40 characters, with
    /// every web address left out (LANDNAME2). A title with no words git takes — one written in another script — is `work`; a
    /// title whose words were all an address leaves nothing, and <see cref="Expand"/> takes its separator with it.
    /// </summary>
    public static string Slug(string? title) => Slug(title, ticket: null);

    /// <summary>
    /// The slug of a title the pattern also names by its ticket (PROCESS1b-i): the ticket's own words left out too, in any case,
    /// so the branch does not say them twice; a title that was only the ticket leaves nothing.
    /// </summary>
    public static string Slug(string? title, string? ticket)
    {
        var text = WebAddress.Replace(title ?? "", " ");
        if (ticket is { Length: > 0 }) text = text.Replace(ticket, " ", StringComparison.OrdinalIgnoreCase);
        var slug = Words(text);
        return slug.Length > 0 ? slug : Words(title).Length > 0 ? "" : "work";
    }

    private static string Words(string? text)
    {
        var slug = new StringBuilder();
        foreach (var word in Regex.Split((text ?? "").ToLowerInvariant(), "[^a-z0-9]+").Where(word => word.Length > 0))
        {
            if (slug.Length + (slug.Length > 0 ? 1 : 0) + word.Length > 40) break;
            if (slug.Length > 0) slug.Append('-');
            slug.Append(word);
        }

        return slug.ToString();
    }

    /// <summary>
    /// The ticket a subject's work is for (PROCESS1b-i, D164's amendment point 2): the first match of <paramref name="key"/>, else
    /// <see cref="DefaultKey"/>, in its name, its title, its request's body, then each of its links, in that order, as written. A
    /// match git would not take in a branch's name is passed over, as are words a search runs past its time on. Null where none is
    /// found: the session's id stands in.
    /// </summary>
    public static string? Ticket(LandingSubject subject, string? key)
    {
        Regex shape;
        try
        {
            shape = new Regex(key ?? DefaultKey, RegexOptions.CultureInvariant, KeyTimeout);
        }
        catch (ArgumentException)
        {
            return null; // a key the rule's own check refuses is never kept, so none is found by it
        }

        string?[] sources = [subject.Name, subject.Title, subject.Body, .. subject.Links ?? []];
        foreach (var source in sources.OfType<string>())
        {
            try
            {
                for (var match = shape.Match(source); match.Success; match = match.NextMatch())
                {
                    if (BranchName.IsValid(match.Value)) return match.Value;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // These words took the key too long; the next source is searched.
            }
        }

        return null;
    }

    /// <summary>
    /// What one session's branch is named from under a rule: where the pattern says <c>{ticket}</c>, the ticket its key finds, and
    /// the slug without it (PROCESS1b-i); otherwise the names as they always were.
    /// </summary>
    public static LandingNames Names(LandingSubject subject, LandingRule rule, string repository)
    {
        var ticket = (rule.Pattern ?? "").Contains("{ticket}", StringComparison.Ordinal) ? Ticket(subject, rule.Key) : null;
        var slug = ticket is null ? subject.Slug : Slug(subject.Name ?? subject.Title, ticket);
        return new(subject.Quest ?? subject.Session, slug, repository, subject.Session, ticket);
    }

    /// <summary>The branch a branch rule's pattern names for one session: what the plan says and the press makes.</summary>
    public static string Branch(LandingSubject subject, LandingRule rule, string repository) =>
        Expand(rule.Pattern ?? "", Names(subject, rule, repository));

    /// <summary>
    /// Whether a rule's pattern reads a subject's words (ACCEPTFOOT2): <c>{slug}</c>, or <c>{ticket}</c>, searched in them. A pattern
    /// of ids alone names the branch the same whatever the work's words, so nothing need be read for them.
    /// </summary>
    public static bool ReadsWords(LandingRule rule) =>
        (rule.Pattern ?? "").Contains("{slug}", StringComparison.Ordinal) || (rule.Pattern ?? "").Contains("{ticket}", StringComparison.Ordinal);

    /// <summary>The git log format <see cref="OldestSubject"/> reads: each commit's subject after a unit separator, oldest first.</summary>
    public const string SubjectsFormat = "--format=%x1f%s";

    /// <summary>
    /// The oldest commit's subject from <c>git log --reverse</c> in <see cref="SubjectsFormat"/> (ACCEPTFOOT2): the first commit's,
    /// trimmed, empty where its message is, never a later commit's; null where the log names no commit.
    /// </summary>
    public static string? OldestSubject(string log)
    {
        var commits = log.Split('\u001f');
        return commits.Length < 2 ? null : commits[1].Trim();
    }

    /// <summary>
    /// What a landing is named for (WSR5): the session, and the chain's FIRST quest with its title — the
    /// one the ask became — walking up each step's parent; a chat with no quest has its opening line.
    /// </summary>
    /// <remarks>
    /// 🔴 A chain lands from its last step (WSR1), so naming it for that step's quest named TK-2202's work
    /// `feature/verify-in-prod-…-381807d1f7bd`, after its verify step. The walk stops where the service no
    /// longer answers for a parent, and after twenty steps, which no chain reaches.
    /// </remarks>
    public static async Task<LandingSubject> SubjectAsync(
        string session, string? quest, Func<string, Task<QuestView?>> find, string? opening)
    {
        if (quest is null) return new LandingSubject(session, null, opening);

        var current = await find(quest).ConfigureAwait(false);
        if (current is null) return new LandingSubject(session, quest, opening);
        for (var steps = 0; steps < 20 && current.Parent is { Length: > 0 } parent; steps++)
        {
            if (await find(parent).ConfigureAwait(false) is not { } above) break;
            current = above;
        }

        return LandingSubject.Of(session, current);
    }

    /// <summary>
    /// The line's landing as the doors outside a run read it: a branch carrying the line's policy (<see cref="DriverConfig.LinePolicyOf"/>)
    /// where one is set, else a merge. A run's own landing is its bound <c>land</c>'s.
    /// </summary>
    public static Landing Line(DriverConfig config, string repository, string? workspace) =>
        config.LinePolicyOf(repository, workspace) is { } policy
            ? new(new LandingRule(LandingForm.Branch) { Integrate = policy }, LandingSource.Default)
            : new(LandingRule.Merge, LandingSource.Default);
}
