namespace Daoris.Driver;

/// <summary>
/// One kind of run a conversation takes, registered with <see cref="ExecutorAttribute"/> and built once per chat runner with its wiring:
/// a public constructor taking <see cref="IExecutorWiring"/>. The runner's door for the kind reaches it by its type, so the doors' callers
/// are unchanged by where the kind lives.
/// </summary>
/// <remarks>
/// An executor reads its wiring when it runs, never in its constructor: the runner hands itself as the wiring while it is still being made.
/// Its <see cref="IExecutorWiring.Context"/> alone is made first, so a default an executor computes as it is built may read it.
/// </remarks>
internal interface IExecutor
{
}
