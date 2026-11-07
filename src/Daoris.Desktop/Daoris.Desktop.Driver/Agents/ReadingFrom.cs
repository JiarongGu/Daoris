using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using static Daoris.Driver.AccountFileNodes;

namespace Daoris.Driver;

/// <summary>Where a reading came from: whose, through what, read by which reader, and when.</summary>
/// <param name="Agent">The account's owner.</param>
/// <param name="Account">The account's opaque local id: its folder's name, or <see cref="AccountWindows.Own"/> for the tool's own.</param>
/// <param name="Door">The adapter whose session carried it, or whose question asked it.</param>
/// <param name="Source">
/// <see cref="UsageHistory.SourceFrame"/> for a session's frame on either door, or the question asked: <c>get_usage</c>, or
/// Codex's <c>account/rateLimits/read</c>.
/// </param>
/// <param name="Reader">The version of the reader that parsed it, its own per source.</param>
/// <param name="Seen">When it is observed: the moment <c>windows.json</c> keeps beside it.</param>
/// <param name="Session">The session where one said it; null for a read.</param>
/// <param name="Plan">The plan as last read (<c>subscription_type</c>'s word, never who or the organisation), or null.</param>
/// <param name="PlanFrom">How the plan is read: <c>get_usage</c>, or <see cref="UsageHistory.PlanFromStatus"/>.</param>
public sealed record ReadingFrom(
    string Agent, string Account, string Door, string Source, int Reader, DateTimeOffset Seen, string? Session = null,
    string? Plan = null, string? PlanFrom = null);
