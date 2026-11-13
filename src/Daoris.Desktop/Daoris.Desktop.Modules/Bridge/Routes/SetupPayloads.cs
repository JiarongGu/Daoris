using System.Text.Json;

namespace Daoris.Desktop;

// The payloads of SetupRoutes, each named for its route.

/// <summary>
/// A place's plan: the <c>Repositories</c> it covers, or one <c>Repository</c>, and the parts asked. <c>Said</c> asks for the verb's words.
/// </summary>
public sealed record PlacePlanRead(
    IReadOnlyList<string>? Repositories, string? Repository, string? Preset, bool Opinion = false, bool Look = false, bool Adoption = false,
    string? Builds = null, bool Drive = false, long? AtOnce = null, long? Pilot = null, bool Said = false, string? Room = null, string? Purpose = null);

/// <summary>One acceptance a place's apply makes.</summary>
public sealed record PlaceAcceptSent(string? Repository);

/// <summary>
/// A place's apply: the plan's own asks, its <c>Digest</c> and the acceptances chosen. <c>Repository</c> is read only to be refused:
/// an apply names its places as <c>Repositories</c>.
/// </summary>
public sealed record PlaceApplyPress(
    IReadOnlyList<string>? Repositories, JsonElement? Repository, string? Preset, string? Digest, IReadOnlyList<PlaceAcceptSent>? Accept,
    bool Opinion = false, bool Look = false, bool Adoption = false, string? Builds = null, bool Drive = false, long? AtOnce = null, long? Pilot = null,
    bool Said = false, string? Room = null, string? Purpose = null);

/// <summary>The repository a reconcile plans or presses.</summary>
public sealed record ReconcileAsk(string? Repository);
