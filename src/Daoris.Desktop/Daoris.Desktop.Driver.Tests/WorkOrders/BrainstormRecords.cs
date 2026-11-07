using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The records the service's desk writes, laid down by a test as the desk lays them, so the look's tests start from a
/// request, a send or a stop with no service. The driver itself writes none of them. <c>brainstorm-files.json</c> holds both sides
/// to one shape: the service suite's tests read the desk's files against its rows, and <c>BrainstormFilesTwinTests</c> reads these.
/// </summary>
internal static class BrainstormRecords
{
    public static string WriteRequest(
        this BrainstormFiles files, string question, string? repository, IReadOnlyList<string> agents, DateTimeOffset at, string? named = null,
        IReadOnlyList<string>? sources = null)
    {
        var id = named ?? Guid.NewGuid().ToString("N")[..8];
        if (named is not null && File.Exists(files.PathOf(id, "request.json"))) return id;
        BrainstormFiles.Write(files.PathOf(id, "request.json"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("brainstorm", BrainstormAnswers.Schema);
            writer.WriteString("id", id);
            writer.WriteString("question", question);
            if (repository is null) writer.WriteNull("repository");
            else writer.WriteString("repository", repository);
            writer.WriteStartArray("agents");
            foreach (var agent in agents) writer.WriteStringValue(agent);
            writer.WriteEndArray();
            writer.WriteString("at", Moments.Write(at));
            if (sources is { Count: > 0 })
            {
                writer.WriteStartArray("sources");
                foreach (var source in sources) writer.WriteStringValue(source);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        });
        return id;
    }

    public static void WriteSent(this BrainstormFiles files, string id, string digest, DateTimeOffset at) =>
        BrainstormFiles.Write(files.PathOf(id, "sent.json"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("packet", digest);
            writer.WriteString("at", Moments.Write(at));
            writer.WriteEndObject();
        });

    public static void WriteStop(this BrainstormFiles files, string id, DateTimeOffset at) =>
        BrainstormFiles.Write(files.PathOf(id, "stop.json"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("at", Moments.Write(at));
            writer.WriteEndObject();
        });

    public static string WriteRequest(
        this MeetingFiles files, string question, string? repository, IReadOnlyList<string> agents, DateTimeOffset at, string? named = null)
    {
        var id = named ?? Guid.NewGuid().ToString("N")[..8];
        if (named is not null && File.Exists(files.PathOf(id, "request.json"))) return id;
        BrainstormFiles.Write(files.PathOf(id, "request.json"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("meeting", MeetingPackets.Version);
            writer.WriteString("id", id);
            writer.WriteString("question", question);
            if (repository is null) writer.WriteNull("repository");
            else writer.WriteString("repository", repository);
            writer.WriteStartArray("agents");
            foreach (var agent in agents) writer.WriteStringValue(agent);
            writer.WriteEndArray();
            writer.WriteString("at", Moments.Write(at));
            writer.WriteEndObject();
        });
        return id;
    }

    public static void WriteSent(this MeetingFiles files, string id, int round, string digest, DateTimeOffset at) =>
        BrainstormFiles.Write(files.PathOf(id, "sent", $"{round}.json"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("round", round);
            writer.WriteString("packet", digest);
            writer.WriteString("at", Moments.Write(at));
            writer.WriteEndObject();
        });

    public static void WriteStop(this MeetingFiles files, string id, DateTimeOffset at) => files.Turns.WriteStop(id, at);
}
