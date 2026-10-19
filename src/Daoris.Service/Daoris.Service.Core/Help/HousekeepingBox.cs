using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>
/// Ask Daoris's housekeeping, the connector's half (HELPMODE1c2a, D183's note; the Ask Daoris modes design §4.1): each tool's call
/// is a keyed request in the home's session requests (<see cref="HomeEntry.Requests"/>) for the conversation's own session, which
/// the driver's loop running that conversation takes once, judges and answers beside it; this waits for the take, then for the
/// answer, and says the answer's words.
/// </summary>
/// <remarks>
/// <para><b>This judges what only the connector can say</b>: that it speaks for a session, that the session's record is an Ask
/// Daoris conversation of this machine's (a driven session is handed the same connector), and that the call names what its
/// operation acts on. Whether the person granted the operation, whether it was handed at this start, the conversation's
/// workspace and the cleanup's own judges are the driver's, at every call, whatever the agent: an agent that takes no
/// permission rules (Codex) is held by that judgement, not by an allow-list.</para>
///
/// <para><b>A twin, the file the contract</b> (<c>.claude/knowledge/twins.md</c>): the request's fields and the answer's are the
/// driver's <c>SessionRequests</c> (<c>session</c>, <c>move</c>, <c>at</c>, <c>by</c>, <c>key</c>, <c>operation</c>,
/// <c>repository</c>, <c>branches</c>; and <c>operation</c>, <c>done</c>, <c>code</c>, <c>said</c>), each side's test holding
/// the same names. So is the withdrawal of a request nobody took (the driver's REQTAKE2): renamed aside, then removed only once
/// nothing holds it, so of a withdrawal and a take only one wins.</para>
/// </remarks>
/// <param name="home">The home the requests go under, or null when there is none: every call is then refused.</param>
public sealed partial class HousekeepingBox(string? home)
{
    /// <summary>A look at what the cleanup would remove and what it keeps, and why.</summary>
    public const string Inspect = "inspect";

    /// <summary>Removing the session and landed branches named, with their trees, that the cleanup's judges clear.</summary>
    public const string CleanUp = "clean-up";

    /// <summary>Discarding one session branch by name, unforced.</summary>
    public const string DiscardBranch = "discard-branch";

    /// <summary>The request's move in the session requests, beside a person's stop, finish, decline, say and take-back.</summary>
    public const string Move = "housekeep";

    /// <summary>Who asked, as the requests name a door: Ask Daoris's tool.</summary>
    public const string By = "help";

    /// <summary>The most branches one clean-up names: a look lists every one, and a press takes those listed.</summary>
    public const int MostBranches = 100;

    private const string AnswerSuffix = ".answer.json";

    private const string NoSession =
        "This connector speaks for no session the driver started here, so there is no Ask Daoris conversation to do housekeeping "
        + "for. Nothing was done.";

    public string? Home { get; } = home;

    /// <summary>How long the request waits for a loop to take it before it is withdrawn: the terminal door's ten seconds.</summary>
    public TimeSpan TakeWithin { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long a taken request waits for its answer: under an agent's own timeout for a tool call (a minute on some), so the
    /// agent hears this rather than a timeout.
    /// </summary>
    public TimeSpan AnswerWithin { get; init; } = TimeSpan.FromSeconds(45);

    /// <summary>How often the folder is looked at while waiting.</summary>
    public TimeSpan Poll { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>The home the driver names on the connector it offers, else the account's (D63): where the session requests live.</summary>
    public static HousekeepingBox FromEnvironment() => new(RuleProposalBox.FromEnvironment().Home);

    /// <summary>
    /// Ask the loop running the conversation for one housekeeping operation, and answer the sentence the agent reads: the loop's
    /// answer, or why there is none.
    /// </summary>
    /// <param name="asker">The record of the session the connector names, or null when it names none or none is held.</param>
    /// <param name="named">The session the connector names, said back where no record is held for it.</param>
    /// <param name="operation">One of <see cref="Inspect"/>, <see cref="CleanUp"/>, <see cref="DiscardBranch"/>.</param>
    /// <param name="repository">The repository by its registered name; none for a look at every one of the conversation's.</param>
    /// <param name="branches">The branches by name: those a clean-up takes, the one a discard takes, none for a look.</param>
    public async Task<string> AskAsync(
        Session? asker, string? named, string operation, string? repository, IReadOnlyList<string>? branches, DateTimeOffset at,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(named)) return NoSession;
        if (asker is not { Origin: null } || !SessionLedger.IsHelp(asker) || !SessionId().IsMatch(asker.Id))
        {
            return $"Housekeeping is Ask Daoris's alone, and session `{named.Trim()}` is no Ask Daoris conversation of this machine's. "
                   + "Nothing was done.";
        }

        var target = string.IsNullOrWhiteSpace(repository) ? null : repository.Trim();
        var names = (branches ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim())
            .Distinct(StringComparer.Ordinal).ToList();
        if (Unfit(operation, target, names) is { } unfit) return $"{unfit} Nothing was done.";
        if (Home is null) return $"{Capital(DaorisHome.Sentence)} Nothing was done.";

        var key = Guid.NewGuid().ToString("N")[..12];
        var request = DaorisHome.Entry(Home, HomeEntry.Requests, $"{asker.Id}.{key}.json");
        var answer = DaorisHome.Entry(Home, HomeEntry.Requests, $"{asker.Id}.{key}{AnswerSuffix}");
        Directory.CreateDirectory(Path.GetDirectoryName(request)!);
        AtomicFile.WriteText(request, Request(asker.Id, key, operation, target, operation == Inspect ? [] : names, at));

        try
        {
            if (!await TakenAsync(request, ct).ConfigureAwait(false) && Withdraw(request))
            {
                return $"No Daoris on this machine took it within {Seconds(TakeWithin)} seconds: the loop that runs this conversation "
                       + "did not answer. Nothing was done.";
            }
        }
        catch (OperationCanceledException)
        {
            Withdraw(request);
            throw;
        }

        return await AnswerAsync(answer, ct).ConfigureAwait(false)
               ?? $"Daoris took it and did not answer within {Seconds(AnswerWithin)} seconds, so it may still be under way. Look with "
               + "`housekeeping_inspect` before you ask for it again.";
    }

    /// <summary>What the operation needs and the call does not name, or null where it names it.</summary>
    private static string? Unfit(string operation, string? repository, IReadOnlyList<string> names)
    {
        if (operation is not (Inspect or CleanUp or DiscardBranch)) return $"`{operation}` is no housekeeping operation.";
        if (names.FirstOrDefault(name => name.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))) is { } spaced)
        {
            return $"A branch is named as git names it, one word, and `{spaced}` is not.";
        }

        return operation switch
        {
            CleanUp when repository is null => "A clean-up names the repository whose branches go, by its registered name.",
            CleanUp when names.Count == 0 => "A clean-up names each branch that goes, as your look listed it as cleared.",
            CleanUp when names.Count > MostBranches => $"A clean-up names at most {MostBranches} branches, and this names {names.Count}.",
            DiscardBranch when repository is null => "A discard names the repository that holds the branch, by its registered name.",
            DiscardBranch when names.Count != 1 => "A discard names one session branch, as git names it.",
            _ => null,
        };
    }

    /// <summary>Whether a loop took the request (its name is gone) before <see cref="TakeWithin"/>.</summary>
    private async Task<bool> TakenAsync(string request, CancellationToken ct)
    {
        var until = DateTimeOffset.UtcNow + TakeWithin;
        while (File.Exists(request))
        {
            if (DateTimeOffset.UtcNow >= until) return false;
            await Task.Delay(Poll, ct).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>The loop's answer's words, read once and removed; null where none came within <see cref="AnswerWithin"/>.</summary>
    private async Task<string?> AnswerAsync(string path, CancellationToken ct)
    {
        var until = DateTimeOffset.UtcNow + AnswerWithin;
        while (true)
        {
            if (Read(path) is { } text)
            {
                for (var attempt = 0; attempt < 10 && !Remove(path); attempt++) await Task.Delay(10, ct).ConfigureAwait(false);
                return Said(text);
            }

            if (DateTimeOffset.UtcNow >= until) return null;
            await Task.Delay(Poll, ct).ConfigureAwait(false);
        }
    }

    /// <summary>The words an answer says, or a sentence saying it did not read as one.</summary>
    internal static string Said(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("said", out var said) && said.ValueKind == JsonValueKind.String)
            {
                return said.GetString()!;
            }
        }
        catch (JsonException)
        {
        }

        return "Daoris answered in a shape this connector does not read, so what was done cannot be said here. Look with "
               + "`housekeeping_inspect`.";
    }

    private static string? Read(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Held a moment by its writer's rename: read at the next look.
            return null;
        }
    }

    private static bool Remove(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Withdraw a request nobody took (the driver's REQTAKE2, this side of the twin): renamed aside as a take renames it, then
    /// opened alone and removed as it closes, which waits out a take's rename begun a moment before. False where a take won, when
    /// the loop has it and the answer is waited for.
    /// </summary>
    private static bool Withdraw(string path)
    {
        var withdrawing = $"{path}.{Guid.NewGuid().ToString("N")[..8]}.withdrawing";
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(path, withdrawing);
                break;
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                return false;
            }
            catch (Exception error) when (attempt < 20 && error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(10);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using (new FileStream(withdrawing, FileMode.Open, FileAccess.Read, FileShare.None, 1, FileOptions.DeleteOnClose))
                {
                    return true;
                }
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                return false;
            }
            catch (Exception error) when (attempt < 100 && error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(10);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Held past every try: put back for a loop to take, and so not withdrawn.
                try
                {
                    File.Move(withdrawing, path);
                }
                catch (Exception again) when (again is IOException or UnauthorizedAccessException)
                {
                }

                return false;
            }
        }
    }

    // Hand-rolled, in the twin's order, for the reason every store here is: nothing may stop working under AOT.
    private static string Request(string session, string key, string operation, string? repository, IReadOnlyList<string> branches, DateTimeOffset at)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("session", session);
            writer.WriteString("move", Move);
            writer.WriteString("at", Moments.Write(at));
            writer.WriteString("by", By);
            writer.WriteString("key", key);
            writer.WriteString("operation", operation);
            if (repository is not null) writer.WriteString("repository", repository);
            writer.WriteStartArray("branches");
            foreach (var branch in branches) writer.WriteStringValue(branch);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n";
    }

    private static string Seconds(TimeSpan span) => Math.Round(span.TotalSeconds).ToString(CultureInfo.InvariantCulture);

    private static string Capital(string sentence) =>
        sentence.Length == 0 ? sentence : char.ToUpperInvariant(sentence[0]) + sentence[1..];

    // A session id of this machine's names a file under the home; anything else names none (the driver's IsLocalId judges again).
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9-]{0,63}$")]
    private static partial Regex SessionId();
}
