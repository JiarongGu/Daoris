using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>What the host answered a helper's record being opened: the record, or why not, in the host's sentence.</summary>
/// <param name="Session">The helper's session id; null where it was refused.</param>
/// <param name="Message">The host's sentence: its yes, or its refusal verbatim.</param>
public sealed record HelperOpened(string? Session, string Message)
{
    public bool Opened => Session is not null;
}

/// <summary>
/// The local host's helper doors, as the driver asks them (MIXAGENT2a-ii; <c>POST /api/helpers</c> and <c>/hand</c>): a class over
/// the service's client, reached as its <see cref="ServiceClient.Helpers"/> (PART8).
/// </summary>
public sealed class HelperDoors
{
    private readonly ServiceClient _service;

    internal HelperDoors(ServiceClient service) => _service = service;

    /// <summary>
    /// Open a helper's record (D163 point 2): a chat on no quest that serves <paramref name="asker"/>, in the asker's repository
    /// and workspace, in <paramref name="tree"/>, the clone the driver made, as <paramref name="adapter"/> and its account. A
    /// refusal is an answer, the host's sentence, not an exception, and so is a host older than the door.
    /// </summary>
    public async Task<HelperOpened> OpenAsync(
        string asker, string adapter, string tree, string? account, string? harnessVersion, string? baseCommit, CancellationToken ct = default)
    {
        const string Door = "/api/helpers";
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("asker", asker);
            writer.WriteString("adapter", adapter);
            writer.WriteString("tree", tree);
            if (account is not null) writer.WriteString("account", account);
            if (harnessVersion is not null) writer.WriteString("harnessVersion", harnessVersion);
            if (baseCommit is not null) writer.WriteString("baseCommit", baseCommit);
            writer.WriteEndObject();
        }

        var (ok, status, payload, root) = await _service.PostJsonAsync(Door, System.Text.Encoding.UTF8.GetString(stream.ToArray()), ct).ConfigureAwait(false);
        if (root is not { } answer) return new(null, $"the service at {_service.BaseUrl} has no `{Door}` door ({status}) — is it older than this driver?");
        if (!ok) return new(null, Text(answer, "error") ?? payload);

        var session = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("session", out var record) ? record : default;
        var id = Text(session, "id");
        if (id is null) return new(null, $"the service at {_service.BaseUrl} answered `{Door}` with no record.");

        // The door the record opened by, for the session log (LOG1b): a helper's record is a chat that serves another session.
        _service.RecordOpened(new SessionOpened(id, HelperPass.OpenedKind, Text(session, "adapter") ?? adapter, Text(session, "repository") ?? "")
        {
            Workspace = Text(session, "workspace"),
        });
        return new(id, Text(answer, "message") ?? "");
    }

    /// <summary>
    /// Hand a helper's answer to the session it serves (<c>POST /api/helpers/{helper}/hand</c>): the host keeps <paramref name="text"/>
    /// on its record as a word marked as the helper's, once, which reopens it at the driver's next look. A refusal is an
    /// answer, the host's sentence, not an exception, and so is a host older than the door.
    /// </summary>
    /// <param name="running">
    /// COWORK1d: this driver runs the working asker and hands an urgent ask's answer for its run's next step, kept on its running record;
    /// a host from before refuses it as running, and the answer waits for the asker's end, as before.
    /// </param>
    /// <param name="level">The ask's level, kept beside the word: <c>urgent</c> where the ask was, else none.</param>
    public async Task<(bool Handed, string Message, string? Word)> HandAsync(
        string helper, string text, CancellationToken ct = default, bool running = false, string? level = null)
    {
        var door = $"/api/helpers/{Uri.EscapeDataString(helper)}/hand";
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("text", text);
            if (running) writer.WriteBoolean("running", true);
            if (level is { Length: > 0 }) writer.WriteString("level", level);
            writer.WriteEndObject();
        }

        var (ok, status, payload, root) = await _service.PostJsonAsync(door, System.Text.Encoding.UTF8.GetString(stream.ToArray()), ct).ConfigureAwait(false);
        if (root is not { } answer) return (false, $"the service at {_service.BaseUrl} has no `{door}` door ({status}) — is it older than this driver?", null);
        if (!ok) return (false, Text(answer, "error") ?? payload, null);

        var word = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("said", out var said) ? Text(said, "id") : null;
        return (true, Text(answer, "message") ?? "", word);
    }
}
