namespace Daoris.Driver;

/// <summary>What the terminal's listing says of a row its run's waits placed: each step and what it waits for, then the verb that opens the run.</summary>
internal static class RunWaitWords
{
    public static IEnumerable<string> Of(SessionGrouping row)
    {
        if (row.Waits is not { Count: > 0 } waits) yield break;
        foreach (var wait in waits) yield return $"run {wait.Run} {What(wait.Why)} at `{wait.Node}`";
        yield return $"daoris workflow run --session {row.Session}";
    }

    private static string What(string why) => why switch
    {
        PersonWaits.Accept => "waits for your Accept",
        PersonWaits.Approval => "waits for your approval",
        PersonWaits.Task => "waits for your Done",
        PersonWaits.GoAhead => "waits for your go-ahead",
        PersonWaits.Held => "is held for your word",
        PersonWaits.Choose => "waits for your choice of the files changed on both sides",
        _ => "waits for you",
    };
}
