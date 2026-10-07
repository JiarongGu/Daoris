using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// How each agent's accounts are used, the page's `bridge/accounts.ts` (TOOL4g; D125 §2.4, §3.7, §6; D130 §3.2, §9,
/// §16.6): each scope's list and how it is used, which account its next start would take and why (TOOL6e), each account's
/// cool-off or when it was offered again, what its agent last said and its learned week, and Daoris's sessions running on
/// it (<c>ACCOUNTS</c>); and the edits Settings → Agents makes (<c>ACCOUNT_USE</c>) —
/// a list written whole, how it is used, <i>Try now</i>, and a workspace returned to this machine's accounts.
/// </summary>
/// <remarks>
/// <para><b>The files are the terminal's</b> (D50): <c>harnesses.json</c>'s lists and settings, which
/// <c>daoris agent profile order|use</c> write, and <c>cooling.json</c>, which <c>profile ready</c> ends. Each edit asks
/// the terminal's own rules first (<see cref="HarnessSettings.OrderProblem"/>, <see cref="ScopeProblem.Of"/>), refuses in
/// a code the page translates, and writes nothing then.</para>
/// <para><b>Read, never probed</b>: no answer here starts a process, so the page may ask at every tick. Nothing reaches
/// an HTTP route (D47 §4): profile names ride this bridge only.</para>
/// </remarks>
public sealed partial class DriverModule
{
    [DriverRoute("ACCOUNTS")]
    private async Task<object?> AccountsAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var running = await RunningAsync(cancellationToken).ConfigureAwait(false);
        return AccountsAnswer(_loop.Harnesses, running, _loop.Harnesses.Clock());
    }

    /// <summary>
    /// Every agent with accounts this build knows, by its accounts' owner (AGT7): whether its sessions say how near their
    /// limits are, the tool's own sign-in's cool-off, each account's facts, and each scope — the machine's first, then each
    /// workspace that names a default or a list of its own.
    /// </summary>
    /// <param name="running">Daoris's sessions running now per <c>owner/account</c>, lower case; null where none were read.</param>
    public static object AccountsAnswer(HarnessRoster roster, IReadOnlyDictionary<string, int>? running, DateTimeOffset now)
    {
        var settings = roster.Settings;
        var cooling = AccountCooling.Read(roster.Home, now);
        // When an account was offered again (TOOL6e): a cool-off that ended within the day, while the file still holds it.
        var offered = Safe(() => AccountCooling.Offered(roster.Home, now)) ?? [];
        var doors = new List<(string Owner, string Door, HarnessToolchain Toolchain)>();
        foreach (var name in roster.Adapters.Names)
        {
            if (Known(roster, name) is { } toolchain) doors.Add((toolchain.Owner(name), name, toolchain));
        }

        return new
        {
            Agents = doors
                .GroupBy(door => door.Owner, StringComparer.OrdinalIgnoreCase)
                .OrderBy(owner => owner.Key, StringComparer.Ordinal)
                .Select(owner =>
                {
                    var agent = owner.Key;
                    var fixedWeek = owner.Any(door => door.Toolchain.WeekFixed);
                    var said = new Dictionary<string, AccountSaid?>(StringComparer.OrdinalIgnoreCase);
                    var accounts = Accounts(roster.Home, agent);
                    foreach (var account in accounts) said[account] = Safe(() => AccountWindows.SaidOf(roster.Home, agent, account, now));
                    // The person's name for each account (ACCT2), by its id; the lists below name ids.
                    var names = Safe(() => AccountNames.Of(roster.Home, agent)) ?? new Dictionary<string, string>();
                    return new
                    {
                        Agent = agent,
                        // D130 §6: switching before the limit reads the agent's own word, which a door carries only where its
                        // table reads it (TOOL6c), or the agent's own server answers where it does not (CODEXUSE1). A door's
                        // readings are its owner's.
                        Speaks = owner.Any(door => roster.Speaks(door.Door)),
                        Own = new
                        {
                            Cooling = CoolingShown(cooling.FirstOrDefault(entry => Same(entry.Agent, agent) && entry.Account is null)),
                            Offered = offered.FirstOrDefault(entry => Same(entry.Agent, agent) && entry.Account is null)?.Until,
                        },
                        Accounts = accounts.Select(account => new
                        {
                            Name = account,
                            DisplayName = names.GetValueOrDefault(account),
                            Cooling = CoolingShown(cooling.FirstOrDefault(entry => Same(entry.Agent, agent) && Same(entry.Account, account))),
                            Offered = offered.FirstOrDefault(entry => Same(entry.Agent, agent) && Same(entry.Account, account))?.Until,
                            Said = SaidShown(said[account]),
                            // The weekly reset known for it: told by a limit, or by the agent's own word (TOOL6b, TOOL6c).
                            Week = Safe(() => AccountWindows.WeekOf(roster.Home, agent, account, now, fixedWeek)),
                            Running = running is null ? (int?)null : running.GetValueOrDefault(Key(agent, account)),
                        }).ToArray(),
                        Scopes = ScopesOf(settings, agent)
                            .Select(scope => ScopeShown(scope.Workspace, scope.Scope, scope.Keep, said, NextOf(roster, agent, scope.Workspace)))
                            .ToArray(),
                    };
                })
                .ToArray(),
        };
    }

    /// <summary>The machine's scope, then each workspace's that names a default or a list of its own, by name.</summary>
    private static IEnumerable<(string? Workspace, RotationScope Scope, string? Keep)> ScopesOf(HarnessSettings settings, string agent)
    {
        yield return (null, settings.ResolveScope(agent, null), RotationUse.Read(settings.Uses.GetValueOrDefault(agent)).Use.Keep);

        var workspaces = settings.Workspaces.Keys.Concat(settings.WorkspaceRotation.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal);
        foreach (var workspace in workspaces)
        {
            var scope = settings.ResolveScope(agent, workspace);
            if (scope.From != ChoiceFrom.Workspace) continue;
            var entry = settings.WorkspaceUses.TryGetValue(workspace, out var uses) ? uses.GetValueOrDefault(agent) : null;
            yield return (workspace, scope, RotationUse.Read(entry).Use.Keep);
        }
    }

    /// <summary>
    /// Which account the scope's next driven start would take, why, and what holds the others (TOOL6e): the roster's own
    /// walk, asked without probing or counting (<see cref="HarnessRoster.Next"/>). A file that cannot be read says nothing.
    /// </summary>
    private static object? NextOf(HarnessRoster roster, string agent, string? workspace)
    {
        var next = Safe(() => roster.Next(agent, workspace));
        return next is null ? null : new
        {
            next.Account,
            Reason = Code(next.Reason.ToString()),
            next.Over,
            next.When,
            Others = next.Others.Select(held => new { held.Account, Hold = Code(held.Hold.ToString()), held.Until }).ToArray(),
        };

        static string Code(string name) => JsonNamingPolicy.CamelCase.ConvertName(name);
    }

    private static object ScopeShown(
        string? workspace, RotationScope scope, string? keep, IReadOnlyDictionary<string, AccountSaid?> said, object? next) => new
    {
        Workspace = workspace,
        scope.Default,
        scope.List,
        scope.Begins,
        Use = new { scope.Use.Use, scope.Use.Keep, scope.Use.Early, scope.Use.Near },
        scope.Unknown,
        // A file edited by hand that breaks the rule is read with the list winning (D130 §3.1), and the screen names it.
        Problem = ScopeProblem.Of(scope.Default, scope.List, keep) is { } problem
            ? new { Kind = JsonNamingPolicy.CamelCase.ConvertName(problem.Kind.ToString()), problem.Account }
            : null,
        // Near by the scope's own *near* (D130 §6 as TOOL6c reads it): its agent's word, credits, or a window at or over it.
        Near = scope.List
            .Select(account => (Account: account, Near: AccountReadings.NearWindow(said.GetValueOrDefault(account), scope.Use.Near)))
            .Where(each => each.Near is not null)
            .Select(each => new { each.Account, each.Near!.Value.Window.Window, By = JsonNamingPolicy.CamelCase.ConvertName(each.Near.Value.By.ToString()) })
            .ToArray(),
        // Which account its next start would take, and why (TOOL6e, D130 §3–§4).
        Next = next,
    };

    private static object? CoolingShown(CoolingEntry? entry) => entry is null ? null : new
    {
        entry.Until, entry.Stated, entry.Window, entry.Seen, entry.AssumedZone, entry.NotBelieved,
    };

    private static object? SaidShown(AccountSaid? said) => said is null ? null : new
    {
        said.Seen,
        Windows = said.Windows.Select(window => new
        {
            window.Window, window.Used, window.Reset, window.Standing, window.Credits, window.Seen,
        }).ToArray(),
    };

    /// <summary>The owner's accounts on this machine, the directories that exist; none where its name is not one.</summary>
    private static IReadOnlyList<string> Accounts(string home, string agent)
    {
        try
        {
            return HarnessSettings.Profiles(home, agent);
        }
        catch (DriverException)
        {
            return [];
        }
    }

    /// <summary>
    /// Daoris's sessions running now on each account (D130 §4.2): the live records on this machine that name the account,
    /// conversations and Ask Daoris included, a door's counted for its owner (AGT7). A teammate's record is another
    /// machine's. Null where the loop's service is not up or did not answer: unknown, never zero.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, int>?> RunningAsync(CancellationToken cancellationToken)
    {
        if (_loop.Service is not { } service) return null;
        try
        {
            var json = await SessionRecords.ReadAsync(
                service.BaseUrl, Environment.GetEnvironmentVariable(ServiceClient.KeyVariable), ct: cancellationToken).ConfigureAwait(false);
            return RunningOn(json, adapter => Known(_loop.Harnesses, adapter)?.Owner(adapter) ?? adapter);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or IOException or JsonException
            or TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>The live records' count per <c>owner/account</c>, lower case, from the service's answer.</summary>
    public static IReadOnlyDictionary<string, int> RunningOn(string json, Func<string, string> ownerOf)
    {
        var counted = new Dictionary<string, int>(StringComparer.Ordinal);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return counted;
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (session.ValueKind != JsonValueKind.Object) continue;
            var id = Text(session, "id");
            var adapter = Text(session, "adapter");
            var profile = Text(session, "profile");
            if (id is null || adapter is null || profile is null) continue;
            var record = new SessionRecord(id, "", Text(session, "state") ?? "");
            if (record.Teammate || !record.Live) continue;
            var key = Key(ownerOf(adapter), profile);
            counted[key] = counted.GetValueOrDefault(key) + 1;
        }

        return counted;

        static string? Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
                ? text
                : null;
    }

    private static string Key(string agent, string account) => $"{agent}/{account}".ToLowerInvariant();

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static HarnessToolchain? Known(HarnessRoster roster, string adapter)
    {
        try
        {
            return roster.Toolchain(adapter);
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>A file read that may fail: nothing read is nothing known, as the driver reads these files (D21).</summary>
    private static T? Safe<T>(Func<T?> read)
    {
        try
        {
            return read();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return default;
        }
    }

    // ---------------------------------------------------------------- the edits

    /// <summary>
    /// The screen's edits to how an agent's accounts are used (TOOL4g), each the terminal's door's twin: <c>order</c> a list
    /// written whole (<c>daoris agent profile order</c>, none clearing it), <c>use</c> how it is used (<c>profile use</c>),
    /// <c>ready</c> a cool-off ended early (<c>profile ready</c>, <i>Try now</i>), and <c>inherit</c> a workspace returned
    /// to this machine's accounts: its default, list and settings cleared at once (D130 §3.2).
    /// </summary>
    /// <remarks>
    /// The loop is asked to look after each, since each changes what the next start runs on: an account offered again
    /// should take a waiting start now, not at the next poll.
    /// </remarks>
    [DriverRoute("ACCOUNT_USE")]
    private object? AccountUse(IpcRequest request)
    {
        var action = PayloadHelper.GetRequiredValue<string>(request.Payload, "action");
        var harness = PayloadHelper.GetRequiredValue<string>(request.Payload, "harness");
        var workspace = Optional(request, "workspace");
        var owner = OwnerOf(harness);

        switch (action)
        {
            case "order":
                OrderEdited(_loop.Harnesses.Settings, HarnessSettings.Profiles(_loop.Harnesses.Home, owner), owner, InOrder(request, "accounts"), workspace)
                    .Save(_loop.Harnesses.SettingsPath);
                break;
            case "use":
                UseEdited(_loop.Harnesses.Settings, owner, Flag(request, "clear") ? null : UseChangeOf(request), workspace)
                    .Save(_loop.Harnesses.SettingsPath);
                break;
            case "inherit":
                var circle = workspace ?? throw Refusals.Because(
                    Refusals.AccountActionUnknown, "returning to this machine's accounts names the workspace that returns.",
                    ("action", action));
                _loop.Harnesses.Settings.WithWorkspaceDefault(circle, owner, null).WithRotation(owner, null, circle)
                    .Save(_loop.Harnesses.SettingsPath);
                break;
            case "ready":
                var own = Flag(request, "own");
                var account = own ? null : Named(request);
                var ended = _loop.Harnesses.Ready(harness, account);
                _loop.Nudge();
                return new { Harness = harness, Action = action, Profile = account, Ended = ended };
            default:
                throw Refusals.Because(
                    Refusals.AccountActionUnknown,
                    $"unknown account action '{action}' — one of: order, use, ready, inherit",
                    ("action", action));
        }

        _loop.Nudge();
        return new { Harness = harness, Action = action, Workspace = workspace };
    }

    /// <summary>A door's accounts are its owner's (AGT7); a door with no toolchain has no accounts to edit.</summary>
    private string OwnerOf(string harness) =>
        (_loop.Harnesses.Toolchain(harness)
            ?? throw new DriverException($"Daoris manages no toolchain for `{harness}` — its accounts are its own tooling's."))
        .Owner(harness);

    /// <summary>
    /// A scope's list written whole (D125 §3.1, D130 §3.1, §4.6), as <c>daoris agent profile order</c> writes it: each name an
    /// account here, once; and the list holding the scope's default and its kept account, with another beside the kept one.
    /// None clears the list, its settings with it.
    /// </summary>
    /// <param name="accounts">The owner's accounts on this machine, <see cref="HarnessSettings.Profiles"/>.</param>
    public static HarnessSettings OrderEdited(
        HarnessSettings settings, IReadOnlyCollection<string> accounts, string owner, IReadOnlyList<string> order, string? workspace)
    {
        IReadOnlyList<string> list = [.. order.Select(name => name.Trim()).Where(name => name.Length > 0)];
        if (HarnessSettings.OrderProblem(accounts, list) is { } problem)
        {
            throw problem.Twice
                ? Refusals.Because(Refusals.AccountOrderTwice, problem.Sentence(owner, accounts), ("account", problem.Account))
                : Refusals.Because(
                    Refusals.AccountOrderUnknown, problem.Sentence(owner, accounts),
                    ("agent", owner), ("account", problem.Account), ("accounts", accounts.Count > 0 ? string.Join(", ", accounts) : "—"));
        }

        if (list.Count > 0 && ScopeProblem.Of(OwnDefault(settings, owner, workspace), list, OwnKeep(settings, owner, workspace)) is { } bound)
        {
            throw ScopeRefusal(bound, owner, workspace);
        }

        return settings.WithRotation(owner, list, workspace);
    }

    /// <summary>
    /// How a scope's list is used (D130 §16.6), as <c>daoris agent profile use</c> writes it: each choice as made, refused where
    /// the scope has no list of its own, a value this build does not know, or a kept account outside the list or alone in it.
    /// Null clears the scope back to today's defaults.
    /// </summary>
    public static HarnessSettings UseEdited(HarnessSettings settings, string owner, UseChange? change, string? workspace)
    {
        if (change is null) return settings.WithUse(owner, null, workspace);

        var list = OwnList(settings, owner, workspace);
        if (list.Count == 0)
        {
            var where = workspace is null ? "this machine" : $"`{workspace}`";
            throw Refusals.Because(
                Refusals.AccountUseNoList,
                $"{where} has no list of its own for `{owner}`, so there is nothing to use — turn on Use for its accounts first.",
                Scoped(workspace, ("agent", owner)));
        }

        if (change.Use is { } mode && !RotationUse.Modes.Contains(mode, StringComparer.Ordinal))
        {
            throw Refusals.Because(Refusals.AccountUseValue, $"`{mode}` is not a way to use accounts — goal or order.", ("setting", "use"), ("value", mode));
        }

        if (change.Near is { } near && near is < RotationUse.NearLowest or > RotationUse.NearHighest)
        {
            throw Refusals.Because(
                Refusals.AccountUseValue,
                $"near is a whole percent from {RotationUse.NearLowest} to {RotationUse.NearHighest}, not {near}.",
                ("setting", "near"), ("value", near.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (change.Keep?.Trim() is { Length: > 0 } keep && ScopeProblem.Of(null, list, keep) is { } problem)
        {
            throw ScopeRefusal(problem, owner, workspace);
        }

        return settings.WithUse(owner, change, workspace);
    }

    /// <summary>
    /// Whether a scope's default may name <paramref name="profile"/> (D130 §3.1, as TOOL6a's terminal refuses it): within its
    /// own list, where it has one. A clear, and a scope with no list of its own, take any account. What <c>profile-default</c>
    /// and Ask Daoris's <c>default</c> door ask before they write.
    /// </summary>
    /// <exception cref="ShenoraException">The scope's list does not hold the account.</exception>
    public static void DefaultAllowed(HarnessSettings settings, string owner, string? profile, string? workspace)
    {
        if (profile is not { Length: > 0 }) return;
        var list = OwnList(settings, owner, workspace);
        if (ScopeProblem.Of(profile, list, null) is not { Kind: ScopeProblemKind.Default }) return;

        var where = workspace is null ? "on this machine" : $"in `{workspace}`";
        throw Refusals.Because(
            Refusals.AccountDefaultOutsideList,
            $"`{owner}`'s list {where} is {string.Join(", then ", list)}, and `{profile}` is not in it — the list is every account "
            + "its starts may run on, and the default is where they begin within it. Turn on Use for it first, or make one of "
            + "the list the default.",
            Scoped(workspace, ("agent", owner), ("account", profile), ("list", string.Join(", ", list))));
    }

    /// <summary>A scope's own list: the machine's, or the workspace's own, never the machine's standing in for it.</summary>
    private static IReadOnlyList<string> OwnList(HarnessSettings settings, string owner, string? workspace) =>
        workspace is null
            ? settings.Rotation.GetValueOrDefault(owner) ?? []
            : settings.WorkspaceRotation.TryGetValue(workspace, out var lists) ? lists.GetValueOrDefault(owner) ?? [] : [];

    private static string? OwnDefault(HarnessSettings settings, string owner, string? workspace) =>
        (workspace is null
            ? settings.Defaults.GetValueOrDefault(owner)
            : settings.Workspaces.TryGetValue(workspace, out var defaults) ? defaults.GetValueOrDefault(owner) : null) is { Length: > 0 } named
            ? named.Trim()
            : null;

    /// <summary>A scope's kept account as its settings name it, whether or not its list holds it.</summary>
    private static string? OwnKeep(HarnessSettings settings, string owner, string? workspace) =>
        RotationUse.Read(workspace is null
            ? settings.Uses.GetValueOrDefault(owner)
            : settings.WorkspaceUses.TryGetValue(workspace, out var uses) ? uses.GetValueOrDefault(owner) : null).Use.Keep;

    /// <summary>What binds a scope, refused in a code the page translates; the sentence is the fallback, naming both sides.</summary>
    private static ShenoraException ScopeRefusal(ScopeProblem problem, string owner, string? workspace)
    {
        var where = workspace is null ? "this machine's" : $"`{workspace}`'s";
        return problem.Kind switch
        {
            ScopeProblemKind.Default => Refusals.Because(
                Refusals.AccountScopeDefault,
                $"{where} default for `{owner}` is `{problem.Account}`, and the list would not hold it — the default is where starts "
                + "begin within the list. Make one of the list the default first.",
                Scoped(workspace, ("agent", owner), ("account", problem.Account))),
            ScopeProblemKind.Keep => Refusals.Because(
                Refusals.AccountScopeKeep,
                $"`{problem.Account}` is kept for conversations, and the list does not hold it — the kept account is one of the list.",
                ("account", problem.Account)),
            _ => Refusals.Because(
                Refusals.AccountScopeAlone,
                $"`{problem.Account}` is kept for conversations, and the list would hold no other account, so driven work would have none.",
                ("account", problem.Account)),
        };
    }

    /// <summary>A refusal's values with the workspace and the catalogue's context where a workspace is named.</summary>
    private static (string Key, string Value)[] Scoped(string? workspace, params (string Key, string Value)[] values) =>
        workspace is null ? values : [.. values, ("workspace", workspace), ("context", "workspace")];

    /// <summary>The settings a <c>use</c> names: a field left out is no change; <c>noKeep</c> keeps none.</summary>
    private static UseChange UseChangeOf(IpcRequest request)
    {
        bool? early = request.Payload is { } payload && payload.TryGetProperty("early", out var flag)
            ? flag.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null }
            : null;
        var near = Number(request, "near");
        return new UseChange(
            Use: Optional(request, "use"),
            Keep: Optional(request, "keep"),
            NoKeep: Flag(request, "noKeep"),
            Early: early,
            Near: near is null ? null : (int)Math.Clamp(near.Value, int.MinValue, int.MaxValue));
    }

    /// <summary>The account names the page sent, in order; a value that is not text is skipped.</summary>
    private static IReadOnlyList<string> InOrder(IpcRequest request, string name) =>
        request.Payload is { } payload && payload.TryGetProperty(name, out var names) && names.ValueKind == JsonValueKind.Array
            ? [.. names.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)]
            : [];
}
