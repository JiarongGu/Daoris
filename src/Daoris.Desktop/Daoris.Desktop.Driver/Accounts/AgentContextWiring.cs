using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class AgentContextOperations : IAgentContext
{
    [ModuleInitializer]
    internal static void Install() => AgentContext.Current = new AgentContextOperations();

    public TextWriter OpenTranscript(string path, bool append) => TranscriptFile.Open(path, append);
    public ComposedInstruction Account(SessionTarget target, IReadOnlyList<TargetPrompt.Piece> pieces) => InstructionAccounting.Account(target, pieces);
    public ComposedInstruction Intake(SessionTarget target, string prompt) => InstructionAccounting.Intake(target, prompt);
    public string Parts(SessionTarget target) => SessionPartsText.Of(target);
}
