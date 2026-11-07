using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What a walk judges each account's offer reading for: the model the start asks, the
/// accounts this start already passed for it, and its own count, left out of the facts so a re-ask ranks without its own load.
/// </summary>
internal sealed record OfferAsk(string? Model, IReadOnlyCollection<string>? Passed, long Except)
{
    public static OfferAsk None { get; } = new(null, null, 0);
}
