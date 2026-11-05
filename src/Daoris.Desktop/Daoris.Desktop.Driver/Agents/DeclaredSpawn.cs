namespace Daoris.Driver;

/// <summary>
/// The spawn of a door a plugin declared: its command, the machine's `commands` row first, with the driver's frames on an open stdin.
/// It hands nothing at its spawn and resumes nothing natively, since its program is unmeasured.
/// </summary>
internal sealed class DeclaredSpawn(IReadOnlyList<string> command) : ISpawn
{
    public ProcessRecipe Start(SessionTarget target, IReadOnlyList<string>? given) =>
        ProcessRecipe.Of(Resolve(given)) with { Input = true };

    public ProcessRecipe Chat(ChatTarget target, IReadOnlyList<string>? given) => ProcessRecipe.Of(Resolve(given));

    private IReadOnlyList<string> Resolve(IReadOnlyList<string>? given) => given is { Count: > 0 } ? given : command;
}
