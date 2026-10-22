using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// A driven session's question to the person (PARKVIEW1, the park's ask design §2): one file per session under the home's sessions
/// folder, <c>&lt;session&gt;.question.json</c>, read as the connector's <c>question_ask</c> writes it (the service's
/// <c>QuestionBox</c>). The file is the contract; the two share no code, and each side's tests hold the same shape.
/// </summary>
/// <remarks>
/// <para><b>Bound to its park here, at the run's conclusion</b> (<see cref="SettleRun"/>): kept only where the run parks and the
/// question was asked since the run last handed its agent words, deleted otherwise. So a question on file while its record is parked
/// is its last turn's: a run that parks again without asking shows none from an earlier park or an earlier turn, and a run that ended
/// any other way leaves none. The connector writes during the run and this settles after it, so the two never write at once.</para>
///
/// <para>Machine-local, as the hand-over ask's files are: no wire carries it, and it goes with the session's history
/// (<see cref="SessionHomeFiles"/>).</para>
/// </remarks>
public sealed class QuestionFiles(string home)
{
    public const string Suffix = ".question.json";

    private string PathOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + Suffix);

    /// <summary>The session's question, or null for none, a file that does not read, or an id that is not one.</summary>
    public ParkQuestion? Read(string session)
    {
        if (!SessionEventText.IsLocalId(session)) return null;
        var path = PathOf(session);
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Text(root, "id") is not { Length: > 0 } id
                || Text(root, "question") is not { Length: > 0 } question)
            {
                return null;
            }

            // An answer with no label is no answer to press; one with no words for what it does says none.
            List<ParkQuestionChoice> choices = root.TryGetProperty("choices", out var list) && list.ValueKind == JsonValueKind.Array
                ? [.. list.EnumerateArray()
                    .Select(choice => choice.ValueKind == JsonValueKind.Object && Text(choice, "label") is { Length: > 0 } label
                        ? new ParkQuestionChoice(label, Text(choice, "does") ?? "")
                        : null)
                    .OfType<ParkQuestionChoice>()]
                : [];
            int? recommend = root.TryGetProperty("recommend", out var number) && number.ValueKind == JsonValueKind.Number
                && number.TryGetInt32(out var chosen) && chosen >= 1 && chosen <= choices.Count ? chosen : null;
            return new ParkQuestion(
                id, session, question, choices, recommend, Text(root, "by") is { Length: > 0 } by ? by : null,
                Moments.Read(root, "at") ?? File.GetLastWriteTimeUtc(path));
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Bind the session's question to the park its run ended in (the design §2): kept where <paramref name="state"/> parks and the
    /// question was asked at or after <paramref name="since"/>, the run's opening; deleted otherwise. Answers whether one is kept.
    /// </summary>
    public bool Settle(string session, string state, DateTimeOffset since)
    {
        if (!SessionEventText.IsLocalId(session)) return false;
        var path = PathOf(session);
        if (!File.Exists(path)) return false;
        if (SessionStates.IsParked(state) && Read(session) is { } asked && asked.At >= since) return true;
        HandOverFiles.Delete(path);
        return false;
    }

    /// <summary>
    /// <see cref="Settle"/> at a run's conclusion: since the run's last handing of words, the later of its opening and the newest
    /// moment its record shows words handed to its agent (<see cref="SessionEvents.LastHandedAt"/>). A run that went on in the same
    /// record with the person's words (MSG1b, on either door) asked its last turn's question after them; one asked before them may
    /// have been answered by them, and goes.
    /// </summary>
    public bool SettleRun(string session, string state, DateTimeOffset openedAt, SessionEvents events) =>
        Settle(session, state, events.LastHandedAt(session) is { } handed && handed > openedAt ? handed : openedAt);
}

/// <summary>
/// A session's question as <c>question_ask</c> wrote it: its id, the session, the agent's words, the answers it offers, the one it
/// recommends by number from 1 (null for none, or one that names no answer), by when as it said, and when it asked.
/// </summary>
public sealed record ParkQuestion(
    string Id, string Session, string Question, IReadOnlyList<ParkQuestionChoice> Choices, int? Recommend, string? By, DateTimeOffset At);

/// <summary>One answer a question offers: the words on its press, and what the agent does on it.</summary>
public sealed record ParkQuestionChoice(string Label, string Does);
