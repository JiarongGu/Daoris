namespace Daoris.Driver;

/// <summary>
/// A Daoris door bound to the layer (the agent-lyntai design §4.1): its registration, the layer door it opens (<c>cli</c> or <c>acp</c>) and
/// the run kinds the binding covers, a driven start with its resume and go-ons, and a conversation. A kind it does not cover stays the door's
/// own, Daoris-run as today.
/// </summary>
internal sealed record LayerBinding(LayerRegistration Registration, string Kind, IReadOnlySet<StartKind> Runs);
