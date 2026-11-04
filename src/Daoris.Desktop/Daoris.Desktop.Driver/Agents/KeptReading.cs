using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using static Daoris.Driver.AccountFileNodes;

namespace Daoris.Driver;

/// <summary>One reading as the history keeps it (USAGECAP1a1): its window, where it came from, and its account's epoch.</summary>
/// <param name="Epoch">The account's binding epoch when it was kept; null where the epochs file did not read.</param>
public sealed record KeptReading(
    string Agent, string Account, int? Epoch, string Door, WindowSeen Window, string Source, DateTimeOffset Seen, string? Session,
    string? Plan, string? PlanFrom, int Reader);
