namespace Daoris.Driver;

/// <summary>
/// The session a new one continues, why (<see cref="HandOverWhy"/>) and who asked (<see cref="HandOverBy"/>): its record's
/// <c>from</c> link (MIXAGENT1a-iii, D163 point 4), named at the open by the driver, the half that knows. A conversation handed
/// over, a quest's session carried on after a hand-over, and one carried on onto another account after a limit say it; since
/// SESSCTX1b every carry-on and fallback says it too (<see cref="HandOverWhy.CarryOn"/>, <see cref="HandOverWhy.Retry"/>), so a
/// brief follows the line. A conversation's own going-on continues its own record and says none.
/// </summary>
public sealed record ContinuedFrom(string Session, string Why, string By);
