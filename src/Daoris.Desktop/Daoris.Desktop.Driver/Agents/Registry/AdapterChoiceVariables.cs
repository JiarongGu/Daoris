namespace Daoris.Driver;

/// <summary>
/// The variables a start's asked choice reads from its spawn's environment (row n): a contract door's spawn says them of its own program;
/// any other adapter, a plugin's, keeps Claude Code's as every door had them, its program unmeasured.
/// </summary>
internal static class AdapterChoiceVariables
{
    public static SpawnChoiceVariables ChoiceVariables(this ISessionAdapter adapter) =>
        adapter is LegacyDoor door ? door.ChoiceVariables : ClaudeCodeSpawn.ChoiceVariablesOfClaude;
}
