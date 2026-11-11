namespace Daoris.Driver;

/// <summary>
/// What a terminal and a trial say of a plugin's interfaces that are not reached: each entry refused with its problem, each interface
/// this build does not know, and a storage's <c>forget</c> that is not run, with why.
/// </summary>
public static class PluginInterfaceLines
{
    public static IEnumerable<string> Of(PluginInterfacesDeclared interfaces)
    {
        foreach (var refusal in interfaces.Refused) yield return $"not reached: {refusal.Problem}";
        foreach (var unknown in interfaces.Unknown) yield return $"interface `{unknown}` is not one this build knows, so it is skipped.";
        if (interfaces.Storage?.ForgetDisabled is { } disabled) yield return $"storage's forget is not run: {disabled}";
    }
}
