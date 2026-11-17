namespace Daoris.Assistant.Tests;

/// <summary>The reader's walk's contract on the scripted fake.</summary>
public sealed class ScriptedReaderContractTests : ReaderWalkContract
{
    private readonly ScriptedReader _reader = new();

    protected override IReaderWalk Backend => _reader;

    protected override void Arrange(ReaderState state) => _reader.Answer(state switch
    {
        ReaderState.Kept => new ReaderAnswer(state, "the reader kept its reading") { Reading = "nothing beyond the person's consent" },
        ReaderState.Ended => new ReaderAnswer(state, "the reader's holder ended before it answered"),
        _ => new ReaderAnswer(state, "the reading waits behind another"),
    });
}
