namespace Daoris.Desktop;

/// <summary>An edit to the permission rules: <c>Action</c> is add, remove or default, and each reads the fields it names.</summary>
public sealed record RuleActionPress(string Action, string? Scope, string? Name, string? List, string? Rule, string? Id, bool? On);

/// <summary>The person's answer to an agent's proposal.</summary>
public sealed record RuleProposalPress(string Id, bool Accept, string? Note);
