using Daoris.Driver;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// What waits on the person or on a role across this machine's sessions, behind the sessions doors: a quiet session's row, the requests
/// and third answers held for the person, the supervisor's decisions and the steward's desk, each read from the driver's memory or files.
/// </summary>
internal sealed class SessionsWaitDesk(DriverContext context)
{
    /// <summary>A quiet row's press refused: the session brings no quiet row, is an intake, or ended first.</summary>
    internal const string QuietNot = "not-quiet", QuietNotQuest = "not-quest", QuietGone = "gone";

    private readonly DriverLoop _loop = context.Loop;

    public Task<object?> QuietLeaveAsync(SessionQuietLeave press)
    {
        var id = press.Id;
        var left = SessionWatch.Of(_loop.Events).Leave(id);
        return Task.FromResult<object?>(left
            ? new { Left = true, Why = (string?)null, Message = "Daoris looks in again once it is heard from." }
            : new { Left = false, Why = (string?)QuietNot, Message = $"{id} is not quiet now: nothing was changed." });
    }

    public Task<object?> QuietCarryOnAsync(SessionQuietCarryOn press)
    {
        var id = press.Id;
        var watch = SessionWatch.Of(_loop.Events);
        object Refused(string why, string message) => new { Stopped = false, Why = (string?)why, Message = message };
        if (watch.RowOf(id) is not { Busy: false } row) return Task.FromResult<object?>(Refused(QuietNot, $"{id} is not quiet now, or something it runs keeps the machine busy: nothing was changed."));
        if (row is not { Kind: SessionWatch.Quest, Quest: { } quest }) return Task.FromResult<object?>(Refused(QuietNotQuest, $"{id} is an intake: it is stopped, never carried on."));

        StandingChoices.Write(_loop.ConfigPath, config => config.WithReleased(quest, id));
        if (!_loop.Processes.Stop(id, note: Observation.QuietPressed(row.Minutes)))
        {
            return Task.FromResult<object?>(Refused(QuietGone, $"{id}'s run had already ended; its quest is released for the next look."));
        }

        watch.Pressed(id);
        _loop.Nudge();
        return Task.FromResult<object?>(new { Stopped = true, Why = (string?)null, Message = "Its run ends, and the work goes on in the same tree at the next look." });
    }

    public Task<object?> PermissionWaitsAsync() =>
        Task.FromResult<object?>(new { Waits = AttentionStateSlice.PermissionWaits(HeldPermissions.Of(_loop.Events)) });

    public Task<object?> PermissionAnswerAsync(SessionPermissionAnswer press)
    {
        var answer = HeldPermissions.Of(_loop.Events).Answer(press.Session, press.Request, press.Option);
        return Task.FromResult<object?>(new { answer.Answered, answer.Why, answer.Message });
    }

    public Task<object?> AnswerWaitsAsync() =>
        Task.FromResult<object?>(new { Waits = AttentionStateSlice.AnswerWaits(_loop.Home) });

    public Task<object?> AnswerAloneAsync(SessionAnswerAlone press)
    {
        var (session, goOn) = press;
        var marks = new GoOnMarks(_loop.Home);
        var answer = goOn
            ? AnswersAloneWord.GoOn(marks, _loop.Events, session)
            : AnswersAloneWord.NotNow(marks, _loop.Events, session, DateTimeOffset.UtcNow);
        if (answer.Stands && goOn)
        {
            // A conversation this shell holds says it, or goes on with it where it ended; a driven record goes on at the look.
            _loop.Chat?.AnswerGoesOn(session);
            _loop.Nudge();
        }

        return Task.FromResult<object?>(new { Answered = answer.Stands, answer.Why, answer.Message });
    }

    public async Task<object?> SupervisorAsync(SessionSupervisor press, CancellationToken cancellationToken)
    {
        // The line's counts and the rules' acts ride beside the decisions: none where the service is not reached (the counts read unknown then).
        var facts = _loop.Service is { } service
            ? await LineCountsRead.FactsAsync(service, _loop.Home, press.Id, cancellationToken).ConfigureAwait(false)
            : LineCountsRead.FactsOf(_loop.Home, press.Id, null);
        return new
        {
            Decisions = _loop.Supervisor.History(press.Id).Select(SupervisorDecisionRow).ToList(),
            Courses = _loop.Supervisor.CoursesOf(press.Id).Select(SupervisorCourseRow).ToList(),
            Landings = _loop.Supervisor.LandingsOf(press.Id).Select(SupervisorLandingRow).ToList(),
            Allows = _loop.Supervisor.AllowsOf(press.Id).Select(SupervisorAllowRow).ToList(),
            Counts = SessionCountsWire.Counts(facts),
            Acts = SessionCountsWire.Acts(facts),
        };
    }

    /// <summary>One decision as the page reads it: never its receipt, its ticket nor its word's text.</summary>
    internal static object SupervisorDecisionRow(SupervisorDecisionView view) => new
    {
        view.Id,
        Kind = SupervisorKinds.Question,
        Question = new { view.Question.Id, Text = view.Question.Question, Choices = view.Question.Choices.Select(choice => choice.Label).ToList() },
        view.Choice,
        view.Bound,
        Reasons = view.Reasons.Select(reason => new { reason.Says, reason.Source }).ToList(),
        Holder = new { view.Holder.Kind, view.Holder.Parent },
        State = SupervisorStates.Spell(view.State),
        view.Why,
        KeptAt = Moments.Write(view.KeptAt),
        GoesOnAt = Moments.Write(view.GoesOnAt),
        StateAt = Moments.Write(view.StateAt),
        view.Earlier,
    };

    /// <summary>One allow decision as the page reads it: which subject it answered, the act and where it stands, no sentence.</summary>
    internal static object SupervisorAllowRow(SupervisorAllowView view) => new
    {
        view.Id,
        Kind = SupervisorKinds.Allow,
        view.Shape,
        view.Subject,
        view.Repository,
        view.Do,
        view.Role,
        view.Act,
        view.Bound,
        Reasons = view.Reasons.Select(reason => new { reason.Says, reason.Source }).ToList(),
        Holder = new { view.Holder.Kind, view.Holder.Parent },
        State = SupervisorStates.Spell(view.State),
        view.Why,
        KeptAt = Moments.Write(view.KeptAt),
        GoesOnAt = Moments.Write(view.GoesOnAt),
        StateAt = Moments.Write(view.StateAt),
    };

    /// <summary>One landing decision as the page reads it: the step and attempt, the act and where it stands, no sentence.</summary>
    internal static object SupervisorLandingRow(SupervisorLandingView view) => new
    {
        view.Id,
        Kind = SupervisorKinds.Landing,
        view.Run,
        view.Node,
        view.Attempt,
        view.Do,
        view.Work,
        Holder = new { view.Holder.Kind, view.Holder.Parent },
        State = SupervisorStates.Spell(view.State),
        view.Why,
        KeptAt = Moments.Write(view.KeptAt),
        GoesOnAt = Moments.Write(view.GoesOnAt),
        StateAt = Moments.Write(view.StateAt),
        view.Earlier,
    };

    /// <summary>One course decision as the page reads it: the point's count, value, rule and place, the act and where it stands, no sentence.</summary>
    internal static object SupervisorCourseRow(SupervisorCourseView view) => new
    {
        view.Id,
        view.Kind,
        view.Point,
        view.Count,
        view.N,
        view.Rule,
        view.Place,
        view.Do,
        Holder = new { view.Holder.Kind, view.Holder.Parent },
        State = SupervisorStates.Spell(view.State),
        view.Why,
        KeptAt = Moments.Write(view.KeptAt),
        GoesOnAt = Moments.Write(view.GoesOnAt),
        StateAt = Moments.Write(view.StateAt),
        view.Earlier,
    };

    public async Task<object?> OverruleAsync(SupervisorOverrule press, CancellationToken cancellationToken)
    {
        var (id, decision) = press;
        var service = _loop.Service ?? throw NotReady();
        var state = await _loop.Supervisor.OverruleAsync(
            id, decision,
            async (session, word, token) => (await service.TakeBackAsync(session, word, ct: token).ConfigureAwait(false)).Done,
            DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        if (state == SupervisorState.Overruled) _loop.Nudge();
        return new { Done = state == SupervisorState.Overruled, State = state is { } moved ? SupervisorStates.Spell(moved) : null };
    }

    public async Task<object?> RoleActAsync(RoleActPress press, CancellationToken cancellationToken) =>
        await SupervisorDoor.ActAsync(_loop.Pipe, press.Form.Json, cancellationToken, context.OwnerScopesOf(), context.StewardWorldOf()).ConfigureAwait(false);

    public object? StewardActs() => _loop.Pipe?.StandingConfirmation is { } judge
        ? new { Generation = judge.LiveGeneration(AppointedRoles.Steward), Acts = judge.StewardActsNow() }
        : new { Generation = (string?)null, Acts = (IReadOnlyList<StewardActNow>)[] };

    public object? StewardRequest(DeskRequestPress press)
    {
        var filed = _loop.Pipe?.StandingConfirmation is { } judge
            ? judge.FileRequest(press.Form.Json, PipeDoor.Client)
            : new DeskFiled(false, null, DeskWhy.NotStaffed);
        return new { filed.Filed, Request = filed.Id, filed.Why };
    }

    public object? StewardDesk() => new
    {
        Requests = _loop.Pipe?.StandingConfirmation is { } judge ? judge.Desk.Now().Select(DeskRow).ToList() : new List<object>(),
    };

    /// <summary>One desk request as the verb and the page read it: its moments written as every route writes them.</summary>
    internal static object DeskRow(DeskRequestNow each) => new
    {
        each.Id, each.Act, each.Target, each.Why, Filed = Moments.Write(each.Filed), each.State, Closed = Moments.Write(each.Closed),
        each.AnsweredBy, each.Reason,
    };
}
