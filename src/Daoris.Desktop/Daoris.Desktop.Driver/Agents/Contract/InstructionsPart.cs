namespace Daoris.Driver;

/// <summary>
/// What a door's program reads of a working tree by itself (AGENTPROVIDER1 row d): whether it loads the tree's skill folders, so the
/// standing block names none of them, and how many bytes of the native instruction file it reads, so the block carries the
/// repository's rules where the program would cut them. <see cref="None"/> loads no skill and cuts no file.
/// </summary>
public sealed record InstructionsPart(bool LoadsTreeSkills, int? FileLimit)
{
    public static InstructionsPart None { get; } = new(false, null);
}
