namespace Daoris.Driver;

/// <summary>Who a supervisor's word on a node is from: the role, and the decision that released it, which the pass asks whether it is live in this run.</summary>
public sealed record NodeWordBy(string Role, string Decision);
