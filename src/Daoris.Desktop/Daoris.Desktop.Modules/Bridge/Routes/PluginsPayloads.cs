namespace Daoris.Desktop;

/// <summary>An installed plugin by its id.</summary>
public sealed record PluginIdRead(string Id);

/// <summary>An installed plugin's activity since a span such as 30m, 2h or 3d; none is the page's opening period.</summary>
public sealed record PluginActivityRead(string Id, string? Since);

/// <summary>What a folder's plugin would run, or the folder installed.</summary>
public sealed record PluginFolderPress(string Folder);

/// <summary>A plugin's <c>Which</c> folder opened; an installed plugin's takes its <c>Id</c>.</summary>
public sealed record PluginOpenFolderPress(string Which, string? Id);

/// <summary>An update planned, or with <c>Apply</c> made; <c>Shipped</c> asks for the install's own copy of an example.</summary>
public sealed record PluginUpdatePress(string Id, bool Shipped = false, bool Apply = false);

/// <summary>A plugin installed from an offer.</summary>
public sealed record PluginInstallPress(string Offer);

/// <summary>
/// An act on an installed plugin; the help grants read the <c>Server</c>, and <c>Writes</c> names the writes the person ticked, none
/// sent meaning every one. <c>Said</c> asks for a terminal's words.
/// </summary>
public sealed record PluginActionPress(string Action, string Id, string? Server, IReadOnlyList<string>? Writes, bool Said = false);

/// <summary>A plugin written into a folder for the points named.</summary>
public sealed record PluginNewPress(string Id, string Folder, IReadOnlyList<string>? Points);

/// <summary>A plugin tried: an installed one by its <c>Id</c>, or a <c>Folder</c> by its path.</summary>
public sealed record PluginTryPress(string? Id, string? Folder, string? Point);

/// <summary>An installed plugin's tool check repaired.</summary>
public sealed record PluginToolFixPress(string Id, string Tool, int Check);
