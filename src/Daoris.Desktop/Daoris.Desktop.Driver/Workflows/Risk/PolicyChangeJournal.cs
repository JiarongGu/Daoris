using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>
/// One <c>policy.changed</c> line: a change to the policy applied, with who proposed it, the digest it is bound to, the turn of the owner's that
/// applied it, the edits, the waits they opened or released, and the policy's revision before and after.
/// </summary>
/// <param name="Door">The owner's channel it is applied through: <c>assistant</c> or <c>card</c>.</param>
/// <param name="Turn">The owner's turn that confirmed it, as <see cref="OwnerTurn.Id"/> names it.</param>
public sealed record PolicyChangeLine(
    DateTimeOffset At, string Door, string Proposal, string Digest, string Session, string Turn, DateTimeOffset TurnAt, string TurnText,
    IReadOnlyList<PolicyEdit> Edits, IReadOnlyList<PolicyShift> Shifts, string RevisionBefore, string RevisionAfter)
{
    public const string Event = "policy.changed";
    public const string Assistant = "assistant";

    /// <summary>The door and the turn of a change the owner confirmed on their own card in the window.</summary>
    public const string Pressed = "card";

    public const string Window = "window";

    public const string Cli = "cli";
}

/// <summary>
/// The home's change journal of the policy (<c>data/workflows/risk/changes.jsonl</c>): every applied change to the policy, one line each, appended
/// when it is written; history, so a purge moves it out with the judge's journal.
/// </summary>
public static class PolicyChangeJournal
{
    private static readonly object Writing = new();
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    public static string PathOf(string home) => Path.Combine(DaorisHome.Entry(home, HomeEntry.Workflows, RiskJournal.Folder), "changes.jsonl");

    public static void Append(string home, PolicyChangeLine line)
    {
        var path = PathOf(home);
        var text = JsonSerializer.Serialize(new { @event = PolicyChangeLine.Event, line }, Wire);
        lock (Writing)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, text + "\n");
        }
    }

    /// <summary>Every line the journal holds, oldest first; a line that does not read is passed over.</summary>
    public static IReadOnlyList<PolicyChangeLine> Read(string home)
    {
        var path = PathOf(home);
        if (!File.Exists(path)) return [];
        List<string> lines = [];
        lock (Writing)
        {
            using var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
            while (reader.ReadLine() is { } each) lines.Add(each);
        }

        var read = new List<PolicyChangeLine>();
        foreach (var each in lines.Where(each => each.Length > 0))
        {
            try
            {
                using var document = JsonDocument.Parse(each);
                if (document.RootElement.TryGetProperty("line", out var body) && body.Deserialize<PolicyChangeLine>(Wire) is { } line) read.Add(line);
            }
            catch (JsonException)
            {
            }
        }

        return read;
    }
}
