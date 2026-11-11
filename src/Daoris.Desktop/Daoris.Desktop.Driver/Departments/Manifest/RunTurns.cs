using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A graph run's turns in its places, each kept as it ends in a file of its own under <c>workflows/units/&lt;key&gt;.turns/</c> beside the
/// run's record and never rewritten: a record's next turn writes its start's manifest again, so only this log keeps a checkout's and a
/// folder's earlier intervals. And the run's baseline: its first turn's start manifest, kept in the place's manifest store as the named
/// record <c>run-&lt;key&gt;-base</c>, so the store's sweep counts its blobs while the tidy is told a review record names it.
/// </summary>
public static class RunTurns
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string FolderOf(string home, string run) =>
        Path.Combine(NodeRecordFiles.FolderOf(home), NodeRecordFiles.KeyOf(RunUnits.IdOf(run)) + ".turns");

    /// <summary>The name of <paramref name="run"/>'s baseline in its place's manifest store.</summary>
    public static string BaselineOf(string run) => $"run-{NodeRecordFiles.KeyOf(RunUnits.IdOf(run))}-base";

    /// <summary>Keep <paramref name="start"/> as its run's baseline where the run keeps none in this place yet, so it is its first turn's.</summary>
    public static void Began(ManifestStore store, RunManifest start)
    {
        if (start.Run is not { } run || store.Read(BaselineOf(run), ManifestMoment.Start) is not null) return;
        store.Keep(BaselineOf(run), ManifestMoment.Start, start);
    }

    /// <summary>Keep the turn of <paramref name="record"/> from <paramref name="start"/> to <paramref name="end"/> in its run's log; nothing outside a run.</summary>
    public static void Ended(string home, ManifestStore store, string record, RunManifest start, RunManifest end)
    {
        if (start.Run is not { } run) return;
        var turn = new RunTurn(record, start.Kind, store.Workspace, store.Department, start.Root ?? end.Root ?? "", start.At, end.At)
        {
            StartHead = start.Head,
            EndHead = end.Head,
            Own = [.. (end.Own ?? []).Select(path => new RunTurnPath(path, end.Listed(path)?.Sha256))],
            Outside = start.Outside ?? [],
            Digest = DigestOf(end),
            TooLarge = start.TooLarge || end.TooLarge,
        };
        var folder = FolderOf(home, run);
        Directory.CreateDirectory(folder);
        var name = $"{start.At.UtcTicks.ToString("D20", CultureInfo.InvariantCulture)}-{record}.json";
        AtomicFile.TryCreateText(Path.Combine(folder, name), JsonSerializer.Serialize(turn, Json) + "\n");
    }

    /// <summary><paramref name="run"/>'s turns in the order they started; a file that does not read is left out.</summary>
    public static IReadOnlyList<RunTurn> Of(string home, string run)
    {
        var folder = FolderOf(home, run);
        if (!Directory.Exists(folder)) return [];
        var turns = new List<RunTurn>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                if (JsonSerializer.Deserialize<RunTurn>(AtomicFile.ReadText(file), Json) is { } turn) turns.Add(turn);
            }
            catch (JsonException)
            {
            }
        }

        return turns;
    }

    /// <summary>What an end held, as a revision reads it: a folder's every file by its hash, a git place's <c>HEAD</c> and its own paths.</summary>
    private static string DigestOf(RunManifest end)
    {
        var text = new StringBuilder(end.Kind).Append('\n').Append(end.Head).Append('\n');
        var own = end.Own?.ToHashSet(StringComparer.Ordinal);
        foreach (var entry in end.Entries.Where(entry => end.Kind == PlaceKind.Folder || own?.Contains(entry.Path) == true))
        {
            text.Append(entry.Path).Append('\t').Append(entry.Sha256).Append('\t').Append(entry.Size).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}

/// <summary>One turn of a run in a place, as its end kept it.</summary>
/// <param name="Kind">The place's kind (<see cref="PlaceKind"/>).</param>
/// <param name="Root">The folder it worked in: the place's, or a copy's.</param>
public sealed record RunTurn(
    string Record, string Kind, string Workspace, string Department, string Root, DateTimeOffset Started, DateTimeOffset Ended)
{
    /// <summary>A git place's <c>HEAD</c> at the turn's start and end; the commits reachable from the end and not the start were made while it ran.</summary>
    public string? StartHead { get; init; }

    public string? EndHead { get; init; }

    /// <summary>A git place's paths the run's line kept uncommitted at the end, each with its hash (null where gone).</summary>
    public IReadOnlyList<RunTurnPath> Own { get; init; } = [];

    /// <summary>The paths its start found changed since the place's last manifest: changed outside the run's turns.</summary>
    public IReadOnlyList<string> Outside { get; init; } = [];

    /// <summary>What its end held, by content: a later turn that changed nothing digests alike.</summary>
    public string Digest { get; init; } = "";

    /// <summary>A folder past the manifest's bounds at either end: what changed in it is not followed.</summary>
    public bool TooLarge { get; init; }
}

/// <summary>One path a turn kept uncommitted, with its working content's hash.</summary>
public sealed record RunTurnPath(string Path, string? Sha256);
