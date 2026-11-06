using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class AgentContextOperations : IAgentContext
{
    [ModuleInitializer]
    internal static void Install() => AgentContext.Current = new AgentContextOperations();

    public TextWriter OpenTranscript(string path, bool append) => TranscriptFile.Open(path, append);
    public ComposedInstruction Account(SessionTarget target, IReadOnlyList<TargetPrompt.Piece> pieces, StandingBlockTold? block) =>
        InstructionAccounting.Account(target, pieces, block);
    public ComposedInstruction Intake(SessionTarget target, string prompt, StandingBlockTold? block) => InstructionAccounting.Intake(target, prompt, block);
    public string Parts(SessionTarget target) => SessionPartsText.Of(target);
    public StandingBlockTold Block(StandingFacts facts, int before) => StandingBlock.Fitted(facts, before);
    public StandingFacts OnHarness(StandingFacts facts, AgentDoor adapter, string tree) => StandingBlocks.OnHarness(facts, adapter, tree);
}
