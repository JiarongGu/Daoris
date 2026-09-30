using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// What an agent Daoris starts may do, the page's `bridge/rules.ts` (MOD5): the rules (PERM1, D72), a
/// change to them, and the person's answer to an agent's proposal (PERM2, D74).
/// </summary>
public sealed partial class DriverModule
{
    // What an agent Daoris starts may do (PERM1, D72): the defaults with their reasons, and
    // every scope the machine's file holds — the file `daoris agent rules` edits. A path
    // under the home rides this bridge like every path here.
    [DriverRoute("RULES")]
    private async Task<object?> RulesAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return Rules(PermissionRules.Load(_loop.Home));
    }

    // The screen's half of `daoris agent rules allow|ask|deny|remove|default` (D50): an edit to
    // the same file, answered with the state after it. A refusal is the driver's own sentence.
    [DriverRoute("RULE_ACTION")]
    private async Task<object?> RuleActionAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var action = PayloadHelper.GetRequiredValue<string>(request.Payload, "action");
        var file = PermissionRules.Load(_loop.Home);
        file = action switch
        {
            "add" => PermissionRules.Add(
                file, ScopeOf(request), Optional(request, "name"),
                Optional(request, "list") switch
                {
                    "allow" => RuleList.Allow,
                    "ask" => RuleList.Ask,
                    "deny" => RuleList.Deny,
                    var other => throw new DriverException($"a rule goes in `allow`, `ask` or `deny`, not `{other}`."),
                },
                PayloadHelper.GetRequiredValue<string>(request.Payload, "rule")),
            "remove" => PermissionRules.Remove(
                file, ScopeOf(request), Optional(request, "name"),
                PayloadHelper.GetRequiredValue<string>(request.Payload, "rule")),
            "default" => PermissionRules.SwitchDefault(
                file, PayloadHelper.GetRequiredValue<string>(request.Payload, "id"),
                PayloadHelper.GetRequiredValue<bool>(request.Payload, "on")),
            _ => throw new DriverException($"unknown rule action '{action}' — one of: add, remove, default."),
        };
        PermissionRules.Save(_loop.Home, file);
        return Rules(file);
    }

    // The screen's half of `daoris agent rules accept|decline` (PERM2, D74): the person's answer
    // to an agent's proposal. 🔴 The only way a widening an agent proposed ever applies.
    [DriverRoute("RULE_PROPOSAL")]
    private async Task<object?> RuleProposalAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        RuleProposals.Answer(
            _loop.Home,
            PayloadHelper.GetRequiredValue<string>(request.Payload, "id"),
            PayloadHelper.GetRequiredValue<bool>(request.Payload, "accept"),
            Optional(request, "note"),
            DateTimeOffset.UtcNow);
        return Rules(PermissionRules.Load(_loop.Home));
    }

    /// <summary>
    /// The rules as the page reads them (PERM1). 🔴 No null on the wire where the page tells anything
    /// by it: the bridge leaves a null out, so the machine's scope simply carries no <c>name</c>, and
    /// a file read cleanly carries no <c>problem</c>.
    /// </summary>
    private object Rules(PermissionFile file) => new
    {
        Path = PermissionRules.PathOf(_loop.Home),
        file.Problem,
        Defaults = PermissionRules.Defaults.Select(shipped => new
        {
            shipped.Id,
            List = shipped.List.ToString().ToLowerInvariant(),
            shipped.Rules,
            shipped.Why,
            // The tools a hook default judges (PERM3) — null for a rule default, which the bridge omits.
            shipped.Hook,
            On = !file.DefaultsOff.Contains(shipped.Id, StringComparer.Ordinal),
        }).ToArray(),
        Scopes = new[] { (Scope: "machine", Name: (string?)null, Lists: file.Machine) }
            .Concat(file.Workspaces.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => (Scope: "workspace", Name: (string?)p.Key, Lists: p.Value)))
            .Concat(file.Repositories.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => (Scope: "repository", Name: (string?)p.Key, Lists: p.Value)))
            .Select(held => new { held.Scope, held.Name, held.Lists.Allow, held.Lists.Ask, held.Lists.Deny })
            .ToArray(),
        // What agents proposed about these rules (PERM2, D74), newest first. Structured rather than a
        // sentence, so the page says it in the person's language. 🔴 Not the folder the session ran in:
        // it is a machine path, and the page is told who proposed, never where they stood.
        Proposals = RuleProposals.Load(_loop.Home).Select(proposal => new
        {
            proposal.Id,
            State = proposal.State.ToString().ToLowerInvariant(),
            proposal.Change.Action,
            Scope = proposal.Change.Scope.ToString().ToLowerInvariant(),
            proposal.Change.Name,
            List = proposal.Change.List?.ToString().ToLowerInvariant(),
            proposal.Change.Rule,
            proposal.Change.Default,
            proposal.Change.On,
            proposal.Why,
            proposal.Session,
            proposal.Ask,
            Proposed = proposal.Proposed.ToString("O"),
            Settled = proposal.Settled?.ToString("O"),
            proposal.SettledBy,
            proposal.Note,
        }).ToArray(),
    };

    private static RuleScope ScopeOf(IpcRequest request) => Optional(request, "scope") switch
    {
        null or "machine" => RuleScope.Machine,
        "workspace" => RuleScope.Workspace,
        "repository" => RuleScope.Repository,
        var other => throw new DriverException($"a rule reaches the `machine`, a `workspace` or a `repository`, not `{other}`."),
    };
}
