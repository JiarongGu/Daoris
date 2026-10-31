namespace Daoris.Driver;

/// <summary>
/// A landed branch whose unit's <c>integrate</c> node waits for the owner's card (INTEGRATE1h; the integration design §17.4): ready, its request
/// not made, and the card the door that makes it. <i>What needs you</i> lists it, its door the repository's Integration tab, whose
/// <i>Request</i> is the card (the recorded-landing door, by its branch).
/// </summary>
/// <param name="Unit">The branch unit whose node waits.</param>
/// <param name="Tip">The landing's recorded tip, the node's subject.</param>
/// <param name="Code">Why its landing is no request (<see cref="IntegrationRefusals"/>).</param>
/// <param name="Said">The door's sentence for it.</param>
/// <param name="Since">When the branch was landed.</param>
public sealed record IntegrationCardWait(
    string Unit, string Repository, string Workspace, string Branch, string Tip, string Code, string Said, DateTimeOffset? Since)
{
    /// <summary>
    /// Whether a landing refused as <paramref name="code"/> waits on the owner's card: an automatic landing under <c>request: owner</c>; one under
    /// <c>steward</c>, whose act no role can press yet, so the card is its one door; and a record that says neither, unless the rule says
    /// <c>request: done</c>, where Daoris's sweep requests it. Any other refusal is no wait of the owner's.
    /// </summary>
    public static bool Waits(string? code, Landing landing) => code switch
    {
        IntegrationRefusals.AwaitsOwner or IntegrationRefusals.AwaitsSteward => true,
        IntegrationRefusals.NoRequest => landing.Rule?.Integrate?.Resolved().Request != LandingIntegrate.Done,
        _ => false,
    };
}
