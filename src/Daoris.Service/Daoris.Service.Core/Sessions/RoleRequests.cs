using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>
/// One request to another role (D325, ACTORFLOW1c): what the asker needs, the role it asks where it is not the line above it, and, for an
/// act, the act (its kind, what it touches and where it lands); and each channel's own parts. <c>question_ask</c> makes it, and
/// <c>go_ahead_ask</c> and <c>helper_ask</c> make it on their channel.
/// </summary>
/// <param name="Session">The session the connector names, or null.</param>
/// <param name="Question">The question; for an act, why the work needs it and exactly what the asker would do.</param>
public sealed record RoleRequest(string? Session, string? Question)
{
    /// <summary>The role asked: <c>helper</c>, <c>credentials-keeper</c>, <c>approver</c>, <c>owner</c> or another role's id; null for the line above.</summary>
    public string? Role { get; init; }

    /// <summary>The act's kind (write, release, push, sign-in, run), where the request is for an act.</summary>
    public string? Kind { get; init; }

    /// <summary>What the act touches, in a few words: the same words join an act already asked.</summary>
    public string? Touches { get; init; }

    /// <summary>Where the act lands: the environment or system.</summary>
    public string? On { get; init; }

    public IReadOnlyList<(string? Label, string? Does)>? Choices { get; init; }

    public int? Recommend { get; init; }

    public string? By { get; init; }

    /// <summary>Why the asker recommends its answer.</summary>
    public string? Reason { get; init; }

    public string? Owner { get; init; }

    public string? Site { get; init; }

    /// <summary>The helper's agent, as Settings lists it.</summary>
    public string? Agent { get; init; }

    public bool Urgent { get; init; }

    public HelperScout? Scout { get; init; }
}

/// <summary>
/// The one door a session asks another role through (D325, ACTORFLOW1c), each request going out on the channel the role's holder takes it on,
/// and each answer coming back as the asker's next turn: the helper role's is one helper's reading (<see cref="HelperBox"/>); an act the person
/// allows, a go-ahead held on the ask (<see cref="SessionGoAheads"/>); the credentials keeper's, the person's sign-in, a question marked
/// <c>sign-in</c> at its site; any other, a question in the asker's conversation naming the role (<see cref="QuestionBox"/>), which a supervisor
/// answers where no owner mark keeps it. A role of another repository is asked as a quest to it, the transport and not a second door.
/// </summary>
public sealed class RoleRequests(QuestionBox questions, HelperBox helpers, SessionLedger? ledger)
{
    /// <summary>The role whose holder reads the asker's work once and answers.</summary>
    public const string Helper = "helper";

    /// <summary>The role that keeps the keys and sign-ins: the person, whose sign-in answers it.</summary>
    public const string CredentialsKeeper = "credentials-keeper";

    /// <summary>A role's id, as the driver's roles are: lowercase words of letters and digits joined by dashes, at most 40.</summary>
    private static readonly Regex Shape = new(@"^(?=.{1,40}\z)[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant);

    public async Task<string> AskAsync(RoleRequest request, DateTimeOffset now, CancellationToken ct = default)
    {
        var role = request.Role?.Trim() is { Length: > 0 } named ? named : null;
        if (role is not null && (role.Contains(':', StringComparison.Ordinal) || role.Contains('/', StringComparison.Ordinal)))
        {
            return $"`{role}` is a role of another repository: a request there travels as a quest to it, so publish it with `quest_publish` to that "
                   + "repository, and its answer is your next turn. Nothing was asked here.";
        }

        if (role is not null && !Shape.IsMatch(role))
        {
            return $"`{role}` is no role's id (lowercase words joined by dashes): name the role that can (`{Helper}`, `{CredentialsKeeper}`, "
                   + "`approver`, `owner`), or leave it out for the line above you. Nothing was asked.";
        }

        var act = request.Kind is not null;
        if (role == Helper)
        {
            if (act) return "A helper reads your work and answers once; it takes no act: ask the role that can take it. Nothing was asked.";
            var record = await RecordAsync(request.Session, ct).ConfigureAwait(false);
            return helpers.Ask(record, request.Session, request.Question, request.Agent, now, request.Urgent, request.Scout).Message;
        }

        if (act && role != CredentialsKeeper)
        {
            // An act the person allows: one go-ahead per act, held on the ask the work is for.
            if (ledger is null || request.Session is not { } session)
            {
                return "This connector speaks for no session the driver started, so there is no ask to hold a go-ahead on: say "
                       + "exactly what you need and why in your last message, and end your turn.";
            }

            return (await ledger.AskGoAheadAsync(session, request.Kind, request.On, request.Touches, request.Question, now, ct).ConfigureAwait(false)).Message;
        }

        var asker = await RecordAsync(request.Session, ct).ConfigureAwait(false);
        var owner = role == CredentialsKeeper && request.Owner is null ? "sign-in" : request.Owner;
        return questions.Ask(asker, request.Session, request.Question, request.Choices, request.Recommend, request.By, now, request.Reason, owner, request.Site, role)
            .Message;
    }

    private async Task<Session?> RecordAsync(string? session, CancellationToken ct) =>
        ledger is not null && session is not null ? await ledger.FindAsync(session, ct).ConfigureAwait(false) : null;
}
