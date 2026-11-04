namespace Daoris.Driver;

/// <summary>
/// What the look's stages after the snapshot reach of the driver beyond <see cref="ILookWiring"/>: its context and the classes that run
/// the hand-overs, the second opinions, the helpers, the partners, the brainstorms and the meetings, whose types sit above Work orders.
/// </summary>
internal interface ILookStagesWiring : ILookWiring
{
    /// <summary>What the loop shares: the channel, the reopens' marks, the trees and the browser among them.</summary>
    LoopContext Context { get; }

    /// <summary>The set-ups' tabs shown to the person, or null where no shell carries a browser.</summary>
    ReviewDesk? Reviews { get; }

    DriverHandOvers HandOvers { get; }

    OpinionGateLook Gate { get; }

    DriverHelpers Helpers { get; }

    DriverPartners Partners { get; }

    DriverBrainstorms Brainstorms { get; }

    DriverMeetings Meetings { get; }
}
