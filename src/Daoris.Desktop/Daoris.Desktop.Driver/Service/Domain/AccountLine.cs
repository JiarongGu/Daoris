namespace Daoris.Driver;

/// <summary>
/// The machine log's account lines (TOOL4d, D125 §5.4, D94 §4): <c>account.limited</c>, <c>starts.waiting</c> and, since TOOL4f,
/// <c>account.rotated</c>, each with
/// the fields its catalogue gives it and nothing else. An account is its profile name, null for the tool's own home:
/// never a key, a key's handle, who signed in, or the agent's sentence.
/// </summary>
public sealed record AccountLine(string Event, IReadOnlyList<(string Key, object? Value)> Data);
