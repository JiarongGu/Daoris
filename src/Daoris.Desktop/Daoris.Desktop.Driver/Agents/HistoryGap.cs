using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using static Daoris.Driver.AccountFileNodes;

namespace Daoris.Driver;

/// <summary>
/// A stretch the history does not hold: months pruned for their <c>age</c> or the log's <c>size</c>, or the
/// rest of a month whose file is <c>full</c>.
/// </summary>
public sealed record HistoryGap(DateTimeOffset From, DateTimeOffset To, string Why, IReadOnlyList<string> Segments);
