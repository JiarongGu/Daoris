using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The person's <i>Accept without the look…</i> on a chat run (CHATFLOW1d2, the chat-flow design §5.3): D154 row 1's skip, re-homed for
/// a run that has no quest, kept at the tip it was pressed at with their words. It covers that tip and the commits before it.
/// </summary>
/// <param name="Tip">The commit the press captured, which its landing is pinned to.</param>
/// <param name="Words">The person's words with it.</param>
public sealed record ChatLookSkip(string Tip, string Words, DateTimeOffset At)
{
    /// <summary>The record that pressed, the line's newest then.</summary>
    public string? Session { get; init; }

    /// <summary><c>terminal</c> or <c>screen</c> (<see cref="ReviewDoors"/>).</summary>
    public string? Door { get; init; }
}

/// <summary>What the review gate reads of a chat run's look (CHATFLOW1d2): the run, the record pressing, and the person's skips kept.</summary>
public sealed record ChatLookFor(string Run, string? Session, IReadOnlyList<ChatLookSkip> Skips);

/// <summary>
/// A chat run's look file, <c>&lt;home&gt;/workflows/looks/chat-&lt;first&gt;.json</c> (CHATFLOW1d2, the chat-flow design §5.3): machine-local
/// (D47 §4), as the opinion gate keeps the person's words, and gone with the session delete of the record its key names (§4.3).
/// </summary>
/// <remarks>Written whole, atomically, as BOM-less UTF-8 with LF; a file that does not read is nothing kept.</remarks>
public sealed class ChatLooks(string home)
{
    // One writer at a time in this process: the bridge's press and a terminal's may write at once.
    private static readonly object Gate = new();

    /// <summary>Where a chat run's look is kept; null for a run that is not a chat run's.</summary>
    public string? PathOf(string run) =>
        ChatRuns.IsChatRun(run) && WorkflowRunBindings.IsRunId(run) ? Path.Combine(ChatRuns.LooksFolder(home), run + ".json") : null;

    /// <summary>The person's skips kept for a chat run, oldest first; none where nothing reads.</summary>
    public IReadOnlyList<ChatLookSkip> Read(string run)
    {
        if (PathOf(run) is not { } path || !File.Exists(path)) return [];
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            return [.. Json.Items(document.RootElement, "skips").Select(SkipOf).OfType<ChatLookSkip>()];
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>What the gate reads of a chat run's look, for the record pressing.</summary>
    public ChatLookFor For(string run, string? session) => new(run, session, Read(run));

    /// <summary>A skip of the person's kept at the tip their press captured.</summary>
    public void Skip(string run, ChatLookSkip skip)
    {
        if (PathOf(run) is not { } path) throw new DriverException($"`{run}` names no chat run this machine keeps a look for.");
        lock (Gate)
        {
            var all = Read(run);
            JsonFile.Write(path, writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("run", run);
                writer.WriteStartArray("skips");
                foreach (var each in all.Append(skip))
                {
                    writer.WriteStartObject();
                    writer.WriteString("tip", each.Tip);
                    writer.WriteString("words", each.Words);
                    writer.WriteString("at", Moments.Write(each.At));
                    if (each.Session is not null) writer.WriteString("session", each.Session);
                    if (each.Door is not null) writer.WriteString("door", each.Door);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });
            HomeWrites.Mine(path);
        }
    }

    private static ChatLookSkip? SkipOf(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object && Json.Text(element, "tip") is { } tip && EvidenceCodes.IsObjectId(tip)
        && Json.Text(element, "words") is { } words && Moments.Read(element, "at") is { } at
            ? new ChatLookSkip(tip, words, at) { Session = Json.Text(element, "session"), Door = Json.Text(element, "door") }
            : null;
}
