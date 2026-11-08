using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>How an agent supervisor's attempt ended; <c>role-changed</c> spends no choice and ends no attempts: the role changed under the turn, so the subject decides again.</summary>
public static class SupervisorAttemptOutcomes
{
    /// <summary>Its block kept as a decision.</summary>
    public const string Decided = "decided";

    /// <summary>It left the subject to the person, a decision of its own and final.</summary>
    public const string Left = "left";

    /// <summary>A failed end, a timeout, a block that did not read or named another subject; closed at a start or past its bound too.</summary>
    public const string Failed = "failed";

    /// <summary>A limited or transient end: a pass of the walk, the next attempt taking the next choice.</summary>
    public const string Limited = "limited";

    public const string RoleChanged = "role-changed";
}

/// <summary>One attempt at a subject: its id, the subject, the choice of the role's order it ran on, when it started, and how it ended.</summary>
public sealed record SupervisorAttempt(string Id, SupervisorSubject Subject, int Choice, DateTimeOffset Started, string? Outcome = null);

/// <summary>
/// An agent supervisor's attempts, kept in the subject's session's decisions folder beside its decisions: each written atomically before
/// its turn starts, so a write that fails starts nothing, and its outcome beside it at its end. One attempt per choice of the role's order
/// and one at a time per subject; a subject whose attempts ended (decided, left or failed) is the person's, and no restart counts one
/// again: an attempt found with no outcome is closed failed.
/// </summary>
public sealed class SupervisorAttempts(string home)
{
    private const string AttemptSuffix = ".attempt";
    private const string OutcomeSuffix = ".outcome";

    private string FolderOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + QuestionSupervisorDecisions.DecisionsSuffix);

    /// <summary>
    /// The attempt on <paramref name="choice"/>, written before its start; null where one runs on the subject, the choice is spent, or the
    /// subject's attempts have ended.
    /// </summary>
    public SupervisorAttempt? Begin(SupervisorSubject subject, int choice, DateTimeOffset now)
    {
        if (Running(subject) || Ended(subject) || Spent(subject).Contains(choice)) return null;
        var attempt = new SupervisorAttempt("a-" + Guid.NewGuid().ToString("N")[..12], subject, choice, now);
        Directory.CreateDirectory(FolderOf(subject.Session));
        AtomicFile.WriteText(Path.Combine(FolderOf(subject.Session), attempt.Id + AttemptSuffix), Json(attempt));
        return attempt;
    }

    /// <summary>The outcome of <paramref name="attempt"/>, written beside it; a second is never written.</summary>
    public void End(SupervisorAttempt attempt, string outcome, DateTimeOffset now)
    {
        var path = Path.Combine(FolderOf(attempt.Subject.Session), attempt.Id + OutcomeSuffix);
        if (File.Exists(path)) return;
        AtomicFile.WriteText(path, JsonSerializer.Serialize(new { outcome, at = now.ToString("O", CultureInfo.InvariantCulture) }));
    }

    /// <summary>The attempts on <paramref name="subject"/> that read, oldest first.</summary>
    public IReadOnlyList<SupervisorAttempt> Of(SupervisorSubject subject) => [.. All(subject.Session).Where(each => each.Subject == subject)];

    /// <summary>Whether an attempt on <paramref name="subject"/> has no outcome yet.</summary>
    public bool Running(SupervisorSubject subject) => Of(subject).Any(each => each.Outcome is null);

    /// <summary>Whether <paramref name="subject"/>'s attempts have ended, so it is the person's.</summary>
    public bool Ended(SupervisorSubject subject) => Of(subject).Any(each => each.Outcome is SupervisorAttemptOutcomes.Decided or SupervisorAttemptOutcomes.Left or SupervisorAttemptOutcomes.Failed);

    /// <summary>The choices <paramref name="subject"/>'s attempts ran on, each once.</summary>
    public IReadOnlyList<int> Spent(SupervisorSubject subject) =>
        [.. Of(subject).Where(each => each.Outcome != SupervisorAttemptOutcomes.RoleChanged).Select(each => each.Choice).Distinct().Order()];

    /// <summary>The start's closure: every attempt with no outcome closed failed, its subject returned, since none runs after a restart.</summary>
    public IReadOnlyList<SupervisorSubject> CloseUnended(DateTimeOffset now) => Close(now, _ => true);

    /// <summary>Every attempt with no outcome started more than <paramref name="bound"/> before <paramref name="now"/>, closed failed.</summary>
    public IReadOnlyList<SupervisorSubject> CloseOverdue(DateTimeOffset now, TimeSpan bound) => Close(now, attempt => now - attempt.Started > bound);

    /// <summary>Every subject whose attempts have ended, of every session.</summary>
    public IReadOnlyList<SupervisorSubject> EndedSubjects() =>
        [.. Sessions().SelectMany(All).Select(each => each.Subject).Distinct().Where(Ended)];

    private IReadOnlyList<SupervisorSubject> Close(DateTimeOffset now, Func<SupervisorAttempt, bool> due)
    {
        var closed = new List<SupervisorSubject>();
        foreach (var attempt in Sessions().SelectMany(All).Where(each => each.Outcome is null && due(each)).ToList())
        {
            End(attempt, SupervisorAttemptOutcomes.Failed, now);
            if (!closed.Contains(attempt.Subject)) closed.Add(attempt.Subject);
        }

        return closed;
    }

    private IEnumerable<string> Sessions()
    {
        var sessions = DaorisHome.Entry(home, HomeEntry.Sessions);
        if (!Directory.Exists(sessions)) return [];
        return Directory.EnumerateDirectories(sessions, "*" + QuestionSupervisorDecisions.DecisionsSuffix)
            .Select(folder => Path.GetFileName(folder)[..^QuestionSupervisorDecisions.DecisionsSuffix.Length])
            .Where(SessionEventText.IsLocalId);
    }

    private IEnumerable<SupervisorAttempt> All(string session)
    {
        var folder = FolderOf(session);
        if (!Directory.Exists(folder)) return [];
        return Directory.EnumerateFiles(folder, "*" + AttemptSuffix).Select(Read).OfType<SupervisorAttempt>().OrderBy(each => each.Started);
    }

    private static SupervisorAttempt? Read(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (!root.TryGetProperty("kind", out var kind) || kind.GetString() is not { } named
                || !root.TryGetProperty("subject", out var wire) || SupervisorSubject.Read(named, wire) is not { } subject
                || !root.TryGetProperty("choice", out var choice) || !choice.TryGetInt32(out var number)
                || !root.TryGetProperty("started", out var started) || !started.TryGetDateTimeOffset(out var at))
            {
                return null;
            }

            var outcomePath = Path.ChangeExtension(path, OutcomeSuffix);
            string? outcome = null;
            if (File.Exists(outcomePath))
            {
                using var ended = JsonDocument.Parse(AtomicFile.ReadText(outcomePath));
                outcome = ended.RootElement.TryGetProperty("outcome", out var said) ? said.GetString() : null;
            }

            return new SupervisorAttempt(Path.GetFileNameWithoutExtension(path), subject, number, at, outcome);
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string Json(SupervisorAttempt attempt) => JsonSerializer.Serialize(new
    {
        id = attempt.Id,
        kind = attempt.Subject.Kind,
        subject = attempt.Subject.Wire(),
        choice = attempt.Choice,
        started = attempt.Started.ToString("O", CultureInfo.InvariantCulture),
    });
}
