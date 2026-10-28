namespace Daoris.Driver;

/// <summary>
/// What the helper's and the partner's verbs read of the terminal's ask: the form it takes, the sessions it names, its words, the agent
/// it is for and a piece's paths. The sessions verb's own <c>SessionsAsk</c> implements it directly, so the participants' verbs name none
/// of the sessions verb's types.
/// </summary>
public interface IParticipantsAsk
{
    /// <summary><c>helper</c>, <c>helper-approve</c>, <c>helper-decline</c>, <c>partner</c>, <c>partner-list</c> or <c>partner-stop</c>.</summary>
    string Verb { get; }

    /// <summary>The sessions, asks or pieces the verb names.</summary>
    IReadOnlyList<string> Ids { get; }

    /// <summary>A helper's question or a piece's words.</summary>
    string? Text { get; }

    /// <summary>The agent the ask names.</summary>
    string? Agent { get; }

    /// <summary>A piece's paths, each repository-relative as the person named it.</summary>
    IReadOnlyList<string> Paths { get; }
}

/// <summary>What the helper's and the partner's verbs touch of the terminal's world: the service, the home and how a piece's base is read.</summary>
public interface IParticipantsWorld
{
    /// <summary>The service the asker's record is read from.</summary>
    ServiceClient Service { get; }

    /// <summary>The home whose helper and partner boxes the verbs write.</summary>
    string Home { get; }

    /// <summary>How a piece's base is read from the tree it is about (a test hands a stand-in).</summary>
    Func<string, CancellationToken, Task<PieceBase>> PieceBase { get; }
}

/// <summary>Makes the terminal's ask from what a participants' verb read of the words.</summary>
public delegate TAsk ParticipantsAskMaker<out TAsk>(string verb, IReadOnlyList<string> ids, string? text, string? agent, IReadOnlyList<string> paths);
