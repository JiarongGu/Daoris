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
                    : "a landing is `merge`, `branch <pattern>` (with `--tidy` to remove the tree once landed), or `--clear`.";
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
