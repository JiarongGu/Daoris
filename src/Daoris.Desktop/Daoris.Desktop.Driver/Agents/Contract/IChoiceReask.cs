namespace Daoris.Driver;

/// <summary>
/// The answer to a refused choice, a port the protocol open declares and Loop's composition implements where the executor is built (the
/// agent-provider design §2.5, row n): the open asks it where its agent refused the model asked, before any prompt, and the provider never
/// walks, chooses another model or moves an account (D205).
/// </summary>
/// <remarks>
/// A run asked for no actor choice is handed no port, and its open judges nothing, as before. The refusal is kept in the offers store before
/// the walk is asked; the answer is a later choice on the same door and account, which the transport carries on the same session after
/// setting back what the refused attempt changed, or none, and the open ends refused.
/// </remarks>
public interface IChoiceReask
{
    /// <summary>The later choice for this session where <paramref name="refusal"/> was refused, or null: none is to be tried and the open ends.</summary>
    Task<ActorAsked?> ReaskAsync(ModelRefusal refusal);
}
