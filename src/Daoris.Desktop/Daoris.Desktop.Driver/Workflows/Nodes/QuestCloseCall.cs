using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A <c>quest.close</c> attempt's close as its record keeps it (§E5 step 1): the token, the quests named, and a refusal's words.</summary>
public sealed record QuestCloseCall(string Token, IReadOnlyList<QuestCloseItem> Quests)
{
    /// <summary>The door's words when it refused the close, kept so the refusal is said as it came; null otherwise.</summary>
    public string? Words { get; init; }

    public bool Equals(QuestCloseCall? other) =>
        other is not null && Token == other.Token && Words == other.Words && Quests.SequenceEqual(other.Quests);

    public override int GetHashCode() => HashCode.Combine(Token, Quests.Count, Words);
}
