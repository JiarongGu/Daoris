namespace Daoris.Driver;

/// <summary>
/// One kind of work a workspace declares, as a repository in it follows it (WORKFLOW1i-b; the workflow design §4.3).
/// </summary>
/// <param name="Workflow">What a task of the kind follows there, as said: a workflow's id and version, or Current.</param>
/// <param name="Lowers">
/// What it asks less of the person than the repository's workflow with no kind does, each said as what it does
/// (<see cref="Involvement.Lowers"/>); empty where it lowers nothing, and null where its steps could not be read here.
/// </param>
public sealed record IntakeKindLine(string Kind, string? Label, IReadOnlyList<string> Paths, string Workflow, IReadOnlyList<string>? Lowers);

/// <summary>A repository's kinds: the workflow it follows with none, and each declared kind's against it.</summary>
public sealed record IntakeRepositoryKinds(string Repository, string Otherwise, IReadOnlyList<IntakeKindLine> Kinds);
