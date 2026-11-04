namespace Daoris.Desktop;

// The payloads of DriverRoutes' standing choices, each named for its route. RoutePayload reads one off the request by its
// parameters' types, and the page's generated routes carry it as the type its hook sends.

public sealed record SetDrivable(string Repository, bool Drivable);

public sealed record SetHold(string Repository, bool Held);

public sealed record SetTrees(string Repository, bool OwnTree);

public sealed record SetCheckoutWrites(string Repository, bool Allow);

/// <summary>No words clear the repository's standing answer.</summary>
public sealed record SetStanding(string Repository, string? Says);

/// <summary>No repository is read as an empty name, and no checks clear them.</summary>
public sealed record SetSessionChecks(string? Repository, string? Checks);

public sealed record SetNotify(bool Notify);

/// <summary>No minutes, or none whole, is the driver's refusal, as less than one is.</summary>
public sealed record SetCoolOff(long? Minutes);

/// <summary>No count, or none whole, is the driver's refusal, as less than one is.</summary>
public sealed record SetCap(long? Cap);

/// <summary>One kind's keep rule, each value one of its kind's: <c>KeepRules.Apply</c> judges which a kind takes.</summary>
public sealed record SetKeep(
    string Kind, long? Days, bool Never = false, bool? Whole = null, bool Default = false, long? Gb = null, long? Percent = null, bool Off = false);

/// <summary>No mode is the driver's refusal.</summary>
public sealed record SetHelpMode(string? Mode);

/// <summary>No grant is housekeeping's, and no operation is the driver's refusal.</summary>
public sealed record SetHelpGrant(string? Grant, string? Operation, bool Chosen = false);

/// <summary>A state slice by its name.</summary>
public sealed record StateSliceRead(string Slice);

/// <summary>A heavy-gate read for a <c>Repository</c>; none named is the empty name.</summary>
public sealed record HeavyRead(string? Repository);

/// <summary>A folder trusted through its trust file.</summary>
public sealed record TrustFolderPress(string Folder, string TrustFile);

/// <summary>A quest by its id, with or without the <c>#</c>.</summary>
public sealed record QuestPress(string Quest);

/// <summary>One circle synced now.</summary>
public sealed record SyncNowPress(string Workspace);
