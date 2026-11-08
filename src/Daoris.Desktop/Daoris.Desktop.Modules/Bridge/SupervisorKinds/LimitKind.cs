using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The limit kind: a point a person's <c>refer</c> rule holds a driven quest at by a limited end, the moves bound or failures in a row on
/// one choice. Its decide is a move to another choice, released as the quest's one approval in <c>role_moves</c>, the yes *Go on now with*
/// writes; or, on a limited end, a wait for the choice's reset to its rule's minutes, as a <c>wait</c> rule makes it. Either is held for the
/// role's window, with <i>Overrule</i>, and the limit's own carry and walk stay the move's way.
/// </summary>
internal sealed class LimitKind : ReferredPointKind
{
    public override string Kind => SupervisorKinds.Limit;
}
