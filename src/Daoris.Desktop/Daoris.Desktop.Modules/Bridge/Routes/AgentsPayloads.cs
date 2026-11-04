using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>The roster read again: with <c>Agent</c> one agent's accounts, with <c>Profile</c> too that one account alone.</summary>
public sealed record HarnessesRefreshPress(string? Agent, string? Profile);

/// <summary>The circles whose driven start is asked for.</summary>
public sealed record StartsRead(IReadOnlyList<string>? Workspaces);

/// <summary>
/// An action on a harness. <c>Join</c> is the lists a sign-in joins as sent (a workspace's name, or null for this machine's list);
/// <c>Way</c> is the sign-in's way and <c>Switch</c> whether it switches from the one running.
/// </summary>
public sealed record HarnessActionPress(
    string Action, string Harness, string? Profile, string? Key, string? Name, string? Version, string? Way, JsonElement? Join,
    bool Switch = false);

/// <summary>The answer a running action's prompt asked for.</summary>
public sealed record HarnessInputPress(string Harness, string Action, string Text);

/// <summary>The running action to stop.</summary>
public sealed record HarnessCancelPress(string Harness, string Action);

/// <summary>
/// An account's own settings. A key sent as null clears it, a text sets it, and one left out is no change, so each is the value as
/// sent; <c>PerModel</c> is the effort per model's name, a text or null each.
/// </summary>
public sealed record AgentSettingsPress(string Harness, string? Profile, JsonElement? Model, JsonElement? Effort, JsonElement? PerModel);
