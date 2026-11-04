using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using static Daoris.Driver.AccountFileNodes;

namespace Daoris.Driver;

/// <summary>
/// One turn as the history keeps it (USAGECAP1a2, D179 point 2): whose it was, when, its four counts each null where its door
/// said none, the models they were spent on, and what its door's report means.
/// </summary>
/// <param name="Agent">The account's owner (AGT7), or null where no start bound the session.</param>
/// <param name="Account">The account's opaque local id, <see cref="AccountWindows.Own"/> for the tool's own sign-in, or null.</param>
/// <param name="Epoch">The account's binding epoch when it was kept; null with no account, or where the epochs file did not read.</param>
/// <param name="Door">The adapter the session ran through, or null where no start bound it.</param>
/// <param name="Seq">The turn's event in its session's record (<see cref="SessionEvent.Seq"/>): with the session, its key.</param>
/// <param name="Started">When the session took the turn's ask, or null where this process did not see it.</param>
/// <param name="Ended">When the turn's end was recorded.</param>
/// <param name="Tokens">The turn's four counts as its door reported them, each null where it said none: absent is never zero (D57).</param>
/// <param name="Model">The one model, <see cref="TurnModels.Mixed"/> or <see cref="TurnModels.Unknown"/>.</param>
/// <param name="Split">Each model's share where the door's split agreed with the total, else null.</param>
/// <param name="Attribution">Where the model and the split came from (<see cref="TurnModels.From"/>), or null.</param>
/// <param name="Whole">False where a helper model's tokens fall outside the total; null where no split says.</param>
/// <param name="Cancelled">Whether the turn was stopped: its counts are as reported, and may be none.</param>
/// <param name="Meaning">What its door's counts mean: <see cref="UsageHistory.PerTurn"/> or <see cref="UsageHistory.MeaningUnknown"/>.</param>
/// <param name="Reader">The version of the reading of its tokens and models.</param>
public sealed record KeptTurn(
    string? Agent, string? Account, int? Epoch, string? Door, string Session, long Seq, DateTimeOffset? Started, DateTimeOffset Ended,
    TurnTokens Tokens, string Model, IReadOnlyList<ModelTokens>? Split, string? Attribution, bool? Whole, bool Cancelled, string Meaning,
    int Reader)
{
    public string? Actor { get; init; }

    /// <summary>The brainstorm a pass's turn was spent for (BRAINSTORM1c, D176 point 7); null for every other session.</summary>
    public string? Brainstorm { get; init; }

    /// <summary>The role's choice the turn was spent on, as its policy keys it, the tuple's JSON (STAFFPOLICY1e2); null where its start chose none.</summary>
    public string? Choice { get; init; }
}
