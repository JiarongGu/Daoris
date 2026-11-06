namespace Daoris.Driver;

/// <summary>
/// A starting kind whose node is started on a port through the performer's attempt journal, as a plugin's step is (D247 §6 point 1): its attempts
/// name the kind as their sender, and are sent, asked after and stopped through the port its door is (<see cref="ICheckPort"/>). The other
/// starting shape is a door started once per node and tip, writing a record the next pass reads back.
/// </summary>
internal interface IJournalled : IStarts
{
    /// <summary>The declaration as the performer sends it: its kind's <c>perform</c> and bound, which no author writes.</summary>
    NodeDeclaration Performed(NodeDeclaration node);
}
