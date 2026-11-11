namespace Daoris.Driver;

/// <summary>The versions a return showed of one path, the copy's and the folder's, each its hash or null where that side holds none or did not read.</summary>
public sealed record ReturnShown(string? Copy, string? Folder);
