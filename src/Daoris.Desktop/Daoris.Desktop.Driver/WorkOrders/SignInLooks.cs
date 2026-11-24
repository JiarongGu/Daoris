namespace Daoris.Driver;

/// <summary>The watch of the accounts' stored sign-ins, armed before the look plans or begins any start, so the writes a start's first refresh makes are said.</summary>
[LookPass("credential-writes", LookPhase.Before)]
internal sealed class CredentialWritesLook(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct)
    {
        wiring.Harnesses.Writes ??= new CredentialWrites(wiring.Harnesses, wiring.Service.AccountSaid);
        wiring.Harnesses.Writes.Follow();
        return Task.CompletedTask;
    }
}

/// <summary>The look's read of the stored sign-ins (<see cref="StoredSignIns"/>), before the sign-outs it may make are claimed.</summary>
[LookPass("stored-sign-ins", LookPhase.After, "plugin-commands")]
internal sealed class StoredSignInsLook(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct)
    {
        var kept = StoredSignIns.Read(wiring.Harnesses);
        if (kept > 0) round.Events.Add($"{kept} account(s) read signed out: their agent cleared the stored sign-in.");

        var removed = StoredLeftovers.Sweep(wiring.Harnesses);
        if (removed > 0) round.Events.Add($"{removed} leftover file(s) of unfinished sign-in writes removed.");
        return Task.CompletedTask;
    }
}

/// <summary>
/// The look's claim of the sign-outs nobody at Daoris made (<see cref="SignOutNotices"/>), so each is in <i>Done and failed</i>
/// once, the news keeping a dismissed one dismissed.
/// </summary>
[LookPass("sign-outs", LookPhase.After, "stored-sign-ins")]
internal sealed class SignOutsLook(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct)
    {
        var told = SignOutNotices.Claim(wiring.Home, wiring.Clock.GetUtcNow());
        if (told > 0) round.Events.Add($"{told} sign-out(s) kept in Done and failed.");
        return Task.CompletedTask;
    }
}
