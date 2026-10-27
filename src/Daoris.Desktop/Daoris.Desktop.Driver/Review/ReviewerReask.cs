namespace Daoris.Driver;

/// <summary>
/// A one-turn owner's refusal at the open, handled inside its own call, over the reviewer's choice (D205 §3.5(f), MODELROLE1e): a second
/// opinion's reviewer, a helper and a partner each walk their entries and ask <see cref="ReviewerChoice"/> again. The rule is Roles'
/// (<see cref="OneTurnReask{TChoice, TPassed}"/>); this is it on the reviewer's choice, under the name its callers use.
/// </summary>
internal sealed class OneTurnReask(ReviewerChoice first, Func<IReadOnlyList<ReviewerPassed>, Task<ReviewerChoice>> walk)
    : OneTurnReask<ReviewerChoice, ReviewerPassed>(first, walk);
