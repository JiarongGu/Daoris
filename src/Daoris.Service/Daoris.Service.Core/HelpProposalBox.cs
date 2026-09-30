using System.Text;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// Where Ask Daoris's proposals are written (HELP1c, D89): one file each, under the driver's home, for
/// the person to apply or not.
/// </summary>
/// <remarks>
/// <para><b>One writer per kind (MOD6)</b>, each a door a screen or a terminal already has, each in a file of
/// its own (<c>HelpProposalBox.&lt;Kind&gt;.cs</c>) and listed in <see cref="Kinds"/>: its <c>Propose…</c>
/// method, which the connector's tool calls, checks the kind's shape and hands its fields to <see cref="Write"/>,
/// which writes every file's head and tail. Every file carries <c>target</c>, <c>workspace</c>, <c>value</c> and
/// <c>sentence</c>, null where the kind has none; a kind's own fields follow them.</para>
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
/// code with this, and each side's tests hold the same shape — and the same table of kinds.</para>
/// </remarks>
public sealed partial class HelpProposalBox(string? home)
{
    /// <summary>
    /// The kinds this box writes, each with the connector's tool that proposes it, in the order the room allows
    /// them (MOD6). A new kind is a file <c>HelpProposalBox.&lt;Kind&gt;.cs</c> and a line here; the driver's
    /// <c>HelpProposalKinds</c> lists the same.
    /// </summary>
    public static readonly IReadOnlyList<(string Kind, string Tool)> Kinds =
    [
        ("setting", "setting_propose"),
        ("ask", "ask_propose"),
        ("agent", "agent_propose"),
        ("delete", "delete_propose"),
        ("account", "agent_settings_propose"),
        ("go", "go_propose"),
        ("plugin", "plugin_propose"),
        ("hand", "hand_propose"),
        ("browser", "browser_propose"),
    ];

    public string? Home { get; } = home;

    /// <summary>The folder a proposal is written in, under the home.</summary>
    public static string FolderOf(string home) => Path.Combine(home, "help", "proposals");

    /// <summary>The driver's named home first — where what this would change lives — then the account's (D63).</summary>
    public static HelpProposalBox FromEnvironment() =>
        new(RuleProposalBox.FromEnvironment().Home);

    private static readonly (string? Id, string Message) NoReason =
        (null, "A proposal needs its reason: what the person asked, and what the change would do. Nothing was proposed.");

    /// <summary>Why a name is not one word, or null when it is: every name a proposal carries is.</summary>
    private static string? Word(string value, string what) =>
        value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)) ? $"{what} is one word — `{value}` is not." : null;

    /// <summary>One proposal's file: its id, when and by whom, the kind's own fields, its reason and its state.</summary>
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
