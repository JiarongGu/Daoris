using Lyntai.Inference;

namespace Daoris.Knowledge;

/// <summary>
/// The one place a vector call's verdict is read. The cognition sibling's 3.x contract reports
/// failure as a verdict beside empty vectors rather than by throwing (its D153) — built for routers
/// advancing to the next candidate. This service routes over a single configured backend, so a
/// non-Ok verdict is terminal here, and it joins the failure path the callers already have: an
/// embedding exception means "the semantic tier did not answer", and the lexical half carries on.
/// </summary>
internal static class Embedding
{
    public static async Task<IReadOnlyList<float[]>> EmbedAsync(
        IVectorProvider embedder, IReadOnlyList<string> texts, EmbeddingRole role, CancellationToken ct)
    {
        var response = await embedder
            .CallAsync(new VectorRequest(texts, role, Consumer: "daoris"), ct)
            .ConfigureAwait(false);

        if (!response.IsOk)
        {
            throw new InvalidOperationException(
                $"embedding failed ({response.Verdict}): {response.Detail ?? "the backend gave no detail"}");
        }

        return response.Vectors;
    }
}
