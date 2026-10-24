using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The person's way past the look on a chat run (CHATFLOW1d2, the chat-flow design §5.3): <i>Accept without the look…</i>, kept with their
/// words at the tip it was pressed at, D154 row 1's skip re-homed for a run that has no quest.
/// </summary>
/// <param name="Tip">The tip the press captured: it covers that commit and those before it, so a later commit asks it again.</param>
/// <param name="Door"><c>screen</c> or <c>terminal</c> (<see cref="ReviewDoors"/>).</param>
public sealed record ChatLookSkip(string Tip, DateTimeOffset At, string? Words, string? Door);

/// <summary>
/// A chat run's look file (CHATFLOW1d2, the chat-flow design §5.3), <c>&lt;home&gt;/workflows/looks/chat-&lt;first&gt;.json</c>: machine-local (D47
/// §4), as the opinion gate keeps the person's words, and taken with the record its key names by the session delete
/// (<see cref="ChatRuns.Files"/>). A conversation publishes no set-up step, so no look can be set up for its work in this build: where its
/// run's process requires one, the gate's look part reads <c>cannot-start</c> with <see cref="Limit"/>, until the person's way past it covers
/// the tip that would land.
/// </summary>
/// <remarks>
/// Read without trusting its shape and written whole, atomically: a file that does not read is no skip kept, so the look holds again.
/// No agent and no Ask Daoris card reaches it (D110): the skip is the person's press alone.
/// </remarks>
public sealed class ChatLooks(string home)
{
    /// <summary>What the look part says of a chat run's look that cannot start, as its limit.</summary>
    public const string Limit = "a conversation publishes no set-up step, so no look can be set up for its work.";

    // One writer at a time in this process: the press and the terminal's verb may write at once.
    private static readonly object Gate = new();

    /// <summary>Where a chat run's look file is kept; null for a key that is no chat run's.</summary>
    public string? PathOf(string run) =>
        ChatRuns.IsChatRun(run) && SafeNames.IsPlainId(run) ? Path.Combine(ChatRuns.LooksFolder(home), run + ".json") : null;

    /// <summary>Every skip kept for the run, oldest first; none where the file is missing or does not read.</summary>
    public IReadOnlyList<ChatLookSkip> Skips(string run)
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

    /// <summary>The person's way past the look kept for the run, at the tip their press captured.</summary>
    public void Skip(string run, ChatLookSkip skip)
    {
        if (PathOf(run) is not { } path) throw new DriverException($"`{run}` names no chat run this machine keeps a look for.");
        lock (Gate)
        {
            List<ChatLookSkip> all = [.. Skips(run), skip];
            JsonFile.Write(path, writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("run", run);
                writer.WriteStartArray("skips");
                foreach (var each in all)
                {
                    writer.WriteStartObject();
                    writer.WriteString("tip", each.Tip);
                    writer.WriteString("at", OpinionViews.Moment(each.At));
                    if (each.Words is not null) writer.WriteString("words", each.Words);
                    if (each.Door is not null) writer.WriteString("door", each.Door);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });
            HomeWrites.Mine(path);
        }
    }

    /// <summary>
    /// The newest skip that covers <paramref name="tip"/>: kept at that commit or a later one that holds it, as git's ancestry says. Null
    /// where none does, so the look holds again.
    /// </summary>
    /// <param name="holds">Whether <paramref name="tip"/> is a commit or an ancestor of it: true, false, or null where git could not say.</param>
    public async Task<ChatLookSkip?> CoveringAsync(string run, string? tip, Func<string, Task<bool?>> holds)
    {
        if (tip is null) return null;
        foreach (var skip in Skips(run).OrderByDescending(each => each.At))
        {
            if (await holds(skip.Tip).ConfigureAwait(false) == true) return skip;
        }

        return null;
    }

    private static ChatLookSkip? SkipOf(JsonElement element) =>
        Json.Text(element, "tip") is { Length: > 0 } tip && Moments.Read(element, "at") is { } at
            ? new ChatLookSkip(tip, at, Json.Text(element, "words"), Json.Text(element, "door"))
            : null;
}
