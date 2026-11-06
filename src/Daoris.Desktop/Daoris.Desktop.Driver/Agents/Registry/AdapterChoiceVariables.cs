namespace Daoris.Driver;

/// <summary>
/// The variables a start's asked choice reads from its spawn's environment (row n): a door's spawn says them of its own program.
/// </summary>
internal static class AdapterChoiceVariables
{
    public static SpawnChoiceVariables ChoiceVariables(this ISessionAdapter adapter) => LegacyDoor.Of(adapter).ChoiceVariables;
}
