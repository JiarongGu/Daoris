using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One running environment or GPU claim the machine holds for a repository: its project (the repository's key), kind and
/// name, who started it and who uses it. It lives while any user's process runs.
/// </summary>
/// <param name="Id">Unique to the lease.</param>
/// <param name="Project">The repository's key, as its heavy-run slot is keyed.</param>
/// <param name="Kind"><see cref="LoadKind.Environment"/> or <see cref="LoadKind.Gpu"/>.</param>
/// <param name="Name">What the repository calls it (its stack, <c>work-stack</c>), which agents of one repository ask by.</param>
/// <param name="Owner">The agent that started it: the one whose place it counts as.</param>
/// <param name="Users">Every agent using it, each with the process that holds its use.</param>
public sealed record LoadLease(string Id, string Project, string Kind, string Name, string? Owner, DateTimeOffset Since, IReadOnlyList<LeaseUser> Users);

/// <summary>An agent's use of a lease: the agent (a session id, or null for a terminal's) and the process that holds it.</summary>
public sealed record LeaseUser(string? Agent, int Pid);

/// <summary>What asks a lease: the repository (its key and name), the agent, the kind and name wanted, the process that will hold its use, and the repository's rule for the kind (the kind's default where null).</summary>
public sealed record LeaseRequest(string Key, string Repository, string? Agent, string Kind, string Name, int Pid, KindRule? Rule = null);

/// <summary>What asking a lease came to: the lease, whether it joined a running one (nothing to start) or started a new one; or why it waits.</summary>
public sealed record LeaseTaken(LoadLease? Lease, bool Joined, CheckWait? Wait);

/// <summary>
/// The environments and GPU claims agents of a repository share: <see cref="Ask"/> before starting one. Where the
/// repository's rule for the kind is to <c>reuse</c>, an agent asking a name that is already running joins it, starting nothing, so ten
/// agents of one repository run one environment; otherwise a new lease is admitted by the room's rule like any heavy run (the fair
/// share, the kind's bound, free memory against its learned need) and every lease counts against the machine's limit with the checks.
/// A lease whose every user's process has ended is gone. Kept in <c>admission/leases.json</c>, changed under the admission's lock.
/// </summary>
public static class LoadLeases
{
    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Locks, "admission", "leases.json");

    /// <summary>The leases that still live, the oldest first. Reads the file only.</summary>
    public static IReadOnlyList<LoadLease> Live(string home, Func<int, bool>? alive = null)
    {
        alive ??= KeepLock.Lives;
        return [.. Read(PathOf(home)).Select(lease => lease with { Users = [.. lease.Users.Where(user => alive(user.Pid))] }).Where(lease => lease.Users.Count > 0).OrderBy(lease => lease.Since)];
    }

    /// <summary>
    /// Ask for the environment or GPU claim <paramref name="request"/> names, for its agent, whose process holds its use: joined where one
    /// runs and the rule reuses, else admitted new or told to wait.
    /// </summary>
    public static LeaseTaken Ask(string home, CheckRules rules, LeaseRequest request, Func<int, bool>? alive = null)
    {
        alive ??= KeepLock.Lives;
        using var admitting = CheckAdmission.Admitting(home);
        if (admitting is null) return new LeaseTaken(null, false, CheckWait.Held);

        var rule = request.Rule ?? KindRule.Default(request.Kind);
        var leases = Live(home, alive).ToList();
        var user = new LeaseUser(request.Agent, request.Pid);
        var at = leases.FindIndex(lease => lease.Project == request.Key && lease.Kind == request.Kind && lease.Name == request.Name);
        if (rule.Reuse && at >= 0)
        {
            if (!leases[at].Users.Contains(user)) leases[at] = leases[at] with { Users = [.. leases[at].Users, user] };
            Write(home, leases);
            return new LeaseTaken(leases[at], true, null);
        }

        var ask = new HeavyAsk(request.Key, request.Repository, null, () => throw new InvalidOperationException("a lease writes no heavy-run record"))
        {
            Agent = request.Agent, Kind = request.Kind, Gate = request.Name,
        };
        var wait = CheckAdmission.Judge(home, CheckAdmission.Holding(home, (process, _) => alive(process)), rules, ask, rule, leases);
        if (wait is not null) return new LeaseTaken(null, false, wait);

        var made = new LoadLease(Guid.NewGuid().ToString("N")[..12], request.Key, request.Kind, request.Name, request.Agent, rules.World.Clock(), [user]);
        leases.Add(made);
        Write(home, leases);
        return new LeaseTaken(made, false, null);
    }

    /// <summary>
    /// Let go of an agent's use of a lease. True where it is the last: the lease is gone and the caller ends what it ran.
    /// <paramref name="observed"/> is what the environment held at its peak, if the caller read it: kept as a run's, so the next ask is judged by it.
    /// </summary>
    public static bool Release(string home, string id, LeaseUser user, RunSample? observed = null, Func<int, bool>? alive = null)
    {
        using var admitting = CheckAdmission.Admitting(home);
        var leases = Live(home, alive).ToList();
        var at = leases.FindIndex(lease => lease.Id == id);
        if (at < 0) return false;
        var lease = leases[at];
        var left = lease.Users.Where(each => each != user).ToList();
        if (left.Count == 0) leases.RemoveAt(at);
        else leases[at] = lease with { Users = left };
        Write(home, leases);
        if (observed is not null) LearnedNeeds.Record(home, LearnedNeeds.KeyOf(lease.Project, lease.Kind, lease.Name), observed);
        return left.Count == 0;
    }

    private static List<LoadLease> Read(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            return [.. document.RootElement.GetProperty("leases").EnumerateArray().Select(lease => new LoadLease(
                lease.GetProperty("id").GetString()!, lease.GetProperty("project").GetString()!, lease.GetProperty("kind").GetString()!, lease.GetProperty("name").GetString()!,
                lease.TryGetProperty("owner", out var owner) ? owner.GetString() : null,
                DateTimeOffset.Parse(lease.GetProperty("since").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                [.. lease.GetProperty("users").EnumerateArray().Select(user => new LeaseUser(user.TryGetProperty("agent", out var agent) ? agent.GetString() : null, user.GetProperty("pid").GetInt32()))]))];
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return [];
        }
    }

    private static void Write(string home, IReadOnlyList<LoadLease> leases)
    {
        var path = PathOf(home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema", 1);
            writer.WriteStartArray("leases");
            foreach (var lease in leases)
            {
                writer.WriteStartObject();
                writer.WriteString("id", lease.Id);
                writer.WriteString("project", lease.Project);
                writer.WriteString("kind", lease.Kind);
                writer.WriteString("name", lease.Name);
                if (lease.Owner is { } owner) writer.WriteString("owner", owner);
                writer.WriteString("since", MachineAdmission.Stamp(lease.Since));
                writer.WriteStartArray("users");
                foreach (var user in lease.Users)
                {
                    writer.WriteStartObject();
                    if (user.Agent is { } agent) writer.WriteString("agent", agent);
                    writer.WriteNumber("pid", user.Pid);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        AtomicFile.WriteText(path, Encoding.UTF8.GetString(buffer.ToArray()).ReplaceLineEndings("\n") + "\n");
    }
}
