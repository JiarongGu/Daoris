using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>What one attempt of a run's <c>rebase</c> came to, at the work's tip and the target's tip it is bound to.</summary>
public sealed record RebaseAttempt(string Run, string Node, int Attempt, string Tip, string Onto, string Outcome, string Said)
{
    public string? Commit { get; init; }

    public string? StopSort { get; init; }

    public string? PatchId { get; init; }

    public IReadOnlyList<string> Paths { get; init; } = [];

    public string? StopCommit { get; init; }

    public IReadOnlyList<string> LeftOut { get; init; } = [];

    public IReadOnlyList<RebaseUncommitted> Uncommitted { get; init; } = [];

    public string? Resolution { get; init; }

    /// <summary>The problem's stop, where it has one.</summary>
    [JsonIgnore]
    public NodeStop? Stop => StopSort is { } sort ? new NodeStop(sort, PatchId, Paths) : null;
}

/// <summary>
/// The door's record of each <c>rebase</c> attempt (<c>&lt;home&gt;/workflows/rebases/&lt;run&gt;.json</c>), which the step's reading reaches
/// it from: the door writes, the runtime reads, so the run's own record keeps its one writer.
/// </summary>
public sealed class RebaseRecords(string home)
{
    private static readonly object Gate = new();

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public static string FolderOf(string home) => DaorisHome.Entry(home, HomeEntry.Workflows, "rebases");

    private string PathOf(string run) => Path.Combine(FolderOf(home), $"{run.Replace(':', '-')}.json");

    /// <summary>The attempts of <paramref name="run"/>, oldest first; none where none is written.</summary>
    public IReadOnlyList<RebaseAttempt> Of(string run)
    {
        var path = PathOf(run);
        return File.Exists(path) ? JsonSerializer.Deserialize<List<RebaseAttempt>>(AtomicFile.ReadText(path), Options) ?? [] : [];
    }

    /// <summary>The attempt of <paramref name="node"/> numbered <paramref name="attempt"/>, null where none is written.</summary>
    public RebaseAttempt? Of(string run, string node, int attempt) => Of(run).LastOrDefault(each => each.Node == node && each.Attempt == attempt);

    /// <summary>The run's attempts let go of, at its settled end; nothing where none is written.</summary>
    public void Release(string run)
    {
        lock (Gate)
        {
            var path = PathOf(run);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>Writes <paramref name="made"/>, replacing an earlier one of the same attempt.</summary>
    public void Write(RebaseAttempt made)
    {
        lock (Gate)
        {
            List<RebaseAttempt> all = [.. Of(made.Run).Where(each => each.Node != made.Node || each.Attempt != made.Attempt), made];
            Directory.CreateDirectory(FolderOf(home));
            var path = PathOf(made.Run);
            AtomicFile.WriteText(path, JsonSerializer.Serialize(all, Options).ReplaceLineEndings("\n"));
            HomeWrites.Mine(path);
        }
    }
}
