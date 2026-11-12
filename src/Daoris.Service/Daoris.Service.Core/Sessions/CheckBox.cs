using System.Text;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// Where a working session's ask for a check is written: <c>&lt;askId&gt;.ask.json</c> under the home's <c>checks</c> folder, the file the
/// contract with the driver's broker (<c>CheckFiles</c>), never changed once written. The state and the log beside it are the driver's alone.
/// </summary>
/// <remarks>
/// This judges what only the record can say: that the connector speaks for a working session of this machine's. Which tree it runs in,
/// the machine's admission and the one-at-a-time order are the driver's, which answers through the state file; the driver's <c>CheckFiles</c>
/// and this share no code, and each side's tests hold the same shape. Never fed to a remote or served over HTTP.
/// </remarks>
/// <param name="home">The home the files go under, or null when there is none: every ask is then refused.</param>
public sealed class CheckBox(string? home)
{
    /// <summary>The most a command of the asker's own may name, in words and in characters each.</summary>
    public const int MaxWords = 64;

    public const int MaxWord = 1000;

    public string? Home { get; } = home;

    public static CheckBox FromEnvironment() => new(RuleProposalBox.FromEnvironment().Home);

    /// <summary>Where one ask's file is: <c>&lt;home&gt;/checks/&lt;id&gt;.ask.json</c>.</summary>
    public static string PathOf(string home, string id) => DaorisHome.Entry(home, HomeEntry.Checks, $"{id}.ask.json");

    /// <summary>
    /// Write an ask for the session the connector names, or refuse it: answered as the sentence an agent reads. A gate by name, a command
    /// of its own, or neither (every heavy gate) is one check; a gate and a command together are refused.
    /// </summary>
    public (string? Id, string Message) Ask(Session? asker, string? named, string? gate, IReadOnlyList<string>? command, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(named)) return (null, "This connector speaks for no session the driver started here, so there is no tree to run a check in. Nothing was asked.");
        if (asker is not { Origin: null }) return (null, $"No session `{named.Trim()}` of this machine's is held to run a check for. Nothing was asked.");
        if (asker.Opinion is not null || asker.Serves is not null || asker.Ask is not null || SessionLedger.IsHelp(asker))
        {
            return (null, $"Session `{asker.Id}` works in no tree of its own, so there is nothing to run a check in. Nothing was asked.");
        }

        var chosen = string.IsNullOrWhiteSpace(gate) ? null : gate.Trim();
        var words = command?.Where(word => !string.IsNullOrWhiteSpace(word)).ToList();
        if (chosen is not null && words is { Count: > 0 }) return (null, "Ask a declared gate by name or a command of your own, not both. Nothing was asked.");
        if (chosen is not null && chosen.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))) return (null, $"A gate is named as one word, as the repository declares it, and `{chosen}` is not. Nothing was asked.");
        if (words is { Count: > MaxWords } || words?.Any(word => word.Length > MaxWord) == true)
        {
            return (null, $"A command is at most {MaxWords} words of {MaxWord} characters each. Nothing was asked.");
        }

        if (Home is null) return (null, $"{char.ToUpperInvariant(DaorisHome.Sentence[0])}{DaorisHome.Sentence[1..]} Nothing was asked.");

        var id = Guid.NewGuid().ToString("N")[..8];
        var path = PathOf(Home, id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        using (var stream = File.Create(temporary))
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("id", id);
            writer.WriteString("session", asker.Id);
            if (chosen is not null) writer.WriteString("gate", chosen);
            if (words is { Count: > 0 })
            {
                writer.WriteStartArray("command");
                foreach (var word in words) writer.WriteStringValue(word);
                writer.WriteEndArray();
            }

            writer.WriteString("by", "agent");
            writer.WriteString("at", Moments.Write(at));
            writer.WriteEndObject();
        }

        File.Move(temporary, path, overwrite: true);
        return (id,
            $"Asked `#{id}`: Daoris runs it in your tree under this machine's one-check-at-a-time admission, once your turn has ended. "
            + "End your turn now, saying in your last message what you asked: the verdict comes back to you as your next turn, and the wait costs nothing.");
    }
}
