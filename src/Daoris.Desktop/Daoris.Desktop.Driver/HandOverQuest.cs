using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>What the roster says of an agent a quest's session may go on with: its accounts and the wire its door rides (MIXAGENT1a-ii).</summary>
/// <param name="Accounts">The accounts the roster holds for the agent; none is the tool's own sign-in.</param>
/// <param name="Wire">The door it drives by: the protocol door hands the quest's connector itself, the pipe door only in an adopted repository (D70).</param>
public sealed record HandOverAgent(IReadOnlyList<string> Accounts, SessionWire Wire);

/// <summary>
/// A quest handed to another agent (MIXAGENT1a-ii, D163 point 1), as this machine holds it: the agent and account its next start
/// runs on, the session it went on from, why, by whom, and when.
/// </summary>
/// <param name="Agent">The adapter every start of the quest runs on from now, until the quest leaves this machine.</param>
/// <param name="Account">The account its next start asks for first, or null for the roster's walk.</param>
/// <param name="From">The session that was stopped to hand the quest over: the one stop the planner reads as released.</param>
/// <param name="Why">One of <see cref="HandOverWhy"/>.</param>
/// <param name="AskedBy">One of <see cref="HandOverBy"/>.</param>
public sealed record HandedQuest(string Agent, string? Account, string From, string Why, string AskedBy, DateTimeOffset At);

/// <summary>
/// The quests held on another agent (MIXAGENT1a-ii, D163): <c>&lt;home&gt;/handed/&lt;quest&gt;.json</c>, written atomically. It is the quest's
/// own override of the machine's adapter, held until the quest leaves this machine (done, declined, taken elsewhere, cleared or
/// abandoned), never for one start; and it is the release of the stop that handed it over: the planner reads a stop whose session it
/// names as released, so no strike counts and driver.json's <c>released</c> is never written.
/// </summary>
/// <remarks><b>This machine's, never the record's.</b> A file that does not read is no override: the quest runs on the machine's agent.</remarks>
public sealed partial class HandedQuests(string home)
{
    /// <summary>Where a quest's file is kept, or null for an id that is no quest's name (never a path out of the folder).</summary>
    public string? PathOf(string quest) => Named(quest) ? Path.Combine(home, "handed", quest + ".json") : null;

    /// <summary>Keep the quest as handed, replacing any before. An id that is no quest's name keeps nothing.</summary>
    /// <exception cref="IOException">It could not be written: the asker is told, rather than told it was kept.</exception>
    public void Write(string quest, HandedQuest handed)
    {
        if (PathOf(quest) is not { } path) return;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("agent", handed.Agent);
            if (handed.Account is { } account) writer.WriteString("account", account); else writer.WriteNull("account");
            writer.WriteString("from", handed.From);
            writer.WriteString("why", handed.Why);
            writer.WriteString("askedBy", handed.AskedBy);
            writer.WriteString("at", handed.At.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }

        // LF on every platform, as every file under the home is written.
        AtomicFile.WriteText(path, Encoding.UTF8.GetString(buffer.ToArray()).Replace("\r\n", "\n") + "\n");
    }

    /// <summary>The quest as handed, or null for none, a file that does not read, or an id that is no quest's name.</summary>
    public HandedQuest? Read(string quest)
    {
        if (PathOf(quest) is not { } path) return null;

        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || Text(root, "agent") is not { Length: > 0 } agent
                || Text(root, "from") is not { Length: > 0 } from
                || Text(root, "why") is not { } why || !HandOverWhy.Known(why)
                || Text(root, "askedBy") is not { } by || !HandOverBy.Known(by)
                || !DateTimeOffset.TryParse(Text(root, "at"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
            {
                return null;
            }

            return new HandedQuest(agent, Text(root, "account") is { Length: > 0 } account ? account : null, from, why, by, at);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Every quest held on another agent, by quest id: what a look reads into its snapshot.</summary>
    public IReadOnlyDictionary<string, HandedQuest> All()
    {
        var all = new Dictionary<string, HandedQuest>(StringComparer.OrdinalIgnoreCase);
        var folder = Path.Combine(home, "handed");
        if (!Directory.Exists(folder)) return all;
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
            {
                var quest = Path.GetFileNameWithoutExtension(file);
                if (Read(quest) is { } handed) all[quest] = handed;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // What did not list is read at the next look.
        }

        return all;
    }

    /// <summary>Let the quest go back to the machine's agent: it left this machine, or was cleared. Nothing to clear is no failure.</summary>
    public void Clear(string quest)
    {
        if (PathOf(quest) is not { } path) return;
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A file left behind is dropped at the next look, which reads the quest's state again.
        }
    }

    private static bool Named(string quest) => NamePattern().IsMatch(quest ?? "");

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex NamePattern();

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

/// <summary>
/// *Hand a quest's session over* (MIXAGENT1a-ii, D163 point 1; the mixed-agents design §2): the quest's own session goes on with
/// another agent when its account's allowance runs low. This is the ask's judge for a driven session with a quest, beside
/// <see cref="HandOver"/>'s for a conversation: it refuses what cannot be handed over by a code before anything is kept, and keeps the
/// same <see cref="HandOverBox"/> the driver's next look takes.
/// </summary>
/// <remarks>
/// <b>A driven session works its quest in one turn</b>, so there is no turn's end to wait for: the look stops it, as the person's stop,
/// and the quest is carried on in the same tree on the named agent. The judge is the look's too, run again as the box is taken.
/// </remarks>
public static class QuestHandOver
{
    /// <summary>Whether a record is a quest's own session: driven, with a quest, never an intake's or a teammate's.</summary>
    public static bool IsQuestSession(SessionRecord record) =>
        record is { Kind: not "chat", Quest: not null, Ask: null, Teammate: false };

    /// <summary>Judge and keep the box for a quest's session of this machine's.</summary>
    public static async Task<HandOverAnswer> AskAsync(
        ServiceClient service, string home, Func<string, HandOverAgent?> drives, string id, string agent, string? account,
        string why, string askedBy, DateTimeOffset now, CancellationToken ct = default)
    {
        var (refused, _) = await JudgeAsync(service, drives, id, agent, account, ct).ConfigureAwait(false);
        if (refused is not null) return refused;

        var box = new HandOverBox(home);
        if (box.Read(id) is { } waiting)
        {
            return HandOver.Refused(HandOver.Waiting, $"{id} is already waiting to be handed over to {waiting.Agent}.");
        }

        try
        {
            box.Write(id, new HandOverRequest(agent, string.IsNullOrWhiteSpace(account) ? null : account.Trim(), why, askedBy, now));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new HandOverAnswer(false, null, $"your ask could not be kept: {error.Message}");
        }

        return new HandOverAnswer(true, null, $"it is stopped at the driver's next look, then goes on in the same tree with {agent}.");
    }

    /// <summary>
    /// What can be known of a quest's hand-over before anything is kept or taken: the refusal by its code, or the record as the
    /// ledger holds it. Judged again as the look takes the box, since the quest, its take or the roster may have changed meanwhile.
    /// </summary>
    public static async Task<(HandOverAnswer? Refused, PriorSession? Prior)> JudgeAsync(
        ServiceClient service, Func<string, HandOverAgent?> drives, string id, string agent, string? account, CancellationToken ct = default)
    {
        var json = await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        var record = SessionRecords.Parse(json).FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal));
        if (record is null) return (HandOver.Refused(WordsNever.NotFound, $"no session here is {id}."), null);

        var last = WordsNever.LastHere(json, record.Quest);
        if (HandOver.Never(record, id, last) is { } never) return (never, null);
        if (!IsQuestSession(record)) return (HandOver.Refused(HandOver.NotConversation, $"{id} is not a quest's session."), null);

        var quest = record.Quest!;
        switch (WordsNever.Judge(record, last))
        {
            case WordsNever.StoodDown:
                return (HandOver.Refused(HandOver.QuestElsewhere,
                    $"it stood down: #{quest} is someone else's, so there is nothing of this machine's to hand over."), null);
            case WordsNever.Superseded:
                return (HandOver.Refused(HandOver.NotLastRun, $"#{quest} went on in a later session here, {last}: hand that one over."), null);
        }

        if (record.State == "awaiting-person")
        {
            return (HandOver.Refused(HandOver.Parked, $"{id} is waiting on you: answer it instead."), null);
        }

        var roster = drives(agent);
        if (roster is null) return (HandOver.Refused(HandOver.UnknownAgent, $"no agent here is called {agent}."), null);

        var named = string.IsNullOrWhiteSpace(account) ? null : account.Trim();
        if (named is not null && !roster.Accounts.Contains(named, StringComparer.OrdinalIgnoreCase))
        {
            return (HandOver.Refused(HandOver.NotItsAccount, $"{named} is not an account of {agent}."), null);
        }

        var prior = ServiceClient.ReadRecord(json, id)!;
        if (string.Equals(prior.Adapter, agent, StringComparison.OrdinalIgnoreCase)
            && string.Equals(prior.Profile ?? "", named ?? "", StringComparison.OrdinalIgnoreCase))
        {
            return (HandOver.Refused(HandOver.SameAgent, named is null ? $"{id} already runs on {agent}." : $"{id} already runs on {agent} as {named}."), null);
        }

        var found = await service.FindQuestAsync(quest, ct).ConfigureAwait(false);
        if (found is not { Status: "Open" or "Taken" })
        {
            return (HandOver.Refused(HandOver.QuestClosed, $"#{quest} is not open or taken any more, so there is no work left to hand over."), null);
        }

        if (found.Status == "Taken")
        {
            var take = ServiceClient.ReadTakes(json, new Dictionary<string, string> { [quest] = await service.ClaimAsync(quest, ct).ConfigureAwait(false) })[quest];
            if (Planner.TakenAway(found, prior, take))
            {
                return (HandOver.Refused(HandOver.QuestElsewhere,
                    $"#{quest} is not taken by this machine, so its work is not this machine's to hand over."), null);
            }
        }

        var repository = (await service.RegistryAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(each => string.Equals(each.Repository, record.Repository, StringComparison.OrdinalIgnoreCase));
        if (repository is null || (!repository.Adopted && (repository.Root is null || roster.Wire != SessionWire.Acp)))
        {
            return (HandOver.Refused(HandOver.NoDoor, repository is null
                ? $"`{record.Repository}` is not registered on this machine, so there is nowhere to go on."
                : $"{agent} drives only repositories that have adopted Daoris, and `{record.Repository}` has not: "
                  + "a session there would have no connector to take the quest with."), null);
        }

        return (null, prior);
    }
}
