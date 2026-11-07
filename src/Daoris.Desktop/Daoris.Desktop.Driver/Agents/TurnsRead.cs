using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using static Daoris.Driver.AccountFileNodes;

namespace Daoris.Driver;

/// <summary>What the turns' log holds, in the order it is written: its turns, its gaps, and the lines that did not read.</summary>
public sealed record TurnsRead(IReadOnlyList<KeptTurn> Turns, IReadOnlyList<HistoryGap> Gaps, int Unread);
