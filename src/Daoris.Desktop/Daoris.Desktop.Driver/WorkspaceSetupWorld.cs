using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The world a workspace plan reads on this machine (WSSETUP6): the single press's world (<see cref="SetupWorld"/>), the
/// registry's declarations, the session records and each one's conversation record, and the service client, through which
/// a plan's lines reach whoever logs them. The plan itself is <see cref="WorkspaceSetup"/>.
/// </summary>
/// <remarks>
/// <para><b>One instance is one reading.</b> The registry, the quests and the tools a child finds are read once per
/// instance, which a terminal's press and a tick each make for themselves: a press judges every repository of a workspace,
/// and asking the tools their version once per repository would spawn two programs for each.</para>
/// </remarks>
public sealed class WorkspaceSetupWorld(ServiceClient service, string home) : IWorkspaceSetupWorld
{
    private readonly SetupWorld _press = new(service, home);

    private readonly SessionEvents _records = new(Path.Combine(home, "sessions"));

    private Task<IReadOnlyList<RegistrationRow>>? _rows;

    private Task<IReadOnlyList<QuestView>>? _quests;

    private Task<SetupTools>? _tools;

    public Task<IReadOnlyList<RegistrationRow>> RegistrationsAsync(CancellationToken ct) => _rows ??= service.RegistrationsAsync(ct);

    public async Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct) =>
        [.. (await RegistrationsAsync(ct).ConfigureAwait(false)).Select(row => new RepoView(row.Repository, row.Adopted, row.Root, row.Workspace))];

    public Task<LineReading> ReadLineAsync(RepoView repository, DriverConfig config, CancellationToken ct) => _press.ReadLineAsync(repository, config, ct);

    public Task<SetupTools> ToolsAsync(CancellationToken ct) => _tools ??= _press.ToolsAsync(ct);

    public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => _quests ??= _press.QuestsAsync(ct);

    public Task<IReadOnlyList<string>> EntriesAsync(string repository, CancellationToken ct) => _press.EntriesAsync(repository, ct);

    public RepositoryDescription? Describe(string root) => _press.Describe(root);

    public Task<AskAnswer> PublishAsync(string workspace, string sentence, string to, CancellationToken ct) => _press.PublishAsync(workspace, sentence, to, ct);

    public Task<IReadOnlyDictionary<string, int>> StrikesAsync(CancellationToken ct) => service.StrikesAsync(ct);

    public Task<IReadOnlyList<SessionRun>> RunsAsync(CancellationToken ct) => service.SessionRunsAsync(ct);

    public IReadOnlyList<ToolTouch> Touched(string session) =>
        SessionEvents.IsId(session) ? ReadTouches(_records.PathOf(session)) : [];

    public void Said(SetupLine line) => service.SetupSaid(line);

    /// <summary>
    /// The files each tool call in one conversation record touched, as the record names them (D76's <c>locations</c>), each
    /// call and path once. A line that does not read is skipped: a record is read for what it can say.
    /// </summary>
    public static IReadOnlyList<ToolTouch> ReadTouches(string file)
    {
        var touches = new List<ToolTouch>();
        if (!File.Exists(file)) return touches;
        var seen = new HashSet<(string, string)>();
        try
        {
            using var reader = new StreamReader(file, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            var unnamed = 0;
            while (reader.ReadLine() is { } line)
            {
                // Most lines are words, not calls: only a line naming locations is parsed.
                if (!line.Contains("\"locations\"", StringComparison.Ordinal)) continue;
                try
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object
                        || !root.TryGetProperty("kind", out var kind) || kind.GetString() != SessionEventKind.Tool
                        || !root.TryGetProperty("locations", out var locations) || locations.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    // A call with no id cannot be told from the next one, so each is its own call (as SessionLog counts them).
                    var call = root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString()! : $"#{++unnamed}";
                    foreach (var location in locations.EnumerateArray())
                    {
                        if (location.ValueKind == JsonValueKind.String && location.GetString() is { Length: > 0 } path && seen.Add((call, path)))
                        {
                            touches.Add(new ToolTouch(call, path));
                        }
                    }
                }
                catch (JsonException)
                {
                    // Skipped: a line cut short by a crash says nothing worth guessing at.
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // What was read so far stands: an order is a judgement, and a record held open by a running session still counts.
        }

        return touches;
    }
}
