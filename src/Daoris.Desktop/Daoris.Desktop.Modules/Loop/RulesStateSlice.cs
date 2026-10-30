using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// What an agent Daoris starts may do (PERM1, D72) and what agents proposed about it (PERM2, D74), a pushed slice (FOUNDATION1s part B,
/// D182 point 2): the rules as <c>RULES</c> answers them (<see cref="RulesRoutes.Answer(string)"/>), judged by the shell's loop from the
/// home's files and pushed whole when they move, so no tick, scope or focus asks a window to read them again.
/// </summary>
/// <remarks>
/// <para><b>One partition</b>, <c>rules</c>, the route's answer whole: the permissions file's path under the home rides this bridge as
/// it always did (desktop-only, D182 point 2).</para>
///
/// <para><b>Its wakes.</b> A change told to the rules: a press's (<c>RULE_ACTION</c>, <c>RULE_PROPOSAL</c>, a terminal's through the
/// instance), and a look's where it moved them; and the slice's period, which reads an edit no one told.</para>
/// </remarks>
public static class RulesStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "rules";

    /// <summary>Its one partition.</summary>
    public const string Partition = "rules";

    /// <summary>What wakes it on the kit's bus: a change told to the rules.</summary>
    public static readonly IReadOnlyList<SliceWake> WakesOn =
    [
        new(StatePublisher.Type, payload => payload is StateChanged changed && changed.Scopes.Contains(Name)),
    ];

    /// <summary>The slice the shell's loop owns, reading <paramref name="loop"/>'s home.</summary>
    public static StateSlice Of(DriverLoop loop, IEventBus bus) =>
        new(Name, bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>([new(Partition, RulesRoutes.Answer(loop.Home))]))
        {
            WakesOn = WakesOn,
            Failed = error => loop.Log?.Warn("state.slice", ("slice", Name), ("message", error.Message)),
        };
}
