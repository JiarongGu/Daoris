using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What one session consumed, as its harness reported it (TOOL3/D57 §4).</summary>
/// <param name="Session">The session record's id — the same id everything else about it is keyed by.</param>
/// <param name="Profile">
/// The credential profile it ran as, or null for the harness's own configuration home. <b>This is
/// what makes the whole record machine-local</b>: a profile name is already served only over
/// loopback, and usage that names one inherits that rather than arguing for itself.
/// </param>
/// <param name="Used">Context held, at its high-water mark, in the harness's own units.</param>
/// <param name="Size">The window it was held against.</param>
public sealed record UsageEntry(
    string Session, string Repository, string Harness, string? Profile,
    long Used, long Size, DateTimeOffset When);

/// <summary>One account's share of the load — a derivation over the sessions, never a second tally.</summary>
public sealed record AccountUsage(string Harness, string? Profile, int Sessions, long Used);

/// <summary>
/// What sessions on this machine consumed (TOOL3/D57 §4) — <b>measured before it is managed</b>.
/// </summary>
/// <remarks>
/// <para><b>The source is the protocol door.</b> ACP reports context pressure per turn; the pipe door
/// gives text and an exit code. So a pipe-door session is recorded not at all and every surface says
/// <i>not measured</i> rather than showing a zero — which is also the honest argument for the ACP
/// door rather than for parsing somebody else's stdout.</para>
///
/// <para>🔴 <b>Machine-local, by an inherited rule.</b> Per-account usage names a credential profile,
/// and <c>ToSession</c> already serves a profile name only over loopback — so this lives beside the
/// transcript under the machine's own <c>.daoris</c>, reaches a surface over the shell's bridge, and
/// has <b>no HTTP route</b>. A teammate sees the session record, exactly as today. Re-deciding that
/// boundary per field is how a boundary erodes, so it is not re-decided here.</para>
///
/// <para><b>No price, ever.</b> Daoris does not know what a token costs — that is the deployment's
/// business (D24) — so this counts what the harness reported and stops. A price table per model per
/// provider, maintained here, would be wrong within a month and would be Daoris claiming to know
/// something it structurally does not.</para>
/// </remarks>
public sealed class SessionUsage(string home)
{
    /// <summary>
    /// Sessions kept. Bounded for the reason the console's ring is: a file that grew forever is the
    /// transcript problem in a smaller shape.
    /// </summary>
    public const int Retained = 500;

    private readonly string _path = Path.Combine(home, "usage.json");

    /// <summary>Every measured session, oldest first.</summary>
    public IReadOnlyList<UsageEntry> Sessions => Load();

    /// <summary>What one session used, or null when it was never measured.</summary>
    public UsageEntry? Of(string session) =>
        Load().FirstOrDefault(e => string.Equals(e.Session, session, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Which account carried what — the question "multiple accounts with usage management" actually
    /// asks. Derived from the sessions so the two can never disagree.
    /// </summary>
    public IReadOnlyList<AccountUsage> ByAccount() =>
    [
        .. Load()
            .GroupBy(e => (e.Harness, e.Profile))
            .Select(g => new AccountUsage(g.Key.Harness, g.Key.Profile, g.Count(), g.Sum(e => e.Used)))
            .OrderBy(a => a.Harness, StringComparer.Ordinal)
            .ThenBy(a => a.Profile ?? "", StringComparer.Ordinal),
    ];

    /// <summary>
    /// Record what a session used.
    /// </summary>
    /// <remarks>
    /// <b>One session is measured once</b>, and a larger reading replaces a smaller one — the same
    /// high-water rule the wire applies within a turn, applied across them. A session that compacted
    /// and then finished small must not read as having used very little.
    /// </remarks>
    public void Record(UsageEntry entry)
    {
        var held = Load().ToList();
        var at = held.FindIndex(
            e => string.Equals(e.Session, entry.Session, StringComparison.OrdinalIgnoreCase));

        if (at >= 0)
        {
            if (entry.Used <= held[at].Used) return;
            held[at] = entry;
        }
        else
        {
            held.Add(entry);
        }

        // Oldest out, never largest: dropping by size would quietly delete exactly the sessions
        // somebody would want to look at.
        while (held.Count > Retained) held.RemoveAt(0);

        Save(held);
    }

    /// <summary>
    /// A missing, half-written or hand-mangled file is a machine that has measured nothing — never a
    /// crash, and never overwritten by the act of reading it. Usage is an observation and losing it
    /// costs nothing, which is the same reading the driver's own wiring files take (D21).
    /// </summary>
    private List<UsageEntry> Load()
    {
        if (!File.Exists(_path)) return [];

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_path));
            if (document.RootElement.ValueKind != JsonValueKind.Array) return [];

            var entries = new List<UsageEntry>();
            foreach (var row in document.RootElement.EnumerateArray())
            {
                if (Read(row) is { } entry) entries.Add(entry);
            }

            return entries;
        }
        catch (Exception error)
            when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static UsageEntry? Read(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object) return null;

        var session = Text(row, "session");
        if (string.IsNullOrWhiteSpace(session)) return null;

        return new UsageEntry(
            session,
            Text(row, "repository") ?? "",
            Text(row, "harness") ?? "",
            Text(row, "profile"),
            Number(row, "used") ?? 0,
            Number(row, "size") ?? 0,
            row.TryGetProperty("when", out var when) && when.TryGetDateTimeOffset(out var at)
                ? at
                : DateTimeOffset.MinValue);
    }

    /// <summary>Written atomically, beside-then-rename, like every write in this family.</summary>
    private void Save(IReadOnlyList<UsageEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartArray();
            foreach (var entry in entries)
            {
                writer.WriteStartObject();
                writer.WriteString("session", entry.Session);
                writer.WriteString("repository", entry.Repository);
                writer.WriteString("harness", entry.Harness);
                if (entry.Profile is { Length: > 0 } profile) writer.WriteString("profile", profile);
                writer.WriteNumber("used", entry.Used);
                writer.WriteNumber("size", entry.Size);
                writer.WriteString("when", entry.When);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        AtomicFile.WriteText(_path, System.Text.Encoding.UTF8.GetString(stream.ToArray()) + "\n");
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out var number)
            ? number
            : null;
}
