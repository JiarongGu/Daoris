using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a search of Daoris's own knowledge found for a brainstorm's packet (BRAINSTORM1a, D176 point 2), before the bounds: an
/// entry's kind, its path from the repository's root, its lines, its text, and the words that found it.
/// </summary>
/// <param name="Words">The wording of the question the search was asked in.</param>
public sealed record BrainstormFound(string Kind, string Path, int? First, int? Last, string Text, string Words);

/// <summary>One source of a frozen packet (D176 point 2): what it is, where, at which revision, why it was taken, and what was kept.</summary>
/// <param name="Id">The packet's own id for it, <c>S1</c>…, which a proposal names in its <c>sources</c>.</param>
/// <param name="Revision">The commit of the checkout it was read at; null where it could not be read.</param>
/// <param name="Words">The search's words that found it.</param>
/// <param name="Tier">The tier that found it: <c>meaning</c>, <c>words</c> or <c>files</c>.</param>
/// <param name="Kept">The characters kept.</param>
/// <param name="Of">The characters it had.</param>
public sealed record BrainstormSource(
    string Id, string Kind, string Path, int? First, int? Last, string? Revision, string Words, string Tier, string Text, int Kept, int Of);

/// <summary>A source found and left out of the packet, with why: <c>bound</c>, <c>machine-path</c> or <c>removed</c>.</summary>
public sealed record BrainstormLeftOut(string Path, string Why);

/// <summary>The packet's bounds, first values (D176 point 2): read again on the first real run (BRAINSTORM1c).</summary>
public sealed record BrainstormBounds(int Sources = 24, int Each = 12_000, int All = 60_000, int Question = 4_000)
{
    public static BrainstormBounds First { get; } = new();
}

/// <summary>
/// The context packet (D176 point 2): assembled by Daoris with no model, frozen once as <c>packet.json</c> (<c>packet: 1</c>),
/// identified by the digest of its content, and handed byte for byte to every pass. It never holds a machine path, an account's
/// name or a credential.
/// </summary>
/// <param name="Repository">The asker's repository by its name; null for a question with none.</param>
/// <param name="Tier">The tier that found its sources: <c>meaning</c>, <c>words</c> or <c>files</c>; null where none was searched.</param>
public sealed record BrainstormPacket(
    string Question, string? Repository, string? Tier, IReadOnlyList<BrainstormSource> Sources, IReadOnlyList<BrainstormLeftOut> LeftOut)
{
    /// <summary>The digest of its content, <c>sha256:</c> and the hex, which names it everywhere.</summary>
    public string Digest => BrainstormPackets.DigestOf(this);
}

/// <summary>The packet's assembly, its bytes and its digest (D176 point 2), with no model and no I/O.</summary>
public static class BrainstormPackets
{
    /// <summary>The schema's version, written as <c>packet</c>.</summary>
    public const int Version = 1;

    public const string Meaning = "meaning";
    public const string Words = "words";
    public const string Files = "files";

    public const string LeftForBound = "bound";
    public const string LeftForMachinePath = "machine-path";

    /// <summary>
    /// The packet from what was found: the question first, then the sources in the search's order, each cut to the bound for one
    /// and all, and what was found and left out listed with why. One path and range found twice is one source. A path that is not
    /// relative to the repository's root is left out, never written (no machine path in a packet).
    /// </summary>
    public static BrainstormPacket Freeze(
        string question, string? repository, string? tier, string? revision, IReadOnlyList<BrainstormFound> found, BrainstormBounds bounds)
    {
        var sources = new List<BrainstormSource>();
        var left = new List<BrainstormLeftOut>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var room = bounds.All;
        foreach (var each in found)
        {
            if (!seen.Add($"{each.Path}:{each.First}-{each.Last}")) continue;
            if (!Relative(each.Path))
            {
                left.Add(new BrainstormLeftOut("(not a repository path)", LeftForMachinePath));
                continue;
            }

            if (sources.Count >= bounds.Sources || room <= 0)
            {
                left.Add(new BrainstormLeftOut(Named(each), LeftForBound));
                continue;
            }

            var kept = Math.Min(Math.Min(each.Text.Length, bounds.Each), room);
            room -= kept;
            sources.Add(new BrainstormSource(
                $"S{sources.Count + 1}", each.Kind, each.Path.Replace('\\', '/'), each.First, each.Last, revision, each.Words, tier ?? Files,
                each.Text[..kept], kept, each.Text.Length));
        }

        return new BrainstormPacket(question, repository, repository is null ? null : tier, sources, left);
    }

    /// <summary>The packet's file: its schema version, its digest, then its content, as the digest was taken of it.</summary>
    public static byte[] Bytes(BrainstormPacket packet) => Write(packet, withDigest: true);

    /// <summary><c>sha256:</c> and the hex of the content's bytes, everything the file holds but its version and its digest.</summary>
    public static string DigestOf(BrainstormPacket packet) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Write(packet, withDigest: false)));

    /// <summary>A packet read back from its file; null where it is not one, or its digest is not its content's.</summary>
    public static BrainstormPacket? Read(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("packet", out var version)
                || version.ValueKind != JsonValueKind.Number || version.GetInt32() != Version)
            {
                return null;
            }

            var sources = new List<BrainstormSource>();
            foreach (var source in Items(root, "sources"))
            {
                var lines = source.TryGetProperty("lines", out var range) && range.ValueKind == JsonValueKind.Array ? range : default;
                var why = source.GetProperty("why");
                sources.Add(new BrainstormSource(
                    Json.Text(source, "id") ?? "", Json.Text(source, "kind") ?? "", Json.Text(source, "path") ?? "",
                    lines.ValueKind == JsonValueKind.Array ? lines[0].GetInt32() : null,
                    lines.ValueKind == JsonValueKind.Array ? lines[1].GetInt32() : null,
                    Json.Text(source, "revision"), Json.Text(why, "words") ?? "", Json.Text(why, "tier") ?? "",
                    Json.Text(source, "text") ?? "", source.GetProperty("kept").GetInt32(), source.GetProperty("of").GetInt32()));
            }

            var left = Items(root, "leftOut").Select(item => new BrainstormLeftOut(Json.Text(item, "path") ?? "", Json.Text(item, "why") ?? "")).ToList();
            var packet = new BrainstormPacket(Json.Text(root, "question") ?? "", Json.Text(root, "repository"), Json.Text(root, "tier"), sources, left);
            return Json.Text(root, "digest") == packet.Digest ? packet : null;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException
                                          or FormatException)
        {
            return null;
        }
    }

    private static IEnumerable<JsonElement> Items(JsonElement root, string name) =>
        root.TryGetProperty(name, out var items) && items.ValueKind == JsonValueKind.Array ? items.EnumerateArray() : [];

    private static string Named(BrainstormFound found) =>
        found.First is { } first ? $"{found.Path.Replace('\\', '/')}:{first}-{found.Last ?? first}" : found.Path.Replace('\\', '/');

    /// <summary>A path from a repository's root: never rooted, never a drive, never climbing out.</summary>
    private static bool Relative(string path) =>
        path.Length > 0 && !Path.IsPathRooted(path) && !path.Contains(':') && !path.StartsWith('/') && !path.StartsWith('\\')
        && !path.Replace('\\', '/').Split('/').Contains("..");

    private static byte[] Write(BrainstormPacket packet, bool withDigest)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            if (withDigest)
            {
                writer.WriteNumber("packet", Version);
                writer.WriteString("digest", packet.Digest);
            }

            writer.WriteString("question", packet.Question);
            Nullable(writer, "repository", packet.Repository);
            Nullable(writer, "tier", packet.Tier);
            writer.WriteStartArray("sources");
            foreach (var source in packet.Sources)
            {
                writer.WriteStartObject();
                writer.WriteString("id", source.Id);
                writer.WriteString("kind", source.Kind);
                writer.WriteString("path", source.Path);
                if (source.First is { } first)
                {
                    writer.WriteStartArray("lines");
                    writer.WriteNumberValue(first);
                    writer.WriteNumberValue(source.Last ?? first);
                    writer.WriteEndArray();
                }
                else
                {
                    writer.WriteNull("lines");
                }

                Nullable(writer, "revision", source.Revision);
                writer.WriteStartObject("why");
                writer.WriteString("words", source.Words);
                writer.WriteString("tier", source.Tier);
                writer.WriteEndObject();
                writer.WriteNumber("kept", source.Kept);
                writer.WriteNumber("of", source.Of);
                writer.WriteString("text", source.Text);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("leftOut");
            foreach (var left in packet.LeftOut)
            {
                writer.WriteStartObject();
                writer.WriteString("path", left.Path);
                writer.WriteString("why", left.Why);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        // LF only, whatever the platform: the digest is of these bytes, on every machine.
        return Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n");
    }

    private static void Nullable(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) writer.WriteNull(name);
        else writer.WriteString(name, value);
    }
}

/// <summary>The wordings a packet's search is asked in (D129 §2): the question as written, then its longer words alone.</summary>
public static class BrainstormWordings
{
    public static IReadOnlyList<string> Of(string question)
    {
        var words = question
            .Split([' ', '\t', '\r', '\n', ',', '.', '?', '!', ';', ':', '"', '\'', '(', ')', '`'], StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length >= 5)
            .Select(word => word.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Take(8)
            .ToList();
        var second = string.Join(' ', words);
        return second.Length > 0 && !string.Equals(second, question.Trim(), StringComparison.OrdinalIgnoreCase)
            ? [question.Trim(), second]
            : [question.Trim()];
    }
}
