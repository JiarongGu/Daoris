using System.Text.Json;

namespace Daoris.Contracts.Brainstorms;

/// <summary>
/// One brainstorm as its records stand: <c>State</c> is <c>asked</c> before the driver's look froze its packet, then the driver's own
/// (<c>frozen</c>, <c>running</c>, <c>ended</c>); <c>Packet</c> the frozen packet's digest, <c>Sent</c> the digest the person sent.
/// A meeting carries its <c>Kind</c> and the <c>Round</c> its state is of; a brainstorm leaves both out.
/// </summary>
public sealed record BrainstormResponse(
    string Id, string Question, string? Repository, IReadOnlyList<string> Agents, DateTimeOffset At, string State, string? Code,
    string? Packet, string? Sent, string? Kind = null, int? Round = null);

/// <summary>One brainstorm shown: the packet as frozen, whole and as written (null before the look froze one), and the document's Markdown.</summary>
public sealed record BrainstormShownResponse(BrainstormResponse Brainstorm, JsonElement? Frozen, string? Document);

/// <summary>A press's answer: the brainstorm as it now stands, and the desk's sentence.</summary>
public sealed record BrainstormActionResponse(BrainstormResponse? Brainstorm, string Message);
