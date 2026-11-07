using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>An agent's accounts read for their usage.</summary>
public sealed record AccountsReadPress(string Door);

/// <summary>
/// An act on an agent's accounts: <c>order</c> reads <c>Accounts</c>, <c>use</c> the settings named (or <c>Clear</c>), <c>said</c> the
/// receipts' <c>Ids</c>, <c>ready</c> the <c>Profile</c> (or <c>Own</c>). A field left out of a <c>use</c> is no change; <c>Curve</c> is
/// the curve as sent, read by the curve's own reader.
/// </summary>
public sealed record AccountUsePress(
    string Action, string Door, string? Workspace, IReadOnlyList<string>? Accounts, IReadOnlyList<string>? Ids, string? Profile,
    string? Use, string? Keep, string? Switch, bool? Early, long? Near, JsonElement? Curve,
    bool Clear = false, bool Own = false, bool NoKeep = false, bool NoCurve = false);
