using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What one pass of a start's walk found (PARALLEL1c): a hold to answer with; an account to ask first, then pass again
/// (<see cref="Owed"/> where TOOL6h's one fresh reading is what asks it); or the account chosen, at <see cref="At"/> of the
/// states it read, counted as start <see cref="Number"/> where the start counts.
/// </summary>
internal sealed record WalkPass(HarnessSelection? Held = null, string? Ask = null, bool Owed = false)
{
    public List<AccountState> States { get; init; } = [];

    public List<string?> Order { get; init; } = [];

    public IReadOnlyDictionary<string, AccountFacts> Facts { get; init; } = HarnessRoster.NoFacts;

    public DateTimeOffset Now { get; init; }

    public int At { get; init; } = -1;

    public long Number { get; init; }
}
