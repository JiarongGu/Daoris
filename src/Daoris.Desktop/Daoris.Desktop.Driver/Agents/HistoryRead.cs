using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using static Daoris.Driver.AccountFileNodes;

namespace Daoris.Driver;

/// <summary>What the history holds, in the order it was written: its readings, its gaps, and the lines that did not read.</summary>
public sealed record HistoryRead(IReadOnlyList<KeptReading> Readings, IReadOnlyList<HistoryGap> Gaps, int Unread);
