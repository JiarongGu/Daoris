using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// Which read answers each agent's usage question (AGENTLYNTAI1c, D248's note): the question itself, today's reader, unless the driver
/// carries Lyntai's agent layer, whose provider folders bind their agent's question to the layer's read when the driver loads. The provider
/// that declares a question declares <see cref="Of"/> it, so the registry, the roster and a door lent the question all hold the one read.
/// </summary>
public static class UsageReads
{
    private static readonly ConcurrentDictionary<UsageQuestion, IUsageRead> Bound = new(ReferenceEqualityComparer.Instance);

    /// <summary>The read <paramref name="question"/> is answered by: the layer's where one is bound, else the question's own.</summary>
    public static IUsageRead Of(UsageQuestion question) => Bound.GetValueOrDefault(question) ?? question;

    /// <summary>Binds <paramref name="question"/> to <paramref name="read"/>, once, before any provider is built.</summary>
    internal static void Bind(UsageQuestion question, IUsageRead read)
    {
        if (!Bound.TryAdd(question, read)) throw new InvalidOperationException($"the usage question of '{question.Harness}' is bound twice.");
    }
}
