using static Daoris.Driver.TraceLookups;

namespace Daoris.Driver;

/// <summary>What stood at a session's start in the chain: the standing answer, the go-aheads and the person's words so far.</summary>
internal static class TraceStoods
{
    /// <summary>
    /// What stood when a driven session on a quest started: the standing answer for its repository, each go-ahead on its ask,
    /// and how many of the person's words were said before it, each by the moments kept and nothing else.
    /// </summary>
    internal static TraceStood? Stood(TracedSession session, TraceAskRead? ask, DriverConfig config)
    {
        if (session.Kind != "driven" || session.Quest is null) return null;
        if (session.Created is not { } opened) return new TraceStood { Missing = TraceStoodGaps.NoMoment };

        TraceStanding? standing = null;
        if (!session.Teammate)
        {
            var held = config.StandingFor(session.Repository);
            standing = held is null ? new TraceStanding(session.Repository, TraceStandings.None)
                : held.At is not { } set ? new TraceStanding(session.Repository, TraceStandings.MomentUnknown)
                : set < opened ? new TraceStanding(session.Repository, TraceStandings.Before) { At = set, Says = held.Says }
                : new TraceStanding(session.Repository, TraceStandings.After) { At = set };
        }

        if (ask?.Ask is not { } read) return new TraceStood { Standing = standing };

        return new TraceStood
        {
            Standing = standing,
            GoAheads =
            [
                .. (read.GoAheads ?? []).Select(goAhead =>
                {
                    var first = goAhead.Asked.MinBy(request => request.At);
                    var mine = goAhead.Asked.Any(request => Same(request.Session, session.Id));
                    return first is null || first.At >= opened ? new TraceGoAheadStood(goAhead.Number, TraceGoAheadStands.AskedAfter) { Mine = mine }
                        : goAhead.Answer is not { } answer ? new TraceGoAheadStood(goAhead.Number, TraceGoAheadStands.Waiting) { Mine = mine }
                        : answer.At < opened
                            ? new TraceGoAheadStood(goAhead.Number, answer.Approved ? TraceGoAheadStands.ApprovedBefore : TraceGoAheadStands.RefusedBefore)
                            {
                                At = answer.At,
                                Mine = mine,
                            }
                        : new TraceGoAheadStood(goAhead.Number, TraceGoAheadStands.AnsweredAfter) { At = answer.At, Mine = mine };
                }),
            ],
            Words = read.Words is { } words ? new TraceWordsBefore(read.Id, words.Count(word => word.At < opened), words.Count) : null,
        };
    }
}
