using System.IO.Compression;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A place's manifests and its content store, under <c>&lt;home&gt;/work/places/&lt;workspace&gt;/&lt;department&gt;/</c>: each manifest
/// at <c>manifests/&lt;record&gt;-&lt;start|end&gt;.jsonl.gz</c>, one line for its head and one per listed path, and each text blob at
/// <c>blobs/&lt;sha256&gt;</c>. The keeping holds the place's newest 20 and every one a review, a waiting return or an open line names.
/// </summary>
public sealed class ManifestStore
{
    private const int Newest = 20;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _manifests;

    public ManifestStore(string home, string workspace, string department)
    {
        var place = DaorisHome.Entry(home, HomeEntry.Places, workspace, department);
        _manifests = Path.Combine(place, "manifests");
        Content = new ContentStore(Path.Combine(place, "blobs"));
        (Workspace, Department) = (workspace, department);
    }

    public string Workspace { get; }

    public string Department { get; }

    /// <summary>The place's text content, by hash.</summary>
    public ContentStore Content { get; }

    /// <summary>Keep <paramref name="manifest"/> as <paramref name="record"/>'s at <paramref name="moment"/>.</summary>
    public void Keep(string record, ManifestMoment moment, RunManifest manifest)
    {
        Directory.CreateDirectory(_manifests);
        using var buffer = new MemoryStream();
        using (var zipped = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        using (var writer = new StreamWriter(zipped))
        {
            writer.Write(JsonSerializer.Serialize(manifest with { Entries = [] }, Json) + "\n");
            foreach (var entry in manifest.Entries) writer.Write(JsonSerializer.Serialize(entry, Json) + "\n");
        }

        AtomicFile.WriteBytes(FileOf(record, moment), buffer.ToArray());
    }

    /// <summary><paramref name="record"/>'s manifest at <paramref name="moment"/>, or null where none is kept.</summary>
    public RunManifest? Read(string record, ManifestMoment moment) => ReadFile(FileOf(record, moment));

    /// <summary>The place's newest manifest, of any line, or null: what a turn's start compares with.</summary>
    public RunManifest? Last() => Kept().Select(ReadFile).OfType<RunManifest>().MaxBy(manifest => manifest.At);

    public RunManifest? Last(string root) => Kept().Select(ReadFile).OfType<RunManifest>()
        .Where(manifest => manifest.Root is { } read && string.Equals(Path.GetFullPath(read), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
        .MaxBy(manifest => manifest.At);

    /// <summary>Keep the newest 20 and every manifest of a record in <paramref name="named"/>, and the blobs those name.</summary>
    public void Tidy(IReadOnlySet<string> named)
    {
        var read = Kept().Select(file => (File: file, Manifest: ReadFile(file))).Where(kept => kept.Manifest is not null).ToList();
        var newest = read.OrderByDescending(kept => kept.Manifest!.At).Take(Newest).Select(kept => kept.File).ToHashSet(StringComparer.Ordinal);
        foreach (var (file, _) in read)
        {
            if (!newest.Contains(file) && !named.Contains(RecordOf(file))) File.Delete(file);
        }

        Content.Sweep(Kept().Select(ReadFile).OfType<RunManifest>()
            .SelectMany(manifest => manifest.Entries).Select(entry => entry.Sha256).OfType<string>().ToHashSet(StringComparer.Ordinal));
    }

    private string FileOf(string record, ManifestMoment moment) =>
        Path.Combine(_manifests, $"{record}-{(moment == ManifestMoment.Start ? "start" : "end")}.jsonl.gz");

    private static string RecordOf(string file)
    {
        var name = Path.GetFileName(file);
        return name[..name.LastIndexOf('-')];
    }

    private IEnumerable<string> Kept() =>
        Directory.Exists(_manifests) ? Directory.EnumerateFiles(_manifests, "*.jsonl.gz").ToList() : [];

    private static RunManifest? ReadFile(string file)
    {
        if (!File.Exists(file)) return null;
        using var zipped = new GZipStream(AtomicFile.OpenRead(file), CompressionMode.Decompress);
        using var reader = new StreamReader(zipped);
        if (reader.ReadLine() is not { } first || JsonSerializer.Deserialize<RunManifest>(first, Json) is not { } head) return null;
        var entries = new List<ManifestEntry>();
        while (reader.ReadLine() is { Length: > 0 } line)
        {
            if (JsonSerializer.Deserialize<ManifestEntry>(line, Json) is { } entry) entries.Add(entry);
        }

        return head with { Entries = entries };
    }
}
