using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Contracts.Platform;

// A local host's alone, read by each process that tells from the ledger. A read every caller may make, as the connector reads the same store.

/// <summary>
/// Which ledger families moved after <c>Since</c> (<c>&lt;epoch&gt;.&lt;n&gt;</c>), answered as soon as one moves or at <c>Wait</c> seconds (30 at
/// most) with none. A missing, malformed, stale or future cursor is answered at once with every family and the current cursor. <c>Journal</c>
/// <c>1</c> (or <c>true</c>) asks for the event journal, woken by it and told its family after the others.
/// </summary>
public sealed record ReadChanges(string? Since = null, int? Wait = null, string? Journal = null) : IRoute<ChangesResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/changes";
}

/// <summary>The journal's rows after a position, oldest first, with where the journal stands.</summary>
public sealed record ReadJournal(long? Since = null, int? Limit = null) : IRoute<JournalPage>
{
    public static string Method => "GET";
    public static string Path => "/api/journal";
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ReadChanges))]
[JsonSerializable(typeof(ReadJournal))]
[JsonSerializable(typeof(ChangesResponse))]
[JsonSerializable(typeof(JournalPage))]
internal sealed partial class PlatformJson : JsonSerializerContext;
