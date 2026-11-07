namespace Daoris.Desktop;

/// <summary>*Open* in This Daoris: the id of a folder row or a file row the read gave, never a path.</summary>
public sealed record OpenFolderPress(string? Folder);

/// <summary>*Put on my PATH* (<c>On</c> true) or *Take off my PATH* (false), on the account's alone.</summary>
public sealed record PathPress(bool? On);

/// <summary>*Change the service's address*: an address of this machine for the account's variable, or none for the default again.</summary>
public sealed record ServiceAddressPress(string? Url);

/// <summary>The wiring card;<c>Measure</c> starts the walk of each kind's size.</summary>
public sealed record WiringRead(bool Measure = false);

/// <summary>
/// The Disk space card. <c>Measure</c> is <c>true</c> to count every kind or a list of kinds' ids to count those, one after another in the
/// background; none counts nothing.
/// </summary>
public sealed record SpaceRead(System.Text.Json.JsonElement? Measure);

/// <summary>
/// *Free up space*: <c>Plan</c> lists what could go and changes nothing; the press names the listed <c>Units</c> and those that could go
/// <c>Early</c>, each <c>{ kind, id, why, own, class, ref }</c> as the list gave it.
/// </summary>
public sealed record SpaceFreePress(System.Text.Json.JsonElement? Units, System.Text.Json.JsonElement? Early, bool Plan = false);

/// <summary>One downloaded version the Downloads row listed: an agent's or a tool's, whether its download <c>Unfinished</c>.</summary>
public sealed record DownloadVersionAsked(string? Kind, string? Name, string? Version, bool Unfinished = false);

/// <summary>The downloaded versions removed, each judged again right before it goes.</summary>
public sealed record DownloadsClearPress(IReadOnlyList<DownloadVersionAsked>? Versions);
