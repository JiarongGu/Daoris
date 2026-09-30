using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>A setting Ask Daoris proposes (HELP1c, D89): one of the driver's doors, spelled as the CLI's verbs are.</summary>
/// <param name="Door">`drive`, `undrive`, `hold`, `resume`, `trees`, `line`, `landing`, `intake`, `helper`, `strikes`, `timeout` or `notify`.</param>
/// <param name="Target">The repository, for the doors that take one.</param>
/// <param name="Workspace">The workspace, for a line or a landing set for a whole workspace.</param>
/// <param name="Value">What it is set to, as the CLI takes it: `on`, a branch, `branch &lt;pattern&gt; --tidy`, an agent…</param>
public sealed record SettingChange(string Door, string? Target, string? Workspace, string? Value);

/// <summary>
/// Where Ask Daoris's proposals are written (HELP1c, D89): one file each, under the driver's home, for
/// the person to apply or not.
/// </summary>
/// <remarks>
/// <para><b>Eight kinds</b>, each a door a screen or a terminal already has: a <c>setting</c> and an
/// <c>ask</c> (HELP1c); an <c>agent</c>'s update or pin, a <c>delete</c> of a quest or an ask, an
/// <c>account</c>'s model and effort, and a <c>go</c> to a screen (HELP6); a <c>plugin</c> added from its
/// folder or from the install's own offers by id, switched on or off, or updated (PLUG9); and a <c>hand</c>-off
/// of a landed branch to a landing plugin (WSR5b). Every file carries <c>target</c>, <c>workspace</c>,
/// <c>value</c> and <c>sentence</c>, null where the kind has none; an account adds <c>account</c>, <c>model</c>
/// and <c>effort</c>, a go <c>domain</c> and <c>part</c>, a plugin <c>repository</c>, <c>folder</c> and
/// <c>offer</c>, and a hand-off <c>repository</c>.</para>
///
/// <para><b>PERM2's shape</b> (<see cref="RuleProposalBox"/>): a file under the home, never a row in the
/// store, since what it would change is machine-local; one file per proposal, so two never collide.</para>
///
/// <para><b>This checks the shape and nothing more.</b> Whether the route would take it — a line git
/// accepts, a pattern that names one branch per session, a repository registered here, an agent this
/// machine has — is the driver's, which judges each with the route's own code before the person sees
/// it and hands a refusal back to the conversation in the route's words.</para>
///
/// <para><b>It never applies.</b> Every change is the person's press (D89).</para>
///
/// <para><b>THE FILE is the contract</b>: the driver's <c>HelpProposals</c> reads and settles it, shares no
/// code with this, and each side's tests hold the same shape.</para>
/// </remarks>
public sealed class HelpProposalBox(string? home)
{
    /// <summary>The doors a setting may name, as the CLI's verbs spell them.</summary>
    public static readonly IReadOnlyList<string> Doors =
        ["drive", "undrive", "hold", "resume", "trees", "line", "landing", "intake", "helper", "strikes", "timeout", "notify"];

    public string? Home { get; } = home;

    /// <summary>The folder a proposal is written in, under the home.</summary>
    public static string FolderOf(string home) => Path.Combine(home, "help", "proposals");

    /// <summary>The driver's named home first — where what this would change lives — then the account's (D63).</summary>
    public static HelpProposalBox FromEnvironment() =>
        new(RuleProposalBox.FromEnvironment().Home);

    /// <summary>Why a setting is no door's shape, or null when it is one.</summary>
    public static string? Refusal(SettingChange change)
    {
        var door = change.Door;
        if (!Doors.Contains(door)) return $"`{door}` is not a door — one of {string.Join(", ", Doors.Select(d => $"`{d}`"))}.";

        var value = change.Value?.Trim();
        var named = !string.IsNullOrWhiteSpace(change.Target);
        var circle = !string.IsNullOrWhiteSpace(change.Workspace);
        switch (door)
        {
            case "drive" or "undrive" or "hold" or "resume":
                return named ? null : $"`{door}` names the repository it is for.";
            case "trees":
                if (!named) return "`trees` names the repository it is for.";
                return value is "on" or "off" ? null : "`trees` is set `on` or `off`.";
            case "line" or "landing":
                if (named == circle) return $"a {door} is set for a repository or a workspace — name exactly one.";
                if (door == "line") return string.IsNullOrWhiteSpace(value) ? "a line is a branch, or `--clear`." : null;
                return value is "merge" or "--clear" or "merge --tidy" || (value?.StartsWith("branch ", StringComparison.Ordinal) ?? false)
                    ? null
                    : "a landing is `merge`, `branch <pattern>` (with `--tidy` to remove the tree once landed, and `--plugin <id>` for an installed plugin that pushes it and opens the pull request), or `--clear`.";
            case "intake" or "helper":
                return string.IsNullOrWhiteSpace(value) ? $"`{door}` is set to an agent, or `off`." : null;
            case "strikes":
                return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _) ? null : "`strikes` is a whole number, 0 or more.";
            case "timeout":
                return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && minutes >= 1
                    ? null
                    : "`timeout` is a whole number of minutes, 1 or more.";
            default: // notify
                return value is "on" or "off" ? null : "`notify` is set `on` or `off`.";
        }
    }

    /// <summary>Write one setting proposal, or refuse it — answered as the sentence the agent reads.</summary>
    public (string? Id, string Message) ProposeSetting(SettingChange change, string why, string? session, DateTimeOffset at)
    {
        var normal = change with { Door = change.Door.Trim().ToLowerInvariant(), Value = change.Value?.Trim() };
        if (string.IsNullOrWhiteSpace(why)) return (null, "A proposal needs its reason: what the person asked, and what the change would do. Nothing was proposed.");
        if (Refusal(normal) is { } refused) return (null, $"{Capital(refused)} Nothing was proposed.");
        return Write(writer =>
        {
            writer.WriteString("kind", "setting");
            writer.WriteString("door", normal.Door);
            Nullable(writer, "target", Blank(normal.Target));
            Nullable(writer, "workspace", Blank(normal.Workspace));
            Nullable(writer, "value", Blank(normal.Value));
            writer.WriteNull("sentence");
        }, why, session, at);
    }

    /// <summary>Write one ask proposal — something to start, which becomes an ask when the person applies it.</summary>
    public (string? Id, string Message) ProposeAsk(string sentence, string workspace, string why, string? session, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(why)) return (null, "A proposal needs its reason: what the person asked, and what the change would do. Nothing was proposed.");
        if (string.IsNullOrWhiteSpace(sentence)) return (null, "An ask needs its words: what is to be done, as the person would say it. Nothing was proposed.");
        if (string.IsNullOrWhiteSpace(workspace)) return (null, "An ask is made at a workspace — name it. Nothing was proposed.");
        return Write(writer =>
        {
            writer.WriteString("kind", "ask");
            writer.WriteString("door", "ask");
            writer.WriteNull("target");
            writer.WriteString("workspace", workspace.Trim());
            writer.WriteNull("value");
            writer.WriteString("sentence", sentence.Trim());
        }, why, session, at);
    }

    /// <summary>
    /// Write one agent proposal (HELP6): <c>update</c> to its newest release, or <c>pin</c> to one exact
    /// version — the Agents screen's two presses, which the driver judges by that door's roster.
    /// </summary>
    public (string? Id, string Message) ProposeAgent(string action, string agent, string? version, string why, string? session, DateTimeOffset at)
    {
        var door = action.Trim().ToLowerInvariant();
        var named = Blank(agent);
        var release = Blank(version);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = door is not ("update" or "pin")
            ? "an agent's change is `update` or `pin`: update moves it to its newest release, and pin to one exact version."
            : named is null ? "the change names the agent it is for, as `daoris agent` spells it."
            : door == "pin" && release is null ? "`pin` names the version: one exact release, such as 2.1.300."
            : door == "update" && release is not null ? "`update` names no version — it moves to the newest release, and `pin` names one."
            : Word(named, "an agent") ?? (release is null ? null : Word(release, "a version"));
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "agent");
            writer.WriteString("door", door);
            writer.WriteString("target", named);
            writer.WriteNull("workspace");
            Nullable(writer, "value", release);
            writer.WriteNull("sentence");
        }, why, session, at);
    }

    /// <summary>
    /// Write one delete proposal (HELP6): a quest or an ask made by mistake, by its id. Whether the record
    /// may go is the service's own reading, which the driver asks before the person sees the card.
    /// </summary>
    public (string? Id, string Message) ProposeDelete(string? quest, string? ask, string why, string? session, DateTimeOffset at)
    {
        var questId = Blank(quest)?.TrimStart('#');
        var askId = Blank(ask)?.TrimStart('#');
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = (questId is null) == (askId is null)
            ? "a delete names a quest or an ask — name exactly one, by its id."
            : Word(questId ?? askId!, "an id");
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "delete");
            writer.WriteString("door", questId is not null ? "quest" : "ask");
            writer.WriteString("target", questId ?? askId);
            writer.WriteNull("workspace");
            writer.WriteNull("value");
            writer.WriteNull("sentence");
        }, why, session, at);
    }

    /// <summary>
    /// Write one proposal to change an account's own model and effort (HELP6): the Agents screen's
    /// <i>Model &amp; effort</i>, which the driver judges with that route's own rules.
    /// </summary>
    /// <param name="model">A model the tool reads, or <c>unset</c> for the tool's own default; null leaves it.</param>
    /// <param name="effort">An effort the tool's settings keep, or <c>unset</c>; null leaves it.</param>
    public (string? Id, string Message) ProposeAgentSettings(
        string agent, string account, string? model, string? effort, string why, string? session, DateTimeOffset at)
    {
        var named = Blank(agent);
        var whose = Blank(account);
        var chosenModel = Blank(model);
        var chosenEffort = Blank(effort);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = named is null ? "the change names the agent whose account it is, as `daoris agent` spells it."
            : whose is null ? "the change names the account — one of Daoris's accounts for that agent; the tool's own configuration home is never touched."
            : chosenModel is null && chosenEffort is null ? "the change sets a model, an effort, or both — `unset` returns either to the tool's own default."
            : Word(named, "an agent") ?? Word(whose, "an account")
              ?? (chosenModel is null ? null : Word(chosenModel, "a model"))
              ?? (chosenEffort is null ? null : Word(chosenEffort, "an effort"));
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "account");
            writer.WriteString("door", "settings");
            writer.WriteString("target", named);
            writer.WriteNull("workspace");
            writer.WriteNull("value");
            writer.WriteNull("sentence");
            writer.WriteString("account", whose);
            Nullable(writer, "model", chosenModel);
            Nullable(writer, "effort", chosenEffort);
        }, why, session, at);
    }

    /// <summary>
    /// Write one proposal to take the person to a screen (HELP6): a view, a domain of Settings, and a part
    /// of it or a setup step. It changes nothing; which places exist is the driver's to judge.
    /// </summary>
    public (string? Id, string Message) ProposeGo(string view, string? domain, string? part, string why, string? session, DateTimeOffset at)
    {
        var where = Blank(view)?.ToLowerInvariant();
        var within = Blank(domain)?.ToLowerInvariant();
        var piece = Blank(part)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = where is null ? "a screen names the view it is on — overview, sessions, quests, projects, map, convergence, search or settings."
            : within is not null && where != "settings" ? "a domain is a part of Settings — name `settings` as the view."
            : Word(where, "a view") ?? (within is null ? null : Word(within, "a domain")) ?? (piece is null ? null : Word(piece, "a part"));
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "go");
            writer.WriteString("door", "go");
            writer.WriteString("target", where);
            writer.WriteNull("workspace");
            writer.WriteNull("value");
            writer.WriteNull("sentence");
            Nullable(writer, "domain", within);
            Nullable(writer, "part", piece);
        }, why, session, at);
    }

    /// <summary>
    /// Write one plugin proposal (PLUG9): <c>add</c> a plugin that has landed, from its folder in the
    /// checkout of the repository that holds it — <c>daoris plugin add</c>'s copy — or <c>enable</c> or
    /// <c>disable</c> one installed here, Settings → Plugins' switch. Whether the folder holds a sound
    /// manifest, and whether the id is installed, is the driver's, read with the catalogue's own reader.
    /// </summary>
    /// <param name="id">For enable, disable and update: the installed plugin's id.</param>
    /// <param name="folder">For add: the folder from the repository's checkout root, or a whole path the person gave.</param>
    /// <param name="repository">For add: the repository whose checkout holds it; null only for a whole path.</param>
    /// <param name="offer">For add, instead of a folder: one of the install's own plugins, by its id (PLUG9 d) — never a path.</param>
    /// <remarks>
    /// Since PLUG9 (c) and (d) (D103) an <c>add</c> may name an offer, and an <c>update</c> takes a newer copy of an
    /// installed plugin from where it came from. Whether the install offers it, and whether the plugin recorded
    /// where it came from, is the driver's, which reads both with its own code.
    /// </remarks>
    public (string? Id, string Message) ProposePlugin(
        string action, string? id, string? folder, string? repository, string why, string? session, DateTimeOffset at,
        string? offer = null)
    {
        var door = action.Trim().ToLowerInvariant();
        var named = Blank(id);
        var where = Blank(folder);
        var holder = Blank(repository);
        var offered = Blank(offer);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = door is not ("add" or "enable" or "disable" or "update")
            ? "a plugin's change is `add`, `enable`, `disable` or `update`: add copies a plugin that has landed, or one of "
              + "Daoris's own, into Daoris's home; enable or disable switches one installed here; update takes a newer copy "
              + "from where an installed one came from."
            : door == "add" ? AddRefusal(named, where, holder, offered)
            : named is null ? $"`{door}` names the plugin by its id, as the room lists the plugins installed here."
            : where is not null || holder is not null || offered is not null
                ? $"`{door}` names only the plugin's id — a folder or an offer is for `add`."
            : Word(named, "an id");
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "plugin");
            writer.WriteString("door", door);
            Nullable(writer, "target", door == "add" ? null : named);
            writer.WriteNull("workspace");
            writer.WriteNull("value");
            writer.WriteNull("sentence");
            Nullable(writer, "repository", door == "add" ? holder : null);
            Nullable(writer, "folder", door == "add" ? where : null);
            Nullable(writer, "offer", door == "add" ? offered : null);
        }, why, session, at);
    }

    /// <summary>
    /// Write one hand-off proposal (WSR5b): a branch a landing made, handed to a landing plugin afterwards —
    /// the review's <i>hand it to</i> and <c>daoris-driver trees hand</c>. Whether the record holds that branch,
    /// and whether the plugin can land work there, is the driver's.
    /// </summary>
    /// <param name="target">The session that landed the branch, or the branch.</param>
    /// <param name="repository">The repository it is in, where a branch's name alone is in several; null otherwise.</param>
    /// <param name="plugin">The plugin to hand it to, where the repository's landing rule names none; null for the rule's.</param>
    public (string? Id, string Message) ProposeHand(string target, string? repository, string? plugin, string why, string? session, DateTimeOffset at)
    {
        var named = Blank(target);
        var holder = Blank(repository);
        var to = Blank(plugin);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = named is null ? "a hand-off names the session that landed the branch, or the branch itself."
            : Word(named, "a session or a branch") ?? (holder is null ? null : Word(holder, "a repository"))
              ?? (to is null || PluginId.IsMatch(to) ? null
                  : $"`{to}` is not a plugin id — one is lowercase letters, digits, dots and dashes, as the room lists the plugins installed here.");
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "hand");
            writer.WriteString("door", "hand");
            writer.WriteString("target", named);
            writer.WriteNull("workspace");
            Nullable(writer, "value", to);
            writer.WriteNull("sentence");
            Nullable(writer, "repository", holder);
        }, why, session, at);
    }

    /// <summary>What a plugin's id may be — the catalogue's own shape (the driver's <c>LandingRules</c> holds the same).</summary>
    private static readonly System.Text.RegularExpressions.Regex PluginId =
        new("^[a-z0-9][a-z0-9.-]*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>Why an add is not an add's shape: an offer by its id, or a folder, no id, and a repository's checkout it stays inside.</summary>
    private static string? AddRefusal(string? id, string? folder, string? repository, string? offer)
    {
        if (offer is not null)
        {
            if (folder is not null || repository is not null)
            {
                return "`add` names an offer or a folder, never both: an offer is one of Daoris's own plugins by its id, and a folder one that has landed in a repository.";
            }

            return id is not null ? "`add` names no id — an offer is named in `offer`, and a folder's id is its manifest's." : Word(offer, "an offer");
        }

        if (folder is null) return "`add` names the plugin's folder: where it landed, in the checkout of the repository that holds it — or one of Daoris's own plugins by its id, in `offer`.";
        if (id is not null) return "`add` names no id — a plugin's id is its manifest's, read from its folder.";
        if (repository is null)
        {
            return Path.IsPathRooted(folder)
                ? null
                : "name the repository whose checkout holds it, with its folder there — a whole path only when the person gave one.";
        }

        if (Word(repository, "a repository") is { } word) return word;
        if (Path.IsPathRooted(folder)) return "a folder in a repository is written from its checkout's root, like `plugins/quiet-hours`, never as a whole path.";
        return folder.Split('/', '\\').Contains("..")
            ? "a folder in a repository stays inside its checkout — `..` leaves it."
            : null;
    }

    private static readonly (string? Id, string Message) NoReason =
        (null, "A proposal needs its reason: what the person asked, and what the change would do. Nothing was proposed.");

    /// <summary>Why a name is not one word, or null when it is: every name a proposal carries is.</summary>
    private static string? Word(string value, string what) =>
        value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)) ? $"{what} is one word — `{value}` is not." : null;

    private (string? Id, string Message) Write(Action<Utf8JsonWriter> body, string why, string? session, DateTimeOffset at)
    {
        if (Home is null) return (null, $"{Capital(DaorisHome.Sentence)} Nothing was proposed.");

        var id = Guid.NewGuid().ToString("N")[..8];
        var directory = FolderOf(Home);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{id}.json");

        // Hand-rolled, for the reason every store here is: nothing may stop working under AOT.
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("id", id);
            writer.WriteString("proposed", at.ToString("O"));
            writer.WriteStartObject("by");
            Nullable(writer, "session", session);
            writer.WriteEndObject();
            body(writer);
            writer.WriteString("why", why.Trim());
            writer.WriteString("state", "proposed");
            writer.WriteNull("note");
            writer.WriteEndObject();
        }

        var beside = path + ".tmp";
        File.WriteAllText(beside, Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));
        File.Move(beside, path);

        return (id,
            $"Proposed `#{id}`. The person sees it as a card saying what it changes and the command that does the "
            + "same, with Apply and Not now; nothing changes until they press Apply, and their answer comes back to "
            + "you as their next message. If the driver finds the route would refuse it, you are told why instead.");
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Nullable(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) writer.WriteNull(name);
        else writer.WriteString(name, value);
    }

    private static string Capital(string sentence) =>
        sentence.Length == 0 ? sentence : char.ToUpperInvariant(sentence[0]) + sentence[1..];
}
