namespace Daoris.Driver;

internal static class AskSender
{
    internal const string Prefix = "ask #";

    internal static string? AskOf(string? sender) =>
        sender is not null && sender.Length > Prefix.Length && sender.StartsWith(Prefix, StringComparison.Ordinal)
            ? sender[Prefix.Length..]
            : null;
}
