using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A verification run's binding (FABRIC-PIPELINE1d; the pipeline design §4.3), written once in a folder of its own,
/// <c>&lt;home&gt;/workflows/lines/&lt;run&gt;.json</c>: its repository and checkout, the verification pipeline's version it runs, the commit it
/// judges, the branch it was asked of where one was, and the declaration's blob the person accepted. Every node of it observes the run itself at
/// that commit.
/// </summary>
/// <param name="Level">Who chose it: <see cref="VerifyRuns.Person"/> for a person's verification of a commit or a branch.</param>
public sealed record VerifyRun(
    string Run, string Repository, string Workspace, string Root, string Workflow, int Version, string Digest, string Commit, string Level, DateTimeOffset At)
{
    /// <summary>The branch a person asked it of; null for a commit.</summary>
    public string? Branch { get; init; }

    /// <summary>The blob of <c>daoris.gates.json</c> the route's acceptance names, <c>none</c>, or null where none is accepted.</summary>
    public string? Accepted { get; init; }

    /// <summary>The binding the runtime governs it by, as a graph run's.</summary>
    public WorkflowRunBinding Binding => new(Run, Repository, Workspace, Workflow, Level, At) { Version = Version, Digest = Digest };
}

/// <summary>The verification runs' bindings under the home's <c>workflows/lines/</c> (FABRIC-PIPELINE1d), apart from the chains' runs.</summary>
public static class VerifyRuns
{
    /// <summary>A person's verification of a commit or a branch, which the line's batching never counts.</summary>
    public const string Person = "person";

    /// <summary>A new run's id: <c>v-</c> and sixteen hex digits, new for each run, so a commit names no run alone.</summary>
    public static string NewId() => "v-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));

    public static string PathOf(string home, string run) =>
        SafeNames.IsPlainId(run) ? Path.Combine(VerifyFiles.LinesFolder(home), run + ".json") : throw new DriverException($"`{run}` is not a run's id.");

    /// <summary>Write the binding once; a run's binding is never written again.</summary>
    public static void Bind(string home, VerifyRun run)
    {
        var path = PathOf(home, run.Run);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) throw new DriverException($"the run {run.Run} is bound already.");
        AtomicFile.WriteText(path, Serialize(run));
    }

    /// <summary>Every verification run bound here, in run order; a file that does not read is passed over.</summary>
    public static IReadOnlyList<VerifyRun> All(string home)
    {
        var folder = VerifyFiles.LinesFolder(home);
        if (!Directory.Exists(folder)) return [];
        var runs = new List<VerifyRun>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                if (Parse(AtomicFile.ReadText(file)) is { } run && string.Equals(run.Run + ".json", Path.GetFileName(file), StringComparison.Ordinal)) runs.Add(run);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
            }
        }

        return runs;
    }

    /// <summary>What a check's attempt on a verification run verifies: the run's repository at its commit, under its accepted declaration.</summary>
    public static VerifySend? SendOf(string home, string unit, NodeAttempt attempt, NodeDeclaration node)
    {
        var id = CheckExecution.RunOf(unit);
        if (!SafeNames.IsPlainId(id) || !File.Exists(PathOf(home, id)) || Parse(AtomicFile.ReadText(PathOf(home, id))) is not { } run) return null;
        return new VerifySend(run.Run, attempt.Call.Invocation, run.Repository, run.Root, run.Commit, run.Accepted)
        {
            Gates = CheckNodes.GatesOf(node),
            Bound = WorkPerformer.Timeout(node),
        };
    }

    public static string Serialize(VerifyRun run)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema", 1);
            writer.WriteString("run", run.Run);
            writer.WriteString("repository", run.Repository);
            writer.WriteString("workspace", run.Workspace);
            writer.WriteString("root", run.Root);
            writer.WriteString("workflow", run.Workflow);
            writer.WriteNumber("version", run.Version);
            writer.WriteString("digest", run.Digest);
            writer.WriteString("commit", run.Commit);
            if (run.Branch is { } branch) writer.WriteString("branch", branch);
            if (run.Accepted is { } accepted) writer.WriteString("gates", accepted);
            writer.WriteString("level", run.Level);
            writer.WriteString("at", run.At.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray()).ReplaceLineEndings("\n") + "\n";
    }

    public static VerifyRun? Parse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            string? Text(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schema", out var schema) || !schema.TryGetInt32(out var number) || number != 1
                || Text("run") is not { } run || Text("repository") is not { } repository || Text("root") is not { } checkout
                || Text("workflow") is not { } workflow || !root.TryGetProperty("version", out var version) || !version.TryGetInt32(out var at)
                || Text("digest") is not { } digest || Text("commit") is not { } commit || Text("level") is not { } level
                || !root.TryGetProperty("at", out var when) || !when.TryGetDateTimeOffset(out var bound))
            {
                return null;
            }

            return new VerifyRun(run, repository, Text("workspace") ?? "", checkout, workflow, at, digest, commit, level, bound)
            {
                Branch = Text("branch"), Accepted = Text("gates"),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
