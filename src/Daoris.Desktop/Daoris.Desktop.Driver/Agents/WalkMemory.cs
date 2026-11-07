using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What one start's walk learned across its passes: what its own status questions answered, by account, and the
/// accounts left unknown for it, whose lock another question kept past its patience.
/// </summary>
internal sealed class WalkMemory
{
    public Dictionary<string, LoginState> Said { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Unknown { get; } = new(StringComparer.OrdinalIgnoreCase);
}
