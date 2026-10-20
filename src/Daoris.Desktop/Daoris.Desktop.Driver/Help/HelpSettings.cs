namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's chosen settings, the driver's half (HELPMODE1c3, D183's note; the Ask Daoris modes design §4.1): each request its
/// connector's tool wrote is judged here at the call, then the setting is changed through the edit its own door makes, judged by the
/// same checks a <c>setting</c> proposal is, with no Apply. What the loop running the conversation answers its
/// <see cref="SessionRequestWatch.Set"/> with.
/// </summary>
/// <remarks>
/// <para><b>Judged at every call, whatever the agent</b>, in housekeeping's order (<see cref="HelpHousekeeping"/>): the record named is
/// an Ask Daoris conversation's; the setting is chosen in <see cref="ConfiguredAccess"/> now, so a revoke refuses the next call, and was
/// handed at this start (its newest receipt, <see cref="HelpWired"/>), so a grant chosen since takes the conversation's next turn
/// (ACCESSSWAP1); and a language names a repository or a workspace of the conversation's own, read from its record
/// (<see cref="HelpCircle.OfRecord"/>, ASKSCOPE1c). An agent that takes no rules (Codex) is held by this, not by an allow-list.</para>
///
/// <para><b>The existing validation and writer</b>: the <c>setting</c> proposal's plan (<see cref="HelpProposals.Plan"/>'s two steps), the
/// route's own edit (<c>WithNotify</c>, <c>WithLanguage</c>, <c>WithCoolOff</c>, the timeout and the cap), made on the driver's file
/// with the writer every door uses (<see cref="DriverConfig.Save"/>: the file read again under its lock, the edit alone made onto it).
/// The file is this home's (<see cref="DriverConfig.ResolvePath(string)"/>), as a chat going on finds it; the loop's watch on that file
/// hears the change and looks again, as it does for a terminal's edit.</para>
/// </remarks>
/// <param name="service">The loop's service once it answers; null before, when every request is refused saying so.</param>
public sealed class HelpSettings(string home, Func<ServiceClient?> service)
{
    /// <summary>Judge one request and change the setting where it holds; a refusal is an answer, never thrown.</summary>
    public async Task<SettingAnswer> AnswerAsync(SessionRequest request, CancellationToken ct)
    {
        var setting = request.Operation ?? "";
        if (!ConfiguredAccess.SettingsBuilt.Contains(setting))
        {
            return SettingAnswer.Refusal(setting, SettingAnswer.Named, $"`{setting}` is no setting Ask Daoris may be granted.");
        }

        if (service() is not { } ledger)
        {
            return SettingAnswer.Refusal(setting, SettingAnswer.NoService, "Daoris's service is not answering yet, so the conversation's record could not be read.");
        }

        var record = (await ledger.SessionRecordsAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(each => string.Equals(each.Id, request.Session, StringComparison.Ordinal));
        if (record is not { Repository: HelpRoom.Repository })
        {
            return SettingAnswer.Refusal(setting, SettingAnswer.NotHelp,
                $"Its settings are Ask Daoris's alone, and session `{request.Session}` is no Ask Daoris conversation of this machine's.");
        }

        var tool = ConfiguredAccess.SettingToolOf(setting);
        if (!ConfiguredAccess.Read(home).Settings.Contains(setting))
        {
            return SettingAnswer.Refusal(setting, SettingAnswer.NotGranted,
                $"The person has not granted `{tool}` under Ask Daoris's configured access, so it is not yours to change: propose it with "
                + "`setting_propose` for their Apply, or say where they change it.");
        }

        if (!(new HelpWired(home).Read(request.Session).FirstOrDefault()?.Settings ?? []).Contains(setting))
        {
            return SettingAnswer.Refusal(setting, SettingAnswer.NotHanded,
                $"The person granted `{tool}` after this conversation's turn began, and a grant holds from the turn after it: it is yours "
                + "at this conversation's next turn.");
        }

        var registry = await ledger.RegistryAsync(ct).ConfigureAwait(false);
        var facts = new HelpMachineFacts(
            [.. registry.Select(row => row.Repository)],
            [.. registry.Select(row => RemoteTarget.Workspace(row.Workspace)).Append(RemoteTarget.DefaultWorkspace).Distinct(StringComparer.OrdinalIgnoreCase)],
            [])
        {
            Circle = HelpCircle.OfRecord(home, record),
            Registered = [.. registry.Select(row => (row.Repository, (string?)row.Workspace, row.Root))],
        };

        var value = setting == ConfiguredAccess.Language && string.Equals(request.Value?.Trim(), "clear", StringComparison.OrdinalIgnoreCase)
            ? "--clear"
            : request.Value?.Trim() ?? "";
        var proposal = new HelpProposal(
            "", "setting", setting, request.Repository?.Trim(), request.Workspace?.Trim(), value, null, "", request.Session, "proposed");
        // The conversation's workspace first, then the setting's own checks, as a proposal of it is judged (HelpProposals.Plan).
        var kind = new HelpSettingProposals();
        if (HelpProposalScope.Refusal(kind.Named(proposal, facts), facts) is { } outside)
        {
            return SettingAnswer.Refusal(setting, SettingAnswer.Outside, $"{Capital(outside.TrimEnd('.'))}.");
        }

        var path = DriverConfig.ResolvePath(home);
        var plan = kind.Plan(proposal, DriverConfig.Load(path), facts);
        if (plan.Refusal is { } refused) return SettingAnswer.Refusal(setting, SettingAnswer.Refused, $"{Capital(refused.TrimEnd('.'))}.");

        // The writer every door uses: loaded now, edited, saved onto the file as it stands under its lock.
        plan.Apply!(DriverConfig.Load(path)).Save(path);
        return new SettingAnswer(setting, true, null,
            $"Set, under the person's grant and with no Apply: {plan.Describe.TrimEnd('.')}. The same as `{plan.Terminal}`.");
    }

    private static string Capital(string sentence) => sentence.Length == 0 ? sentence : char.ToUpperInvariant(sentence[0]) + sentence[1..];
}
