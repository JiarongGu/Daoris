namespace Daoris.Driver;

public sealed record CheckVerdict(CheckAsk Ask, CheckState State)
{
    public string? Name => Ask.Gate ?? (Ask.Command is { } command ? HeavyRun.CommandText(command) : null);

    public static IReadOnlyList<CheckVerdict> Pending(string home)
    {
        var files = new CheckFiles(home);
        return [.. files.Asks()
            .Select(ask => (Ask: ask, State: files.ReadState(ask.Id)))
            .Where(each => each.State is { Handed: null } state && CheckStates.Finished(state.State))
            .Select(each => new CheckVerdict(each.Ask, each.State!))];
    }
}
